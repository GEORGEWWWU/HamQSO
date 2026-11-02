using System;
using System.Collections.Generic;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using HamJi.Core;
using HamJi.Views;

namespace HamJi.Pages
{
    /// <summary>
    /// 搜索页：顶部是搜索框 + 日期筛选按钮（没有大标题）。
    /// 输入即检索（频率 / 模式 / 备注，日志额外支持呼号与 QTH），结果按「通联频率 / 通联日志」分组。
    /// 日期筛选走屏幕居中的悬浮月历（与新建页共用同一份月历实现）。
    /// </summary>
    public class SearchPage : BasePage
    {
        private const int MaxResults = 200;

        /// <summary>点左上角返回箭头 / 系统返回键时触发（由宿主关闭二级页）。</summary>
        public event Action BackRequested;

        /// <summary>供宿主收起输入法用。</summary>
        public EditText InputView => _input;

        private EditText _input;
        private FrameLayout _clearBtn;
        private FrameLayout _dateBtn;
        private ImageView _dateIcon;
        private LinearLayout _filterWrap;
        private TextView _filterLabel;
        private LinearLayout _kindPill;
        private TextView _kindLabel;

        private DateTime? _date;
        private string _query = "";
        private int _kind = -1;   // -1 全部 / 0 通联频率 / 1 通联日志

        public SearchPage(Context c) : base(c)
        {
            Header.RemoveAllViews();
            Header.AddView(BuildBar(), new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
            UpdateDateButton();
        }

        // ---------- 顶部搜索栏 ----------

        private View BuildBar()
        {
            var box = Dui.VBox();

            var row = Dui.HBox();

            // 返回：二级页专用，复用 chevron 转 180°
            var back = new IconAction(Context, Resource.Drawable.ic_chevron, 19, 30);
            back.Rotate(180f);
            back.Tint(Dui.TextPrimary);
            back.Click += (s, e) => { HideKeyboard(); BackRequested?.Invoke(); };
            row.AddView(back, new LinearLayout.LayoutParams(Dui.Dp(30), Dui.Dp(30))
            { RightMargin = Dui.Dp(4) });

            // 搜索框
            var field = Dui.HBox();
            field.SetPadding(Dui.Dp(12), 0, Dui.Dp(6), 0);
            field.SetBackgroundDrawable(Dui.Round(Dui.InputBg, 14));
            field.AddView(Dui.Icon(Resource.Drawable.ic_search, 17, Dui.TextTertiary),
                new LinearLayout.LayoutParams(Dui.Dp(17), Dui.Dp(17)));

            _input = new EditText(Context);
            _input.SetBackgroundColor(Color.Transparent);
            _input.SetSingleLine(true);
            _input.SetIncludeFontPadding(false);
            _input.SetPadding(0, 0, 0, 0);
            _input.Hint = "搜索频率 / 模式 / 备注";
            _input.SetHintTextColor(new Color(Dui.TextTertiary));
            _input.SetTextColor(new Color(Dui.TextPrimary));
            _input.SetTextSize(Android.Util.ComplexUnitType.Sp, 14.5f);
            _input.SetTypeface(Dui.Regular, TypefaceStyle.Normal);
            _input.ImeOptions = ImeAction.Search;
            _input.InputType = InputTypes.ClassText | InputTypes.TextFlagNoSuggestions;
            var ilp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MatchParent, 1f)
            { LeftMargin = Dui.Dp(8), RightMargin = Dui.Dp(4) };
            field.AddView(_input, ilp);

            _clearBtn = new FrameLayout(Context);
            _clearBtn.Clickable = true;
            _clearBtn.Visibility = ViewStates.Gone;
            var ci = Dui.Icon(Resource.Drawable.ic_close, 12, Dui.TextTertiary);
            _clearBtn.AddView(ci, new FrameLayout.LayoutParams(Dui.Dp(12), Dui.Dp(12))
            { Gravity = GravityFlags.Center });
            HamJi.Core.Touch.Bind(_clearBtn, () =>
            {
                _input.Text = "";
                _input.RequestFocus();
            }, 0.9f);
            field.AddView(_clearBtn, new LinearLayout.LayoutParams(Dui.Dp(28), Dui.Dp(28)));

            var flp = new LinearLayout.LayoutParams(0, Dui.Dp(44), 1f);
            row.AddView(field, flp);

            // 日期筛选按钮
            _dateBtn = new FrameLayout(Context);
            _dateBtn.Clickable = true;
            _dateBtn.Focusable = true;
            _dateIcon = Dui.Icon(Resource.Drawable.ic_calendar, 20, Dui.Accent);
            _dateBtn.AddView(_dateIcon, new FrameLayout.LayoutParams(Dui.Dp(20), Dui.Dp(20))
            { Gravity = GravityFlags.Center });
            HamJi.Core.Touch.Bind(_dateBtn, OpenDatePicker, 0.94f);
            row.AddView(_dateBtn, new LinearLayout.LayoutParams(Dui.Dp(44), Dui.Dp(44))
            { LeftMargin = Dui.Dp(8) });

            box.AddView(row, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            // 分类下拉 + 已选日期标签（同一行，省空间；分类常驻，日期只在筛选中出现）
            var tools = Dui.HBox();

            var kindPill = Dui.HBox();
            kindPill.Clickable = true;
            kindPill.Focusable = true;
            kindPill.SetPadding(Dui.Dp(10), 0, Dui.Dp(7), 0);
            kindPill.SetMinimumHeight(Dui.Dp(28));
            _kindLabel = Dui.Label("全部", 11.5f, Dui.TextSecondary, true);
            kindPill.AddView(_kindLabel);
            var kch = Dui.Icon(Resource.Drawable.ic_chevron, 10, Dui.TextTertiary);
            kch.Rotation = 90f;
            kindPill.AddView(kch, new LinearLayout.LayoutParams(Dui.Dp(10), Dui.Dp(10))
            { LeftMargin = Dui.Dp(3) });
            HamJi.Core.Touch.Bind(kindPill, () => ShowKindMenu(kindPill), 0.95f);
            _kindPill = kindPill;
            tools.AddView(kindPill, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent, Dui.Dp(28)));

            tools.AddView(new View(Context), new LinearLayout.LayoutParams(0, 1, 1f));

            _filterWrap = Dui.HBox();
            _filterWrap.Visibility = ViewStates.Gone;

            var pill = Dui.HBox();
            pill.Clickable = true;
            pill.SetPadding(Dui.Dp(10), Dui.Dp(4), Dui.Dp(6), Dui.Dp(4));
            pill.SetBackgroundDrawable(Dui.Round(Dui.ChipBg, 8));
            _filterLabel = Dui.Label("", 11.5f, Dui.Accent, true);
            pill.AddView(_filterLabel);
            var x = new FrameLayout(Context);
            x.Clickable = true;
            var xi = Dui.Icon(Resource.Drawable.ic_close, 10, Dui.Accent);
            x.AddView(xi, new FrameLayout.LayoutParams(Dui.Dp(10), Dui.Dp(10)) { Gravity = GravityFlags.Center });
            HamJi.Core.Touch.Bind(x, ClearDate, 0.9f);
            pill.AddView(x, new LinearLayout.LayoutParams(Dui.Dp(20), Dui.Dp(20)) { LeftMargin = Dui.Dp(4) });
            HamJi.Core.Touch.Bind(pill, OpenDatePicker, 0.96f);
            _filterWrap.AddView(pill);

            tools.AddView(_filterWrap, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));

