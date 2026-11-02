using System;
using System.IO;
using System.Linq;
using Android.Content;
using Android.Graphics;

namespace HamJi.Core
{
    /// <summary>
    /// 系统分享：把海报位图写成 PNG → 通过 <see cref="ShareProvider"/> 暴露成 content:// → 拉起系统选择器。
    ///
    /// 兼容性处理（按踩过的坑排的，别随手删）：
    /// · API 24+ 禁止 file:// 外发，所以一律走 content://；
    /// · 光加 FLAG_GRANT_READ_URI_PERMISSION 不够：Android 5.x / 部分国产 ROM 只认 ClipData 里的授权，
    ///   必须 SetClipData 再给 chooser 也加一遍 flag；
    /// · 某些 ROM 的 chooser 会重建 Intent，PutExtra 的 URI 丢了授权，ClipData 是唯一保底；
    /// · 没有可分享应用时 StartActivity 抛 ActivityNotFoundException，这里接住并回一句提示。
    /// </summary>
    public static class ShareKit
    {
        private const int KeepFiles = 6;

        /// <summary>把位图写到 cache/share/xxx.png，返回文件。同名覆盖。</summary>
        public static Java.IO.File SavePng(Context c, Bitmap bmp, string baseName)
        {
            string dir = ShareProvider.DirPath(c);
            string name = SafeName(baseName) + ".png";
            var file = new Java.IO.File(dir, name);

            using (var fs = System.IO.File.Create(file.AbsolutePath))
            {
                if (!bmp.Compress(Bitmap.CompressFormat.Png, 100, fs))
                    throw new IOException("PNG 编码失败");
                fs.Flush();
            }

            Cleanup(dir);
            return file;
        }

        /// <summary>
        /// 拉起系统分享。成功返回 true；失败带出原因（调用方负责 toast）。
        /// </summary>
        public static bool ShareImage(Context c, Bitmap bmp, string baseName, string text, out string error)
        {
            error = null;
            try
            {
                var file = SavePng(c, bmp, baseName);
                var uri = ShareProvider.UriFor(file.Name);

                var send = new Intent(Intent.ActionSend);
                send.SetType("image/png");
                send.PutExtra(Intent.ExtraStream, uri);
                if (!string.IsNullOrEmpty(text)) send.PutExtra(Intent.ExtraText, text);
                // ClipData 是授权的主要载体：很多接收方只从这里拿 URI 和权限
                send.ClipData = ClipData.NewUri(c.ContentResolver, file.Name, uri);
                send.AddFlags(ActivityFlags.GrantReadUriPermission);

                var chooser = Intent.CreateChooser(send, "分享通联卡片");
                chooser.AddFlags(ActivityFlags.GrantReadUriPermission);
                // chooser 自己也要带上 clipData，否则个别 ROM 在重建 Intent 时会丢掉授权
                chooser.ClipData = ClipData.NewUri(c.ContentResolver, file.Name, uri);

                // 从 Dialog / 非 Activity 上下文启动时必须有 NEW_TASK
                if (!(c is Android.App.Activity)) chooser.AddFlags(ActivityFlags.NewTask);

                c.StartActivity(chooser);
                return true;
            }
            catch (ActivityNotFoundException)
            {
                error = "没有找到可以分享的应用";
                return false;
            }
            catch (Exception ex)
            {
                error = "分享失败：" + ex.Message;
                return false;
            }
        }

        /// <summary>文件名只留 ASCII：中文名在部分接收方里会变成乱码甚至判定为无效文件。</summary>
        private static string SafeName(string baseName)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch in (baseName ?? ""))
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') ||
                    (ch >= '0' && ch <= '9') || ch == '-' || ch == '_') sb.Append(ch);
                else if (ch == ' ') sb.Append('-');
            }
            if (sb.Length == 0) sb.Append("hamji-poster");
            if (sb.Length > 48) sb.Length = 48;
            return sb.ToString();
        }

        /// <summary>只保留最近几张，避免缓存目录越堆越大。</summary>
        private static void Cleanup(string dir)
        {
            try
            {
                var files = new DirectoryInfo(dir).GetFiles("*.png")
                    .OrderByDescending(f => f.LastWriteTimeUtc).ToList();
                for (int i = KeepFiles; i < files.Count; i++)
                {
                    try { files[i].Delete(); } catch { }
                }
            }
            catch { }
        }
    }
}
