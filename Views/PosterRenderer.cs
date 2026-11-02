using System;
using System.Collections.Generic;
using System.Text;
using Android.Graphics;
using HamJi.Core;

namespace HamJi.Views
{
    /// <summary>
    /// 通联卡片海报：用 Canvas 逐层绘制成一张 1200×1600（3:4）的位图。
    ///
    /// 刻意和详情卡片长得不一样：详情卡片是「信息面板」（标签在左、值在右、按钮在底），
    /// 海报是「仪表 / HUD」构图 —— 细网格 + 主题色光晕 + 四角定位框 + 信号波形，
    /// 呼号 / 频率放在左上主视觉位，数据以两列读数表铺开，底部压一条台站信息栏。
    ///
    /// 配色全部取自 Dui（跟随系统深浅色 + 设置里的主题色），不写死颜色。
    /// </summary>
    public static class PosterRenderer
    {
        public const int W = 1200;
        public const int H = 1600;
        private const int M = 88;        // 页面外边距
        private const int CX = 134;      // 正文左边界（给主题色竖条让位）

        public static Bitmap Render(QsoRecord rec)
        {
            bool dark = Dui.Dark;
            int accent = Dui.Accent;

            // ---------- 调色板 ----------
            int bg0 = dark ? Dui.Mix(unchecked((int)0xFF05080E), accent, 0.13f)
                           : Dui.Mix(unchecked((int)0xFFF8FBFF), accent, 0.05f);
            int bg1 = dark ? unchecked((int)0xFF010306) : unchecked((int)0xFFE5EDF9);
            int ink = dark ? unchecked((int)0xFFF2F7FF) : unchecked((int)0xFF0A1420);
            int ink2 = dark ? unchecked((int)0xFF9DAEC2) : unchecked((int)0xFF53647A);
            int ink3 = dark ? unchecked((int)0xFF5E7186) : unchecked((int)0xFF8A97A6);
            int line = Dui.WithAlpha(accent, dark ? 46 : 38);

            var bmp = Bitmap.CreateBitmap(W, H, Bitmap.Config.Argb8888);
            var cv = new Canvas(bmp);

            // ---------- 1. 底：竖向渐变 ----------
            // 注意：必须用 (int[], float[]) 这个老重载。Xamarin 的 Color / long 重载在 API 29+
            // 会走 ColorLong 解析，半透明或 alpha=0 的颜色直接被判成非法色彩空间而抛异常。
            var bg = new Paint(PaintFlags.AntiAlias);
            bg.SetShader(new LinearGradient(0f, 0f, 0f, H,
                new[] { bg0, bg1 }, new[] { 0f, 1f }, Shader.TileMode.Clamp));
            cv.DrawRect(0, 0, W, H, bg);

            // 顶部主题色光晕（整张图的"光源"）
            var glow = new Paint(PaintFlags.AntiAlias);
            glow.SetShader(new RadialGradient(W * 0.84f, -H * 0.04f, W * 0.92f,
                new[]
                {
                    Dui.WithAlpha(accent, dark ? 96 : 52),
                    Dui.WithAlpha(accent, dark ? 30 : 16),
                    Dui.WithAlpha(accent, 0)
                },
                new[] { 0f, 0.48f, 1f }, Shader.TileMode.Clamp));
            cv.DrawRect(0, 0, W, (int)(H * 0.72f), glow);

            // 左下补一团冷色，避免下半张发死
            var glow2 = new Paint(PaintFlags.AntiAlias);
            glow2.SetShader(new RadialGradient(W * 0.1f, H * 1.02f, W * 0.8f,
                new[]
                {
                    Dui.WithAlpha(accent, dark ? 44 : 26),
                    Dui.WithAlpha(accent, 0)
                },
                new[] { 0f, 1f }, Shader.TileMode.Clamp));
            cv.DrawRect(0, (int)(H * 0.45f), W, H, glow2);

            // ---------- 2. 细网格 ----------
            var grid = new Paint(PaintFlags.AntiAlias);
            grid.Color = new Color(Dui.WithAlpha(accent, dark ? 15 : 13));
            grid.StrokeWidth = 1.2f;
            for (int x = 0; x <= W; x += 60) cv.DrawLine(x, 0, x, H, grid);
            for (int y = 0; y <= H; y += 60) cv.DrawLine(0, y, W, y, grid);

            var grid2 = new Paint(PaintFlags.AntiAlias);
            grid2.Color = new Color(Dui.WithAlpha(accent, dark ? 26 : 20));
            grid2.StrokeWidth = 1.6f;
            for (int x = 0; x <= W; x += 300) cv.DrawLine(x, 0, x, H, grid2);
            for (int y = 0; y <= H; y += 300) cv.DrawLine(0, y, W, y, grid2);

            // ---------- 3. 扫描线纹理 ----------
            var scan = new Paint();
            scan.Color = new Color(dark ? Dui.WithAlpha(unchecked((int)0xFF9FC4FF), 7) : Dui.WithAlpha(ink, 6));
            for (int y = 0; y < H; y += 4) cv.DrawLine(0, y, W, y, scan);

            // ---------- 4. 四角定位框 ----------
            var bracket = new Paint(PaintFlags.AntiAlias);
            bracket.SetStyle(Paint.Style.Stroke);
            bracket.StrokeWidth = 3f;
            bracket.StrokeCap = Paint.Cap.Round;
            bracket.Color = new Color(Dui.WithAlpha(accent, dark ? 170 : 130));
            const int inset = 46, arm = 80;
            cv.DrawLine(inset, inset, inset + arm, inset, bracket);
            cv.DrawLine(inset, inset, inset, inset + arm, bracket);
            cv.DrawLine(W - inset, inset, W - inset - arm, inset, bracket);
            cv.DrawLine(W - inset, inset, W - inset, inset + arm, bracket);
            cv.DrawLine(inset, H - inset, inset + arm, H - inset, bracket);
            cv.DrawLine(inset, H - inset, inset, H - inset - arm, bracket);
            cv.DrawLine(W - inset, H - inset, W - inset - arm, H - inset, bracket);
            cv.DrawLine(W - inset, H - inset, W - inset, H - inset - arm, bracket);

            // ---------- 5. 页眉 ----------
            var tiny = Text(20f, true, Dui.WithAlpha(accent, dark ? 235 : 220), true);
            var tinyR = Text(26f, true, ink2, false);
            string tag = rec.IsLog ? "HAMJI · QSO ARCHIVE" : "HAMJI · FREQ ARCHIVE";
            DrawTracked(cv, tag, CX, 108, tiny, 7f);
            DrawTrackedRight(cv, rec.LocalTime.ToString("yyyy.MM.dd"), W - M, 108, tinyR, 1.5f);

            var h1 = Text(46f, false, ink, true);
            cv.DrawText(rec.IsLog ? "通联日志" : "通联频率", CX, 182, h1);

            var h2 = Text(30f, true, ink2, false);
            DrawTrackedRight(cv, rec.LocalTime.ToString("HH:mm") + "  " + TimeKit.LocalAbbr(rec.Time),
                W - M, 180, h2, 1.5f);

            cv.DrawRect(CX, 218, W - M, 220, LinePaint(line, 2f));

            // ---------- 6. 主视觉 ----------
            // 左侧主题色竖条（上实下虚）
            var barPaint = new Paint(PaintFlags.AntiAlias);
            barPaint.SetShader(new LinearGradient(0f, 268f, 0f, 640f,
                new[] { accent, Dui.WithAlpha(accent, 0) }, new[] { 0f, 1f }, Shader.TileMode.Clamp));
            cv.DrawRoundRect(new RectF(M, 268, M + 7, 640), 3.5f, 3.5f, barPaint);

            bool isLog = rec.IsLog;
            string call = string.IsNullOrWhiteSpace(rec.Call) ? "" : rec.Call.ToUpperInvariant();
            string big = isLog
                ? (call.Length > 0 ? call : "未知呼号")
                : (rec.Freq > 0 ? rec.FreqText : "—");

            var label = Text(23f, true, ink3, true);
            DrawTracked(cv, isLog ? "呼号 / CALLSIGN" : "频率 / FREQUENCY", CX, 322, label, 6f);

            // 主数字：先按宽度收敛字号，再逐字描（等宽 + 字距，读数感）
            var bigPaint = Text(150f, true, call.Length > 0 || !isLog ? ink : ink3, true);
            const float bigTrack = 2f;
            float bigMaxW = W - CX - M - 40;
            while (bigPaint.TextSize > 54f && MeasureTracked(bigPaint, big, bigTrack) > bigMaxW)
                bigPaint.TextSize -= 3f;

            const float bigBase = 496f;
            DrawTracked(cv, big, CX, bigBase, bigPaint, bigTrack);

            // 频率单位跟在主数字后面（只有频率类记录需要）
            if (!isLog && rec.Freq > 0)
            {
                float ux = CX + MeasureTracked(bigPaint, big, bigTrack) + 18f;
                var unit = Text(Math.Max(26f, bigPaint.TextSize * 0.24f), true, ink2, false);
                DrawTracked(cv, rec.UnitText, ux, bigBase, unit, 2f);
            }

            // 次级信息行
            float subBase = 578f;
            float chipTop = 612f;
            if (isLog)
            {
                var freqPaint = Text(52f, true, accent, true);
                string ft = rec.Freq > 0 ? rec.FreqText : "—";
                DrawTracked(cv, ft, CX, subBase, freqPaint, 1.5f);
                float ux = CX + MeasureTracked(freqPaint, ft, 1.5f) + 12f;
                var unit = Text(24f, true, ink3, false);
                DrawTracked(cv, rec.Freq > 0 ? rec.UnitText : "", ux, subBase, unit, 2f);
            }
            else
            {
                // 频率类记录：次级位置给呼号
                var callPaint = Text(44f, true, call.Length > 0 ? ink : ink3, true);
                DrawTracked(cv, call.Length > 0 ? call : "未记录呼号", CX, subBase, callPaint, 2f);
            }

            // 模式 / RST 徽标
            float chipX = CX;
            if (!string.IsNullOrWhiteSpace(rec.Mode))
                chipX = DrawChip(cv, chipX, chipTop, rec.Mode, accent, Dui.WithAlpha(accent, dark ? 44 : 30), 26f);
            if (isLog && !string.IsNullOrWhiteSpace(rec.RstText))
                DrawChip(cv, chipX + 14f, chipTop, "RST " + rec.RstText, ink2,
                    Dui.WithAlpha(ink, dark ? 26 : 14), 26f);

            // ---------- 7. 信号波形（分隔带） ----------
            // 716f：波形上沿要压住但不碰到模式 / RST 徽标（徽标底边 666 + 波形振幅 ≈ 42）
            DrawWave(cv, rec, accent, dark, 716f);

            // ---------- 8. 读数表 ----------
            var pairs = BuildPairs(rec, call);
            int rows = (pairs.Count + 1) / 2;
            const float rowH = 146f;
            float tableTop = 768f;

            var keyPaint = Text(22f, true, ink3, true);
            var valPaint = Text(38f, false, ink, true);

            for (int i = 0; i < rows; i++)
            {
                float top = tableTop + i * rowH;
                cv.DrawRect(CX, top, W - M, top + 1.4f, LinePaint(Dui.WithAlpha(accent, dark ? 34 : 26), 1.4f));

                for (int col = 0; col < 2; col++)
                {
                    int idx = i * 2 + col;
                    if (idx >= pairs.Count) break;
                    float x = col == 0 ? CX : W * 0.52f;
                    float maxW = (col == 0 ? W * 0.52f : W - M) - x - 24f;

                    DrawTracked(cv, pairs[idx][0], x, top + 46f, keyPaint, 3.5f);
                    DrawFit(cv, pairs[idx][1], x, top + 104f, maxW, valPaint, 38f, 24f);
                }
            }
            float tableBottom = tableTop + rows * rowH;
            cv.DrawRect(CX, tableBottom, W - M, tableBottom + 1.4f,
                LinePaint(Dui.WithAlpha(accent, dark ? 34 : 26), 1.4f));

            // ---------- 9. 备注 ----------
            float noteY = tableBottom + 62f;
            if (!string.IsNullOrWhiteSpace(rec.Notes) && noteY < H - 300)
            {
                DrawTracked(cv, "备注 / NOTE", CX, noteY, keyPaint, 3.5f);
                var notePaint = Text(30f, false, ink2, false);
                var lines = Wrap(notePaint, rec.Notes.Trim(), W - CX - M, 3);
                for (int i = 0; i < lines.Count; i++)
                    cv.DrawText(lines[i], CX, noteY + 56f + i * 46f, notePaint);
            }

            // ---------- 10. 页脚 ----------
            // 底部刻度尺
            var tick = new Paint();
            tick.Color = new Color(Dui.WithAlpha(accent, dark ? 90 : 70));
            for (int i = 0; i < 46; i++)
            {
                float x = CX + i * 20f;
                if (x > W - M) break;
                float hgt = i % 5 == 0 ? 20f : 10f;
                tick.StrokeWidth = i % 5 == 0 ? 3f : 2f;
                cv.DrawLine(x, H - 214f, x, H - 214f + hgt, tick);
            }

            cv.DrawRect(CX, H - 178, W - M, H - 176.4f, LinePaint(line, 1.6f));

            var footL = Text(24f, false, ink3, false);
            cv.DrawText("HAM记 · 记录每一段电波", CX, H - 128, footL);

            string de = Profile.Call;
            if (!string.IsNullOrEmpty(de))
            {
                var footR = Text(26f, true, accent, true);
                DrawTrackedRight(cv, "DE " + de, W - M, H - 128, footR, 3f);
            }

            // ---------- 11. 暗角 ----------
            var vig = new Paint(PaintFlags.AntiAlias);
            vig.SetShader(new RadialGradient(W / 2f, H * 0.44f, H * 0.78f,
                new[] { Color.Transparent, Dui.WithAlpha(Color.Black, dark ? 165 : 34) },
                new[] { 0.52f, 1f }, Shader.TileMode.Clamp));
            cv.DrawRect(0, 0, W, H, vig);

            return bmp;
        }

