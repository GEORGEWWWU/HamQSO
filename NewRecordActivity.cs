using System;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Views.Animations;
using Android.Views.InputMethods;
using Android.Widget;
using Android.Animation;
using HamJi.Core;
using HamJi.Views;

namespace HamJi
{
    [Activity(
        Label = "新建记录",
        Theme = "@style/SheetTheme",
        Exported = false,
        WindowSoftInputMode = SoftInput.AdjustResize,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.Density
            | ConfigChanges.KeyboardHidden | ConfigChanges.Keyboard)]
    public class NewRecordActivity : Activity
    {
        private static readonly string[] ModeNames =
        {
            "SSB", "CW", "FM", "AM", "FT8", "FT4", "RTTY", "PSK31", "DMR", "C4FM", "D-STAR", "其他"
        };

        private FrameLayout _root;
        private View _scrim;
        private LinearLayout _sheet;
        private LinearLayout _form;
        private MaxHeightScrollView _scroll;
        private Segmented _seg;
        private View _dragArea;
        private LinearLayout _saveWrap;
        private View _fade;

        private EditText _freq, _notes, _call, _rstTx, _rstRx, _qth, _rig, _power, _ant;
        private TextView _dateBtn, _timeBtn;
        private ModeDropdown _mode;
        private TextView _titleView, _subView;
        private string[] _modes = ModeNames;
        private QsoRecord _editing;

        private RecKind _kind = RecKind.Frequency;
        private DateTime _when = DateTime.Now;

        private int _statusInset, _imeInset, _navInset;
        private int _fitH, _fullH;
        private bool _dragSheet;
        private float _downRawY;
        private int _startH;
        private ValueAnimator _heightAnim;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Dui.Init(this);
            Window.SetSoftInputMode(SoftInput.AdjustResize);
            Window.SetStatusBarColor(Color.Transparent);
            Window.SetNavigationBarColor(Color.Transparent);
            if ((int)Build.VERSION.SdkInt >= 30) Window.SetDecorFitsSystemWindows(false);

            var editId = Intent?.GetStringExtra("edit_id");
            if (!string.IsNullOrEmpty(editId))
            {
                try { _editing = Store.Find(editId); } catch { }
            }
            BuildUi();
        }

