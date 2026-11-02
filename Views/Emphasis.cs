using System;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using HamJi.Core;

namespace HamJi.Views
{
    /// <summary>底层氛围背景：竖向渐变 + 三团柔光，用于支撑整页亚克力质感。</summary>
    public class BackdropView : View
    {
        private readonly Paint _bg = new Paint(PaintFlags.AntiAlias);
        // 三团光各自缓存一份 Paint：shader 只在尺寸变化时重建，绘制时零分配
        private readonly Paint _blobA = new Paint(PaintFlags.AntiAlias);
        private readonly Paint _blobB = new Paint(PaintFlags.AntiAlias);
        private readonly Paint _blobC = new Paint(PaintFlags.AntiAlias);
        private readonly Paint _topGlow = new Paint(PaintFlags.AntiAlias);
        private bool _built;

        public BackdropView(Context c) : base(c)
        {
            SetWillNotDraw(false);
        }

        private void Build()
        {
            int w = Width, h = Height;
            if (w <= 0 || h <= 0) return;

            _bg.SetShader(new LinearGradient(0, 0, 0, h,
                new Color(Dui.BgTop), new Color(Dui.BgBottom), Shader.TileMode.Clamp));

            _topGlow.SetShader(new LinearGradient(0, 0, 0, Dui.Dpf(320),
                new Color(Dui.WithAlpha(Dui.Accent, Dui.Dark ? 34 : 22)),
                new Color(Dui.WithAlpha(Dui.Accent, 0)), Shader.TileMode.Clamp));

            Blob(_blobA, w * 0.88f, h * 0.06f, w * 0.62f, Dui.BlobA, Dui.Dark ? 150 : 120);
            Blob(_blobB, w * 0.02f, h * 0.34f, w * 0.55f, Dui.BlobB, Dui.Dark ? 120 : 100);
            Blob(_blobC, w * 0.62f, h * 0.86f, w * 0.66f, Dui.BlobC, Dui.Dark ? 120 : 105);
            _built = true;
        }

        protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
        {
            base.OnSizeChanged(w, h, oldw, oldh);
            Build();
        }

        protected override void OnDraw(Canvas canvas)
        {
            int w = Width, h = Height;
            if (w <= 0 || h <= 0) return;
            if (!_built) Build();
            if (!_built) return;

            canvas.DrawRect(0, 0, w, h, _bg);
            canvas.DrawCircle(w * 0.88f, h * 0.06f, w * 0.62f, _blobA);
            canvas.DrawCircle(w * 0.02f, h * 0.34f, w * 0.55f, _blobB);
            canvas.DrawCircle(w * 0.62f, h * 0.86f, w * 0.66f, _blobC);
            canvas.DrawRect(0, 0, w, Dui.Dpf(320), _topGlow);
        }

        private static void Blob(Paint p, float cx, float cy, float radius, int color, int alpha)
        {
            p.SetShader(new RadialGradient(cx, cy, radius,
                new[] { Dui.WithAlpha(color, alpha), Dui.WithAlpha(color, alpha / 3), Dui.WithAlpha(color, 0) },
                new[] { 0f, 0.55f, 1f }, Shader.TileMode.Clamp));
        }
    }

    /// <summary>亚克力面板：降采样 + 高斯近似模糊当前页面，再叠加半透明色与高光描边。</summary>
    public class GlassPanel : FrameLayout
    {
        private Bitmap _thumb;
        private Rect _src;
        private readonly Paint _bmpPaint = new Paint(PaintFlags.FilterBitmap);
        private readonly Paint _tint = new Paint(PaintFlags.AntiAlias);
        private readonly Paint _border = new Paint(PaintFlags.AntiAlias);
        private readonly Paint _sheen = new Paint(PaintFlags.AntiAlias);
        private float _radiusDp = 30f;

        public View Backdrop { get; set; }
        public View Content { get; set; }
        public int TintColor { get; set; } = 0;
        public int BorderColor { get; set; } = 0;

        public GlassPanel(Context c) : base(c)
        {
            SetWillNotDraw(false);
            TintColor = Dui.NavBg;
            BorderColor = Dui.NavBorder;
            _border.SetStyle(Paint.Style.Stroke);
            _border.StrokeWidth = Math.Max(1, Dui.Dp(1));
            _border.Color = new Color(Dui.WithAlpha(BorderColor, 120));
            ClipToOutline = true;
            OutlineProvider = ViewOutlineProvider.Background;
            BuildBackgroundDrawable();
        }

        public void SetRadiusDp(float v)
        {
            _radiusDp = v;
            BuildBackgroundDrawable();
        }

        public void BuildBackgroundDrawable()
        {
            var d = new GradientDrawable();
            d.SetColor(Color.Transparent);
            d.SetCornerRadius(Dui.Dpf(_radiusDp));
            Background = d;
        }

        private bool _buildQueued;

        /// <summary>重新生成模糊底图；旧图保留到新图就绪，避免滚动时闪一下。</summary>
        public void RefreshBlur()
        {
            QueueBuild();
        }

        private void QueueBuild()
        {
            if (_buildQueued) return;
            _buildQueued = true;
            Post(() =>
            {
                _buildQueued = false;
                BuildThumb();
                Invalidate();
            });
        }

