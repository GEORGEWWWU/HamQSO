using System;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using HamJi.Core;

namespace HamJi.Views
{
    /// <summary>
    /// 悬浮在屏幕正中的日期选择器（搜索页用）。
    /// 月历本体和新建页底部弹层是同一份 <see cref="DayGrid"/>，只是外壳不同：
    /// 窗口全屏 + 透明遮罩，卡片居中、四角全圆角，进出场是缩放 + 淡入淡出。
    /// </summary>
    public class CenterDayDialog : Dialog
    {
        public event Action<DateTime> Picked;
        public event Action Cleared;

        private readonly LinearLayout _card;
        private bool _introDone;
        private bool _closing;

        public CenterDayDialog(Context c, DateTime current, bool allowClear)
            : base(c, Resource.Style.TransparentDialogTheme)
        {
            Window.RequestFeature(WindowFeatures.NoTitle);
            var win = Window;
            win.SetBackgroundDrawable(new ColorDrawable(Color.Transparent));
            win.AddFlags(WindowManagerFlags.DimBehind);
            win.SetDimAmount(0.42f);

            var lp = win.Attributes;
            lp.Gravity = GravityFlags.Center;
            lp.Width = ViewGroup.LayoutParams.MatchParent;
            lp.Height = ViewGroup.LayoutParams.MatchParent;
            win.Attributes = lp;
            SetCanceledOnTouchOutside(true);

            var root = new FrameLayout(c);

            // 全屏窗口后「点外面关闭」要自己实现
            var scrim = new View(c);
            scrim.Clickable = true;
            scrim.Click += (s, e) => Dismiss();
            root.AddView(scrim, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

            var card = Dui.VBox();
            var bg = new GradientDrawable();
            bg.SetColor(new Color(Dui.SheetBg));
            bg.SetCornerRadius(Dui.Dpf(24));
            card.SetBackgroundDrawable(bg);
            card.SetPadding(Dui.Dp(18), Dui.Dp(16), Dui.Dp(18), Dui.Dp(16));
            card.ClipToOutline = true;
            card.OutlineProvider = ViewOutlineProvider.Background;
            card.Elevation = Dui.Dpf(20);
            _card = card;

            var head = Dui.HBox();
            var title = Dui.Label("选择日期", 16f, Dui.TextPrimary, true);
            head.AddView(title, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
            var close = new FrameLayout(c);
            close.Clickable = true;
            var ci = Dui.Icon(Resource.Drawable.ic_close, 16, Dui.TextTertiary);
            close.AddView(ci, new FrameLayout.LayoutParams(Dui.Dp(16), Dui.Dp(16)) { Gravity = GravityFlags.Center });
            HamJi.Core.Touch.Bind(close, () => Dismiss(), 0.9f);
            head.AddView(close, new LinearLayout.LayoutParams(Dui.Dp(32), Dui.Dp(32)));
            card.AddView(head, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            DayGrid.Build(card, DateTime.Today, current.Date,
                day =>
                {
                    try { Picked?.Invoke(day); } catch { }
                    Dismiss();
                },
                allowClear ? "不限" : null,
                () =>
                {
                    try { Cleared?.Invoke(); } catch { }
                    Dismiss();
                });

            int screenW = c.Resources.DisplayMetrics.WidthPixels;
            int cardW = Math.Min(screenW - Dui.Dp(56), Dui.Dp(340));
            root.AddView(card, new FrameLayout.LayoutParams(cardW, ViewGroup.LayoutParams.WrapContent)
            { Gravity = GravityFlags.Center });

            SetContentView(root);

            card.AddOnLayoutChangeListener(new IntroWatcher(this));
        }

        public override void Show()
        {
            base.Show();
            try { Window?.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent); }
            catch { }
        }

        private void PlayIntro()
        {
            if (_introDone || _card == null || _card.Height <= 0) return;
            _introDone = true;
            _card.PivotX = _card.Width / 2f;
            _card.PivotY = _card.Height / 2f;
            _card.Alpha = 0f;
            _card.ScaleX = 0.92f;
            _card.ScaleY = 0.92f;
            _card.Animate().Alpha(1f).ScaleX(1f).ScaleY(1f).SetDuration(200)
                .SetInterpolator(new Android.Views.Animations.DecelerateInterpolator(1.6f)).Start();
        }

        private void DismissNow()
        {
            try { base.Dismiss(); } catch { }
        }

        public override void Dismiss()
        {
            if (_closing) return;
            _closing = true;
            if (_card == null || _card.Height <= 0) { DismissNow(); return; }
            _card.Animate().Cancel();
            _card.Animate().Alpha(0f).ScaleX(0.96f).ScaleY(0.96f).SetDuration(150)
                .SetInterpolator(new Android.Views.Animations.AccelerateInterpolator(1.4f))
                .WithEndAction(new Java.Lang.Runnable(() => DismissNow())).Start();
        }

        private sealed class IntroWatcher : Java.Lang.Object, View.IOnLayoutChangeListener
        {
            private readonly CenterDayDialog _o;
            public IntroWatcher(CenterDayDialog o) { _o = o; }

            public void OnLayoutChange(View v, int l, int t, int r, int b, int ol, int ot, int or, int ob)
            {
                if (b - t <= 0) return;
                v.RemoveOnLayoutChangeListener(this);
                _o.PlayIntro();
            }
        }
    }
}
