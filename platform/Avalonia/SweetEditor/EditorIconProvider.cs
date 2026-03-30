namespace SweetEditor {
	public interface EditorIconProvider {
		Avalonia.Media.IImage? GetIconImage(int iconId);
	}
}