        // ================= 内容 =================

        /// <summary>两列读数表的内容：只放有值的字段，最多 6 格（3 行）。</summary>
        private static List<string[]> BuildPairs(QsoRecord rec, string call)
        {
            var list = new List<string[]>();
            void Add(string k, string v)
            {
                if (!string.IsNullOrWhiteSpace(v)) list.Add(new[] { k, v.Trim() });
            }

            Add("模式 / MODE", rec.Mode);
            Add("信号报告 / RST", rec.RstText);
            Add("QTH / 位置", rec.Qth);
            Add("设备 / RIG", Join(rec.Rig, rec.Power, rec.Ant));

            if (!rec.IsLog) Add("呼号 / CALLSIGN", call);

            Add("本地时间 / LOCAL", rec.LocalTime.ToString("HH:mm") + " " + TimeKit.LocalAbbr(rec.Time));
            Add("协调世界时 / UTC", TimeKit.UtcHm(rec.Time) + " UTC");

            return list.Count > 6 ? list.GetRange(0, 6) : list;
        }

        /// <summary>设备 / 功率 / 天线挤进同一格：读起来像一条台站配置。</summary>
        private static string Join(params string[] parts)
        {
            var sb = new StringBuilder();
            foreach (var p in parts)
            {
                if (string.IsNullOrWhiteSpace(p)) continue;
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(p.Trim());
            }
            return sb.ToString();
        }

