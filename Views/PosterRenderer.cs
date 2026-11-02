using System;
using System.Collections.Generic;
using System.Text;
using Android.Graphics;
using HamJi.Core;

namespace HamJi.Views
{
    /// <summary>
    /// 分享海报：用 Canvas 画成 1200×1600（3:4）的位图。
    ///
    /// 构图刻意和详情卡片一致 —— 同一套顺序：类型徽标 + 日期 → 主题色主视觉（呼号 / 频率最大）
    /// → 两列读数表 → 备注 → 页脚。这样点开卡片看到的和分享出去的图是同一个东西。
    ///
    /// 视觉只留三样：底色渐变、两团模糊主题色光球、一张浮在光球上的圆角内容卡。
    /// 没有网格、扫描线、四角定位框、波形、刻度尺 —— 第一版那些显得杂，删掉了。
    ///
    /// 配色全部取自 Dui（跟随系统深浅色 + 设置里的主题色），不写死颜色。
    /// </summary>
    public static class PosterRenderer
    {
        public const int W = 1200;
        public const int H = 1600;

        // 内容卡
        private const int CARD_L = 56;
        private const int CARD_T = 96;
        private const int CARD_R = W - 56;
        private const int CARD_B = H - 96;

        // 卡内留白
        private const int PAD = 52;
        private const int LEFT = CARD_L + PAD;          // 108
        private const int RIGHT = CARD_R - PAD;         // 1092
        private const int INNER_W = RIGHT - LEFT;       // 984

