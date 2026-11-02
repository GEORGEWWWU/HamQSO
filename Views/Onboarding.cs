using System;
using System.Text;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using HamJi.Core;

namespace HamJi.Views
{
    /// <summary>
    /// 首次启动的全屏引导：标准 logo（原图，不加容器）+ 呼号输入。
    /// 界面只走黑白，不套主题色；没有返回入口，只能「确定」或「我就看看」
    /// （后者下次启动还会再弹）。
    /// </summary>
    public class OnboardingView : FrameLayout
    {
        /// <summary>填写并确认（参数为已归一化的合法呼号）。</summary>
        public event Action<string> Confirmed;
        /// <summary>点了「我就看看」。</summary>
        public event Action Skipped;

        private EditText _input;
        private TextView _error;

        private readonly int _bg;
        private readonly int _ink;
        private readonly int _soft;
        private readonly int _field;

        public OnboardingView(Context c) : base(c)
        {
            Clickable = true;
            Focusable = true;
            Elevation = Dui.Dpf(40);

            // 黑白两档，跟着系统深浅色反转，不掺主题色
            _bg = Dui.Dark ? Color.Black : Color.White;
            _ink = Dui.Dark ? Color.White : Color.Black;
            _soft = Dui.Dark ? Color.Argb(255, 138, 138, 138) : Color.Argb(255, 150, 150, 150);
            _field = Dui.Dark ? Color.Argb(255, 20, 20, 20) : Color.Argb(255, 244, 244, 244);

            Build();
        }

