using System;
using System.Collections.Generic;

namespace HamJi.Core
{
    /// <summary>
    /// 时间显示：本地时间（带时区缩写）与 UTC 时间成对出现。
    /// 记录里存的是 UTC 毫秒，展示时按设备当前时区换算本地时间，UTC 固定换算。
    /// 注意：不能直接用 DateTimeOffset.FromUnixTimeMilliseconds 的 Offset（这里拿到的是 0），
    /// 必须走 DateTime.ToLocalTime / TimeZoneInfo.Local 换算。
    /// </summary>
    public static class TimeKit
    {
        // 偏移（小时）→ 时区缩写。键为「实际生效的偏移」，所以夏令时后的偏移也能对上。
        private static readonly Dictionary<double, string> Abbr = new Dictionary<double, string>
        {
            { -11, "SST" }, { -10, "HST" }, { -9, "AKST" }, { -8, "PST" }, { -7, "MST" },
            { -6, "CST" }, { -5, "EST" }, { -4, "AST" }, { -3, "BRT" }, { -2, "FNT" },
            { -1, "AZOT" }, { 0, "UTC" }, { 1, "CET" }, { 2, "EET" }, { 3, "MSK" },
            { 3.5, "IRST" }, { 4, "GST" }, { 4.5, "AFT" }, { 5, "PKT" }, { 5.5, "IST" },
            { 6, "BST" }, { 6.5, "MMT" }, { 7, "ICT" }, { 8, "BJT" }, { 9, "JST" },
            { 9.5, "ACST" }, { 10, "AEST" }, { 11, "AEDT" }, { 12, "NZST" }, { 13, "TOT" }
        };

        // 夏令时偏移 → 缩写（表里没有的一律落到下方兜底）
        private static readonly Dictionary<double, string> DstAbbr = new Dictionary<double, string>
        {
            { -3, "BRST" }, { -2, "BRST" }, { 1, "CEST" }, { 2, "CEST" },
            { 3, "EEST" }, { 10, "AEDT" }, { 11, "AEDT" }
        };

        /// <summary>UTC 毫秒 → 本地时钟文本（HH:mm）。</summary>
        public static string LocalHm(long ms)
            => Utc(ms).ToLocalTime().ToString("HH:mm");

        /// <summary>UTC 毫秒 → UTC 时钟文本（HH:mm）。</summary>
        public static string UtcHm(long ms)
            => Utc(ms).ToString("HH:mm");

        /// <summary>该时刻本地所在的时区缩写，如 BJT / JST / EST；认不出来的给 UTC+05:30。</summary>
        public static string LocalAbbr(long ms)
        {
            var utc = Utc(ms);
            TimeSpan off;
            try { off = TimeZoneInfo.Local.GetUtcOffset(utc); }
            catch { off = TimeSpan.Zero; }

            double h = off.TotalHours;

            // 东八区在国内按惯例写 BJT（北京时间），这也是最常用的那一个
            if (Math.Abs(h - 8) < 0.01) return "BJT";

            string name;
            if (SafeIsDst(utc) && DstAbbr.TryGetValue(h, out name)) return name;
            if (Abbr.TryGetValue(h, out name)) return name;

            return OffsetText(h);
        }

        private static DateTime Utc(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;

        private static bool SafeIsDst(DateTime utc)
        {
            try { return TimeZoneInfo.Local.IsDaylightSavingTime(utc); }
            catch { return false; }
        }

        private static string OffsetText(double h)
        {
            int sign = h < 0 ? -1 : 1;
            double abs = Math.Abs(h);
            int hh = (int)abs;
            int mm = (int)Math.Round((abs - hh) * 60);
            if (mm == 60) { hh++; mm = 0; }
            return "UTC" + (sign < 0 ? "-" : "+") + hh.ToString("00") + (mm > 0 ? ":" + mm.ToString("00") : "");
        }
    }
}
