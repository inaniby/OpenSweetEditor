# Demo.Android

Android 端 Avalonia 示例工程。

## 环境要求

- .NET 8.0 SDK
- .NET Android workload
- Android SDK (API 21+)
- Java 11+

## 项目配置

| 属性 | 值 |
|------|-----|
| 目标框架 | net8.0-android |
| 最低 Android 版本 | API 21 |
| 应用 ID | com.qiplat.sweeteditor.avalonia.demo.android |

## 快速开始

### 安装 workload

```bash
dotnet workload install android
```

### 编译

```bash
cd platform/Avalonia
dotnet build Demo.Android -c Debug -f net8.0-android
```

### 运行

```bash
adb install -r Demo.Android/bin/Debug/net8.0-android/android-arm64/com.qiplat.sweeteditor.avalonia.demo.android-Signed.apk
adb shell am start -n com.qiplat.sweeteditor.avalonia.demo.android/com.qiplat.sweeteditor.avalonia.demo.android.MainActivity
```

## 原生库依赖

项目引用 `prebuilt/android/` 目录下的原生库：

| 架构 | 库文件 |
|------|--------|
| arm64-v8a | `libsweeteditor.so`, `libsweetline.so` |
| x86_64 | `libsweeteditor.so`, `libsweetline.so` |

## 架构说明

- 与桌面端共用 `Demo.Shared` 示例逻辑
- 通过 `DemoPlatformServices` 注入 Android IME 可视区域适配
- 平台差异仅收口到平台服务层

## Termux 编译

如需在 Termux 环境下编译，请参考 [termux-dotnet-android-build.md](./termux-dotnet-android-build.md)。
