using System;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using HamJi.Core;

namespace HamJi.Views
{
    /// <summary>
    /// 记录卡片：按住向左拖动滑出圆形删除按钮。
    /// 拖动过程中卡片横向拉伸（非线性阻尼），松手用 Overshoot 回弹。
    /// 删除按钮与卡片之间留出间距，且需要点两次（第一次变深色确认）。
    /// </summary>
    public class SwipeRow : FrameLayout
    {
        public event Action<SwipeRow> Opened;
        public event Action<SwipeRow> DeleteRequested;

        private readonly FrameLayout _action;      // 右侧占位区（透明，只负责摆放圆钮）
        private readonly FrameLayout _circle;      // 圆形删除按钮
        private readonly ImageView _circleIcon;
        private readonly FrameLayout _content;
        private readonly View _inner;

        private readonly float _btnW;
        private readonly float _circleD;
        private readonly float _gap;

        private float _downX, _downY, _startTx;
        private bool _dragging, _tracking, _open, _confirm;
        private readonly int _slop;

        public bool IsOpen => _open;
        public View Inner => _inner;

        public SwipeRow(Context c, View content) : base(c)
        {
            _slop = Dui.Dp(7);
            _gap = Dui.Dp(10);              // 圆钮与卡片之间的间距
            _circleD = Dui.Dp(40);          // 圆钮直径
            _btnW = _circleD + _gap + Dui.Dp(8);  // 卡片让出的总宽度

            var lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            { BottomMargin = Dui.Dp(8) };
            LayoutParameters = lp;

            // 右侧删除按钮：透明占位区 + 圆形填充钮（不铺满、不贴卡片）
            _action = new FrameLayout(c);
            _action.Alpha = 0f;
            _circle = new FrameLayout(c);
            _circle.SetBackgroundDrawable(Dui.Round(Dui.Danger, 20));
            _circle.Clickable = true;
            _circleIcon = Dui.Icon(Resource.Drawable.ic_trash, 19, Color.White);
            _circle.AddView(_circleIcon, new FrameLayout.LayoutParams(Dui.Dp(19), Dui.Dp(19)) { Gravity = GravityFlags.Center });
            var clp = new FrameLayout.LayoutParams((int)_circleD, (int)_circleD)
            { Gravity = GravityFlags.Right | GravityFlags.CenterVertical, RightMargin = Dui.Dp(8) };
            _action.AddView(_circle, clp);
            AddView(_action, new FrameLayout.LayoutParams((int)_btnW, ViewGroup.LayoutParams.MatchParent)
            { Gravity = GravityFlags.Right });

            _circle.Click += (s, e) => OnActionTap();
            HamJi.Core.Touch.Press(_circle, 0.92f);

            // 内容卡片
            _inner = content;
            _content = new FrameLayout(c);
            _content.SetBackgroundDrawable(Dui.GlassCard());
            _content.ClipToOutline = true;
            _content.OutlineProvider = ViewOutlineProvider.Background;
            _content.Elevation = Dui.Dpf(2);
            _content.AddView(_inner, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
            AddView(_content, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
        }

        private void OnActionTap()
        {
            if (!_confirm)
            {
                _confirm = true;
                _circle.SetBackgroundDrawable(Dui.Round(Dui.Mix(Dui.Danger, Color.Black, 0.34f), 20));
                _circleIcon.SetColorFilter(new Color(Dui.WithAlpha(Color.White, 235)));
                return;
            }
            _confirm = false;
            DeleteRequested?.Invoke(this);
        }

        private void ResetConfirm()
        {
            if (!_confirm) return;
            _confirm = false;
            _circle.SetBackgroundDrawable(Dui.Round(Dui.Danger, 20));
            _circleIcon.SetColorFilter(new Color(Color.White));
        }

        public void Close(bool animate = true)
        {
            _open = false;
            ResetConfirm();
            if (animate)
            {
                _content.Animate().TranslationX(0f).ScaleX(1f).SetDuration(280)
                    .SetInterpolator(new OvershootInterpolator(1.4f)).Start();
                _action.Animate().Alpha(0f).SetDuration(200).Start();
            }
            else
            {
                _content.TranslationX = 0f;
                _content.ScaleX = 1f;
                _action.Alpha = 0f;
            }
        }

        private void Settle(bool open)
        {
            _open = open;
            float target = open ? -_btnW : 0f;
            _content.Animate().TranslationX(target).ScaleX(1f).SetDuration(320)
                .SetInterpolator(new OvershootInterpolator(1.35f)).Start();
            _action.Animate().Alpha(open ? 1f : 0f).SetDuration(open ? 160 : 220).Start();
            _circle.Animate().ScaleX(1f).ScaleY(1f).SetDuration(320)
                .SetInterpolator(new OvershootInterpolator(1.6f)).Start();
            _circleIcon.Animate().Alpha(1f).SetDuration(200).Start();
            if (!open) ResetConfirm();
        }

        private void ApplyDeform(float tx)
        {
            float p = Math.Min(1f, Math.Abs(tx) / _btnW);
            _content.PivotX = _content.Width;
            _content.PivotY = _content.Height / 2f;
            _content.ScaleX = 1f + 0.05f * p;

            _action.Alpha = Math.Min(1f, p * 2.4f);
            float s = 0.7f + 0.3f * p;
            _circle.ScaleX = s;
            _circle.ScaleY = s;
        }

        protected override void OnDetachedFromWindow()
        {
            _content?.Animate()?.Cancel();
            base.OnDetachedFromWindow();
        }

        public override bool OnInterceptTouchEvent(MotionEvent e)
        {
            if (e == null) return false;
            switch (e.ActionMasked)
            {
                case MotionEventActions.Down:
                    _downX = e.RawX;
                    _downY = e.RawY;
                    _startTx = _content.TranslationX;
                    _dragging = false;
                    _tracking = true;
                    // 先把手势占住：方向还没出来之前，别让外层 ScrollView 抢去当滚动
                    Parent?.RequestDisallowInterceptTouchEvent(true);
                    // 已展开时：只有点在卡片上才接管（点右侧圆钮要留给子视图）
                    return _open && e.GetX() < Width - _btnW;

                case MotionEventActions.Move:
                    if (!_tracking) return false;
                    float dx = e.RawX - _downX;
                    float dy = e.RawY - _downY;
                    // 明确是上下滑：立刻把手势还给外层，让列表正常滚
                    if (Math.Abs(dy) > _slop && Math.Abs(dy) > Math.Abs(dx) * 1.2f)
                    {
                        _tracking = false;
                        Parent?.RequestDisallowInterceptTouchEvent(false);
                        return false;
                    }
                    if (Math.Abs(dx) > _slop && Math.Abs(dx) > Math.Abs(dy))
                    {
                        _dragging = true;
                        // 一旦认定是左右滑，到松手为止都不许父级 ScrollView 抢走手势
                        Parent?.RequestDisallowInterceptTouchEvent(true);
                        _content.Animate().Cancel();
                        Opened?.Invoke(this);
                        return true;
                    }
                    return false;
            }
            return false;
        }

        public override bool OnTouchEvent(MotionEvent e)
        {
            if (e == null) return false;
            switch (e.ActionMasked)
            {
                case MotionEventActions.Down:
                    return true;

                case MotionEventActions.Move:
                    if (!_dragging) return true;
                    float target = _startTx + (e.RawX - _downX);
                    // 超过展开位置后阻尼跟随，反向只跟 1/4
                    if (target < -_btnW)
                    {
                        float over = -_btnW - target;
                        target = -_btnW - Math.Min(Dui.Dpf(18f), over * 0.28f);
                    }
                    if (target > 0) target *= 0.25f;
                    _content.TranslationX = target;
                    ApplyDeform(target);
                    return true;

                case MotionEventActions.Up:
                case MotionEventActions.Cancel:
                    _tracking = false;
                    try { Parent?.RequestDisallowInterceptTouchEvent(false); } catch { }
                    if (!_dragging)
                    {
                        if (_open && e.ActionMasked == MotionEventActions.Up) Close();
                        return true;
                    }
                    _dragging = false;
                    Settle(_content.TranslationX < -_btnW * 0.45f);
                    return true;
            }
            return base.OnTouchEvent(e);
        }
    }
}
