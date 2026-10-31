using System;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Views;
using Android.Widget;
using Android.Animation;
using Android.Views.Animations;
using HamJi.Core;

namespace HamJi.Views
{
    public class NavItemView : FrameLayout
    {
        public ImageView Icon { get; private set; }
        public TextView Label { get; private set; }
        public int Index { get; set; }

        public NavItemView(Context c, int iconRes, string label, int index) : base(c)
        {
            Index = index;
            Clickable = false;
            Focusable = false;

            var col = Dui.VBox();
            col.SetGravity(GravityFlags.Center);
            Icon = Dui.Icon(iconRes, 25, Dui.NavIcon);
            Label = Dui.Label(label, 10.5f, Dui.NavIcon, false);
            Label.LetterSpacing = 0.02f;
            var lp = new LinearLayout.LayoutParams(Dui.Dp(25), Dui.Dp(25)) { Gravity = GravityFlags.CenterHorizontal };
            col.AddView(Icon, lp);
            var tlp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
            { Gravity = GravityFlags.CenterHorizontal };
            tlp.TopMargin = Dui.Dp(4);
            col.AddView(Label, tlp);

            AddView(col, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent)
            { Gravity = GravityFlags.Center });
        }

        public void SetSelected(bool selected, bool animate)
        {
            int iconColor = selected ? Dui.NavIconOn : Dui.NavIcon;
            Icon.SetColorFilter(new Color(iconColor));
            Label.SetTextColor(new Color(iconColor));
            Label.SetTypeface(selected ? Dui.Medium : Dui.Regular, TypefaceStyle.Normal);

            if (animate)
            {
                Icon.Animate().ScaleX(selected ? 1.1f : 1f).ScaleY(selected ? 1.1f : 1f)
                    .SetDuration(220).SetInterpolator(new OvershootInterpolator(1.6f)).Start();
            }
            else
            {
                Icon.ScaleX = selected ? 1.1f : 1f;
                Icon.ScaleY = selected ? 1.1f : 1f;
            }
        }
    }

    /// <summary>
    /// 底部常驻悬浮胶囊导航。选中态是一块独立滑块：
    /// 点击时弹簧滑到目标，按住可拖拽（带放大反馈），松手按位置/速度弹到最近的 tab。
    /// </summary>
    public class NavBar : GlassPanel
    {
        public event Action<int> Selected;

        private readonly NavItemView[] _items = new NavItemView[3];
        private readonly View _indicator;
        private readonly LinearLayout _row;
        private readonly Spring _spring;

        private int _current = -1;
        private float _itemW;
        private float _iw;
        private int _slopPx;

        private bool _dragging;
        private bool _moved;
        private float _downX, _lastX, _vel;
        private long _lastT;
        private int _downIndex;
        private long _swallowUntil;

        public int Current => _current;

        public NavBar(Context c, View backdrop) : base(c)
        {
            Backdrop = backdrop;
            SetRadiusDp(33);
            BuildBackgroundDrawable();
            Clickable = true;
            Focusable = true;

            _indicator = new View(c);
            _indicator.SetBackgroundDrawable(Dui.Round(Dui.AccentSoft, 26));
            AddView(_indicator, new FrameLayout.LayoutParams(Dui.Dp(90), Dui.Dp(52))
            { Gravity = GravityFlags.Left | GravityFlags.CenterVertical });

            _row = Dui.HBox();
            _row.SetClipChildren(false);
            _items[0] = new NavItemView(c, Resource.Drawable.ic_freq, "通联频率", 0);
            _items[1] = new NavItemView(c, Resource.Drawable.ic_log, "通联日志", 1);
            _items[2] = new NavItemView(c, Resource.Drawable.ic_settings, "设置", 2);
            foreach (var it in _items)
                _row.AddView(it, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MatchParent, 1f));
            AddView(_row, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

            _spring = new Spring(x => { if (_indicator != null) _indicator.TranslationX = x; });
            _slopPx = Dui.Dp(6);
            Elevation = Dui.Dpf(14);
        }

        protected override void OnAttachedToWindow()
        {
            base.OnAttachedToWindow();
            // 冷启动瞬间可能收到上一屏残留的触摸事件（会误切 tab），前 600ms 内忽略
            _swallowUntil = Android.OS.SystemClock.UptimeMillis() + 600;
        }

        protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
        {
            base.OnSizeChanged(w, h, oldw, oldh);
            _itemW = w / 3f;
            _iw = Math.Max(Dui.Dp(64), _itemW - Dui.Dp(10));
            var lp = (FrameLayout.LayoutParams)_indicator.LayoutParameters;
            lp.Width = (int)_iw;
            lp.Height = Math.Max(Dui.Dp(40), h - Dui.Dp(12));
            _indicator.LayoutParameters = lp;
            if (!_dragging)
            {
                _spring.Stop();
                _indicator.TranslationX = XOf(Math.Max(0, _current));
                _spring.Sync(_indicator.TranslationX);
            }
        }

        private float XOf(int index) => index * _itemW + (_itemW - _iw) / 2f;

        private int IndexAt(float x)
        {
            int i = (int)Math.Floor(x / Math.Max(1, _itemW));
            return Math.Max(0, Math.Min(2, i));
        }

        public void Select(int index, bool animate)
        {
            if (index < 0 || index > 2) return;
            if (index != _current)
            {
                _current = index;
                foreach (var it in _items) it.SetSelected(it.Index == index, animate);
            }
            if (_dragging) return;
            _spring.Sync(_indicator.TranslationX);
            if (animate) _spring.To(XOf(index));
            else { _spring.Stop(); _indicator.TranslationX = XOf(index); }
        }

        private void Settle(int index, bool animate)
        {
            index = Math.Max(0, Math.Min(2, index));
            bool changed = index != _current;
            int old = _current;
            _current = index;
            foreach (var it in _items) it.SetSelected(it.Index == index, animate);
            _spring.Sync(_indicator.TranslationX);
            if (animate) _spring.To(XOf(index));
            else { _spring.Stop(); _indicator.TranslationX = XOf(index); }
            if (changed)
            {
                // 轻点切换：朝移动方向先拉长再弹回，做出"水滴被甩过去"的味道
                if (animate)
                {
                    _indicator.Animate().Cancel();
                    _indicator.PivotX = index > old ? _indicator.Width : 0f;
                    _indicator.ScaleX = 1.16f;
                    _indicator.Animate().ScaleX(1f).SetDuration(460)
                        .SetInterpolator(new OvershootInterpolator(3.2f)).Start();
                }
                Selected?.Invoke(index);
            }
        }

        private int IndexFromDrag(float velocity)
        {
            float center = _indicator.TranslationX + _iw / 2f;
            int nearest = (int)Math.Round((center - _itemW / 2f) / Math.Max(1, _itemW));
            nearest = Math.Max(0, Math.Min(2, nearest));

            // 快速甩动时按方向推进，模拟 iOS 26 的 flick 手感
            if (Math.Abs(velocity) > Dui.Dp(700))
            {
                int dir = velocity > 0 ? 1 : -1;
                int flicked = Math.Max(0, Math.Min(2, _downIndex + dir));
                if (flicked != nearest) nearest = flicked;
            }
            return nearest;
        }

        public override bool OnTouchEvent(MotionEvent e)
        {
            if (e == null) return false;
            switch (e.ActionMasked)
            {
                case MotionEventActions.Down:
                    if (Android.OS.SystemClock.UptimeMillis() < _swallowUntil) return true;
                    _dragging = true;
                    _moved = false;
                    _downX = e.GetX();
                    _lastX = _downX;
                    _lastT = e.EventTime;
                    _vel = 0;
                    _downIndex = IndexAt(_downX);
                    _spring.Stop();
                    _indicator.Animate().Cancel();
                    _indicator.PivotX = _indicator.Width / 2f;
                    _indicator.ScaleX = 1f;
                    _indicator.Animate().ScaleY(1.07f).SetDuration(130)
                        .SetInterpolator(new DecelerateInterpolator()).Start();
                    return true;

                case MotionEventActions.Move:
                    if (!_dragging) return true;
                    float dx = e.GetX() - _downX;
                    if (!_moved && Math.Abs(dx) > _slopPx) _moved = true;
                    if (_moved)
                    {
                        float x = e.GetX() - _iw / 2f;
                        x = Math.Max(0, Math.Min(Width - _iw, x));
                        _indicator.TranslationX = x;
                        _spring.Sync(x);
                        long dt = e.EventTime - _lastT;
                        if (dt > 0) _vel = (e.GetX() - _lastX) * 1000f / dt;
                        _lastX = e.GetX();
                        _lastT = e.EventTime;

                        // 果冻：速度越快横向拉得越长，方向跟随移动方向
                        float stretch = 1f + Math.Min(0.45f, Math.Abs(_vel) / Dui.Dpf(2200f));
                        _indicator.PivotX = _vel >= 0 ? _indicator.Width : 0f;
                        _indicator.ScaleX = stretch;
                    }
                    return true;

                case MotionEventActions.Up:
                case MotionEventActions.Cancel:
                    if (!_dragging) return true;
                    _dragging = false;
                    _indicator.Animate().Cancel();
                    _indicator.Animate().ScaleX(1f).ScaleY(1f).SetDuration(420)
                        .SetInterpolator(new OvershootInterpolator(2.8f)).Start();
                    int target = _moved ? IndexFromDrag(_vel) : _downIndex;
                    Settle(target, true);
                    return true;
            }
            return base.OnTouchEvent(e);
        }

        /// <summary>欠阻尼弹簧：位置驱动 UI，肉眼就是那种 Q 弹回弹。</summary>
        private sealed class Spring : Java.Lang.Object, Choreographer.IFrameCallback
        {
            private readonly Action<float> _apply;
            private float _x, _vx, _target;
            private bool _running;
            private long _last;
            private const float K = 240f;
            private const float C = 26f;

            public Spring(Action<float> apply) { _apply = apply; }

            public void To(float target)
            {
                _target = target;
                if (_running) return;
                _running = true;
                _last = 0;
                Choreographer.Instance.PostFrameCallback(this);
            }

            public void Stop()
            {
                _running = false;
                _last = 0;
            }

            /// <summary>把内部位置同步到当前视觉位置（拖拽后调用），否则弹簧会从旧位置闪回。</summary>
            public void Sync(float x)
            {
                _x = x;
            }

            public void DoFrame(long frameTimeNanos)
            {
                if (!_running) return;
                if (_last == 0) _last = frameTimeNanos;
                float dt = (frameTimeNanos - _last) / 1e9f;
                _last = frameTimeNanos;
                if (dt <= 0f) dt = 0.016f;
                if (dt > 0.05f) dt = 0.05f;

                int steps = 4;
                float h = dt / steps;
                for (int i = 0; i < steps; i++)
                {
                    float a = -K * (_x - _target) - C * _vx;
                    _vx += a * h;
                    _x += _vx * h;
                }

                if (Math.Abs(_x - _target) < 0.5f && Math.Abs(_vx) < 8f)
                {
                    _x = _target;
                    _apply(_x);
                    _running = false;
                    _last = 0;
                    return;
                }
                _apply(_x);
                Choreographer.Instance.PostFrameCallback(this);
            }
        }
    }

    /// <summary>悬浮新建按钮：渐变圆形 + 阴影 + 按压反馈。</summary>
    public class Fab : FrameLayout
    {
        public Fab(Context c) : base(c)
        {
            Clickable = true;
            Focusable = true;

            var bg = new GradientDrawable(GradientDrawable.Orientation.TopBottom, new[] { Dui.AccentFill, Dui.AccentFill2 });
            bg.SetCornerRadius(Dui.Dpf(30));
            Background = Dui.Ripple(Dui.WithAlpha(Color.White, 60), bg);
            ClipToOutline = true;
            OutlineProvider = ViewOutlineProvider.Background;
            Elevation = Dui.Dpf(14);

            var icon = Dui.Icon(Resource.Drawable.ic_plus, 26, Color.White);
            var lp = new FrameLayout.LayoutParams(Dui.Dp(26), Dui.Dp(26)) { Gravity = GravityFlags.Center };
            AddView(icon, lp);

            HamJi.Core.Touch.Press(this, 0.92f);
        }
    }

    /// <summary>悬浮圆形图标按钮（云上传等次级入口）。</summary>
    public class RoundIconButton : FrameLayout
    {
        public RoundIconButton(Context c, int iconRes) : base(c)
        {
            Clickable = true;
            Focusable = true;

            var bg = Dui.Stroke(Dui.WithAlpha(Dui.CardBg, 235), 23, Dui.CardBorder, 1);
            Background = Dui.Ripple(Dui.WithAlpha(Dui.Accent, 40), bg);
            ClipToOutline = true;
            OutlineProvider = ViewOutlineProvider.Background;
            Elevation = Dui.Dpf(8);

            var icon = Dui.Icon(iconRes, 22, Dui.Accent);
            AddView(icon, new FrameLayout.LayoutParams(Dui.Dp(22), Dui.Dp(22)) { Gravity = GravityFlags.Center });
            HamJi.Core.Touch.Press(this, 0.9f);
        }
    }
}
