# Avalonia Demo Projects

## Project Structure

```
platform/Avalonia/
├── SweetEditor/          # Core control library
├── Demo.Shared/          # Shared demo logic and resources
├── Demo.Desktop/         # Desktop demo (Windows/Linux/macOS)
├── Demo.Android/         # Android demo
├── Demo.iOS/             # iOS demo
└── Demo.Mac/             # macOS native demo
```

## Platform Support

| Platform | Project | Target Framework | Status |
|----------|---------|------------------|--------|
| Windows | Demo.Desktop | net8.0 | ✅ Supported |
| Linux | Demo.Desktop | net8.0 | ✅ Supported |
| macOS | Demo.Desktop | net8.0 | ✅ Supported |
| Android | Demo.Android | net8.0-android | ✅ Supported |
| iOS | Demo.iOS | net8.0-ios | ✅ Supported |
| macOS (Native) | Demo.Mac | net8.0-macos | ✅ Supported |

## Quick Start

### Desktop

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

## Resource References

Demo.Shared directly references shared resources from `platform/_res/` directory via csproj:

```xml
<EmbeddedResource Include="../../_res/files/*.*">
  <LogicalName>SweetEditor.PlatformRes.files.%(Filename)%(Extension)</LogicalName>
</EmbeddedResource>
<EmbeddedResource Include="../../_res/syntaxes/*.json">
  <LogicalName>SweetEditor.PlatformRes.syntaxes.%(Filename)%(Extension)</LogicalName>
</EmbeddedResource>
```

## Native Library Dependencies

All platforms use native libraries from the unified `prebuilt/` directory:

| Platform | Library File |
|----------|--------------|
| Linux x64 | `prebuilt/linux/x86_64/libsweetline.so` |
| Windows x64 | `prebuilt/windows/x64/sweetline.dll` |
| macOS x86_64 | `prebuilt/osx/x86_64/libsweetline.dylib` |
| macOS arm64 | `prebuilt/osx/arm64/libsweetline.dylib` |
| Android arm64 | `prebuilt/android/arm64-v8a/libsweetline.so` |
| Android x86_64 (Emulator) | `prebuilt/android/x86_64/libsweetline.so` |
| iOS arm64 | `prebuilt/ios/arm64/libsweetline.a` |
| iOS simulator | `prebuilt/ios/x86_64/libsweetline.a` |

## Design Goals

- Unified entry through `SweetEditorControl` / `SweetEditorController`
- Coverage: decorations / completion / inline suggestion / snippet / selection menu / new line action / perf overlay / keymap / large document switching
- Platform differences isolated to platform service layer only

## Performance Optimization

Demo includes the following performance optimization components:

- **LruCache**: LRU cache implementation
- **FrameRateMonitor**: Real-time frame rate monitoring
- **GlyphRunCache**: Glyph run caching optimization
- **RenderOptimizer**: Dirty region rendering optimization
- **RenderBufferPool**: Array pooling to reduce GC pressure

See [PERFORMANCE_OPTIMIZATION_REPORT.md](./PERFORMANCE_OPTIMIZATION_REPORT.md) for details.
