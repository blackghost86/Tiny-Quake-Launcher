using System.IO;

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

            string fullPath =
                Path.Combine(quakeFolder, directory);

            if (Directory.Exists(fullPath))
            {
                DetectedDirectory = directory;
                return true;
            }
        }

        DetectedDirectory = null;
        return false;
    }

    public override string ToString()
    {
        return Name;
    }
}