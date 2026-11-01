using System;
using System.Collections.Generic;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using HamJi.Core;

namespace HamJi.Views
{
    /// <summary>
    /// 分天列表的懒加载器。
    /// - 只构建视口附近的内容，滚动时按块补，新行进屏淡入；
    /// - 每补一块都会重新校验一次（Post 到下一帧），保证「滑到底但没加载出来」不会发生；
    /// - 整组被滑到上方很远时，把行换成等高占位（滚动位置不跳），回滚时再重建。
    /// </summary>
    public sealed class LazyGroupList
    {
        public sealed class DayPlan
        {
            public DateTime Key;
            public string Label;
            public string Extra;
            public readonly List<QsoRecord> Records = new List<QsoRecord>();
        }

        private sealed class Slot
        {
            public QsoRecord Rec;
            public View View;     // 真实行（SwipeRow）或等高占位
            public int Height;    // 量过的高度，占位用
            public int Margin;    // 行的下边距
            public bool Live;
        }

        private sealed class DayState
        {
            public DayPlan Plan;
            public DayGroup Group;
            public readonly List<Slot> Slots = new List<Slot>();
            public int Cursor;    // 已加载到第几行
        }

        private const int Chunk = 6;          // 每块行数
        private const int ReleaseRows = 28;   // 少于这个行数不做回收（避免来回抖动）

        private readonly ScrollView _scroller;
        private readonly LinearLayout _host;
        private readonly Func<QsoRecord, SwipeRow> _makeRow;
        private readonly Func<DateTime, bool> _isCollapsed;
        private readonly Action<DateTime, bool> _onToggle;

        private readonly List<DayState> _days = new List<DayState>();
        private View _sentinel;
        private int _dayCursor;
        private int _scrollY;
        private int _viewport;
        private int _totalRows;
        private int _lastReleaseY = int.MinValue;
        private bool _pumping;
        private bool _checkQueued;
        private bool _dead;

        public LazyGroupList(ScrollView scroller, LinearLayout host,
            Func<QsoRecord, SwipeRow> makeRow,
            Func<DateTime, bool> isCollapsed,
            Action<DateTime, bool> onToggle)
        {
            _scroller = scroller;
            _host = host;
            _makeRow = makeRow;
            _isCollapsed = isCollapsed;
            _onToggle = onToggle;
        }

        public void Build(List<DayPlan> plans)
        {
            _days.Clear();
            _dayCursor = 0;
            _scrollY = 0;
            _totalRows = 0;
            _lastReleaseY = int.MinValue;

            foreach (var p in plans)
            {
                var st = new DayState { Plan = p };
                foreach (var rec in p.Records) st.Slots.Add(new Slot { Rec = rec });
                _totalRows += p.Records.Count;
                _days.Add(st);
            }

            _sentinel = new View(_host.Context);
            _host.AddView(_sentinel, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, 1));

            _scroller.ScrollChange += OnScrollChanged;
            Pump();
        }

        public void Detach()
        {
            _dead = true;
            try { _scroller.ScrollChange -= OnScrollChanged; } catch { }
        }

        // ---------- 滚动 ----------

        private void OnScrollChanged(object sender, View.ScrollChangeEventArgs e)
        {
            if (_dead) return;
            _scrollY = e.ScrollY;
            if (_viewport <= 0) _viewport = _scroller.Height;

            Pump();

            // 回收/重建按半屏步长触发，避免每帧都算坐标
            if (_totalRows >= ReleaseRows && _viewport > 0 &&
                Math.Abs(_scrollY - _lastReleaseY) > _viewport / 2)
            {
                _lastReleaseY = _scrollY;
                ReleasePass();
            }
        }

        // ---------- 懒加载 ----------

        private bool ShouldLoad()
        {
            if (_dayCursor >= _days.Count) return false;
            int vp = _viewport > 0 ? _viewport : Dui.Dp(760);
            // 提前一屏半预加载：快速滑动时不至于看到空白
            return _sentinel.Top - _scrollY < vp + vp / 2;
        }

        private void Pump()
        {
            if (_dead || _pumping) return;
            _pumping = true;
            bool more;
            try
            {
                // 一次只补一块，避免一次滑动手势把整表都建出来
                if (ShouldLoad()) Step();
                more = ShouldLoad();
            }
            finally { _pumping = false; }

            if (more) ScheduleCheck();
        }

