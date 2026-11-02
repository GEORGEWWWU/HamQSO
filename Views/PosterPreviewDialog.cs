using System;
using Android.Animation;
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
    /// 分享海报预览：全屏悬浮，图片居中。
    /// 交互按需求定死：轻点图片 = 在「适合屏幕 / 原始像素」之间切换，长按 = 拉起系统分享，
    /// 点图片以外的区域 = 关闭。
    ///
    /// 用一个自绘 View 而不是 ImageView + ScrollView：
    /// 点击 / 长按 / 拖拽三种手势在嵌套滚动容器里会互相抢事件，
    /// 自己画的话命中判定、缩放动画、平移边界都在手里，行为完全可控。
    /// </summary>
    public class PosterPreviewDialog : Dialog
    {
        private readonly Bitmap _bmp;
        private readonly PosterView _view;
        private readonly QsoRecord _rec;
        private readonly TextView _hint;

        public static void Show(Context c, QsoRecord rec)
        {
            Bitmap bmp;
            try
            {
                bmp = PosterRenderer.Render(rec);
            }
            catch (Exception ex)
            {
                Toast.MakeText(c, "生成分享图失败：" + ex.Message, ToastLength.Short).Show();
                return;
            }

            var dlg = new PosterPreviewDialog(c, rec, bmp);
            try
            {
                dlg.Show();
            }
            catch (Exception ex)
            {
                Toast.MakeText(c, "打开预览失败：" + ex.Message, ToastLength.Short).Show();
                try { bmp.Recycle(); } catch { }
            }
        }

        private PosterPreviewDialog(Context c, QsoRecord rec, Bitmap bmp)
            : base(c, Resource.Style.TransparentDialogTheme)
        {
            _rec = rec;
            _bmp = bmp;

            Window.RequestFeature(WindowFeatures.NoTitle);
            var win = Window;
            win.SetBackgroundDrawable(new ColorDrawable(Color.Transparent));
            win.AddFlags(WindowManagerFlags.DimBehind);
            win.SetDimAmount(0.9f);
            var lp = win.Attributes;
            lp.Gravity = GravityFlags.Center;
            lp.Width = ViewGroup.LayoutParams.MatchParent;
            lp.Height = ViewGroup.LayoutParams.MatchParent;
            win.Attributes = lp;
            SetCanceledOnTouchOutside(false);

            var root = new FrameLayout(c);
            root.SetBackgroundColor(new Color(unchecked((int)0xF2000000)));

            _view = new PosterView(c, bmp);
            _view.CloseRequested += Dismiss;
            _view.ShareRequested += OnShare;
            root.AddView(_view, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

            // 右上角关闭（点图片是切尺寸，得留一个明确的退出入口）
            var close = new FrameLayout(c);
            close.SetBackgroundDrawable(Dui.Round(Dui.WithAlpha(Color.White, 34), 22));
            var ci = Dui.Icon(Resource.Drawable.ic_close, 17, Color.White);
            close.AddView(ci, new FrameLayout.LayoutParams(Dui.Dp(17), Dui.Dp(17)) { Gravity = GravityFlags.Center });
            HamJi.Core.Touch.Bind(close, Dismiss, 0.9f);
            var clp = new FrameLayout.LayoutParams(Dui.Dp(38), Dui.Dp(38))
            {
                Gravity = GravityFlags.Top | GravityFlags.Right,
                TopMargin = Dui.Dp(44),
                RightMargin = Dui.Dp(16)
            };
            root.AddView(close, clp);

            _hint = Dui.Label("轻点切换尺寸 · 长按分享", 11.5f, Dui.WithAlpha(Color.White, 170), false);
            _hint.Gravity = GravityFlags.Center;
            var hlp = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            {
                Gravity = GravityFlags.Bottom,
                BottomMargin = Dui.Dp(44)
            };
            root.AddView(_hint, hlp);
            _view.HintChanged += fit =>
            {
                _hint.Text = fit ? "轻点切换尺寸 · 长按分享" : "已按原始尺寸显示 · 可拖动 · 长按分享";
            };

            SetContentView(root);

            root.Alpha = 0f;
            root.Post(() => root.Animate().Alpha(1f).SetDuration(180).Start());
        }

        /// <summary>Dialog 会用自己的方式重算窗口尺寸，必须在 Show 之后再压一次全屏。</summary>
        public override void Show()
        {
            base.Show();
            try { Window?.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent); }
            catch { }
        }

        private void OnShare()
        {
            try
            {
                string baseName = "hamji-" + (_rec.IsLog ? "log" : "freq") + "-" +
                                  DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string err;
                if (!ShareKit.ShareImage(Context, _bmp, baseName, BuildShareText(_rec), out err))
                    Toast.MakeText(Context, err ?? "分享失败", ToastLength.Short).Show();
            }
            catch (Exception ex)
            {
                Toast.MakeText(Context, "分享失败：" + ex.Message, ToastLength.Short).Show();
            }
        }

        /// <summary>分享附带的文字：短信 / 邮件这类接收方会把它一起带出去。</summary>
        private static string BuildShareText(QsoRecord rec)
        {
            var sb = new System.Text.StringBuilder();
            if (rec.IsLog)
            {
                sb.Append(string.IsNullOrWhiteSpace(rec.Call) ? "通联记录" : rec.Call.ToUpperInvariant());
                if (rec.Freq > 0) sb.Append(" · ").Append(Dui.FreqLine(rec));
            }
            else
            {
                sb.Append("通联频点 · ").Append(Dui.FreqLine(rec));
            }
            if (!string.IsNullOrWhiteSpace(rec.Mode)) sb.Append(" · ").Append(rec.Mode);
            sb.Append("\n来自 HAM记");
            return sb.ToString();
        }

        public override void Dismiss()
        {
            try { _view?.Detach(); } catch { }
            base.Dismiss();
        }

        /// <summary>自绘图片：负责绘制、缩放动画、平移、点击/长按判定。</summary>
        private sealed class PosterView : View
        {
            public event Action CloseRequested;
            public event Action ShareRequested;
            /// <summary>true=适合屏幕，false=原始尺寸。</summary>
            public event Action<bool> HintChanged;

            private Bitmap _bmp;
            private readonly Paint _paint = new Paint(PaintFlags.FilterBitmap | PaintFlags.Dither | PaintFlags.AntiAlias);

            private float _scale = 1f;
            private bool _fitMode = true;
            private float _tx, _ty;

            private float _downX, _downY, _lastX, _lastY;
            private bool _moved;
            private bool _longFired;

            private readonly int _slop = Dui.Dp(8);
            private readonly GestureDetector _gd;
            private ValueAnimator _anim;

            public PosterView(Context c, Bitmap bmp) : base(c)
            {
                _bmp = bmp;
                Focusable = true;
                Clickable = true;
                _gd = new GestureDetector(c, new TapListener(this));
                // 长按默认就是开的，不用再 setLongpressEnabled(true)
            }

            /// <summary>退出时把大图从视图上摘掉（1200×1600 ≈ 7.7MB，别让它挂在已销毁的视图上）。</summary>
            public void Detach()
            {
                try { _anim?.Cancel(); } catch { }
                _anim = null;
                _bmp = null;
                Invalidate();
            }

            protected override void OnDetachedFromWindow()
            {
                try { _anim?.Cancel(); } catch { }
                _anim = null;
                base.OnDetachedFromWindow();
            }

            private float FitScale()
            {
                if (_bmp == null || Width <= 0 || Height <= 0) return 1f;
                float padX = Dui.Dpf(20f);
                float padTop = Dui.Dpf(92f);      // 让开右上角关闭钮
                float padBottom = Dui.Dpf(84f);   // 让开底部提示
                float sw = (Width - padX * 2) / _bmp.Width;
                float sh = (Height - padTop - padBottom) / _bmp.Height;
                return Math.Max(0.05f, Math.Min(sw, sh));
            }

            protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
            {
                base.OnSizeChanged(w, h, oldw, oldh);
                if (_fitMode) _scale = FitScale();
                ClampOffset();
            }

            private void ClampOffset()
            {
                if (_bmp == null || Width <= 0) { _tx = _ty = 0f; return; }
                float dw = _bmp.Width * _scale;
                float dh = _bmp.Height * _scale;
                float maxX = Math.Max(0f, (dw - Width) / 2f);
                float maxY = Math.Max(0f, (dh - Height) / 2f);
                _tx = Math.Max(-maxX, Math.Min(maxX, _tx));
                _ty = Math.Max(-maxY, Math.Min(maxY, _ty));
            }

            protected override void OnDraw(Canvas canvas)
            {
                base.OnDraw(canvas);
                if (_bmp == null || _bmp.IsRecycled) return;

                float dw = _bmp.Width * _scale;
                float dh = _bmp.Height * _scale;
                float left = (Width - dw) / 2f + _tx;
                float top = (Height - dh) / 2f + _ty;
                var dst = new RectF(left, top, left + dw, top + dh);

                // 圆角裁切：海报本身是直角矩形，屏幕上一圈圆角更像"一张卡片"
                canvas.Save();
                var clip = new Path();
                clip.AddRoundRect(dst, Dui.Dpf(14f), Dui.Dpf(14f), Path.Direction.Cw);
                canvas.ClipPath(clip);
                canvas.DrawBitmap(_bmp, null, dst, _paint);
                canvas.Restore();
            }

            private void AnimateScaleTo(float target)
            {
                try { _anim?.Cancel(); } catch { }
                float from = _scale;
                var a = ValueAnimator.OfFloat(0f, 1f);
                a.SetDuration(220);
                a.SetInterpolator(new Android.Views.Animations.DecelerateInterpolator(1.6f));
                a.Update += (s, e) =>
                {
                    float t = a.AnimatedFraction;
                    _scale = from + (target - from) * t;
                    ClampOffset();
                    Invalidate();
                };
                a.Start();
                _anim = a;
            }

            private void ToggleZoom()
            {
                if (_bmp == null) return;
                _fitMode = !_fitMode;
                if (!_fitMode) { _tx = 0f; _ty = 0f; }
                AnimateScaleTo(_fitMode ? FitScale() : 1f);
                try { HintChanged?.Invoke(_fitMode); } catch { }
            }

            private bool HitImage(float x, float y)
            {
                if (_bmp == null) return false;
                float dw = _bmp.Width * _scale;
                float dh = _bmp.Height * _scale;
                float left = (Width - dw) / 2f + _tx;
                float top = (Height - dh) / 2f + _ty;
                return x >= left && x <= left + dw && y >= top && y <= top + dh;
            }

            public override bool OnTouchEvent(MotionEvent e)
            {
                if (e == null) return false;

                switch (e.ActionMasked)
                {
                    case MotionEventActions.Down:
                        _downX = _lastX = e.RawX;
                        _downY = _lastY = e.RawY;
                        _moved = false;
                        _longFired = false;
                        try { _anim?.Cancel(); } catch { }
                        break;

                    case MotionEventActions.Move:
                        float dx = e.RawX - _lastX;
                        float dy = e.RawY - _lastY;
                        if (Math.Abs(e.RawX - _downX) > _slop || Math.Abs(e.RawY - _downY) > _slop)
                            _moved = true;
                        // 只有放大到超出屏幕时拖动才有意义（适合屏幕时拖不动，避免误触）
                        if (_moved && !_fitMode && _bmp != null)
                        {
                            _tx += dx;
                            _ty += dy;
                            ClampOffset();
                            Invalidate();
                        }
                        _lastX = e.RawX;
                        _lastY = e.RawY;
                        break;
                }

                _gd.OnTouchEvent(e);
                return true;
            }

            private sealed class TapListener : GestureDetector.SimpleOnGestureListener
            {
                private readonly PosterView _o;
                public TapListener(PosterView o) { _o = o; }

                public override bool OnDown(MotionEvent e) => true;

                public override void OnLongPress(MotionEvent e)
                {
                    _o._longFired = true;
                    if (!_o.HitImage(e.GetX(), e.GetY())) return;
                    try { _o.ShareRequested?.Invoke(); } catch { }
                }

                public override bool OnSingleTapUp(MotionEvent e)
                {
                    if (_o._longFired || _o._moved) return true;
                    if (_o.HitImage(e.GetX(), e.GetY())) _o.ToggleZoom();
                    else { try { _o.CloseRequested?.Invoke(); } catch { } }
                    return true;
                }
            }
        }
    }
}
