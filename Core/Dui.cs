using System;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Content.Res;
using Android.Views;
using Android.Widget;

namespace HamJi.Core
{
    /// <summary>设计系统：颜色、间距、字体、可绘制对象与交互反馈。</summary>
    public static class Dui
    {
        public static Context Ctx;
        public static float Density = 2.5f;
        public static bool Dark;

        public static int BgTop, BgBottom, BlobA, BlobB, BlobC;
        public static int TextPrimary, TextSecondary, TextTertiary;
        public static int CardBg, CardBorder, Hairline;
        public static int Accent, AccentDeep, AccentSoft, AccentText, AccentFill, AccentFill2;
        public static int ChipBg, NavBg, NavBorder, NavIcon, NavIconOn;
        public static int InputBg, InputBorder, Scrim, SheetBg, Danger, IconBg;

        public static void Init(Context c)
        {
            Ctx = c;
            Density = c.Resources.DisplayMetrics.Density;
            var ui = c.Resources.Configuration.UiMode & UiMode.NightMask;
            Dark = ui == UiMode.NightYes;
            var r = c.Resources;
            BgTop = Clr(r, Resource.Color.bg_top);
            BgBottom = Clr(r, Resource.Color.bg_bottom);
            BlobA = Clr(r, Resource.Color.blob_a);
            BlobB = Clr(r, Resource.Color.blob_b);
            BlobC = Clr(r, Resource.Color.blob_c);
            TextPrimary = Clr(r, Resource.Color.text_primary);
            TextSecondary = Clr(r, Resource.Color.text_secondary);
            TextTertiary = Clr(r, Resource.Color.text_tertiary);
            CardBg = Clr(r, Resource.Color.card_bg);
            CardBorder = Clr(r, Resource.Color.card_border);
            Hairline = Clr(r, Resource.Color.hairline);
            Accent = Clr(r, Resource.Color.accent);
            AccentDeep = Clr(r, Resource.Color.accent_deep);
            AccentSoft = Clr(r, Resource.Color.accent_soft);
            AccentText = Clr(r, Resource.Color.accent_text);
            AccentFill = Clr(r, Resource.Color.accent_fill);
            AccentFill2 = Clr(r, Resource.Color.accent_fill2);
            ChipBg = Clr(r, Resource.Color.chip_bg);
            NavBg = Clr(r, Resource.Color.nav_bg);
            NavBorder = Clr(r, Resource.Color.nav_border);
            NavIcon = Clr(r, Resource.Color.nav_icon);
            NavIconOn = Clr(r, Resource.Color.nav_icon_on);
            InputBg = Clr(r, Resource.Color.input_bg);
            InputBorder = Clr(r, Resource.Color.input_border);
            Scrim = Clr(r, Resource.Color.scrim);
            SheetBg = Clr(r, Resource.Color.sheet_bg);
            Danger = Clr(r, Resource.Color.danger);
            IconBg = Clr(r, Resource.Color.icon_bg);
        }

        private static int Clr(Resources r, int id)
        {
#pragma warning disable CS0618
            return r.GetColor(id);
#pragma warning restore CS0618
        }

        public static int Dp(float v) => (int)(v * Density + 0.5f);
        public static float Dpf(float v) => v * Density;

        public static string FreqLine(QsoRecord r)
            => r == null || r.Freq <= 0 ? "—" : r.FreqText + " " + r.UnitText;

        public static Typeface Medium => Typeface.Create("sans-serif-medium", TypefaceStyle.Normal);
        public static Typeface Regular => Typeface.Create("sans-serif", TypefaceStyle.Normal);
        public static Typeface Bold => Typeface.Create("sans-serif", TypefaceStyle.Bold);
        public static Typeface Mono => Typeface.Create("monospace", TypefaceStyle.Bold);

        public static int WithAlpha(int color, int alpha)
            => Color.Argb(alpha, Color.GetRedComponent(color), Color.GetGreenComponent(color), Color.GetBlueComponent(color));

        public static int Mix(int a, int b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.Argb(
                (int)(Color.GetAlphaComponent(a) + (Color.GetAlphaComponent(b) - Color.GetAlphaComponent(a)) * t),
                (int)(Color.GetRedComponent(a) + (Color.GetRedComponent(b) - Color.GetRedComponent(a)) * t),
                (int)(Color.GetGreenComponent(a) + (Color.GetGreenComponent(b) - Color.GetGreenComponent(a)) * t),
                (int)(Color.GetBlueComponent(a) + (Color.GetBlueComponent(b) - Color.GetBlueComponent(a)) * t));
        }

