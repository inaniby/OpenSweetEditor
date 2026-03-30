# Contributing

This document gives practical development entry points based on the current repository structure. If docs conflict with code, use the code.

## Suggested Reading Order

1. `docs/en/architecture.md`
2. `docs/en/api-editor-core.md`
3. `src/include/*.h`
4. `src/core/*.cpp`
5. Read `platform/*` last

## Repository Map

```text
├── 3dparty
│   ├── simdutf / json / utfcpp      third-party dependencies
├── docs
│   ├── zh/architecture.md           architecture overview (ZH)
│   ├── zh/api-editor-core.md        C API / core contract (ZH)
│   ├── zh/api-platform*.md          platform API docs (ZH)
│   ├── en/architecture.md           architecture overview (EN)
│   ├── en/api-editor-core.md        C API / core contract (EN)
│   └── en/api-platform*.md          platform API docs (EN)
├── src
│   ├── include                      core headers and c_api.h
│   └── core                         Document / Layout / Decoration / EditorCore / c_api
├── tests                            core regression tests
├── platform
│   ├── Android                      Android SDK + direct JNI
│   ├── Swing                        Java FFM + C API
│   ├── WinForms                     C# P/Invoke + C API
│   ├── Apple                        Swift Package + manual C bridge
│   ├── OHOS                         OHOS SDK + direct NAPI
│   └── Emscripten                   experimental Web testing (unofficial fork: https://github.com/LangLang03/OpenSweetEditor-Web/tree/main/platform/Emscripten)
└── prebuilt                         prebuilt shared libs
```

## Platform Directory Responsibilities

### Core Layer

- `src/include/document.h` / `src/core/document.cpp`
  - text storage, position mapping, Piece Table / LineArray
- `src/include/layout.h` / `src/core/layout.cpp`
  - text layout, auto wrap, hit test, measure cache, visible-area clipping
- `src/include/decoration.h` / `src/core/decoration.cpp`
  - decorations: highlight, Inlay Hint, Ghost Text, guide lines, fold, diagnostics
- `src/include/editor_core.h` / `src/core/editor_core.cpp`
  - main edit coordinator: input, selection, IME, undo/redo, render model assembly
- `src/include/c_api.h` / `src/core/c_api.cpp`
  - stable bridge boundary for non-Android platforms

### Android

- `platform/Android/sweeteditor/src/main/java/com/qiplat/sweeteditor/SweetEditor.java`
  - control layer, semantic API for app side
- `platform/Android/sweeteditor/src/main/java/com/qiplat/sweeteditor/core/EditorCore.java`
  - JNI bridge and binary-result decoding
- `platform/Android/sweeteditor/src/main/cpp/jni_entry.cpp`
- `platform/Android/sweeteditor/src/main/cpp/jeditor.hpp`
  - main direct path to C++

### Swing

- `platform/Swing/sweeteditor/src/main/java/com/qiplat/sweeteditor/SweetEditor.java`
  - Swing control layer
- `platform/Swing/sweeteditor/src/main/java/com/qiplat/sweeteditor/core/EditorCore.java`
  - semantic wrapper on Java side
- `platform/Swing/sweeteditor/src/main/java/com/qiplat/sweeteditor/core/EditorNative.java`
  - FFM downcall / upcall

### WinForms

- `platform/WinForms/SweetEditor/EditorControl.cs`
  - WinForms control layer, input and drawing
- `platform/WinForms/SweetEditor/EditorCore.cs`
  - P/Invoke wrapper and protocol bridge
- `platform/WinForms/SweetEditor/EditorProtocol.cs`
  - binary payload decoding
- `platform/WinForms/SweetEditor/EditorCompletion.cs`
  - completion providers and popup coordination
- `platform/WinForms/SweetEditor/EditorDecoration.cs`
  - decoration providers and refresh scheduling
- `platform/WinForms/SweetEditor/EditorNewLine.cs`
  - newline action provider plumbing
- `platform/WinForms/SweetEditor/Perf.cs`
  - performance logging and overlay drawing

### Apple

- `platform/Apple/Sources/SweetEditorBridge/include/SweetEditorBridge.h`
  - manual C bridge header
- `platform/Apple/Sources/SweetEditorCoreInternal/api/SweetEditorCore.swift`
  - core Swift wrapper and bridge-facing entry points
- `platform/Apple/Sources/SweetEditorCoreInternal/protocol/ProtocolDecoder.swift`
  - binary payload decoding aligned with Android `ProtocolDecoder`
- `platform/Apple/Sources/SweetEditorCoreInternal/visual`
  - render-model DTOs aligned with Android `core.visual`
- `platform/Apple/Sources/SweetEditorCoreInternal/EditorRenderer.swift`
  - shared Apple renderer consuming the visual model
- `platform/Apple/Sources/SweetEditoriOS`
- `platform/Apple/Sources/SweetEditorMacOS`
  - iOS / macOS platform views

## If You Change X, Start from Y

- Change text editing semantics, undo/redo, selection, IME:
  - check `editor_core.*`, `document.*`, `gesture.*` first
- Change auto wrap, hit test, fold placeholders, inlay / ghost layout:
  - check `layout.*`, `visual.h` first
- Change decoration offsets, diagnostics, guide lines, fold:
  - check `decoration.*` first
- Change public ABI, binary protocol, enum values:
  - change `c_api.h` / `c_api.cpp` first
  - then sync Swing / WinForms / Apple
  - if Android has equivalent capability, sync JNI path too
- Change platform input behavior:
  - first confirm core semantic support exists, then change platform forwarding; do not hard-code edit rules in platform layer

## Cross-Platform Sync Checkpoints

If any item below is touched, do not change only one layer:

- New or changed function in `c_api.h`
- Binary payload field order/type/enum value changed
- `TextEditResult` / `GestureResult` / `KeyEventResult` / `ScrollMetrics` / `LayoutMetrics` changed
- Render-model fields changed
- Core behavior changed for IME, gesture, fold, or decorations

Usual sync targets:

- Android: `jeditor.hpp`, `jni_entry.cpp`, Java `ProtocolDecoder`
- Swing: `EditorNative.java`, `ProtocolDecoder.java`
- WinForms: `EditorCore.cs`, `EditorProtocol.cs`
- Apple: `SweetEditorBridge.h`, `SweetEditorCore.swift`

## Build Entry

- Core / tests: repo root `cmake` + `tests/CMakeLists.txt`
- Android: `platform/Android`
- Swing: `platform/Swing`
- WinForms: `platform/WinForms/WinForms.sln`
- Apple: `platform/Apple/Package.swift`

## Doc and Encoding Conventions

- Doc updates should reflect capabilities already implemented in current code. Do not write roadmap items as current status.
- For Chinese files in Windows repos, verify encoding first; in this repo, `README.md` and most `docs/zh/*.md` files are UTF-8.
- After platform protocol changes, sync at least one note in `README.md`, `docs/zh/architecture.md`, `docs/en/architecture.md`, and matching `docs/zh/api-platform*.md` / `docs/en/api-platform*.md` files.

## Naming Style (Current Code Habits)

- C++ file names are lowercase; headers use `.h`, implementation files use `.cpp`
- C++ type names use PascalCase
- C++ function names use lowerCamelCase
- C++ member variables usually use `m_` prefix
- Public APIs in platform layer should be semantic first; bridge layer should stay close to low-level protocol
