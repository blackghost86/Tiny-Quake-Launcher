using System.IO;
using TinyQuakeLauncher.Models;
using TinyQuakeLauncher.Games;

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
            "quakespasm.exe" => CreateEngine(
                "Quakespasm",
                executablePath),

            "quakespasm-sdl12.exe" => CreateEngine(
                "Quakespasm SDL",
                executablePath),

            "quakespasm-spiked-win32.exe" => CreateEngine(
                "Quakespasm-Spiked",
                executablePath),

            "quakespasm-spiked-win64.exe" => CreateEngine(
                "Quakespasm-Spiked",
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

            "chocolate-quake.exe" => CreateEngine(
                "Chocolate Quake",
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

            "quake_shipping_playfab_gog_x64.exe" => CreateEngine(
                "Quake GOG",
                executablePath),

            "quake_gog.exe" => CreateEngine(
                "Quake GOG",
                executablePath),

            "quake_egs.exe" => CreateEngine(
                "Quake EGS",
                executablePath),

            "quake_x64_steam.exe" => CreateEngine(
                "Quake (Steam)",
                executablePath),

            "quake.exe" => CreateEngine(
                "Vanilla Quake",
                executablePath),

            "glquake.exe" => CreateEngine(
                "GLQuake",
                executablePath),

            "qwcl.exe" => CreateEngine(
                "QuakeWorld Client",
                executablePath),

            "glqwcl.exe" => CreateEngine(
                "QuakeWorld Client (OpenGL)",
                executablePath),

            "winquake.exe" => CreateEngine(
                "WinQuake",
                executablePath),

            // This engine is outdated.
            "fitzquake.exe" => CreateEngine(
                "WinQuake",
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
            Game = QuakeGame.Quake1
        };
    }
}