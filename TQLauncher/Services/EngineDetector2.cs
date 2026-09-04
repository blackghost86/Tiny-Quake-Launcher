using System.IO;
using TinyQuakeLauncher.Models;
using TinyQuakeLauncher.Games;

namespace TinyQuakeLauncher.Services;

public class EngineDetector2
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
            "*.exe",
            System.IO.SearchOption.AllDirectories);

        foreach (string executable in executables)
        {
            Engine? engine = IdentifyEngine(executable);

            if (engine != null)
            {
                engines.Add(engine);
            }
        }

        return engines
            .OrderBy(engine => engine.Name)
            .ToList();
    }

    private Engine? IdentifyEngine(string executablePath)
    {
        string fileName =
            Path.GetFileName(executablePath)
                .ToLowerInvariant();

        return fileName switch
        {
            "yquake2.exe" => CreateEngine(
                "Yamagi Quake II",
                executablePath),

            "q2pro.exe" => CreateEngine(
                "Q2Pro",
                executablePath),

            "q2pro-ng.exe" => CreateEngine(
                "Q2Pro-NG",
                executablePath),

            "kmquake2.exe" => CreateEngine(
                "KMQuake II",
                executablePath),

            "q2rtx.exe" => CreateEngine(
                "Quake II RTX",
                executablePath),

            "quake2ex_gog.exe" => CreateEngine(
                "Quake II GOG",
                executablePath),

            "quake2ex_steam.exe" => CreateEngine(
                "Quake II (Steam)",
                executablePath),

            "quake2.exe" => CreateEngine(
                "Vanilla Quake II",
                executablePath),

            // This engine supports both games.
            "fteqw.exe" => CreateEngine(
                "FTEQW",
                executablePath),

            "fteqw64.exe" => CreateEngine(
                "FTEQW",
                executablePath),

            _ => null
        };
    }

    private Engine CreateEngine(
        string name,
        string executablePath)
    {
        return new Engine
        {
            Name = name,
            ExecutablePath = executablePath,
            Game = QuakeGame.Quake2
        };
    }
}