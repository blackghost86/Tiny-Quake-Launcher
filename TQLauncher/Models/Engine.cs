namespace TinyQuakeLauncher.Models;

public class Engine
{
    public string Name { get; set; } = "";

    public string ExecutablePath { get; set; } = "";

    public override string ToString()
    {
        return Name;
    }
}
