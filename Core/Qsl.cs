using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HamJi.Core
{
    /// <summary>
    /// 本机 QSL 卡片图片（完全离线，只存在本机 files/qsl 里）。
    /// 卡片有正反面：固定两个槽位，0 = 正面，1 = 反面；可以一次选两张，也可以分两次上传。
    /// 文件名带槽位号（qsl-01.jpg / qsl-02.png），扩展名随来源图片走。
    /// </summary>
    public static class Qsl
    {
        /// <summary>槽位数量：正反面。改大即可支持更多面，命名规则与 UI 会自动跟上。</summary>
        public const int MaxCount = 2;

        /// <summary>单张上限。只防误选超大原图，不做严格压缩。</summary>
        public const long MaxBytes = 50L * 1024 * 1024;

        private const string PrefName = "hamji_qsl";
        private const string PrefWarned = "warned";
        private const string PrefFill = "fill_slot";

        public static string DirPath
            => Path.Combine(Android.App.Application.Context.FilesDir.AbsolutePath, "qsl");

        /// <summary>各槽位的文件路径，空槽为 null。0 = 正面。</summary>
        public static string[] Slots
        {
            get
            {
                var arr = new string[MaxCount];
                try
                {
                    if (!Directory.Exists(DirPath)) return arr;
                    var legacy = new List<string>();
                    foreach (var f in Directory.GetFiles(DirPath))
                    {
                        var name = Path.GetFileName(f);
                        if (name.StartsWith(".")) continue;
                        int idx = SlotIndex(name);
                        if (idx >= 0)
                        {
                            if (arr[idx] == null) arr[idx] = f;
                        }
                        else legacy.Add(f);
                    }
                    // 老版本用时间戳命名：迁移到空槽，多出来的直接删掉
                    if (legacy.Count > 0)
                    {
                        legacy.Sort((a, b) => SafeWriteTime(a).CompareTo(SafeWriteTime(b)));
                        foreach (var f in legacy)
                        {
                            int free = Array.IndexOf(arr, null);
                            if (free < 0) { TryDelete(f); continue; }
                            var target = Path.Combine(DirPath, SlotFileName(free, Path.GetExtension(f)));
                            try
                            {
                                if (File.Exists(target)) { TryDelete(f); continue; }
                                File.Move(f, target);
                                arr[free] = target;
                            }
                            catch { }
                        }
                    }
                }
                catch { }
                return arr;
            }
        }

        /// <summary>已上传的卡片数量（0 / 1 / 2）。</summary>
        public static int Count
        {
            get
            {
                try { return Slots.Count(s => s != null); } catch { return 0; }
            }
        }

        public static bool Has => Count > 0;

        /// <summary>当前「填写页」（打印时填写的那一面），-1 = 还没设置。</summary>
        public static int FillSlot
        {
            get
            {
                try
                {
                    int v = Prefs.GetInt(PrefFill, -1);
                    return v >= 0 && v < MaxCount ? v : -1;
                }
                catch { return -1; }
            }
            set
            {
                try { Prefs.Edit().PutInt(PrefFill, Math.Max(-1, Math.Min(MaxCount - 1, value))).Apply(); } catch { }
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
        /// 把选中图片写进指定槽位：写临时文件 → 校验是图片 → 落到正式文件名 → 删掉该槽旧文件。
        /// 任何一步失败都不会破坏已有卡片。
        /// </summary>
        public static bool SaveSlot(int index, Android.Content.ContentResolver resolver,
                                    Android.Net.Uri uri, out string error)
        {
            error = null;
            if (index < 0 || index >= MaxCount) { error = "无效的卡片位置"; return false; }
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

                var final = Path.Combine(DirPath, SlotFileName(index, ExtOf(resolver, uri)));
                File.Move(tmp, final, true);
                tmp = null;

                // 同槽位可能残留别的扩展名（jpg → png），一并清掉
                foreach (var f in Directory.GetFiles(DirPath))
                {
                    if (string.Equals(f, final, StringComparison.OrdinalIgnoreCase)) continue;
                    if (SlotIndex(Path.GetFileName(f)) == index) TryDelete(f);
                }

                Android.Util.Log.Info("HamJiQsl", "slot " + (index + 1) + " saved " + Path.GetFileName(final) + " " + total + "B");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (tmp != null) TryDelete(tmp);
            }
        }

        /// <summary>把多张图片依次填进空槽位，返回成功写入的数量。</summary>
        public static int FillEmpty(Android.Content.ContentResolver resolver,
                                   IList<Android.Net.Uri> uris, out string firstError)
        {
            firstError = null;
            int saved = 0;
            if (uris == null) return 0;

            foreach (var uri in uris)
            {
                int free = Array.IndexOf(Slots, null);
                if (free < 0) break;                      // 两个槽位都满了，多的忽略

                string err;
                if (SaveSlot(free, resolver, uri, out err)) saved++;
                else if (firstError == null) firstError = err;
            }
            return saved;
        }

        /// <summary>删除某一面（至少保留一张，删到最后一张时拒绝）。</summary>
        public static bool DeleteSlot(int index)
        {
            if (index < 0 || index >= MaxCount) return false;
            if (Count <= 1) return false;             // 必须至少留一张

            try
            {
                foreach (var f in Directory.GetFiles(DirPath))
                {
                    if (SlotIndex(Path.GetFileName(f)) == index) TryDelete(f);
                }
            }
            catch { return false; }

            if (FillSlot == index) FillSlot = -1;
            return true;
        }

        /// <summary>清空所有卡片（删除入口）。</summary>
        public static void Clear()
        {
            try
            {
                if (!Directory.Exists(DirPath)) return;
                foreach (var f in Directory.GetFiles(DirPath)) TryDelete(f);
            }
            catch { }
            FillSlot = 0;
        }

        /// <summary>
        /// 预览用位图：交给 <see cref="HdrDisplay"/> 解码（HDR / 广色域 / gainmap 都保留），
        /// 返回的 Bitmap 由调用方负责 Recycle。
        /// </summary>
        public static Android.Graphics.Bitmap LoadBitmap(string path)
            => HdrDisplay.Decode(path, out _);

        /// <summary>
        /// 图片平均亮度（0 黑 ~ 1 白）：预览背景要跟图片反着来，否则黑底配黑图整块糊在一起。
        /// 缩到 16×16 采样，几十毫秒的事。
        /// </summary>
        public static float Luma(Android.Graphics.Bitmap bmp)
        {
            if (bmp == null || bmp.IsRecycled || bmp.Width <= 0 || bmp.Height <= 0) return 0.5f;
            Android.Graphics.Bitmap small = null;
            try
            {
                small = Android.Graphics.Bitmap.CreateScaledBitmap(bmp, 16, 16, false);
                if (small == null) return 0.5f;
                var px = new int[16 * 16];
                small.GetPixels(px, 0, 16, 0, 0, 16, 16);

                double sum = 0;
                foreach (int p in px)
                {
                    int r = (p >> 16) & 0xFF, g = (p >> 8) & 0xFF, b = p & 0xFF;
                    sum += 0.2126 * r + 0.7152 * g + 0.0722 * b;
                }
                return (float)(sum / px.Length / 255.0);
            }
            catch { return 0.5f; }
            finally
            {
                try { if (small != null && !small.IsRecycled) small.Recycle(); } catch { }
            }
        }

        // ---- 文件命名 ----

        private static string SlotFileName(int index, string ext)
            => "qsl-" + (index + 1).ToString("00") + (string.IsNullOrEmpty(ext) ? ".jpg" : ext);

        /// <summary>从 qsl-01.jpg 解析出 0；不是槽位文件返回 -1。</summary>
        private static int SlotIndex(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || !fileName.StartsWith("qsl-")) return -1;
            if (fileName.Length < 7 || fileName[6] != '.') return -1;
            int n;
            if (!int.TryParse(fileName.Substring(4, 2), out n)) return -1;
            n -= 1;
            return n >= 0 && n < MaxCount ? n : -1;
        }

        private static DateTime SafeWriteTime(string path)
        {
            try { return File.GetLastWriteTimeUtc(path); } catch { return DateTime.MinValue; }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
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
