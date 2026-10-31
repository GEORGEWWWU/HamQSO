using System;
using System.Text;
using Android.Content;
using Android.Graphics;
using Android.Widget;
using Android.Views;

namespace HamJi.Core
{
    /// <summary>兜底：捕获托管异常，落盘日志并给出可读提示，避免"无提示闪退"。</summary>
    public static class Crash
    {
        private static bool _installed;

        public static void Install(Context ctx)
        {
            if (_installed) return;
            _installed = true;

            try
            {
                Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (s, e) =>
                {
                    var text = e.Exception == null ? "(null)" : e.Exception.ToString();
                    Save(ctx, text);
                    TryToast(ctx, text);
                };
            }
            catch { }

            try
            {
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    var ex = e.ExceptionObject as System.Exception;
                    Save(ctx, ex?.ToString() ?? "(unknown)");
                };
            }
            catch { }
        }

        public static void Save(Context ctx, string text)
        {
            try
            {
                var dir = ctx.FilesDir.AbsolutePath;
                var path = System.IO.Path.Combine(dir, "crash.log");
                System.IO.File.AppendAllText(path, "===== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " =====\n" + text + "\n\n");
            }
            catch { }
        }

        public static void TryToast(Context ctx, string text)
        {
            try
            {
                var one = text.Replace("\n", " ");
                if (one.Length > 160) one = one.Substring(0, 160) + "…";
                Toast.MakeText(ctx, "Ham记异常：" + one, ToastLength.Long).Show();
            }
            catch { }
        }

        /// <summary>把异常直接渲染成可滚动、可复制的文字页，方便截图反馈。</summary>
        public static View BuildFatalView(Context ctx, string title, string detail)
        {
            var scroll = new ScrollView(ctx);
            scroll.SetBackgroundColor(new Color(Color.ParseColor("#F2F5FA")));

            var box = new LinearLayout(ctx) { Orientation = Android.Widget.Orientation.Vertical };
            box.SetPadding(Dui.Dp(20), Dui.Dp(72), Dui.Dp(20), Dui.Dp(40));

            var t = new TextView(ctx);
            t.Text = title;
            t.SetTextSize(Android.Util.ComplexUnitType.Sp, 18f);
            t.SetTextColor(new Color(Color.ParseColor("#C0392B")));
            t.SetTypeface(Typeface.Create("sans-serif-medium", TypefaceStyle.Normal), TypefaceStyle.Normal);
            box.AddView(t);

            var d = new TextView(ctx);
            d.Text = detail;
            d.SetTextSize(Android.Util.ComplexUnitType.Sp, 11.5f);
            d.SetTextColor(new Color(Color.ParseColor("#243447")));
            d.SetTextIsSelectable(true);
            d.SetLineSpacing(Dui.Dpf(3), 1f);
            var lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            { TopMargin = Dui.Dp(14) };
            box.AddView(d, lp);

            var hint = new TextView(ctx);
            hint.Text = "请把本页截图发给开发者；日志已写入 crash.log";
            hint.SetTextSize(Android.Util.ComplexUnitType.Sp, 11f);
            hint.SetTextColor(new Color(Color.ParseColor("#7A8A9A")));
            var hlp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            { TopMargin = Dui.Dp(18) };
            box.AddView(hint, hlp);

            scroll.AddView(box);
            return scroll;
        }
    }
}
