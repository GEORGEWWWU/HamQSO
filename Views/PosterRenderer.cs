using System;
using System.Collections.Generic;
using System.Text;
using Android.Graphics;
using HamJi.Core;

namespace HamJi.Views
{
    /// <summary>
    /// 分享海报：Canvas 画成 1200×1600（3:4）位图。
    ///
    /// 参考过的方向（不是凭空想）：
    /// · 传统 QSL 卡片：一张纸上一个巨大的呼号，四周角落里塞满极小的技术参数，中间不留色块；
    /// · Typographic Brutalism：极端字号对比（主标题吃掉半个版面，说明文字小到像标注）、
    ///   等宽字体做元数据、粗细分明的横线、元素直接压到出血边；
    /// · 柏林 techno / 工业风：纯黑或纯白底、紧缩无衬线大字、零圆角、细硬分割线。
    ///
    /// 所以这一版没有「卡片」这个概念 —— 所有元素直接坐在背景上，只有上下出血的细线做分区，
    /// 背景是加深的底色 + 几团糊开的主题色光球（模糊感保留，但不再是主体）。
    ///
    /// 排版纪律：任何一行文字在画之前都要先量宽（MeasureText / MeasureTracked），
    /// 超宽先缩字号、缩到底再省略号；绝不靠目测摆坐标。
    /// </summary>
    public static class PosterRenderer
    {
        public const int W = 1200;
        public const int H = 1600;

        private const int M = 72;                 // 出血边距（不是卡片内边距）
        private const int INNER = W - M * 2;      // 1056

