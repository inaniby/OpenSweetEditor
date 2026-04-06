# Termux 编译 .NET Android 流程（含环境与工具链）

最后更新：2026-04-01
适用环境：Termux（aarch64，Android）

## 0. 当前机器已验证环境

以下版本来自当前 Termux 实机环境（可作为基线）：

```bash
dotnet --info
# .NET SDK 9.0.115
# Workload: android (35.0.105)

java -version
# openjdk version "21.0.10"

adb version
# Android Debug Bridge 1.0.41 (35.0.2)
```

当前已验证路径：

```bash
DOTNET_ROOT=/data/data/com.termux/files/usr/lib/dotnet
JAVA_HOME=/data/data/com.termux/files/usr/lib/jvm/java-21-openjdk/
ANDROID_SDK_ROOT=/data/data/com.termux/files/home/android-sdk
AAPT2=/data/data/com.termux/files/usr/bin/aapt2
ZIPALIGN=/data/data/com.termux/files/usr/bin/zipalign
APKSIGNER=/data/data/com.termux/files/usr/bin/apksigner
ADB=/data/data/com.termux/files/usr/bin/adb
```

## 1. 基础环境准备

先更新仓库并安装基础工具：

```bash
pkg update && pkg upgrade -y
pkg install -y git curl wget unzip clang cmake ninja make
pkg install -y android-tools aapt aapt2 apksigner
pkg install -y openjdk-21
pkg install -y dotnet9.0 dotnet-sdk-9.0
```

可选检查：

```bash
which dotnet java adb aapt2 zipalign apksigner
```

## 2. Android SDK 安装与配置

如果你还没有 SDK 目录，按下面执行。

```bash
mkdir -p $HOME/android-sdk
```

下载 Android Command-line Tools（建议到 Android 官方页面取最新链接；下面是通用模板）：

```bash
cd $HOME
wget -O cmdline-tools.zip "https://dl.google.com/android/repository/commandlinetools-linux-<VERSION>_latest.zip"
mkdir -p android-sdk/cmdline-tools
unzip -q cmdline-tools.zip -d android-sdk/cmdline-tools
mv android-sdk/cmdline-tools/cmdline-tools android-sdk/cmdline-tools/latest
```

配置环境变量（写入 `~/.bashrc` 或 `~/.zshrc`）：

```bash
export ANDROID_SDK_ROOT=$HOME/android-sdk
export ANDROID_HOME=$ANDROID_SDK_ROOT
export JAVA_HOME=$PREFIX/lib/jvm/java-21-openjdk
export DOTNET_ROOT=$PREFIX/lib/dotnet
export PATH=$ANDROID_SDK_ROOT/cmdline-tools/latest/bin:$ANDROID_SDK_ROOT/platform-tools:$PATH
```

立即生效：

```bash
source ~/.bashrc 2>/dev/null || true
source ~/.zshrc 2>/dev/null || true
```

安装 SDK 组件并接受 license：

```bash
yes | sdkmanager --sdk_root=$ANDROID_SDK_ROOT --licenses
sdkmanager --sdk_root=$ANDROID_SDK_ROOT \
  "platform-tools" \
  "platforms;android-34" \
  "build-tools;34.0.0"
```

termux的aapt2貌似不支持android-35

## 3. 安装 .NET Android workload

```bash
dotnet workload install android
dotnet workload list
```
若workload没有android，可以下载官方workload清单然后手动注入来解决。
如果 workload 异常，可修复：

```bash
dotnet workload repair
```

## 4. 项目编译命令（Termux 实测稳定模板）

以下命令是 Termux 中稳定可用的模板，重点是显式指定 SDK 路径与 aapt2/zipalign 工具路径。
建议 Debug/Release 都使用同一套参数，避免 AOT 与工具链自动探测差异导致的环境问题。

```bash
dotnet build <YOUR_ANDROID_CSPROJ> \
  -c Debug \
  -f net8.0-android \
  -m:1 \
  -p:RuntimeIdentifier=android-arm64 \
  -p:AndroidSdkDirectory=$HOME/android-sdk \
  -p:Aapt2ToolPath=$PREFIX/bin \
  -p:Aapt2ToolExe=aapt2 \
  -p:AndroidBinUtilsDirectory=$PREFIX/bin \
  -p:ZipAlignToolPath=$PREFIX/bin \
  -p:ZipalignToolExe=zipalign \
  -p:RunAOTCompilation=false \
  -p:PublishAot=false \
  -p:AndroidEnableProfiledAot=false \
  -p:AndroidEnableLLVM=false \
  -p:UseInterpreter=true
```

说明：

- `-m:1`：减少并行，降低 Termux 内存压力。
- `UseInterpreter=true`：调试构建更稳，编译更快。
- 明确 `Aapt2ToolPath/ZipAlignToolPath` 可避免某些环境下工具探测失败。
- 不要省略 `RunAOTCompilation=false` / `PublishAot=false`，否则在部分 .NET SDK + Termux 组合下，`Release` 可能触发 `Microsoft.NET.Runtime.MonoAOTCompiler.Task` 解析失败。
- 不要依赖隐式 SDK 路径探测；若未传 `AndroidSdkDirectory`，常会直接报 `XA5300`（找不到 Android SDK）。

