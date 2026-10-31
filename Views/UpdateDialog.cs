using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Views;
using Android.Widget;
using HamJi.Core;

namespace HamJi.Views
{
    /// <summary>
    /// 发现新版本弹窗：只有「不再提示」和「点击更新」两个选项。
    /// 点更新后原地下载，弹窗下方实时显示进度条与百分比，下完直接拉起系统安装器覆盖安装。
    ///
    /// 窗口结构（踩过的坑，别改回去）：
    /// - 平台 Dialog 的浮窗自带一层不透明窗口背景，圆角外会露出灰色矩形；
    ///   而且窗口高度在内容变化后不会重新测量，底部按钮会被裁掉。
    ///   所以用「全屏透明窗口 + 自绘遮罩 + 居中卡片」，卡片随内容自然长高。
    /// - 进度条不用 ProgressBar，自己画轨道和填充，避免动态宽度在布局里不生效。
    /// </summary>
    public class UpdateDialog : Dialog
    {
        private readonly Updater.Result _r;
        private LinearLayout _card, _progressBox;
        private ProgressTrack _bar;
        private TextView _pctView, _hintView, _skipBtn, _updateBtn;
        private View _scrim;
        private bool _busy, _downloaded;

        public UpdateDialog(Activity a, Updater.Result r) : base(a, Resource.Style.TransparentDialogTheme)
        {
            _r = r;
            Window.RequestFeature(WindowFeatures.NoTitle);
            BuildUi();
        }

        private void BuildUi()
        {
            var win = Window;
            win.SetBackgroundDrawable(new ColorDrawable(Color.Transparent));
            var lp = win.Attributes;
            lp.Gravity = GravityFlags.FillHorizontal | GravityFlags.CenterVertical;
            lp.Width = ViewGroup.LayoutParams.MatchParent;
            lp.Height = ViewGroup.LayoutParams.MatchParent;
            win.Attributes = lp;

            // 全屏根布局：遮罩 + 居中卡片
            var root = new FrameLayout(Context);
            _scrim = new View(Context);
            _scrim.SetBackgroundColor(new Color(Color.Argb(120, 0, 0, 0)));
            _scrim.Clickable = true;
            _scrim.Click += (s, e) => { if (!_busy) Dismiss(); };
            root.AddView(_scrim, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

            _card = Dui.VBox();
            var bg = new GradientDrawable();
            bg.SetColor(new Color(Dui.SheetBg));
            bg.SetCornerRadius(Dui.Dpf(24));
            _card.SetBackgroundDrawable(bg);
            _card.SetPadding(Dui.Dp(22), Dui.Dp(22), Dui.Dp(22), Dui.Dp(20));
            _card.ClipToOutline = true;
            _card.OutlineProvider = ViewOutlineProvider.Background;
            _card.Elevation = Dui.Dpf(18);

            // 标题
            _card.AddView(Dui.Label("发现新版本 " + (_r.Tag ?? ""), 17f, Dui.TextPrimary, true));
            _card.AddView(Dui.Label("当前版本 v" + Updater.AppVersion() + " · 更新后数据会保留", 11.5f, Dui.TextTertiary, false),
                new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Dui.Dp(6) });

            // 更新说明
            string notes = (_r.Notes ?? "").Replace("\r", "").Trim();
            if (notes.Length > 300) notes = notes.Substring(0, 300) + "…";
            if (!string.IsNullOrEmpty(notes))
            {
                var body = Dui.Label(notes, 12.5f, Dui.TextSecondary, false);
                body.SetLineSpacing(Dui.Dpf(3), 1f);
                body.SetMaxLines(8);
                body.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
                _card.AddView(body, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
                { TopMargin = Dui.Dp(14) });
            }

            // 进度区（默认隐藏）
            _progressBox = Dui.VBox();
            _progressBox.Visibility = ViewStates.Gone;

            var row = Dui.HBox();
            _hintView = Dui.Label("正在下载…", 11.5f, Dui.TextTertiary, false);
            _hintView.SetSingleLine(true);
            _hintView.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
            _pctView = Dui.Label("0%", 11.5f, Dui.Accent, true);
            _pctView.SetSingleLine(true);
            _pctView.Gravity = GravityFlags.End | GravityFlags.CenterVertical;
            _pctView.SetMinWidth(Dui.Dp(46));
            row.AddView(_hintView, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
            row.AddView(_pctView, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
            { LeftMargin = Dui.Dp(8) });
            _progressBox.AddView(row);

            _bar = new ProgressTrack(Context);
            _progressBox.AddView(_bar, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dui.Dp(6))
            { TopMargin = Dui.Dp(10) });

            _card.AddView(_progressBox, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            { TopMargin = Dui.Dp(16) });

            // 两个按钮
            var btns = Dui.HBox();
            _skipBtn = Button("不再提示", false);
            _skipBtn.Click += (s, e) =>
            {
                if (_busy) return;
                try { Updater.SkipVersion(Context, _r.Tag); } catch { }
                Dismiss();
            };
            btns.AddView(_skipBtn, new LinearLayout.LayoutParams(0, Dui.Dp(46), 1f) { RightMargin = Dui.Dp(10) });

            _updateBtn = Button("点击更新", true);
            _updateBtn.Click += (s, e) => StartUpdate();
            btns.AddView(_updateBtn, new LinearLayout.LayoutParams(0, Dui.Dp(46), 1f));

            _card.AddView(btns, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            { TopMargin = Dui.Dp(20) });

            root.AddView(_card, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            {
                Gravity = GravityFlags.Center,
                LeftMargin = Dui.Dp(24),
                RightMargin = Dui.Dp(24)
            });

            SetContentView(root);
        }

