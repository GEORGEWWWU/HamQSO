using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HamJi.Core
{
    /// <summary>
    /// 本机 QSL 卡片图片（完全离线，只存在本机 files/qsl 里）。
    /// 目前只保留 1 张：<see cref="MaxCount"/> 改大即可支持多张，其余逻辑按写入时间排序通用，
    /// 超出数量的旧卡片会在导入成功后立即删除，不留垃圾文件。
    /// </summary>
    public static class Qsl
    {
        /// <summary>最多保留几张。默认 1 张，上传新的替换旧的。</summary>
        public const int MaxCount = 1;

        /// <summary>单张上限。只防误选超大原图，不做严格压缩。</summary>
        public const long MaxBytes = 50L * 1024 * 1024;

        private const string PrefName = "hamji_qsl";
        private const string PrefWarned = "warned";

        public static string DirPath
            => Path.Combine(Android.App.Application.Context.FilesDir.AbsolutePath, "qsl");

        /// <summary>已有卡片，最新的一张排在最前。</summary>
        public static IReadOnlyList<string> Paths
        {
            get
            {
                try
                {
                    if (!Directory.Exists(DirPath)) return Array.Empty<string>();
                    return Directory.GetFiles(DirPath)
                        .Where(f => !Path.GetFileName(f).StartsWith("."))
                        .OrderByDescending(SafeWriteTime)
                        .ToList();
                }
                catch { return Array.Empty<string>(); }
            }
        }

        public static bool Has => Paths.Count > 0;

        /// <summary>当前展示用的那张（没有返回 null）。</summary>
        public static string Main
        {
            get
            {
                var all = Paths;
                return all.Count > 0 ? all[0] : null;
            }
        }

        /// <summary>是否已经给用户看过「只存本机、备份不含卡片」的提示。</summary>
        public static bool Warned
        {
            get
            {
                try { return Prefs.GetBoolean(PrefWarned, false); }
                catch { return true; }        // 读不到就别反复打扰
            }
        }

        public static void MarkWarned()
        {
            try { Prefs.Edit().PutBoolean(PrefWarned, true).Apply(); } catch { }
        }

        /// <summary>
        /// 把选中的图片复制进本机目录：先写临时文件 → 校验是图片 → 落到正式文件名 → 删掉多余旧卡片。
        /// 任何一步失败都不会破坏已有卡片。
        /// </summary>
        public static bool Import(Android.Content.ContentResolver resolver, Android.Net.Uri uri, out string error)
        {
            error = null;
            if (resolver == null || uri == null) { error = "没有选中图片"; return false; }

            string tmp = null;
            try
            {
                Directory.CreateDirectory(DirPath);
                tmp = Path.Combine(DirPath, ".import-" + Guid.NewGuid().ToString("N"));

                long total = 0;
                using (var input = resolver.OpenInputStream(uri))
                {
                    if (input == null) { error = "无法读取该图片"; return false; }
                    using (var output = File.Create(tmp))
                    {
                        var buf = new byte[64 * 1024];
                        int n;
                        while ((n = input.Read(buf, 0, buf.Length)) > 0)
                        {
                            total += n;
                            if (total > MaxBytes)
                            {
                                error = "图片超过 " + (MaxBytes / 1024 / 1024) + " MB";
                                return false;
                            }
                            output.Write(buf, 0, n);
                        }
                    }
                }

                if (total == 0) { error = "图片内容为空"; return false; }
                if (!IsImage(tmp)) { error = "这不是可用的图片文件"; return false; }

                var final = Path.Combine(DirPath,
                    "qsl-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-"
                    + Guid.NewGuid().ToString("N").Substring(0, 6) + ExtOf(resolver, uri));

                File.Move(tmp, final, true);
                tmp = null;
                Trim();
                Android.Util.Log.Info("HamJiQsl", "import ok " + System.IO.Path.GetFileName(final)
                    + " " + total + "B keep=" + Paths.Count);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (tmp != null)
                {
                    try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                }
            }
        }

        /// <summary>清空所有卡片（用于「删除卡片」，删完回到未上传状态）。</summary>
        public static void Clear()
        {
            try
            {
                if (!Directory.Exists(DirPath)) return;
                foreach (var f in Directory.GetFiles(DirPath))
                {
                    try { File.Delete(f); } catch { }
                }
            }
            catch { }
        }

        /// <summary>只保留最新的 MaxCount 张，其余删除（替换 upload 时不会留下旧图）。</summary>
        private static void Trim()
        {
            try
            {
                var files = Directory.GetFiles(DirPath)
                    .Where(f => !Path.GetFileName(f).StartsWith("."))
                    .OrderByDescending(SafeWriteTime)
                    .ToList();
                for (int i = MaxCount; i < files.Count; i++)
                {
                    try { File.Delete(files[i]); } catch { }
                }
            }
            catch { }
        }

        /// <summary>
        /// 预览用位图：按需采样（长边上限 2560，避免大图 OOM），并纠正相机照片的 EXIF 方向。
        /// 返回的 Bitmap 由调用方负责 Recycle。
        /// </summary>
        public static Android.Graphics.Bitmap LoadBitmap(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            try
            {
                var bounds = new Android.Graphics.BitmapFactory.Options { InJustDecodeBounds = true };
                Android.Graphics.BitmapFactory.DecodeFile(path, bounds);
                if (bounds.OutWidth <= 0 || bounds.OutHeight <= 0) return null;

                int sample = 1;
                int maxSide = Math.Max(bounds.OutWidth, bounds.OutHeight);
                while (maxSide / (sample * 2) >= 2560) sample *= 2;

                var opt = new Android.Graphics.BitmapFactory.Options { InSampleSize = sample };
                var bmp = Android.Graphics.BitmapFactory.DecodeFile(path, opt);
                if (bmp == null) return null;

                return ApplyExif(bmp, path);
            }
            catch { return null; }
        }

        /// <summary>相机/相册里的图常带旋转标记，BitmapFactory 不认，得自己转。</summary>
        private static Android.Graphics.Bitmap ApplyExif(Android.Graphics.Bitmap bmp, string path)
        {
            int deg = 0;
            bool mirror = false;
            try
            {
#pragma warning disable CA1422
                var exif = new Android.Media.ExifInterface(path);
                int o = exif.GetAttributeInt(Android.Media.ExifInterface.TagOrientation, 1);
#pragma warning restore CA1422
                switch (o)
                {
                    case 3: deg = 180; break;
                    case 6: deg = 90; break;
                    case 8: deg = 270; break;
                    case 2: mirror = true; break;
                    case 5: deg = 90; mirror = true; break;
                    case 7: deg = 270; mirror = true; break;
                }
            }
            catch { return bmp; }

            if (deg == 0 && !mirror) return bmp;

            try
            {
                var m = new Android.Graphics.Matrix();
                if (deg != 0) m.PostRotate(deg);
                if (mirror) m.PostScale(-1f, 1f);
                var rotated = Android.Graphics.Bitmap.CreateBitmap(bmp, 0, 0, bmp.Width, bmp.Height, m, true);
                if (rotated != null && rotated != bmp) bmp.Recycle();
                return rotated ?? bmp;
            }
            catch { return bmp; }
        }

        private static DateTime SafeWriteTime(string path)
        {
            try { return File.GetLastWriteTimeUtc(path); } catch { return DateTime.MinValue; }
        }

        /// <summary>只解析文件头判断能不能当图片用，不整张解码，几十 MB 也不卡。</summary>
        private static bool IsImage(string path)
        {
            try
            {
                var opt = new Android.Graphics.BitmapFactory.Options { InJustDecodeBounds = true };
                Android.Graphics.BitmapFactory.DecodeFile(path, opt);
                return opt.OutWidth > 0 && opt.OutHeight > 0;
            }
            catch { return false; }
        }

        private static string ExtOf(Android.Content.ContentResolver resolver, Android.Net.Uri uri)
        {
            string mime = null;
            try { mime = resolver.GetType(uri); } catch { }
            switch (mime)
            {
                case "image/png": return ".png";
                case "image/webp": return ".webp";
                case "image/gif": return ".gif";
                case "image/heic":
                case "image/heif": return ".heic";
                case "image/bmp": return ".bmp";
                default: return ".jpg";
            }
        }

        private static Android.Content.ISharedPreferences Prefs
            => Android.App.Application.Context.GetSharedPreferences(
                PrefName, Android.Content.FileCreationMode.Private);
    }
}
