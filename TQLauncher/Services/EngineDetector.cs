using System.IO;
using TinyQuakeLauncher.Models;

namespace TinyQuakeLauncher.Services;

public class EngineDetector
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
            SearchOption.TopDirectoryOnly);

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
            Path.GetFileName(executablePath).ToLowerInvariant();

        return fileName switch
        {
            "quakespasm.exe" => CreateEngine(
                "Quakespasm",
                executablePath),

            "vkquake.exe" => CreateEngine(
                "vkQuake",
                executablePath),

            "ironwail.exe" => CreateEngine(
                "Ironwail",
                executablePath),

            "fteqw.exe" => CreateEngine(
                "FTEQW",
                executablePath),

            "fteqw64.exe" => CreateEngine(
                "FTEQW",
                executablePath),

            "quakespasm-spiked.exe" => CreateEngine(
                "Quakespasm-Spiked",
                executablePath),

            "qss.exe" => CreateEngine(
                "Quakespasm-Spiked",
                executablePath),

            "darkplaces.exe" => CreateEngine(
                "DarkPlaces",
                executablePath),

            "mark_v.exe" => CreateEngine(
                "Mark V",
                executablePath),

            "markv.exe" => CreateEngine(
                "Mark V",
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
            ExecutablePath = executablePath
        };
    }
}