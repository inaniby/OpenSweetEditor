# Binaries

`SweetEditorCoreIOS.xcframework` and `SweetEditorCoreOSX.xcframework` are generated locally by:

```bash
make native
```

This directory only stores the final Apple binary artifact consumed by `Package.swift`.

- `SweetEditorCoreIOS.xcframework` contains dynamic `SweetEditorCore.framework` slices for iOS device and simulator.
- `SweetEditorCoreOSX.xcframework` contains dynamic `SweetEditorCore.framework` slices for macOS.
- Intermediate framework and dynamic-library build outputs stay under the repository `build/` directory and are not treated as stable packaged artifacts.
