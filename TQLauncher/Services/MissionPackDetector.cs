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
        // 1. Detect known Quake episodes/folders.
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
        // 2. Detect unknown Quake episodes/folders.
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

            // Don't add known episode folders again.
            if (knownDirectories.Contains(folderName))
            {
                continue;
            }

            // id1 is the base game directory.
            if (string.Equals(
                folderName,
                "id1",
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Custom mods are detected only when progs.dat exists,
            // either directly in the folder or inside a PAK/PK3 archive.
            if (!ContainsProgsDat(directory))
            {
                continue;
            }

            // Custom mod.
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

    private bool ContainsProgsDat(
        string folder)
    {
        // -------------------------------------------------
        // Loose progs.dat directly in the mod folder.
        // -------------------------------------------------

        if (File.Exists(
            Path.Combine(
                folder,
                "progs.dat")))
        {
            return true;
        }

        // -------------------------------------------------
        // progs.dat inside PAK archives.
        // -------------------------------------------------

        string[] pakFiles =
            Directory.GetFiles(
                folder,
                "*.pak",
                SearchOption.AllDirectories);

        foreach (string pakFile in pakFiles)
        {
            if (PakContainsFile(
                pakFile,
                "progs.dat"))
            {
                return true;
            }
        }

        // -------------------------------------------------
        // progs.dat inside PK3/ZIP archives.
        // -------------------------------------------------

        string[] archiveFiles =
            Directory.GetFiles(
                folder,
                "*",
                SearchOption.AllDirectories);

        foreach (string archiveFile in archiveFiles)
        {
            string extension =
                Path.GetExtension(archiveFile);

            if (!string.Equals(
                    extension,
                    ".pk3",
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(
                    extension,
                    ".zip",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (ZipContainsFile(
                archiveFile,
                "progs.dat"))
            {
                return true;
            }
        }

        return false;
    }

    private bool PakContainsFile(
        string pakFile,
        string targetFile)
    {
        try
        {
            using FileStream stream =
                File.OpenRead(pakFile);

            using BinaryReader reader =
                new(stream);

            if (stream.Length < 12)
            {
                return false;
            }

            string magic =
                System.Text.Encoding.ASCII.GetString(
                    reader.ReadBytes(4));

            if (!string.Equals(
                magic,
                "PACK",
                StringComparison.Ordinal))
            {
                return false;
            }

            int directoryOffset =
                reader.ReadInt32();

            int directoryLength =
                reader.ReadInt32();

            if (directoryOffset < 0 ||
                directoryLength < 0 ||
                directoryLength % 64 != 0 ||
                directoryOffset > stream.Length ||
                directoryLength >
                    stream.Length - directoryOffset)
            {
                return false;
            }

            stream.Position =
                directoryOffset;

            int entryCount =
                directoryLength / 64;

            for (int i = 0;
                 i < entryCount;
                 i++)
            {
                byte[] nameBytes =
                    reader.ReadBytes(56);

                if (nameBytes.Length != 56)
                {
                    return false;
                }

                string entryName =
                    DecodePakString(nameBytes);

                // PAK directory entry:
                // 56 bytes name + 4 bytes offset + 4 bytes size.
                _ = reader.ReadInt32();
                _ = reader.ReadInt32();

                if (string.Equals(
                    Path.GetFileName(entryName),
                    targetFile,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Ignore invalid or unreadable PAK files.
        }

        return false;
    }

    private bool ZipContainsFile(
        string archiveFile,
        string targetFile)
    {
        try
        {
            using ZipArchive archive =
                ZipFile.OpenRead(archiveFile);

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (string.Equals(
                    Path.GetFileName(entry.FullName),
                    targetFile,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Ignore invalid or unreadable PK3/ZIP files.
        }

        return false;
    }

    private static string DecodePakString(
        byte[] bytes)
    {
        int length = 0;

        while (length < bytes.Length &&
               bytes[length] != 0)
        {
            length++;
        }

        return System.Text.Encoding.ASCII.GetString(
            bytes,
            0,
            length);
    }

}