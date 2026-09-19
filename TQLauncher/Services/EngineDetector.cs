using System.IO;
using System.Linq;
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

        AddQuakeSpasmSpikedEngine(
            engines,
            folder);

        AddIronwailEngine(
            engines,
            folder);

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

            // Alternative executable for Quake GOG.
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
                "QW Client",
                executablePath),

            "glqwcl.exe" => CreateEngine(
                "QW Client (OpenGL)",
                executablePath),

            "winquake.exe" => CreateEngine(
                "WinQuake",
                executablePath),

            // This engine is outdated.
            "fitzquake.exe" => CreateEngine(
                "FitzQuake",
                executablePath),

            "fitzquake85.exe" => CreateEngine(
                "FitzQuake",
                 executablePath),

            _ => null
        };
    }

    private static void AddQuakeSpasmSpikedEngine(
        List<Engine> engines,
        string quakeFolder)
    {
        //Quakespasm-Spiked could be located in a qss folder.
        string qssFolder =
            Path.Combine(
                quakeFolder,
                "qss");

        if (!Directory.Exists(qssFolder))
        {
            return;
        }

        string? executablePath =
            Directory.GetFiles(
                    qssFolder,
                    "*.exe",
                    SearchOption.TopDirectoryOnly)
                .FirstOrDefault(
                    path =>
                        Path.GetFileNameWithoutExtension(path)
                            .StartsWith(
                                "quakespasm-spiked",
                                StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return;
        }

        if (engines.Any(
                engine =>
                    string.Equals(
                        engine.ExecutablePath,
                        executablePath,
                        StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        engines.Add(
            new Engine
            {
                Name = "Quakespasm-Spiked",
                ExecutablePath = executablePath,
                Game = QuakeGame.Quake1
            });
    }

    private static void AddIronwailEngine(
        List<Engine> engines,
        string quakeFolder)
    {
        // Ironwail is usually located in a folder.
        string? executablePath =
            Directory.GetFiles(
                    quakeFolder,
                    "ironwail.exe",
                    SearchOption.AllDirectories)
                .FirstOrDefault(
                    path =>
                    {
                        string? directory =
                            Path.GetDirectoryName(path);

                        return !string.IsNullOrWhiteSpace(directory) &&
                            string.Equals(
                                Path.GetFileName(directory),
                                "ironwail",
                                StringComparison.OrdinalIgnoreCase);
                    });

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return;
        }

        if (engines.Any(
                engine =>
                    string.Equals(
                        engine.ExecutablePath,
                        executablePath,
                        StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        engines.Add(
            new Engine
            {
                Name = "Ironwail",
                ExecutablePath = executablePath,
                Game = QuakeGame.Quake1
            });
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