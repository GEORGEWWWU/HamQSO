using System;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Content.Res;
using Android.Text;
using Android.Views;
using Android.Widget;
using HamJi.Core;

namespace HamJi.Views
{
    public static class Widgets
    {
        public static TextView Chip(string text)
        {
            var tv = Dui.Label(text, 10.5f, Dui.Accent, true);
            tv.LetterSpacing = 0.04f;
            tv.SetPadding(Dui.Dp(8), Dui.Dp(3), Dui.Dp(8), Dui.Dp(3));
            tv.SetBackgroundDrawable(Dui.Round(Dui.ChipBg, 8));
            return tv;
        }

        /// <summary>
        /// 行右侧时间：本地时间 / 固定的 UTC 时间，例如「15:55 / 07:55 UTC」。
        /// 本地时间不加时区标签（省宽度，本地就是设备当前时区），只有 UTC 侧带一个更小更暗的标签，
        /// 两者用斜杠分开，成百条排下来不抢视线。
        /// </summary>
        public static LinearLayout DualTime(long ms, float sp = 12f, string datePrefix = null)
        {
            var box = Dui.HBox();

            if (!string.IsNullOrEmpty(datePrefix))
            {
                var d = Dui.Label(datePrefix, Math.Max(9f, sp - 2f), Dui.TextTertiary, false);
                d.SetTypeface(Typeface.Create("monospace", TypefaceStyle.Normal), TypefaceStyle.Normal);
                var dlp = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
                { RightMargin = Dui.Dp(6), Gravity = GravityFlags.Bottom, BottomMargin = Dui.Dp(1) };
                box.AddView(d, dlp);
            }

            box.AddView(TimeCell(TimeKit.LocalHm(ms), null, sp, Dui.TextSecondary));

            var sep = Dui.Label("/", sp - 1f, Dui.WithAlpha(Dui.TextTertiary, Dui.Dark ? 120 : 140), false);
            sep.SetPadding(Dui.Dp(5), 0, Dui.Dp(5), 0);
            var slp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
            { Gravity = GravityFlags.Bottom, BottomMargin = Dui.Dp(1) };
            box.AddView(sep, slp);

            box.AddView(TimeCell(TimeKit.UtcHm(ms), "UTC", sp, Dui.TextTertiary));
            return box;
        }

        /// <summary>「时钟 + 小时区缩写」这一节。</summary>
        private static LinearLayout TimeCell(string hm, string tag, float sp, int color)
        {
            var box = Dui.HBox();

            var t = Dui.Label(hm, sp, color, false);
            t.SetTypeface(Typeface.Create("monospace", TypefaceStyle.Normal), TypefaceStyle.Normal);
            t.LetterSpacing = -0.01f;
            box.AddView(t);

            if (!string.IsNullOrEmpty(tag))
            {
                var g = Dui.Label(tag, Math.Max(8f, sp - 3.5f),
                    Dui.WithAlpha(Dui.TextTertiary, Dui.Dark ? 185 : 205), true);
                g.LetterSpacing = 0.08f;
                var lp = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
                { LeftMargin = Dui.Dp(3), Gravity = GravityFlags.Bottom, BottomMargin = Dui.Dp(1.5f) };
                box.AddView(g, lp);
            }
            return box;
        }

        public static LinearLayout Card()
        {
            var card = Dui.VBox();
            card.SetBackgroundDrawable(Dui.GlassCard());
            card.ClipToOutline = true;
            card.OutlineProvider = ViewOutlineProvider.Background;
            card.Elevation = Dui.Dpf(2);
            var lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            lp.BottomMargin = Dui.Dp(8);
            card.LayoutParameters = lp;
            return card;
        }

        public static View Divider()
        {
            var v = new View(Dui.Ctx);
            v.SetBackgroundColor(new Color(Dui.Hairline));
            var lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Math.Max(1, Dui.Dp(0.7f)));
            lp.LeftMargin = Dui.Dp(16);
            lp.RightMargin = Dui.Dp(16);
            v.LayoutParameters = lp;
            return v;
        }

