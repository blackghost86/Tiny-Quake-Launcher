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

            "q2repro.exe" => CreateEngine(
                "Q2REPro",
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
            // Sometimes these folders can be present or hidden.
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