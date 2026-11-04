using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HamJi.Core
{
    /// <summary>
    /// 启动自检：只读检查本地数据文件与记录内容，不改动、不修复任何数据。
    /// 全部正常返回 null；发现问题返回一句可直接给用户看的描述。
    /// </summary>
    public static class DataCheck
    {
        private static bool _done;

        public static string Run()
        {
            if (_done) return null;        // 每次进程启动只查一次，避免旋转/重建重复提示
            _done = true;

            var issues = new List<string>();
            try
            {
                Store.EnsureLoaded();
                Profile.EnsureLoaded();

                AddStoreIssues(issues);
                AddRecordIssues(issues);
                AddProfileIssues(issues);
            }
            catch (Exception ex)
            {
                issues.Add("自检未完成（" + ex.GetType().Name + "）");
            }

            if (issues.Count == 0) return null;
            if (issues.Count <= 2) return string.Join("；", issues);
            return issues[0] + "；" + issues[1] + " 等 " + issues.Count + " 项";
        }

        /// <summary>加载阶段留下的状态：写保护 / 主文件损坏后从备份恢复 / 上次写入失败。</summary>
        private static void AddStoreIssues(List<string> issues)
        {
            if (Store.IsBlocked)
            {
                issues.Add("数据文件损坏，已暂停写入以保护原始数据");
                return;
            }
            if (!string.IsNullOrEmpty(Store.LastError)) issues.Add(Store.LastError);
        }

        /// <summary>直接读一遍记录文件，检查格式与记录级异常（重复 ID、重复内容、非法类型）。</summary>
        private static void AddRecordIssues(List<string> issues)
        {
            string path = Store.FilePath;
            long len;
            try
            {
                if (!File.Exists(path)) return;        // 首次运行没有文件，属正常
                len = new FileInfo(path).Length;
            }
            catch { return; }

            if (len == 0)
            {
                issues.Add("记录文件为空");
                return;
            }

            List<QsoRecord> list;
            try
            {
                list = JsonSerializer.Deserialize(File.ReadAllText(path), AppJson.Default.ListQsoRecord);
            }
            catch
            {
                issues.Add("记录文件无法解析");
                return;
            }
            if (list == null)
            {
                issues.Add("记录文件格式不正确");
                return;
            }

            int nulls = 0, dupId = 0, dupRec = 0, badKind = 0;
            var ids = new HashSet<string>();
            var seen = new HashSet<string>();
            foreach (var r in list)
            {
                if (r == null) { nulls++; continue; }
                if (r.Kind != (int)RecKind.Frequency && r.Kind != (int)RecKind.Log) badKind++;
                if (!string.IsNullOrEmpty(r.Id) && !ids.Add(r.Id)) dupId++;
                var key = r.Kind + "|" + r.Time + "|" + r.Freq.ToString("R") + "|" + (r.Call ?? "");
                if (!seen.Add(key)) dupRec++;
            }

            if (badKind > 0) issues.Add(badKind + " 条记录类型异常");
            if (dupId > 0) issues.Add(dupId + " 条记录 ID 重复");
            if (dupRec > 0) issues.Add(dupRec + " 条记录内容重复");
            if (nulls > 0) issues.Add(nulls + " 条空记录");
        }

        /// <summary>呼号档案只在「有文件但读不出来」时才算异常，缺失属正常（会走引导页）。</summary>
        private static void AddProfileIssues(List<string> issues)
        {
            try
            {
                var path = Profile.FilePath;
                if (!File.Exists(path) || new FileInfo(path).Length == 0) return;

                ProfileFile pf;
                try
                {
                    pf = JsonSerializer.Deserialize(File.ReadAllText(path), AppJson.Default.ProfileFile);
                }
                catch
                {
                    issues.Add("呼号档案无法解析");
                    return;
                }

                var call = Profile.Normalize(pf?.Call);
                if (!string.IsNullOrEmpty(call) && !Profile.IsValid(call)) issues.Add("呼号档案内容异常");
            }
            catch { }
        }
    }
}
