using System;
using Android.Content;
using Android.Graphics;
using Android.Text;
using Android.Views;
using Android.Widget;
using HamJi.Core;
using HamJi.Views;

namespace HamJi.Pages
{
    public class FrequencyPage : BasePage
    {
        public FrequencyPage(Context c) : base(c) { AddHeaderEntries(); }

        protected override void OnBuild()
        {
            var items = Store.Of((int)RecKind.Frequency);
            TitleView.Text = "通联频率";
            SubtitleView.Text = items.Count == 0
                ? "守听到的通联频点都记在这里"
                : UpdatedLine(items.Count, "条频点");

            RenderGrouped(items, Row, Resource.Drawable.ic_freq,
                "还没有频点记录", "点击右下角的 + 记录你的第一段频率");
        }

        private View Row(QsoRecord rec)
        {
            var row = Dui.VBox();
            row.SetPadding(Dui.Dp(14), Dui.Dp(10), Dui.Dp(14), Dui.Dp(10));
            row.Clickable = true;
            row.Focusable = true;
            row.LongClickable = true;

            var l1 = Dui.HBox();
            var freq = Dui.Label(rec.FreqText, 21f, Dui.TextPrimary, true);
            freq.SetTypeface(Dui.Mono, TypefaceStyle.Normal);
            freq.LetterSpacing = -0.02f;
            l1.AddView(freq);

            var unit = Dui.Label(rec.UnitText, 10.5f, Dui.TextTertiary, true);
            var ulp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
            { LeftMargin = Dui.Dp(4), Gravity = GravityFlags.Bottom, BottomMargin = Dui.Dp(3) };
            l1.AddView(unit, ulp);

            l1.AddView(new View(Dui.Ctx), new LinearLayout.LayoutParams(0, 1, 1f));

            var time = Dui.Label(rec.TimeText, 12f, Dui.TextTertiary, false);
            l1.AddView(time);
            row.AddView(l1);

            var l2 = Dui.HBox();
            var lp2 = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            { TopMargin = Dui.Dp(5) };
            l2.LayoutParameters = lp2;

            if (!string.IsNullOrWhiteSpace(rec.Mode))
            {
                var chip = Widgets.Chip(rec.Mode);
                var clp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
                clp.RightMargin = Dui.Dp(8);
                l2.AddView(chip, clp);
            }

            var note = string.IsNullOrWhiteSpace(rec.Notes) ? "无备注" : rec.Notes;
            var nl = Dui.Label(note, 12.5f, string.IsNullOrWhiteSpace(rec.Notes) ? Dui.TextTertiary : Dui.TextSecondary, false);
            nl.SetSingleLine(true);
            nl.Ellipsize = TextUtils.TruncateAt.End;
            l2.AddView(nl, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

            row.AddView(l2);

            row.Click += (s, e) => ShowDetail(rec);
            row.LongClick += (s, e) => { e.Handled = true; OpenEditor(rec); };
            HamJi.Core.Touch.Press(row, 0.985f);
            return row;
        }
    }

    public class LogPage : BasePage
    {
        public LogPage(Context c) : base(c) { AddHeaderEntries(); }

        protected override void OnBuild()
        {
            var items = Store.Of((int)RecKind.Log);
            TitleView.Text = "通联日志";
            SubtitleView.Text = items.Count == 0
                ? "每一次通联，都值得被记住"
                : UpdatedLine(items.Count, "次通联");

            RenderGrouped(items, Row, Resource.Drawable.ic_log,
                "还没有通联日志", "点击右下角的 + 记录你的第一次通联");
        }

        private View Row(QsoRecord rec)
        {
            var row = Dui.VBox();
            row.SetPadding(Dui.Dp(14), Dui.Dp(10), Dui.Dp(14), Dui.Dp(10));
            row.Clickable = true;
            row.Focusable = true;
            row.LongClickable = true;

            var l1 = Dui.HBox();
            var call = Dui.Label(string.IsNullOrWhiteSpace(rec.Call) ? "未填写呼号" : rec.Call.ToUpperInvariant(),
                18.5f, string.IsNullOrWhiteSpace(rec.Call) ? Dui.TextTertiary : Dui.TextPrimary, true);
            call.LetterSpacing = 0.01f;
            l1.AddView(call);

            if (!string.IsNullOrWhiteSpace(rec.Mode))
            {
                var chip = Widgets.Chip(rec.Mode);
                var clp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
                clp.LeftMargin = Dui.Dp(8);
                l1.AddView(chip, clp);
            }

            l1.AddView(new View(Dui.Ctx), new LinearLayout.LayoutParams(0, 1, 1f));
            l1.AddView(Dui.Label(rec.TimeText, 12f, Dui.TextTertiary, false));
            row.AddView(l1);

            var meta = Dui.FreqLine(rec);
            if (!string.IsNullOrWhiteSpace(rec.RstText)) meta += "   " + rec.RstText;
            if (!string.IsNullOrWhiteSpace(rec.Qth)) meta += "   " + rec.Qth;
            var ml = Dui.Label(meta, 12.5f, Dui.TextSecondary, false);
            ml.SetSingleLine(true);
            ml.Ellipsize = TextUtils.TruncateAt.End;
            var mlp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            { TopMargin = Dui.Dp(4) };
            row.AddView(ml, mlp);

            if (!string.IsNullOrWhiteSpace(rec.Notes))
            {
                var nl = Dui.Label(rec.Notes, 12f, Dui.TextTertiary, false);
                nl.SetSingleLine(true);
                nl.Ellipsize = TextUtils.TruncateAt.End;
                var nlp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
                { TopMargin = Dui.Dp(3) };
                row.AddView(nl, nlp);
            }

            row.Click += (s, e) => ShowDetail(rec);
            row.LongClick += (s, e) => { e.Handled = true; OpenEditor(rec); };
            HamJi.Core.Touch.Press(row, 0.985f);
            return row;
        }
    }
}
