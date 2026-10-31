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
        public const int ReqImport = 0x51A2;
        private PopupWindow _themePop;
        public event Action<int> Navigate;

        public SettingsPage(Context c) : base(c) { ShowHeader = false; }

        protected override void OnBuild()
        {
            ShowHeader = false;

            // 顶部 App 信息：无卡片背景，直接沉浸
            var hero = Dui.VBox();
            hero.SetGravity(GravityFlags.CenterHorizontal);
            hero.SetPadding(Dui.Dp(22), Dui.Dp(18), Dui.Dp(22), Dui.Dp(18));

            // 直接用 App 自己的图标图形（radio-tower），不再拿功能图标凑数
            var badge = new FrameLayout(Context);
            var g = new GradientDrawable(GradientDrawable.Orientation.TopBottom, new[] { Dui.AccentFill, Dui.AccentFill2 });
            g.SetCornerRadius(Dui.Dpf(22));
            badge.SetBackgroundDrawable(g);
            badge.ClipToOutline = true;
            badge.OutlineProvider = ViewOutlineProvider.Background;
            badge.Elevation = Dui.Dpf(10);
            var ic = Dui.Icon(Resource.Drawable.ic_tower, 78, Color.White);
            badge.AddView(ic, new FrameLayout.LayoutParams(Dui.Dp(78), Dui.Dp(78)) { Gravity = GravityFlags.Center });
            hero.AddView(badge, new LinearLayout.LayoutParams(Dui.Dp(78), Dui.Dp(78)) { Gravity = GravityFlags.CenterHorizontal });

            var name = Dui.Label("Ham记", 20f, Dui.TextPrimary, true);
            name.LetterSpacing = 0.02f;
            var nlp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Dui.Dp(12) };
            nlp.Gravity = GravityFlags.CenterHorizontal;
            hero.AddView(name, nlp);

            var desc = Dui.Label("业余无线电通联日记 · v" + AppInfo.VersionName, 11.5f, Dui.TextTertiary, false);
            var dlp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Dui.Dp(4) };
            dlp.Gravity = GravityFlags.CenterHorizontal;
            hero.AddView(desc, dlp);
            ListHost.AddView(hero);

            // 数据（无分组标题、无分割线）
            var card1 = Widgets.Card();
            card1.AddView(Row(Resource.Drawable.ic_info, "数据自检", "查看", ShowDataStatus));
            card1.AddView(Row(Resource.Drawable.ic_export, "导出备份", "JSON", Export));
            card1.AddView(Row(Resource.Drawable.ic_import, "导入恢复", "JSON", Import));
            card1.AddView(Row(Resource.Drawable.ic_trash, "清空全部记录", "", ClearAll, true));
            ListHost.AddView(card1);

            ListHost.AddView(Gap());

            // 关于
            var card2 = Widgets.Card();

            // 右侧显示远端最新版本号：先给缓存值，没有就后台拉一次再回填
            string remote = Updater.LatestTag;
            TextView updateValue;
            var updateRow = Widgets.SettingRow(Resource.Drawable.ic_update, "检查更新",
                string.IsNullOrEmpty(remote) ? "检测中…" : remote, false, true, 0, out updateValue);
            updateRow.Click += (s2, e2) => CheckUpdate();
            card2.AddView(updateRow);
            if (string.IsNullOrEmpty(remote))
            {
                Updater.EnsureLatest(Context, tag =>
                {
                    if (updateValue != null && !string.IsNullOrEmpty(tag)) updateValue.Text = tag;
                });
            }

            var themeRow = Widgets.SettingRow(Resource.Drawable.ic_settings, "主题色",
                AppTheme.Names[AppTheme.Current], false, true, AppTheme.Swatch(AppTheme.Current));
            themeRow.Click += (s2, e2) => ShowThemeMenu(themeRow);
            card2.AddView(themeRow);
            card2.AddView(Row(Resource.Drawable.ic_export, "源码仓库", "GitHub", OpenRepo));
            ListHost.AddView(card2);

            ListHost.AddView(Footer());
        }

        /// <summary>底部版权与署名。</summary>
        private View Footer()
        {
            var box = Dui.VBox();
            box.SetGravity(GravityFlags.CenterHorizontal);
            box.SetPadding(Dui.Dp(8), Dui.Dp(26), Dui.Dp(8), Dui.Dp(14));

            var sign = Dui.Label("BH7IDW", 11.5f, Dui.TextSecondary, true);
            sign.LetterSpacing = 0.08f;
            var slp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
            { Gravity = GravityFlags.CenterHorizontal };
            box.AddView(sign, slp);

            var copy = Dui.Label("© 2026 Ham记 · 保留所有权利", 10.5f, Dui.TextTertiary, false);
            copy.LetterSpacing = 0.02f;
            var clp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
            { TopMargin = Dui.Dp(6), Gravity = GravityFlags.CenterHorizontal };
            box.AddView(copy, clp);

            return box;
        }

        private View Gap()
        {
            var v = new View(Context);
            v.LayoutParameters = new LinearLayout.LayoutParams(1, Dui.Dp(12));
            return v;
        }

        private void CheckUpdate()
        {
            Toast.MakeText(Context, "正在检查更新…", ToastLength.Short).Show();
            Updater.ManualCheck(Context);
        }

        private LinearLayout Row(int icon, string title, string value, Action onClick, bool danger = false)
        {
            var row = Widgets.SettingRow(icon, title, value, danger, onClick != null);
            if (onClick != null) row.Click += (s2, e2) => onClick();
            return row;
        }

        private void ShowThemeMenu(View anchor)
        {
            var list = Dui.VBox();
            list.SetPadding(Dui.Dp(6), Dui.Dp(6), Dui.Dp(6), Dui.Dp(6));

            for (int i = 0; i < AppTheme.Names.Length; i++)
            {
                int idx = i;
                var row = Dui.HBox();
                row.SetPadding(Dui.Dp(12), 0, Dui.Dp(10), 0);
                row.Clickable = true;

                var dot = new View(Context);
                dot.SetBackgroundDrawable(Dui.Round(AppTheme.Swatch(i), 8));
                row.AddView(dot, new LinearLayout.LayoutParams(Dui.Dp(16), Dui.Dp(16)));

                var tv = Dui.Label(AppTheme.Names[i], 14f, Dui.TextPrimary, i == AppTheme.Current);
                row.AddView(tv, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f) { LeftMargin = Dui.Dp(10) });

                if (i == AppTheme.Current)
                {
                    var chk = Dui.Icon(Resource.Drawable.ic_check, 16, Dui.Accent);
                    row.AddView(chk, new LinearLayout.LayoutParams(Dui.Dp(16), Dui.Dp(16)));
                }

                row.Click += (s2, e2) =>
                {
                    _themePop?.Dismiss();
                    if (idx == AppTheme.Current) return;
                    AppTheme.Set(Context, idx);
                    if (Context is Activity act) act.Recreate();
                };
                list.AddView(row, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dui.Dp(42)));
            }

            var sv = new MaxHeightScrollView(Context) { MaxRatio = 1f };
            sv.VerticalScrollBarEnabled = false;
            sv.AddView(list, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            var w = anchor.Width > 0 ? anchor.Width : Dui.Dp(240);
            int screenH = Resources.DisplayMetrics.HeightPixels;
            var loc = new int[2];
            anchor.GetLocationOnScreen(loc);

            int want = Dui.Dp(42) * AppTheme.Names.Length + Dui.Dp(12);
            int below = screenH - (loc[1] + anchor.Height) - Dui.Dp(20);
            int above = loc[1] - Dui.Dp(20);

            bool up = below < want && above > below;
            int h = Math.Max(Dui.Dp(120), Math.Min(want, up ? above : below));

            _themePop = new PopupWindow(sv, w, h);
            _themePop.SetBackgroundDrawable(Dui.Round(Dui.SheetBg, 16));
            _themePop.OutsideTouchable = true;
            _themePop.Focusable = true;
            _themePop.Elevation = Dui.Dpf(12);

            if (up) _themePop.ShowAtLocation(anchor, GravityFlags.NoGravity, loc[0], loc[1] - h - Dui.Dp(6));
            else _themePop.ShowAsDropDown(anchor, 0, Dui.Dp(4));
        }

        private const string RepoUrl = "https://github.com/GEORGEWWWU/HamQSO";

        private void OpenRepo()
        {
            try
            {
                var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse(RepoUrl));
                intent.AddFlags(ActivityFlags.NewTask);
                Context.StartActivity(intent);
            }
            catch (Exception ex)
            {
                Toast.MakeText(Context, "无法打开浏览器：" + ex.Message, ToastLength.Short).Show();
            }
        }

        private void ShowDataStatus()
        {
            string info;
            try
            {
                var f = Store.FilePath;
                var exists = System.IO.File.Exists(f);
                long size = exists ? new System.IO.FileInfo(f).Length : 0;
                var bak = System.IO.File.Exists(Store.BackupPath);
                var saved = exists ? System.IO.File.GetLastWriteTime(f).ToString("MM-dd HH:mm") : "—";
                info = "记录数：" + Store.All.Count + " 条\n"
                     + "数据文件：" + (exists ? (size < 1024 ? size + " B" : (size / 1024) + " KB") : "不存在") + "\n"
                     + "最近写入：" + saved + "\n"
                     + "备份副本：" + (bak ? "有（records.json.bak）" : "暂无") + "\n"
                     + "写入状态：" + (Store.IsBlocked ? "已保护（检测到数据异常，暂停写入）" : "正常");
                if (!string.IsNullOrEmpty(Store.LastError)) info += "\n提示：" + Store.LastError;
            }
            catch (Exception ex)
            {
                info = "读取状态失败：" + ex.Message;
            }

            new AlertDialog.Builder(Context, Resource.Style.AppDialogTheme)
                .SetTitle("数据自检")
                .SetMessage(info)
                .SetPositiveButton("关闭", (s2, e2) => { })
                .Show();
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

        private void Import()
        {
            try
            {
                var intent = new Intent(Intent.ActionOpenDocument);
                intent.AddCategory(Intent.CategoryOpenable);
                intent.SetType("*/*");
                intent.PutExtra(Intent.ExtraMimeTypes,
                    new[] { "application/json", "text/plain", "application/octet-stream" });
                ((Activity)Context).StartActivityForResult(intent, ReqImport);
            }
            catch (Exception ex)
            {
                Toast.MakeText(Context, "无法打开文件选择器：" + ex.Message, ToastLength.Short).Show();
            }
        }

        /// <summary>读取选中的备份文件，让用户选「合并」或「覆盖」后写回存储。</summary>
        public static void HandleImportResult(Activity act, Result resultCode, Intent data)
        {
            if (resultCode != Result.Ok || data?.Data == null) return;

            string json;
            try
            {
                using (var stream = act.ContentResolver.OpenInputStream(data.Data))
                using (var reader = new System.IO.StreamReader(stream))
                    json = reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                Toast.MakeText(act, "读取文件失败：" + ex.Message, ToastLength.Short).Show();
                return;
            }

            System.Collections.Generic.List<QsoRecord> list;
            try
            {
                list = Store.ParseBackup(json);
            }
            catch (Exception ex)
            {
                Toast.MakeText(act, "不是有效的备份文件：" + ex.Message, ToastLength.Long).Show();
                return;
            }

            if (list.Count == 0)
            {
                Toast.MakeText(act, "备份文件里没有记录", ToastLength.Short).Show();
                return;
            }

            int have = Store.All.Count;
            new AlertDialog.Builder(act, Resource.Style.AppDialogTheme)
                .SetTitle("导入 " + list.Count + " 条记录")
                .SetMessage("当前已有 " + have + " 条记录。\n\n"
                          + "合并：保留现有记录，只补充备份里没有的条目。\n"
                          + "覆盖：清空现有记录，完全恢复成备份内容。")
                .SetPositiveButton("合并", (s, e) =>
                {
                    int n = Store.MergeIn(list);
                    Toast.MakeText(act, n > 0 ? "已导入 " + n + " 条记录" : "没有新增记录（全部已存在）",
                        ToastLength.Short).Show();
                })
                .SetNeutralButton("覆盖", (s, e) =>
                {
                    Store.ReplaceAll(list);
                    Toast.MakeText(act, "已恢复 " + list.Count + " 条记录", ToastLength.Short).Show();
                })
                .SetNegativeButton("取消", (s, e) => { })
                .Show();
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
