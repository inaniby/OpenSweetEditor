# Avalonia Cross-Platform Alignment Report

## Scope
- Baseline: `upstream/main` (`ff8bb1c`)
- Alignment targets:
  - `platform/Swing/sweeteditor/EditorRenderer.java`
  - `platform/WinForms/SweetEditor/EditorRenderer.cs`
  - `platform/Swing/demo/Main.java`
  - `platform/WinForms/Demo/Form1.cs`
  - `platform/Android/app/.../MainActivity.java`

## Key Findings
1. Demo settings are mostly aligned (`FoldArrowMode.AUTO`, `MaxGutterIcons(1)`, `CurrentLineRenderMode.BORDER`, wrap toggle).
2. Avalonia rendering path had visual gaps versus Swing/WinForms:
   - No dedicated `FOLD_PLACEHOLDER` rounded-block rendering.
   - Diagnostic underline was straight line only (desktop demos use wavy line for non-hint severities).
   - Guide color source differed from desktop implementations.
   - Composition underline color used completion border color instead of composition color.
   - Phantom text color was alpha-mixed from run color, not theme phantom color.

## Applied Fixes (Avalonia)
File: `platform/Avalonia/SweetEditor/EditorRenderer.cs`

- Added dedicated `VisualRunType.FOLD_PLACEHOLDER` rendering using:
  - `FoldPlaceholderBgColor`
  - `FoldPlaceholderTextColor`
  - rounded rectangle geometry
- Upgraded diagnostics rendering:
  - severity != hint: wavy underline
  - hint severity: dashed straight line
  - fallback colors now use theme diagnostic colors
- Updated composition underline color:
  - use `CompositionColor`
- Updated guide segment colors:
  - separator -> `SeparatorColor`
  - others -> `GuideColor`
- Updated phantom text rendering:
  - use `PhantomTextColor` directly

## Verification
- `dotnet build platform/Avalonia/Demo/Demo.csproj -c Release` passed.
- `dotnet test platform/Avalonia/Tests/Tests.csproj -c Release` passed (after one transient Avalonia XAML task retry).
- Demo runtime screenshot:
  - `platform/Avalonia/Demo/artifacts/demo_after_renderer_alignment_fix.png`

## Environment Limitations
- In this Linux CI/container environment, non-Avalonia platform demos were not fully runnable end-to-end:
  - Swing demo Gradle config is gated to Windows/macOS native lib path setup.
  - WinForms/Apple/OHOS/Android demos require platform-specific runtime/tooling.
- Therefore cross-platform comparison is based on source-level behavior parity plus Avalonia runtime validation.
