# Avalonia Demo 项目

## 项目结构

```
platform/Avalonia/
├── SweetEditor/          # 核心控件库
├── Demo.Shared/          # 共享示例逻辑与资源
├── Demo.Desktop/         # 桌面端示例 (Windows/Linux/macOS)
├── Demo.Android/         # Android 端示例
├── Demo.iOS/             # iOS 端示例
└── Demo.Mac/             # macOS 原生示例
```

## 平台支持

| 平台 | 项目 | 目标框架 | 状态 |
|------|------|----------|------|
| Windows | Demo.Desktop | net8.0 | ✅ 支持 |
| Linux | Demo.Desktop | net8.0 | ✅ 支持 |
| macOS | Demo.Desktop | net8.0 | ✅ 支持 |
| Android | Demo.Android | net8.0-android | ✅ 支持 |
| iOS | Demo.iOS | net8.0-ios | ✅ 支持 |
| macOS (原生) | Demo.Mac | net8.0-macos | ✅ 支持 |

## 快速开始

### 桌面端

```bash
cd platform/Avalonia
dotnet run --project Demo.Desktop
```

### Android

```bash
cd platform/Avalonia
dotnet build Demo.Android -c Release -f net8.0-android
```

### iOS

```bash
cd platform/Avalonia
dotnet build Demo.iOS -c Release -f net8.0-ios
```

## 资源引用

Demo.Shared 通过 csproj 直接引用 `platform/_res/` 目录下的共享资源：

```xml
<EmbeddedResource Include="../../_res/files/*.*">
  <LogicalName>SweetEditor.PlatformRes.files.%(Filename)%(Extension)</LogicalName>
</EmbeddedResource>
<EmbeddedResource Include="../../_res/syntaxes/*.json">
  <LogicalName>SweetEditor.PlatformRes.syntaxes.%(Filename)%(Extension)</LogicalName>
</EmbeddedResource>
```

## 原生库依赖

所有平台统一使用 `prebuilt/` 目录下的原生库：

| 平台 | 库文件 |
|------|--------|
| Linux x64 | `prebuilt/linux/x86_64/libsweetline.so` |
| Windows x64 | `prebuilt/windows/x64/sweetline.dll` |
| macOS x86_64 | `prebuilt/osx/x86_64/libsweetline.dylib` |
| macOS arm64 | `prebuilt/osx/arm64/libsweetline.dylib` |
| Android arm64 | `prebuilt/android/arm64-v8a/libsweetline.so` |
| Android x86_64 (模拟器) | `prebuilt/android/x86_64/libsweetline.so` |
| iOS arm64 | `prebuilt/ios/arm64/libsweetline.a` |
| iOS simulator | `prebuilt/ios/x86_64/libsweetline.a` |

## 设计目标

- 统一走 `SweetEditorControl` / `SweetEditorController`
- 覆盖 decorations / completion / inline suggestion / snippet / selection menu / new line action / perf overlay / keymap / 大文档切换
- 平台差异仅收口到平台服务层

## 性能优化

Demo 包含以下性能优化组件：

- **LruCache**: LRU 缓存实现
- **FrameRateMonitor**: 实时帧率监控
- **GlyphRunCache**: 字形缓存优化
- **RenderOptimizer**: 脏区域渲染优化
- **RenderBufferPool**: 数组池化减少 GC

详见 [PERFORMANCE_OPTIMIZATION_REPORT.md](./PERFORMANCE_OPTIMIZATION_REPORT.md)
