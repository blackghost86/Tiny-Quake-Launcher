using System;

namespace TinyQuakeLauncher.Models;

public class MapInfo
{
    public static MapInfo None =>
        new MapInfo
        {
            FileName = "",
            Title = "None",
            Foreground =
                System.Windows.Media.Brushes.Black
        };

    public string FileName { get; set; } = "";

    public string Title { get; set; } = "";

    public System.Windows.Media.Brush Foreground { get; set; } =
        System.Windows.Media.Brushes.Black;

    public string DisplayName =>
        string.Equals(
            Title,
            "None",
            StringComparison.OrdinalIgnoreCase)
            ? "None"
            : string.IsNullOrWhiteSpace(Title)
                ? FileName
                : $"{FileName} | {Title}";

    public override string ToString()
    {
        return DisplayName;
    }
}
