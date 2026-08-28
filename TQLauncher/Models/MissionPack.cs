using System.IO;
using System.IO.Compression;

namespace TinyQuakeLauncher.Models;

public class MissionPack
{
    public string Name { get; set; } = "";

    public List<string> PossibleDirectories { get; set; } = new();

    public string? DetectedDirectory { get; set; }

    public string GameDirectory
    {
        get
        {
            return DetectedDirectory ?? "";
        }
    }

    public bool TryDetectDirectory(string quakeFolder)
    {
        foreach (string directory in PossibleDirectories)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                if (Directory.Exists(quakeFolder))
                {
                    DetectedDirectory = "";
                    return true;
                }

                continue;
            }

            // ---------------------------------------------
            // Check the main Quake game directory.
            // ---------------------------------------------

            string fullPath =
                Path.Combine(
                    quakeFolder,
                    directory);

            if (Directory.Exists(fullPath))
            {
                DetectedDirectory = directory;
                return true;
            }

            // ---------------------------------------------
            // Check for a PK3 archive directly in the
            // main Quake folder.
            // ---------------------------------------------

            string pk3Path =
                Path.Combine(
                    quakeFolder,
                    directory + ".pk3");

            if (File.Exists(pk3Path) &&
                ContainsQuakeMapInArchive(pk3Path))
            {
                DetectedDirectory = "";
                return true;
            }

            // ---------------------------------------------
            // Check for a ZIP archive directly in the
            // main Quake folder.
            // ---------------------------------------------

            string zipPath =
                Path.Combine(
                    quakeFolder,
                    directory + ".zip");

            if (File.Exists(zipPath) &&
                ContainsQuakeMapInArchive(zipPath))
            {
                DetectedDirectory = "";
                return true;
            }
        }

        DetectedDirectory = null;
        return false;
    }

    private static bool ContainsQuakeMapInArchive(
    string archivePath)
    {
        try
        {
            using ZipArchive archive =
                ZipFile.OpenRead(archivePath);

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string entryPath =
                    entry.FullName
                        .Replace('\\', '/')
                        .TrimStart('/');

                if (entryPath.StartsWith(
                        "maps/",
                        StringComparison.OrdinalIgnoreCase) &&
                    entryPath.EndsWith(
                        ".bsp",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Ignore invalid or unreadable archives.
        }

        return false;
    }

    public override string ToString()
    {
        return Name;
    }
}