        public static Bitmap Render(QsoRecord rec)
        {
            bool dark = Dui.Dark;
            int accent = Dui.Accent;

            // ---------- 调色板 ----------
            int bgTop = dark ? Dui.Mix(unchecked((int)0xFF0B1018), accent, 0.12f)
                             : Dui.Mix(unchecked((int)0xFFFFFFFF), accent, 0.06f);
            int bgBot = dark ? unchecked((int)0xFF03050A) : unchecked((int)0xFFE9EEF6);
            int ink = dark ? unchecked((int)0xFFF4F8FF) : unchecked((int)0xFF0B1520);
            int ink2 = dark ? unchecked((int)0xFFA9B8CA) : unchecked((int)0xFF4E6076);
            int ink3 = dark ? unchecked((int)0xFF6D7F94) : unchecked((int)0xFF93A0AF);
            int cardBg = dark ? Dui.Mix(unchecked((int)0xFF12171F), accent, 0.07f)
                              : unchecked((int)0xFFFFFFFF);
            int hair = Dui.WithAlpha(ink, dark ? 22 : 14);

            var bmp = Bitmap.CreateBitmap(W, H, Bitmap.Config.Argb8888);
            var cv = new Canvas(bmp);

            // ---------- 1. 底色 ----------
            // 必须用 (int[], float[]) 重载：Color/long 重载在 API 29+ 会按 ColorLong 解析，
            // 半透明色会被判成非法色彩空间抛异常。
            var bg = new Paint(PaintFlags.AntiAlias);
            bg.SetShader(new LinearGradient(0f, 0f, 0f, H,
                new[] { bgTop, bgBot }, new[] { 0f, 1f }, Shader.TileMode.Clamp));
            cv.DrawRect(0, 0, W, H, bg);

            // ---------- 2. 主题色光球（模糊感 = 多段径向渐变，边缘完全化开） ----------
            Blob(cv, W * 0.88f, H * 0.02f, W * 0.78f, Dui.WithAlpha(accent, dark ? 120 : 70));
            Blob(cv, W * 0.02f, H * 1.00f, W * 0.66f, Dui.WithAlpha(accent, dark ? 82 : 48));
            Blob(cv, W * 0.50f, H * 0.62f, W * 0.70f, Dui.WithAlpha(accent, dark ? 30 : 18));

            // ---------- 3. 内容卡 ----------
            var cardRect = new RectF(CARD_L, CARD_T, CARD_R, CARD_B);
            var cardFill = new Paint(PaintFlags.AntiAlias);
            cardFill.Color = new Color(cardBg);
            cv.DrawRoundRect(cardRect, 44f, 44f, cardFill);

            var cardStroke = new Paint(PaintFlags.AntiAlias);
            cardStroke.SetStyle(Paint.Style.Stroke);
            cardStroke.StrokeWidth = 2f;
            cardStroke.Color = new Color(Dui.WithAlpha(ink, dark ? 26 : 16));
            cv.DrawRoundRect(cardRect, 44f, 44f, cardStroke);

            // ---------- 4. 页眉：类型徽标 + 日期 ----------
            bool isLog = rec.IsLog;
            float badgeTop = CARD_T + 46f;
            DrawChip(cv, LEFT, badgeTop, isLog ? "通联日志" : "通联频率",
                accent, Dui.WithAlpha(accent, dark ? 46 : 26), 30f, 16f);

            string dateText = rec.LocalTime.ToString("yyyy.MM.dd");
            var datePaint = Text(32f, false, ink3, false);
            cv.DrawText(dateText, RIGHT - datePaint.MeasureText(dateText), badgeTop + 38f, datePaint);

            // ---------- 5. 主视觉 ----------
            float heroTop = badgeTop + 88f;
            float heroBottom = heroTop + 404f;
            var heroRect = new RectF(LEFT, heroTop, RIGHT, heroBottom);
            var heroFill = new Paint(PaintFlags.AntiAlias);
            heroFill.Color = new Color(Dui.WithAlpha(accent, dark ? 34 : 16));
            cv.DrawRoundRect(heroRect, 34f, 34f, heroFill);

            var heroStroke = new Paint(PaintFlags.AntiAlias);
            heroStroke.SetStyle(Paint.Style.Stroke);
            heroStroke.StrokeWidth = 2f;
            heroStroke.Color = new Color(Dui.WithAlpha(accent, dark ? 60 : 40));
            cv.DrawRoundRect(heroRect, 34f, 34f, heroStroke);

            string call = string.IsNullOrWhiteSpace(rec.Call) ? "" : rec.Call.ToUpperInvariant();
            bool hasCall = call.Length > 0;

            var capPaint = Text(27f, true, accent, true);
            DrawTracked(cv, isLog ? "呼号 / CALLSIGN" : "频率 / FREQUENCY",
                LEFT + 40f, heroTop + 74f, capPaint, 5f);

            // 主数字：先按宽度收敛字号，再逐字描（等宽 + 字距，读数感）
            string big = isLog
                ? (hasCall ? call : "—")
                : (rec.Freq > 0 ? rec.FreqText : "—");
            var bigPaint = Text(196f, true, ink, true);
            const float bigTrack = 2f;
            float bigMaxW = INNER_W - 80f;
            while (bigPaint.TextSize > 96f && MeasureTracked(bigPaint, big, bigTrack) > bigMaxW)
                bigPaint.TextSize -= 4f;
            DrawTracked(cv, big, LEFT + 40f, heroTop + 250f, bigPaint, bigTrack);

            // 频点记录：主数字右边跟一个单位，和详情卡片一样
            if (!isLog && rec.Freq > 0)
            {
                float ux = LEFT + 40f + MeasureTracked(bigPaint, big, bigTrack) + 16f;
                cv.DrawText(rec.UnitText, ux, heroTop + 250f, Text(44f, true, ink3, false));
            }

            // 次级行：日志看频率，频点看呼号（没有呼号就不画，不留占位文字）
            float subBase = heroTop + 362f;
            float chipLeft = LEFT + 40f;
            string freqLine = rec.Freq > 0 ? rec.FreqText : "—";

            if (isLog)
            {
                var f = Text(66f, true, accent, true);
                DrawTracked(cv, freqLine, LEFT + 40f, subBase, f, 1.5f);
                float ux = LEFT + 40f + MeasureTracked(f, freqLine, 1.5f) + 14f;
                var unitPaint = Text(32f, true, ink3, false);
                string unit = rec.Freq > 0 ? rec.UnitText : "";
                cv.DrawText(unit, ux, subBase, unitPaint);
                // chip 的起点必须按单位的实际宽度算，否则长呼号/长单位会把徽标压到文字上
                chipLeft = ux + unitPaint.MeasureText(unit) + 30f;
            }
            else if (hasCall)
            {
                var cp = Text(58f, true, ink, true);
                DrawTracked(cv, call, LEFT + 40f, subBase, cp, 2f);
                chipLeft = LEFT + 40f + MeasureTracked(cp, call, 2f) + 32f;
            }

            if (!string.IsNullOrWhiteSpace(rec.Mode))
            {
                float chipRight = DrawChip(cv, chipLeft, subBase - 66f, rec.Mode,
                    accent, Dui.WithAlpha(accent, dark ? 46 : 26), 34f, 18f);
                if (isLog && !string.IsNullOrWhiteSpace(rec.RstText) && chipRight + 210f < RIGHT - 40f)
                {
                    DrawChip(cv, chipRight + 18f, subBase - 66f, "RST " + rec.RstText,
                        ink2, Dui.WithAlpha(ink, dark ? 26 : 12), 34f, 18f);
                }
            }

            // ---------- 6. 读数表（两列，键在上、值在下，和详情卡片一致） ----------
            var pairs = BuildPairs(rec, call);
            // 落单时在第二个位置补空格：这样最后的时间两格（本地 / UTC）能配到同一行，
            // 而不是 UTC 孤零零占半行、右边一片空。
            if (pairs.Count % 2 == 1) pairs.Insert(1, new[] { "", "" });

            const float rowHMin = 128f, rowHMax = 168f;
            float tableTop = heroBottom + 44f;
            var keyPaint = Text(27f, false, ink3, false);
            var valPaint = Text(46f, false, ink, true);

            float footLine = CARD_B - 92f;
            int rows = Math.Max(1, (pairs.Count + 1) / 2);
            // 行高按剩余空间摊开（128–168px）：字段少的时候把空白吃掉，不留一大块空底
            float reserve = string.IsNullOrWhiteSpace(rec.Notes) ? 60f : 210f;
            float avail = footLine - reserve - tableTop;
            float rowH = Math.Min(rowHMax, Math.Max(rowHMin, avail / rows));

            for (int i = 0; i < pairs.Count; i += 2)
            {
                float top = tableTop + (i / 2) * rowH;

                cv.DrawRect(LEFT, top, RIGHT, top + 2f, LinePaint(hair, 2f));

                for (int col = 0; col < 2; col++)
                {
                    var cell = pairs[i + col];
                    if (cell[0].Length == 0) continue;
                    float x = col == 0 ? LEFT : LEFT + INNER_W * 0.54f;
                    float maxW = col == 0 ? INNER_W * 0.54f - 40f : INNER_W * 0.46f - 40f;
                    DrawTracked(cv, cell[0], x, top + 46f, keyPaint, 2f);
                    DrawFit(cv, cell[1], x, top + rowH - 26f, maxW, valPaint, 46f, 30f);
                }
            }

            float tableBottom = tableTop + rows * rowH;
            cv.DrawRect(LEFT, tableBottom, RIGHT, tableBottom + 2f, LinePaint(hair, 2f));

            // ---------- 7. 备注 ----------
            float noteY = tableBottom + 74f;
            if (!string.IsNullOrWhiteSpace(rec.Notes) && noteY < footLine - 60f)
            {
                DrawTracked(cv, "备注 / NOTE", LEFT, noteY, keyPaint, 2f);
                var notePaint = Text(38f, false, ink2, false);
                var lines = Wrap(notePaint, rec.Notes.Trim(), INNER_W, 2);
                for (int i = 0; i < lines.Count; i++)
                    cv.DrawText(lines[i], LEFT, noteY + 58f + i * 54f, notePaint);
            }

            // ---------- 8. 页脚 ----------
            cv.DrawRect(LEFT, footLine, RIGHT, footLine + 2f, LinePaint(hair, 2f));

            var footL = Text(28f, false, ink3, false);
            cv.DrawText("HAM记 · 记录每一段电波", LEFT, footLine + 62f, footL);

            string de = Profile.Call;
            if (!string.IsNullOrEmpty(de))
            {
                var footR = Text(30f, true, accent, true);
                string s = "DE " + de.ToUpperInvariant();
                DrawTracked(cv, s, RIGHT - MeasureTracked(footR, s, 2f), footLine + 62f, footR, 2f);
            }

            return bmp;
        }