            box.AddView(tools, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            { TopMargin = Dui.Dp(10) });

            UpdateKindPill();

            _input.TextChanged += OnQueryChanged;
            return box;
        }

        private void OnQueryChanged(object sender, TextChangedEventArgs e)
        {
            var t = _input?.Text ?? "";
            if (_clearBtn != null)
                // 没有内容时整块让位（Gone 而不是 Invisible），否则会挤掉 hint 末尾的字
                _clearBtn.Visibility = t.Length > 0 ? ViewStates.Visible : ViewStates.Gone;

            if (t == _query) return;
            _query = t;
            Reload();
        }

        private void ClearDate()
        {
            if (!_date.HasValue) return;
            _date = null;
            UpdateDateButton();
            Reload();
        }

        private void OpenDatePicker()
        {
            HideKeyboard();
            var dlg = new CenterDayDialog(Context, _date ?? DateTime.Today, _date.HasValue);
            dlg.Picked += d =>
            {
                _date = d.Date;
                UpdateDateButton();
                Reload();
            };
            dlg.Cleared += () =>
            {
                _date = null;
                UpdateDateButton();
                Reload();
            };
            dlg.Show();
        }

        private void UpdateDateButton()
        {
            if (_dateBtn == null) return;
            bool on = _date.HasValue;
            _dateBtn.SetBackgroundDrawable(Dui.Round(
                on ? Dui.WithAlpha(Dui.Accent, Dui.Dark ? 58 : 30) : Dui.InputBg, 14));
            _dateIcon?.SetColorFilter(new Color(on ? Dui.Accent : Dui.TextSecondary));

            if (_filterWrap != null)
                _filterWrap.Visibility = on ? ViewStates.Visible : ViewStates.Gone;
            if (on && _filterLabel != null)
                _filterLabel.Text = DayLabel(_date.Value);
        }

