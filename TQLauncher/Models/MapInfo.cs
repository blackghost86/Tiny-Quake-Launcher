namespace TinyQuakeLauncher.Models;

public class MapInfo
{
    public string FileName { get; set; } = "";

    public string Title { get; set; } = "";

    public System.Windows.Media.Brush Foreground { get; set; } =
        System.Windows.Media.Brushes.Black;

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Title)
            ? FileName
            : $"{FileName} | {Title}";

    public override string ToString()
    {
        return DisplayName;
    }
}