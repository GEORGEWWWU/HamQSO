# HamQSO 快速编译 + 真机验证指南（给 agent 看）

目标：**改一行代码 → 装机 → 看到界面 ≤ 60 秒**。做不到就是姿势错了，不是机器慢。

实测数据（本机，2026-11-02）：

| 动作 | 耗时 |
|---|---|
| 增量构建（无改动） | 8 s |
| 增量构建（改 1 个 .cs） | 20–40 s |
| `adb install -r -d` | 6 s |
| `am start -W` 冷启 | 0.3 s |
| `screencap` 一张 | 1 s |

> 如果构建是「好几分钟」，99% 是你做了全量重建（删 obj/bin、`dotnet clean`、`dotnet publish`、改了 csproj）。

---

## 1. 环境（一次性）

- SDK：`I:/AndroidDev/sdk`（adb 在 `platform-tools/adb.exe`）
- JDK：`I:/AndroidDev/jdk/jdk-17.0.21.0.2+1`
- 目标机：`PLJ110`（Android 17 / API 36），1080×2374，density 覆盖 480（1dp = 3px）
- 包名 `com.hamji.app`，入口 `com.hamji.app/.MainActivity`

**必须导出这 6 个环境变量**，否则报 `XA5300 找不到 Android SDK`（假故障，SDK 路径是对的）：

```bash
export ProgramData="C://ProgramData" ALLUSERSPROFILE="C://ProgramData" \
       APPDATA="C://Users//Administrator//AppData//Roaming" \
       ProgramFiles="C://Program Files" \
       ComSpec="C://Windows//system32//cmd.exe" \
       PATHEXT=".COM;.EXE;.BAT;.CMD"
```

根因：`Xamarin.Android.Tools` 靠 `PATHEXT` 枚举可执行扩展名，缺了它 `FindExecutablesInDirectory(platform-tools,"adb")` 返回空 → SDK 校验永远 False。
同理 `dotnet workload list/repair` 也会因缺变量抛 `InstallerBase` 类型初始化异常——同一个病。

---

## 2. 唯一的构建命令

```bash
dotnet build -c Release -v:m \
  -p:AndroidSdkDirectory=I:/AndroidDev/sdk \
  -p:JavaSdkDirectory=I:/AndroidDev/jdk/jdk-17.0.21.0.2+1
```

输出 APK（唯一要装的那个，**必须 `-Signed`**）：
`bin/Release/net10.0-android/com.hamji.app-Signed.apk`

**不要做的事：**

| 禁止 | 原因 |
|---|---|
| `rm -rf obj bin` / `dotnet clean` | 这是「几分钟」的唯一来源。只有**改 csproj/manifest/资源 id 类大改**才需要，平时绝不 |
| `dotnet publish -c Release` | 多走一套 RID 打包流程，慢且无必要 |
| `-c Debug` | 本项目 Debug 会报 `XAWAS7023`（WrapAssembliesAsSharedLibraries 读不到引用程序集）。要验证就用 Release |
| 连跑 2 次 build「确认一下」 | 第二次 8 s 但纯浪费；看 `0 个错误` 就够了 |
| 后台开 build 再轮询 | 直接前台等，输出用 `grep` 收窄 |

输出过滤（避免被 120 条 CA1416 警告刷屏、也避免误判成功）：

```bash
... 2>&1 | grep -E "error|个错误|已用时间" | tail -20
```

---

## 3. 装机：只准 `-r -d`

```bash
I:/AndroidDev/sdk/platform-tools/adb.exe install -r -d bin/Release/net10.0-android/com.hamji.app-Signed.apk
```

- `-r` 覆盖安装、`-d` 允许降 versionCode。
- **绝对禁止 `adb uninstall`**：会连 `/data/data/com.hamji.app` 一起删，用户的 records.json 直接没了（2026-10-31 已经踩过一次，用户明确骂过）。
- 装完必须能读到 `dumpsys package com.hamji.app | grep versionName`，确认装的是新包。

---

## 4. 启动 + 截图 + 看日志（三连）

