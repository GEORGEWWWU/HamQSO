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
    /// 底部弹出的选择器基类：
    /// 1) 窗口做成「全屏 + 卡片贴底」，卡片背景才能一直铺到物理屏幕底部，
    ///    否则系统手势条那条区域会露出下面的新建页；
    /// 2) 进出场动画自己做（卡片 TranslationY 只往下走），不用窗口动画，方向可控；
    /// 3) 只能向下拖动关闭，向上一点都动不了。
    /// </summary>
    public abstract class BottomSheetDialog : Dialog
    {
        protected LinearLayout Card;

        private float _downRawY, _lastRawY, _vel;
        private long _lastT;
        private bool _dragging, _dragAllowed, _closing, _introDone;
        private int _slop;

        protected BottomSheetDialog(Context c) : base(c)
        {
            _slop = Dui.Dp(6);
        }

        internal void AttachCard(LinearLayout card)
        {
            Card = card;
            card.AddOnLayoutChangeListener(new IntroWatcher(this));
        }

        public override void Show()
        {
            base.Show();
            try { Window?.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent); }
            catch { }
        }

        /// <summary>首次布局完成后把卡片从下方滑入。</summary>
        internal void PlayIntro()
        {
            if (_introDone || Card == null || Card.Height <= 0) return;
            _introDone = true;
            Card.TranslationY = Card.Height;
            Card.Animate().TranslationY(0f).SetDuration(240)
                .SetInterpolator(new Android.Views.Animations.DecelerateInterpolator(1.5f)).Start();
        }

        private void DismissNow()
        {
            try { base.Dismiss(); } catch { }
        }

        /// <summary>关闭统一走这里：卡片先向下滑出，再真正消失。</summary>
        public override void Dismiss()
        {
            if (_closing) return;
            if (Card == null || Card.Height <= 0) { _closing = true; DismissNow(); return; }
            _closing = true;
            Card.Animate().Cancel();
            Card.Animate().TranslationY(Card.Height).SetDuration(200)
                .SetInterpolator(new Android.Views.Animations.AccelerateInterpolator(1.4f))
                .WithEndAction(new Java.Lang.Runnable(() => DismissNow())).Start();
        }

        private sealed class IntroWatcher : Java.Lang.Object, View.IOnLayoutChangeListener
        {
            private readonly BottomSheetDialog _o;
            public IntroWatcher(BottomSheetDialog o) { _o = o; }
            public void OnLayoutChange(View v, int l, int t, int r, int b, int ol, int ot, int or, int ob)
            {
                if (b - t <= 0) return;
                v.RemoveOnLayoutChangeListener(this);
                _o.PlayIntro();
            }
        }

        internal bool DragIntercept(MotionEvent e)
        {
            switch (e.ActionMasked)
            {
                case MotionEventActions.Down:
                    _downRawY = e.RawY;
                    _lastRawY = _downRawY;
                    _lastT = e.EventTime;
                    _vel = 0;
                    _dragging = false;
                    // 只有从顶部把手/标题区域起手才算拖拽，否则会把时/分列表的滚动抢走
                    _dragAllowed = e.GetY() <= Dui.Dp(96);
                    return false;
                case MotionEventActions.Move:
                    if (!_dragAllowed) return false;
                    if (!_dragging && e.RawY - _downRawY > _slop) _dragging = true;
                    return _dragging;
            }
            return false;
        }

        internal bool DragTouch(MotionEvent e)
        {
            if (Card == null) return false;
            switch (e.ActionMasked)
            {
                case MotionEventActions.Down:
                    return true;

                case MotionEventActions.Move:
                    // 只允许向下：向上拖一点都不跟手，卡片纹丝不动
                    float dy = Math.Max(0f, e.RawY - _downRawY);
                    Card.TranslationY = dy;
                    long dt = e.EventTime - _lastT;
                    if (dt > 0) _vel = (e.RawY - _lastRawY) * 1000f / dt;
                    _lastRawY = e.RawY;
                    _lastT = e.EventTime;
                    return true;

                case MotionEventActions.Up:
                case MotionEventActions.Cancel:
                    bool shouldClose = Card.TranslationY > Card.Height * 0.28f || _vel > Dui.Dpf(900);
                    if (shouldClose)
                    {
                        Dismiss();
                    }
                    else
                    {
                        Card.Animate().TranslationY(0f).SetDuration(220)
                            .SetInterpolator(new Android.Views.Animations.DecelerateInterpolator(1.6f)).Start();
                    }
                    _dragging = false;
                    return true;
            }
            return false;
        }
    }

    /// <summary>可以整卡拖动关闭的容器：竖向手势超过阈值就拦截，点击仍然给子 View。</summary>
    internal sealed class DragCard : LinearLayout
    {
        private readonly BottomSheetDialog _owner;

        public DragCard(Context c, BottomSheetDialog owner) : base(c)
        {
            _owner = owner;
            Orientation = Orientation.Vertical;
        }

        public override bool OnInterceptTouchEvent(MotionEvent e) => _owner.DragIntercept(e);

        public override bool OnTouchEvent(MotionEvent e) => _owner.DragTouch(e);
    }

    /// <summary>底部弹出的选择器外壳：圆角卡片 + 把手 + 标题 + 关闭按钮。</summary>
    internal static class SheetShell
    {
        public static LinearLayout Build(Dialog dlg, string title, out TextView titleView)
        {
            var win = dlg.Window;
            win.SetBackgroundDrawable(new ColorDrawable(Color.Transparent));
            win.AddFlags(WindowManagerFlags.DimBehind);
            win.SetDimAmount(0.45f);
            if ((int)Android.OS.Build.VERSION.SdkInt >= 30)
            {
                try { win.SetDecorFitsSystemWindows(false); } catch { }
            }
            // 关键：窗口做成全屏。只要窗口是 wrap_content，系统就会把它顶在导航栏上方，
            // 卡片下面必然露出一条能看见下层页面的透明缝。
            var lp = win.Attributes;
            lp.Gravity = GravityFlags.Bottom;
            lp.Width = ViewGroup.LayoutParams.MatchParent;
            lp.Height = ViewGroup.LayoutParams.MatchParent;
            win.Attributes = lp;
            dlg.SetCanceledOnTouchOutside(true);

            var root = new FrameLayout(dlg.Context);
            // 全屏窗口后「点外面关闭」要自己实现：上层铺一层透明可点区域
            var scrim = new View(dlg.Context);
            scrim.Clickable = true;
            scrim.Click += (s, e) => dlg.Dismiss();
            root.AddView(scrim, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

            var sheet = dlg as BottomSheetDialog;
            LinearLayout card = sheet != null ? new DragCard(dlg.Context, sheet) : Dui.VBox();
            var bg = new GradientDrawable();
            bg.SetColor(new Color(Dui.SheetBg));
            bg.SetCornerRadii(new[] { Dui.Dpf(24), Dui.Dpf(24), Dui.Dpf(24), Dui.Dpf(24), 0f, 0f, 0f, 0f });
            card.SetBackgroundDrawable(bg);
            card.SetPadding(Dui.Dp(18), Dui.Dp(10), Dui.Dp(18), Dui.Dp(14));
            if (sheet != null) sheet.AttachCard(card);

            // 手势条高度自己补到卡片内边距里，卡片背景就能一直填到屏幕底
            var insets = new BottomPad(card, Dui.Dp(14));
            card.SetOnApplyWindowInsetsListener(insets);
            card.RequestApplyInsets();

            // 把手
            var handleBox = new FrameLayout(dlg.Context);
            var handle = new View(dlg.Context);
            handle.SetBackgroundDrawable(Dui.Round(Dui.WithAlpha(Dui.TextTertiary, 110), 3));
            handleBox.AddView(handle, new FrameLayout.LayoutParams(Dui.Dp(40), Dui.Dp(5)) { Gravity = GravityFlags.Center });
            card.AddView(handleBox, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dui.Dp(20)));

            var head = Dui.HBox();
            titleView = Dui.Label(title, 16f, Dui.TextPrimary, true);
            head.AddView(titleView, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
            var close = new FrameLayout(dlg.Context);
            close.Clickable = true;
            var ci = Dui.Icon(Resource.Drawable.ic_close, 16, Dui.TextTertiary);
            close.AddView(ci, new FrameLayout.LayoutParams(Dui.Dp(16), Dui.Dp(16)) { Gravity = GravityFlags.Center });
            HamJi.Core.Touch.Bind(close, () => dlg.Dismiss(), 0.9f);
            head.AddView(close, new LinearLayout.LayoutParams(Dui.Dp(32), Dui.Dp(32)));
            card.AddView(head, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            dlg.SetContentView(root);
            root.AddView(card, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            { Gravity = GravityFlags.Bottom });
            return card;
        }

        /// <summary>把底部系统栏高度并进卡片内边距，避免手势条压在内容上或背景断开。</summary>
        private sealed class BottomPad : Java.Lang.Object, View.IOnApplyWindowInsetsListener
        {
            private readonly View _v;
            private readonly int _base;
            public BottomPad(View v, int baseBottom) { _v = v; _base = baseBottom; }

            public WindowInsets OnApplyWindowInsets(View view, WindowInsets insets)
            {
                int nav = 0;
                try
                {
                    if ((int)Android.OS.Build.VERSION.SdkInt >= 30) nav = insets.GetInsets(7).Bottom;
#pragma warning disable CS0618
                    else nav = insets.SystemWindowInsetBottom;
#pragma warning restore CS0618
                }
                catch { }
                _v.SetPadding(Dui.Dp(18), Dui.Dp(10), Dui.Dp(18), _base + Math.Max(0, nav));
                return insets;
            }
        }

        public static TextView Chip(string text)
        {
            var tv = Dui.Label(text, 12.5f, Dui.TextSecondary, false);
            tv.Gravity = GravityFlags.Center;
            tv.Clickable = true;
            tv.SetPadding(Dui.Dp(14), 0, Dui.Dp(14), 0);
            tv.SetBackgroundDrawable(Dui.Round(Dui.InputBg, 12));
            HamJi.Core.Touch.Press(tv, 0.95f);
            return tv;
        }
    }

    /// <summary>日期选择：自带月历，替代系统 DatePickerDialog（原生那个和 App 风格完全不搭）。</summary>
    public class DaySheet : BottomSheetDialog
    {
        public event Action<DateTime> Picked;

        private readonly DateTime _today = DateTime.Today;
        private DateTime _sel;
        private DateTime _month;
        private LinearLayout _grid;
        private TextView _monthLabel;

        public DaySheet(Context c, DateTime current) : base(c)
        {
            _sel = current.Date;
            _month = new DateTime(_sel.Year, _sel.Month, 1);
            Window.RequestFeature(WindowFeatures.NoTitle);
            Build();
        }

        private void Build()
        {
            TextView title;
            var card = SheetShell.Build(this, "选择日期", out title);

            // 月份导航
            var nav = Dui.HBox();
            nav.SetPadding(0, Dui.Dp(12), 0, Dui.Dp(4));
            var prev = NavButton("‹");
            var next = NavButton("›");
            prev.Click += (s, e) => Shift(-1);
            next.Click += (s, e) => Shift(1);
            _monthLabel = Dui.Label("", 14f, Dui.TextPrimary, true);
            _monthLabel.Gravity = GravityFlags.Center;
            nav.AddView(prev, new LinearLayout.LayoutParams(Dui.Dp(36), Dui.Dp(36)));
            nav.AddView(_monthLabel, new LinearLayout.LayoutParams(0, Dui.Dp(36), 1f));
            nav.AddView(next, new LinearLayout.LayoutParams(Dui.Dp(36), Dui.Dp(36)));
            card.AddView(nav, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            // 星期表头
            var week = Dui.HBox();
            week.SetPadding(0, Dui.Dp(4), 0, Dui.Dp(2));
            foreach (var w in new[] { "一", "二", "三", "四", "五", "六", "日" })
            {
                var tv = Dui.Label(w, 11f, Dui.TextTertiary, false);
                tv.Gravity = GravityFlags.Center;
                week.AddView(tv, new LinearLayout.LayoutParams(0, Dui.Dp(24), 1f));
            }
            card.AddView(week, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            _grid = Dui.VBox();
            card.AddView(_grid, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            // 快捷
            var quick = Dui.HBox();
            quick.SetPadding(0, Dui.Dp(12), 0, 0);
            AddChip(quick, "今天", () => Pick(_today));
            AddChip(quick, "昨天", () => Pick(_today.AddDays(-1)));
            AddChip(quick, "前天", () => Pick(_today.AddDays(-2)));
            card.AddView(quick, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            RenderMonth();
        }

        private void AddChip(LinearLayout row, string text, Action act)
        {
            var chip = SheetShell.Chip(text);
            chip.Click += (s, e) => act();
            var lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dui.Dp(34));
            lp.RightMargin = Dui.Dp(8);
            row.AddView(chip, lp);
        }

        private TextView NavButton(string glyph)
        {
            var tv = Dui.Label(glyph, 18f, Dui.TextSecondary, false);
            tv.Gravity = GravityFlags.Center;
            tv.Clickable = true;
            tv.SetBackgroundDrawable(Dui.Round(Dui.InputBg, 12));
            HamJi.Core.Touch.Press(tv, 0.92f);
            return tv;
        }

        private void Shift(int months)
        {
            _month = _month.AddMonths(months);
            RenderMonth();
        }

        private void RenderMonth()
        {
            _monthLabel.Text = _month.Year + "年" + _month.Month + "月";
            _grid.RemoveAllViews();

            int offset = (((int)_month.DayOfWeek) + 6) % 7;   // 周一为第一列
            var start = _month.AddDays(-offset);

            for (int r = 0; r < 6; r++)
            {
                var row = Dui.HBox();
                for (int c = 0; c < 7; c++)
                {
                    var day = start.AddDays(r * 7 + c);
                    row.AddView(DayCell(day), new LinearLayout.LayoutParams(0, Dui.Dp(40), 1f));
                }
                _grid.AddView(row, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dui.Dp(40)));
            }
        }

        private View DayCell(DateTime day)
        {
            bool inMonth = day.Month == _month.Month;
            bool selected = day.Date == _sel.Date;
            bool isToday = day.Date == _today;

            var box = new FrameLayout(Context);
            var tv = Dui.Label(day.Day.ToString(), 14f,
                selected ? Color.White : (inMonth ? (isToday ? Dui.Accent : Dui.TextPrimary) : Dui.TextTertiary),
                selected || isToday);
            tv.Gravity = GravityFlags.Center;
            box.AddView(tv, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

            if (selected)
            {
                var g = new GradientDrawable(GradientDrawable.Orientation.TopBottom, new[] { Dui.AccentFill, Dui.AccentFill2 });
                g.SetCornerRadius(Dui.Dpf(20));
                tv.SetBackgroundDrawable(g);
            }
            else if (isToday)
            {
                var ring = new GradientDrawable();
                ring.SetColor(Color.Transparent);
                ring.SetCornerRadius(Dui.Dpf(20));
                ring.SetStroke(Math.Max(1, Dui.Dp(1.2f)), new Color(Dui.WithAlpha(Dui.Accent, 120)));
                tv.SetBackgroundDrawable(ring);
            }

            if (!inMonth) tv.Alpha = 0.45f;

            box.Clickable = true;
            HamJi.Core.Touch.Press(box, 0.94f);
            box.Click += (s, e) => Pick(day);
            return box;
        }

        private void Pick(DateTime day)
        {
            _sel = day.Date;
            try { Picked?.Invoke(day.Date); } catch { }
            Dismiss();
        }
    }

    /// <summary>时间选择：时 / 分两列滚动列表，替代系统 TimePickerDialog。</summary>
    public class TimeSheet : BottomSheetDialog
    {
        public event Action<int, int> Picked;

        private const int ItemH = 42;

        private int _h, _m;
        private readonly TextView[] _hourViews = new TextView[24];
        private readonly TextView[] _minViews = new TextView[60];
        private ScrollView _hourScroll, _minScroll;

        public TimeSheet(Context c, DateTime current) : base(c)
        {
            _h = current.Hour;
            _m = current.Minute;
            Window.RequestFeature(WindowFeatures.NoTitle);
            Build();
        }

        private void Build()
        {
            TextView title;
            var card = SheetShell.Build(this, "选择时间", out title);

            var body = Dui.HBox();
            body.SetPadding(0, Dui.Dp(14), 0, 0);

            _hourScroll = Column(out LinearLayout hourList);
            _minScroll = Column(out LinearLayout minList);
            for (int i = 0; i < 24; i++) hourList.AddView(HourItem(i));
            for (int i = 0; i < 60; i++) minList.AddView(MinItem(i));

            body.AddView(_hourScroll, new LinearLayout.LayoutParams(0, Dui.Dp(ItemH * 6), 1f) { RightMargin = Dui.Dp(10) });
            body.AddView(_minScroll, new LinearLayout.LayoutParams(0, Dui.Dp(ItemH * 6), 1f));
            card.AddView(body, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            // 快捷 + 按钮
            var quick = Dui.HBox();
            quick.SetPadding(0, Dui.Dp(12), 0, 0);
            var now = SheetShell.Chip("现在");
            now.Click += (s, e) => SetNow();
            quick.AddView(now, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dui.Dp(34)));
            card.AddView(quick, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            var btns = Dui.HBox();
            btns.SetPadding(0, Dui.Dp(12), 0, 0);
            var cancel = Btn("取消", false);
            cancel.Click += (s, e) => Dismiss();
            var ok = Btn("确定", true);
            ok.Click += (s, e) =>
            {
                try { Picked?.Invoke(_h, _m); } catch { }
                Dismiss();
            };
            btns.AddView(cancel, new LinearLayout.LayoutParams(0, Dui.Dp(48), 1f) { RightMargin = Dui.Dp(10) });
            btns.AddView(ok, new LinearLayout.LayoutParams(0, Dui.Dp(48), 1f));
            card.AddView(btns, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            PaintAll();
            PostScroll();
        }

        private ScrollView Column(out LinearLayout list)
        {
            var sv = new ScrollView(Context);
            sv.VerticalScrollBarEnabled = false;
            sv.OverScrollMode = OverScrollMode.Never;
            list = Dui.VBox();
            list.SetPadding(0, Dui.Dp(ItemH * 2.5f), 0, Dui.Dp(ItemH * 2.5f));
            sv.AddView(list, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
            return sv;
        }

        private TextView Item(string text, Action onClick)
        {
            var tv = Dui.Label(text, 15f, Dui.TextPrimary, false);
            tv.Gravity = GravityFlags.Center;
            tv.Clickable = true;
            tv.SetBackgroundDrawable(Dui.Round(Color.Transparent, 12));
            tv.Click += (s, e) => onClick();
            HamJi.Core.Touch.Press(tv, 0.95f);
            return tv;
        }

        private View HourItem(int i)
        {
            var tv = Item(i.ToString("00"), () => { _h = i; PaintAll(); });
            _hourViews[i] = tv;
            return Wrap(tv);
        }

        private View MinItem(int i)
        {
            var tv = Item(i.ToString("00"), () => { _m = i; PaintAll(); });
            _minViews[i] = tv;
            return Wrap(tv);
        }

        private View Wrap(TextView tv)
        {
            var box = new FrameLayout(Context);
            box.AddView(tv, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dui.Dp(ItemH - 6))
            {
                Gravity = GravityFlags.Center,
                LeftMargin = Dui.Dp(10),
                RightMargin = Dui.Dp(10)
            });
            return box;
        }

        private void PaintAll()
        {
            Paint(_hourViews, _h);
            Paint(_minViews, _m);
        }

        private void Paint(TextView[] views, int sel)
        {
            for (int i = 0; i < views.Length; i++)
            {
                var tv = views[i];
                if (tv == null) continue;
                bool on = i == sel;
                tv.SetTextColor(new Color(on ? Dui.Accent : Dui.TextPrimary));
                tv.SetTypeface(on ? Dui.Medium : Dui.Regular, TypefaceStyle.Normal);
                tv.SetBackgroundDrawable(Dui.Round(on ? Dui.WithAlpha(Dui.Accent, Dui.Dark ? 52 : 30) : Color.Transparent, 12));
            }
        }

        private void SetNow()
        {
            var n = DateTime.Now;
            _h = n.Hour;
            _m = n.Minute;
            PaintAll();
            PostScroll();
        }

        private void PostScroll()
        {
            _hourScroll?.Post(() =>
            {
                CenterOn(_hourScroll, _hourViews, _h);
                CenterOn(_minScroll, _minViews, _m);
            });
        }

        /// <summary>把选中项滚到视口正中。用子项的真实偏移算，别用「行高×序号」硬推，
        /// 否则列表内边距和布局测量差异会让滚动位置偏掉。</summary>
        private static void CenterOn(ScrollView sv, TextView[] views, int idx)
        {
            if (sv == null || idx < 0 || idx >= views.Length) return;
            var tv = views[idx];
            if (tv?.Parent is not View row) return;
            if (sv.Height <= 0 || row.Height <= 0) return;
            sv.ScrollTo(0, Math.Max(0, row.Top + row.Height / 2 - sv.Height / 2));
        }

        public override void Show()
        {
            base.Show();
            // 窗口真正测量完之后再定位一次，首次打开就能停在当前时间
            PostScroll();
        }

        private TextView Btn(string text, bool primary)
        {
            var tv = Dui.Label(text, 15f, primary ? Color.White : Dui.TextPrimary, true);
            tv.Gravity = GravityFlags.Center;
            tv.Clickable = true;
            if (primary)
            {
                var g = new GradientDrawable(GradientDrawable.Orientation.TopBottom, new[] { Dui.AccentFill, Dui.AccentFill2 });
                g.SetCornerRadius(Dui.Dpf(15));
                tv.SetBackgroundDrawable(g);
            }
            else
            {
                tv.SetBackgroundDrawable(Dui.Round(Dui.InputBg, 15));
            }
            HamJi.Core.Touch.Press(tv, 0.96f);
            return tv;
        }
    }
}
