using System;
using System.Collections.Generic;
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
    /// QSL 卡片全屏预览。卡片有正反两面，这里按槽位逐张显示：
    ///
    /// · 图片无边框无阴影直接铺开，默认按屏宽 100% 显示，轻点切原始尺寸，放大后可拖动；
    /// · 两张卡片左右滑动切换，底部小圆点指示当前第几张；
    /// · 背景按当前图片平均亮度自动反色（深卡配浅底、浅卡配深底）；
    /// · 底部按钮两行：分享 / 更新卡片，下面一行「设为填写页」（打印时填写的那一面）；
    /// · 点图片以外的区域或右上角 × 退出（不再需要「取消」按钮）。
    /// </summary>
    public class QslPreviewDialog : Dialog
    {
        /// <summary>底部按钮带高度：一排按钮 + 外边距。</summary>
        private const int BarSpaceDp = 62;

        private readonly QslView _view;
        private readonly Action<int> _onPick;
        private readonly Action _onDeleted;
        private readonly LinearLayout _dots;
        private readonly TextView _fillBtn;
        private readonly List<View> _dotViews = new List<View>();

        private Android.Views.Window _win;
        private int _slot;

        /// <param name="onPick">参数为要替换的槽位；-1 表示「补上另一面」。</param>
        /// <param name="onDeleted">某一面被删除后回调（由设置页刷新并重新打开预览）。</param>
        public static void Show(Context c, Action<int> onPick, Action onDeleted)
        {
            var slots = Qsl.Slots;
            var pages = new List<int>();
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null) pages.Add(i);

            if (pages.Count == 0)
            {
                Toast.MakeText(c, "还没有卡片", ToastLength.Short).Show();
                return;
            }

            Bitmap first;
            try { first = Qsl.LoadBitmap(slots[pages[0]]); }
            catch (Exception ex)
            {
                Toast.MakeText(c, "卡片图片读不出来：" + ex.Message, ToastLength.Short).Show();
                return;
            }
            if (first == null)
            {
                Toast.MakeText(c, "卡片图片读不出来，请重新上传", ToastLength.Short).Show();
                return;
            }

            var dlg = new QslPreviewDialog(c, slots, pages, first, onPick, onDeleted);
            try
            {
                dlg.Show();
            }
            catch (Exception ex)
            {
                Toast.MakeText(c, "打开预览失败：" + ex.Message, ToastLength.Short).Show();
                try { first.Recycle(); } catch { }
            }
        }

        private QslPreviewDialog(Context c, string[] slots, List<int> pages, Bitmap first,
                                 Action<int> onPick, Action onDeleted)
            : base(c, Resource.Style.TransparentDialogTheme)
        {
            _onPick = onPick;
            _onDeleted = onDeleted;
            _slot = pages[0];

            Window.RequestFeature(WindowFeatures.NoTitle);
            _win = Window;
            // 沉浸：隐藏状态栏，图片直接顶到屏幕边缘
            _win.AddFlags(WindowManagerFlags.Fullscreen);
            _win.AddFlags(WindowManagerFlags.DimBehind);
            _win.SetDimAmount(1f);
            var lp = _win.Attributes;
            lp.Gravity = GravityFlags.Center;
            lp.Width = ViewGroup.LayoutParams.MatchParent;
            lp.Height = ViewGroup.LayoutParams.MatchParent;
            _win.Attributes = lp;
            SetCanceledOnTouchOutside(false);

            bool light = Qsl.Luma(first) < 0.42f;
            int bgColor = light ? unchecked((int)0xFFF3F3F4) : unchecked((int)0xFF000000);
            int fgColor = light ? unchecked((int)0xFF1B1B1D) : Color.White;
            int pillBg = light ? unchecked((int)0x14000000) : unchecked((int)0x24FFFFFF);
            int pillBgPrimary = light ? unchecked((int)0x1F000000) : unchecked((int)0x3DFFFFFF);

            _win.SetBackgroundDrawable(new ColorDrawable(new Color(bgColor)));

            var root = new FrameLayout(c);
            root.SetBackgroundColor(new Color(bgColor));

            _view = new QslView(c, slots, pages, first);
            _view.CloseRequested += Dismiss;
            _view.PageChanged += OnPageChanged;
            root.AddView(_view, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

            // 右上角关闭：点图片是切尺寸，得留一个明确的退出入口
            var close = new FrameLayout(c);
            close.SetBackgroundDrawable(Dui.Round(light ? unchecked((int)0x14000000) : unchecked((int)0x59000000), 20));
            var ci = Dui.Icon(Resource.Drawable.ic_close, 17, fgColor);
            close.AddView(ci, new FrameLayout.LayoutParams(Dui.Dp(17), Dui.Dp(17)) { Gravity = GravityFlags.Center });
            HamJi.Core.Touch.Bind(close, Dismiss, 0.9f);
            root.AddView(close, new FrameLayout.LayoutParams(Dui.Dp(36), Dui.Dp(36))
            {
                Gravity = GravityFlags.Top | GravityFlags.Right,
                TopMargin = Dui.Dp(44),
                RightMargin = Dui.Dp(16)
            });

            // 页码小圆点：两张卡片才显示
            _dots = Dui.HBox();
            _dots.SetGravity(GravityFlags.CenterHorizontal);
            _dots.Visibility = pages.Count > 1 ? ViewStates.Visible : ViewStates.Gone;
            for (int i = 0; i < pages.Count; i++)
            {
                var dot = new View(c);
                dot.SetBackgroundDrawable(Dui.Round(fgColor, 3.5f));
                dot.LayoutParameters = new LinearLayout.LayoutParams(Dui.Dp(6), Dui.Dp(6))
                { LeftMargin = Dui.Dp(4), RightMargin = Dui.Dp(4) };
                _dotViews.Add(dot);
                _dots.AddView(dot);
            }
            root.AddView(_dots, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            {
                Gravity = GravityFlags.Bottom,
                BottomMargin = Dui.Dp(66)
            });

            // 底部两行按钮：分享 / 更新卡片，下面一行「设为填写页」
            // 三个按钮放一排：分享 / 更新卡片（或添加另一面） / 设为填写页
            var bar = Dui.HBox();
            bar.SetGravity(GravityFlags.CenterHorizontal);
            bar.Clickable = false;
            bar.AddView(Pill(c, "分享", false, fgColor, pillBg, OnShare));

            // 还有空槽位时 === 补另一面；两面都齐了才是真正意义上的「更新这一面」
            bool canAdd = Qsl.Count < Qsl.MaxCount;
            var pick = Pill(c, canAdd ? "添加另一面" : "更新卡片", true, fgColor, pillBgPrimary, () =>
            {
                int target = canAdd ? -1 : _slot;
                Dismiss();
                try { _onPick?.Invoke(target); } catch { }
            });
            // 长按删除当前这一面（至少保留一张）
            pick.LongClick += (s, e) =>
            {
                e.Handled = true;
                ConfirmDeleteSide();
            };
            bar.AddView(pick);

            _fillBtn = Pill(c, "设为填写页", false, fgColor, pillBg, OnSetFillPage);
            bar.AddView(_fillBtn);

            root.AddView(bar, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            {
                Gravity = GravityFlags.Bottom,
                BottomMargin = Dui.Dp(22)
            });

            SetContentView(root);
            RefreshDots();
            RefreshFillButton();

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
            tv.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
            { LeftMargin = Dui.Dp(3), RightMargin = Dui.Dp(3) };
            HamJi.Core.Touch.Bind(tv, onClick, 0.94f);
            return tv;
        }

        private void OnPageChanged(int slotIndex, int page, int pageCount)
        {
            _slot = slotIndex;
            RefreshDots();
            RefreshFillButton();

            // 翻到另一张时重新决定窗口要不要 HDR
            try { HdrDisplay.SetWindowHdr(_win, HdrDisplay.IsHdr(_view.CurrentBitmap)); }
            catch { }
        }

        private void RefreshDots()
        {
            if (_dots == null || _dotViews.Count == 0) return;
            int cur = _view.CurrentPage;
            for (int i = 0; i < _dotViews.Count; i++)
            {
                var lp = (LinearLayout.LayoutParams)_dotViews[i].LayoutParameters;
                int size = Dui.Dp(i == cur ? 7 : 5);
                lp.Width = size;
                lp.Height = size;
                _dotViews[i].LayoutParameters = lp;
                _dotViews[i].Alpha = i == cur ? 1f : 0.4f;
            }
        }

        /// <summary>「设为填写页」：给后续打印功能用，标记这张卡哪一面用来填写。</summary>
        private void RefreshFillButton()
        {
            if (_fillBtn == null) return;
            bool isFill = Qsl.FillSlot == _slot;
            _fillBtn.Text = isFill ? "已设为填写页" : "设为填写页";
            _fillBtn.Alpha = isFill ? 0.7f : 1f;
        }

        private void OnSetFillPage()
        {
            if (Qsl.FillSlot == _slot)
            {
                Toast.MakeText(Context, "这一面已经是填写页", ToastLength.Short).Show();
                return;
            }
            Qsl.FillSlot = _slot;
            RefreshFillButton();
            Toast.MakeText(Context, _slot == 0 ? "已设为填写页（正面）" : "已设为填写页（反面）",
                ToastLength.Short).Show();
        }

        /// <summary>长按「更新卡片」：只删掉当前这一面，保留另一面。</summary>
        private void ConfirmDeleteSide()
        {
            if (Qsl.Count <= 1)
            {
                Toast.MakeText(Context, "至少要保留一张卡片", ToastLength.Short).Show();
                return;
            }

            new AlertDialog.Builder(Context, Resource.Style.AppDialogTheme)
                .SetTitle("删除这一面？")
                .SetMessage(_slot == 0 ? "正面卡片会被删除，反面保留。" : "反面卡片会被删除，正面保留。")
                .SetNegativeButton("取消", (s, e) => { })
                .SetPositiveButton("删除", (s, e) =>
                {
                    bool ok = Qsl.DeleteSlot(_slot);
                    Toast.MakeText(Context, ok ? "已删除这一面" : "至少要保留一张卡片", ToastLength.Short).Show();
                    if (!ok) return;
                    Dismiss();
                    try { _onDeleted?.Invoke(); } catch { }
                })
                .Show();
        }

        /// <summary>分享：和频率 / 日志海报走同一套 ShareKit，都是 PNG 位图。</summary>
        private void OnShare()
        {
            var bmp = _view?.CurrentBitmap;
            if (bmp == null || bmp.IsRecycled) return;
            try
            {
                string baseName = "hamji-qsl-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string err;
                if (!ShareKit.ShareImage(Context, bmp, baseName,
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

            // 窗口真正挂到 SurfaceFlinger 之后再补一次色彩模式：
            // 部分 ROM（HyperOS / ColorOS 等）只在 surface 创建后读这个值
            try { HdrDisplay.SetWindowHdr(Window, HdrDisplay.IsHdr(_view.CurrentBitmap)); }
            catch (Exception ex) { Android.Util.Log.Info("HamJiHdr", "Show 后设置 HDR 失败：" + ex.Message); }
        }

        public override void Dismiss()
        {
            try { _view?.Detach(); } catch { }
            base.Dismiss();
        }

        /// <summary>自绘图片：适合宽度 / 原始像素切换、放大拖动、左右滑动翻页。</summary>
        private sealed class QslView : View
        {
            public event Action CloseRequested;
            /// <summary>参数：槽位号、当前页序号、总页数。</summary>
            public event Action<int, int, int> PageChanged;

            private readonly string[] _slots;
            private readonly List<int> _pages;

            private Bitmap _bmp;
            private int _page;

            private readonly Paint _paint = new Paint(PaintFlags.FilterBitmap | PaintFlags.Dither | PaintFlags.AntiAlias);

            private float _scale = 1f;
            private bool _fitMode = true;
            private float _tx, _ty;

            private float _downX, _downY, _lastX, _lastY;
            private float _dragDx;
            private bool _moved;
            private bool _paging;

            private readonly int _slop = Dui.Dp(8);
            private readonly GestureDetector _gd;
            private ValueAnimator _anim;

            public QslView(Context c, string[] slots, List<int> pages, Bitmap first) : base(c)
            {
                _slots = slots;
                _pages = pages;
                _bmp = first;
                Focusable = true;
                Clickable = true;
                _gd = new GestureDetector(c, new TapListener(this));
            }

            public Bitmap CurrentBitmap => _bmp;

            public int CurrentPage => _page;

            /// <summary>退出时把大图从 native 堆还回去（几 MB 起步，连续看几张很容易顶内存）。</summary>
            public void Detach()
            {
                try { _anim?.Cancel(); } catch { }
                _anim = null;
                RecycleBmp();
                Invalidate();
            }

            private void RecycleBmp()
            {
                try { if (_bmp != null && !_bmp.IsRecycled) _bmp.Recycle(); } catch { }
                _bmp = null;
            }

            protected override void OnDetachedFromWindow()
            {
                try { _anim?.Cancel(); } catch { }
                _anim = null;
                base.OnDetachedFromWindow();
            }

            /// <summary>图片绘制区域：底部留出按钮带，按钮永远落在纯色底上。</summary>
            private int ContentBottom => Height - Dui.Dp(BarSpaceDp);

            private float FitScale()
            {
                if (_bmp == null || Width <= 0) return 1f;
                return Math.Max(0.02f, (float)Width / _bmp.Width);
            }

            protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
            {
                base.OnSizeChanged(w, h, oldw, oldh);
                if (_fitMode) { _scale = _bmp != null ? FitScale() : 1f; _ty = 0f; }
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
                float left = (Width - dw) / 2f + _tx + _dragDx;
                float top = (bottom - dh) / 2f + _ty;
                var dst = new RectF(left, top, left + dw, top + dh);

                // 不裁圆角、不加边框阴影；但裁到按钮带以上，放大时不会压到底部按钮下面
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
                        _dragDx = 0f;
                        _moved = false;
                        _paging = false;
                        try { _anim?.Cancel(); } catch { }
                        break;

                    case MotionEventActions.Move:
                        float dx = e.RawX - _lastX;
                        float dy = e.RawY - _lastY;
                        float totalX = e.RawX - _downX;
                        float totalY = e.RawY - _downY;
                        if (Math.Abs(totalX) > _slop || Math.Abs(totalY) > _slop) _moved = true;

                        // 多张卡片 + 适合宽度时：横向拖 = 翻页手势；其余情况按放大后的平移处理
                        if (_moved && _pages.Count > 1 && _fitMode && !_paging
                            && Math.Abs(totalX) > Math.Abs(totalY) * 1.4)
                        {
                            _paging = true;
                        }

                        if (_paging)
                        {
                            _dragDx = totalX;
                            Invalidate();
                        }
                        else if (_moved && _bmp != null && IsOverflowing())
                        {
                            _tx += dx;
                            _ty += dy;
                            ClampOffset();
                            Invalidate();
                        }
                        _lastX = e.RawX;
                        _lastY = e.RawY;
                        break;

                    case MotionEventActions.Up:
                    case MotionEventActions.Cancel:
                        if (_paging)
                        {
                            float threshold = Math.Max(Dui.Dp(56), Width * 0.18f);
                            if (_dragDx < -threshold) SwitchPage(+1);
                            else if (_dragDx > threshold) SwitchPage(-1);
                            _dragDx = 0f;
                            _paging = false;
                            Invalidate();
                        }
                        break;
                }

                if (!_paging) _gd.OnTouchEvent(e);
                return true;
            }

            private bool IsOverflowing()
            {
                int bottom = Math.Max(0, ContentBottom);
                return _bmp.Width * _scale > Width + 1 || _bmp.Height * _scale > bottom + 1;
            }

            /// <summary>翻到相邻页：回收当前位图 → 解码新图 → 复位缩放。</summary>
            private void SwitchPage(int delta)
            {
                int next = _page + delta;
                if (next < 0 || next >= _pages.Count) return;

                _page = next;
                int slot = _pages[_page];
                RecycleBmp();

                Bitmap bmp = null;
                try { bmp = Qsl.LoadBitmap(_slots[slot]); }
                catch { }
                _bmp = bmp;
                _fitMode = true;
                _scale = FitScale();
                _tx = _ty = 0f;
                Invalidate();

                try { PageChanged?.Invoke(slot, _page, _pages.Count); } catch { }
                Alpha = 0.35f;
                Animate().Alpha(1f).SetDuration(140).Start();
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
