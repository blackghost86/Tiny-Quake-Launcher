using TinyQuakeLauncher.Data;

namespace TinyQuakeLauncher.Models;

public class Demo
{
    public string Name { get; set; } = "";

    public string FileName { get; set; } = "";

    public string GameDirectory { get; set; } = "";

    public DemoResourceType ResourceType { get; set; } =
        DemoResourceType.Folder;

    public string ResourcePath { get; set; } = "";

    public string MapFileName { get; set; } = "";

    public string MapTitle { get; set; } = "";

    public override string ToString()
    {
        return Name;
    }
}