        private void Build()
        {
            SetBackgroundColor(new Color(_bg));

            var scroll = new ScrollView(Context);
            scroll.FillViewport = true;
            scroll.VerticalScrollBarEnabled = false;
            AddView(scroll, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

            var col = Dui.VBox();
            col.SetGravity(GravityFlags.Center);
            col.SetPadding(Dui.Dp(32), Dui.Dp(40), Dui.Dp(32), Dui.Dp(40));
            scroll.AddView(col, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            // ---- 标准 logo：直接用 App / README 那张原图，不加任何底色和容器 ----
            var logo = new ImageView(Context);
            logo.SetImageResource(Resource.Drawable.logo_std);
            logo.SetScaleType(ImageView.ScaleType.FitCenter);
            col.AddView(logo, new LinearLayout.LayoutParams(Dui.Dp(112), Dui.Dp(112))
            { Gravity = GravityFlags.CenterHorizontal });

            // ---- 标题 ----
            var title = Dui.Label("Ham记", 26f, _ink, true);
            title.LetterSpacing = 0.02f;
            title.Gravity = GravityFlags.Center;
            col.AddView(title, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
            { TopMargin = Dui.Dp(22), Gravity = GravityFlags.CenterHorizontal });

            // ---- 输入框 ----
            _input = new EditText(Context);
            _input.Hint = "BH7IDW";
            _input.SetTextSize(Android.Util.ComplexUnitType.Sp, 22f);
            _input.SetTextColor(new Color(_ink));
            _input.SetHintTextColor(new Color(_soft));
            _input.SetTypeface(Typeface.Create("monospace", TypefaceStyle.Normal), TypefaceStyle.Normal);
            _input.LetterSpacing = 0.16f;
            _input.Gravity = GravityFlags.Center;
            var fieldBg = new GradientDrawable();
            fieldBg.SetColor(_field);
            fieldBg.SetCornerRadius(Dui.Dpf(14));
            _input.SetBackgroundDrawable(fieldBg);
            _input.SetPadding(Dui.Dp(16), 0, Dui.Dp(16), 0);
            _input.SetIncludeFontPadding(false);
            _input.SetSingleLine(true);
            _input.InputType = InputTypes.ClassText | InputTypes.TextFlagCapCharacters | InputTypes.TextFlagNoSuggestions;
            _input.SetFilters(new Android.Text.IInputFilter[] { new InputFilterLengthFilter(6), new CallFilter() });
            _input.EditorAction += (s, e) =>
            {
                if (e.ActionId == ImeAction.Done)
                {
                    e.Handled = true;
                    Confirm();
                }
            };
            _input.TextChanged += (s, e) => OnInputChanged();
            col.AddView(_input, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dui.Dp(58))
            { TopMargin = Dui.Dp(34) });

            // ---- 规则 ----
            var rule = Dui.Label("5-6 位，字母和数字组合", 11f, _soft, false);
            rule.Gravity = GravityFlags.Center;
            col.AddView(rule, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
            { TopMargin = Dui.Dp(10), Gravity = GravityFlags.CenterHorizontal });

            _error = Dui.Label("", 11.5f, Dui.Danger, false);
            _error.Gravity = GravityFlags.Center;
            _error.Visibility = ViewStates.Gone;
            col.AddView(_error, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
            { TopMargin = Dui.Dp(8), Gravity = GravityFlags.CenterHorizontal });

            // ---- 主按钮：半宽居中，不跟输入框等宽 ----
            var go = new TextView(Context);
            go.Text = "确定";
            go.Gravity = GravityFlags.Center;
            go.SetTextSize(Android.Util.ComplexUnitType.Sp, 15f);
            go.SetTextColor(new Color(_bg));
            go.SetTypeface(Dui.Medium, TypefaceStyle.Normal);
            var btnBg = new GradientDrawable();
            btnBg.SetColor(_ink);
            btnBg.SetCornerRadius(Dui.Dpf(26));
            go.SetBackgroundDrawable(btnBg);
            go.Clickable = true;
            HamJi.Core.Touch.Bind(go, Confirm, 0.97f);

            var btnRow = Dui.HBox();
            btnRow.AddView(new View(Context), new LinearLayout.LayoutParams(0, 1, 1f));
            btnRow.AddView(go, new LinearLayout.LayoutParams(Dui.Dp(132), Dui.Dp(48)));
            btnRow.AddView(new View(Context), new LinearLayout.LayoutParams(0, 1, 1f));
            col.AddView(btnRow, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            { TopMargin = Dui.Dp(30) });

            // ---- 我就看看 ----
            var skip = Dui.Label("我就看看", 13f, _soft, false);
            skip.Gravity = GravityFlags.Center;
            skip.SetPadding(Dui.Dp(18), Dui.Dp(10), Dui.Dp(18), Dui.Dp(10));
            skip.Clickable = true;
            skip.Focusable = true;
            skip.SetBackgroundDrawable(Dui.Ripple(
                Dui.WithAlpha(_ink, Dui.Dark ? 26 : 14), Dui.Round(Color.Transparent, 12)));
            skip.Click += (s, e) => Skipped?.Invoke();
            col.AddView(skip, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
            { TopMargin = Dui.Dp(12), Gravity = GravityFlags.CenterHorizontal });
        }

        /// <summary>进场：淡入，避免生硬地砸在屏幕上。不自动弹键盘，先把整页看清。</summary>
        public void PlayIn()
        {
            Alpha = 0f;
            Animate().Alpha(1f).SetDuration(240).Start();
        }

        /// <summary>输入即转大写，输入法没走过滤器时也不漏。</summary>
        private void OnInputChanged()
        {
            var t = _input.Text ?? "";
            var up = t.ToUpperInvariant();
            if (up != t)
            {
                int pos = Math.Max(0, Math.Min(_input.SelectionStart, up.Length));
                _input.SetText(new Java.Lang.String(up), TextView.BufferType.Editable);
                try { _input.SetSelection(Math.Min(pos, up.Length)); } catch { }
            }
            if (_error.Visibility == ViewStates.Visible) _error.Visibility = ViewStates.Gone;
        }

        private void Confirm()
        {
            var raw = _input.Text;
            string err;
            if (!Profile.TrySave(raw, out err))
            {
                ShowError(err);
                _input.Animate().Cancel();
                _input.Animate().TranslationX(Dui.Dpf(7)).SetDuration(60)
                    .WithEndAction(new Java.Lang.Runnable(() =>
                        _input.Animate().TranslationX(-Dui.Dpf(5)).SetDuration(60)
                            .WithEndAction(new Java.Lang.Runnable(() =>
                                _input.Animate().TranslationX(0f).SetDuration(70).Start()))
                            .Start()))
                    .Start();
                return;
            }

            HideKeyboard();
            Confirmed?.Invoke(Profile.Call);
        }

        public void HideKeyboard()
        {
            try
            {
                var imm = Context.GetSystemService(Context.InputMethodService) as InputMethodManager;
                imm?.HideSoftInputFromWindow(_input.WindowToken, 0);
                _input.ClearFocus();
            }
            catch { }
        }

        private void ShowError(string msg)
        {
            _error.Text = msg;
            _error.Visibility = ViewStates.Visible;
        }

        /// <summary>只放行字母和数字，并实时转大写 —— 用户怎么敲都不会因为格式被拒。</summary>
        private sealed class CallFilter : Java.Lang.Object, IInputFilter
        {
            public Java.Lang.ICharSequence FilterFormatted(Java.Lang.ICharSequence source, int start, int end,
                ISpanned dest, int dstart, int dend)
            {
                if (source == null) return null;
                var raw = source.SubSequenceFormatted(start, end).ToString();
                var sb = new StringBuilder(raw.Length);
                foreach (char ch in raw.ToUpperInvariant())
                    if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9')) sb.Append(ch);
                var clean = sb.ToString();
                if (clean == raw) return null;      // 没改动，交给系统处理
                return new Java.Lang.String(clean);
            }
        }
    }
}
