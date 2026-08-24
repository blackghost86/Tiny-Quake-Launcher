namespace TinyQuakeLauncher.Models;

public class MapInfo
{
    public string FileName { get; set; } = "";

    public string Title { get; set; } = "";

    public override string ToString()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            return FileName;
        }

        return $"{FileName} | {Title}";
    }
}