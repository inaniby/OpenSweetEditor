# SweetEditor for Avalonia

`SweetEditor` is an Avalonia editor control backed by the SweetEditor C++ core.
The core handles text layout, cursor/selection logic, folding, decoration data, and interaction math; Avalonia handles native rendering and input dispatch.

GitHub:
- Main repository: [https://github.com/FinalScave/OpenSweetEditor](https://github.com/FinalScave/OpenSweetEditor)
- Avalonia package source: `platform/Avalonia/SweetEditor`

## Features

- Syntax/semantic style spans
- Inlay hints and ghost text
- Diagnostics and custom decorations
- Gutter icons and fold markers
- Code folding and wrap mode switching
- Current-line rendering modes
- Completion and newline extension providers
- Monospace and proportional font support

## Requirements

- .NET 8 (`net8.0`)
- Native runtime: `sweeteditor.dll`/`libsweeteditor.so`/`libsweeteditor.dylib` (included in NuGet package under `runtimes/[platform]/native/`)

## Install

```bash
dotnet add package SweetEditor.Avalonia
```

## Quick Start

```csharp
using Avalonia.Controls;
using SweetEditor;

public sealed class MainWindow : Window
{
    private readonly EditorControl editor = new EditorControl { };

    public MainWindow()
    {
        Content = editor;

        editor.ApplyTheme(EditorTheme.Dark());
        editor.Settings.SetWrapMode(WrapMode.WORD_BREAK);
        editor.Settings.SetCurrentLineRenderMode(CurrentLineRenderMode.BORDER);

        var code = "int main() {\n    return 0;\n}\n";
        editor.LoadDocument(new Document(code));
    }
}
```

## Common API

```csharp
editor.InsertText("Hello");
editor.ReplaceText(new TextRange(new TextPosition(0, 0), new TextPosition(0, 5)), "Hi");
editor.SelectAll();
editor.SetSelection(0, 0, 0, 2);
editor.ScrollToLine(100);
editor.ToggleFold(42);
editor.TriggerCompletion();
```

## Theme and Style Registration

```csharp
var theme = EditorTheme.Dark()
    .DefineTextStyle(EditorTheme.STYLE_KEYWORD, new TextStyle(unchecked((int)0xFF7AA2F7), EditorControl.FONT_STYLE_BOLD));
editor.ApplyTheme(theme);
```

## Extension Points

- Decoration providers: `IDecorationProvider`
- Completion providers: `ICompletionProvider`
- Newline action providers: `INewLineActionProvider`
- Icon rendering: `EditorIconProvider`

## Build and Pack

From repository root:

```bash
dotnet build .\platform\Avalonia\SweetEditor\SweetEditor.csproj -c Release
dotnet pack .\platform\Avalonia\SweetEditor\SweetEditor.csproj -c Release
```

Output package:
- `platform/Avalonia/SweetEditor/bin/Release/SweetEditor.Avalonia.<version>.nupkg`

## Publish to NuGet

```bash
dotnet nuget push .\platform\Avalonia\SweetEditor\bin\Release\SweetEditor.Avalonia.<version>.nupkg `
  --api-key <NUGET_API_KEY> `
  --source https://api.nuget.org/v3/index.json
```