        public static LinearLayout EmptyState(int iconRes, string title, string desc)
        {
            var box = Dui.VBox();
            box.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            box.SetGravity(GravityFlags.CenterHorizontal);
            box.SetPadding(Dui.Dp(24), Dui.Dp(76), Dui.Dp(24), Dui.Dp(40));

            var circle = new FrameLayout(Dui.Ctx);
            circle.SetBackgroundDrawable(Dui.Round(Dui.Dark ? Dui.AccentSoft : Dui.WithAlpha(Dui.Accent, 22), 44));
            var ic = Dui.Icon(iconRes, 38, Dui.Accent);
            var ilp = new FrameLayout.LayoutParams(Dui.Dp(38), Dui.Dp(38)) { Gravity = GravityFlags.Center };
            circle.AddView(ic, ilp);
            var clp = new LinearLayout.LayoutParams(Dui.Dp(88), Dui.Dp(88)) { Gravity = GravityFlags.CenterHorizontal };
            box.AddView(circle, clp);

            var t = Dui.Label(title, 15f, Dui.TextPrimary, true);
            t.Gravity = GravityFlags.Center;
            var tlp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Dui.Dp(18) };
            tlp.Gravity = GravityFlags.CenterHorizontal;
            box.AddView(t, tlp);

            var d = Dui.Label(desc, 12.5f, Dui.TextTertiary, false);
            d.Gravity = GravityFlags.Center;
            d.SetLineSpacing(Dui.Dpf(3), 1f);
            var dlp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Dui.Dp(7) };
            dlp.Gravity = GravityFlags.CenterHorizontal;
            box.AddView(d, dlp);
            return box;
        }

        /// <summary>设置页列表项：图标 + 标题 + 右侧值/箭头。</summary>
        public static LinearLayout SettingRow(int iconRes, string title, string value, bool danger = false, bool chevron = true, int dotColor = 0)
        {
            TextView ignored;
            return SettingRow(iconRes, title, value, danger, chevron, dotColor, out ignored);
        }

        /// <summary>同上，额外带回右侧值文本控件，便于异步更新（如远端版本号）。</summary>
        public static LinearLayout SettingRow(int iconRes, string title, string value, bool danger, bool chevron, int dotColor, out TextView valueView)
        {
            valueView = null;
            var row = Dui.HBox();
            row.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            row.SetPadding(Dui.Dp(16), Dui.Dp(12), Dui.Dp(14), Dui.Dp(12));
            row.Clickable = true;
            row.Focusable = true;
            HamJi.Core.Touch.Press(row, 0.985f);

            int fg = danger ? Dui.Danger : Dui.TextPrimary;

            // 图标不再带底色块，直接放大一点，和标题贴近
            var ic = Dui.Icon(iconRes, 21, danger ? Dui.Danger : Dui.Accent);
            row.AddView(ic, new LinearLayout.LayoutParams(Dui.Dp(21), Dui.Dp(21)));

            var tv = Dui.Label(title, 14.5f, fg, false);
            var tlp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f) { LeftMargin = Dui.Dp(9) };
            row.AddView(tv, tlp);

            if (dotColor != 0)
            {
                var dot = new View(Dui.Ctx);
                dot.SetBackgroundDrawable(Dui.Round(dotColor, 5));
                row.AddView(dot, new LinearLayout.LayoutParams(Dui.Dp(10), Dui.Dp(10)) { RightMargin = Dui.Dp(7) });
            }

            if (!string.IsNullOrEmpty(value))
            {
                var v = Dui.Label(value, 12.5f, Dui.TextTertiary, false);
                v.SetSingleLine(true);
                row.AddView(v);
                valueView = v;
            }

            if (chevron)
            {
                var ch = Dui.Icon(Resource.Drawable.ic_chevron, 16, Dui.TextTertiary);
                var clp = new LinearLayout.LayoutParams(Dui.Dp(16), Dui.Dp(16)) { LeftMargin = Dui.Dp(4) };
                ch.LayoutParameters = clp;
                ch.Alpha = 0.7f;
                row.AddView(ch);
            }
            return row;
        }

        public static LinearLayout SectionLabel(string text)
        {
            var box = Dui.VBox();
            var tv = Dui.Label(text, 11.5f, Dui.TextTertiary, true);
            tv.LetterSpacing = 0.1f;
            box.SetPadding(Dui.Dp(8), Dui.Dp(20), Dui.Dp(8), Dui.Dp(8));
            box.AddView(tv);
            return box;
        }

        public static LinearLayout ProfileCard()
        {
            var card = Dui.VBox();
            card.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            card.SetBackgroundDrawable(Dui.GlassCard());
            card.ClipToOutline = true;
            card.OutlineProvider = ViewOutlineProvider.Background;
            card.Elevation = Dui.Dpf(3);
            return card;
        }
    }
}
