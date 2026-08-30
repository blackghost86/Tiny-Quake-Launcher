using TinyQuakeLauncher.Games;

namespace TinyQuakeLauncher.Models;

public class Engine
{
    public string Name { get; set; } = "";

    public string ExecutablePath { get; set; } = "";

    public QuakeGame Game { get; set; }

    public override string ToString()
    {
        return Game == QuakeGame.Quake2
            ? $"{Name} | Q2"
            : $"{Name} | Q1";
    }
}