        // ================= 内容 =================

        /// <summary>两列读数表的内容：只放有值的字段。设备 / 功率 / 天线分三格，不挤成一行。</summary>
        private static List<string[]> BuildPairs(QsoRecord rec, string call)
        {
            var list = new List<string[]>();
            void Add(string k, string v)
            {
                if (!string.IsNullOrWhiteSpace(v)) list.Add(new[] { k, v.Trim() });
            }

            Add("模式 / MODE", rec.Mode);
            if (rec.IsLog) Add("信号报告 / RST", rec.RstText);
            else Add("呼号 / CALLSIGN", call);

            Add("QTH / 位置", rec.Qth);
            Add("设备 / RIG", rec.Rig);
            Add("功率 / PWR", rec.Power);
            Add("天线 / ANT", rec.Ant);
            Add("本地时间 / LOCAL", rec.LocalTime.ToString("HH:mm") + " " + TimeKit.LocalAbbr(rec.Time));
            Add("协调世界时 / UTC", TimeKit.UtcHm(rec.Time) + " UTC");
            return list;
        }

        // ================= 绘制小工具 =================

        /// <summary>一团柔和的主题色光：径向渐变 + 五段 alpha，边缘完全化开，看着像高斯模糊。</summary>
        private static void Blob(Canvas cv, float cx, float cy, float r, int color)
        {
            int a = Color.GetAlphaComponent(color);
            int rgb = color & 0x00FFFFFF;

            var p = new Paint(PaintFlags.AntiAlias);
            p.SetShader(new RadialGradient(cx, cy, r,
                new[]
                {
                    (a << 24) | rgb,
                    ((int)(a * 0.55f) << 24) | rgb,
                    ((int)(a * 0.18f) << 24) | rgb,
                    ((int)(a * 0.04f) << 24) | rgb,
                    0
                },
                new[] { 0f, 0.34f, 0.62f, 0.84f, 1f }, Shader.TileMode.Clamp));
            cv.DrawCircle(cx, cy, r, p);
        }

