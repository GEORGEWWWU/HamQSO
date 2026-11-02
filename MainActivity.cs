using System;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using Android.Animation;
using Android.Views.Animations;
using HamJi.Core;
using HamJi.Pages;
using HamJi.Views;

namespace HamJi
{
    [Activity(
        Label = "Ham记",
        Name = "com.hamji.app.MainActivity",
        Theme = "@style/AppTheme",
        MainLauncher = true,
        Exported = true,
        LaunchMode = LaunchMode.SingleTop,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.Density
            | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize)]
    [MetaData("android.app.shortcuts", Resource = "@xml/shortcuts")]
    public class MainActivity : Activity
    {
        private FrameLayout _root;
        private BackdropView _backdrop;
        private FrameLayout _content;
        private Fab _fab;
        private NavBar _nav;

        private readonly BasePage[] _pages = new BasePage[3];
        private BasePage _currentPage;
        private int _current = -1;

        // 搜索是二级页：整层压在 tab 页与导航之上，返回即销毁
        private FrameLayout _searchLayer;
        private SearchPage _searchPage;

        private int _topInset;
        private int _bottomInset;

        // 首次启动引导（没有呼号才出现）
        private OnboardingView _onboard;
        private bool _onboardDone;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            AppTheme.Load(this);
            Dui.Init(this);
            AppTheme.Apply();
            _themeAtBuild = AppTheme.Current;
            _darkAtBuild = Dui.Dark;
            Crash.Install(this);
            try
            {
                SetupWindow();
                BuildUi();
                Store.Changed += OnStoreChanged;
                Store.Error += OnStoreError;
                Profile.Changed += OnProfileChanged;
                // 静默检查更新挪到首帧之后：启动阶段不跟渲染抢线程/网络
                _root.PostDelayed(() => Updater.SilentCheck(this), 2500);
                _root.Post(() => LateStart(savedInstanceState));
                _root.Post(() => MaybeOnboard(true));
            }
            catch (System.Exception ex)
            {
                Crash.Save(this, ex.ToString());
                var view = Crash.BuildFatalView(this, "启动失败", ex.GetType().Name + "\n\n" + ex.Message + "\n\n" + ex.StackTrace);
                SetContentView(view);
            }
        }

        /// <summary>
        /// 数据加载 + 首页构建放到首帧之后：先把壳画出来，别让用户对着白屏等
        /// （这两步合计约 150ms，主要在 JIT 和 View 树构建上）。
        /// </summary>
        private void LateStart(Bundle savedInstanceState)
        {
            if (IsFinishing || IsDestroyed) return;
            try
            {
                Store.EnsureLoaded();

                int idx = savedInstanceState != null ? savedInstanceState.GetInt("tab", 1) : 1;
                ShowPage(idx, false);

                ApplyShortcut(Intent);
            }
            catch (System.Exception ex)
            {
                Crash.Save(this, "LateStart: " + ex);
            }
        }

        protected override void OnSaveInstanceState(Bundle outState)
        {
            base.OnSaveInstanceState(outState);
            outState.PutInt("tab", Math.Max(0, _current));
        }

        protected override void OnDestroy()
        {
            Store.Changed -= OnStoreChanged;
            Store.Error -= OnStoreError;
            Profile.Changed -= OnProfileChanged;
            base.OnDestroy();
        }

        protected override void OnNewIntent(Intent intent)
        {
            base.OnNewIntent(intent);
            Intent = intent;
            ApplyShortcut(intent);
        }

        private void SetupWindow()
        {
            Window.SetSoftInputMode(SoftInput.AdjustResize);
            Window.SetStatusBarColor(Color.Transparent);
            Window.SetNavigationBarColor(Color.Transparent);

            if ((int)Build.VERSION.SdkInt >= 30)
            {
                Window.SetDecorFitsSystemWindows(false);
            }
            else
            {
                var flags = (StatusBarVisibility)0x700; // LAYOUT_STABLE | LAYOUT_FULLSCREEN | LAYOUT_HIDE_NAVIGATION
                if (!Dui.Dark)
                {
                    if ((int)Build.VERSION.SdkInt >= 23) flags |= (StatusBarVisibility)0x2000; // LIGHT_STATUS_BAR
                    if ((int)Build.VERSION.SdkInt >= 26) flags |= (StatusBarVisibility)0x10;   // LIGHT_NAVIGATION_BAR
                }
                Window.DecorView.SystemUiVisibility = flags;
            }

            if ((int)Build.VERSION.SdkInt >= 29)
            {
            }
            ApplyBarIcons();
        }

