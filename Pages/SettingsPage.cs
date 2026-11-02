using System;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
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
            hero.SetPadding(Dui.Dp(22), Dui.Dp(16), Dui.Dp(22), Dui.Dp(10));

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

            // 呼号：单独一张卡（身份信息，不和下面的数据管理混在一起）
            var cardCall = Widgets.Card();
            cardCall.AddView(CallRow());
            ListHost.AddView(cardCall);

            ListHost.AddView(Gap());

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

            // 检查更新：右侧显示远端最新版本号，真有新版时后面再挂一个主题色标签
            card2.AddView(UpdateRow());

            var themeRow = Widgets.SettingRow(Resource.Drawable.ic_paint, "主题色",
                AppTheme.Names[AppTheme.Current], false, true, AppTheme.Swatch(AppTheme.Current));
            themeRow.Click += (s2, e2) => ShowThemeMenu(themeRow);
            card2.AddView(themeRow);
            card2.AddView(Row(Resource.Drawable.ic_github, "源码仓库", "GitHub", OpenRepo));
            ListHost.AddView(card2);

            ListHost.AddView(Footer());
        }

        /// <summary>底部版权。</summary>
        private View Footer()
        {
            var box = Dui.VBox();
            box.SetGravity(GravityFlags.CenterHorizontal);
            box.SetPadding(Dui.Dp(8), Dui.Dp(8), Dui.Dp(8), Dui.Dp(12));

            var copy = Dui.Label("© 2026 Ham记 · 保留所有权利", 10.5f, Dui.TextTertiary, false);
            copy.LetterSpacing = 0.02f;
            var clp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
            { Gravity = GravityFlags.CenterHorizontal };
            box.AddView(copy, clp);

            return box;
        }

        private View Gap()
        {
            var v = new View(Context);
            v.LayoutParameters = new LinearLayout.LayoutParams(1, Dui.Dp(8));
            return v;
        }

        /// <summary>检查更新行：右侧「版本号 + 新版可用标签」，标签只在真有新版时出现。</summary>
        private LinearLayout UpdateRow()
        {
            var row = Dui.HBox();
            row.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            row.SetPadding(Dui.Dp(16), Dui.Dp(12), Dui.Dp(14), Dui.Dp(12));
            row.Clickable = true;
            row.Focusable = true;
            HamJi.Core.Touch.Press(row, 0.985f);

            var ic = Dui.Icon(Resource.Drawable.ic_update, 21, Dui.Accent);
            row.AddView(ic, new LinearLayout.LayoutParams(Dui.Dp(21), Dui.Dp(21)));

            var tv = Dui.Label("检查更新", 14.5f, Dui.TextPrimary, false);
            row.AddView(tv, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
            { LeftMargin = Dui.Dp(9) });

            _updValue = Dui.Label("检测中…", 12.5f, Dui.TextTertiary, false);
            _updValue.SetSingleLine(true);
            row.AddView(_updValue, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));

            _updTag = Widgets.Chip("新版可用");
            _updTag.Visibility = ViewStates.Gone;
            row.AddView(_updTag, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
            { LeftMargin = Dui.Dp(6) });

            var ch = Dui.Icon(Resource.Drawable.ic_chevron, 16, Dui.TextTertiary);
            ch.Alpha = 0.7f;
            row.AddView(ch, new LinearLayout.LayoutParams(Dui.Dp(16), Dui.Dp(16)) { LeftMargin = Dui.Dp(4) });

            row.Click += (s, e) => CheckUpdate();
            LoadUpdate();
            return row;
        }

        private TextView _updValue;
        private TextView _updTag;

        /// <summary>把行内文字与标签同步到当前已知的远端版本。</summary>
        private void ApplyUpdateState(string tag)
        {
            if (_updValue == null) return;
            if (string.IsNullOrEmpty(tag))
            {
                _updValue.Text = "—";
                if (_updTag != null) _updTag.Visibility = ViewStates.Gone;
                return;
            }
            _updValue.Text = tag;
            bool newer = Updater.Newer(tag, Updater.AppVersion());
            if (_updTag != null)
            {
                _updTag.Visibility = newer ? ViewStates.Visible : ViewStates.Gone;
            }
        }

        /// <summary>有缓存先用缓存，没有就后台拉一次（不阻塞页面）。</summary>
        private void LoadUpdate()
        {
            var tag = Updater.LatestTag;
            if (!string.IsNullOrEmpty(tag))
            {
                ApplyUpdateState(tag);
                return;
            }
            Updater.Check(Context, r =>
            {
                if (_updValue == null) return;
                if (!IsAttachedToWindow) return;
                ApplyUpdateState(r.Ok ? r.Tag : null);
            });
        }

        private void CheckUpdate()
        {
            if (_updValue != null) _updValue.Text = "检测中…";
            if (_updTag != null) _updTag.Visibility = ViewStates.Gone;
            Toast.MakeText(Context, "正在检查更新…", ToastLength.Short).Show();
            Updater.ManualCheck(Context, () => ApplyUpdateState(Updater.LatestTag));
        }

        private LinearLayout Row(int icon, string title, string value, Action onClick, bool danger = false)
        {
            var row = Widgets.SettingRow(icon, title, value, danger, onClick != null);
            if (onClick != null) row.Click += (s2, e2) => onClick();
            return row;
        }

        /// <summary>独立的呼号卡：图标比普通设置项大一档，右侧用等宽字体显示呼号。</summary>
        private LinearLayout CallRow()
        {
            var row = Dui.HBox();
            row.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            row.SetPadding(Dui.Dp(16), Dui.Dp(14), Dui.Dp(14), Dui.Dp(14));
            row.Clickable = true;
            row.Focusable = true;
            HamJi.Core.Touch.Press(row, 0.985f);

            // ic_call 的视口裁到了图形本身，视觉大小和别的 21dp 图标一致
            var ic = Dui.Icon(Resource.Drawable.ic_call, 21, Dui.Accent);
            row.AddView(ic, new LinearLayout.LayoutParams(Dui.Dp(21), Dui.Dp(21)));

            var tv = Dui.Label("我的呼号", 15f, Dui.TextPrimary, false);
            row.AddView(tv, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
            { LeftMargin = Dui.Dp(11) });

            bool has = Profile.HasCall;
            var val = Dui.Label(has ? Profile.Call : "未设置", has ? 14f : 12.5f,
                has ? Dui.TextPrimary : Dui.TextTertiary, has);
            if (has) val.SetTypeface(Typeface.Create("monospace", TypefaceStyle.Normal), TypefaceStyle.Normal);
            val.LetterSpacing = has ? 0.06f : 0f;
            val.SetSingleLine(true);
            row.AddView(val, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
            { RightMargin = Dui.Dp(4) });

            var ch = Dui.Icon(Resource.Drawable.ic_chevron, 16, Dui.TextTertiary);
            ch.Alpha = 0.7f;
            row.AddView(ch, new LinearLayout.LayoutParams(Dui.Dp(16), Dui.Dp(16)) { LeftMargin = Dui.Dp(4) });

            row.Click += (s, e) => EditCall();
            return row;
        }

        /// <summary>随时修改呼号：校验不过就不让保存，且不会允许改空。</summary>
        private void EditCall()
        {
            var box = Dui.VBox();
            box.SetPadding(Dui.Dp(20), Dui.Dp(8), Dui.Dp(20), 0);

            var input = new EditText(Context);
            input.SetTextSize(Android.Util.ComplexUnitType.Sp, 18f);
            input.SetTextColor(new Color(Dui.TextPrimary));
            input.SetHintTextColor(new Color(Dui.WithAlpha(Dui.TextTertiary, 160)));
            input.Hint = "BH7IDW";
            input.SetTypeface(Typeface.Create("monospace", TypefaceStyle.Normal), TypefaceStyle.Normal);
            input.LetterSpacing = 0.08f;
            input.SetBackgroundDrawable(Dui.InputFieldBg());
            input.SetPadding(Dui.Dp(14), 0, Dui.Dp(14), 0);
            input.SetIncludeFontPadding(false);
            input.SetSingleLine(true);
            input.InputType = InputTypes.ClassText | InputTypes.TextFlagCapCharacters | InputTypes.TextFlagNoSuggestions;
            input.SetFilters(new Android.Text.IInputFilter[]
            {
                new Android.Text.InputFilterLengthFilter(6), new CallFilter()
            });
            input.Text = Profile.Call ?? "";
            try { input.SetSelection(input.Text.Length); } catch { }
            box.AddView(input, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, Dui.Dp(50)) { TopMargin = Dui.Dp(4) });

            var err = Dui.Label("", 11f, Dui.Danger, false);
            err.Visibility = ViewStates.Gone;
            box.AddView(err, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
            { TopMargin = Dui.Dp(6) });

            var note = Dui.Label(Profile.Rule, 11f, Dui.TextTertiary, false);
            box.AddView(note, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
            { TopMargin = Dui.Dp(8), BottomMargin = Dui.Dp(4) });

            var dlg = new AlertDialog.Builder(Context, Resource.Style.AppDialogTheme)
                .SetTitle("我的呼号")
                .SetView(box)
                .SetPositiveButton("保存", (EventHandler<DialogClickEventArgs>)null)
                .SetNegativeButton("取消", (EventHandler<DialogClickEventArgs>)null)
                .Create();

            dlg.SetOnShowListener(new CallDialogHook(dlg, input, err, () => Refresh()));
            dlg.Show();
        }

        private sealed class CallDialogHook : Java.Lang.Object, IDialogInterfaceOnShowListener
        {
            private readonly AlertDialog _dlg;
            private readonly EditText _input;
            private readonly TextView _err;
            private readonly Action _onSaved;

            public CallDialogHook(AlertDialog dlg, EditText input, TextView err, Action onSaved)
            {
                _dlg = dlg; _input = input; _err = err; _onSaved = onSaved;
            }

            public void OnShow(IDialogInterface dialog)
            {
                var btn = _dlg.GetButton((int)DialogButtonType.Positive);
                if (btn != null)
                    btn.Click += (s, e) =>
                    {
                        string error;
                        if (!Profile.TrySave(_input.Text, out error))
                        {
                            _err.Text = error;
                            _err.Visibility = ViewStates.Visible;
                            _input.RequestFocus();
                            return;
                        }
                        _dlg.Dismiss();
                        Toast.MakeText(_dlg.Context, "呼号已保存：" + Profile.Call, ToastLength.Short).Show();
                        _onSaved?.Invoke();
                    };
                var neg = _dlg.GetButton((int)DialogButtonType.Negative);
                if (neg != null) neg.Click += (s, e) => _dlg.Dismiss();
            }
        }

        /// <summary>只放行字母数字并转大写。</summary>
        private sealed class CallFilter : Java.Lang.Object, Android.Text.IInputFilter
        {
            public Java.Lang.ICharSequence FilterFormatted(Java.Lang.ICharSequence source, int start, int end,
                ISpanned dest, int dstart, int dend)
            {
                if (source == null) return null;
                var raw = source.SubSequenceFormatted(start, end).ToString();
                var sb = new System.Text.StringBuilder(raw.Length);
                foreach (char ch in raw.ToUpperInvariant())
                    if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9')) sb.Append(ch);
                var clean = sb.ToString();
                if (clean == raw) return null;
                return new Java.Lang.String(clean);
            }
        }

        private void ShowThemeMenu(View anchor)        {
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
                    // 就地重建，不走 Recreate（Recreate 会露一帧系统背景，看起来像闪屏）
                    if (Context is MainActivity main) main.ReloadTheme();
                    else if (Context is Activity act) act.Recreate();
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
                info = "呼号：" + (Profile.HasCall ? Profile.Call : "未设置") + "\n"
                     + "记录数：" + Store.All.Count + " 条\n"
                     + "数据文件：" + (exists ? (size < 1024 ? size + " B" : (size / 1024) + " KB") : "不存在") + "\n"
                     + "最近写入：" + saved + "\n"
                     + "备份副本：" + (bak ? "有（records.json.bak）" : "暂无") + "\n"
                     + "呼号档案：" + (System.IO.File.Exists(Profile.FilePath) ? "有（profile.json）" : "暂无") + "\n"
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
            if (Store.All.Count == 0 && !Profile.HasCall)
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
                Toast.MakeText(act, "已导出 " + Store.All.Count + " 条记录"
                    + (Profile.HasCall ? " · 含呼号 " + Profile.Call : ""), ToastLength.Short).Show();
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
            string backupCall;
            try
            {
                list = Store.ParseBackup(json, out backupCall);
            }
            catch (Exception ex)
            {
                Toast.MakeText(act, "不是有效的备份文件：" + ex.Message, ToastLength.Long).Show();
                return;
            }

            bool callUsable = !Profile.HasCall && Profile.IsValid(Profile.Normalize(backupCall));

            if (list.Count == 0)
            {
                if (callUsable)
                {
                    Profile.AdoptIfEmpty(backupCall);
                    Toast.MakeText(act, "已从备份恢复呼号：" + Profile.Call, ToastLength.Short).Show();
                }
                else
                {
                    Toast.MakeText(act, "备份文件里没有记录", ToastLength.Short).Show();
                }
                return;
            }

            int have = Store.All.Count;
            var msg = "当前已有 " + have + " 条记录。\n\n"
                      + "合并：保留现有记录，只补充备份里没有的条目。\n"
                      + "覆盖：清空现有记录，完全恢复成备份内容。";
            if (!string.IsNullOrEmpty(backupCall))
            {
                msg += "\n\n备份里的呼号：" + Profile.Normalize(backupCall)
                     + (Profile.HasCall ? "（保留当前呼号 " + Profile.Call + "）" : "（恢复时会一并写入）");
            }

            new AlertDialog.Builder(act, Resource.Style.AppDialogTheme)
                .SetTitle("导入 " + list.Count + " 条记录")
                .SetMessage(msg)
                .SetPositiveButton("合并", (s, e) =>
                {
                    if (callUsable) Profile.AdoptIfEmpty(backupCall);
                    int n = Store.MergeIn(list);
                    Toast.MakeText(act, n > 0 ? "已导入 " + n + " 条记录" : "没有新增记录（全部已存在）",
                        ToastLength.Short).Show();
                })
                .SetNeutralButton("覆盖", (s, e) =>
                {
                    if (callUsable) Profile.AdoptIfEmpty(backupCall);
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
