using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace HamJi.Core
{
    /// <summary>呼号档案：单独存一份，和记录数据分开，任何一次记录读写都不会碰到它。</summary>
    public class ProfileFile
    {
        public string Call { get; set; } = "";
    }

    /// <summary>
    /// 本机呼号（台站身份）。
    /// 存三处互为兜底：profile.json → profile.json.bak → SharedPreferences，
    /// 只要有一处读得出来就不会「变成没填过」而重新弹引导。
    /// 写入只接受合法呼号（5-6 位字母数字），一旦写入就不会被写空。
    /// </summary>
    public static class Profile
    {
        private const string PrefName = "hamji_profile";
        private const string PrefKey = "call";

        private static readonly object Gate = new object();
        private static string _call = "";
        private static bool _loaded;

        public static event Action Changed;

        public static string FilePath
        {
            get
            {
                var dir = Android.App.Application.Context.FilesDir.AbsolutePath;
                return Path.Combine(dir, "profile.json");
            }
        }

        public static string BackupPath => FilePath + ".bak";

        /// <summary>当前呼号，未填过返回空串。</summary>
        public static string Call
        {
            get { EnsureLoaded(); return _call; }
        }

        public static bool HasCall => !string.IsNullOrEmpty(Call);

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            lock (Gate)
            {
                if (_loaded) return;
                _call = ReadFromFile(FilePath) ?? ReadFromFile(BackupPath) ?? ReadFromPrefs() ?? "";
                _loaded = true;
            }
        }

        private static string ReadFromFile(string path)
        {
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length == 0) return null;
                var pf = JsonSerializer.Deserialize(File.ReadAllText(path), AppJson.Default.ProfileFile);
                var c = Normalize(pf?.Call);
                return IsValid(c) ? c : null;
            }
            catch { return null; }
        }

        private static string ReadFromPrefs()
        {
            try
            {
                var sp = Android.App.Application.Context.GetSharedPreferences(
                    PrefName, Android.Content.FileCreationMode.Private);
                var c = Normalize(sp.GetString(PrefKey, ""));
                return IsValid(c) ? c : null;
            }
            catch { return null; }
        }

        /// <summary>校验并保存；非法输入拒绝写入，返回 false 并带出原因。</summary>
        public static bool TrySave(string raw, out string error)
        {
            var call = Normalize(raw);
            if (string.IsNullOrEmpty(call))
            {
                error = "呼号不能为空";
                return false;
            }
            if (!IsValid(call))
            {
                error = "呼号需 5-6 位，由字母和数字组成";
                return false;
            }
            Write(call);
            error = null;
            return true;
        }

        /// <summary>备份恢复用：本地还没有呼号时才写入，绝不覆盖已有呼号。</summary>
        public static bool AdoptIfEmpty(string raw)
        {
            EnsureLoaded();
            if (HasCall) return false;
            var call = Normalize(raw);
            if (!IsValid(call)) return false;
            Write(call);
            return true;
        }

        private static void Write(string call)
        {
            lock (Gate)
            {
                _call = call;
                _loaded = true;
            }

            // 1) 主文件（原子替换，旧内容留 .bak）
            try
            {
                var json = JsonSerializer.Serialize(new ProfileFile { Call = call }, AppJson.Default.ProfileFile);
                if (File.Exists(FilePath))
                {
                    try { File.Copy(FilePath, BackupPath, true); } catch { }
                }
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, json, new UTF8Encoding(false));
                File.Move(tmp, FilePath, true);
            }
            catch (Exception ex)
            {
                Android.Util.Log.Info("HamJiProfile", "write file fail: " + ex.Message);
            }

            // 2) SharedPreferences 再存一份（commit 同步落盘）
            try
            {
                var sp = Android.App.Application.Context.GetSharedPreferences(
                    PrefName, Android.Content.FileCreationMode.Private);
                sp.Edit().PutString(PrefKey, call).Commit();
            }
            catch { }

            Changed?.Invoke();
        }

        /// <summary>去空格、转大写。</summary>
        public static string Normalize(string raw)
            => (raw ?? "").Replace(" ", "").Replace("\t", "").Replace("\n", "").Trim().ToUpperInvariant();

        /// <summary>5-6 位，只允许字母和数字，且字母数字都要有。</summary>
        public static bool IsValid(string call)
        {
            if (string.IsNullOrEmpty(call)) return false;
            if (call.Length < 5 || call.Length > 6) return false;
            bool letter = false, digit = false;
            foreach (char ch in call)
            {
                if (ch >= 'A' && ch <= 'Z') letter = true;
                else if (ch >= '0' && ch <= '9') digit = true;
                else return false;
            }
            return letter && digit;
        }

        public const string Rule = "5-6 位，字母和数字组合，例如 BH7IDW";
    }
}
