using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Widget;

namespace HamJi.Core
{
    /// <summary>GitHub Release 更新检查。静默联网，只在真有新版时才弹窗。</summary>
    public static class Updater
    {
        public const string Repo = "GEORGEWWWU/HamQSO";
        public const string ApiUrl = "https://api.github.com/repos/GEORGEWWWU/HamQSO/releases/latest";
        public const string ReleasesUrl = "https://github.com/GEORGEWWWU/HamQSO/releases/latest";

        private const string PrefFile = "hamji_updater";
        private const string KeySkip = "skip_tag";

        private static string _latest;
        private static DateTime _latestAt = DateTime.MinValue;

        /// <summary>最近一次成功拉取到的远端版本号（形如 v1.2.0）；未知时为 null。</summary>
        public static string LatestTag => _latest;

        private static bool LatestFresh
            => !string.IsNullOrEmpty(_latest) && (DateTime.UtcNow - _latestAt).TotalMinutes < 10;

        /// <summary>取远端最新版本号用于设置页展示：10 分钟内复用缓存，否则后台拉一次。</summary>
        public static void EnsureLatest(Context c, Action<string> done)
        {
            if (LatestFresh) { done(_latest); return; }
            Check(c, r =>
            {
                if (r.Ok && !string.IsNullOrEmpty(r.Tag)) done(r.Tag);
            });
        }

        public class Result
        {
            public bool Ok;
            public bool HasUpdate;
            public string Tag = "";
            public string Name = "";
            public string Url = ReleasesUrl;
            public string Notes = "";
            public string ApkUrl = "";
        }

        /// <summary>后台拉一次最新版本，回调回到 UI 线程。</summary>
        public static void Check(Context c, Action<Result> done)
        {
            Task.Run(() =>
            {
                var r = Fetch();
                try
                {
                    if (c is Activity act) act.RunOnUiThread(() => done(r));
                    else new Android.OS.Handler(Android.OS.Looper.MainLooper).Post(() => done(r));
                }
                catch { done(r); }
            });
        }

        private static Result Fetch()
        {
            var r = new Result();
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(ApiUrl);
                req.Method = "GET";
                req.Timeout = 8000;
                req.ReadWriteTimeout = 8000;
                req.UserAgent = "HamJi/" + AppVersion();
                req.Accept = "application/vnd.github+json";
                req.Headers["Accept-Charset"] = "utf-8";

                string json;
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream()))
                    json = sr.ReadToEnd();

                // 手搓解析，避免为这一个字段引入整个 JSON 库
                r.Tag = Json(json, "tag_name");
                r.Name = Json(json, "name");
                var url = Json(json, "html_url");
                if (!string.IsNullOrEmpty(url)) r.Url = url;
                r.Notes = Json(json, "body") ?? "";
                r.ApkUrl = FirstApk(json);