        /// <summary>
        /// 关键保底：只要「还没填够预加载区」就每帧再校验一次。
        /// 这样即使已经滑到底、ScrollChange 不再触发，也不会停在半截（不会出现滑到底却没加载）。
        /// </summary>
        private void ScheduleCheck()
        {
            if (_dead || _checkQueued) return;
            _checkQueued = true;
            _host.PostDelayed(CheckTick, 16);
        }

        private void CheckTick()
        {
            _checkQueued = false;
            if (_dead) return;
            if (_viewport <= 0) _viewport = _scroller.Height;
            Pump();
        }

        /// <summary>补一块：先建分组标题，再按 Chunk 加行。</summary>
        private bool Step()
        {
            while (_dayCursor < _days.Count)
            {
                var d = _days[_dayCursor];

                if (d.Group == null)
                {
                    CreateGroup(d);
                    return true;
                }

                if (d.Cursor >= d.Plan.Records.Count)
                {
                    _dayCursor++;
                    continue;
                }

                // 收起的分组不建行，直接跳过（展开时再补）
                if (_isCollapsed(d.Plan.Key))
                {
                    d.Cursor = d.Plan.Records.Count;
                    continue;
                }

                int n = Math.Min(Chunk, d.Plan.Records.Count - d.Cursor);
                for (int i = 0; i < n; i++)
                {
                    Materialize(d, d.Cursor, true);
                    d.Cursor++;
                }
                return true;
            }
            return false;
        }

        private void CreateGroup(DayState d)
        {
            d.Group = new DayGroup(_host.Context, d.Plan.Label, d.Plan.Extra,
                d.Plan.Records.Count, !_isCollapsed(d.Plan.Key));
            d.Group.Toggled += (g, expanded) =>
            {
                _onToggle(d.Plan.Key, expanded);
                if (expanded) MaterializeDay(d, false);
                Pump();
            };
            _host.AddView(d.Group, Math.Max(0, _host.ChildCount - 1));
        }

        private void Materialize(DayState d, int index, bool fade)
        {
            if (index < 0 || index >= d.Slots.Count) return;
            var slot = d.Slots[index];
            if (slot.Live) return;

            var row = _makeRow(slot.Rec);
            slot.View = row;
            slot.Live = true;

            var body = d.Group.Body;
            if (index < body.ChildCount)
            {
                body.RemoveViewAt(index);
                body.AddView(row, index);
            }
            else
            {
                body.AddView(row);
            }

            if (fade)
            {
                row.Alpha = 0f;
                row.Animate().Alpha(1f).SetDuration(230)
                    .SetStartDelay(Math.Min(index, 6) * 18).Start();
            }
            else
            {
                row.Alpha = 1f;
            }
        }

        /// <summary>把已加载但被回收的行重建回来（不淡入，避免闪烁）。</summary>
        private void MaterializeDay(DayState d, bool fade)
        {
            if (d.Group == null) return;
            for (int i = 0; i < d.Cursor && i < d.Slots.Count; i++)
                Materialize(d, i, fade);
        }

        // ---------- 回收 ----------

        private void ReleasePass()
        {
            int y = _scrollY;
            var rect = new Rect();
            foreach (var d in _days)
            {
                if (d.Group == null) continue;
                _host.OffsetDescendantRectToMyCoords(d.Group, rect);

                // 整组滑到上方 4 屏以外 → 释放；回到上方 1 屏以内 → 重建
                if (rect.Bottom < y - 4 * _viewport) ReleaseDay(d);
                else if (rect.Top > y - _viewport) MaterializeDay(d, false);
            }
        }

        private void ReleaseDay(DayState d)
        {
            var body = d.Group.Body;
            bool any = false;
            for (int i = 0; i < d.Slots.Count && i < body.ChildCount; i++)
            {
                var slot = d.Slots[i];
                if (!slot.Live) continue;

                int h = slot.View.Height;
                if (h <= 0) continue;      // 没量过就不动它
                var lp = slot.View.LayoutParameters as LinearLayout.LayoutParams;
                slot.Height = h;
                slot.Margin = lp != null ? lp.BottomMargin : 0;
                slot.Live = false;

                var ph = new View(_host.Context);
                slot.View = ph;
                body.RemoveViewAt(i);
                body.AddView(ph, i, new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent, slot.Height)
                { BottomMargin = slot.Margin });
                any = true;
            }
            if (any) d.Group.InvalidateBody();
        }
    }
}