        private static Paint Text(float size, bool mono, int color, bool bold)
        {
            var p = new Paint(PaintFlags.AntiAlias | PaintFlags.SubpixelText);
            p.SetTypeface(Typeface.Create(mono ? "monospace" : "sans-serif-medium",
                bold ? TypefaceStyle.Bold : TypefaceStyle.Normal));
            p.TextSize = size;
            p.Color = new Color(color);
            return p;
        }

        private static Paint LinePaint(int color, float width)
        {
            var p = new Paint(PaintFlags.AntiAlias);
            p.Color = new Color(color);
            p.StrokeWidth = width;
            return p;
        }

        private static float MeasureTracked(Paint p, string s, float track)
        {
            if (string.IsNullOrEmpty(s)) return 0f;
            float w = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                w += p.MeasureText(s[i].ToString());
                if (i < s.Length - 1) w += track;
            }
            return w;
        }

        private static void DrawTracked(Canvas cv, string s, float x, float baseline, Paint p, float track)
        {
            if (string.IsNullOrEmpty(s)) return;
            float cx = x;
            for (int i = 0; i < s.Length; i++)
            {
                string ch = s[i].ToString();
                cv.DrawText(ch, cx, baseline, p);
                cx += p.MeasureText(ch) + track;
            }
        }

        /// <summary>单行自适应：先缩字号，缩到底还长就截断加省略号。</summary>
        private static void DrawFit(Canvas cv, string s, float x, float baseline,
            float maxW, Paint p, float start, float min)
        {
            if (string.IsNullOrEmpty(s)) return;
            p.TextSize = start;
            while (p.TextSize > min && p.MeasureText(s) > maxW) p.TextSize -= 2f;

            if (p.MeasureText(s) <= maxW)
            {
                cv.DrawText(s, x, baseline, p);
                return;
            }

            var sb = new StringBuilder();
            float w = 0f;
            float ell = p.MeasureText("…");
            for (int i = 0; i < s.Length; i++)
            {
                float cw = p.MeasureText(s[i].ToString());
                if (w + cw + ell > maxW) break;
                sb.Append(s[i]);
                w += cw;
            }
            cv.DrawText(sb.Append("…").ToString(), x, baseline, p);
        }

        /// <summary>圆角徽标，返回右边界（方便接着排下一个）。</summary>
        private static float DrawChip(Canvas cv, float x, float top, string text, int fg, int bg,
            float size, float padX)
        {
            var tp = Text(size, false, fg, true);
            float tw = tp.MeasureText(text);
            float w = tw + padX * 2f;
            float h = size * 1.9f;

            var fill = new Paint(PaintFlags.AntiAlias);
            fill.Color = new Color(bg);
            cv.DrawRoundRect(new RectF(x, top, x + w, top + h), h * 0.32f, h * 0.32f, fill);

            var stroke = new Paint(PaintFlags.AntiAlias);
            stroke.SetStyle(Paint.Style.Stroke);
            stroke.StrokeWidth = 1.8f;
            stroke.Color = new Color(Dui.WithAlpha(fg, 80));
            cv.DrawRoundRect(new RectF(x, top, x + w, top + h), h * 0.32f, h * 0.32f, stroke);

            cv.DrawText(text, x + padX, top + h / 2f + size * 0.36f, tp);
            return x + w;
        }

        /// <summary>按宽度折行（中文按字断，英文也按字断——备注里基本是中英混排，够用）。</summary>
        private static List<string> Wrap(Paint p, string text, float maxW, int maxLines)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;

            var sb = new StringBuilder();
            float w = 0f;
            foreach (char ch in text)
            {
                if (ch == '\n')
                {
                    lines.Add(sb.ToString());
                    sb.Clear();
                    w = 0f;
                    if (lines.Count >= maxLines) break;
                    continue;
                }
                float cw = p.MeasureText(ch.ToString());
                if (w + cw > maxW && sb.Length > 0)
                {
                    lines.Add(sb.ToString());
                    sb.Clear();
                    w = 0f;
                    if (lines.Count >= maxLines) break;
                }
                sb.Append(ch);
                w += cw;
            }

            if (lines.Count < maxLines && sb.Length > 0) lines.Add(sb.ToString());
            if (lines.Count == maxLines && sb.Length > 0 && lines.Count > 0)
            {
                // 还有没排完的内容：最后一行补省略号
                string last = lines[lines.Count - 1];
                while (last.Length > 1 && p.MeasureText(last + "…") > maxW)
                    last = last.Substring(0, last.Length - 1);
                lines[lines.Count - 1] = last + "…";
            }
            return lines;
        }
    }
}