        private void BuildUi()
        {
            _root = new FrameLayout(this);

            _scrim = new View(this);
            _scrim.SetBackgroundColor(new Color(Dui.Scrim));
            _scrim.Alpha = 0f;
            _root.AddView(_scrim, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
            _scrim.Click += (s, e) => Close(false);

            _sheet = Dui.VBox();
            var bg = new GradientDrawable();
            bg.SetColor(new Color(Dui.SheetBg));
            bg.SetCornerRadii(new[] { Dui.Dpf(26), Dui.Dpf(26), Dui.Dpf(26), Dui.Dpf(26), 0f, 0f, 0f, 0f });
            _sheet.SetBackgroundDrawable(bg);
            _sheet.Elevation = Dui.Dpf(24);

            // ---- 可拖拽区：指示条 + 标题栏 ----
            var dragArea = Dui.VBox();
            dragArea.SetClipChildren(false);

            var handleBox = new FrameLayout(this);
            var handle = new View(this);
            handle.SetBackgroundDrawable(Dui.Round(Dui.WithAlpha(Dui.TextTertiary, 110), 3));
            handleBox.AddView(handle, new FrameLayout.LayoutParams(Dui.Dp(40), Dui.Dp(5)) { Gravity = GravityFlags.Center });
            dragArea.AddView(handleBox, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dui.Dp(22)));

            var head = Dui.HBox();
            head.SetPadding(Dui.Dp(22), 0, Dui.Dp(14), Dui.Dp(6));

            var titleBox = Dui.VBox();
            var t = Dui.Label("新建记录", 19f, Dui.TextPrimary, true);
            var st = Dui.Label("记录一次守听或一次通联 · 可上拉铺满", 11.5f, Dui.TextTertiary, false);
            _titleView = t;
            _subView = st;
            var stlp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Dui.Dp(3) };
            titleBox.AddView(t);
            titleBox.AddView(st, stlp);
            head.AddView(titleBox, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f) { Gravity = GravityFlags.CenterVertical });

            var close = new FrameLayout(this);
            close.Clickable = true;
            var ci = Dui.Icon(Resource.Drawable.ic_close, 18, Dui.TextSecondary);
            close.AddView(ci, new FrameLayout.LayoutParams(Dui.Dp(18), Dui.Dp(18)) { Gravity = GravityFlags.Center });
            HamJi.Core.Touch.Bind(close, () => Close(false), 0.9f);
            head.AddView(close, new LinearLayout.LayoutParams(Dui.Dp(36), Dui.Dp(36)) { Gravity = GravityFlags.CenterVertical });

            dragArea.AddView(head, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
            _sheet.AddView(dragArea, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
            _dragArea = dragArea;
            dragArea.Clickable = true;
            dragArea.SetOnTouchListener(new DragHandler(this));

            // ---- 类型选择 ----
            _seg = new Segmented(this, new[] { "通联频率", "通联日志" });
            _seg.Selected += idx =>
            {
                _kind = idx == 0 ? RecKind.Frequency : RecKind.Log;
                BuildForm();
            };
            var segLp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            { TopMargin = Dui.Dp(4) };
            segLp.LeftMargin = Dui.Dp(22);
            segLp.RightMargin = Dui.Dp(22);
            _sheet.AddView(_seg, segLp);

            // ---- 表单 ----
            _scroll = new MaxHeightScrollView(this) { MaxRatio = 1f };
            _scroll.VerticalScrollBarEnabled = false;
            _scroll.OverScrollMode = OverScrollMode.Never;

            _form = Dui.VBox();
            _form.SetPadding(Dui.Dp(22), Dui.Dp(16), Dui.Dp(22), Dui.Dp(10));
            _scroll.AddView(_form, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            // 滚动区外面套一层，用于放置顶部渐隐遮罩（避免内容被硬切）
            var scrollWrap = new FrameLayout(this);
            scrollWrap.AddView(_scroll, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
            _fade = new View(this);
            _fade.SetBackgroundDrawable(new GradientDrawable(GradientDrawable.Orientation.TopBottom,
                new[] { new Color(Dui.SheetBg), Dui.WithAlpha(Dui.SheetBg, 0) }));
            _fade.Clickable = false;
            _fade.Focusable = false;
            scrollWrap.AddView(_fade, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dui.Dp(18))
            { Gravity = GravityFlags.Top });
            _sheet.AddView(scrollWrap, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

            CreateInputs();

            // ---- 保存 ----
            var saveWrap = new LinearLayout(this);
            _saveWrap = saveWrap;
            saveWrap.SetPadding(Dui.Dp(22), Dui.Dp(10), Dui.Dp(22), Dui.Dp(16));
            var save = new TextView(this);
            save.Text = "保存";
            save.Gravity = GravityFlags.Center;
            save.SetTextSize(Android.Util.ComplexUnitType.Sp, 15f);
            save.SetTextColor(Color.White);
            save.SetTypeface(Dui.Medium, TypefaceStyle.Normal);
            var sbg = new GradientDrawable(GradientDrawable.Orientation.TopBottom, new[] { Dui.AccentFill, Dui.AccentFill2 });
            sbg.SetCornerRadius(Dui.Dpf(16));
            save.SetBackgroundDrawable(sbg);
            save.Clickable = true;
            HamJi.Core.Touch.Bind(save, Save, 0.97f);
            saveWrap.AddView(save, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dui.Dp(52)));
            _sheet.AddView(saveWrap, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            var sheetLp = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            { Gravity = GravityFlags.Bottom };
            _root.AddView(_sheet, sheetLp);

            SetContentView(_root);
            _root.SetOnApplyWindowInsetsListener(new SheetInsets(this));

            BuildForm();
            if (_editing != null) Prefill();

            _sheet.Post(() =>
            {
                int natural = NaturalHeight();
                if (natural > 0) _fitH = natural;
                _sheet.TranslationY = _sheet.Height;
                _sheet.Animate().TranslationY(0f).SetDuration(300).SetInterpolator(new DecelerateInterpolator(1.5f)).Start();
                _scrim.Animate().Alpha(1f).SetDuration(220).Start();
            });
        }

        private void Prefill()
        {
            var r = _editing;
            if (r == null) return;

            _titleView.Text = "编辑记录";
            _subView.Text = "修改后点击保存 · 不改动直接退出则放弃";

            _when = r.LocalTime;
            // 用列表里一致的格式化结果回填，避免出现 "1" 这种截断感
            _freq.Text = r.Freq > 0 ? r.FreqText : "";
            _notes.Text = r.Notes ?? "";
            _call.Text = r.Call ?? "";
            _rstTx.Text = r.RstTx ?? "";
            _rstRx.Text = r.RstRx ?? "";
            _qth.Text = r.Qth ?? "";
            _rig.Text = r.Rig ?? "";
            _power.Text = r.Power ?? "";
            _ant.Text = r.Ant ?? "";

            int mi = Array.IndexOf(_modes, (r.Mode ?? "").Trim());
            _mode.Select(mi < 0 ? 0 : mi, false);

            _kind = (RecKind)r.Kind;
            if (r.IsLog) _seg.Select(1); else BuildForm();
            RefreshTimeText();
        }

        private void CreateInputs()
        {
            _freq = Input("145.500", InputTypes.ClassNumber | InputTypes.NumberFlagDecimal, false, 48);
            _notes = Input("通联内容、天气、天线方向……", InputTypes.ClassText | InputTypes.TextFlagMultiLine, true);
            _call = Input("例如 BG7XXX", InputTypes.ClassText | InputTypes.TextFlagCapCharacters);
            _rstTx = Input("发 59", InputTypes.ClassText);
            _rstRx = Input("收 59", InputTypes.ClassText);
            _qth = Input("对方位置", InputTypes.ClassText);
            _rig = Input("设备型号", InputTypes.ClassText);
            _power = Input("功率 W", InputTypes.ClassNumber | InputTypes.NumberFlagDecimal);
            _ant = Input("天线 / 频段", InputTypes.ClassText);

            if (_editing != null && !string.IsNullOrWhiteSpace(_editing.Mode)
                && Array.IndexOf(ModeNames, _editing.Mode) < 0)
            {
                var list = new System.Collections.Generic.List<string>(ModeNames) { _editing.Mode };
                _modes = list.ToArray();
            }
            _mode = new ModeDropdown(this, _modes);
            _mode.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dui.Dp(48));
            _mode.Select(0, false);
        }

        private void BuildForm()
        {
            Detach(_freq); Detach(_notes); Detach(_call); Detach(_rstTx); Detach(_rstRx);
            Detach(_qth); Detach(_rig); Detach(_power); Detach(_ant); Detach(_mode);
            _form.RemoveAllViews();

            // 频率 + 模式 合并成一行
            var row = Dui.HBox();
            row.SetClipChildren(false);
            row.AddView(Field("频率 (MHz)", _freq, 48), new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f) { RightMargin = Dui.Dp(10) });
            row.AddView(Field("模式", _mode, 48), new LinearLayout.LayoutParams(Dui.Dp(114), ViewGroup.LayoutParams.WrapContent));
            _form.AddView(row);

            if (_kind == RecKind.Frequency)
            {
                AddGap();
                _form.AddView(Field("接收时间", TimeRow()));
                AddGap();
                _form.AddView(Field("备注", _notes));
            }
            else
            {
                AddGap();
                _form.AddView(Field("呼号", _call));
                AddGap();
                _form.AddView(Field("通联时间", TimeRow()));
                AddGap();

                var rstRow = Dui.HBox();
                rstRow.SetClipChildren(false);
                rstRow.AddView(Field("信号报告 (发)", _rstTx), new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f) { RightMargin = Dui.Dp(10) });
                rstRow.AddView(Field("信号报告 (收)", _rstRx), new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
                _form.AddView(rstRow);

                AddGap();
                _form.AddView(Field("QTH / 对方位置", _qth));

                AddGap();
                var rigRow = Dui.HBox();
                rigRow.SetClipChildren(false);
                rigRow.AddView(Field("设备", _rig), new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1.6f) { RightMargin = Dui.Dp(10) });
                rigRow.AddView(Field("功率 (W)", _power), new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
                _form.AddView(rigRow);

                AddGap();
                _form.AddView(Field("天线", _ant));

                AddGap();
                _form.AddView(Field("备注", _notes));
            }

            _form.Post(() =>
            {
                if (_dragSheet || _imeInset > 0) return;
                int natural = NaturalHeight();
                if (natural > 0 && Math.Abs(natural - _fitH) > Dui.Dp(4))
                {
                    _fitH = natural;
                    ApplyInsets(_statusInset, _imeInset, _navInset);
                }
            });
        }

        private View TimeRow()
        {
            var row = Dui.HBox();
            row.SetClipChildren(false);
            _dateBtn = PillText();
            _timeBtn = PillText();

            row.AddView(_dateBtn, new LinearLayout.LayoutParams(0, Dui.Dp(48), 1f) { RightMargin = Dui.Dp(10) });
            row.AddView(_timeBtn, new LinearLayout.LayoutParams(0, Dui.Dp(48), 1f));

            _dateBtn.Click += (s, e) =>
            {
                var dlg = new DatePickerDialog(this, (o, ev) =>
                {
                    _when = new DateTime(ev.Year, ev.Month + 1, ev.DayOfMonth, _when.Hour, _when.Minute, 0);
                    RefreshTimeText();
                }, _when.Year, _when.Month - 1, _when.Day);
                dlg.Show();
            };

            _timeBtn.Click += (s, e) =>
            {
                var dlg = new TimePickerDialog(this, (o, ev) =>
                {
                    _when = new DateTime(_when.Year, _when.Month, _when.Day, ev.HourOfDay, ev.Minute, 0);
                    RefreshTimeText();
                }, _when.Hour, _when.Minute, true);
                dlg.Show();
            };

            RefreshTimeText();
            return row;
        }

        private TextView PillText()
        {
            var tv = new TextView(this);
            tv.SetTextSize(Android.Util.ComplexUnitType.Sp, 14f);
            tv.SetTextColor(new Color(Dui.TextPrimary));
            tv.SetTypeface(Dui.Regular, TypefaceStyle.Normal);
            tv.Gravity = GravityFlags.Center;
            tv.SetBackgroundDrawable(Dui.InputFieldBg());
            tv.Clickable = true;
            tv.SetPadding(Dui.Dp(12), 0, Dui.Dp(12), 0);
            HamJi.Core.Touch.Press(tv, 0.97f);
            return tv;
        }

        private void RefreshTimeText()
        {
            if (_dateBtn == null) return;
            var today = DateTime.Today;
            string day = _when.Date == today ? "今天" : (_when.Date == today.AddDays(-1) ? "昨天" : _when.ToString("yyyy-MM-dd"));
            _dateBtn.Text = day;
            _timeBtn.Text = _when.ToString("HH:mm");
        }

        private void AddGap() => _form.AddView(new View(this), new LinearLayout.LayoutParams(1, Dui.Dp(14)));

        private static void Detach(View v)
        {
            if (v == null) return;
            if (v.Parent is ViewGroup vg) vg.RemoveView(v);
        }

        private View Field(string label, View input, int inputHeightDp = 0)
        {
            var box = Dui.VBox();
            box.SetClipChildren(false);
            box.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            var tv = Dui.Label(label, 11.5f, Dui.TextSecondary, true);
            tv.LetterSpacing = 0.03f;
            box.AddView(tv);
            var lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent,
                inputHeightDp > 0 ? Dui.Dp(inputHeightDp) : ViewGroup.LayoutParams.WrapContent)
            { TopMargin = Dui.Dp(8) };
            box.AddView(input, lp);
            return box;
        }

        private EditText Input(string hint, InputTypes type, bool multiline = false, int heightDp = 0)
        {
            var e = new EditText(this);
            e.Hint = hint;
            e.InputType = type;
            e.SetTextSize(Android.Util.ComplexUnitType.Sp, 15f);
            e.SetTextColor(new Color(Dui.TextPrimary));
            e.SetHintTextColor(new Color(Dui.WithAlpha(Dui.TextTertiary, 190)));
            e.SetTypeface(Dui.Regular, TypefaceStyle.Normal);
            e.SetBackgroundDrawable(Dui.InputFieldBg());
            e.SetPadding(Dui.Dp(14), multiline ? Dui.Dp(12) : Dui.Dp(10), Dui.Dp(14), Dui.Dp(10));
            e.SetIncludeFontPadding(false);
            if (multiline)
            {
                e.Gravity = GravityFlags.Top | GravityFlags.Start;
                e.SetMinLines(3);
                e.SetMaxLines(6);
                e.SetSingleLine(false);
            }
            else
            {
                e.SetSingleLine(true);
                e.Gravity = GravityFlags.CenterVertical;
            }
            if (heightDp > 0)
            {
                e.SetMinHeight(Dui.Dp(heightDp));
                e.SetMaxHeight(Dui.Dp(heightDp));
                e.Gravity = GravityFlags.CenterVertical;
                e.SetPadding(Dui.Dp(14), 0, Dui.Dp(14), 0);
            }
            e.FocusChange += (s, ev) => { if (ev.HasFocus) EnsureVisible(e); };
            return e;
        }

        /// <summary>把焦点输入框滚进可视区（键盘弹起后也能看到）。</summary>
        private void EnsureVisible(View v)
        {
            _scroll.PostDelayed(() =>
            {
                try
                {
                    var lv = new int[2];
                    var ls = new int[2];
                    v.GetLocationInWindow(lv);
                    _scroll.GetLocationInWindow(ls);
                    int topInScroll = lv[1] - ls[1] + _scroll.ScrollY;
                    int target = Math.Max(0, topInScroll - Dui.Dp(24));
                    _scroll.SmoothScrollTo(0, target);
                }
                catch { }
            }, 300);
        }

        // ---------------- 高度 / 拖拽 ----------------

        internal void ApplyInsets(int status, int ime, int nav)
        {
            _statusInset = status;
            _imeInset = ime;
            _navInset = nav;

            int screenH = Resources.DisplayMetrics.HeightPixels;

            // 弹层底边：有键盘时压在键盘上沿，无键盘时贴屏幕底（盖住手势条）
            int bottomGap = ime;
            var lp = (FrameLayout.LayoutParams)_sheet.LayoutParameters;
            lp.BottomMargin = bottomGap;

            // 顶部上限：始终停在状态栏下方（留 12dp，避免下拉手势被系统通知栏抢走）
            int topSafe = status + Dui.Dp(12);
            _fullH = Math.Max(Dui.Dp(280), screenH - bottomGap - topSafe);

            // 保存按钮下方让出系统手势条（键盘显示时不需要）
            if (_saveWrap != null)
            {
                int padBottom = ime > 0 ? Dui.Dp(16) : nav + Dui.Dp(16);
                _saveWrap.SetPadding(Dui.Dp(22), Dui.Dp(10), Dui.Dp(22), padBottom);
            }

            if (!_dragSheet)
            {
                if (ime > 0) lp.Height = _fullH;
                else lp.Height = _fitH > 0 ? Math.Min(_fitH, _fullH) : Math.Min(Dui.Dp(520), _fullH);
            }
            _sheet.LayoutParameters = lp;

            // 表单区是唯一可伸缩部分：限高 = 可用高度 - 固定部分
            int others = FixedChromeHeight();
            _scroll.MaxHeightPx = Math.Max(Dui.Dp(120), _fullH - others);
            _scroll.RequestLayout();

            if (ime > 0 && CurrentFocus is EditText) EnsureVisible(CurrentFocus);
        }

        /// <summary>除表单区以外的固定高度（标题区 + 分段 + 保存区 + 内边距）。</summary>
        private int FixedChromeHeight()
        {
            int h = 0;
            try
            {
                if (_dragArea != null) h += _dragArea.Height;
                if (_seg != null)
                {
                    h += _seg.Height;
                    if (_seg.LayoutParameters is LinearLayout.LayoutParams slp) h += slp.TopMargin + slp.BottomMargin;
                }
                if (_saveWrap != null) h += _saveWrap.Height;
                if (_form != null) h += _form.PaddingTop + _form.PaddingBottom;
            }
            catch { }
            if (h < Dui.Dp(180)) h = Dui.Dp(260);
            return h;
        }

        /// <summary>内容自然高度：标题区 + 分段 + 表单 + 保存区。</summary>
        private int NaturalHeight()
        {
            try
            {
                int h = 0;
                if (_dragArea != null) h += _dragArea.Height;
                if (_seg != null)
                {
                    h += _seg.Height;
                    if (_seg.LayoutParameters is LinearLayout.LayoutParams slp) h += slp.TopMargin + slp.BottomMargin;
                }
                if (_saveWrap != null) h += _saveWrap.Height;
                if (_form != null) h += _form.Height + _form.PaddingTop + _form.PaddingBottom;
                return h;
            }
            catch { return 0; }
        }

        private void SetSheetHeight(int h)
        {
            var lp = (FrameLayout.LayoutParams)_sheet.LayoutParameters;
            lp.Height = h;
            _sheet.LayoutParameters = lp;
        }

        private void AnimateHeight(int from, int to)
        {
            try { _heightAnim?.Cancel(); } catch { }
            _heightAnim = ValueAnimator.OfInt(from, to);
            _heightAnim.SetDuration(220);
            _heightAnim.SetInterpolator(new DecelerateInterpolator(1.4f));
            _heightAnim.Update += (s, e) =>
            {
                int v = from;
                var raw = e.Animation.AnimatedValue;
                if (raw is Java.Lang.Integer ji) v = ji.IntValue();
                else if (raw != null) int.TryParse(raw.ToString(), out v);
                SetSheetHeight(v);
            };
            _heightAnim.Start();
        }

        private class DragHandler : Java.Lang.Object, View.IOnTouchListener
        {
            private readonly NewRecordActivity _a;
            public DragHandler(NewRecordActivity a) { _a = a; }

            public bool OnTouch(View v, MotionEvent e)
            {
                try { return Handle(e); }
                catch { return false; }
            }

            private bool Handle(MotionEvent e)
            {
                switch (e.ActionMasked)
                {
                    case MotionEventActions.Down:
                        _a._dragSheet = true;
                        _a._startH = _a._sheet.Height > 0 ? _a._sheet.Height : _a._fitH;
                        _a._downRawY = e.RawY;
                        try { _a._heightAnim?.Cancel(); } catch { }
                        return true;

                    case MotionEventActions.Move:
                        if (!_a._dragSheet) return true;
                        int h = (int)(_a._startH - (e.RawY - _a._downRawY));
                        int min = Dui.Dp(240);
                        int max = _a._fullH > 0 ? _a._fullH : h;
                        h = Math.Max(min, Math.Min(max, h));
                        _a.SetSheetHeight(h);
                        return true;

                    case MotionEventActions.Up:
                    case MotionEventActions.Cancel:
                        if (!_a._dragSheet) return true;
                        _a._dragSheet = false;
                        int cur = _a._sheet.Height;
                        if (cur < _a._fullH * 0.45f) { _a.Close(false); return true; }
                        int target = cur > _a._fullH * 0.8f ? _a._fullH : (_a._fitH > 0 ? _a._fitH : cur);
                        _a.AnimateHeight(cur, target);
                        return true;
                }
                return false;
            }
        }

        private void Close(bool saved)
        {
            var imm = (InputMethodManager)GetSystemService(Context.InputMethodService);
            imm?.HideSoftInputFromWindow(_root.WindowToken, 0);
            if (saved) { Finish(); OverridePendingTransition(0, 0); return; }
            _sheet.Animate().TranslationY(_sheet.Height).SetDuration(200).SetInterpolator(new AccelerateInterpolator()).Start();
            _scrim.Animate().Alpha(0f).SetDuration(180).Start();
            new Handler(Looper.MainLooper).PostDelayed(() =>
            {
                Finish();
                OverridePendingTransition(0, 0);
            }, 190);
        }

        private void Save()
        {
            var freqText = (_freq.Text ?? "").Trim().Replace(",", ".");
            double freq = 0;
            if (freqText.Length == 0 || !double.TryParse(freqText, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out freq) || freq <= 0 || freq > 1000)
            {
                Shake(_freq, "请输入 0~1000 之间的频率，例如 145.500");
                return;
            }

            if (_kind == RecKind.Log && string.IsNullOrWhiteSpace(_call.Text))
            {
                Shake(_call, "请填写对方呼号");
                return;
            }

            var rec = _editing ?? new QsoRecord();
            rec.Kind = (int)_kind;
            rec.Freq = freq;
            rec.Mode = _mode.SelectedText;
            rec.Time = new DateTimeOffset(DateTime.SpecifyKind(_when, DateTimeKind.Local)).ToUnixTimeMilliseconds();
            rec.Notes = (_notes.Text ?? "").Trim();

            if (_kind == RecKind.Log)
            {
                rec.Call = (_call.Text ?? "").Trim().ToUpperInvariant();
                rec.RstTx = (_rstTx.Text ?? "").Trim();
                rec.RstRx = (_rstRx.Text ?? "").Trim();
                rec.Qth = (_qth.Text ?? "").Trim();
                rec.Rig = (_rig.Text ?? "").Trim();
                rec.Power = (_power.Text ?? "").Trim();
                rec.Ant = (_ant.Text ?? "").Trim();
            }

            if (_editing != null)
            {
                Store.Update(rec);
                Toast.MakeText(this, "已更新", ToastLength.Short).Show();
            }
            else
            {
                Store.Add(rec);
                Toast.MakeText(this, "已保存", ToastLength.Short).Show();
            }
            Close(true);
        }

        private void Shake(View v, string msg)
        {
            Toast.MakeText(this, msg, ToastLength.Short).Show();
            v.RequestFocus();
            var anim = ObjectAnimator.OfFloat(v, "translationX", 0f, Dui.Dpf(6), -Dui.Dpf(5), Dui.Dpf(4), -Dui.Dpf(2), 0f);
            anim.SetDuration(300);
            anim.Start();
        }

        public override void OnBackPressed()
        {
            if (_mode != null && _mode.IsOpen) { _mode.Dismiss(); return; }
            Close(false);
        }

        private class SheetInsets : Java.Lang.Object, View.IOnApplyWindowInsetsListener
        {
            private readonly NewRecordActivity _a;
            public SheetInsets(NewRecordActivity a) { _a = a; }

            public WindowInsets OnApplyWindowInsets(View v, WindowInsets insets)
            {
                int status = 0, ime = 0, nav = 0;
                try
                {
                    if ((int)Build.VERSION.SdkInt >= 30)
                    {
                        var sys = insets.GetInsets(7);
                        var cut = insets.GetInsets(128);
                        var im = insets.GetInsets(8);
                        status = Math.Max(sys.Top, cut.Top);
                        nav = sys.Bottom;
                        ime = im.Bottom;
                    }
                    else
                    {
#pragma warning disable CS0618
                        status = insets.SystemWindowInsetTop;
                        nav = insets.SystemWindowInsetBottom;
#pragma warning restore CS0618
                    }
                }
                catch { }
                _a.ApplyInsets(status, ime, nav);
                return insets;
            }
        }
    }

    /// <summary>限制最大高度的滚动容器。</summary>
    public class MaxHeightScrollView : ScrollView
    {
        public float MaxRatio { get; set; } = 0.62f;
        /// <summary>像素上限；>0 时优先于 MaxRatio。</summary>
        public int MaxHeightPx { get; set; }

        public MaxHeightScrollView(Context c) : base(c) { }

        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            // 父容器用 EXACTLY 分配空间时（weight 撑满）不能改，否则会被内容高度卡住
            if (MeasureSpec.GetMode(heightMeasureSpec) == MeasureSpecMode.Exactly)
            {
                base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
                return;
            }
            int max = MaxHeightPx > 0 ? MaxHeightPx : (int)(Resources.DisplayMetrics.HeightPixels * MaxRatio);
            base.OnMeasure(widthMeasureSpec, MeasureSpec.MakeMeasureSpec(max, MeasureSpecMode.AtMost));
        }
    }

    /// <summary>胶囊分段选择器。</summary>
    public class Segmented : FrameLayout
    {
        public event Action<int> Selected;
        private readonly TextView[] _items;
        private int _sel;

        public Segmented(Context c, string[] labels) : base(c)
        {
            SetBackgroundDrawable(Dui.Round(Dui.InputBg, 14));

            var row = Dui.HBox();
            row.SetPadding(Dui.Dp(4), Dui.Dp(4), Dui.Dp(4), Dui.Dp(4));
            _items = new TextView[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                var tv = new TextView(c);
                tv.Text = labels[i];
                tv.Gravity = GravityFlags.Center;
                tv.SetTextSize(Android.Util.ComplexUnitType.Sp, 13.5f);
                tv.Clickable = true;
                _items[i] = tv;
                int idx = i;
                tv.Click += (s, e) => Select(idx);
                HamJi.Core.Touch.Press(tv, 0.97f);
                row.AddView(tv, new LinearLayout.LayoutParams(0, Dui.Dp(42), 1f));
            }
            AddView(row, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
            Paint(0, false);
        }

        public void Select(int idx)
        {
            if (idx == _sel) return;
            int old = _sel;
            _sel = idx;
            Paint(old, true);
            Paint(idx, true);
            Selected?.Invoke(idx);
        }

        private void Paint(int idx, bool animate)
        {
            var tv = _items[idx];
            bool on = idx == _sel;
            if (on)
            {
                var g = new GradientDrawable(GradientDrawable.Orientation.TopBottom, new[] { Dui.AccentFill, Dui.AccentFill2 });
                g.SetCornerRadius(Dui.Dpf(11));
                tv.SetBackgroundDrawable(g);
                tv.SetTextColor(Color.White);
                tv.SetTypeface(Dui.Medium, TypefaceStyle.Normal);
                tv.Elevation = Dui.Dpf(3);
            }
            else
            {
                tv.SetBackgroundDrawable(null);
                tv.SetTextColor(new Color(Dui.TextSecondary));
                tv.SetTypeface(Dui.Regular, TypefaceStyle.Normal);
                tv.Elevation = 0;
            }
            if (animate)
            {
                tv.ScaleX = 0.94f;
                tv.ScaleY = 0.94f;
                tv.Animate().ScaleX(1f).ScaleY(1f).SetDuration(180).SetInterpolator(new OvershootInterpolator(1.2f)).Start();
            }
        }
    }

    /// <summary>模式下拉：弹窗限高，不会盖到底部保存按钮。</summary>
    public class ModeDropdown : FrameLayout
    {
        private readonly string[] _names;
        private readonly TextView _label;
        private PopupWindow _pop;
        private int _sel;

        public string SelectedText => _names.Length > 0 ? _names[_sel] : "";

        public bool IsOpen => _pop != null && _pop.IsShowing;

        public void Dismiss() => _pop?.Dismiss();

        public ModeDropdown(Context c, string[] names) : base(c)
        {
            _names = names;
            Clickable = true;
            SetBackgroundDrawable(Dui.InputFieldBg());
            SetPadding(Dui.Dp(12), 0, Dui.Dp(10), 0);

            _label = Dui.Label("", 15f, Dui.TextPrimary, false);
            _label.Gravity = GravityFlags.Center;
            AddView(_label, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent)
            { Gravity = GravityFlags.Center });

            var chev = Dui.Icon(Resource.Drawable.ic_chevron, 16, Dui.TextTertiary);
            chev.Rotation = 90f;
            AddView(chev, new FrameLayout.LayoutParams(Dui.Dp(16), Dui.Dp(16))
            { Gravity = GravityFlags.Right | GravityFlags.CenterVertical });

            HamJi.Core.Touch.Press(this, 0.97f);
            Click += (s, e) => Show();
        }

        public void Select(int idx, bool fire)
        {
            _sel = Math.Max(0, Math.Min(_names.Length - 1, idx));
            _label.Text = SelectedText;
        }

        private void Show()
        {
            var list = Dui.VBox();
            list.SetPadding(Dui.Dp(6), Dui.Dp(6), Dui.Dp(6), Dui.Dp(6));

            for (int i = 0; i < _names.Length; i++)
            {
                var tv = Dui.Label(_names[i], 14f, i == _sel ? Dui.Accent : Dui.TextPrimary, i == _sel);
                tv.SetPadding(Dui.Dp(14), 0, Dui.Dp(14), 0);
                tv.Gravity = GravityFlags.Center;
                tv.Clickable = true;
                HamJi.Core.Touch.Press(tv, 0.96f);
                int idx = i;
                tv.Click += (s, e) =>
                {
                    Select(idx, true);
                    _pop?.Dismiss();
                };
                list.AddView(tv, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dui.Dp(44)));
            }

            var sv = new MaxHeightScrollView(Context) { MaxRatio = 0.3f };
            sv.VerticalScrollBarEnabled = false;
            sv.AddView(list, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            _pop = new PopupWindow(sv, Width, ViewGroup.LayoutParams.WrapContent);
            _pop.SetBackgroundDrawable(Dui.Round(Dui.SheetBg, 16));
            _pop.OutsideTouchable = true;
            _pop.Focusable = true;
            _pop.Elevation = Dui.Dpf(12);
            _pop.ShowAsDropDown(this, 0, Dui.Dp(6));
        }
    }
}