        private void ApplyBarIcons()
        {
            if ((int)Build.VERSION.SdkInt >= 30)
            {
                try
                {
                    var c = Window.InsetsController;
                    if (c != null)
                    {
                        const int LightStatus = 8;
                        const int LightNav = 16;
                        c.SetSystemBarsAppearance(Dui.Dark ? 0 : (LightStatus | LightNav), LightStatus | LightNav);
                    }
                }
                catch { }
            }
            else if ((int)Build.VERSION.SdkInt >= 23)
            {
                var flags = Window.DecorView.SystemUiVisibility;
                if (!Dui.Dark)
                {
                    flags |= (StatusBarVisibility)0x2000;
                    if ((int)Build.VERSION.SdkInt >= 26) flags |= (StatusBarVisibility)0x10;
                }
                Window.DecorView.SystemUiVisibility = flags;
            }
        }

        /// <summary>
        /// 换主题色：就地重建界面，不走 Activity.Recreate()。
        /// Recreate 会走一遍窗口销毁/重建，中间必然露一帧系统背景，看起来就是闪一下。
        /// 这里在同一个消息里换掉整棵视图树，不会被绘制打断，切换是「瞬间变色」而不是「闪一下」。
        /// </summary>
        internal void ReloadTheme()
        {
            try
            {
                int keep = _current < 0 ? 1 : _current;
                int top = _topInset, bottom = _bottomInset;

                // 重算调色板（Dui.Init 会把颜色重新读一遍，再按新主题覆盖强调色）
                Dui.Init(this);
                AppTheme.Apply();
                _themeAtBuild = AppTheme.Current;
                _darkAtBuild = Dui.Dark;
                ApplyBarIcons();

                // 旧页面里存的是旧配色，必须丢掉重建
                _pages[0] = null;
                _pages[1] = null;
                _pages[2] = null;
                _currentPage = null;
                _searchPage = null;
                _searchLayer = null;

                BuildUi();
                ShowPage(keep, false);

                // 关键：新视图树的导航栏只带 NavGap，还没算上系统栏 inset。
                // OnInsetsChanged 有「值没变就跳过」的短路，重建后值恰好没变，
                // 不强制重放一次，导航栏就会掉到手势条边上（换主题后"位置往下跑"就是这个）。
                if (top >= 0 || bottom >= 0)
                {
                    _topInset = -1;
                    _bottomInset = -1;
                    OnInsetsChanged(top, bottom);
                }

                _root.Post(() => _nav.RefreshBlur());
            }
            catch (System.Exception ex)
            {
                Crash.Save(this, "ReloadTheme: " + ex);
            }
        }