        public static Bitmap Render(QsoRecord rec)
        {
            bool dark = Dui.Dark;
            int accent = Dui.Accent;

            // ---------- 调色板 ----------
            int bgTop = dark ? Dui.Mix(unchecked((int)0xFF070A10), accent, 0.16f)
                             : Dui.Mix(unchecked((int)0xFFFFFFFF), accent, 0.07f);
            int bgBot = dark ? unchecked((int)0xFF020306) : unchecked((int)0xFFEDF1F7);
            int ink = dark ? unchecked((int)0xFFFFFFFF) : unchecked((int)0xFF070C14);
            int ink2 = dark ? unchecked((int)0xFFB4C2D4) : unchecked((int)0xFF44566C);
            int ink3 = dark ? unchecked((int)0xFF6E8096) : unchecked((int)0xFF8B99A9);
            int hair = Dui.WithAlpha(ink, dark ? 34 : 26);
            int hairSoft = Dui.WithAlpha(ink, dark ? 20 : 14);

            var bmp = Bitmap.CreateBitmap(W, H, Bitmap.Config.Argb8888);
            var cv = new Canvas(bmp);

            // ---------- 1. 底 + 光球 ----------
            // (int[], float[]) 重载：Color/long 重载在 API 29+ 会按 ColorLong 解析，半透明色会抛异常
            var bg = new Paint(PaintFlags.AntiAlias);
            bg.SetShader(new LinearGradient(0f, 0f, 0f, H,
                new[] { bgTop, bgBot }, new[] { 0f, 1f }, Shader.TileMode.Clamp));
            cv.DrawRect(0, 0, W, H, bg);

            Blob(cv, W * 0.92f, H * 0.06f, W * 0.72f, Dui.WithAlpha(accent, dark ? 132 : 66));
            Blob(cv, W * 0.04f, H * 0.92f, W * 0.62f, Dui.WithAlpha(accent, dark ? 92 : 44));
            Blob(cv, W * 0.56f, H * 0.52f, W * 0.60f, Dui.WithAlpha(accent, dark ? 34 : 16));

            bool isLog = rec.IsLog;
            string call = string.IsNullOrWhiteSpace(rec.Call) ? "" : rec.Call.ToUpperInvariant();
            bool hasCall = call.Length > 0;

            // 字体：数字用紧缩无衬线（硬、窄、工业），元数据用等宽（终端 / 标注感），
            // 两者刻意不统一 —— 这在 typographic brutalism 里是标准做法。
            var numFace = Typeface.Create("sans-serif-condensed", TypefaceStyle.Bold);
            var monoFace = Typeface.Create("monospace", TypefaceStyle.Normal);
            var monoBold = Typeface.Create("monospace", TypefaceStyle.Bold);

            // ---------- 2. 顶部行程条 ----------
            var rail = Text(24f, monoBold, Dui.WithAlpha(accent, 235), true, 0f);
            DrawTracked(cv, "HAMJI / " + (isLog ? "QSO ARCHIVE" : "FREQ ARCHIVE"), M, 96, rail, 4f);

            var railR = Text(24f, monoFace, ink3, false, 0f);
            DrawTrackedRight(cv, rec.LocalTime.ToString("yyyy.MM.dd"), W - M, 96, railR, 1.6f);

            cv.DrawRect(M, 124, W - M, 126, LinePaint(hair, 2f));

            // ---------- 3. 主视觉：巨号呼号 / 频率 ----------
            var cap = Text(22f, monoBold, Dui.WithAlpha(accent, 225), true, 0f);
            DrawTracked(cv, isLog ? "呼号 / CALLSIGN" : "频率 / FREQUENCY", M, 194, cap, 5f);

            string big = isLog
                ? (hasCall ? call : "—")
                : (rec.Freq > 0 ? rec.FreqText : "—");

            // 字号从 300 往下收敛，直到塞得进 INNER（含单位位），再决定要不要画单位
            var bigPaint = new Paint(PaintFlags.AntiAlias | PaintFlags.SubpixelText | PaintFlags.LinearText);
            bigPaint.SetTypeface(numFace);
            bigPaint.Color = new Color(ink);
            float unitReserve = (!isLog && rec.Freq > 0) ? 150f : 0f;
            float bigSize = 300f;
            for (; bigSize > 110f; bigSize -= 4f)
            {
                bigPaint.TextSize = bigSize;
                if (bigPaint.MeasureText(big) + unitReserve <= INNER) break;
            }
            bigPaint.TextSize = bigSize;
            // 紧缩字距：大字要紧一点才「硬」，默认字距会显得软
            bigPaint.LetterSpacing = -0.03f;
            // 收到 110 还放不下（超长便携呼号如 VP2E/G4XYZ/P）：截断补省略号，别让它冲出画布
            if (bigPaint.MeasureText(big) > INNER - unitReserve)
                big = Ellipsize(bigPaint, big, INNER - unitReserve);
            cv.DrawText(big, M, 424, bigPaint);
            float bigRight = M + bigPaint.MeasureText(big);

            if (unitReserve > 0f && bigRight + 20f + 110f <= W - M)
            {
                var unitPaint = Text(44f, monoBold, ink3, false, 0f);
                cv.DrawText(rec.UnitText, bigRight + 20f, 424, unitPaint);
                bigRight += 20f + unitPaint.MeasureText(rec.UnitText);
            }

            // ---------- 4. 次级行：一行技术串，量宽后再画 ----------
            // 频点看呼号，日志看频率；模式 / RST 用「 / 」接上，不做圆角徽标（徽标会把版面拉软）
            var parts = new List<string>();
            if (isLog)
            {
                if (rec.Freq > 0) parts.Add(rec.FreqText + " " + rec.UnitText);
            }
            else if (hasCall)
            {
                parts.Add(call);
            }
            if (!string.IsNullOrWhiteSpace(rec.Mode)) parts.Add(rec.Mode.Trim());
            if (isLog && !string.IsNullOrWhiteSpace(rec.RstText)) parts.Add("RST " + rec.RstText.Trim());

            if (parts.Count > 0)
            {
                string joined = string.Join("   /   ", parts);
                var subPaint = new Paint(PaintFlags.AntiAlias | PaintFlags.SubpixelText);
                subPaint.SetTypeface(numFace);
                subPaint.Color = new Color(Dui.WithAlpha(accent, 245));
                float subSize = 46f;
                for (; subSize > 26f; subSize -= 2f)
                {
                    subPaint.TextSize = subSize;
                    if (subPaint.MeasureText(joined) <= INNER) break;
                }
                subPaint.TextSize = subSize;
                if (subPaint.MeasureText(joined) > INNER) joined = Ellipsize(subPaint, joined, INNER);
                cv.DrawText(joined, M, 486, subPaint);
            }

            // ---------- 5. 粗分割线（主题色） ----------
            cv.DrawRect(M, 522, W - M, 528, LinePaint(Dui.WithAlpha(accent, dark ? 230 : 200), 6f));

            // ---------- 5.5 「73」水印：位置永远固定，先画，让后面的线和字压在它上面 ----------
            // 这就是固定装饰，不跟着内容多少变化 —— 日志有 4 行读数、频点只有 2 行，水印都在同一处。
            float footLine = 1404f;
            {
                var wm = new Paint(PaintFlags.AntiAlias);
                wm.SetTypeface(numFace);
                wm.TextSize = 380f;
                wm.LetterSpacing = -0.05f;
                wm.Color = new Color(Dui.WithAlpha(accent, dark ? 30 : 24));
                float wmW = wm.MeasureText("73");
                // 右边只出血 40px：留一点裁切感，但不能把「3」整只切掉
                cv.DrawText("73", W - wmW + 40f, footLine - 34f, wm);
            }

            // ---------- 6. 读数表：两列，竖线 + 横线，没有卡片底 ----------
            var pairs = BuildPairs(rec, call);
            if (pairs.Count % 2 == 1) pairs.Insert(1, new[] { "", "" });
            int rows = Math.Max(1, (pairs.Count + 1) / 2);

            float tableTop = 560f;
            bool hasNotes = !string.IsNullOrWhiteSpace(rec.Notes);
            float notesReserve = hasNotes ? 190f : 0f;
            float rowH = Math.Min(190f, Math.Max(122f, (footLine - 56f - notesReserve - tableTop) / rows));
            // 字段少的时候整块往下挪一点，把空白均分到上下，而不是全堆在页脚上方
            float slack = (footLine - 56f) - (tableTop + rows * rowH + notesReserve);
            if (slack > 0f) tableTop += Math.Min(slack * 0.45f, 40f);

            var keyPaint = Text(22f, monoFace, ink3, false, 3f);
            var valPaint = new Paint(PaintFlags.AntiAlias | PaintFlags.SubpixelText);
            valPaint.SetTypeface(numFace);
            valPaint.Color = new Color(ink);
            valPaint.TextSize = 50f;

            float colRule = M + INNER * 0.5f;
            float colW = INNER * 0.5f - 36f;

            for (int i = 0; i < pairs.Count; i += 2)
            {
                float top = tableTop + (i / 2) * rowH;
                cv.DrawRect(M, top, W - M, top + 2f, LinePaint(hairSoft, 2f));

                for (int col = 0; col < 2; col++)
                {
                    var cell = pairs[i + col];
                    if (cell[0].Length == 0) continue;
                    float x = col == 0 ? M : colRule + 36f;

                    DrawTracked(cv, cell[0], x, top + 44f, keyPaint, 3f);
                    DrawFitLeft(cv, cell[1], x, top + rowH - 26f, colW, valPaint, 50f, 28f);
                }
            }

            float tableBottom = tableTop + rows * rowH;
            cv.DrawRect(M, tableBottom, W - M, tableBottom + 2f, LinePaint(hairSoft, 2f));
            // 两列之间的竖线只画在表格区，不穿越其它区块
            cv.DrawRect(colRule, tableTop, colRule + 1.6f, tableBottom, LinePaint(hairSoft, 1.6f));

            // ---------- 7. 备注 ----------
            float noteY = tableBottom + 58f;
            if (!string.IsNullOrWhiteSpace(rec.Notes) && noteY < footLine - 70f)
            {
                DrawTracked(cv, "备注 / NOTE", M, noteY, keyPaint, 3f);
                var notePaint = Text(34f, monoFace, ink2, false, 0f);
                var lines = Wrap(notePaint, rec.Notes.Trim(), INNER, 2);
                for (int i = 0; i < lines.Count; i++)
                    cv.DrawText(lines[i], M, noteY + 50f + i * 46f, notePaint);
            }

            // ---------- 8. 页脚 ----------
            cv.DrawRect(M, footLine, W - M, footLine + 2f, LinePaint(hair, 2f));

            var footL = Text(24f, monoFace, ink3, false, 0f);
            DrawTracked(cv, "HAM记 · 记录每一段电波", M, footLine + 58f, footL, 2f);

            string de = Profile.Call;
            if (!string.IsNullOrEmpty(de))
            {
                var footR = Text(26f, monoBold, Dui.WithAlpha(accent, 240), true, 0f);
                DrawTrackedRight(cv, "DE " + de.ToUpperInvariant(), W - M, footLine + 58f, footR, 3f);
            }

            return bmp;
        }

