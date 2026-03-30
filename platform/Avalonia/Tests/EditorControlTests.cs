using System;
using Xunit;
using SweetEditor;

namespace Tests {
	public class EditorControlTests {
		[Fact]
		public void EditorControl_ShouldInitialize() {
			var editor = new EditorControl();
			Assert.NotNull(editor);
		}

		[Fact]
		public void EditorControl_ShouldLoadDocument() {
			var editor = new EditorControl();
			var document = new Document("Hello, World!");
			editor.LoadDocument(document);
			
			var cursorPosition = editor.GetCursorPosition();
			Assert.Equal(0, cursorPosition.Line);
			Assert.Equal(0, cursorPosition.Column);
		}

		[Fact]
		public void EditorControl_ShouldInsertText() {
			var editor = new EditorControl();
			var document = new Document("Hello");
			editor.LoadDocument(document);
			
			var result = editor.InsertText(", World!");
			Assert.NotEmpty(result.Changes);
			
			var cursorPosition = editor.GetCursorPosition();
			Assert.Equal(0, cursorPosition.Line);
		}

		[Fact]
		public void EditorControl_ShouldUndoRedo() {
			var editor = new EditorControl();
			var document = new Document("Hello");
			editor.LoadDocument(document);
			
			editor.InsertText(", World!");
			Assert.True(editor.CanUndo());
			
			editor.Undo();
			Assert.True(editor.CanRedo());
			
			editor.Redo();
			Assert.False(editor.CanRedo());
		}

		[Fact]
		public void EditorControl_ShouldMoveCursor() {
			var editor = new EditorControl();
			var document = new Document("Hello, World!");
			editor.LoadDocument(document);
			
			editor.MoveCursorRight(false);
			var cursorPosition = editor.GetCursorPosition();
			Assert.Equal(1, cursorPosition.Column);
		}

		[Fact]
		public void EditorControl_ShouldSelectText() {
			var editor = new EditorControl();
			var document = new Document("Hello, World!");
			editor.LoadDocument(document);
			
			editor.MoveCursorRight(true);
			editor.MoveCursorRight(true);
			
			var selection = editor.GetSelection();
			Assert.True(selection.Start.Line == 0 && selection.Start.Column == 0);
			Assert.True(selection.End.Line == 0 && selection.End.Column == 2);
		}

		[Fact]
		public void EditorTheme_ShouldCreateDarkTheme() {
			var theme = EditorTheme.Dark();
			Assert.NotEqual(0u, theme.BackgroundColor);
			Assert.NotEqual(0u, theme.ForegroundColor);
			Assert.NotEqual(0u, theme.CursorColor);
		}

		[Fact]
		public void EditorTheme_ShouldCreateLightTheme() {
			var theme = EditorTheme.Light();
			Assert.NotEqual(0u, theme.BackgroundColor);
			Assert.NotEqual(0u, theme.ForegroundColor);
			Assert.NotEqual(0u, theme.CursorColor);
		}

		[Fact]
		public void Document_ShouldCreateFromString() {
			var document = new Document("Hello, World!");
			Assert.NotNull(document);
			Assert.NotNull(document.Handle);
		}

		[Fact]
		public void Document_ShouldGetLineText() {
			var document = new Document("Hello\nWorld");
			var line0 = document.GetLineText(0);
			var line1 = document.GetLineText(1);
			
			Assert.Equal("Hello", line0);
			Assert.Equal("World", line1);
		}

		[Fact]
		public void TextPosition_ShouldCompare() {
			var pos1 = new TextPosition { Line = 0, Column = 0 };
			var pos2 = new TextPosition { Line = 0, Column = 0 };
			var pos3 = new TextPosition { Line = 1, Column = 0 };
			
			Assert.Equal(pos1.Line, pos2.Line);
			Assert.Equal(pos1.Column, pos2.Column);
			Assert.NotEqual(pos1.Line, pos3.Line);
		}

		[Fact]
		public void TextRange_ShouldCreate() {
			var start = new TextPosition { Line = 0, Column = 0 };
			var end = new TextPosition { Line = 0, Column = 5 };
			var range = new TextRange(start, end);
			
			Assert.Equal(start.Line, range.Start.Line);
			Assert.Equal(start.Column, range.Start.Column);
			Assert.Equal(end.Line, range.End.Line);
			Assert.Equal(end.Column, range.End.Column);
		}
	}
}
