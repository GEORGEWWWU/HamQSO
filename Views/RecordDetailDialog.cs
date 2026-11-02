using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Views;
using Android.Widget;
using HamJi.Core;

namespace HamJi.Views
{
    /// <summary>
    /// 记录详情大卡片（替代原来的系统 AlertDialog）。
    ///
    /// 构图：类型徽标 + 日期 → 主题色主视觉块（呼号 / 频率最大）→ 两列读数表 → 备注 → 固定按钮栏。
    /// 按钮栏固定在卡片底部（内容超长时只有中间那段滚动），顺序：删除 / 关闭 / 分享。
    /// 删除是二次确认：第一次点变红并提示，3 秒内再点才真删。
    /// </summary>
    public class RecordDetailDialog : Dialog
    {
        /// <summary>记录已被删除（调用方负责刷新列表）。</summary>
        public event Action Deleted;

        private readonly QsoRecord _rec;
        private readonly TicketCard _card;
        private View _footer;
        private ScrollView _scroll;
        private bool _closing;
        private bool _clamped;
        private Action _afterClose;

        // 删除二次确认
        private bool _confirming;
        private LinearLayout _delBtn;
        private TextView _delText;
        private ImageView _delIcon;
        private TextView _delHint;
        private Java.Lang.Runnable _confirmReset;

