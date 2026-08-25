using System.IO;
using TinyQuakeLauncher.Models;

namespace TinyQuakeLauncher.Services;

public class MissionPackDetector2
{
    private readonly List<MissionPack> allMissionPacks =
        new()
        {
            new MissionPack
            {
                Name = "Quake II",
                PossibleDirectories = new List<string>
                {
                    "baseq2"
                }
            },

            new MissionPack
            {
                Name = "The Reckoning",
                PossibleDirectories = new List<string>
                {
                    "xatrix"
                }
            },

            new MissionPack
            {
                Name = "Ground Zero",
                PossibleDirectories = new List<string>
                {
                    "rogue"
                }
            }
        };

    public List<MissionPack> DetectMissionPacks(
        string quake2Folder)
    {
        List<MissionPack> detected =
            new();

        if (!Directory.Exists(quake2Folder))
        {
            return detected;
        }

        // ---------------------------------------------
        // 1. Detect known Quake II episodes.
        // ---------------------------------------------

        foreach (MissionPack missionPack in allMissionPacks)
        {
            missionPack.DetectedDirectory = null;

            if (missionPack.TryDetectDirectory(quake2Folder))
            {
                detected.Add(missionPack);
            }
        }

        // ---------------------------------------------
        // 2. Detect unknown Quake II episodes/mods.
        // ---------------------------------------------

        HashSet<string> knownDirectories =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (MissionPack missionPack in detected)
        {
            if (!string.IsNullOrWhiteSpace(
                missionPack.DetectedDirectory))
            {
                knownDirectories.Add(
                    missionPack.DetectedDirectory);
            }
        }

        string[] directories =
            Directory.GetDirectories(
                quake2Folder,
                "*",
                SearchOption.TopDirectoryOnly);

        foreach (string directory in directories)
        {
            string folderName =
                Path.GetFileName(
                    directory.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar));

            if (string.IsNullOrWhiteSpace(folderName))
            {
                continue;
            }

            // Don't add known episode folders.
            if (knownDirectories.Contains(folderName))
            {
                continue;
            }

            // baseq2 is the main Quake II game directory.
            if (string.Equals(
                folderName,
                "baseq2",
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!ContainsQuake2Content(directory))
            {
                continue;
            }

            detected.Add(
                new MissionPack
                {
                    Name = folderName,
                    PossibleDirectories = new List<string>
                    {
                        folderName
                    },
                    DetectedDirectory = folderName
                });
        }

        return detected;
    }

    private bool ContainsQuake2Content(
        string folder)
    {
        // Check for PAK files directly in the folder.
        if (Directory.GetFiles(
            folder,
            "*.pak",
            SearchOption.TopDirectoryOnly).Length > 0)
        {
            return true;
        }

        // Check for loose BSP maps.
        string mapsFolder =
            Path.Combine(
                folder,
                "maps");

        if (Directory.Exists(mapsFolder))
        {
            if (Directory.GetFiles(
                mapsFolder,
                "*.bsp",
                SearchOption.TopDirectoryOnly).Length > 0)
            {
                return true;
            }
        }

        return false;
    }
}