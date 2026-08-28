using System.IO;
using System.IO.Compression;
using TinyQuakeLauncher.Models;

namespace TinyQuakeLauncher.Services;

public class MissionPackDetector
{
    private readonly List<MissionPack> allMissionPacks =
        new()
        {
            new MissionPack
            {
                Name = "Quake",
                PossibleDirectories = new List<string>
                {
                    "id1"
                }
            },

            new MissionPack
            {
                Name = "Scourge of Armagon",
                PossibleDirectories = new List<string>
                {
                    "hipnotic"
                }
            },

            new MissionPack
            {
                Name = "Dissolution of Eternity",
                PossibleDirectories = new List<string>
                {
                    "rogue"
                }
            },

            new MissionPack
            {
                Name = "Dimension of the Past",
                PossibleDirectories = new List<string>
                {
                    "dopa"
                }
            },

            new MissionPack
            {
                Name = "Dimension of the Machine",
                PossibleDirectories = new List<string>
                {
                    "mg1"
                }
            },

            new MissionPack
            {
                Name = "Dawn of the Machine",
                PossibleDirectories = new List<string>
                {
                    "mg3"
                }
            }
        };

    public List<MissionPack> DetectMissionPacks(
        string quakeFolder)
    {
        List<MissionPack> detected =
            new();

        if (!Directory.Exists(quakeFolder))
        {
            return detected;
        }

        // ---------------------------------------------
        // 1. Detect known Quake episodes/mods.
        // ---------------------------------------------

        foreach (MissionPack missionPack in allMissionPacks)
        {
            missionPack.DetectedDirectory = null;

            if (missionPack.TryDetectDirectory(quakeFolder))
            {
                detected.Add(missionPack);
            }
        }

        // ---------------------------------------------
        // 2. Detect unknown Quake episodes/mods.
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
                quakeFolder,
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

            // id1 is the Vanilla Quake game directory.
            if (string.Equals(
                folderName,
                "id1",
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!ContainsQuakeContent(directory))
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

    private bool ContainsQuakeContent(
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

        // Check for BSP maps inside PK3 files.
        string[] pk3Files =
            Directory.GetFiles(
                folder,
                "*.pk3",
                SearchOption.TopDirectoryOnly);

        foreach (string pk3File in pk3Files)
        {
            try
            {
                using ZipArchive archive =
                    ZipFile.OpenRead(pk3File);

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string entryPath =
                        entry.FullName.Replace('\\', '/');

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
                // Ignore invalid or unreadable PK3 files
                // and continue checking other content.
            }
        }

        return false;
    }
}