        /// <summary>从「安装未知应用」设置页返回时窗口重新拿到焦点，若已授权就自动继续安装。</summary>
        public override void OnWindowFocusChanged(bool hasFocus)
        {
            base.OnWindowFocusChanged(hasFocus);
            if (!hasFocus || !_downloaded || _busy) return;
            Install();
        }

        private TextView Button(string text, bool primary)
        {
            var tv = Dui.Label(text, 14f, primary ? Color.White : Dui.TextPrimary, true);
            tv.Gravity = GravityFlags.Center;
            tv.Clickable = true;
            if (primary)
            {
                var g = new GradientDrawable(GradientDrawable.Orientation.TopBottom, new[] { Dui.AccentFill, Dui.AccentFill2 });
                g.SetCornerRadius(Dui.Dpf(14));
                tv.SetBackgroundDrawable(g);
            }
            else
            {
                tv.SetBackgroundDrawable(Dui.Round(Dui.InputBg, 14));
            }
            HamJi.Core.Touch.Press(tv, 0.96f);
            return tv;
        }

        private void StartUpdate()
        {
            if (_busy) return;

            // 已经下好了（比如上次被「未知来源」授权打断），直接装
            if (_downloaded)
            {
                Install();
                return;
            }

            if (string.IsNullOrEmpty(_r.ApkUrl))
            {
                Updater.OpenRelease(Context, _r.Url);
                Dismiss();
                return;
            }

            _busy = true;
            _progressBox.Visibility = ViewStates.Visible;
            _pctView.Text = "0%";
            _hintView.Text = "正在下载…";
            _hintView.SetTextColor(new Color(Dui.TextTertiary));
            _bar.SetPercent(0);
            _updateBtn.Text = "下载中…";
            _updateBtn.Alpha = 0.6f;
            _updateBtn.Clickable = false;
            _skipBtn.Alpha = 0.4f;
            _skipBtn.Clickable = false;

            Task.Run(() => Download());
        }

        /// <summary>Android 8+ 需要「安装未知应用」授权；没开就引导去开，下载好的包保留着。</summary>
        private bool EnsureInstallPermission()
        {
            if ((int)Android.OS.Build.VERSION.SdkInt < 26) return true;
            bool can = false;
            try { can = Context.PackageManager.CanRequestPackageInstalls(); } catch { }
            if (can) return true;

            Android.Util.Log.Info("HamJiUpd", "install permission missing, open settings");
            Toast.MakeText(Context, "请允许「Ham记」安装未知应用，返回后会自动继续", ToastLength.Long).Show();
            try
            {
                var i = new Intent(Android.Provider.Settings.ActionManageUnknownAppSources);
                i.SetData(Android.Net.Uri.Parse("package:" + Context.PackageName));
                i.AddFlags(ActivityFlags.NewTask);
                Context.StartActivity(i);
            }
            catch
            {
                try { Context.StartActivity(new Intent(Android.Provider.Settings.ActionManageUnknownAppSources)); } catch { }
            }

            _busy = false;
            _updateBtn.Text = "继续安装";
            _updateBtn.Alpha = 1f;
            _updateBtn.Clickable = true;
            _skipBtn.Alpha = 1f;
            _skipBtn.Clickable = true;
            _hintView.Text = "已下载完成，请允许安装未知应用";
            return false;
        }

        private void Download()
        {
            string path = ApkProvider.FilePathFor(Context);
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }

