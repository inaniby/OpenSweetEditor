import XCTest
#if os(macOS)
@testable import SweetEditorMacOS
@testable import SweetEditorCoreInternal

final class EditorSettingsParityTests: XCTestCase {
    func testMacEditorExposesCentralizedEditorSettings() {
        let view = SweetEditorViewMacOS(frame: .zero)

        XCTAssertNotNil(view.settings)
    }

    func testEditorSettingsDefaultsFollowAndroidInspiredContract() {
        let view = SweetEditorViewMacOS(frame: .zero)
        let settings = view.settings

        XCTAssertEqual(settings.editorTextSize, 14.0, accuracy: 0.001)
        XCTAssertEqual(settings.scale, 1.0, accuracy: 0.001)
        XCTAssertEqual(settings.foldArrowMode, .always)
        XCTAssertEqual(settings.wrapMode, .none)
        XCTAssertEqual(settings.lineSpacingAdd, 0.0, accuracy: 0.001)
        XCTAssertEqual(settings.lineSpacingMult, 1.0, accuracy: 0.001)
        XCTAssertEqual(settings.contentStartPadding, 0.0, accuracy: 0.001)
        XCTAssertTrue(settings.showSplitLine)
        XCTAssertEqual(settings.currentLineRenderMode, .background)
        XCTAssertEqual(settings.autoIndentMode, .keepIndent)
        XCTAssertFalse(settings.readOnly)
        XCTAssertEqual(settings.maxGutterIcons, 0)
    }

    func testEditorSettingsCanMutateCoreBackedConfiguration() {
        let view = SweetEditorViewMacOS(frame: .zero)
        let settings = view.settings

        settings.setScale(1.25)
        settings.setFoldArrowMode(.hidden)
        settings.setWrapMode(.wordBreak)
        settings.setLineSpacing(add: 2.0, mult: 1.4)
        settings.setContentStartPadding(8.0)
        settings.setShowSplitLine(false)
        settings.setCurrentLineRenderMode(.border)
        settings.setAutoIndentMode(.none)
        settings.setReadOnly(true)
        settings.setMaxGutterIcons(3)

        XCTAssertEqual(settings.scale, 1.25, accuracy: 0.001)
        XCTAssertEqual(settings.foldArrowMode, .hidden)
        XCTAssertEqual(settings.wrapMode, .wordBreak)
        XCTAssertEqual(settings.lineSpacingAdd, 2.0, accuracy: 0.001)
        XCTAssertEqual(settings.lineSpacingMult, 1.4, accuracy: 0.001)
        XCTAssertEqual(settings.contentStartPadding, 8.0, accuracy: 0.001)
        XCTAssertFalse(settings.showSplitLine)
        XCTAssertEqual(settings.currentLineRenderMode, .border)
        XCTAssertEqual(settings.autoIndentMode, .none)
        XCTAssertTrue(settings.readOnly)
        XCTAssertEqual(settings.maxGutterIcons, 3)
    }

    func testLegacyMacSettersForwardIntoCentralizedSettings() {
        let view = SweetEditorViewMacOS(frame: .zero)

        view.setScale(1.1)
        view.setWrapMode(2)
        view.setMaxGutterIcons(4)
        view.setFoldArrowMode(.hidden)
        view.setLineSpacing(add: 1.0, mult: 1.2)
        view.setContentStartPadding(6.0)
        view.setShowSplitLine(false)
        view.setCurrentLineRenderMode(.border)
        view.setReadOnly(true)

        XCTAssertEqual(view.settings.scale, 1.1, accuracy: 0.001)
        XCTAssertEqual(view.settings.wrapMode, .wordBreak)
        XCTAssertEqual(view.settings.maxGutterIcons, 4)
        XCTAssertEqual(view.settings.foldArrowMode, .hidden)
        XCTAssertEqual(view.settings.lineSpacingAdd, 1.0, accuracy: 0.001)
        XCTAssertEqual(view.settings.lineSpacingMult, 1.2, accuracy: 0.001)
        XCTAssertEqual(view.settings.contentStartPadding, 6.0, accuracy: 0.001)
        XCTAssertFalse(view.settings.showSplitLine)
        XCTAssertEqual(view.settings.currentLineRenderMode, .border)
        XCTAssertTrue(view.settings.readOnly)
    }

    func testEditorThemeUsesAndroidAlignedColorSemantics() {
        let dark = EditorTheme.dark()

        XCTAssertGreaterThan(dark.currentLineNumberColor.alpha, 0)
        XCTAssertGreaterThan(dark.inlayHintTextColor.alpha, 0)
        XCTAssertGreaterThan(dark.foldPlaceholderBgColor.alpha, 0)
        XCTAssertGreaterThan(dark.foldPlaceholderTextColor.alpha, 0)
        XCTAssertGreaterThan(dark.phantomTextColor.alpha, 0)
    }

    func testLanguageConfigurationRetainsAndroidCoreFieldsAndAppleCommentMetadata() {
        let config = LanguageConfiguration(
            languageId: "swift",
            brackets: [.init(open: "(", close: ")")],
            autoClosingPairs: [.init(open: "{", close: "}")],
            lineComment: "//",
            blockComment: .init(open: "/*", close: "*/"),
            tabSize: 4,
            insertSpaces: true
        )

        XCTAssertEqual(config.languageId, "swift")
        XCTAssertEqual(config.brackets?.count, 1)
        XCTAssertEqual(config.autoClosingPairs?.count, 1)
        XCTAssertEqual(config.lineComment, "//")
        XCTAssertEqual(config.blockComment?.open, "/*")
        XCTAssertEqual(config.blockComment?.close, "*/")
        XCTAssertEqual(config.tabSize, 4)
        XCTAssertEqual(config.insertSpaces, true)
    }
}
#endif
