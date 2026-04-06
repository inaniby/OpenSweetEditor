namespace SweetEditor {
	public readonly struct TextStyle {
		public int Color { get; }
		public int BackgroundColor { get; }
		public int FontStyle { get; }

		public TextStyle(int color, int fontStyle) : this(color, 0, fontStyle) {
		}

		public TextStyle(int color, int backgroundColor, int fontStyle) {
			Color = color;
			BackgroundColor = backgroundColor;
			FontStyle = fontStyle;
		}
	}
}