        public RecordDetailDialog(Context c, QsoRecord rec)
            : base(c, Resource.Style.TransparentDialogTheme)
        {
            _rec = rec;

            Window.RequestFeature(WindowFeatures.NoTitle);
            var win = Window;
            win.SetBackgroundDrawable(new ColorDrawable(Color.Transparent));
            win.AddFlags(WindowManagerFlags.DimBehind);
            win.SetDimAmount(0.48f);
            var wlp = win.Attributes;
            wlp.Gravity = GravityFlags.Center;
            wlp.Width = ViewGroup.LayoutParams.MatchParent;
            wlp.Height = ViewGroup.LayoutParams.MatchParent;
            win.Attributes = wlp;
            SetCanceledOnTouchOutside(true);

            var root = new FrameLayout(c);

            var scrim = new View(c);
            scrim.Clickable = true;
            scrim.Click += (s, e) => Dismiss();
            root.AddView(scrim, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

            // ---------- 卡片 ----------
            _card = new TicketCard(c);
            var bg = new GradientDrawable(GradientDrawable.Orientation.TopBottom,
                new[] { Dui.Mix(Dui.SheetBg, Color.White, Dui.Dark ? 0.05f : 0.35f), Dui.SheetBg });
            bg.SetCornerRadius(Dui.Dpf(26));
            _card.SetBackgroundDrawable(bg);
            _card.ClipToOutline = true;
            _card.OutlineProvider = ViewOutlineProvider.Background;
            _card.Elevation = Dui.Dpf(22);

            _card.AddView(BuildHeader());
            _card.AddView(BuildHero());

            // 中间可滚（备注很长时）
            var body = Dui.VBox();
            body.AddView(BuildGrid());
            BuildNotes(body);
            _scroll = new ScrollView(c);
            _scroll.VerticalScrollBarEnabled = false;
            _scroll.OverScrollMode = OverScrollMode.Never;
            _scroll.AddView(body, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
            _card.AddView(_scroll, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            _footer = BuildFooter();
            _card.AddView(_footer, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            int screenW = c.Resources.DisplayMetrics.WidthPixels;
            int cardW = Math.Min(screenW - Dui.Dp(40), Dui.Dp(380));
            root.AddView(_card, new FrameLayout.LayoutParams(cardW, ViewGroup.LayoutParams.WrapContent)
            { Gravity = GravityFlags.Center });

            SetContentView(root);

            _card.AddOnLayoutChangeListener(new LayoutWatcher(this));
        }

        public override void Show()
        {
            base.Show();
            try { Window?.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent); }
            catch { }
        }

        // ================= 页眉 =================

        private View BuildHeader()
        {
            var head = Dui.HBox();
            head.SetPadding(Dui.Dp(20), Dui.Dp(18), Dui.Dp(20), 0);

            var badge = Dui.Label(_rec.IsLog ? "通联日志" : "通联频率", 10.5f, Dui.Accent, true);
            badge.LetterSpacing = 0.08f;
            badge.SetPadding(Dui.Dp(9), Dui.Dp(4), Dui.Dp(9), Dui.Dp(4));
            badge.SetBackgroundDrawable(Dui.Round(Dui.ChipBg, 8));
            head.AddView(badge);

            head.AddView(new View(Context), new LinearLayout.LayoutParams(0, 1, 1f));

            var day = _rec.LocalTime;
            string[] wd = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
            var date = Dui.Label(day.ToString("yyyy.MM.dd") + " · " + wd[(int)day.DayOfWeek],
                11.5f, Dui.TextTertiary, false);
            head.AddView(date);
            return head;
        }

        // ================= 主视觉 =================

        private View BuildHero()
        {
            var hero = Dui.VBox();
            hero.SetBackgroundDrawable(Dui.Round(
                Dui.WithAlpha(Dui.Accent, Dui.Dark ? 30 : 18), 16));
            var hlp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            { TopMargin = Dui.Dp(14), LeftMargin = Dui.Dp(16), RightMargin = Dui.Dp(16) };
            hero.LayoutParameters = hlp;
            hero.SetPadding(Dui.Dp(16), Dui.Dp(14), Dui.Dp(16), Dui.Dp(14));

            bool isLog = _rec.IsLog;
            string call = string.IsNullOrWhiteSpace(_rec.Call) ? "" : _rec.Call.ToUpperInvariant();

            var cap = Dui.Label(isLog ? "呼号 / CALLSIGN" : "频率 / FREQUENCY", 9.5f, Dui.Accent, true);
            cap.LetterSpacing = 0.16f;
            hero.AddView(cap);

            // 主信息：日志看呼号，频点看频率
            var line = Dui.HBox();
            var llp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            { TopMargin = Dui.Dp(6) };
            line.LayoutParameters = llp;

            string bigText = isLog
                ? (call.Length > 0 ? call : "未填写呼号")
                : (_rec.Freq > 0 ? _rec.FreqText : "—");

            var big = Dui.Label(bigText, isLog ? 30f : 32f,
                isLog && call.Length == 0 ? Dui.TextTertiary : Dui.TextPrimary, true);
            big.SetTypeface(Dui.Mono, TypefaceStyle.Bold);
            big.LetterSpacing = -0.02f;
            big.SetSingleLine(true);
            big.Ellipsize = TextUtils.TruncateAt.End;
            line.AddView(big, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

            if (!isLog && _rec.Freq > 0)
            {
                var unit = Dui.Label(_rec.UnitText, 11f, Dui.TextTertiary, true);
                var ulp = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
                { LeftMargin = Dui.Dp(5), Gravity = GravityFlags.Bottom, BottomMargin = Dui.Dp(5) };
                line.AddView(unit, ulp);
            }
            hero.AddView(line);

            // 次行：频率 / 模式标签
            var sub = Dui.HBox();
            var slp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            { TopMargin = Dui.Dp(8) };
            sub.LayoutParameters = slp;

            // 次行里 chip 前面的间距只在「chip 前面真有内容」时才加，否则模式徽标会莫名右移
            bool hasLead = false;

            if (isLog)
            {
                hasLead = true;
                // 频率 + 单位合成一行，超长直接省略（卡片宽度是固定的，长数字不能撑破布局）
                var freq = Dui.Label(_rec.Freq > 0 ? Dui.FreqLine(_rec) : "—", 15f, Dui.Accent, true);
                freq.SetTypeface(Dui.Mono, TypefaceStyle.Bold);
                freq.SetSingleLine(true);
                freq.Ellipsize = TextUtils.TruncateAt.End;
                sub.AddView(freq, new LinearLayout.LayoutParams(
                    0, ViewGroup.LayoutParams.WrapContent, 1f));
            }
            else if (call.Length > 0)
            {
                hasLead = true;
                var c2 = Dui.Label(call, 14f, Dui.TextSecondary, true);
                c2.SetTypeface(Dui.Mono, TypefaceStyle.Normal);
                c2.SetSingleLine(true);
                c2.Ellipsize = TextUtils.TruncateAt.End;
                sub.AddView(c2, new LinearLayout.LayoutParams(
                    0, ViewGroup.LayoutParams.WrapContent, 1f));
            }

            if (!string.IsNullOrWhiteSpace(_rec.Mode))
            {
                var chip = Widgets.Chip(_rec.Mode);
                sub.AddView(chip, new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
                { LeftMargin = hasLead ? Dui.Dp(10) : 0 });
            }
            hero.AddView(sub);
            return hero;
        }

        // ================= 读数表 =================

        private View BuildGrid()
        {
            var cells = new List<string[]>();
            void Add(string k, string v)
            {
                if (!string.IsNullOrWhiteSpace(v)) cells.Add(new[] { k, v.Trim() });
            }

            Add("模式 MODE", _rec.Mode);
            Add("信号报告 RST", _rec.RstText);
            Add("QTH", _rec.Qth);
            Add("设备 RIG", _rec.Rig);
            Add("功率 PWR", _rec.Power);
            Add("天线 ANT", _rec.Ant);
            Add("本地时间 LOCAL", _rec.LocalTime.ToString("HH:mm") + " " + TimeKit.LocalAbbr(_rec.Time));
            Add("UTC", TimeKit.UtcHm(_rec.Time) + " UTC");

            var grid = Dui.VBox();
            grid.SetPadding(Dui.Dp(20), Dui.Dp(6), Dui.Dp(20), Dui.Dp(2));

            // 字段数为奇数时在第二个位置补一格空的：本地时间 / UTC 才能落在同一行，
            // 否则 UTC 会单独占左边半行，右边空一块。
            if (cells.Count % 2 == 1) cells.Insert(1, new[] { "", "" });

            for (int i = 0; i < cells.Count; i += 2)
            {
                var row = Dui.HBox();
                row.SetGravity(GravityFlags.Top);
                row.SetPadding(0, Dui.Dp(10), 0, Dui.Dp(10));

                row.AddView(Cell(cells[i][0], cells[i][1]),
                    new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

                if (i + 1 < cells.Count)
                {
                    row.AddView(Cell(cells[i + 1][0], cells[i + 1][1]),
                        new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
                        { LeftMargin = Dui.Dp(16) });
                }
                grid.AddView(row);
            }
            return grid;
        }

        private View Cell(string key, string val)
        {
            var box = Dui.VBox();
            var k = Dui.Label(key, 10f, Dui.TextTertiary, false);
            k.LetterSpacing = 0.08f;
            k.SetSingleLine(true);
            k.Ellipsize = TextUtils.TruncateAt.End;
            box.AddView(k);

            var v = Dui.Label(val, 13.5f, Dui.TextPrimary, true);
            v.SetSingleLine(true);
            v.Ellipsize = TextUtils.TruncateAt.End;
            box.AddView(v, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            { TopMargin = Dui.Dp(4) });
            return box;
        }

        private void BuildNotes(LinearLayout host)
        {
            if (string.IsNullOrWhiteSpace(_rec.Notes)) return;

            var box = Dui.VBox();
            box.SetPadding(Dui.Dp(20), Dui.Dp(6), Dui.Dp(20), Dui.Dp(4));

            var k = Dui.Label("备注 NOTE", 10f, Dui.TextTertiary, false);
            k.LetterSpacing = 0.08f;
            box.AddView(k);

            var panel = Dui.VBox();
            panel.SetBackgroundDrawable(Dui.Round(Dui.WithAlpha(Dui.TextPrimary, Dui.Dark ? 20 : 9), 12));
            panel.SetPadding(Dui.Dp(12), Dui.Dp(10), Dui.Dp(12), Dui.Dp(10));

            var t = Dui.Label(_rec.Notes.Trim(), 13f, Dui.TextSecondary, false);
            t.SetLineSpacing(Dui.Dpf(4f), 1f);
            // 备注最长 4 行，再长就省略（卡片高度有限，不能无限长）
            t.SetMaxLines(4);
            t.Ellipsize = TextUtils.TruncateAt.End;
            panel.AddView(t);
            box.AddView(panel, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            { TopMargin = Dui.Dp(7) });

            host.AddView(box);
        }

        // ================= 按钮栏 =================

        private View BuildFooter()
        {
            var box = Dui.VBox();
            // 顶部的分割线由 TicketCard 画（虚线 + 两侧缺口），这里留出呼吸空间。
            // 上边距 15dp：备注框底边到虚线的距离，比原来多 5dp，别压得太紧。
            box.SetPadding(0, Dui.Dp(15), 0, 0);

            _delHint = Dui.Label("再点一次删除，删除后无法恢复", 10.5f, Dui.Danger, false);
            _delHint.SetPadding(Dui.Dp(20), Dui.Dp(9), Dui.Dp(20), 0);
            _delHint.Visibility = ViewStates.Gone;
            box.AddView(_delHint);

            var row = Dui.HBox();
            row.SetPadding(Dui.Dp(14), Dui.Dp(12), Dui.Dp(14), Dui.Dp(14));

            // 左：删除（二次确认）
            _delBtn = Pill("删除", Resource.Drawable.ic_trash, Dui.Danger,
                Dui.WithAlpha(Dui.Danger, Dui.Dark ? 40 : 24), out _delText, out _delIcon);
            _delBtn.Click += (s, e) => OnDeleteTap();
            row.AddView(_delBtn);

            row.AddView(new View(Context), new LinearLayout.LayoutParams(0, 1, 1f));

            // 右：关闭 → 分享
            var close = Pill("关闭", 0, Dui.TextSecondary,
                Dui.WithAlpha(Dui.TextPrimary, Dui.Dark ? 26 : 12), out _, out _);
            close.Click += (s, e) => { ResetConfirm(); Dismiss(); };
            row.AddView(close, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
            { RightMargin = Dui.Dp(8) });

            var share = Pill("分享", Resource.Drawable.ic_share, Color.White, Dui.AccentFill, out _, out _);
            share.Click += (s, e) =>
            {
                ResetConfirm();
                // 先关详情，再浮出海报
                DismissWith(() => PosterPreviewDialog.Show(Context, _rec));
            };
            row.AddView(share);

            box.AddView(row);
            return box;
        }

        private LinearLayout Pill(string text, int iconRes, int fg, int bg,
            out TextView textView, out ImageView iconView)
        {
            var box = Dui.HBox();
            box.SetPadding(Dui.Dp(15), Dui.Dp(10), Dui.Dp(16), Dui.Dp(10));
            box.SetBackgroundDrawable(Dui.Round(bg, 13));
            box.Clickable = true;
            box.Focusable = true;

            iconView = null;
            if (iconRes != 0)
            {
                iconView = Dui.Icon(iconRes, 15, fg);
                box.AddView(iconView, new LinearLayout.LayoutParams(Dui.Dp(15), Dui.Dp(15))
                { RightMargin = Dui.Dp(6) });
            }

            textView = Dui.Label(text, 13f, fg, true);
            box.AddView(textView);

            HamJi.Core.Touch.Press(box, 0.95f);
            return box;
        }

        private void OnDeleteTap()
        {
            if (!_confirming)
            {
                _confirming = true;
                _delBtn.SetBackgroundDrawable(Dui.Round(Dui.Danger, 13));
                _delText.Text = "确认删除？";
                _delText.SetTextColor(new Color(Color.White));
                _delIcon?.SetColorFilter(new Color(Color.White));
                _delHint.Visibility = ViewStates.Visible;
                _delHint.Alpha = 0f;
                _delHint.Animate().Alpha(1f).SetDuration(140).Start();

                if (_confirmReset == null) _confirmReset = new Java.Lang.Runnable(ResetConfirm);
                _delBtn.RemoveCallbacks(_confirmReset);
                _delBtn.PostDelayed(_confirmReset, 3000);
                return;
            }

            // 第二次点击：真删
            ResetConfirm();
            try
            {
                Store.Remove(_rec.Id);
                Deleted?.Invoke();
            }
            catch (Exception ex)
            {
                Toast.MakeText(Context, "删除失败：" + ex.Message, ToastLength.Short).Show();
                return;
            }
            Dismiss();
        }

        private void ResetConfirm()
        {
            if (_confirmReset != null) _delBtn?.RemoveCallbacks(_confirmReset);
            if (!_confirming) return;
            _confirming = false;
            _delBtn.SetBackgroundDrawable(Dui.Round(Dui.WithAlpha(Dui.Danger, Dui.Dark ? 40 : 24), 13));
            _delText.Text = "删除";
            _delText.SetTextColor(new Color(Dui.Danger));
            _delIcon?.SetColorFilter(new Color(Dui.Danger));
            _delHint.Visibility = ViewStates.Gone;
        }

        // ================= 进出场 =================

        private void DismissWith(Action after)
        {
            _afterClose = after;
            Dismiss();
        }

        private void PlayIntro()
        {
            if (_card == null || _card.Height <= 0) return;
            _card.PivotX = _card.Width / 2f;
            _card.PivotY = _card.Height / 2f;
            _card.Alpha = 0f;
            _card.ScaleX = 0.94f;
            _card.ScaleY = 0.94f;
            _card.Animate().Alpha(1f).ScaleX(1f).ScaleY(1f).SetDuration(210)
                .SetInterpolator(new Android.Views.Animations.DecelerateInterpolator(1.6f)).Start();
        }

        private void DismissNow()
        {
            try { base.Dismiss(); } catch { }
            var after = _afterClose;
            _afterClose = null;
            if (after == null) return;
            try { after(); }
            catch (Exception ex)
            {
                Toast.MakeText(Context, "打开预览失败：" + ex.Message, ToastLength.Short).Show();
            }
        }

        public override void Dismiss()
        {
            if (_closing) return;
            _closing = true;
            ResetConfirm();
            if (_card == null || _card.Height <= 0) { DismissNow(); return; }
            _card.Animate().Cancel();
            _card.Animate().Alpha(0f).ScaleX(0.96f).ScaleY(0.96f).SetDuration(140)
                .SetInterpolator(new Android.Views.Animations.AccelerateInterpolator(1.4f))
                .WithEndAction(new Java.Lang.Runnable(DismissNow)).Start();
        }

        /// <summary>卡片超过屏幕高度时，把中间那段收成可滚动区，按钮栏永远贴在底部。</summary>
        private void ClampHeight()
        {
            if (_clamped || _card == null || _scroll == null) return;
            int screenH = Context.Resources.DisplayMetrics.HeightPixels;
            int maxH = (int)(screenH * 0.84f);
            if (_card.Height <= maxH) return;

            _clamped = true;
            int chrome = _card.Height - _scroll.Height;   // 页眉 + 主视觉 + 按钮栏
            int target = Math.Max(Dui.Dp(120), maxH - chrome);
            var lp = _scroll.LayoutParameters;
            lp.Height = target;
            _scroll.LayoutParameters = lp;
        }

        private sealed class LayoutWatcher : Java.Lang.Object, View.IOnLayoutChangeListener
        {
            private readonly RecordDetailDialog _o;
            private bool _intro;
            public LayoutWatcher(RecordDetailDialog o) { _o = o; }

            public void OnLayoutChange(View v, int l, int t, int r, int b, int ol, int ot, int or, int ob)
            {
                if (b - t <= 0) return;
                if (!_intro)
                {
                    _intro = true;
                    _o.PlayIntro();
                }
                _o.UpdatePerforation();
                _o.ClampHeight();
            }
        }

        /// <summary>票根线永远贴着按钮栏顶边，布局一变就重算。</summary>
        private void UpdatePerforation()
        {
            if (_card == null || _footer == null) return;
            float y = _footer.Top;
            if (Math.Abs(_card.PerforationY - y) < 0.5f) return;
            _card.PerforationY = y;
            _card.Invalidate();
        }

        /// <summary>
        /// 票根卡片：按钮栏上方画一条虚线，两端各抠一个半圆缺口。
        ///
        /// 缺口是真的"挖穿"卡片（PorterDuff.Clear），露出后面的遮罩层，所以必须开软件层 ——
        /// 硬件加速下 Clear 只会在这层上画成透明，看不出洞。
        /// </summary>
        private sealed class TicketCard : LinearLayout
        {
            /// <summary>票根线在卡片内的 Y 坐标（= 按钮栏顶边），&lt;0 表示还没量出来。</summary>
            public float PerforationY = -1f;

            private readonly Paint _dash;
            private readonly Paint _clear;
            private readonly float _notchR;

            public TicketCard(Context c) : base(c)
            {
                Orientation = Orientation.Vertical;
                SetLayerType(LayerType.Software, null);

                _notchR = Dui.Dpf(11f);

                _dash = new Paint(PaintFlags.AntiAlias);
                _dash.Color = new Color(Dui.WithAlpha(Dui.TextPrimary, Dui.Dark ? 92 : 60));
                _dash.StrokeWidth = Dui.Dpf(1.3f);
                _dash.SetPathEffect(new DashPathEffect(new[] { Dui.Dpf(4f), Dui.Dpf(4f) }, 0f));

                _clear = new Paint(PaintFlags.AntiAlias);
                _clear.SetXfermode(new PorterDuffXfermode(PorterDuff.Mode.Clear));
            }

            protected override void DispatchDraw(Canvas canvas)
            {
                base.DispatchDraw(canvas);
                if (PerforationY <= 0 || Width <= 0) return;

                float y = PerforationY;
                float inset = _notchR + Dui.Dpf(10f);
                canvas.DrawLine(inset, y, Width - inset, y, _dash);
                canvas.DrawCircle(0f, y, _notchR, _clear);
                canvas.DrawCircle(Width, y, _notchR, _clear);
            }
        }
    }
}
