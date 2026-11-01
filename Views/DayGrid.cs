using System;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using HamJi.Core;

namespace HamJi.Views
{
    /// <summary>
    /// 自绘月历（月份导航 + 星期表头 + 6×7 格子 + 今天/昨天/前天 快捷）。
    /// 新建页的底部日期弹层和搜索页的居中日期弹层共用这一份。
    /// </summary>
    internal sealed class DayGrid
    {
        private readonly LinearLayout _card;
        private readonly DateTime _today;
        private readonly Action<DateTime> _onPick;
        private readonly string _clearText;
        private readonly Action _onClear;

        private DateTime _sel;
        private DateTime _month;
        private LinearLayout _grid;
        private TextView _monthLabel;

        private DayGrid(LinearLayout card, DateTime today, DateTime sel,
            Action<DateTime> onPick, string clearText, Action onClear)
        {
            _card = card;
            _today = today;
            _sel = sel.Date;
            _month = new DateTime(_sel.Year, _sel.Month, 1);
            _onPick = onPick;
            _clearText = clearText;
            _onClear = onClear;
        }

        /// <summary>把月历追加到 card 里。<paramref name="clearText"/> 非空时，快捷行末尾多一个「清除」入口。</summary>
        public static void Build(LinearLayout card, DateTime today, DateTime sel,
            Action<DateTime> onPick, string clearText = null, Action onClear = null)
        {
            new DayGrid(card, today, sel, onPick, clearText, onClear).Render();
        }

        private void Render()
        {
            var ctx = _card.Context;

            // 月份导航
            var nav = Dui.HBox();
            nav.SetPadding(0, Dui.Dp(12), 0, Dui.Dp(4));
            var prev = NavButton(ctx, "‹");
            var next = NavButton(ctx, "›");
            prev.Click += (s, e) => Shift(-1);
            next.Click += (s, e) => Shift(1);
            _monthLabel = Dui.Label("", 14f, Dui.TextPrimary, true);
            _monthLabel.Gravity = GravityFlags.Center;
            nav.AddView(prev, new LinearLayout.LayoutParams(Dui.Dp(36), Dui.Dp(36)));
            nav.AddView(_monthLabel, new LinearLayout.LayoutParams(0, Dui.Dp(36), 1f));
            nav.AddView(next, new LinearLayout.LayoutParams(Dui.Dp(36), Dui.Dp(36)));
            _card.AddView(nav, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            // 星期表头（周一起）
            var week = Dui.HBox();
            week.SetPadding(0, Dui.Dp(4), 0, Dui.Dp(2));
            foreach (var w in new[] { "一", "二", "三", "四", "五", "六", "日" })
            {
                var tv = Dui.Label(w, 11f, Dui.TextTertiary, false);
                tv.Gravity = GravityFlags.Center;
                week.AddView(tv, new LinearLayout.LayoutParams(0, Dui.Dp(24), 1f));
            }
            _card.AddView(week, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            _grid = Dui.VBox();
            _card.AddView(_grid, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            // 快捷
            var quick = Dui.HBox();
            quick.SetPadding(0, Dui.Dp(12), 0, 0);
            AddChip(ctx, quick, "今天", () => Pick(_today));
            AddChip(ctx, quick, "昨天", () => Pick(_today.AddDays(-1)));
            AddChip(ctx, quick, "前天", () => Pick(_today.AddDays(-2)));
            if (!string.IsNullOrEmpty(_clearText) && _onClear != null)
            {
                var chip = SheetShell.Chip(_clearText);
                chip.SetTextColor(new Color(Dui.Accent));
                chip.Click += (s, e) => { _onClear(); };
                var lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dui.Dp(34));
                lp.LeftMargin = Dui.Dp(4);
                quick.AddView(chip, lp);
            }
            _card.AddView(quick, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            RenderMonth();
        }

        private void AddChip(Context ctx, LinearLayout row, string text, Action act)
        {
            var chip = SheetShell.Chip(text);
            chip.Click += (s, e) => act();
            var lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dui.Dp(34));
            lp.RightMargin = Dui.Dp(8);
            row.AddView(chip, lp);
        }

        private TextView NavButton(Context ctx, string glyph)
        {
            var tv = Dui.Label(glyph, 18f, Dui.TextSecondary, false);
            tv.Gravity = GravityFlags.Center;
            tv.Clickable = true;
            tv.SetBackgroundDrawable(Dui.Round(Dui.InputBg, 12));
            HamJi.Core.Touch.Press(tv, 0.92f);
            return tv;
        }

        private void Shift(int months)
        {
            _month = _month.AddMonths(months);
            RenderMonth();
        }

        private void RenderMonth()
        {
            _monthLabel.Text = _month.Year + "年" + _month.Month + "月";
            _grid.RemoveAllViews();

            int offset = (((int)_month.DayOfWeek) + 6) % 7;   // 周一为第一列
            var start = _month.AddDays(-offset);

            for (int r = 0; r < 6; r++)
            {
                var row = Dui.HBox();
                for (int c = 0; c < 7; c++)
                {
                    var day = start.AddDays(r * 7 + c);
                    row.AddView(DayCell(day), new LinearLayout.LayoutParams(0, Dui.Dp(40), 1f));
                }
                _grid.AddView(row, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dui.Dp(40)));
            }
        }

        private View DayCell(DateTime day)
        {
            bool inMonth = day.Month == _month.Month;
            bool selected = day.Date == _sel.Date;
            bool isToday = day.Date == _today;

            var box = new FrameLayout(_card.Context);
            var tv = Dui.Label(day.Day.ToString(), 14f,
                selected ? Color.White : (inMonth ? (isToday ? Dui.Accent : Dui.TextPrimary) : Dui.TextTertiary),
                selected || isToday);
            tv.Gravity = GravityFlags.Center;
            box.AddView(tv, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

            if (selected)
            {
                var g = new GradientDrawable(GradientDrawable.Orientation.TopBottom, new[] { Dui.AccentFill, Dui.AccentFill2 });
                g.SetCornerRadius(Dui.Dpf(20));
                tv.SetBackgroundDrawable(g);
            }
            else if (isToday)
            {
                var ring = new GradientDrawable();
                ring.SetColor(Color.Transparent);
                ring.SetCornerRadius(Dui.Dpf(20));
                ring.SetStroke(Math.Max(1, Dui.Dp(1.2f)), new Color(Dui.WithAlpha(Dui.Accent, 120)));
                tv.SetBackgroundDrawable(ring);
            }

            if (!inMonth) tv.Alpha = 0.45f;

            box.Clickable = true;
            HamJi.Core.Touch.Press(box, 0.94f);
            box.Click += (s, e) => Pick(day);
            return box;
        }

        private void Pick(DateTime day)
        {
            _sel = day.Date;
            try { _onPick?.Invoke(day.Date); } catch { }
        }
    }
}
