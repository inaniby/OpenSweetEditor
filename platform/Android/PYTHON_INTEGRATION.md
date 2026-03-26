# Python Android 集成说明（com.nanib.pydroid）

本目录下的 Android 示例已改造为 `com.nanib.pydroid`，并接入“用户自带官方 Python Android 包”的运行方式。

## 目标

- APK 不内置 Python 运行时。
- 由用户自行下载官方 Android 包，然后在 App 内导入。
- 支持：
  - 运行编辑器中的 Python 代码
  - `pip install` 第三方包到应用私有目录

## 支持的官方包来源

应用支持两种导入方式：

1. 选择官方 `.tar.gz`
2. 选择已解压目录（`ACTION_OPEN_DOCUMENT_TREE`）

官方包中可包含以下内容：

- `prefix`（必需）
- `testbed`（忽略）
- `android.py`（忽略）
- `android-env.sh`（忽略）
- `README.md`（忽略）

运行时仅依赖 `prefix` 目录。

## 关键实现位置

- `app/src/main/java/com/nanib/pydroid/MainActivity.java`
  - UI 操作入口（安装包、导入目录、导入 get-pip.py、运行、pip）
- `app/src/main/java/com/nanib/pydroid/python/PythonPackageManager.java`
  - `.tar.gz` 解压、目录导入、`prefix` 检测、ABI 与版本识别
- `app/src/main/java/com/nanib/pydroid/python/PythonRuntime.java`
  - 运行时生命周期、代码执行、模块执行、pip 调用、输出读取
- `app/src/main/java/com/nanib/pydroid/python/PythonBridge.java`
  - JNI 声明
- `app/src/main/cpp/python_bridge.c`
  - `dlopen(libpython)` + CPython C-API 调用桥

## pip 说明

- 优先尝试直接运行 `python -m pip --version`。
- 若官方 Android 构建不带 `ensurepip`，需先在 UI 中导入 `get-pip.py`。
- 第三方包安装到：`files/python/site-packages`。

## 运行输出

标准输出/错误输出会写入应用缓存目录，并在页面下方输出面板实时展示。
