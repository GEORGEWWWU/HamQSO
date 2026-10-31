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
        public static Drawable RowRipple(float radiusDp = 16f)
        {
            var mask = new GradientDrawable();
            mask.SetColor(Color.White);
            mask.SetCornerRadius(Dui.Dpf(radiusDp));
            return new RippleDrawable(ColorStateList.ValueOf(new Color(Dui.WithAlpha(Dui.Accent, 46))),
                new ColorDrawable(Color.Transparent), mask);
        }

        public static TextView Chip(string text)
        {
            var tv = Dui.Label(text, 10.5f, Dui.Accent, true);
            tv.LetterSpacing = 0.04f;
            tv.SetPadding(Dui.Dp(8), Dui.Dp(3), Dui.Dp(8), Dui.Dp(3));
            tv.SetBackgroundDrawable(Dui.Round(Dui.ChipBg, 8));
            return tv;
        }

        public static LinearLayout DayHeader(string day, string extra, int count)
        {
            var row = Dui.HBox();
            row.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            row.SetPadding(Dui.Dp(8), Dui.Dp(14), Dui.Dp(8), Dui.Dp(7));

            var left = Dui.Label(day, 12.5f, Dui.TextSecondary, true);
            left.LetterSpacing = 0.06f;
            row.AddView(left);

            if (!string.IsNullOrEmpty(extra))
            {
                var mid = Dui.Label(" · " + extra, 12.5f, Dui.TextTertiary, false);
                row.AddView(mid);
            }

            row.AddView(new View(Dui.Ctx), new LinearLayout.LayoutParams(0, 1, 1f));

            var cnt = Dui.Label(count + " 条", 11f, Dui.TextTertiary, false);
            cnt.LetterSpacing = 0.03f;
            row.AddView(cnt);
            return row;
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
        public static LinearLayout SettingRow(int iconRes, string title, string value, bool danger = false, bool chevron = true)
        {
            var row = Dui.HBox();
            row.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            row.SetPadding(Dui.Dp(16), Dui.Dp(14), Dui.Dp(14), Dui.Dp(14));
            row.Clickable = true;
            row.Focusable = true;
            row.SetBackgroundDrawable(RowRipple(18));

            int fg = danger ? Dui.Danger : Dui.TextPrimary;
            var bubble = new FrameLayout(Dui.Ctx);
            bubble.SetBackgroundDrawable(Dui.Round(danger ? Dui.WithAlpha(Dui.Danger, 26) : Dui.AccentSoft, 11));
            var ic = Dui.Icon(iconRes, 19, danger ? Dui.Danger : Dui.Accent);
            bubble.AddView(ic, new FrameLayout.LayoutParams(Dui.Dp(19), Dui.Dp(19)) { Gravity = GravityFlags.Center });
            row.AddView(bubble, new LinearLayout.LayoutParams(Dui.Dp(34), Dui.Dp(34)));

            var tv = Dui.Label(title, 14.5f, fg, false);
            var tlp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f) { LeftMargin = Dui.Dp(12) };
            row.AddView(tv, tlp);

            if (!string.IsNullOrEmpty(value))
            {
                var v = Dui.Label(value, 12.5f, Dui.TextTertiary, false);
                v.SetSingleLine(true);
                row.AddView(v);
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