        // ================= 内容 =================

        /// <summary>读数表：字段顺序和详情卡片一致，空的直接不排（不留占位）。</summary>
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

        /// <summary>一团糊开的主题色光：径向渐变 + 五段 alpha，边缘完全化开，等于自带高斯模糊。</summary>
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

        private static Paint Text(float size, Typeface face, int color, bool bold, float track)
        {
            var p = new Paint(PaintFlags.AntiAlias | PaintFlags.SubpixelText);
            p.SetTypeface(face);
            p.TextSize = size;
            p.Color = new Color(color);
            // 字距一律由 DrawTracked 逐字累加控制，这里不设 Paint.LetterSpacing，
            // 否则逐字绘制时会被叠加两次。
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

        private static void DrawTrackedRight(Canvas cv, string s, float right, float baseline, Paint p, float track)
            => DrawTracked(cv, s, right - MeasureTracked(p, s, track), baseline, p, track);

        /// <summary>单行自适应：先缩字号，缩到底还长就截断补省略号。</summary>
        private static void DrawFitLeft(Canvas cv, string s, float x, float baseline,
            float maxW, Paint p, float start, float min)
        {
            if (string.IsNullOrEmpty(s)) return;
            p.TextSize = start;
            while (p.TextSize > min && p.MeasureText(s) > maxW) p.TextSize -= 2f;
            if (p.MeasureText(s) > maxW) s = Ellipsize(p, s, maxW);
            cv.DrawText(s, x, baseline, p);
        }

        /// <summary>按当前画笔宽度截断，末尾补「…」。</summary>
        private static string Ellipsize(Paint p, string s, float maxW)
        {
            float ell = p.MeasureText("…");
            var sb = new StringBuilder();
            float w = 0f;
            foreach (char ch in s)
            {
                float cw = p.MeasureText(ch.ToString());
                if (w + cw + ell > maxW) break;
                sb.Append(ch);
                w += cw;
            }
            return sb.Length == 0 ? "…" : sb.Append("…").ToString();
        }

        /// <summary>按宽度折行（中文按字断，英文也按字断——备注基本是中英混排，够用）。</summary>
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
                string last = lines[lines.Count - 1];
                while (last.Length > 1 && p.MeasureText(last + "…") > maxW)
                    last = last.Substring(0, last.Length - 1);
                lines[lines.Count - 1] = last + "…";
            }
            return lines;
        }
    }
}
