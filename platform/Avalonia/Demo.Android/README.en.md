# Demo.Android

Android Avalonia demo project.

## Requirements

- .NET 8.0 SDK
- .NET Android workload
- Android SDK (API 21+)
- Java 11+

## Project Configuration

| Property | Value |
|----------|-------|
| Target Framework | net8.0-android |
| Minimum Android Version | API 21 |
| Application ID | com.qiplat.sweeteditor.avalonia.demo.android |

## Quick Start

### Install workload

```bash
dotnet workload install android
```

### Build

```bash
cd platform/Avalonia
dotnet build Demo.Android -c Debug -f net8.0-android
```

### Run

```bash
adb install -r Demo.Android/bin/Debug/net8.0-android/android-arm64/com.qiplat.sweeteditor.avalonia.demo.android-Signed.apk
adb shell am start -n com.qiplat.sweeteditor.avalonia.demo.android/com.qiplat.sweeteditor.avalonia.demo.android.MainActivity
```

## Native Library Dependencies

The project references native libraries from `prebuilt/android/` directory:

| Architecture | Library Files |
|--------------|---------------|
| arm64-v8a | `libsweeteditor.so`, `libsweetline.so` |
| x86_64 | `libsweeteditor.so`, `libsweetline.so` |

## Architecture

- Shares `Demo.Shared` demo logic with desktop
- Injects Android IME visible area adaptation via `DemoPlatformServices`
- Platform differences isolated to platform service layer

## Termux Build

For building in Termux environment, see [termux-dotnet-android-build.en.md](./termux-dotnet-android-build.en.md).
