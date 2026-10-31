# Ham记 · 业余无线电日记

一个 UI 现代、体积很小、完全离线的安卓通联记录 App。用 C# / .NET 10 for Android 原生 View 编写，**不使用 MAUI、不使用任何 AndroidX 依赖**，因此 APK 只有约 6 MB。

## 产物

```
bin/Release/net10.0-android/com.hamji.app-Signed.apk
```

- minSdk 21（Android 5.0） / targetSdk 36（Android 16）
- ABI：arm64-v8a + armeabi-v7a（单个 APK，双 ABI）
- 权限：**0 个**（不联网、不读存储、不申请任何权限）
- 数据落盘：`/data/data/com.hamji.app/files/records.json`，原子写入

## 变更记录

### v1.0.3（真机通过）
OPPO Find X9 / ColorOS 17 / Android 17 / 16KB 页实测：冷启动 307ms，无 FATAL。

三个独立闪退原因（逐层剥开）：
1. `dlopen failed: cannot locate symbol "compressed_assembly_count"` —— 关掉 `AndroidEnableAssemblyCompression`
   后生成的 `libxamarin-app.so` 不再导出该符号，但预编译的 `libmonodroid.so` 仍引用它。**这两个开关别手动改**，用 SDK 默认。
2. `IllegalArgumentException: Invalid ID: 60` @ `ColorSpace.get` ← `LinearGradient` ——
   绑定里 **没有** `(float,float,float,float,int,int,TileMode)` 重载，只有 `Color/Color` 与 `Int64/Int64`；
   传 int 会命中 Int64 版本，Android 17 把 long 当 wide-gamut 颜色 → 崩。**必须传 `new Color(argb)`**。
3. `No implementation found for MainActivity.n_onCreate` ——
   切换构建属性后增量构建，JNI typemap 与 `libxamarin-app.so` 不一致。**改 csproj/清单后必须 `rm -rf obj bin`**。

同时 `android:allowBackup` 改为 `false`（纯本地应用，避免云备份回灌数据）。

### v1.0.1（修复启动闪退·初版）
真机（OPPO Find X9 / ColorOS 17 / Android 17）安装后秒退、界面不显示，原因是打包方式：

- v1.0.0 的 APK 里 `lib/*/**.so` 是 **deflate 压缩**，同时 manifest 是 **`extractNativeLibs=false`**（targetSdk≥23 默认值）。
  这种组合下系统会直接从 APK 里 mmap `.so`，压缩条目无法映射 → `dlopen` 失败 → 进程秒退。
- 修复：`AndroidUseLegacyPackaging=true`（manifest 写入 `extractNativeLibs=true`，安装时解压原生库）
  + `AndroidEnableAssemblyCompression=false`。已核对新包 manifest 为 `extractNativeLibs=true`。
- 同时把 `TrimMode` 从 `link`（全裁剪）改为 `partial`，避免 Release 裁剪引发的启动期异常。
- 增加启动兜底：托管异常不再直接闪退，而是渲染一页可复制的错误详情（并写入 `files/crash.log`）。

### v1.0.0
首个版本。

## 功能

| Tab | 内容 |
| --- | --- |
| 通联频率 | 按天分组的频点记录（频率 / 模式 / 接收时间 / 备注），最新在前 |
| 通联日志 | 按天分组的通联记录（呼号 / 频率 / 模式 / 时间 / RST / QTH / 设备 / 功率 / 天线 / 备注） |
| 设置 | 大头像 + 记录概览 + 导出 JSON 备份 + 清空数据 + 版本信息 |

- 右下角浮动 + 按钮为频率页/日志页共用，进入设置页自动收起
- 新建页为底部抽屉：先选「通联频率 / 通联日志」，再展开对应表单；未保存退出即丢弃
- 列表项：点击查看详情、长按删除（带二次确认）
- 深色/浅色自动跟随系统，主色蓝 `#1F6BFF`（深色 `#5B9CFF`）
- 亚克力：背景渐变 + 柔光；底部导航栏为悬浮胶囊，对当前页面做降采样 + 分离式盒式模糊（纯 C# 实现，Android 5.0 起表现一致，不依赖 RenderEffect）
- 沉浸式：`setDecorFitsSystemWindows(false)`（API 30+）/ `LAYOUT_FULLSCREEN`（API 21~29），状态栏与手势条全透明、无黑边；列表内容延伸到底部导航栏下方
- Android 12+ 系统强制启动画面，已把 `windowSplashScreenAnimatedIcon` 换成透明 drawable 并把背景色设为与 App 一致，视觉上直接进入首页

## 构建

需要 .NET 10 SDK + android workload、JDK 17、Android SDK Platform 36。

```bash
dotnet workload install android

export ANDROID_HOME=/c/android-dev/sdk
export JAVA_HOME=/c/android-dev/jdk/jdk-17.0.21.0.2+1
dotnet build HamJi.csproj -c Release \
  -p:AndroidSdkDirectory=C:/android-dev/sdk \
  -p:JavaSdkDirectory=C:/android-dev/jdk/jdk-17.0.21.0.2+1
```

安装到设备：

```bash
adb install -r bin/Release/net10.0-android/com.hamji.app-Signed.apk
```

> Release 包默认用 debug keystore 签名，方便直接安装体验；正式发布请改成自己的 keystore（`AndroidKeyStore=true` + `AndroidSigningKeyStore` 等属性）。

## 体积优化点

- 不引入 MAUI / AndroidX / Material，界面全部用平台 View + 自绘 drawable
- `PublishTrimmed=true`、`TrimMode=link`、`AndroidLinkMode=Full`、`AndroidLinkTool=r8`
- 关闭 AOT / Profiled AOT / Debugger / EventSource
- `AndroidUseAssemblyStore=true` + `AndroidEnableAssemblyCompression=true`（托管程序集压缩进 `libassembly-store.so`）
- 只打包 arm64 + arm 两个 ABI（模拟器用的 x86/x64 不打包）
- 图标全部为矢量，无位图资源
- 图标：应用图标取自 Material Design Icons 的 `radio-tower`（Apache-2.0，临时占位，可随时替换）
- 注意：`.so` 必须**不要**压缩 + `extractNativeLibs=false` 同时出现，否则 Android 15+ / 新机型上会启动即崩

想更小可以只保留 `android-arm64`（约 4 MB）。

## 目录

```
Core/     Dui(设计系统/颜色/可绘制)、Model、Store(JSON 本地存储)
Views/    Backdrop/GlassPanel/ImgBlur、NavBar、Fab、Widgets(卡片/行/空状态)
Pages/    BasePage(分组列表)、ListPages(频率/日志)、SettingsPage
MainActivity.cs          三个 Tab 的宿主、沉浸式、Insets、亚克力刷新
NewRecordActivity.cs     底部抽屉式新建页（分段选择器 / 模式选择 / 日期时间）
```
