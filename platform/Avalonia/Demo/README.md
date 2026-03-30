# SweetEditor Avalonia Demo

This is a demo application showcasing the SweetEditor Avalonia control.

## Build and Run

### Prerequisites

- .NET 9 SDK
- Native runtime: `sweeteditor.dll`/`libsweeteditor.so`/`libsweeteditor.dylib`

### Build

```bash
cd platform/Avalonia
dotnet build Demo/Demo.csproj -c Release
```

### Run

```bash
dotnet run --project Demo/Demo.csproj
```

## Features Demo

The demo application showcases the following features:

- **Text Editing**: Basic text editing operations (insert, delete, undo, redo)
- **Syntax Highlighting**: SweetLine-based cross-language highlighting via `IDecorationProvider`
- **Sample File Picker**: Loads demo files from `platform/_res/files`
- **Line Numbers**: Automatic line numbering
- **Word Wrap**: Cycle wrap mode presets
- **Theme Switching**: Toggle between dark and light themes
- **Decoration Controls**: Load/Clear decorations for parity testing
- **Selection Tools**: `Select All` and `Get Selection`
- **Completion Preview**: Manual and trigger-based completion items with overlay preview
- **Phantom Text**: Demo insertion and clearing of manual phantom text
- **Linked Editing**: Start linked editing on repeated identifiers
- **Folding Demo**: Fold all and unfold all visible fold regions
- **Performance Overlay**: Live metrics for FPS, render time, CPU, memory, cursor, selection and action rates
- **Cursor Movement**: Arrow keys, Home, End, Page Up, Page Down
- **Selection**: Text selection with mouse and keyboard
- **Current Line Highlighting**: Visual indication of the current line
- **Scrolling**: Smooth scrolling with mouse wheel
- **Events**: Text changed, cursor changed, and selection changed events

## Usage

1. **Choose File**: Use the file combo box to switch demo files under `platform/_res/files`
2. **Undo/Redo**: Use the Undo and Redo buttons to navigate edit history
3. **Selection**: Click `Select All` and `Get Selection` to test selection APIs
4. **Decorations**: Click `Load Decorations` / `Clear Decorations` to compare decorated and plain states
5. **Completion/Phantom/Snippet**: Use `Completion`, `Phantom+`, `Phantom-` and `Snippet` to exercise core editing features
6. **Linked Edit/Folding**: Use `Linked Edit`, `Fold All` and `Unfold All` to verify structural editing
7. **Selection Menu**: Open `Selection Menu` to test copy/cut/paste and word-based selection commands
8. **Theme**: Click `Toggle Theme` to toggle dark/light editor theme
9. **Wrap Mode**: Click `WrapMode` to cycle wrap settings
10. **Reload**: Click `Reload` to reload the current file from disk

### Test Sample File

- Preferred default sample: `platform/_res/files/example.cpp` when present
- Resource fallback sample: first file under `platform/_res/files`
- Avalonia fallback sample: `platform/Avalonia/Demo/Assets/example.cpp`

## Keyboard Shortcuts

- Arrow keys: Move cursor
- Shift + Arrow keys: Extend selection
- Ctrl + Home/End: Move to document start/end
- Ctrl + Z: Undo
- Ctrl + Y: Redo
- Tab: Insert tab
- Backspace/Delete: Delete characters
- Enter: Insert new line

## Architecture

The demo follows the SweetEditor architecture:

```
┌─────────────────────────────────────────────────────────────┐
│                    MainWindow (Avalonia)                 │
│  ┌─────────────────────────────────────────────────────┐  │
│  │              EditorControl (Avalonia)            │  │
│  │  - Input event handling (Pointer, Keyboard)    │  │
│  │  - Rendering (DrawingContext)                 │  │
│  │  - Theme management                           │  │
│  └─────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                          │
                          │ P/Invoke
                          ▼
┌─────────────────────────────────────────────────────────────┐
│              SweetEditor C++ Core                       │
│  - Text layout and cursor management                    │
│  - Selection and editing operations                    │
│  - Folding and decorations                            │
└─────────────────────────────────────────────────────────────┘
```

## Extending the Demo

To extend the demo with custom features:

1. **Add Custom Syntax Highlighting**:
   ```csharp
   var theme = EditorTheme.Dark()
       .DefineTextStyle(EditorTheme.STYLE_KEYWORD, new TextStyle(0xFF7AA2F7, 0, 1));
   editor.ApplyTheme(theme);
   ```

2. **Add Decorations**:
   ```csharp
   editor.SetLineSpans(line, SpanLayer.SYNTAX, new List<StyleSpan> {
       new StyleSpan(0, 5, EditorTheme.STYLE_KEYWORD)
   });
   ```

3. **Handle Events**:
   ```csharp
   editor.TextChanged += (sender, e) => {
       Console.WriteLine($"Text changed: {e.Action}");
   };
   ```

## Troubleshooting

### Native Library Not Found

If you see an error about missing native libraries:

1. Ensure the native library files are in the correct location:
   - Windows: `runtimes/win-x64/native/sweeteditor.dll`
   - Linux: `runtimes/linux-x64/native/libsweeteditor.so`
   - macOS: `runtimes/osx-x64/native/libsweeteditor.dylib`

2. Copy the native libraries from the CMake build output:
   ```bash
   cp cmake-build-release-visual-studio/bin/sweeteditor.dll \
      platform/Avalonia/SweetEditor/bin/Release/net8.0/runtimes/win-x64/native/
   ```

### Build Errors

If you encounter build errors:

1. Ensure .NET 9 SDK is installed:
   ```bash
   dotnet --version
   ```

2. Restore NuGet packages:
   ```bash
   dotnet restore platform/Avalonia/SweetEditor/SweetEditor.csproj
   dotnet restore platform/Avalonia/Demo/Demo.csproj
   ```

3. Clean and rebuild:
   ```bash
   dotnet clean platform/Avalonia/Demo/Demo.csproj
   dotnet build platform/Avalonia/Demo/Demo.csproj -c Release
   ```

## License

This demo is part of the SweetEditor project. See the main project license for details.
