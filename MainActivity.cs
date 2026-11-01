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

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            AppTheme.Load(this);
            Dui.Init(this);
            AppTheme.Apply();
            Crash.Install(this);
            try
            {
                SetupWindow();
                BuildUi();
                Store.EnsureLoaded();
                Store.Changed += OnStoreChanged;
                Store.Error += OnStoreError;
                Updater.SilentCheck(this);   // 静默检查更新：有新版才打扰
                if (savedInstanceState != null)
                {
                    var idx = savedInstanceState.GetInt("tab", 1);
                    ShowPage(idx, false);
                }
                else
                {
                    // 默认落在通联日志页
                    ShowPage(1, false);
                }
                ApplyShortcut(Intent);
            }
            catch (System.Exception ex)
            {
                Crash.Save(this, ex.ToString());
                var view = Crash.BuildFatalView(this, "启动失败", ex.GetType().Name + "\n\n" + ex.Message + "\n\n" + ex.StackTrace);
                SetContentView(view);
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
                BottomMargin = Dui.Dp(86)
            };
            _root.AddView(_fab, fabLp);
            _fab.Click += (s, e) => OpenNew();

            _nav = new NavBar(this, _backdrop);
            var navLp = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dui.Dp(66))
            {
                Gravity = GravityFlags.Bottom,
                LeftMargin = Dui.Dp(16),
                RightMargin = Dui.Dp(16),
                BottomMargin = Dui.Dp(12)
            };
            _root.AddView(_nav, navLp);
            _nav.Selected += idx => { if (idx != _current) ShowPage(idx, true); };

            SetContentView(_root);
            _root.SetOnApplyWindowInsetsListener(new InsetsWatcher(this));
            _root.RequestApplyInsets();

            _root.Post(() => _nav.RefreshBlur());
            _root.AddOnLayoutChangeListener(new LayoutWatcher(this));
            _root.ViewTreeObserver.AddOnScrollChangedListener(new ScrollWatcher(this));
        }

        internal void OnInsetsChanged(int top, int bottom)
        {
            if (top == _topInset && bottom == _bottomInset) return;
            _topInset = top;
            _bottomInset = bottom;

            var navLp = (FrameLayout.LayoutParams)_nav.LayoutParameters;
            navLp.BottomMargin = bottom + Dui.Dp(10);
            _nav.LayoutParameters = navLp;

            var fabLp = (FrameLayout.LayoutParams)_fab.LayoutParameters;
            fabLp.BottomMargin = bottom + Dui.Dp(10) + Dui.Dp(66) + Dui.Dp(14);
            _fab.LayoutParameters = fabLp;

            _currentPage?.ApplyInsets(_topInset, NavSpace());
            _searchPage?.ApplyInsets(_topInset, _bottomInset + Dui.Dp(18));
            _root.Post(() => _nav.RefreshBlur());
        }

        private int NavSpace()
            => _bottomInset + Dui.Dp(66) + Dui.Dp(28);

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
            page.Refresh();
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

        public override void OnBackPressed()
        {
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

        protected override void OnResume()
        {
            base.OnResume();
            try
            {
                Dui.Init(this);
                AppTheme.Apply();
                ApplyBarIcons();
                // 首次进入时页面刚构建完，再刷新一次会闪一下（内容被清空重建），跳过
                if (_firstResume) _firstResume = false;
                else _currentPage?.Refresh();
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