        // ---------- 可绘制对象 ----------

        public static GradientDrawable Round(int color, float radiusDp)
        {
            var d = new GradientDrawable();
            d.SetColor(color);
            d.SetCornerRadius(Dpf(radiusDp));
            return d;
        }

        public static GradientDrawable Stroke(int fill, float radiusDp, int strokeColor, float strokeDp)
        {
            var d = new GradientDrawable();
            d.SetColor(fill);
            d.SetCornerRadius(Dpf(radiusDp));
            d.SetStroke(Math.Max(1, Dp(strokeDp)), new Color(strokeColor));
            return d;
        }

        /// <summary>亚克力卡片：半透明填充 + 高光描边。</summary>
        public static Drawable GlassCard()
        {
            var d = new GradientDrawable(GradientDrawable.Orientation.TopBottom,
                new[] { Mix(CardBg, Color.White, Dark ? 0.05f : 0.32f), CardBg });
            d.SetCornerRadius(Dpf(22));
            d.SetStroke(Math.Max(1, Dp(1)), new Color(CardBorder));
            return d;
        }

        public static Drawable Ripple(int rippleColor, Drawable content)
        {
            return new RippleDrawable(ColorStateList.ValueOf(new Color(rippleColor)), content, null);
        }

        public static Drawable AccentRipple(Drawable content)
            => Ripple(WithAlpha(Accent, 40), content);

        public static Drawable InputFieldBg()
        {
            var normal = Stroke(InputBg, 14, InputBorder, 1.2f);
            var focused = Stroke(InputBg, 14, WithAlpha(Accent, 210), 1.8f);
            var sl = new StateListDrawable();
            sl.AddState(new[] { Android.Resource.Attribute.StateFocused }, focused);
            sl.AddState(new int[] { }, normal);
            return sl;
        }

        // ---------- 视图工厂 ----------

        public static TextView Label(string text, float sp, int color, bool medium = false)
        {
            var tv = new TextView(Ctx);
            tv.Text = text;
            tv.SetTextSize(Android.Util.ComplexUnitType.Sp, sp);
            tv.SetTextColor(new Color(color));
            tv.SetTypeface(medium ? Medium : Regular, TypefaceStyle.Normal);
            tv.SetIncludeFontPadding(false);
            return tv;
        }

        public static LinearLayout VBox()
        {
            var l = new LinearLayout(Ctx);
            l.Orientation = Android.Widget.Orientation.Vertical;
            return l;
        }

        public static LinearLayout HBox()
        {
            var l = new LinearLayout(Ctx);
            l.Orientation = Android.Widget.Orientation.Horizontal;
            l.SetGravity(GravityFlags.CenterVertical);
            return l;
        }

        public static View Spacer(int heightDp)
            => new View(Ctx) { LayoutParameters = new LinearLayout.LayoutParams(1, Dp(heightDp)) };

        public static View Gap(int widthDp)
            => new View(Ctx) { LayoutParameters = new LinearLayout.LayoutParams(Dp(widthDp), 1) };

        public static ImageView Icon(int resId, int sizeDp, int tint)
        {
            var iv = new ImageView(Ctx);
            iv.SetImageResource(resId);
            iv.SetScaleType(ImageView.ScaleType.CenterInside);
            iv.SetColorFilter(new Color(tint));
            var lp = new LinearLayout.LayoutParams(Dp(sizeDp), Dp(sizeDp));
            iv.LayoutParameters = lp;
            return iv;
        }
    }

    /// <summary>按压反馈：轻微缩放。注意 OnTouch 必须返回 false，否则 View 的 Click 不会触发。</summary>
    public static class Touch
    {
        private sealed class PressListener : Java.Lang.Object, View.IOnTouchListener
        {
            private readonly float _scale;
            public PressListener(float scale) { _scale = scale; }

            public bool OnTouch(View v, MotionEvent e)
            {
                if (e == null) return false;
                switch (e.ActionMasked)
                {
                    case MotionEventActions.Down:
                        v.Animate().ScaleX(_scale).ScaleY(_scale).SetDuration(90).Start();
                        break;
                    case MotionEventActions.Up:
                    case MotionEventActions.Cancel:
                        v.Animate().ScaleX(1f).ScaleY(1f).SetDuration(180).Start();
                        break;
                }
                return false;
            }
        }

        public static void Press(View v, float scale = 0.96f)
            => v.SetOnTouchListener(new PressListener(scale));

        public static void Bind(View v, Action action, float scale = 0.96f)
        {
            Press(v, scale);
            v.Click += (s, e) => action();
        }
    }
}