        private void BuildUi()
        {
            _root = new FrameLayout(this);
            _root.SetBackgroundColor(new Color(Dui.BgTop));

            _backdrop = new BackdropView(this);
            _root.AddView(_backdrop, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

            _content = new FrameLayout(this);
            _root.AddView(_content, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

            _fab = new Fab(this);
            var fabLp = new FrameLayout.LayoutParams(Dui.Dp(58), Dui.Dp(58))
            {
                Gravity = GravityFlags.Bottom | GravityFlags.Right,
                RightMargin = Dui.Dp(20),
                BottomMargin = NavGap + Dui.Dp(66) + Dui.Dp(14)
            };
            _root.AddView(_fab, fabLp);
            _fab.Click += (s, e) => OpenNew();

            _nav = new NavBar(this, _backdrop);
            var navLp = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dui.Dp(66))
            {
                Gravity = GravityFlags.Bottom,
                LeftMargin = Dui.Dp(16),
                RightMargin = Dui.Dp(16),
                BottomMargin = NavGap
            };
            _root.AddView(_nav, navLp);
            _nav.Selected += idx => { if (idx != _current) ShowPage(idx, true); };

            SetContentView(_root);
            _root.SetOnApplyWindowInsetsListener(new InsetsWatcher(this));
            _root.RequestApplyInsets();

            _root.PostDelayed(() => _nav.RefreshBlur(), 120);
            _root.AddOnLayoutChangeListener(new LayoutWatcher(this));
            _root.ViewTreeObserver.AddOnScrollChangedListener(new ScrollWatcher(this));
        }

        internal void OnInsetsChanged(int top, int bottom)
        {
            if (top == _topInset && bottom == _bottomInset) return;
            _topInset = top;
            _bottomInset = bottom;

            var navLp = (FrameLayout.LayoutParams)_nav.LayoutParameters;
            navLp.BottomMargin = bottom + NavGap;
            _nav.LayoutParameters = navLp;

            var fabLp = (FrameLayout.LayoutParams)_fab.LayoutParameters;
            fabLp.BottomMargin = bottom + NavGap + Dui.Dp(66) + Dui.Dp(14);
            _fab.LayoutParameters = fabLp;

            _currentPage?.ApplyInsets(_topInset, NavSpace());
            _searchPage?.ApplyInsets(_topInset, _bottomInset + Dui.Dp(18));
            _root.Post(() => _nav.RefreshBlur());
        }

        /// <summary>导航条与系统手势区之间的留白：手势条本身已经占掉 inset，这里只留一点点。</summary>
        private static int NavGap => Dui.Dp(4);

        private int NavSpace()
            => _bottomInset + NavGap + Dui.Dp(66) + Dui.Dp(18);

        private void ShowPage(int idx, bool animate)
        {
            if (idx < 0 || idx > 2) return;
            if (_current == idx && _currentPage != null && _currentPage.Parent != null) return;

            var page = _pages[idx];
            if (page == null)
            {
                page = CreatePage(idx);
                _pages[idx] = page;
            }

            if (_currentPage != null && _currentPage.Parent != null)
                _content.RemoveView(_currentPage);

            page.ApplyInsets(_topInset, NavSpace());
            page.RefreshIfStale();      // 数据没变就直接复用已建好的页面
            _content.AddView(page, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

            if (animate)
            {
                page.Alpha = 0f;
                page.Animate().Alpha(1f).SetDuration(200).Start();
            }

            _currentPage = page;
            _nav.Content = page;
            _current = idx;
            _nav.Select(idx, animate);
            // 设置页不显示新建按钮（频率 / 日志页才需要 +）
            SetFabVisible(idx == 0 || idx == 1, animate);
            _root.Post(() => _nav.RefreshBlur());
            _root.PostDelayed(() => _nav.RefreshBlur(), 320);
        }

        private BasePage CreatePage(int idx)
        {
            if (idx == 0) return new FrequencyPage(this);
            if (idx == 1) return new LogPage(this);
            var s = new SettingsPage(this);
            s.Navigate += target => ShowPage(target, true);
            return s;
        }

        private void SetFabVisible(bool visible, bool animate)
        {
            // 悬浮按钮不参与切页动画：只做淡入淡出，且不做缩放，避免"闪现"感
            _fab.ScaleX = 1f;
            _fab.ScaleY = 1f;
            if (visible)
            {
                _fab.Visibility = ViewStates.Visible;
                _fab.Alpha = 1f;
            }
            else
            {
                if (animate)
                {
                    _fab.Animate().Alpha(0f).SetDuration(140)
                        .WithEndAction(new Java.Lang.Runnable(() => { if (_current >= 2) _fab.Visibility = ViewStates.Gone; }))
                        .Start();
                }
                else
                {
                    _fab.Alpha = 0f;
                    _fab.Visibility = ViewStates.Gone;
                }
            }
        }

        /// <summary>打开搜索二级页：整层压在 tab 页与底部导航之上，从右侧轻微滑入。</summary>
        internal void OpenSearch()
        {
            if (_searchPage != null) return;

            var layer = new FrameLayout(this);
            layer.Clickable = true;
            layer.Focusable = true;
            // 导航栏与 FAB 都带 elevation，只靠 addView 顺序压不住，抬高层级一并解决点击穿透
            layer.Elevation = Dui.Dpf(40);

            // 自带一份背景，否则会透出下面 tab 页的内容
            var bg = new BackdropView(this);
            layer.AddView(bg, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

            var page = new SearchPage(this);
            page.BackRequested += CloseSearch;
            page.ApplyInsets(_topInset, _bottomInset + Dui.Dp(18));
            page.Refresh();
            layer.AddView(page, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

            _searchLayer = layer;
            _searchPage = page;
            // 二级页整屏接管：底部导航与新建按钮让位
            _fab.Visibility = ViewStates.Gone;
            _nav.Visibility = ViewStates.Invisible;
            _root.AddView(layer, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

            float from = _root.Width > 0 ? _root.Width * 0.16f : Dui.Dpf(60);
            layer.TranslationX = from;
            layer.Alpha = 0f;
            layer.Animate().TranslationX(0f).Alpha(1f).SetDuration(240)
                .SetInterpolator(new DecelerateInterpolator()).Start();
        }

        internal void CloseSearch()
        {
            var layer = _searchLayer;
            var page = _searchPage;
            _searchLayer = null;
            _searchPage = null;
            if (layer == null) return;

            try
            {
                if (page != null && page.InputView != null)
                {
                    var imm = GetSystemService(InputMethodService) as Android.Views.InputMethods.InputMethodManager;
                    imm?.HideSoftInputFromWindow(page.InputView.WindowToken, 0);
                    page.InputView.ClearFocus();
                }
            }
            catch { }

            layer.Animate().TranslationX(_root.Width > 0 ? _root.Width * 0.12f : Dui.Dpf(50))
                .Alpha(0f).SetDuration(170)
                .SetInterpolator(new AccelerateInterpolator())
                .WithEndAction(new Java.Lang.Runnable(() =>
                {
                    try { _root.RemoveView(layer); } catch { }
                    _nav.Visibility = ViewStates.Visible;
                    SetFabVisible(_current == 0 || _current == 1, false);
                    _root.Post(() => _nav.RefreshBlur());
                }))
                .Start();
        }

        /// <summary>
        /// 没有呼号就弹全屏引导。呼号一旦写入（Profile 有文件 + 备份 + prefs 三重兜底），
        /// 无论升级到哪个版本都不会再弹。
        /// </summary>
        private void MaybeOnboard(bool animate)
        {
            if (_onboardDone || _onboard != null) return;
            if (Profile.HasCall) { _onboardDone = true; return; }

            var v = new OnboardingView(this);
            v.Confirmed += call => CloseOnboard(true);
            v.Skipped += () => CloseOnboard(false);
            _onboard = v;

            _fab.Visibility = ViewStates.Gone;
            _nav.Visibility = ViewStates.Invisible;
            _root.AddView(v, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

            if (animate) v.PlayIn();
            else v.Alpha = 1f;
        }

        private void CloseOnboard(bool saved)
        {
            var v = _onboard;
            _onboard = null;
            _onboardDone = true;
            if (v == null) return;

            try { v.HideKeyboard(); } catch { }
            v.Animate().Alpha(0f).SetDuration(180)
                .WithEndAction(new Java.Lang.Runnable(() =>
                {
                    try { _root.RemoveView(v); } catch { }
                    _nav.Visibility = ViewStates.Visible;
                    SetFabVisible(_current == 0 || _current == 1, false);
                    _root.Post(() => _nav.RefreshBlur());
                }))
                .Start();

            if (saved)
                Toast.MakeText(this, "呼号已保存：" + Profile.Call, ToastLength.Short).Show();
        }

        public override void OnBackPressed()
        {
            // 引导页没有返回入口
            if (_onboard != null) return;
            if (_searchPage != null)
            {
                CloseSearch();
                return;
            }
            base.OnBackPressed();
        }

        private void OpenNew()
        {
            // 当前在哪一页新建，就打开对应的类型（0=通联频率，1=通联日志）
            OpenNew(_current == 1 ? 1 : 0);
        }

        private void OpenNew(int kind)
        {
            var intent = new Intent(this, typeof(NewRecordActivity));
            intent.PutExtra("kind", kind == 1 ? 1 : 0);
            StartActivity(intent);
            OverridePendingTransition(0, 0);
        }

        /// <summary>桌面长按快捷方式：新频率 / 新日志 → 切到对应页并直接打开新建弹层。</summary>
        private void ApplyShortcut(Intent intent)
        {
            if (intent == null) return;
            string sc;
            try { sc = intent.GetStringExtra("shortcut"); } catch { return; }
            if (string.IsNullOrEmpty(sc)) return;
            try { intent.RemoveExtra("shortcut"); } catch { }

            int target = sc == "log" ? 1 : 0;
            ShowPage(target, false);
            _root?.Post(() => OpenNew(target));
        }

        private bool _firstResume = true;
        private int _themeAtBuild = -1;
        private bool _darkAtBuild;

        protected override void OnResume()
        {
            base.OnResume();
            try
            {
                // 主题 / 深浅色没变就别重复读一遍资源（原先是每次回前台都 Init + Apply）
                if (_themeAtBuild < 0 || _themeAtBuild != AppTheme.Current || _darkAtBuild != Dui.Dark)
                {
                    Dui.Init(this);
                    AppTheme.Apply();
                    _themeAtBuild = AppTheme.Current;
                    _darkAtBuild = Dui.Dark;
                }
                ApplyBarIcons();
                // 首次进入时页面刚构建完，再刷新一次会闪一下（内容被清空重建），跳过
                if (_firstResume) _firstResume = false;
                else _currentPage?.RefreshIfStale();
                // 跳过引导后又回到前台：呼号仍为空时补弹一次（同一会话里不重复骚扰）
                if (!_onboardDone && _onboard == null && !Profile.HasCall) MaybeOnboard(true);
            }
            catch (System.Exception ex)
            {
                Crash.Save(this, "OnResume: " + ex);
            }
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);
            if (requestCode == SettingsPage.ReqExport)
                SettingsPage.HandleExportResult(this, resultCode, data);
            else if (requestCode == SettingsPage.ReqImport)
                SettingsPage.HandleImportResult(this, resultCode, data);
        }

        private void OnProfileChanged()
        {
            _root?.Post(() => _currentPage?.Refresh());
        }

        private void OnStoreChanged()
        {
            _root?.Post(() =>
            {
                _currentPage?.Refresh();
                _searchPage?.Refresh();
            });
        }

        private void OnStoreError(string msg)
        {
            _root?.Post(() => Toast.MakeText(this, msg, ToastLength.Short).Show());
        }

        internal void RelayoutBlur() => _nav?.RefreshBlur();

        private bool _blurPending;

        internal void OnScrollIdle()
        {
            if (_blurPending) return;
            _blurPending = true;
            _root.PostDelayed(() =>
            {
                _blurPending = false;
                _nav?.RefreshBlur();
            }, 240);
        }

        private class ScrollWatcher : Java.Lang.Object, ViewTreeObserver.IOnScrollChangedListener
        {
            private readonly MainActivity _a;
            public ScrollWatcher(MainActivity a) { _a = a; }
            public void OnScrollChanged() => _a.OnScrollIdle();
        }

        internal int StatusFallback()
        {
            var id = Resources.GetIdentifier("status_bar_height", "dimen", "android");
            return id > 0 ? Resources.GetDimensionPixelSize(id) : Dui.Dp(24);
        }

        private class LayoutWatcher : Java.Lang.Object, View.IOnLayoutChangeListener
        {
            private readonly MainActivity _a;
            public LayoutWatcher(MainActivity a) { _a = a; }
            public void OnLayoutChange(View v, int l, int t, int r, int b, int ol, int ot, int or, int ob)
                => _a.RelayoutBlur();
        }

        private class InsetsWatcher : Java.Lang.Object, View.IOnApplyWindowInsetsListener
        {
            private readonly MainActivity _a;
            public InsetsWatcher(MainActivity a) { _a = a; }

            public WindowInsets OnApplyWindowInsets(View v, WindowInsets insets)
            {
                int top = 0, bottom = 0;
                try
                {
                    if ((int)Build.VERSION.SdkInt >= 30)
                    {
                        var sb = insets.GetInsets(7);       // statusBars | navigationBars | captionBar
                        var cut = insets.GetInsets(128);    // displayCutout，避免被刘海裁切
                        top = Math.Max(sb.Top, cut.Top);
                        bottom = sb.Bottom;
                    }
                    else
                    {
#pragma warning disable CS0618
                        top = insets.SystemWindowInsetTop;
                        bottom = insets.SystemWindowInsetBottom;
#pragma warning restore CS0618
                    }
                }
                catch { }
                if (top <= 0) top = _a.StatusFallback();
                _a.OnInsetsChanged(top, bottom);
                return insets;
            }
        }
    }
}
