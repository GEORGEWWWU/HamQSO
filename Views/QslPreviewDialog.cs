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
    /// QSL 卡片全屏预览：图片无边框无阴影直接铺开，
    /// 底部固定悬浮「取消 / 分享 / 更新卡片」三个小按钮。
    ///
    /// 背景按图片亮度自动反色：浅色卡片用深色底，深色卡片用浅色底，
    /// 否则「黑底 + 黑卡片」整块糊在一起看不出边界。按钮 / 关闭钮的颜色跟着一起反。
    ///
    /// 交互与分享海报预览一致：轻点图片在「适合宽度 / 原始尺寸」之间切换，
    /// 放大后可拖动，点图片以外的区域退出（右上角另有明确的关闭按钮）。
    /// </summary>
    public class QslPreviewDialog : Dialog
    {
        /// <summary>底部按钮带高度：只够放下 32dp 的药丸 + 20dp 外边距，不多占图片空间。</summary>
        private const int BarSpaceDp = 62;

        private readonly QslView _view;
        private readonly Bitmap _bmp;

        public static void Show(Context c, string path, Action onReplace)
        {
            var bmp = Qsl.LoadBitmap(path);
            if (bmp == null)
            {
                Toast.MakeText(c, "卡片图片读不出来，请重新上传", ToastLength.Short).Show();
                return;
            }

            var dlg = new QslPreviewDialog(c, bmp, onReplace);
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

        private QslPreviewDialog(Context c, Bitmap bmp, Action onReplace)
            : base(c, Resource.Style.TransparentDialogTheme)
        {
            _bmp = bmp;

            // 深色卡片 → 浅色背景；浅色卡片 → 深色背景。0.42 是实测下来两边都够分明的分界
            float luma = Qsl.Luma(bmp);
            bool light = luma < 0.42f;
            Android.Util.Log.Info("HamJiQsl", "preview luma=" + luma.ToString("0.000") + " lightBg=" + light);
            int bgColor = light ? unchecked((int)0xFFF3F3F4) : unchecked((int)0xFF000000);
            int fgColor = light ? unchecked((int)0xFF1B1B1D) : Color.White;
            int pillBg = light ? unchecked((int)0x14000000) : unchecked((int)0x24FFFFFF);
            int pillBgPrimary = light ? unchecked((int)0x1F000000) : unchecked((int)0x3DFFFFFF);
            int closeBg = light ? unchecked((int)0x14000000) : unchecked((int)0x59000000);

            Window.RequestFeature(WindowFeatures.NoTitle);
            var win = Window;
            win.SetBackgroundDrawable(new ColorDrawable(new Color(bgColor)));
            // 沉浸：隐藏状态栏，图片直接顶到屏幕边缘
            win.AddFlags(WindowManagerFlags.Fullscreen);
            win.AddFlags(WindowManagerFlags.DimBehind);
            win.SetDimAmount(1f);
            var lp = win.Attributes;
            lp.Gravity = GravityFlags.Center;
            lp.Width = ViewGroup.LayoutParams.MatchParent;
            lp.Height = ViewGroup.LayoutParams.MatchParent;
            win.Attributes = lp;
            SetCanceledOnTouchOutside(false);

            var root = new FrameLayout(c);
            root.SetBackgroundColor(new Color(bgColor));

            _view = new QslView(c, bmp);
            _view.CloseRequested += Dismiss;
            root.AddView(_view, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

            // 右上角关闭：点图片是切尺寸，得留一个明确的退出入口
            var close = new FrameLayout(c);
            close.SetBackgroundDrawable(Dui.Round(closeBg, 20));
            var ci = Dui.Icon(Resource.Drawable.ic_close, 17, fgColor);
            close.AddView(ci, new FrameLayout.LayoutParams(Dui.Dp(17), Dui.Dp(17)) { Gravity = GravityFlags.Center });
            HamJi.Core.Touch.Bind(close, Dismiss, 0.9f);
            root.AddView(close, new FrameLayout.LayoutParams(Dui.Dp(36), Dui.Dp(36))
            {
                Gravity = GravityFlags.Top | GravityFlags.Right,
                TopMargin = Dui.Dp(44),
                RightMargin = Dui.Dp(16)
            });

            // 底部按钮：小尺寸、紧凑内边距、整体居中，浮在图片之上
            var bar = Dui.HBox();
            bar.SetGravity(GravityFlags.CenterHorizontal);
            bar.Clickable = false;

            bar.AddView(Pill(c, "取消", false, fgColor, pillBg, Dismiss));
            bar.AddView(Pill(c, "分享", false, fgColor, pillBg, OnShare));
            bar.AddView(Pill(c, "更新卡片", true, fgColor, pillBgPrimary, () =>
            {
                Dismiss();
                try { onReplace?.Invoke(); } catch { }
            }));

            root.AddView(bar, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            {
                Gravity = GravityFlags.Bottom,
                BottomMargin = Dui.Dp(20)
            });

            SetContentView(root);

            root.Alpha = 0f;
            root.Post(() => root.Animate().Alpha(1f).SetDuration(160).Start());
        }

        private static TextView Pill(Context c, string text, bool primary, int fg, int bg, Action onClick)
        {
            var tv = Dui.Label(text, 13f, fg, primary);
            tv.Gravity = GravityFlags.Center;
            tv.SetPadding(Dui.Dp(15), Dui.Dp(7), Dui.Dp(15), Dui.Dp(7));
            tv.SetBackgroundDrawable(Dui.Round(bg, 18));
            tv.Clickable = true;
            var lp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
            { LeftMargin = Dui.Dp(3), RightMargin = Dui.Dp(3) };
            tv.LayoutParameters = lp;
            HamJi.Core.Touch.Bind(tv, onClick, 0.94f);
            return tv;
        }

        /// <summary>分享：和频率 / 日志海报走同一套 ShareKit，都是 PNG 位图。</summary>
        private void OnShare()
        {
            if (_bmp == null || _bmp.IsRecycled) return;
            try
            {
                string baseName = "hamji-qsl-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string err;
                if (!ShareKit.ShareImage(Context, _bmp, baseName,
                        "我的 QSL 卡片" + (Profile.HasCall ? " · " + Profile.Call : "") + "\n来自 HAM记", out err))
                    Toast.MakeText(Context, err ?? "分享失败", ToastLength.Short).Show();
            }
            catch (Exception ex)
            {
                Toast.MakeText(Context, "分享失败：" + ex.Message, ToastLength.Short).Show();
            }
        }

        /// <summary>Dialog 会自己重算窗口尺寸，必须在 Show 之后再压一次全屏。</summary>
        public override void Show()
        {
            base.Show();
            try { Window?.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent); }
            catch { }
        }

        public override void Dismiss()
        {
            try { _view?.Detach(); } catch { }
            base.Dismiss();
        }

        /// <summary>自绘图片：默认按宽度铺满，轻点切到原始像素，放大后可拖动。</summary>
        private sealed class QslView : View
        {
            public event Action CloseRequested;

            private Bitmap _bmp;
            private readonly Paint _paint = new Paint(PaintFlags.FilterBitmap | PaintFlags.Dither | PaintFlags.AntiAlias);

            private float _scale = 1f;
            private bool _fitMode = true;
            private float _tx, _ty;

            private float _downX, _downY, _lastX, _lastY;
            private bool _moved;

            private readonly int _slop = Dui.Dp(8);
            private readonly GestureDetector _gd;
            private ValueAnimator _anim;

            public QslView(Context c, Bitmap bmp) : base(c)
            {
                _bmp = bmp;
                Focusable = true;
                Clickable = true;
                _gd = new GestureDetector(c, new TapListener(this));
            }

            /// <summary>图片绘制区域：底部只留一条按钮带，按钮永远落在纯色底上。</summary>
            private int ContentBottom => Height - Dui.Dp(BarSpaceDp);

            /// <summary>退出时把大图从 native 堆还回去（几 MB 起步，连续看几张很容易顶内存）。</summary>
            public void Detach()
            {
                try { _anim?.Cancel(); } catch { }
                _anim = null;
                try { if (_bmp != null && !_bmp.IsRecycled) _bmp.Recycle(); } catch { }
                _bmp = null;
                Invalidate();
            }

            protected override void OnDetachedFromWindow()
            {
                try { _anim?.Cancel(); } catch { }
                _anim = null;
                base.OnDetachedFromWindow();
            }

            /// <summary>适合宽度：铺满屏宽，高度按比例（竖图可能比可视区高，可上下拖）。</summary>
            private float FitScale()
            {
                if (_bmp == null || Width <= 0) return 1f;
                return Math.Max(0.02f, (float)Width / _bmp.Width);
            }

            protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
            {
                base.OnSizeChanged(w, h, oldw, oldh);
                if (_fitMode) { _scale = FitScale(); _ty = 0f; }
                ClampOffset();
            }

            private void ClampOffset()
            {
                if (_bmp == null || Width <= 0) { _tx = _ty = 0f; return; }
                int bottom = Math.Max(0, ContentBottom);
                float dw = _bmp.Width * _scale;
                float dh = _bmp.Height * _scale;
                float maxX = Math.Max(0f, (dw - Width) / 2f);
                float maxY = Math.Max(0f, (dh - bottom) / 2f);
                _tx = Math.Max(-maxX, Math.Min(maxX, _tx));
                _ty = Math.Max(-maxY, Math.Min(maxY, _ty));
            }

            protected override void OnDraw(Canvas canvas)
            {
                base.OnDraw(canvas);
                if (_bmp == null || _bmp.IsRecycled) return;

                int bottom = Math.Max(0, ContentBottom);
                float dw = _bmp.Width * _scale;
                float dh = _bmp.Height * _scale;
                float left = (Width - dw) / 2f + _tx;
                float top = (bottom - dh) / 2f + _ty;
                var dst = new RectF(left, top, left + dw, top + dh);

                // 不裁圆角、不加边框阴影：图片本体直接铺满；
                // 但裁到按钮带以上，放大时图片不会压到底部按钮下面
                canvas.Save();
                canvas.ClipRect(0, 0, Width, bottom);
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
            }

            private bool HitImage(float x, float y)
            {
                if (_bmp == null) return false;
                int bottom = Math.Max(0, ContentBottom);
                float dw = _bmp.Width * _scale;
                float dh = _bmp.Height * _scale;
                float left = (Width - dw) / 2f + _tx;
                float top = (bottom - dh) / 2f + _ty;
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
                        try { _anim?.Cancel(); } catch { }
                        break;

                    case MotionEventActions.Move:
                        float dx = e.RawX - _lastX;
                        float dy = e.RawY - _lastY;
                        if (Math.Abs(e.RawX - _downX) > _slop || Math.Abs(e.RawY - _downY) > _slop)
                            _moved = true;
                        // 只有内容超出可视区时拖动才有意义（适合宽度且不超高时拖不动，避免误触）
                        if (_moved && _bmp != null && IsOverflowing())
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

            private bool IsOverflowing()
            {
                int bottom = Math.Max(0, ContentBottom);
                return _bmp.Width * _scale > Width + 1 || _bmp.Height * _scale > bottom + 1;
            }

            private sealed class TapListener : GestureDetector.SimpleOnGestureListener
            {
                private readonly QslView _o;
                public TapListener(QslView o) { _o = o; }

                public override bool OnDown(MotionEvent e) => true;

                public override bool OnSingleTapUp(MotionEvent e)
                {
                    if (_o._moved) return true;
                    if (_o.HitImage(e.GetX(), e.GetY())) _o.ToggleZoom();
                    else { try { _o.CloseRequested?.Invoke(); } catch { } }
                    return true;
                }
            }
        }
    }
}
