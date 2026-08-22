namespace TinyQuakeLauncher.Models;

public class MissionPack
{
    public string Name { get; set; } = "";

    public string GameDirectory { get; set; } = "";

    public override string ToString()
    {
        return Name;
    }
}