        // ================= 绘制小工具 =================

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

        private static void DrawTrackedRight(Canvas cv, string s, float right, float baseline, Paint p, float track)
            => DrawTracked(cv, s, right - MeasureTracked(p, s, track), baseline, p, track);

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

            // 逐字累加，放不下就补省略号
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
        private static float DrawChip(Canvas cv, float x, float top, string text, int fg, int bg, float size)
        {
            var tp = Text(size, false, fg, true);
            float tw = tp.MeasureText(text);
            float w = tw + 34f;
            float h = 54f;

            var fill = new Paint(PaintFlags.AntiAlias);
            fill.Color = new Color(bg);
            cv.DrawRoundRect(new RectF(x, top, x + w, top + h), 12f, 12f, fill);

            var stroke = new Paint(PaintFlags.AntiAlias);
            stroke.SetStyle(Paint.Style.Stroke);
            stroke.StrokeWidth = 1.6f;
            stroke.Color = new Color(Dui.WithAlpha(fg, 90));
            cv.DrawRoundRect(new RectF(x, top, x + w, top + h), 12f, 12f, stroke);

            cv.DrawText(text, x + 17f, top + h / 2f + size * 0.36f, tp);
            return x + w;
        }

        /// <summary>信号波形：按记录 Id 生成固定的随机幅度，同一条记录每次导出都长一样。</summary>
        private static void DrawWave(Canvas cv, QsoRecord rec, int accent, bool dark, float centerY)
        {
            var baseLine = new Paint(PaintFlags.AntiAlias);
            baseLine.Color = new Color(Dui.WithAlpha(accent, dark ? 40 : 30));
            baseLine.StrokeWidth = 1.4f;
            cv.DrawLine(CX, centerY, W - M, centerY, baseLine);

            int seed = 7;
            string id = rec.Id ?? "";
            for (int i = 0; i < id.Length; i++) seed = unchecked(seed * 31 + id[i]);
            seed &= 0x7FFFFFFF;

            var wave = new Paint(PaintFlags.AntiAlias);
            wave.SetStyle(Paint.Style.Stroke);
            wave.StrokeWidth = 3f;
            wave.StrokeCap = Paint.Cap.Round;
            wave.StrokeJoin = Paint.Join.Round;
            wave.Color = new Color(Dui.WithAlpha(accent, dark ? 200 : 170));

            var path = new Path();
            float x = CX;
            path.MoveTo(x, centerY);
            while (x < W - M)
            {
                seed = unchecked(seed * 1103515245 + 12345) & 0x7FFFFFFF;
                float amp = 8f + ((seed >> 9) % 100) / 100f * 34f;
                float half = 8f + ((seed >> 3) % 100) / 100f * 12f;
                path.LineTo(x + half * 0.5f, centerY - amp);
                path.LineTo(x + half, centerY);
                x += half + 6f;
            }
            cv.DrawPath(path, wave);
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
