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
    /// 记录卡片：按住向左拖动滑出删除区；删除按钮需要点两次（第一次变色确认）。
    /// 拖动过程中内容会被横向拉伸，松手用弹簧回弹。
    /// </summary>
    public class SwipeRow : FrameLayout
    {
        public event Action<SwipeRow> Opened;
        public event Action<SwipeRow> DeleteRequested;

        private readonly FrameLayout _action;
        private readonly TextView _actionText;
        private readonly FrameLayout _content;
        private readonly View _inner;

        private float _btnW;
        private float _downX, _downY;
        private float _startTx;
        private bool _dragging;
        private bool _deciding;
        private int _slop;
        private int _confirm;
        private bool _open;
        private bool _closePending;

        public bool IsOpen => _open;
        public View Inner => _inner;

        public SwipeRow(Context c, View content) : base(c)
        {
            _slop = Dui.Dp(7);
            _btnW = Dui.Dp(92);

            var lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            { BottomMargin = Dui.Dp(8) };
            LayoutParameters = lp;

            // 右侧删除区
            _action = new FrameLayout(c);
            _action.SetBackgroundDrawable(Dui.Round(Dui.Danger, 16));
            _actionText = Dui.Label("删除", 13f, Color.White, true);
            _actionText.Gravity = GravityFlags.Center;
            _actionText.Clickable = false;
            _action.AddView(_actionText, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
            AddView(_action, new FrameLayout.LayoutParams(0, ViewGroup.LayoutParams.MatchParent)
            { Gravity = GravityFlags.Right });
            _action.Clickable = true;
            _action.Click += (s, e) => OnActionTap();

            // 内容
            _inner = content;
            _content = new FrameLayout(c);
            _content.SetBackgroundDrawable(Dui.GlassCard());
            _content.ClipToOutline = true;
            _content.OutlineProvider = ViewOutlineProvider.Background;
            _content.Elevation = Dui.Dpf(2);
            _content.AddView(_inner, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
            AddView(_content, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
            // 触摸监听挂在真正可点击的行上（外层容器会被子 View 的点击吃掉事件）
            _inner.SetOnTouchListener(new DragHandler(this));
        }

        private void OnActionTap()
        {
            if (_confirm == 0)
            {
                _confirm = 1;
                _actionText.Text = "再次点击删除";
                _action.SetBackgroundDrawable(Dui.Round(Dui.Mix(Dui.Danger, Color.Black, 0.35f), 16));
                _actionText.ScaleX = 0.92f;
                _actionText.ScaleY = 0.92f;
                _actionText.Animate().ScaleX(1f).ScaleY(1f).SetDuration(220)
                    .SetInterpolator(new OvershootInterpolator(2f)).Start();
                return;
            }
            _confirm = 0;
            DeleteRequested?.Invoke(this);
        }

        private void SetActionWidth(int w)
        {
            var lp = (FrameLayout.LayoutParams)_action.LayoutParameters;
            if (lp.Width == w) return;
            lp.Width = w;
            _action.LayoutParameters = lp;
        }

        private void ResetActionWidth(int duration)
        {
            int from = _action.Width;
            if (from <= 0) { SetActionWidth(0); return; }
            var anim = Android.Animation.ValueAnimator.OfInt(from, 0);
            anim.SetDuration(duration);
            anim.SetInterpolator(new DecelerateInterpolator());
            anim.Update += (s, e) =>
            {
                int v = 0;
                if (e.Animation.AnimatedValue is Java.Lang.Integer ji) v = ji.IntValue();
                SetActionWidth(v);
            };
            anim.Start();
            _actionText.Alpha = 0f;
        }

        private void ResetConfirm()
        {
            if (_confirm == 0) return;
            _confirm = 0;
            _actionText.Text = "删除";
            _action.SetBackgroundDrawable(Dui.Round(Dui.Danger, 16));
        }

        public void Close(bool animate = true)
        {
            _open = false;
            ResetConfirm();
            if (animate)
            {
                _content.Animate().TranslationX(0f).ScaleX(1f).SetDuration(260)
                    .SetInterpolator(new OvershootInterpolator(1.4f)).Start();
                ResetActionWidth(260);
            }
            else
            {
                _content.TranslationX = 0f;
                _content.ScaleX = 1f;
                SetActionWidth(0);
            }
        }

        private void Settle(bool open)
        {
            _open = open;
            float target = open ? -_btnW : 0f;
            _content.Animate().TranslationX(target).ScaleX(1f).SetDuration(300)
                .SetInterpolator(new OvershootInterpolator(1.5f)).Start();
            if (open) SetActionWidth((int)_btnW);
            else ResetActionWidth(300);
            if (!open) ResetConfirm();
        }

        private void ApplyDeform(float tx)
        {
            float p = Math.Min(1f, Math.Abs(tx) / _btnW);
            _content.PivotX = _content.Width;
            _content.PivotY = _content.Height / 2f;
            _content.ScaleX = 1f + 0.07f * p;

            // 删除区只占"已露出"的那部分宽度，静止时宽度 0，红色不会透过半透明卡片
            int reveal = (int)Math.Max(0f, -tx);
            var lp = (FrameLayout.LayoutParams)_action.LayoutParameters;
            if (lp.Width != reveal)
            {
                lp.Width = reveal;
                _action.LayoutParameters = lp;
            }
            _actionText.Alpha = Math.Min(1f, p * 1.6f);
        }

        private class DragHandler : Java.Lang.Object, View.IOnTouchListener
        {
            private readonly SwipeRow _r;
            public DragHandler(SwipeRow r) { _r = r; }

            public bool OnTouch(View v, MotionEvent e)
            {
                switch (e.ActionMasked)
                {
                    case MotionEventActions.Down:
                        if (_r._open)
                        {
                            _r._closePending = true;
                            return true;
                        }
                        _r._deciding = true;
                        _r._dragging = false;
                        _r._downX = e.RawX;
                        _r._downY = e.RawY;
                        _r._startTx = _r._content.TranslationX;
                        return false;

                    case MotionEventActions.Move:
                        if (!_r._deciding && !_r._dragging) return false;
                        float dx = e.RawX - _r._downX;
                        float dy = e.RawY - _r._downY;

                        if (!_r._dragging)
                        {
                            if (Math.Abs(dy) > _r._slop && Math.Abs(dy) > Math.Abs(dx))
                            {
                                _r._deciding = false;
                                return false;
                            }
                            if (Math.Abs(dx) > _r._slop && Math.Abs(dx) > Math.Abs(dy) * 1.2f)
                            {
                                _r._dragging = true;
                                _r._deciding = false;
                                _r.Opened?.Invoke(_r);
                                _r._content.Animate().Cancel();
                            }
                            else return false;
                        }

                        float target = _r._startTx + dx;
                        // 边界阻尼：超过展开位置后只跟随 1/3
                        if (target < -_r._btnW)
                            target = -_r._btnW - (_r._btnW * 0.18f) * (1f - 1f / (1f + Math.Abs(target + _r._btnW) / (_r._btnW * 0.6f)));
                        if (target > 0) target = target * 0.25f;

                        _r._content.TranslationX = target;
                        _r.ApplyDeform(target);
                        return true;

                    case MotionEventActions.Up:
                    case MotionEventActions.Cancel:
                        if (_r._closePending) { _r._closePending = false; _r.Close(); return true; }
                        if (!_r._dragging) { _r._deciding = false; return false; }
                        _r._dragging = false;
                        bool shouldOpen = _r._content.TranslationX < -_r._btnW * 0.42f;
                        _r.Settle(shouldOpen);
                        return true;
                }
                return false;
            }
        }
    }
}
