using System;
using Android.Animation;
using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using HamJi.Core;

namespace HamJi.Views
{
    /// <summary>细线小箭头「&gt;」：收起时朝右（0°），展开时旋转 90° 朝下。</summary>
    public class CaretView : View
    {
        private readonly Paint _paint;
        private readonly Path _path = new Path();

        public CaretView(Context c) : base(c)
        {
            _paint = new Paint(PaintFlags.AntiAlias);
            _paint.SetStyle(Paint.Style.Stroke);
            _paint.StrokeWidth = Dui.Dpf(1.7f);
            _paint.StrokeJoin = Paint.Join.Round;
            _paint.StrokeCap = Paint.Cap.Round;
            _paint.Color = new Color(Dui.TextTertiary);
        }

        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);
            float cx = Width / 2f, cy = Height / 2f;
            if (cx <= 0 || cy <= 0) return;

            float dx = Dui.Dpf(2.2f);
            float dy = Dui.Dpf(4.2f);

            _path.Reset();
            _path.MoveTo(cx - dx, cy - dy);
            _path.LineTo(cx + dx, cy);
            _path.LineTo(cx - dx, cy + dy);
            canvas.DrawPath(_path, _paint);
        }
    }

    /// <summary>
    /// 一天的分组：日期标题 + 条数标签 + 折叠箭头，下面是记录列表。
    /// 列表高度做过渡动画，动画可被下一次点击打断（从当前高度继续走）。
    /// </summary>
    public class DayGroup : LinearLayout
    {
        /// <summary>用户切换折叠状态（第二个参数为切换后的展开状态）。</summary>
        public event Action<DayGroup, bool> Toggled;

        private readonly ClipBox _body;
        private readonly CaretView _caret;
        private ValueAnimator _anim;
        private int _gen;
        private bool _expanded;

        public LinearLayout Body => _body;
        public bool Expanded => _expanded;

        public DayGroup(Context c, string day, string extra, int count, bool expanded) : base(c)
        {
            Orientation = Orientation.Vertical;
            LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);

            // ---- 标题行（整行可点） ----
            var header = Dui.HBox();
            header.SetPadding(Dui.Dp(8), Dui.Dp(12), Dui.Dp(4), Dui.Dp(7));
            header.Clickable = true;
            header.Focusable = true;
            header.SetBackgroundDrawable(Dui.Ripple(
                Dui.WithAlpha(Dui.TextPrimary, Dui.Dark ? 26 : 14),
                Dui.Round(Color.Transparent, 10)));

            var left = Dui.Label(day, 12.5f, Dui.TextSecondary, true);
            left.LetterSpacing = 0.06f;
            header.AddView(left);

            if (!string.IsNullOrEmpty(extra))
            {
                var mid = Dui.Label(" · " + extra, 12.5f, Dui.TextTertiary, false);
                header.AddView(mid);
            }

            // 条数标签：小圆角浅色底
            var tag = Dui.Label(count + " 条", 10.5f, Dui.TextTertiary, true);
            tag.LetterSpacing = 0.02f;
            tag.SetPadding(Dui.Dp(6), Dui.Dp(2), Dui.Dp(6), Dui.Dp(2));
            tag.SetBackgroundDrawable(Dui.Round(Dui.WithAlpha(Dui.TextPrimary, Dui.Dark ? 26 : 13), 6));
            var tlp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
            { LeftMargin = Dui.Dp(8) };
            header.AddView(tag, tlp);

            header.AddView(new View(Dui.Ctx), new LinearLayout.LayoutParams(0, 1, 1f));

            _caret = new CaretView(c);
            var clp = new LinearLayout.LayoutParams(Dui.Dp(20), Dui.Dp(20))
            { RightMargin = Dui.Dp(6) };
            header.AddView(_caret, clp);

            header.Click += (s, e) => Toggle();
            AddView(header, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            // ---- 记录列表 ----
            _body = new ClipBox(c);
            AddView(_body, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            ApplyExpanded(expanded, false);
        }

        public void Toggle()
        {
            ApplyExpanded(!_expanded, true);
            Toggled?.Invoke(this, _expanded);
        }

        private void ApplyExpanded(bool expand, bool animate)
        {
            _expanded = expand;
            RotateCaret(expand, animate);

            if (!animate || _body.Width <= 0)
            {
                _body.ForcedHeight = expand ? -1 : 0;
                _body.RequestLayout();
                return;
            }

            int from = _body.ForcedHeight >= 0 ? _body.ForcedHeight : _body.Height;
            int to = expand ? _body.MeasureNatural() : 0;
            if (from == to)
            {
                _body.ForcedHeight = expand ? -1 : 0;
                _body.RequestLayout();
                return;
            }
            AnimateHeight(from, to, expand);
        }

        private void RotateCaret(bool expand, bool animate)
        {
            float target = expand ? 90f : 0f;
            _caret.Animate().Cancel();
            if (!animate)
            {
                _caret.Rotation = target;
                return;
            }
            _caret.Animate().Rotation(target).SetDuration(190)
                .SetInterpolator(new DecelerateInterpolator()).Start();
        }

        private void AnimateHeight(int from, int to, bool expand)
        {
            int gen = ++_gen;
            var old = _anim;
            _anim = null;
            old?.Cancel();

            var va = ValueAnimator.OfFloat(0f, 1f);
            va.SetDuration(expand ? 250 : 210);
            va.SetInterpolator(new DecelerateInterpolator());
            va.Update += (s, e) =>
            {
                if (gen != _gen) return;
                float f = va.AnimatedFraction;
                _body.ForcedHeight = (int)Math.Round(from + (to - from) * f);
                _body.RequestLayout();
            };
            va.AnimationEnd += (s, e) =>
            {
                if (gen != _gen) return;
                _anim = null;
                _body.ForcedHeight = expand ? -1 : 0;
                _body.RequestLayout();
            };
            _anim = va;
            va.Start();
        }

        /// <summary>行被回收（换成占位）后，列表高度可能变，需要重新量一次。</summary>
        public void InvalidateBody()
        {
            if (_body.ForcedHeight >= 0) _body.ForcedHeight = _expanded ? -1 : 0;
            _body.RequestLayout();
        }

        protected override void OnDetachedFromWindow()
        {
            _gen++;
            var old = _anim;
            _anim = null;
            old?.Cancel();
            _caret?.Animate()?.Cancel();
            base.OnDetachedFromWindow();
        }

        /// <summary>
        /// 纵向容器：子项永远按「自然高度」测量，自身高度取 ForcedHeight。
        /// 这样收缩时是从底部被裁掉（正常折叠观感），而不是把每行压扁。
        /// </summary>
        private sealed class ClipBox : LinearLayout
        {
            public int ForcedHeight = -1;

            // 自然高度缓存：滚动时每加一块都会重新布局，别每次都把几十行重量一遍
            private int _natW = -1;
            private int _natCount = -1;
            private int _natH = -1;

            public ClipBox(Context c) : base(c) { Orientation = Orientation.Vertical; }

            /// <summary>量出「完全展开」时的高度。注意必须临时解除 ForcedHeight，
            /// 否则收起态（0）会把自然高度也量成 0，展开动画就退化成瞬间跳变。</summary>
            public int MeasureNatural()
            {
                int keep = ForcedHeight;
                ForcedHeight = -1;
                int w = Width;
                if (w <= 0) w = Context.Resources.DisplayMetrics.WidthPixels;
                Measure(MeasureSpec.MakeMeasureSpec(w, MeasureSpecMode.Exactly),
                        MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified));
                int h = MeasuredHeight;
                ForcedHeight = keep;
                return h;
            }

            public override void OnViewAdded(View child)
            {
                base.OnViewAdded(child);
                _natH = -1;
            }

            public override void OnViewRemoved(View child)
            {
                base.OnViewRemoved(child);
                _natH = -1;
            }

            protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
            {
                int w = MeasureSpec.GetSize(widthMeasureSpec);
                if (_natH >= 0 && _natW == w && _natCount == ChildCount)
                {
                    SetMeasuredDimension(w, ForcedHeight >= 0 ? ForcedHeight : _natH);
                    return;
                }

                base.OnMeasure(widthMeasureSpec,
                    MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified));
                _natW = w;
                _natCount = ChildCount;
                _natH = MeasuredHeight;
                SetMeasuredDimension(MeasuredWidth, ForcedHeight >= 0 ? ForcedHeight : _natH);
            }
        }
    }
}