        private void BuildThumb()
        {
            if (Backdrop == null || Backdrop.Width <= 0 || Width <= 0 || Height <= 0) return;
            try
            {
                // 0.09 已经够糊；再低会糊成一片色块，再高白花 CPU
                const float scale = 0.09f;
                int tw = Math.Max(4, (int)(Backdrop.Width * scale));
                int th = Math.Max(4, (int)(Backdrop.Height * scale));

                var locA = new int[2];
                var locS = new int[2];
                Backdrop.GetLocationInWindow(locA);
                GetLocationInWindow(locS);
                int ox = locS[0] - locA[0];
                int oy = locS[1] - locA[1];

                var bmp = Bitmap.CreateBitmap(tw, th, Bitmap.Config.Argb8888);
                var canvas = new Canvas(bmp);
                canvas.Scale(scale, scale);
                // 只光栅化面板自己盖住的那一条，别把整页内容都画一遍
                canvas.ClipRect(ox, oy, ox + Width, oy + Height);

                Backdrop.Draw(canvas);
                if (Content != null && Content.Width > 0)
                {
                    try
                    {
                        var locC = new int[2];
                        Content.GetLocationInWindow(locC);
                        canvas.Save();
                        canvas.Translate(locC[0] - locA[0], locC[1] - locA[1]);
                        Content.Draw(canvas);
                        canvas.Restore();
                    }
                    catch { }
                }

                ImgBlur.Box(bmp, 4);

                var src = new Rect();
                src.Set((int)(ox * scale), (int)(oy * scale), (int)((ox + Width) * scale), (int)((oy + Height) * scale));
                src.Intersect(0, 0, tw, th);

                // 原子替换：新图就绪后才切换，中间不会出现"只有底色"的一帧
                _thumb = bmp;
                _src = src;
            }
            catch
            {
                // 保留旧底图，宁可略旧也不要闪一帧空白
            }
        }

        protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
        {
            base.OnSizeChanged(w, h, oldw, oldh);
            _thumb = null;
            // 高光渐变随高度变化，缓存在这里，绘制时不再每帧 new
            if (h > 0)
            {
                _sheen.SetShader(new LinearGradient(0, 0, 0, h * 0.55f,
                    new Color(Dui.WithAlpha(Color.White, Dui.Dark ? 18 : 56)),
                    new Color(Dui.WithAlpha(Color.White, 0)), Shader.TileMode.Clamp));
                _sheenBuilt = true;
            }
        }

        private bool _sheenBuilt;

        protected override void OnDraw(Canvas canvas)
        {
            int w = Width, h = Height;
            if (w <= 0 || h <= 0) return;
            if (_thumb == null) QueueBuild();

            var full = new RectF(0, 0, w, h);
            if (_thumb != null && _src != null && !_src.IsEmpty)
            {
                canvas.DrawBitmap(_thumb, _src, new Rect(0, 0, w, h), _bmpPaint);
            }

            _tint.Color = new Color(TintColor);
            canvas.DrawRect(full, _tint);

            if (_sheenBuilt)
            {
                canvas.DrawRect(full, _sheen);
            }

            float sw = _border.StrokeWidth;
            var rr = new RectF(sw / 2f, sw / 2f, w - sw / 2f, h - sw / 2f);
            float rad = Math.Max(0, Dui.Dpf(_radiusDp) - sw / 2f);
            canvas.DrawRoundRect(rr, rad, rad, _border);
        }
    }

    /// <summary>纯 C# 分离式盒式模糊，所有 Android 版本表现一致，不依赖 RenderEffect。</summary>
    public static class ImgBlur
    {
        public static void Box(Bitmap bmp, int radius)
        {
            if (bmp == null || radius <= 0) return;
            int w = bmp.Width, h = bmp.Height;
            if (w < 2 || h < 2) return;
            var px = new int[w * h];
            var tmp = new int[w * h];
            bmp.GetPixels(px, 0, w, 0, 0, w, h);

            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    int a = 0, r = 0, g = 0, b = 0, n = 0;
                    int x0 = Math.Max(0, x - radius), x1 = Math.Min(w - 1, x + radius);
                    for (int i = x0; i <= x1; i++)
                    {
                        int c = px[row + i];
                        a += (c >> 24) & 0xFF; r += (c >> 16) & 0xFF; g += (c >> 8) & 0xFF; b += c & 0xFF; n++;
                    }
                    tmp[row + x] = ((a / n) << 24) | ((r / n) << 16) | ((g / n) << 8) | (b / n);
                }
            }

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    int a = 0, r = 0, g = 0, b = 0, n = 0;
                    int y0 = Math.Max(0, y - radius), y1 = Math.Min(h - 1, y + radius);
                    for (int i = y0; i <= y1; i++)
                    {
                        int c = tmp[i * w + x];
                        a += (c >> 24) & 0xFF; r += (c >> 16) & 0xFF; g += (c >> 8) & 0xFF; b += c & 0xFF; n++;
                    }
                    px[y * w + x] = ((a / n) << 24) | ((r / n) << 16) | ((g / n) << 8) | (b / n);
                }
            }

            bmp.SetPixels(px, 0, w, 0, 0, w, h);
        }
    }
}
