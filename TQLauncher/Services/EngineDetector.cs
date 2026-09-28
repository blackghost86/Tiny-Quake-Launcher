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

        string[] executables =
            EnumerateFilesSafe(
                folder,
                "*.exe")
                .ToArray();

        foreach (string executable in executables)
        {
            Engine? engine = IdentifyEngine(executable);

            if (engine != null)
            {
                engines.Add(engine);
            }
        }

        AddQuakeSpasmEngine(
            engines,
            folder);

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

            "QSS-M-w32.exe" => CreateEngine(
                "QSS-M",
                executablePath),

            "QSS-M-w64.exe" => CreateEngine(
                "QSS-M",
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

            // FitzQuake engine is outdated, but still in use.
            "fitzquake.exe" => CreateEngine(
                "FitzQuake",
                executablePath),

            // Last known FitzQuake version released.
            "fitzquake85.exe" => CreateEngine(
                "FitzQuake",
                 executablePath),

            _ => null
        };
    }

    private static void AddQuakeSpasmEngine(
        List<Engine> engines,
        string quakeFolder)
    {
        // Quakespasm may be located inside a dedicated QS folder.
        string? executablePath =
            EnumerateFilesSafe(
                    quakeFolder,
                    "*.exe")
                .FirstOrDefault(
                    path =>
                    {
                        string? directory =
                            Path.GetDirectoryName(path);

                        if (string.IsNullOrWhiteSpace(directory))
                        {
                            return false;
                        }

                        string fileName =
                            Path.GetFileName(path);

                        bool isQuakespasmExecutable =
                            string.Equals(
                                fileName,
                                "quakespasm.exe",
                                StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(
                                fileName,
                                "quakespasm-sdl12.exe",
                                StringComparison.OrdinalIgnoreCase);

                        return isQuakespasmExecutable &&
                            string.Equals(
                                Path.GetFileName(directory),
                                "quakespasm",
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

        string fileName =
            Path.GetFileName(executablePath);

        engines.Add(
            new Engine
            {
                Name = string.Equals(
                    fileName,
                    "quakespasm-sdl12.exe",
                    StringComparison.OrdinalIgnoreCase)
                    ? "Quakespasm SDL"
                    : "Quakespasm",
                ExecutablePath = executablePath,
                Game = QuakeGame.Quake1
            });
    }

    private static void AddQuakeSpasmSpikedEngine(
        List<Engine> engines,
        string quakeFolder)
    {
        // Quakespasm-Spiked may be located inside a QSS folder.
        string? executablePath =
            EnumerateFilesSafe(
                    quakeFolder,
                    "*.exe")
                .FirstOrDefault(
                    path =>
                    {
                        string? directory =
                            Path.GetDirectoryName(path);

                        if (string.IsNullOrWhiteSpace(directory))
                        {
                            return false;
                        }

                        string fileName =
                            Path.GetFileName(path);

                        bool isQuakespasmSpikedExecutable =
                            string.Equals(
                                fileName,
                                "quakespasm-spiked-win32.exe",
                                StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(
                                fileName,
                                "quakespasm-spiked-win64.exe",
                                StringComparison.OrdinalIgnoreCase);

                        return isQuakespasmSpikedExecutable &&
                            string.Equals(
                                Path.GetFileName(directory),
                                "qss",
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
            EnumerateFilesSafe(
                    quakeFolder,
                    "ironwail.exe")
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

    private static IEnumerable<string> EnumerateFilesSafe(
        string rootFolder,
        string searchPattern)
    {
        if (!Directory.Exists(rootFolder))
        {
            yield break;
        }

        Stack<string> folders = new();
        folders.Push(rootFolder);

        while (folders.Count > 0)
        {
            string currentFolder = folders.Pop();

            string folderName =
                Path.GetFileName(
                    currentFolder.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar));

            // Windows system/protected folders that should never be scanned.
            if (string.Equals(
                    folderName,
                    "$Recycle.Bin",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    folderName,
                    "Config.Msi",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    folderName,
                    "PerfLogs",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    folderName,
                    "System Volume Information",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(
                    currentFolder,
                    searchPattern,
                    SearchOption.TopDirectoryOnly);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            foreach (string file in files)
            {
                yield return file;
            }

            string[] subdirectories;
            try
            {
                subdirectories = Directory.GetDirectories(
                    currentFolder,
                    "*",
                    SearchOption.TopDirectoryOnly);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            foreach (string subdirectory in subdirectories)
            {
                string subdirectoryName =
                    Path.GetFileName(
                        subdirectory.TrimEnd(
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar));

                // Exclude Windows system/protected folders.
                if (string.Equals(
                        subdirectoryName,
                        "$Recycle.Bin",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        subdirectoryName,
                        "Config.Msi",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        subdirectoryName,
                        "PerfLogs",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        subdirectoryName,
                        "System Volume Information",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                folders.Push(subdirectory);
            }
        }
    }
}