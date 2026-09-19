using System.IO;
using TinyQuakeLauncher.Models;
using TinyQuakeLauncher.Games;

namespace TinyQuakeLauncher.Services;

public class EngineDetector3
{
    public List<Engine> DetectEngines(string folder)
    {
        List<Engine> engines = new();

        if (!Directory.Exists(folder))
        {
            return engines;
        }

        string[] executables = Directory.GetFiles(
            folder,
            "ioquake3.x86_64.exe",
            SearchOption.AllDirectories);

        foreach (string executable in executables)
        {
            // ioquake3 is the default engine for Quake 3.
            engines.Add(
                new Engine
                {
                    Name = "ioquake3",
                    ExecutablePath = executable,
                    Game = QuakeGame.Quake3
                });
        }

        return engines
            .OrderBy(engine => engine.Name)
            .ThenBy(engine => engine.ExecutablePath)
            .ToList();
    }
}