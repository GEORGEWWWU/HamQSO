using System;
using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Views;
using Android.Widget;
using HamJi.Core;
using HamJi.Views;

namespace HamJi.Pages
{
    public abstract class BasePage : LinearLayout
    {
        protected readonly LinearLayout Header;
        protected readonly TextView TitleView;
        protected readonly TextView SubtitleView;
        protected readonly ScrollView Scroller;
        protected readonly LinearLayout ListHost;
        protected bool ShowHeader = true;
        private SwipeRow _openRow;

        protected BasePage(Context c) : base(c)
        {
            Orientation = Orientation.Vertical;

            Header = Dui.VBox();
            TitleView = Dui.Label("", 27f, Dui.TextPrimary, true);
            TitleView.LetterSpacing = -0.02f;
            Header.AddView(TitleView, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));

            SubtitleView = Dui.Label("", 12.5f, Dui.TextSecondary, false);
            var slp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
            { TopMargin = Dui.Dp(5) };
            Header.AddView(SubtitleView, slp);
            AddView(Header, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            Scroller = new ScrollView(c);
            Scroller.VerticalScrollBarEnabled = false;
            Scroller.OverScrollMode = OverScrollMode.IfContentScrolls;
            ListHost = Dui.VBox();
            Scroller.AddView(ListHost, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
            AddView(Scroller, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));
        }

        public void ApplyInsets(int topInset, int bottomSpace)
        {
            if (ShowHeader)
            {
                Header.Visibility = ViewStates.Visible;
                Header.SetPadding(Dui.Dp(22), topInset + Dui.Dp(12), Dui.Dp(22), Dui.Dp(6));
            }
            else
            {
                Header.Visibility = ViewStates.Gone;
            }
            ListHost.SetPadding(Dui.Dp(16), ShowHeader ? 0 : topInset + Dui.Dp(6), Dui.Dp(16), bottomSpace);
        }

        public void Refresh()
        {
            ListHost.RemoveAllViews();
            OnBuild();
        }

        protected abstract void OnBuild();

        /// <summary>长按记录 → 拉起新建页并自动填入数据（不改动直接退出即不保存）。</summary>
        protected void OpenEditor(QsoRecord rec)
        {
            try
            {
                var intent = new Intent(Context, typeof(NewRecordActivity));
                intent.PutExtra("edit_id", rec.Id);
                Context.StartActivity(intent);
                if (Context is Activity act) act.OverridePendingTransition(0, 0);
            }
            catch (Exception ex)
            {
                Toast.MakeText(Context, "无法打开编辑页：" + ex.Message, ToastLength.Short).Show();
            }
        }

        /// <summary>「共 N 条 · 最后更新于今天/昨天/yyyy年M月d日」。</summary>
        protected string UpdatedLine(int count, string unit)
        {
            var day = Store.LastUpdated.Date;
            var today = DateTime.Today;
            string when = day == today ? "今天"
                : day == today.AddDays(-1) ? "昨天"
                : day.Year == today.Year ? day.ToString("M月d日")
                : day.ToString("yyyy年M月d日");
            return "共 " + count + " " + unit + " · 最后更新于" + when;
        }

        protected void RenderGrouped(List<QsoRecord> items, Func<QsoRecord, View> rowFactory,
            int emptyIcon, string emptyTitle, string emptyDesc)
        {
            if (items == null || items.Count == 0)
            {
                ListHost.AddView(Widgets.EmptyState(emptyIcon, emptyTitle, emptyDesc));
                return;
            }

            var today = DateTime.Today;
            foreach (var g in items.GroupBy(x => x.LocalTime.Date))
            {
                var day = g.Key;
                string label;
                string extra = null;
                if (day == today) label = "今天";
                else if (day == today.AddDays(-1)) label = "昨天";
                else if (day.Year == today.Year) { label = day.ToString("M月d日"); }
                else { label = day.ToString("yyyy年M月d日"); }

                ListHost.AddView(Widgets.DayHeader(label, extra, g.Count()));

                foreach (var rec in g)
                {
                    var record = rec;
                    var swipe = new SwipeRow(Context, rowFactory(rec));
                    swipe.Opened += sw =>
                    {
                        if (_openRow != null && _openRow != sw) _openRow.Close();
                        _openRow = sw;
                    };
                    swipe.DeleteRequested += sw =>
                    {
                        Store.Remove(record.Id);
                        _openRow = null;
                        Refresh();
                    };
                    ListHost.AddView(swipe);
                }
            }
        }

        protected void ShowDetail(QsoRecord rec)
        {
            var sb = new System.Text.StringBuilder();
            if (rec.IsLog)
            {
                sb.AppendLine("呼号： " + (string.IsNullOrWhiteSpace(rec.Call) ? "—" : rec.Call));
                sb.AppendLine("频率： " + Dui.FreqLine(rec));
                sb.AppendLine("模式： " + (string.IsNullOrWhiteSpace(rec.Mode) ? "—" : rec.Mode));
                sb.AppendLine("时间： " + rec.LocalTime.ToString("yyyy-MM-dd HH:mm"));
                sb.AppendLine("信号报告： " + (string.IsNullOrWhiteSpace(rec.RstText) ? "—" : rec.RstText));
                if (!string.IsNullOrWhiteSpace(rec.Qth)) sb.AppendLine("QTH： " + rec.Qth);
                if (!string.IsNullOrWhiteSpace(rec.Rig)) sb.AppendLine("设备： " + rec.Rig);
                if (!string.IsNullOrWhiteSpace(rec.Power)) sb.AppendLine("功率： " + rec.Power);
                if (!string.IsNullOrWhiteSpace(rec.Ant)) sb.AppendLine("天线： " + rec.Ant);
            }
            else
            {
                sb.AppendLine("频率： " + Dui.FreqLine(rec));
                sb.AppendLine("模式： " + (string.IsNullOrWhiteSpace(rec.Mode) ? "—" : rec.Mode));
                sb.AppendLine("接收时间： " + rec.LocalTime.ToString("yyyy-MM-dd HH:mm"));
            }
            if (!string.IsNullOrWhiteSpace(rec.Notes)) sb.AppendLine("备注： " + rec.Notes);

            new AlertDialog.Builder(Context, Resource.Style.AppDialogTheme)
                .SetTitle("记录详情")
                .SetMessage(sb.ToString().TrimEnd())
                .SetPositiveButton("关闭", (s, e) => { })
                .SetNeutralButton("删除", (s, e) => ConfirmDelete(rec))
                .Show();
        }

        protected void ConfirmDelete(QsoRecord rec)
        {
            new AlertDialog.Builder(Context, Resource.Style.AppDialogTheme)
                .SetTitle("删除这条记录？")
                .SetMessage("删除后无法恢复。")
                .SetNegativeButton("取消", (s, e) => { })
                .SetPositiveButton("删除", (s, e) =>
                {
                    Store.Remove(rec.Id);
                    Refresh();
                })
                .Show();
        }
    }
}
