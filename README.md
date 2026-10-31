# Ham记 · 业余无线电日记

<div align="center">

<img src="assets/logo-512.png" alt="Ham记" width="180" />

<h1>Ham记</h1>

<p><b>业余无线电通联日记 · 完全离线 · 零权限 · 约 6 MB</b></p>

<p>用 C# / .NET 10 for Android 原生 View 写的通联记录本。<br/>
不用 MAUI，不引任何 AndroidX 依赖，不联网、不申请权限，数据只存在你自己手机里。</p>

<p>
<a href="https://github.com/GEORGEWWWU/HamQSO/releases/latest"><img src="https://img.shields.io/badge/下载-APK-1F6BFF?style=for-the-badge&logo=android&logoColor=white" alt="下载 APK" /></a>
<a href="https://github.com/GEORGEWWWU/HamQSO/releases/latest"><img src="https://img.shields.io/badge/版本-v1.0.0-0B44C9?style=for-the-badge" alt="版本" /></a>
<a href="#快速开始"><img src="https://img.shields.io/badge/平台-Android%205.0+-17924F?style=for-the-badge" alt="平台" /></a>
<a href="LICENSE"><img src="https://img.shields.io/badge/许可-MIT-6D4AD8?style=for-the-badge" alt="许可" /></a>
</p>

<p>
<a href="#功能">功能</a> ·
<a href="#截图">截图</a> ·
<a href="#快速开始">快速开始</a> ·
<a href="#构建">构建</a> ·
<a href="#数据存储">数据存储</a> ·
<a href="#项目结构">项目结构</a>
</p>

</div>

---

## 功能

<div align="center">

| 记录 | 管理 | 个性化 |
|:---:|:---:|:---:|
| 频点 / 通联日志两类 | 搜索 · 排序 · 筛选 | 7 套主题色 |
| 上下行频率、CTCSS、哑音 | 长按编辑 · 左滑删除 | 明暗跟随系统 |
| 呼号、RST、QTH、设备、功率、天线 | 下拉刷新 · 空态引导 | 纯色无渐变 |
| 备注、时间戳 | JSON 导入 / 导出 | 无广告无统计 |

</div>

- **两类记录**：通联频率（频点，含上下行、CTCSS、哑音、模式）与通联日志（QSO，含呼号、RST、QTH、设备、功率、天线、备注）。
- **输入实时约束**：频率/功率只接受数字与一个小数点；呼号只接受字母、数字与 `/` 并自动大写；中文输入法的全角字符自动折半角。
- **编辑时锁类型**：已保存的记录不能从日志改成频点，反之亦然。
- **完全离线**：0 权限、0 网络请求、0 第三方 SDK。

## 截图

<div align="center">
<img src="assets/screens/home.png" width="30%" alt="首页" />
<img src="assets/screens/log.png" width="30%" alt="日志" />
<img src="assets/screens/settings.png" width="30%" alt="设置" />
</div>

## 快速开始

```
https://github.com/GEORGEWWWU/HamQSO/releases/latest
```

下载最新 APK，手机上允许「未知来源」后安装即可。首次打开会生成两条示例记录，长按可编辑，左滑可删除。

## 构建

```bash
dotnet build HamJi.csproj -c Release \
  -p:AndroidSdkDirectory=<sdk> \
  -p:JavaSdkDirectory=<jdk17>
```

产物：`bin/Release/net10.0-android/com.hamji.app-Signed.apk`

- minSdk 21（Android 5.0） / targetSdk 36（Android 16）
- ABI：arm64-v8a + armeabi-v7a 单包
- 需要 JDK 17 与 Android SDK（.NET 10 for Android 工作负载）

## 数据存储

记录写在 `/data/data/com.hamji.app/files/records.json`，**原子写入**（先写临时文件再 rename），断电也不会写坏。
设置页可导出 / 导入 JSON 做备份。

## 项目结构

```
Core/        Dui（UI 工具箱）、Store（数据）、AppTheme、Filters（输入约束）
Pages/       首页各分页（频点、日志、设置）
Views/       列表卡片、分段控件、小组件
Resources/   纯手写 drawable / values，不用 AndroidX
NewRecordActivity.cs  底部弹层式新建 / 编辑页
```

## 变更记录

### v1.0.0

首个正式版。包含：两类记录、实时输入约束、编辑锁类型、7 套主题色、导入导出、明暗跟随系统。

<div align="center">

<sub>用 .NET 10 for Android 手写 · 为业余无线电爱好者</sub>

</div>