                r.HasUpdate = Newer(r.Tag, AppVersion());
                r.Ok = true;
                if (!string.IsNullOrEmpty(r.Tag))
                {
                    _latest = r.Tag;
                    _latestAt = DateTime.UtcNow;
                }
            }
            catch (System.Exception ex)
            {
                Android.Util.Log.Info("HamJiUpd", "fetch fail: " + ex.Message);
                r.Ok = false;
            }
            Android.Util.Log.Info("HamJiUpd", "ok=" + r.Ok + " tag=" + r.Tag + " newer=" + r.HasUpdate);
            return r;
        }

        private static string Json(string s, string key)
        {
            var k = "\"" + key + "\"";
            int i = s.IndexOf(k, StringComparison.Ordinal);
            if (i < 0) return "";
            i = s.IndexOf(':', i + k.Length);
            if (i < 0) return "";
            int a = s.IndexOf('"', i);
            if (a < 0) return "";
            int b = a + 1;
            while (b < s.Length)
            {
                if (s[b] == '\\') { b += 2; continue; }
                if (s[b] == '"') break;
                b++;
            }
            return s.Substring(a + 1, b - a - 1).Replace("\\n", "\n").Replace("\\r", "").Replace("\\\"", "\"");
        }

        private static string FirstApk(string s)
        {
            // 优先找 .apk 结尾的下载地址，避免拿到源码包之类的资产
            int i = 0;
            while (true)
            {
                int k = s.IndexOf("\"browser_download_url\"", i, StringComparison.Ordinal);
                if (k < 0) break;
                var u = Json(s.Substring(k), "browser_download_url");
                if (!string.IsNullOrEmpty(u) && u.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) return u;
                i = k + 1;
            }
            int first = s.IndexOf("\"browser_download_url\"", StringComparison.Ordinal);
            if (first < 0) return "";
            return Json(s.Substring(first), "browser_download_url");
        }

        public static string AppVersion()
        {
            try
            {
                var ctx = Application.Context;
                return ctx.PackageManager.GetPackageInfo(ctx.PackageName, 0).VersionName ?? "1.0.0";
            }
            catch { return "1.0.0"; }
        }

        /// <summary>比较 v1.2.3 形式；返回 remote 是否比 local 新。</summary>
        public static bool Newer(string remote, string local)
        {
            var a = Nums(remote);
            var b = Nums(local);
            int n = Math.Max(a.Length, b.Length);
            for (int i = 0; i < n; i++)
            {
                int x = i < a.Length ? a[i] : 0;
                int y = i < b.Length ? b[i] : 0;
                if (x > y) return true;
                if (x < y) return false;
            }
            return false;
        }

        private static int[] Nums(string s)
        {
            if (string.IsNullOrEmpty(s)) return new int[0];
            var t = s.Trim().TrimStart('v', 'V');
            var parts = t.Split(new[] { '.', '-', '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var arr = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                int v;
                arr[i] = int.TryParse(parts[i], out v) ? v : 0;
            }
            return arr;
        }

        /// <summary>启动时的静默检查：有新版才弹，且同一版本只打扰一次。</summary>
        public static void SilentCheck(Context c)
        {
            Check(c, r =>
            {
                if (!r.Ok || !r.HasUpdate) return;
                if (SkipTag(c) == r.Tag) return;
                ShowUpdate(c, r);
            });
        }

        /// <summary>设置页手动检查：无论结果都给反馈。</summary>
        public static void ManualCheck(Context c, Action done = null)
        {
            Check(c, r =>
            {
                done?.Invoke();
                if (!r.Ok)
                {
                    Toast.MakeText(c, "检查失败，请检查网络", ToastLength.Short).Show();
                    return;
                }
                if (!r.HasUpdate)
                {
                    Toast.MakeText(c, "已是最新版本 v" + AppVersion(), ToastLength.Short).Show();
                    return;
                }
                ShowUpdate(c, r);
            });
        }

        public static void ShowUpdate(Context c, Result r)
        {
            // 优先用 App 内的自绘弹窗（可原地下载 + 进度），拿不到 Activity 时退回浏览器
            if (c is Activity act)
            {
                try
                {
                    if (!act.IsFinishing)
                    {
                        new HamJi.Views.UpdateDialog(act, r).Show();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Android.Util.Log.Info("HamJiUpd", "dialog fail: " + ex.Message);
                }
            }
            Open(c, r.Url);
        }

        /// <summary>不再提示这个版本（直到出现更新的 tag 才会再提醒）。</summary>
        public static void SkipVersion(Context c, string tag)
        {
            if (!string.IsNullOrEmpty(tag)) SetSkip(c, tag);
        }

        public static void OpenRelease(Context c, string url)
        {
            Open(c, string.IsNullOrEmpty(url) ? ReleasesUrl : url);
        }

        private static void Open(Context c, string url)
        {
            try
            {
                c.StartActivity(new Intent(Intent.ActionView, Android.Net.Uri.Parse(url)));
            }
            catch { Toast.MakeText(c, url, ToastLength.Long).Show(); }
        }

        private static string SkipTag(Context c)
        {
            try { return c.GetSharedPreferences(PrefFile, FileCreationMode.Private).GetString(KeySkip, "") ?? ""; }
            catch { return ""; }
        }

        private static void SetSkip(Context c, string tag)
        {
            try { c.GetSharedPreferences(PrefFile, FileCreationMode.Private).Edit().PutString(KeySkip, tag).Commit(); }
            catch { }
        }
    }
}