## 5. APK 安装与启动调试

先连接设备：可通过无线调试进行连接

```bash
adb devices -l
```

安装 APK：

```bash
adb install -r <APK_PATH>
```

查启动 Activity（避免手写错类名）：

```bash
adb shell cmd package resolve-activity --brief <PACKAGE_NAME>
```

启动应用：

```bash
adb shell am start -n <PACKAGE_NAME>/<ACTIVITY_NAME>
```

查看前台焦点：

```bash
adb shell dumpsys activity activities | rg "topResumedActivity|mCurrentFocus|<PACKAGE_NAME>"
```

## 6. 常见问题与解决方案

### 6.1 `Error type 3: Activity class does not exist`

原因：启动 Activity 名称写错（常见于 .NET Android 生成类名变化）。

解决：

```bash
adb shell cmd package resolve-activity --brief <PACKAGE_NAME>
```

使用返回值作为真实入口再启动。

### 6.2 `aapt2` / `zipalign` 找不到

现象：MSBuild 报错找不到打包工具。

解决：

- 确认安装：`pkg install aapt aapt2 apksigner`
- 编译时显式传参：
  - `-p:Aapt2ToolPath=$PREFIX/bin -p:Aapt2ToolExe=aapt2`
  - `-p:ZipAlignToolPath=$PREFIX/bin -p:ZipalignToolExe=zipalign`

### 6.3 SDK license 未接受

现象：Android 构建报 license 错误。

解决：

```bash
yes | sdkmanager --sdk_root=$ANDROID_SDK_ROOT --licenses
```

### 6.4 `adb unauthorized` 或无设备

解决：

```bash
adb kill-server
adb start-server
adb devices -l
```

在设备上确认 USB 调试授权弹窗。

### 6.5 `Could not extract the MVID ... refint/*.dll`

这条在本项目中多次出现但不阻塞构建，通常可忽略；关注是否有 `Build succeeded`。

### 6.6 CA1416 警告（Android API 版本）

通常是警告非错误。发布前可通过 API level 判断或 `SupportedOSPlatform` 标注处理。

### 6.7 `Could not resolve SDK "Microsoft.NET.Runtime.MonoAOTCompiler.Task"`

现象：

- `dotnet build -c Release` 时报 `MSB4236` / `Could not resolve SDK "Microsoft.NET.Runtime.MonoAOTCompiler.Task"`。

常见触发：

- 只传 `-c Release`，未显式传 `RunAOTCompilation=false`（或未同时关闭 `PublishAot`）。
- 未显式提供 `AndroidSdkDirectory`，导致后续又触发 `XA5300`，误以为是同一个错误。

处理顺序（建议固定成脚本）：

```bash
dotnet workload list
dotnet workload repair

dotnet build <YOUR_ANDROID_CSPROJ> \
  -c Release \
  -f net8.0-android \
  -m:1 \
  -p:RuntimeIdentifier=android-arm64 \
  -p:AndroidSdkDirectory=$HOME/android-sdk \
  -p:Aapt2ToolPath=$PREFIX/bin \
  -p:Aapt2ToolExe=aapt2 \
  -p:AndroidBinUtilsDirectory=$PREFIX/bin \
  -p:ZipAlignToolPath=$PREFIX/bin \
  -p:ZipalignToolExe=zipalign \
  -p:RunAOTCompilation=false \
  -p:PublishAot=false \
  -p:AndroidEnableProfiledAot=false \
  -p:AndroidEnableLLVM=false \
  -p:UseInterpreter=true
```

说明：`workload repair` 可以修复缺失 pack 记录；但若构建参数没有固定，仍可能复现该错误。

## 7. 推荐的可复用脚本化变量

每次构建前先设置，减少命令长度与手误：

```bash
export ANDROID_SDK_ROOT=$HOME/android-sdk
export ANDROID_HOME=$ANDROID_SDK_ROOT
export JAVA_HOME=$PREFIX/lib/jvm/java-21-openjdk
export DOTNET_ROOT=$PREFIX/lib/dotnet
export PATH=$ANDROID_SDK_ROOT/platform-tools:$PATH
```

## 8. Termux 场景注意事项

- Termux 在同一台手机上做 ADB 自动化时，前台焦点和输入模拟可能不稳定。
- UI 手势问题建议优先人工实测，ADB 仅用于安装、启动、日志抓取。
- 大项目建议保持 `-m:1`，避免并发编译引发内存抖动。

## 9. 快速检查清单

```bash
dotnet --info
java -version
adb version
aapt2 version
ls $HOME/android-sdk/platforms
ls $HOME/android-sdk/build-tools
dotnet workload list
adb devices -l
```

如果以上都正常，`.NET Android` 在 Termux 的编译链基本可用。
