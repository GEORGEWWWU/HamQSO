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
        /// <summary>标题行右侧的图标入口容器（搜索 / 云同步等）。</summary>
        protected readonly LinearLayout ActionsHost;
        protected readonly ScrollView Scroller;
        protected readonly LinearLayout ListHost;
        protected bool ShowHeader = true;
        private SwipeRow _openRow;
        /// <summary>被收起的日期（跨 Refresh 保留折叠状态）。</summary>
        private readonly HashSet<DateTime> _collapsedDays = new HashSet<DateTime>();
        private LazyGroupList _lazy;
        private int _insetTop;
        private int _insetBottom;
        private bool _insetsKnown;

        protected BasePage(Context c) : base(c)
        {
            Orientation = Orientation.Vertical;

            Header = Dui.VBox();

            // 主标题与右侧图标入口同一行：入口只跟主标题对齐，不跟着副标题往下沉
            var titleRow = Dui.HBox();
            TitleView = Dui.Label("", 27f, Dui.TextPrimary, true);
            TitleView.LetterSpacing = -0.02f;
            TitleView.SetSingleLine(true);
            TitleView.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
            titleRow.AddView(TitleView, new LinearLayout.LayoutParams(
                0, ViewGroup.LayoutParams.WrapContent, 1f));

            ActionsHost = Dui.HBox();
            titleRow.AddView(ActionsHost, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));

            Header.AddView(titleRow, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            SubtitleView = Dui.Label("", 12.5f, Dui.TextSecondary, false);
            var slp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
            { TopMargin = Dui.Dp(5) };
            Header.AddView(SubtitleView, slp);
            AddView(Header, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            Scroller = new ScrollView(c);
            Scroller.VerticalScrollBarEnabled = false;
            Scroller.SetOnTouchListener(new DragWatcher(this));
            // 上下滚动（含惯性）也会打断左右滑：把展开的行收回去
            Scroller.ScrollChange += (s2, e2) => CloseOpenRow();
            Scroller.OverScrollMode = OverScrollMode.IfContentScrolls;
            ListHost = Dui.VBox();
            Scroller.AddView(ListHost, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
            AddView(Scroller, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));
        }

        public void ApplyInsets(int topInset, int bottomSpace)
        {
            _insetTop = topInset;
            _insetBottom = bottomSpace;
            _insetsKnown = true;
            ApplyChrome();
        }

        /// <summary>
        /// 按当前 insets 与 ShowHeader 重排内边距。ShowHeader 可能在 OnBuild 里才被改掉，
        /// 所以构建完必须再调一次，否则首次进入和再次进入的顶部留白会不一致。
        /// </summary>
        private void ApplyChrome()
        {
            if (ShowHeader)
            {
                Header.Visibility = ViewStates.Visible;
                Header.SetPadding(Dui.Dp(22), _insetTop + Dui.Dp(12), Dui.Dp(22), Dui.Dp(6));
            }
            else
            {
                Header.Visibility = ViewStates.Gone;
            }
            ListHost.SetPadding(Dui.Dp(16), ShowHeader ? 0 : _insetTop + Dui.Dp(10), Dui.Dp(16), _insetBottom);
        }

        /// <summary>在标题行右侧加一个纯图标入口（无底色/无阴影/无边框）。</summary>
        protected IconAction AddHeaderAction(int iconRes, Action onClick, float iconDp = 21f, float boxDp = 32f)
        {
            var btn = new IconAction(Context, iconRes, (int)iconDp, (int)boxDp);
            var lp = new LinearLayout.LayoutParams(Dui.Dp(boxDp), Dui.Dp(boxDp))
            { LeftMargin = Dui.Dp(6) };
            ActionsHost.AddView(btn, lp);
            btn.Click += (s, e) => onClick();
            return btn;
        }

        /// <summary>通联频率 / 通联日志页右上角固定的两个入口，顺序：搜索、云同步。</summary>
        protected void AddHeaderEntries()
        {
            AddHeaderAction(Resource.Drawable.ic_search, OpenSearch);
            AddHeaderAction(Resource.Drawable.ic_cloud_upload, () =>
                Toast.MakeText(Context, "云同步入口已预留，功能开发中", ToastLength.Short).Show());
        }

        /// <summary>搜索是二级页：压在 tab 页之上，返回即关闭。</summary>
        protected void OpenSearch()
        {
            if (Context is MainActivity act) act.OpenSearch();
        }

        /// <summary>手指拖动列表：true=拖动中，false=松手/取消（返回 false 不消费事件，滚动照旧）。</summary>
        public event Action<bool> DragStateChanged;

        private void RaiseDrag(bool dragging)
        {
            // 上下拖动 = 打断左右滑：先把手动展开的那一行收回去
            if (dragging) CloseOpenRow();
            try { DragStateChanged?.Invoke(dragging); } catch { }
        }

        /// <summary>收起当前展开（露出删除钮）的那一行。</summary>
        public void CloseOpenRow()
        {
            var r = _openRow;
            if (r == null) return;
            _openRow = null;
            try { r.Close(); } catch { }
        }

        private sealed class DragWatcher : Java.Lang.Object, View.IOnTouchListener
        {
            private enum Axis { None, Vertical, Horizontal }

            private readonly BasePage _page;
            private readonly int _slopX = Dui.Dp(8);
            private readonly int _slopY = Dui.Dp(16);   // 上下要明显更大才开始算滚动，别抢左右滑
            private float _x0, _y0;
            private Axis _lock;

            public DragWatcher(BasePage page) { _page = page; }

            public bool OnTouch(View v, MotionEvent e)
            {
                if (e == null) return false;
                switch (e.ActionMasked)
                {
                    case MotionEventActions.Down:
                        _x0 = e.RawX;
                        _y0 = e.RawY;
                        _lock = Axis.None;
                        break;

                    case MotionEventActions.Move:
                        if (_lock != Axis.None) break;   // 本次手势方向已经定死，不再改判
                        float dx = Math.Abs(e.RawX - _x0);
                        float dy = Math.Abs(e.RawY - _y0);
                        // 先判横向：手指想左右滑的时候，中途抖一下不能把行收回去
                        if (dx > _slopX && dx > dy * 1.15f)
                        {
                            _lock = Axis.Horizontal;
                        }
                        else if (dy > _slopY && dy > dx * 1.6f)
                        {
                            _lock = Axis.Vertical;
                            _page.RaiseDrag(true);
                        }
                        break;

                    case MotionEventActions.Up:
                    case MotionEventActions.Cancel:
                        if (_lock == Axis.Vertical) _page.RaiseDrag(false);
                        _lock = Axis.None;
                        break;
                }
                return false;   // 必须 false，否则 ScrollView 收不到手势
            }
        }

        /// <summary>内容是否超过一屏（不够长的列表就不做"滚动时收起 FAB"这类处理）。</summary>
        public bool CanScroll
            => Scroller.ChildCount > 0 && Scroller.Height > 0
               && Scroller.GetChildAt(0).Height > Scroller.Height + Dui.Dp(4);

        private int _builtVersion = -1;

        /// <summary>
        /// 数据没变就别重建整页（切 tab 时省掉一次全量 View 树重建）。
        /// 需要强制刷新的场景（改设置等）直接调 Refresh()。
        /// </summary>
        public void RefreshIfStale()
        {
            if (ListHost.ChildCount > 0 && _builtVersion == Store.Version) return;
            Refresh();
        }

        public void Refresh()
        {
            _builtVersion = Store.Version;
            _lazy?.Detach();
            _lazy = null;
            ListHost.RemoveAllViews();
            OnBuild();
            if (_insetsKnown) ApplyChrome();
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
            var plans = new List<LazyGroupList.DayPlan>();
            foreach (var g in items.GroupBy(x => x.LocalTime.Date))
            {
                var day = g.Key;
                string label;
                if (day == today) label = "今天";
                else if (day == today.AddDays(-1)) label = "昨天";
                else if (day.Year == today.Year) label = day.ToString("M月d日");
                else label = day.ToString("yyyy年M月d日");

                var plan = new LazyGroupList.DayPlan { Key = day, Label = label };
                plan.Records.AddRange(g);
                plans.Add(plan);
            }

            _lazy?.Detach();
            _lazy = new LazyGroupList(Scroller, ListHost,
                rec => WrapRow(rec, rowFactory),
                key => _collapsedDays.Contains(key),
                (key, expanded) =>
                {
                    if (expanded) _collapsedDays.Remove(key);
                    else _collapsedDays.Add(key);
                });
            _lazy.Build(plans);
        }

        /// <summary>把行包进 SwipeRow，并接上滑动删除与「同一时间只开一个」的逻辑。</summary>
        private SwipeRow WrapRow(QsoRecord rec, Func<QsoRecord, View> rowFactory)
        {
            var swipe = new SwipeRow(Context, rowFactory(rec));
            swipe.Opened += sw =>
            {
                if (_openRow != null && _openRow != sw) _openRow.Close();
                _openRow = sw;
            };
            swipe.DeleteRequested += sw =>
            {
                Store.Remove(rec.Id);
                _openRow = null;
                Refresh();
            };
            return swipe;
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