            try
            {
                var req = (HttpWebRequest)WebRequest.Create(_r.ApkUrl);
                req.Method = "GET";
                req.Timeout = 30000;
                req.ReadWriteTimeout = 60000;
                req.AllowAutoRedirect = true;
                req.UserAgent = "HamJi/" + Updater.AppVersion();

                Android.Util.Log.Info("HamJiUpd", "download start " + _r.ApkUrl);
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var input = resp.GetResponseStream())
                using (var output = File.Create(path))
                {
                    long total = resp.ContentLength;
                    Android.Util.Log.Info("HamJiUpd", "download resp " + (int)resp.StatusCode + " len=" + total);
                    var buf = new byte[64 * 1024];
                    long got = 0;
                    int last = -1;
                    int n;
                    while ((n = input.Read(buf, 0, buf.Length)) > 0)
                    {
                        output.Write(buf, 0, n);
                        got += n;
                        int pct = total > 0 ? (int)(got * 100 / total) : -1;
                        if (pct != last)
                        {
                            last = pct;
                            int p = pct;
                            long g = got, t = total;
                            Ui(() => ShowProgress(p, g, t));
                        }
                    }
                }

                Ui(OnDownloaded);
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                Android.Util.Log.Info("HamJiUpd", "download fail: " + ex.GetType().Name + " " + msg);
                Ui(() => Fail("下载失败：" + msg));
            }
        }

        private void Ui(Action a)
        {
            try
            {
                if (Context is Activity act) act.RunOnUiThread(a);
                else a();
            }
            catch { }
        }

        private void ShowProgress(int pct, long got, long total)
        {
            if (pct < 0)
            {
                _pctView.Text = "";
                _hintView.Text = "正在下载… " + Mb(got);
                _bar.SetPercent(0);
                return;
            }
            _pctView.Text = pct + "%";
            _hintView.Text = total > 0 ? (Mb(got) + " / " + Mb(total)) : "正在下载…";
            _bar.SetPercent(pct);
        }

        private static string Mb(long bytes)
            => (bytes / 1024.0 / 1024.0).ToString("0.0") + " MB";

        private void OnDownloaded()
        {
            _downloaded = true;
            _busy = false;
            // 直接拉起系统安装器：未知来源授权交给系统自己弹（比我们先跳设置页顺得多），
            // 只有真的被拒了再回退到授权引导。
            Install();
        }

        private void Install()
        {
            try
            {
                _pctView.Text = "100%";
                _hintView.Text = "下载完成，正在打开安装程序…";
                _bar.SetPercent(100);

                Android.Util.Log.Info("HamJiUpd", "install launch, uri=" + ApkProvider.UriFor());
                var intent = new Intent(Intent.ActionView);
                intent.SetDataAndType(ApkProvider.UriFor(), "application/vnd.android.package-archive");
                intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.GrantReadUriPermission);
                Context.StartActivity(intent);
                Dismiss();
            }
            catch (Exception ex)
            {
                Android.Util.Log.Info("HamJiUpd", "install fail: " + ex.GetType().Name + " " + ex.Message);
                if (!EnsureInstallPermission()) return;
                Fail("无法打开安装程序：" + ex.Message);
            }
        }

        private void Fail(string msg)
        {
            _busy = false;
            _hintView.Text = msg;
            _hintView.SetTextColor(new Color(Dui.Danger));
            _updateBtn.Text = "重试";
            _updateBtn.Alpha = 1f;
            _updateBtn.Clickable = true;
            _skipBtn.Alpha = 1f;
            _skipBtn.Clickable = true;
        }

        /// <summary>自绘进度条：轨道 + 填充，不依赖子 View 的动态宽度。</summary>
        private sealed class ProgressTrack : View
        {
            private readonly Paint _paint = new Paint(PaintFlags.AntiAlias);
            private int _pct;

            public ProgressTrack(Context c) : base(c) { }

            public void SetPercent(int pct)
            {
                _pct = Math.Max(0, Math.Min(100, pct));
                Invalidate();
            }

            protected override void OnDraw(Canvas canvas)
            {
                base.OnDraw(canvas);
                if (Width <= 0 || Height <= 0) return;
                float r = Height / 2f;

                _paint.Color = new Color(Dui.InputBg);
                canvas.DrawRoundRect(new RectF(0, 0, Width, Height), r, r, _paint);

                float w = Width * _pct / 100f;
                if (w <= 0) return;
                _paint.Color = new Color(Dui.Accent);
                canvas.DrawRoundRect(new RectF(0, 0, w, Height), r, r, _paint);
            }
        }
    }
}
