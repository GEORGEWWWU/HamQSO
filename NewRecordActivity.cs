using System;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using Android.Animation;
using Android.Views.Animations;
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
        private static readonly string[] Modes =
        {
            "SSB", "CW", "FM", "AM", "FT8", "FT4", "RTTY", "PSK31", "DMR", "C4FM", "D-STAR", "其他"
        };

        private FrameLayout _root;
        private LinearLayout _sheet;
        private LinearLayout _form;
        private View _scrim;
        private Segmented _seg;

        private EditText _freq, _notes, _call, _rstTx, _rstRx, _qth, _rig, _power, _ant;
        private TextView _dateBtn, _timeBtn;
        private ModePicker _modePicker;
        private TextView _saveBtn;

        private RecKind _kind = RecKind.Frequency;
        private DateTime _when = DateTime.Now;
        private int _imeBottom;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Dui.Init(this);
            Window.SetSoftInputMode(SoftInput.AdjustResize);
            Window.SetStatusBarColor(Color.Transparent);
            Window.SetNavigationBarColor(Color.Transparent);
            if ((int)Build.VERSION.SdkInt >= 30) Window.SetDecorFitsSystemWindows(false);
            if ((int)Build.VERSION.SdkInt >= 29)
            {
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

            // 拖拽指示条
            var handleBox = new FrameLayout(this);
            var handle = new View(this);
            handle.SetBackgroundDrawable(Dui.Round(Dui.WithAlpha(Dui.TextTertiary, 90), 3));
            handleBox.AddView(handle, new FrameLayout.LayoutParams(Dui.Dp(38), Dui.Dp(4)) { Gravity = GravityFlags.Center });
            var hlp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dui.Dp(18));
            _sheet.AddView(handleBox, hlp);

            // 顶部栏
            var head = Dui.HBox();
            head.SetPadding(Dui.Dp(22), 0, Dui.Dp(14), Dui.Dp(6));

            var titleBox = Dui.VBox();
            var t = Dui.Label("新建记录", 19f, Dui.TextPrimary, true);
            var st = Dui.Label("记录一次守听或一次通联", 11.5f, Dui.TextTertiary, false);
            var stlp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Dui.Dp(3) };
            titleBox.AddView(t);
            titleBox.AddView(st, stlp);
            head.AddView(titleBox, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f) { Gravity = GravityFlags.CenterVertical });

            var close = new FrameLayout(this);
            close.SetBackgroundDrawable(Widgets.RowRipple(18));
            close.Clickable = true;
            var ci = Dui.Icon(Resource.Drawable.ic_close, 18, Dui.TextSecondary);
            close.AddView(ci, new FrameLayout.LayoutParams(Dui.Dp(18), Dui.Dp(18)) { Gravity = GravityFlags.Center });
            HamJi.Core.Touch.Bind(close, () => Close(false), 0.9f);
            head.AddView(close, new LinearLayout.LayoutParams(Dui.Dp(36), Dui.Dp(36)) { Gravity = GravityFlags.CenterVertical });

            _sheet.AddView(head, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            // 类型选择
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

            // 表单
            var scroll = new MaxHeightScrollView(this) { MaxRatio = 0.62f };
            scroll.VerticalScrollBarEnabled = false;
            scroll.OverScrollMode = OverScrollMode.Never;
            _form = Dui.VBox();
            _form.SetPadding(Dui.Dp(22), Dui.Dp(16), Dui.Dp(22), Dui.Dp(6));
            scroll.AddView(_form, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
            _sheet.AddView(scroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            CreateInputs();

            // 保存按钮
            var saveWrap = new LinearLayout(this);
            saveWrap.SetPadding(Dui.Dp(22), Dui.Dp(10), Dui.Dp(22), Dui.Dp(16));
            var save = new TextView(this);
            save.Text = "保存";
            save.Gravity = GravityFlags.Center;
            save.SetTextSize(Android.Util.ComplexUnitType.Sp, 15f);
            save.SetTextColor(Color.White);
            save.SetTypeface(Dui.Medium, TypefaceStyle.Normal);
            var sbg = new GradientDrawable(GradientDrawable.Orientation.TopBottom, new[] { Dui.AccentFill, Dui.AccentFill2 });
            sbg.SetCornerRadius(Dui.Dpf(16));
            save.SetBackgroundDrawable(Dui.Ripple(Dui.WithAlpha(Color.White, 70), sbg));
            save.Clickable = true;
            _saveBtn = save;
            HamJi.Core.Touch.Bind(save, Save, 0.97f);
            saveWrap.AddView(save, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dui.Dp(52)));
            _sheet.AddView(saveWrap, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            var sheetLp = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            { Gravity = GravityFlags.Bottom };
            _root.AddView(_sheet, sheetLp);

            SetContentView(_root);
            _root.SetOnApplyWindowInsetsListener(new SheetInsets(this));

            BuildForm();

            _sheet.Post(() =>
            {
                _sheet.TranslationY = _sheet.Height;
                _sheet.Animate().TranslationY(0f).SetDuration(300).SetInterpolator(new DecelerateInterpolator(1.5f)).Start();
                _scrim.Animate().Alpha(1f).SetDuration(220).Start();
            });
        }

        private void CreateInputs()
        {
            _freq = Input("例如 145.500", InputTypes.ClassNumber | InputTypes.NumberFlagDecimal);
            _notes = Input("通联内容、天气、天线方向……", InputTypes.ClassText | InputTypes.TextFlagMultiLine, true);
            _call = Input("例如 BG7XXX", InputTypes.ClassText | InputTypes.TextFlagCapCharacters);
            _rstTx = Input("发 59", InputTypes.ClassText);
            _rstRx = Input("收 59", InputTypes.ClassText);
            _qth = Input("对方位置", InputTypes.ClassText);
            _rig = Input("设备型号", InputTypes.ClassText);
            _power = Input("功率 W", InputTypes.ClassNumber | InputTypes.NumberFlagDecimal);
            _ant = Input("天线 / 频段", InputTypes.ClassText);

            _modePicker = new ModePicker(this, Modes);
            _modePicker.Select(0);
        }

        private void BuildForm()
        {
            Detach(_freq); Detach(_notes); Detach(_call); Detach(_rstTx); Detach(_rstRx);
            Detach(_qth); Detach(_rig); Detach(_power); Detach(_ant); Detach(_modePicker);
            _form.RemoveAllViews();

            _form.AddView(Field("频率 (MHz)", _freq));

            AddGap();
            var modeLabel = Dui.Label("模式", 11.5f, Dui.TextSecondary, true);
            modeLabel.LetterSpacing = 0.03f;
            _form.AddView(modeLabel);
            var mp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Dui.Dp(8) };
            _form.AddView(_modePicker, mp);

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
                var l = Field("信号报告 (发)", _rstTx);
                var r = Field("信号报告 (收)", _rstRx);
                var llp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f) { RightMargin = Dui.Dp(10) };
                var rlp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);
                rstRow.AddView(l, llp);
                rstRow.AddView(r, rlp);
                _form.AddView(rstRow);

                AddGap();
                _form.AddView(Field("QTH / 对方位置", _qth));

                AddGap();
                var rigRow = Dui.HBox();
                rigRow.AddView(Field("设备", _rig), new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1.6f) { RightMargin = Dui.Dp(10) });
                rigRow.AddView(Field("功率 (W)", _power), new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
                _form.AddView(rigRow);

                AddGap();
                _form.AddView(Field("天线", _ant));

                AddGap();
                _form.AddView(Field("备注", _notes));
            }
        }

        private View TimeRow()
        {
            var row = Dui.HBox();
            _dateBtn = PillText();
            _timeBtn = PillText();

            var dlp = new LinearLayout.LayoutParams(0, Dui.Dp(48), 1f) { RightMargin = Dui.Dp(10) };
            row.AddView(_dateBtn, dlp);
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

        private View Field(string label, View input)
        {
            var box = Dui.VBox();
            box.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            var tv = Dui.Label(label, 11.5f, Dui.TextSecondary, true);
            tv.LetterSpacing = 0.03f;
            box.AddView(tv);
            var lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Dui.Dp(8) };
            box.AddView(input, lp);
            return box;
        }

        private EditText Input(string hint, InputTypes type, bool multiline = false)
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
            return e;
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
                    System.Globalization.CultureInfo.InvariantCulture, out freq) || freq <= 0 || freq > 100000)
            {
                Shake(_freq, "请输入有效频率，例如 145.500");
                return;
            }

            if (_kind == RecKind.Log && string.IsNullOrWhiteSpace(_call.Text))
            {
                Shake(_call, "请填写对方呼号");
                return;
            }

            var rec = new QsoRecord
            {
                Kind = (int)_kind,
                Freq = freq,
                Mode = _modePicker.SelectedText,
                Time = new DateTimeOffset(DateTime.SpecifyKind(_when, DateTimeKind.Local)).ToUnixTimeMilliseconds(),
                Notes = (_notes.Text ?? "").Trim()
            };

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

            Store.Add(rec);
            Toast.MakeText(this, "已保存", ToastLength.Short).Show();
            Close(true);
        }

        private void Shake(View v, string msg)
        {
            Toast.MakeText(this, msg, ToastLength.Short).Show();
            v.RequestFocus();
            var anim = ObjectAnimator.OfFloat(v, "translationX", 0f, Dui.Dpf(9), -Dui.Dpf(7), Dui.Dpf(5), -Dui.Dpf(3), 0f);
            anim.SetDuration(320);
            anim.Start();
        }

        internal void OnImeChanged(int imeBottom)
        {
            if (_sheet == null) return;
            if (imeBottom == _imeBottom) return;
            _imeBottom = imeBottom;
            var lp = (FrameLayout.LayoutParams)_sheet.LayoutParameters;
            lp.BottomMargin = imeBottom;
            _sheet.LayoutParameters = lp;
        }

        public override void OnBackPressed()
        {
            Close(false);
        }

        private class SheetInsets : Java.Lang.Object, View.IOnApplyWindowInsetsListener
        {
            private readonly NewRecordActivity _a;
            public SheetInsets(NewRecordActivity a) { _a = a; }

            public WindowInsets OnApplyWindowInsets(View v, WindowInsets insets)
            {
                try
                {
                    if ((int)Build.VERSION.SdkInt >= 30)
                    {
                        var ime = insets.GetInsets(8);
                        var sys = insets.GetInsets(7);
                        _a.OnImeChanged(Math.Max(ime.Bottom, 0));
                        var lp = (FrameLayout.LayoutParams)_a._sheet.LayoutParameters;
                        lp.BottomMargin = Math.Max(ime.Bottom, sys.Bottom);
                        _a._sheet.LayoutParameters = lp;
                    }
                    else
                    {
#pragma warning disable CS0618
                        _a.OnImeChanged(insets.SystemWindowInsetBottom);
#pragma warning restore CS0618
                    }
                }
                catch { }
                return insets;
            }
        }
    }

    /// <summary>限制最大高度的滚动容器，让底部弹层高度自适应但不超过屏幕比例。</summary>
    public class MaxHeightScrollView : ScrollView
    {
        public float MaxRatio { get; set; } = 0.62f;

        public MaxHeightScrollView(Context c) : base(c) { }

        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            var metrics = Resources.DisplayMetrics;
            int max = (int)(metrics.HeightPixels * MaxRatio);
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
            var bg = Dui.Round(Dui.InputBg, 14);
            var stroke = new GradientDrawable();
            stroke.SetColor(Color.Transparent);
            stroke.SetCornerRadius(Dui.Dpf(14));
            stroke.SetStroke(Math.Max(1, Dui.Dp(1)), new Color(Dui.InputBorder));
            var layer = new LayerDrawable(new Drawable[] { bg, stroke });
            SetBackgroundDrawable(layer);

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

    /// <summary>模式选择：横向可滚动的胶囊单选。</summary>
    public class ModePicker : HorizontalScrollView
    {
        private readonly TextView[] _chips;
        private readonly string[] _labels;
        private int _sel;

        public string SelectedText => _labels.Length > 0 ? _labels[_sel] : "";

        public ModePicker(Context c, string[] labels) : base(c)
        {
            _labels = labels;
            HorizontalScrollBarEnabled = false;
            OverScrollMode = OverScrollMode.Never;
            var row = Dui.HBox();
            _chips = new TextView[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                var tv = new TextView(c);
                tv.Text = labels[i];
                tv.SetTextSize(Android.Util.ComplexUnitType.Sp, 12.5f);
                tv.SetPadding(Dui.Dp(14), Dui.Dp(9), Dui.Dp(14), Dui.Dp(9));
                tv.Clickable = true;
                var lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
                { RightMargin = Dui.Dp(8) };
                row.AddView(tv, lp);
                _chips[i] = tv;
                int idx = i;
                tv.Click += (s, e) => Select(idx);
                HamJi.Core.Touch.Press(tv, 0.94f);
            }
            AddView(row, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));
            for (int i = 0; i < _chips.Length; i++) Paint(i, false);
        }

        public void Select(int idx)
        {
            int old = _sel;
            _sel = idx;
            Paint(old, true);
            Paint(idx, true);
        }

        private void Paint(int idx, bool animate)
        {
            var tv = _chips[idx];
            bool on = idx == _sel;
            tv.SetBackgroundDrawable(Dui.Round(on ? Dui.Accent : Dui.ChipBg, 11));
            tv.SetTextColor(new Color(on ? Color.White : Dui.TextSecondary));
            tv.SetTypeface(on ? Dui.Medium : Dui.Regular, TypefaceStyle.Normal);
            if (animate && on)
            {
                tv.ScaleX = 0.92f;
                tv.ScaleY = 0.92f;
                tv.Animate().ScaleX(1f).ScaleY(1f).SetDuration(170).SetInterpolator(new OvershootInterpolator(1.3f)).Start();
            }
        }
    }
}