        /// <summary>分类下拉：全部 / 通联频率 / 通联日志。用小尺寸自绘弹层，和 App 风格一致。</summary>
        private void ShowKindMenu(View anchor)
        {
            HideKeyboard();

            var items = new[] { "全部", "通联频率", "通联日志" };
            var values = new[] { -1, 0, 1 };

            var box = Dui.VBox();
            box.SetPadding(Dui.Dp(6), Dui.Dp(6), Dui.Dp(6), Dui.Dp(6));
            box.SetBackgroundDrawable(Dui.Round(Dui.SheetBg, 12));
            box.Elevation = Dui.Dpf(12);
            box.ClipToOutline = true;
            box.OutlineProvider = ViewOutlineProvider.Background;

            PopupWindow pop = null;
            for (int i = 0; i < items.Length; i++)
            {
                int value = values[i];
                bool on = value == _kind;

                var row = Dui.HBox();
                row.SetPadding(Dui.Dp(8), 0, Dui.Dp(8), 0);
                row.Clickable = true;
                row.Focusable = true;
                row.SetBackgroundDrawable(Dui.Ripple(
                    Dui.WithAlpha(Dui.Accent, 40), Dui.Round(Color.Transparent, 8)));

                var tv = Dui.Label(items[i], 12.5f, on ? Dui.Accent : Dui.TextPrimary, on);
                row.AddView(tv, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

                if (on)
                {
                    var ck = Dui.Icon(Resource.Drawable.ic_check, 12, Dui.Accent);
                    row.AddView(ck, new LinearLayout.LayoutParams(Dui.Dp(12), Dui.Dp(12))
                    { LeftMargin = Dui.Dp(6) });
                }

                row.Click += (s, e) =>
                {
                    pop?.Dismiss();
                    if (_kind == value) return;
                    _kind = value;
                    UpdateKindPill();
                    Reload();
                };

                box.AddView(row, new LinearLayout.LayoutParams(Dui.Dp(124), Dui.Dp(34)));
            }

            pop = new PopupWindow(box, Dui.Dp(136), ViewGroup.LayoutParams.WrapContent);
            pop.SetBackgroundDrawable(new ColorDrawable(Color.Transparent));
            pop.OutsideTouchable = true;
            pop.Focusable = true;
            pop.Elevation = Dui.Dpf(10);
            pop.ShowAsDropDown(anchor, 0, Dui.Dp(6));
        }

        private void UpdateKindPill()
        {
            if (_kindPill == null) return;
            bool on = _kind != -1;
            if (_kindLabel != null)
            {
                _kindLabel.Text = _kind == 0 ? "通联频率" : _kind == 1 ? "通联日志" : "全部";
                _kindLabel.SetTextColor(new Color(on ? Dui.Accent : Dui.TextSecondary));
            }
            _kindPill.SetBackgroundDrawable(Dui.Round(
                on ? Dui.WithAlpha(Dui.Accent, Dui.Dark ? 58 : 30) : Dui.InputBg, 14));
        }

        private void HideKeyboard()
        {
            try
            {
                var imm = Context.GetSystemService(Context.InputMethodService) as InputMethodManager;
                if (imm != null && _input != null)
                    imm.HideSoftInputFromWindow(_input.WindowToken, 0);
            }
            catch { }
        }

        private void Reload()
        {
            ListHost.RemoveAllViews();
            OnBuild();
        }

        // ---------- 检索 ----------

        protected override void OnBuild()
        {
            var q = (_query ?? "").Trim();
            bool hasQuery = q.Length > 0;
            bool hasDate = _date.HasValue;

            if (!hasQuery && !hasDate && _kind == -1)
            {
                ListHost.AddView(Widgets.EmptyState(Resource.Drawable.ic_search,
                    "搜索通联记录",
                    "输入频率、模式或备注即可实时检索\n也可以点右上角按日期筛选"));
                return;
            }

            var freq = new List<QsoRecord>();
            var log = new List<QsoRecord>();
            foreach (var r in Store.All)
            {
                if (hasDate && r.LocalTime.Date != _date.Value.Date) continue;
                if (hasQuery && !Matches(r, q)) continue;
                if (r.IsLog) log.Add(r); else freq.Add(r);
            }

            if (freq.Count == 0 && log.Count == 0)
            {
                ListHost.AddView(Widgets.EmptyState(Resource.Drawable.ic_search,
                    "没有匹配的记录",
                    hasQuery ? "换个关键词，或取消日期筛选再试" : "这一天还没有记录"));
                return;
            }

            // 分类筛选：只留下选中的那一段
            int wantFreq = _kind == 1 ? 0 : freq.Count;
            int wantLog = _kind == 0 ? 0 : log.Count;
            if (wantFreq + wantLog == 0)
            {
                ListHost.AddView(Widgets.EmptyState(Resource.Drawable.ic_search,
                    "这个分类里没有记录",
                    "切到「全部」看看另一类记录"));
                return;
            }

            SortByTime(freq);
            SortByTime(log);

            int shown = 0;
            if (_kind != 1)
                shown += RenderSection("通联频率", Resource.Drawable.ic_freq, freq, hasDate, shown);
            if (_kind != 0)
                shown += RenderSection("通联日志", Resource.Drawable.ic_log, log, hasDate, shown);

            if (wantFreq + wantLog > shown)
            {
                var more = Dui.Label("结果较多，仅显示最新的 " + shown + " 条，输入更精确的关键词可缩小范围",
                    11.5f, Dui.TextTertiary, false);
                more.Gravity = GravityFlags.Center;
                more.SetLineSpacing(Dui.Dpf(3), 1f);
                var mlp = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
                { TopMargin = Dui.Dp(6), BottomMargin = Dui.Dp(10) };
                ListHost.AddView(more, mlp);
            }
        }

        private static void SortByTime(List<QsoRecord> list)
        {
            list.Sort((a, b) =>
            {
                int c = b.Time.CompareTo(a.Time);
                return c != 0 ? c : b.Created.CompareTo(a.Created);
            });
        }

        /// <summary>频率 / 模式 / 备注（日志再加呼号、QTH）的包含匹配，忽略大小写与小数点。</summary>
        private static bool Matches(QsoRecord r, string q)
        {
            string needle = q.ToLowerInvariant();
            string needleNoDot = needle.Replace(".", "").Replace(" ", "");

            var hay = new System.Text.StringBuilder(96);
            hay.Append(r.FreqText).Append(' ').Append(r.UnitText).Append(' ');
            hay.Append(r.Mode).Append(' ').Append(r.Notes).Append(' ');
            if (r.IsLog) hay.Append(r.Call).Append(' ').Append(r.Qth).Append(' ').Append(r.RstText);

            string text = hay.ToString();
            if (text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return true;

            // 「14270」也要能命中 14.270
            if (needleNoDot.Length > 0 &&
                text.Replace(".", "").Replace(" ", "").IndexOf(needleNoDot, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return false;
        }

        private int RenderSection(string title, int iconRes, List<QsoRecord> items, bool dateFiltered, int already)
        {
            if (items.Count == 0) return 0;

            int budget = Math.Max(0, MaxResults - already);
            int show = Math.Min(items.Count, budget);
            if (show == 0) return 0;

            var head = Dui.HBox();
            head.SetPadding(Dui.Dp(8), Dui.Dp(16), Dui.Dp(8), Dui.Dp(8));
            head.AddView(Dui.Icon(iconRes, 15, Dui.TextTertiary),
                new LinearLayout.LayoutParams(Dui.Dp(15), Dui.Dp(15)));
            var t = Dui.Label(title, 12.5f, Dui.TextSecondary, true);
            t.LetterSpacing = 0.06f;
            var tlp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
            { LeftMargin = Dui.Dp(7) };
            head.AddView(t, tlp);

            var tag = Dui.Label(items.Count + " 条", 10.5f, Dui.TextTertiary, true);
            tag.LetterSpacing = 0.02f;
            tag.SetPadding(Dui.Dp(6), Dui.Dp(2), Dui.Dp(6), Dui.Dp(2));
            tag.SetBackgroundDrawable(Dui.Round(Dui.WithAlpha(Dui.TextPrimary, Dui.Dark ? 26 : 13), 6));
            head.AddView(tag, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
            { LeftMargin = Dui.Dp(8) });

            ListHost.AddView(head, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            for (int i = 0; i < show; i++)
                ListHost.AddView(ResultRow(items[i], !dateFiltered));

            return show;
        }

        private View ResultRow(QsoRecord rec, bool showDate)
        {
            var card = Dui.VBox();
            card.SetBackgroundDrawable(Dui.GlassCard());
            card.ClipToOutline = true;
            card.OutlineProvider = ViewOutlineProvider.Background;
            card.Elevation = Dui.Dpf(2);
            card.SetPadding(Dui.Dp(14), Dui.Dp(10), Dui.Dp(14), Dui.Dp(10));
            card.Clickable = true;
            card.Focusable = true;
            card.LongClickable = true;
            card.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            { BottomMargin = Dui.Dp(8) };

            var l1 = Dui.HBox();
            if (rec.IsLog)
            {
                var call = Dui.Label(string.IsNullOrWhiteSpace(rec.Call) ? "未填写呼号" : rec.Call.ToUpperInvariant(),
                    16.5f, string.IsNullOrWhiteSpace(rec.Call) ? Dui.TextTertiary : Dui.TextPrimary, true);
                call.LetterSpacing = 0.01f;
                l1.AddView(call);
            }
            else
            {
                var freq = Dui.Label(string.IsNullOrEmpty(rec.FreqText) ? "未填写频率" : rec.FreqText,
                    19f, string.IsNullOrEmpty(rec.FreqText) ? Dui.TextTertiary : Dui.TextPrimary, true);
                freq.SetTypeface(Dui.Mono, TypefaceStyle.Normal);
                freq.LetterSpacing = -0.02f;
                l1.AddView(freq);
                if (!string.IsNullOrEmpty(rec.FreqText))
                {
                    var unit = Dui.Label(rec.UnitText, 10.5f, Dui.TextTertiary, true);
                    l1.AddView(unit, new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
                    { LeftMargin = Dui.Dp(4), Gravity = GravityFlags.Bottom, BottomMargin = Dui.Dp(3) });
                }
            }

            l1.AddView(new View(Dui.Ctx), new LinearLayout.LayoutParams(0, 1, 1f));
            l1.AddView(Widgets.DualTime(rec.Time, 11.5f,
                showDate ? rec.LocalTime.ToString("MM-dd") : null));
            card.AddView(l1);

            string meta = rec.IsLog ? Dui.FreqLine(rec) : null;
            if (rec.IsLog)
            {
                if (!string.IsNullOrWhiteSpace(rec.RstText)) meta += "   " + rec.RstText;
                if (!string.IsNullOrWhiteSpace(rec.Qth)) meta += "   " + rec.Qth;
            }
            else
            {
                meta = string.IsNullOrWhiteSpace(rec.Notes) ? "无备注" : rec.Notes;
            }

            // 第二行：模式标签打头，后面接频率 / RST / QTH 或备注
            var l2 = Dui.HBox();
            l2.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            { TopMargin = Dui.Dp(5) };

            if (!string.IsNullOrWhiteSpace(rec.Mode))
            {
                var chip = Widgets.Chip(rec.Mode);
                l2.AddView(chip, new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
                { RightMargin = Dui.Dp(8) });
            }

            var ml = Dui.Label(meta, 12.5f,
                (!rec.IsLog && string.IsNullOrWhiteSpace(rec.Notes)) ? Dui.TextTertiary : Dui.TextSecondary, false);
            ml.SetSingleLine(true);
            ml.Ellipsize = TextUtils.TruncateAt.End;
            l2.AddView(ml, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
            card.AddView(l2);

            if (rec.IsLog && !string.IsNullOrWhiteSpace(rec.Notes))
            {
                var nl = Dui.Label(rec.Notes, 12f, Dui.TextTertiary, false);
                nl.SetSingleLine(true);
                nl.Ellipsize = TextUtils.TruncateAt.End;
                card.AddView(nl, new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
                { TopMargin = Dui.Dp(3) });
            }

            card.Click += (s, e) => ShowDetail(rec);
            card.LongClick += (s, e) => { e.Handled = true; OpenEditor(rec); };
            HamJi.Core.Touch.Press(card, 0.985f);
            return card;
        }

        private string DayLabel(DateTime day)
        {
            var today = DateTime.Today;
            if (day.Date == today) return "今天 · " + day.ToString("M月d日");
            if (day.Date == today.AddDays(-1)) return "昨天 · " + day.ToString("M月d日");
            return day.Year == today.Year ? day.ToString("M月d日") : day.ToString("yyyy年M月d日");
        }
    }
}
