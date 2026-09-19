using System.IO;
using System.IO.Compression;
using System.Linq;
using TinyQuakeLauncher.Models;

namespace TinyQuakeLauncher.Services;

public class MissionPackDetector3
{
    private readonly List<MissionPack> allMissionPacks =
        new()
        {
            new MissionPack
            {
                Name = "Quake III Arena",
                PossibleDirectories = new List<string>
                {
                    "baseq3"
                }
            },

            new MissionPack
            {
                Name = "Quake III: Team Arena",
                PossibleDirectories = new List<string>
                {
                    "missionpack"
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
        // 1. Detect the standard Quake 3 folders.
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
        // 2. Detect root-level PK3/ZIP episode archives.
        // ---------------------------------------------

        foreach (string archiveFile in Directory.GetFiles(
            quakeFolder,
            "*",
            SearchOption.TopDirectoryOnly))
        {
            string extension =
                Path.GetExtension(archiveFile);

            if (!IsPk3OrZip(extension))
            {
                continue;
            }

            if (!ContainsQuake3ArchiveContent(archiveFile))
            {
                continue;
            }

            string archiveName =
                Path.GetFileNameWithoutExtension(archiveFile);

            if (string.IsNullOrWhiteSpace(archiveName))
            {
                continue;
            }

            // These names represent the standard game folders,
            // not separate archive-based episodes.
            if (string.Equals(
                    archiveName,
                    "baseq3",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    archiveName,
                    "missionpack",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            detected.Add(
                new MissionPack
                {
                    Name = archiveName,
                    PossibleDirectories =
                        new List<string>
                        {
                            archiveName
                        },
                    DetectedDirectory = archiveFile
                });
        }

        // ---------------------------------------------
        // 3. Detect custom Quake 3 directories.
        // ---------------------------------------------

        HashSet<string> knownDirectories =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (MissionPack missionPack in detected)
        {
            string? detectedDirectory =
                missionPack.DetectedDirectory;

            if (string.IsNullOrWhiteSpace(
                detectedDirectory))
            {
                continue;
            }

            string name =
                Path.GetFileName(
                    detectedDirectory.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar));

            if (!string.IsNullOrWhiteSpace(name))
            {
                knownDirectories.Add(name);
            }
        }

        foreach (string directory in Directory.GetDirectories(
            quakeFolder,
            "*",
            SearchOption.TopDirectoryOnly))
        {
            string folderName =
                Path.GetFileName(
                    directory.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar));

            if (string.IsNullOrWhiteSpace(folderName) ||
                knownDirectories.Contains(folderName))
            {
                continue;
            }

            if (string.Equals(
                    folderName,
                    "baseq3",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    folderName,
                    "missionpack",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!ContainsQuake3Content(directory))
            {
                continue;
            }

            // GameDirectory is intentionally not assigned here:
            // the MissionPack model exposes it as read-only.
            detected.Add(
                new MissionPack
                {
                    Name = folderName,
                    PossibleDirectories =
                        new List<string>
                        {
                            folderName
                        },
                    DetectedDirectory = folderName
                });
        }

        return detected
            .GroupBy(
                missionPack =>
                    missionPack.DetectedDirectory ?? missionPack.Name,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static bool ContainsQuake3Content(
        string folder)
    {
        if (!Directory.Exists(folder))
        {
            return false;
        }

        // Loose Quake 3 maps.
        string mapsFolder =
            Path.Combine(
                folder,
                "maps");

        if (Directory.Exists(mapsFolder) &&
            Directory.GetFiles(
                mapsFolder,
                "*.bsp",
                SearchOption.TopDirectoryOnly).Length > 0)
        {
            return true;
        }

        // Common Quake 3 game-code files.
        string[] gameCodeNames =
        {
            "qagamex86.dll",
            "qagamex86_64.dll",
            "qagame.dll",
            "cgamex86.dll",
            "cgamex86_64.dll",
            "cgame.dll",
            "uiq3x86.dll",
            "uiq3x86_64.dll",
            "ui.dll"
        };

        foreach (string gameCodeName in gameCodeNames)
        {
            if (File.Exists(
                Path.Combine(
                    folder,
                    gameCodeName)))
            {
                return true;
            }
        }

        // A Quake 3 mod/episode directory can itself contain
        // PK3/ZIP archives with its maps or game code.
        foreach (string archiveFile in Directory.GetFiles(
            folder,
            "*",
            SearchOption.TopDirectoryOnly))
        {
            if (!IsPk3OrZip(
                Path.GetExtension(archiveFile)))
            {
                continue;
            }

            if (ContainsQuake3ArchiveContent(archiveFile))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsQuake3ArchiveContent(
        string archiveFile)
    {
        try
        {
            using ZipArchive archive =
                ZipFile.OpenRead(archiveFile);

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string entryPath =
                    entry.FullName.Replace(
                        '\\',
                        '/');

                if (entryPath.StartsWith(
                        "maps/",
                        StringComparison.OrdinalIgnoreCase) &&
                    entryPath.EndsWith(
                        ".bsp",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                string fileName =
                    Path.GetFileName(entryPath);

                if (IsQuake3GameCodeFile(fileName))
                {
                    return true;
                }
            }
        }
        catch (InvalidDataException)
        {
            // Ignore invalid/corrupt PK3/ZIP files.
        }
        catch (IOException)
        {
            // Ignore archives that cannot be accessed.
        }
        catch (UnauthorizedAccessException)
        {
            // Ignore archives we cannot access.
        }

        return false;
    }

    private static bool IsQuake3GameCodeFile(
        string fileName)
    {
        return string.Equals(
                   fileName,
                   "qagamex86.dll",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   fileName,
                   "qagamex86_64.dll",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   fileName,
                   "qagame.dll",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   fileName,
                   "cgamex86.dll",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   fileName,
                   "cgamex86_64.dll",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   fileName,
                   "cgame.dll",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   fileName,
                   "uiq3x86.dll",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   fileName,
                   "uiq3x86_64.dll",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   fileName,
                   "ui.dll",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPk3OrZip(
        string extension)
    {
        return string.Equals(
                   extension,
                   ".pk3",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   extension,
                   ".zip",
                   StringComparison.OrdinalIgnoreCase);
    }
}