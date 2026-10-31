using System;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using HamJi.Core;
using HamJi.Views;

namespace HamJi.Pages
{
    public class SettingsPage : BasePage
    {
        public const int ReqExport = 0x51A1;
        public event Action<int> Navigate;

        public SettingsPage(Context c) : base(c) { }

        protected override void OnBuild()
        {
            TitleView.Text = "设置";
            SubtitleView.Text = "Ham记 · 本地优先，无需联网";

            // 大头像
            var hero = Widgets.ProfileCard();
            var inner = Dui.VBox();
            inner.SetGravity(GravityFlags.CenterHorizontal);
            inner.SetPadding(Dui.Dp(22), Dui.Dp(26), Dui.Dp(22), Dui.Dp(24));

            var badge = new FrameLayout(Context);
            var g = new GradientDrawable(GradientDrawable.Orientation.TopBottom, new[] { Dui.AccentFill, Dui.AccentFill2 });
            g.SetCornerRadius(Dui.Dpf(26));
            badge.SetBackgroundDrawable(g);
            badge.ClipToOutline = true;
            badge.OutlineProvider = ViewOutlineProvider.Background;
            badge.Elevation = Dui.Dpf(8);
            var ic = Dui.Icon(Resource.Drawable.ic_freq, 44, Color.White);
            badge.AddView(ic, new FrameLayout.LayoutParams(Dui.Dp(44), Dui.Dp(44)) { Gravity = GravityFlags.Center });
            var blp = new LinearLayout.LayoutParams(Dui.Dp(84), Dui.Dp(84)) { Gravity = GravityFlags.CenterHorizontal };
            inner.AddView(badge, blp);

            var name = Dui.Label("Ham记", 20f, Dui.TextPrimary, true);
            name.LetterSpacing = 0.02f;
            var nlp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Dui.Dp(14) };
            nlp.Gravity = GravityFlags.CenterHorizontal;
            inner.AddView(name, nlp);

            var desc = Dui.Label("业余无线电通联日记 · v" + AppInfo.VersionName, 12f, Dui.TextTertiary, false);
            var dlp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Dui.Dp(5) };
            dlp.Gravity = GravityFlags.CenterHorizontal;
            inner.AddView(desc, dlp);

            hero.AddView(inner);
            ListHost.AddView(hero);

            // 记录概览
            ListHost.AddView(Widgets.SectionLabel("记录概览"));
            var card1 = Widgets.Card();
            var r1 = Widgets.SettingRow(Resource.Drawable.ic_freq, "通联频率", Store.CountOf((int)RecKind.Frequency) + " 条");
            r1.Click += (s, e) => Navigate?.Invoke(0);
            card1.AddView(r1);
            card1.AddView(Widgets.Divider());
            var r2 = Widgets.SettingRow(Resource.Drawable.ic_log, "通联日志", Store.CountOf((int)RecKind.Log) + " 条");
            r2.Click += (s, e) => Navigate?.Invoke(1);
            card1.AddView(r2);
            ListHost.AddView(card1);

            // 数据
            ListHost.AddView(Widgets.SectionLabel("数据"));
            var card2 = Widgets.Card();
            var e1 = Widgets.SettingRow(Resource.Drawable.ic_export, "导出备份", "JSON");
            e1.Click += (s, e) => Export();
            card2.AddView(e1);
            card2.AddView(Widgets.Divider());
            var e2 = Widgets.SettingRow(Resource.Drawable.ic_trash, "清空全部记录", "", true, false);
            e2.Click += (s, e) => ClearAll();
            card2.AddView(e2);
            ListHost.AddView(card2);

            // 关于
            ListHost.AddView(Widgets.SectionLabel("关于"));
            var card3 = Widgets.Card();
            var a1 = Widgets.SettingRow(Resource.Drawable.ic_info, "版本", AppInfo.VersionName, false, false);
            card3.AddView(a1);
            card3.AddView(Widgets.Divider());
            var a2 = Widgets.SettingRow(Resource.Drawable.ic_settings, "主题", Dui.Dark ? "深色 · 跟随系统" : "浅色 · 跟随系统", false, false);
            card3.AddView(a2);
            card3.AddView(Widgets.Divider());
            var a3 = Widgets.SettingRow(Resource.Drawable.ic_clock, "数据存储", "仅本机", false, false);
            card3.AddView(a3);
            ListHost.AddView(card3);

            var tip = Dui.Label("颜色与深色模式自动跟随系统设置", 11.5f, Dui.TextTertiary, false);
            tip.SetPadding(Dui.Dp(10), Dui.Dp(18), Dui.Dp(10), 0);
            ListHost.AddView(tip);
        }

        private void Export()
        {
            if (Store.All.Count == 0)
            {
                Toast.MakeText(Context, "还没有可导出的记录", ToastLength.Short).Show();
                return;
            }
            try
            {
                var intent = new Intent(Intent.ActionCreateDocument);
                intent.AddCategory(Intent.CategoryOpenable);
                intent.SetType("application/json");
                intent.PutExtra(Intent.ExtraTitle, "hamji-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".json");
                ((Activity)Context).StartActivityForResult(intent, ReqExport);
            }
            catch (Exception ex)
            {
                Toast.MakeText(Context, "无法打开文件选择器：" + ex.Message, ToastLength.Short).Show();
            }
        }

        public static void HandleExportResult(Activity act, Result resultCode, Intent data)
        {
            if (resultCode != Result.Ok || data?.Data == null) return;
            try
            {
                var json = Store.ToJson();
                using (var stream = act.ContentResolver.OpenOutputStream(data.Data))
                using (var writer = new System.IO.StreamWriter(stream))
                {
                    writer.Write(json);
                    writer.Flush();
                }
                Toast.MakeText(act, "已导出 " + Store.All.Count + " 条记录", ToastLength.Short).Show();
            }
            catch (Exception ex)
            {
                Toast.MakeText(act, "导出失败：" + ex.Message, ToastLength.Short).Show();
            }
        }

        private void ClearAll()
        {
            if (Store.All.Count == 0)
            {
                Toast.MakeText(Context, "当前没有记录", ToastLength.Short).Show();
                return;
            }
            new AlertDialog.Builder(Context, Resource.Style.AppDialogTheme)
                .SetTitle("清空全部记录？")
                .SetMessage("共 " + Store.All.Count + " 条记录将被永久删除，建议先导出备份。")
                .SetNegativeButton("取消", (s, e) => { })
                .SetPositiveButton("清空", (s, e) =>
                {
                    Store.ClearAll();
                    Refresh();
                })
                .Show();
        }
    }

    public static class AppInfo
    {
        private static string _ver;

        public static string VersionName
        {
            get
            {
                if (_ver != null) return _ver;
                try
                {
                    var ctx = Dui.Ctx;
#pragma warning disable CS0618
                    _ver = ctx.PackageManager.GetPackageInfo(ctx.PackageName, 0).VersionName;
#pragma warning restore CS0618
                }
                catch { _ver = "1.0.0"; }
                return _ver;
            }
        }
    }
}
