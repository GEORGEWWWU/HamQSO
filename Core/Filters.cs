using System;
using System.Text;
using Android.Text;

namespace HamJi.Core
{
    /// <summary>输入约束：只允许合法字符 + 长度上限 + 呼号自动大写。</summary>
    public static class Filters
    {
        /// <summary>中文输入法会把 "." 变成 "。"，全角字符统一折成半角。</summary>
        private static char Norm(char c)
        {
            if (c >= '０' && c <= '９') return (char)('0' + (c - '０'));
            if (c >= 'Ａ' && c <= 'Ｚ') return (char)('A' + (c - 'Ａ'));
            if (c >= 'ａ' && c <= 'ｚ') return (char)('a' + (c - 'ａ'));
            switch (c)
            {
                case '。':
                case '．':
                case '｡':
                case '·':
                    return '.';
                case '，':
                    return ',';
                case '／':
                    return '/';
                case '－':
                case '−':
                case '—':
                    return '-';
                case '＋':
                    return '+';
            }
            return c;
        }

        private static bool Digit(char c) => c >= '0' && c <= '9';
        private static bool DigitOrDot(char c) => Digit(c) || c == '.';
        private static bool CallChar(char c) => Digit(c) || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '/';
        private static bool RstChar(char c) => Digit(c) || c == '+' || c == '-';

        /// <summary>频率：数字 + 一个小数点，最多 8 位。</summary>
        public static IInputFilter[] Freq() => new IInputFilter[] { new Guard(DigitOrDot, false, 8, true) };

        /// <summary>呼号：字母数字与斜杠，自动转大写，最多 10 位。</summary>
        public static IInputFilter[] CallSign() => new IInputFilter[] { new Guard(CallChar, true, 10, false) };

        /// <summary>信号报告：数字与 +/-，最多 4 位。</summary>
        public static IInputFilter[] Rst() => new IInputFilter[] { new Guard(RstChar, false, 4, false) };

        /// <summary>小数（功率等）：数字 + 一个小数点。</summary>
        public static IInputFilter[] Decimal(int max = 6) => new IInputFilter[] { new Guard(DigitOrDot, false, max, true) };

        /// <summary>纯长度限制。</summary>
        public static IInputFilter[] Length(int max) => new IInputFilter[] { new Guard(_ => true, false, max, false) };

        private sealed class Guard : Java.Lang.Object, IInputFilter
        {
            private readonly Func<char, bool> _allowed;
            private readonly bool _upper;
            private readonly int _max;
            private readonly bool _singleDot;

            public Guard(Func<char, bool> allowed, bool upper, int max, bool singleDot)
            {
                _allowed = allowed;
                _upper = upper;
                _max = max;
                _singleDot = singleDot;
            }

            public Java.Lang.ICharSequence FilterFormatted(Java.Lang.ICharSequence source, int start, int end,
                ISpanned dest, int dstart, int dend)
            {
                if (source == null) return null;

                string raw;
                try
                {
                    var all = source.ToString() ?? "";
                    int len = end - start;
                    if (start < 0) { start = 0; }
                    if (len <= 0 || start + len > all.Length) return null;
                    raw = all.Substring(start, len);
                }
                catch { return null; }

                var sb = new StringBuilder(raw.Length);
                foreach (var ch in raw)
                {
                    var c = Norm(ch);
                    if (!_allowed(c)) continue;
                    sb.Append(_upper ? char.ToUpperInvariant(c) : c);
                }
                string outp = sb.ToString();

                if (_singleDot && outp.IndexOf('.') >= 0)
                {
                    int dots = 0;
                    var dstr = dest?.ToString() ?? "";
                    foreach (var c in dstr) if (c == '.') dots++;
                    var sb2 = new StringBuilder(outp.Length);
                    foreach (var c in outp)
                    {
                        if (c == '.')
                        {
                            if (dots > 0) continue;
                            dots++;
                        }
                        sb2.Append(c);
                    }
                    outp = sb2.ToString();
                }

                if (_max > 0)
                {
                    int keep = (dest?.Length() ?? 0) - (dend - dstart);
                    int room = _max - Math.Max(0, keep);
                    if (room <= 0) return new Java.Lang.String("");
                    if (outp.Length > room) outp = outp.Substring(0, room);
                }

                if (outp.Length == raw.Length && outp == raw) return null;
                return new Java.Lang.String(outp);
            }

            public Java.Lang.ICharSequence Filter(Java.Lang.ICharSequence source, int start, int end,
                ISpanned dest, int dstart, int dend)
                => FilterFormatted(source, start, end, dest, dstart, dend);
        }
    }
}