```bash
ADB=I:/AndroidDev/sdk/platform-tools/adb.exe
$ADB shell am force-stop com.hamji.app
$ADB shell am start -W -n com.hamji.app/.MainActivity      # 看 TotalTime / LaunchState
$ADB exec-out screencap -p > shots/_shot.png               # 别用 screencap /sdcard + pull，慢一倍
$ADB logcat -d -t 500 | grep -iE "hamji|AndroidRuntime|FATAL"
```

- `am start -W` 直接给出冷启毫秒数，比肉眼估快。
- **禁止把测试文件写到手机用户目录**（用户 2026-11-04 明确禁止）：截图一律
  `exec-out screencap -p > shots/x.png`（本地管道，手机上不落文件）；`uiautomator dump`
  只写 `/data/local/tmp/ui.xml`，看完 `rm -f`。`/sdcard`、`Pictures`、`DCIM`、`Download` 会进相册和
  图片选择器，污染用户设备。收尾 `adb shell ls /sdcard | grep -Ei "\.png|\.xml"` 必须为空。
- **不要用 `uiautomator dump` 判崩溃**：进程死了会给你上一次的残树。判存活用
  `adb shell pidof com.hamji.app`，判崩溃用 logcat 里的 `FATAL EXCEPTION` / `mono-rt`。
- 截图后看图的顺序：先看有没有白屏/半透明遮罩残留，再看间距对齐，最后看字号颜色。

---

## 5. 坐标从哪来

- **原生控件**（设置页行、系统对话框）：`adb shell uiautomator dump` 有 `text` + `bounds`，可靠。
- **自绘 View**（卡片、导航 pill、滑块）：dump 里只有 `android.view.View` 的 bounds，没有文字，只能靠
  `bounds` 的几何或截图量。
- 换算：`px / 3 = dp`（density 480）。
- 点击失败先怀疑 ColorOS 拦截 adb 注入（`input tap` 无反应），换真实手指/让用户点，别死磕。

**输入文字的坑**：`adb shell input text` 不会直接进 EditText（微信输入法先给悬浮候选词）。
验证输入类功能时，点软键盘按键（坐标从截图量），或直接跳过、让用户手测。

---

## 6. 常见报错对照

| 报错 | 真因 | 处理 |
|---|---|---|
| `XA5300 找不到 Android SDK 目录` | 缺 `PATHEXT` | 补环境变量，别改 SDK 路径 |
| `XAWAS7023` | 用了 Debug 配置 | 换 `-c Release` |
| `APT2259 incompatible with attribute` | 资源里写了中文字面量（如 shortcutShortLabel） | 改 `@string/...` 引用 |
| `InstallerBase` 类型初始化异常 | 缺环境变量 | 同上 |
| 改了版本号但装上去还是旧的 | obj 缓存 | 这一次才值得全量：python `shutil.rmtree('obj'); shutil.rmtree('bin')` 后重建（`rm -rf` 会触发批量删除保护） |

---

## 7. 一轮完整循环（复制即用）

```bash
cd I:/A_APK/HamQSO
export ProgramData="C://ProgramData" ALLUSERSPROFILE="C://ProgramData" \
       APPDATA="C://Users//Administrator//AppData//Roaming" \
       ProgramFiles="C://Program Files" \
       ComSpec="C://Windows//system32//cmd.exe" PATHEXT=".COM;.EXE;.BAT;.CMD"
ADB=I:/AndroidDev/sdk/platform-tools/adb.exe

dotnet build -c Release -v:m \
  -p:AndroidSdkDirectory=I:/AndroidDev/sdk \
  -p:JavaSdkDirectory=I:/AndroidDev/jdk/jdk-17.0.21.0.2+1 2>&1 \
  | grep -E "error|个错误|已用时间" | tail -5

$ADB install -r -d bin/Release/net10.0-android/com.hamji.app-Signed.apk
$ADB shell am force-stop com.hamji.app
$ADB shell am start -W -n com.hamji.app/.MainActivity | grep -E "TotalTime|Status"
sleep 1 && $ADB exec-out screencap -p > shots/_shot.png
```

---

## 8. 纪律

- 版本号（`ApplicationDisplayVersion` / `ApplicationVersion`）**只有用户明确要求才改**；不为「出包」顺手 bump。
- 每完成一步就 commit + push 一次，别攒到最后。
- 不删用户数据：不 uninstall、不 `pm clear`。要试破坏性流程，先让用户用应用内「导出」备份。
