using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using TinyQuakeLauncher.Data;
using TinyQuakeLauncher.Games;
using TinyQuakeLauncher.Models;
using TinyQuakeLauncher.Services;

namespace TinyQuakeLauncher;

public class Quake2Handler
{
    public static bool IsQuake2Executable(
        string executablePath)
    {
        return string.Equals(
            Path.GetFileName(executablePath),
            "quake2.exe",
            StringComparison.OrdinalIgnoreCase);
    }

    public static List<MissionPack> DetectClassicQuake2Folders(
        string quakeFolder)
    {
        List<MissionPack> missionPacks =
            new();

        if (!Directory.Exists(quakeFolder))
        {
            return missionPacks;
        }

        //Vanilla Quake 2 and its official mission packs.
        string[] knownDirectories =
        {
            "baseq2",
            "xatrix",
            "rogue"
        };

        string[] knownNames =
        {
            "Quake II",
            "The Reckoning",
            "Ground Zero"
        };

        // Only these official Quake 2 folders are displayed.
        for (int i = 0; i < knownDirectories.Length; i++)
        {
            string directory =
                knownDirectories[i];

            string episodePath =
                Path.Combine(
                    quakeFolder,
                    directory);

            if (!Directory.Exists(episodePath))
            {
                continue;
            }

            missionPacks.Add(
                new MissionPack
                {
                    Name = knownNames[i],
                    PossibleDirectories =
                        new List<string>
                        {
                            directory
                        },
                    DetectedDirectory =
                        directory
                });
        }

        return missionPacks;
    }

    private static string? DecodeQuake2DemoTitle(
        byte[] data)
    {
        try
        {
            using MemoryStream stream =
                new(data);
            return GetQuake2DemoTitleFromStream(
                stream);
        }
        catch
        {
            return null;
        }
    }

    private static bool ContainsGameDirectory(
        string folder,
        QuakeGame game)
    {
        string[] directories =
            game == QuakeGame.Quake2
                ? new[]
                {
                    "baseq2",
                    "xatrix",
                    "rogue"
                }
                : new[]
                {
                    "id1",
                    "hipnotic",
                    "rogue",
                    "ctf",
                    "rerelease"
                };

        return directories.Any(
            directory =>
                Directory.Exists(
                    Path.Combine(folder, directory)));
    }

    public string GetEngineGameFolder(
        Engine engine,
        string quakeFolder)
    {
        string? engineDirectory =
            Path.GetDirectoryName(
                engine.ExecutablePath);

        if (string.IsNullOrWhiteSpace(engineDirectory) ||
            !Directory.Exists(engineDirectory) ||
            !Directory.Exists(quakeFolder))
        {
            return quakeFolder;
        }

        string rootFolder =
            Path.GetFullPath(quakeFolder)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

        string currentFolder =
            Path.GetFullPath(engineDirectory)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

        // If the engine executable is outside the selected folder, the
        // selected folder remains the game-data root.
        bool isBelowSelectedFolder =
            string.Equals(
                currentFolder,
                rootFolder,
                StringComparison.OrdinalIgnoreCase) ||
            currentFolder.StartsWith(
                rootFolder + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase) ||
            currentFolder.StartsWith(
                rootFolder + Path.AltDirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);

        if (!isBelowSelectedFolder)
        {
            return quakeFolder;
        }

        // Walk upward from the engine executable directory and use the
        // nearest folder containing a recognized Quake 2 game directory.
        // This resolves each engine to its own installation when the user
        // selects a parent folder containing multiple Quake 2 installs.
        string candidate = currentFolder;

        while (true)
        {
            if (ContainsGameDirectory(
                    candidate,
                    QuakeGame.Quake2))
            {
                return candidate;
            }

            if (string.Equals(
                    candidate,
                    rootFolder,
                    StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            DirectoryInfo? parent =
                Directory.GetParent(candidate);

            if (parent == null)
            {
                break;
            }

            string parentFolder =
                parent.FullName
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);

            // Do not walk above the folder selected by the user.
            if (!string.Equals(
                    parentFolder,
                    rootFolder,
                    StringComparison.OrdinalIgnoreCase) &&
                !parentFolder.StartsWith(
                    rootFolder + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) &&
                !parentFolder.StartsWith(
                    rootFolder + Path.AltDirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            candidate = parentFolder;
        }

        // No recognized Quake 2 game directory was found. Keep the selected
        // folder rather than guessing, so map/episode detection cannot
        // accidentally combine unrelated installations.
        return quakeFolder;
    }

    public List<MissionPack> DetectMissionPacks(
        Engine engine,
        string detectionFolder,
        MissionPackDetector2 missionPackDetector2)
    {
        // Q2Pro-NG stores the original Quake 2 episodes together in the
        // baseq2 directory. Expose those episodes separately in the
        // launcher while keeping baseq2 as their shared game directory.
        if (string.Equals(
            engine.Name,
            "Q2Pro-NG",
            StringComparison.OrdinalIgnoreCase))
        {
            return DetectQ2ProNgEpisodes(
                detectionFolder);
        }
        if (string.Equals(engine.Name, "Quake II (Steam)", StringComparison.OrdinalIgnoreCase))
        {
            return DetectSteamQuake2Episodes(detectionFolder);
        }


        if (string.Equals(
            engine.Name,
            "Quake II GOG",
            StringComparison.OrdinalIgnoreCase))
        {
            return missionPackDetector2
                .DetectGogMissionPacks(detectionFolder);
        }

        return missionPackDetector2
            .DetectMissionPacks(detectionFolder);
    }

    private static List<MissionPack> DetectSteamQuake2Episodes(string detectionFolder)
    {
        List<MissionPack> missionPacks = new();
        string baseq2Folder = Path.Combine(detectionFolder, "baseq2");
        if (!Directory.Exists(baseq2Folder)) return missionPacks;
        string[] names = { "Quake II", "The Reckoning", "Ground Zero", "Quake II 64", "Capture the Flag", "Call of the Machine" };
        foreach (string name in names)
        {
            missionPacks.Add(new MissionPack { Name = name, PossibleDirectories = new List<string> { "baseq2" }, DetectedDirectory = "baseq2" });
        }

        string? callOfTheVoidDirectory =
            new[] { "q1q2", "void" }
                .FirstOrDefault(directory =>
                    Directory.Exists(
                        Path.Combine(detectionFolder, directory)));

        // Detect Call of the Void expansion for Quake 2.
        if (callOfTheVoidDirectory != null)
        {
            missionPacks.Add(
                new MissionPack
                {
                    Name = "Call of the Void",
                    PossibleDirectories = new List<string>
                    {
                        callOfTheVoidDirectory
                    },
                    DetectedDirectory = callOfTheVoidDirectory
                });
        }

        return missionPacks;
    }

    private static List<MissionPack> DetectQ2ProNgEpisodes(
        string detectionFolder)
    {
        List<MissionPack> missionPacks =
            new();

        string baseq2Folder =
            Path.Combine(
                detectionFolder,
                "baseq2");

        if (!Directory.Exists(baseq2Folder))
        {
            return missionPacks;
        }

        string[] names =
        {
            "Quake II",
            "The Reckoning",
            "Ground Zero",
            "Quake II 64",
            "Capture the Flag",
            "Call of the Machine"
        };

        foreach (string name in names)
        {
            missionPacks.Add(
                new MissionPack
                {
                    Name = name,
                    PossibleDirectories =
                        new List<string>
                        {
                            "baseq2"
                        },
                    DetectedDirectory =
                        "baseq2"
                });
        }

        return missionPacks;
    }

    public string? GetDefaultMap(
        MissionPack missionPack,
        Engine? engine)
    {
        if (engine?.Game != QuakeGame.Quake2)
        {
            return null;
        }

        if (string.Equals(
                engine.Name,
                "Q2Pro-NG",
                StringComparison.OrdinalIgnoreCase))
        {
            return missionPack.Name switch
            {
                "Quake II" => "base1.bsp",
                "The Reckoning" => "badlands.bsp",
                "Ground Zero" => "rammo1.bsp",
                "Quake II 64" => "outpost.bsp",
                "Capture the Flag" => "q2ctf1.bsp",
                "Call of the Machine" => "mguhub.bsp",
                "Call of the Void" => "voidhub.bsp",
                _ => null
            };
        }

        if (string.Equals(engine.Name, "Quake II (Steam)", StringComparison.OrdinalIgnoreCase))
        {
            return missionPack.Name switch
            {
                "Quake II" => "base1.bsp",
                "The Reckoning" => "badlands.bsp",
                "Ground Zero" => "rammo1.bsp",
                "Quake II 64" => "outpost.bsp",
                "Capture the Flag" => "q2ctf1.bsp",
                "Call of the Machine" => "mguhub.bsp",
                "Call of the Void" => "voidhub.bsp",
                _ => null
            };
        }

        if (!string.Equals(
                engine.Name,
                "Quake II GOG",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return missionPack.Name switch
        {
            "Quake II" => "base1.bsp",
            "The Reckoning" => "badlands.bsp",
            "Ground Zero" => "rammo1.bsp",
            "Quake II 64" => "outpost.bsp",
            "Capture the Flag" => "q2ctf1.bsp",
            "Call of the Machine" => "mguhub.bsp",
            "Call of the Void" => "voidhub.bsp",

            _ => null
        };
    }

    public List<Demo> DetectDemosForEpisode(
        string gameFolder,
        Engine engine,
        MissionPack missionPack)
    {
        List<Demo> demos = new();

        HashSet<string>? allowedDemoNames =
            GetQuake2AllowedDemoNames(
                engine,
                missionPack);

        if (!Directory.Exists(gameFolder))
        {
            return demos;
        }

        // Build a map-title fallback from the same Quake 2 game directory.
        // This is only used when a demo does not contain a readable
        // CS_NAME/configstring.
        Dictionary<string, string> mapTitles =
            new(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (MapInfo map in
                new MapDetector2().DetectMaps(gameFolder))
            {
                string mapName =
                    Path.GetFileNameWithoutExtension(
                        map.FileName);

                if (!string.IsNullOrWhiteSpace(mapName) &&
                    !string.IsNullOrWhiteSpace(map.Title))
                {
                    mapTitles[mapName] =
                        map.Title;
                }
            }
        }
        catch
        {
            // Demo detection must not depend on map detection succeeding.
        }

        HashSet<string> seenDemoNames =
            new(StringComparer.OrdinalIgnoreCase);

        // Loose .dm2 files can live in the normal demos directory or in
        // subdirectories below the selected Quake 2 episode.
        try
        {
            foreach (string demoFile in
                Directory.GetFiles(
                    gameFolder,
                    "*.dm2",
                    SearchOption.AllDirectories))
            {
                string fileName =
                    Path.GetFileName(demoFile);

                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                if (allowedDemoNames != null &&
                    !allowedDemoNames.Contains(fileName))
                {
                    continue;
                }

                if (!seenDemoNames.Add(fileName))
                {
                    continue;
                }

                string? title =
                    GetQuake2DemoTitleFromFile(
                        demoFile);

                string mapName =
                    Path.GetFileNameWithoutExtension(
                        fileName);

                if (string.IsNullOrWhiteSpace(title) &&
                    mapTitles.TryGetValue(
                        mapName,
                        out string? mapTitle))
                {
                    title = mapTitle;
                }

                demos.Add(
                    new Demo
                    {
                        Name =
                            string.IsNullOrWhiteSpace(title)
                                ? fileName
                                : title,
                        FileName = fileName,
                        GameDirectory =
                            Path.GetFileName(gameFolder),
                        ResourceType =
                            DemoResourceType.Folder,
                        ResourcePath = "",
                        MapTitle = title ?? ""
                    });
            }
        }
        catch
        {
            // Ignore inaccessible directories/files.
        }

        // Quake 2 installations commonly keep demos inside PAK or PK3.
        // Search every archive below the selected episode so this
        // also works for GOG, Steam and similar installations.
        try
        {
            foreach (string archiveFile in
                Directory.GetFiles(
                    gameFolder,
                    "*",
                    SearchOption.AllDirectories))
            {
                string extension =
                    Path.GetExtension(archiveFile);

                if (string.Equals(
                        extension,
                        ".pak",
                        StringComparison.OrdinalIgnoreCase))
                {
                    AddQuake2PakDemos(
                        demos,
                        seenDemoNames,
                        archiveFile,
                        gameFolder,
                        mapTitles,
                        allowedDemoNames);
                }
                else if (
                    string.Equals(
                        extension,
                        ".pk3",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        extension,
                        ".zip",
                        StringComparison.OrdinalIgnoreCase))
                {
                    AddQuake2ZipDemos(
                        demos,
                        seenDemoNames,
                        archiveFile,
                        gameFolder,
                        mapTitles,
                        allowedDemoNames);
                }
            }
        }
        catch
        {
            // Ignore inaccessible directories/archives.
        }

        return demos
            .OrderBy(
                demo => demo.FileName,
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HashSet<string>? GetQuake2AllowedDemoNames(
        Engine engine,
        MissionPack missionPack)
    {
        if (engine.Game != QuakeGame.Quake2)
        {
            return null;
        }

        bool isGog =
            string.Equals(
                engine.Name,
                "Quake II GOG",
                StringComparison.OrdinalIgnoreCase);

        bool isSteam =
            string.Equals(engine.Name, "Quake II (Steam)", StringComparison.OrdinalIgnoreCase);

        // Q2Pro-NG uses the shared baseq2 directory for all episodes.
        if (string.Equals(
            engine.Name,
            "Q2Pro-NG",
            StringComparison.OrdinalIgnoreCase))
        {
            return missionPack.Name switch
            {
                "Quake II" =>
                    new HashSet<string>(
                        new[] { "demo1.dm2", "demo2.dm2" },
                        StringComparer.OrdinalIgnoreCase),

                "The Reckoning" =>
                    new HashSet<string>(
                        new[] { "xdemo1.dm2", "xdemo2.dm2", "xdemo3.dm2" },
                        StringComparer.OrdinalIgnoreCase),

                "Ground Zero" =>
                    new HashSet<string>(
                        new[] { "rdemo1.dm2", "rdemo2.dm2" },
                        StringComparer.OrdinalIgnoreCase),

                "Quake II 64" =>
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase),

                "Capture the Flag" =>
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase),

                "Call of the Machine" =>
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase),

                _ => null
            };
        }

        // Quake 2 Steam demos.
        if (isSteam)
        {
            return missionPack.Name switch
            {
                "Quake II" => new HashSet<string>(new[] { "demo1.dm2", "demo2.dm2" }, StringComparer.OrdinalIgnoreCase),
                "The Reckoning" => new HashSet<string>(new[] { "rdemo1.dm2", "rdemo2.dm2" }, StringComparer.OrdinalIgnoreCase),
                "Ground Zero" => new HashSet<string>(new[] { "xdemo1.dm2", "xdemo2.dm2", "xdemo3.dm2" }, StringComparer.OrdinalIgnoreCase),
                "Quake II 64" => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                "Capture the Flag" => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                "Call of the Machine" => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                _ => null
            };
        }

        // Quake 2 GOG demos.
        if (isGog)
        {
            return missionPack.Name switch
            {
                "Quake II" =>
                    new HashSet<string>(
                        new[] { "demo1.dm2", "demo2.dm2" },
                        StringComparer.OrdinalIgnoreCase),

                "The Reckoning" =>
                    new HashSet<string>(
                        new[] { "xdemo1.dm2", "xdemo2.dm2", "xdemo3.dm2" },
                        StringComparer.OrdinalIgnoreCase),

                "Ground Zero" =>
                    new HashSet<string>(
                        // GOG Ground Zero does not include a third demo.
                        new[] { "rdemo1.dm2", "rdemo2.dm2" },
                        StringComparer.OrdinalIgnoreCase),

                "Capture the Flag" =>
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase),

                "Quake II 64" =>
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase),

                "Call of the Machine" =>
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase),

                "Call of the Void" =>
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase),

                _ => null
            };
        }

        // Vanilla Quake 2 demos.
        if (IsQuake2Executable(engine.ExecutablePath))
        {
            return missionPack.Name switch
            {
                "Quake II" =>
                    new HashSet<string>(
                        new[] { "demo1.dm2", "demo2.dm2" },
                        StringComparer.OrdinalIgnoreCase),

                "The Reckoning" =>
                    new HashSet<string>(
                        new[] { "xdemo1.dm2", "xdemo2.dm2", "xdemo3.dm2" },
                        StringComparer.OrdinalIgnoreCase),

                "Ground Zero" =>
                    new HashSet<string>(
                        new[] { "demo1.dm2", "demo2.dm2", "demo3.dm2" },
                        StringComparer.OrdinalIgnoreCase),

                "Capture the Flag" =>
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase),

                _ => null
            };
        }

        return null;
    }

    private static void AddQuake2PakDemos(
        List<Demo> demos,
        HashSet<string> seenDemoNames,
        string pakFile,
        string gameFolder,
        Dictionary<string, string> mapTitles,
        HashSet<string>? allowedDemoNames)
    {
        try
        {
            using FileStream stream =
                File.OpenRead(pakFile);

            using BinaryReader reader =
                new(stream);

            if (stream.Length < 12)
            {
                return;
            }

            string magic =
                System.Text.Encoding.ASCII.GetString(
                    reader.ReadBytes(4));

            if (!string.Equals(
                    magic,
                    "PACK",
                    StringComparison.Ordinal))
            {
                return;
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
                return;
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
                    break;
                }

                string entryName =
                    DecodePakCString(nameBytes);

                int entryOffset =
                    reader.ReadInt32();

                int entryLength =
                    reader.ReadInt32();

                string normalizedEntryName =
                    entryName.Replace(
                        '\\',
                        '/');

                string demoFileName =
                    Path.GetFileName(
                        normalizedEntryName);

                if (string.IsNullOrWhiteSpace(demoFileName) ||
                    !demoFileName.EndsWith(
                        ".dm2",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (allowedDemoNames != null &&
                    !allowedDemoNames.Contains(demoFileName))
                {
                    continue;
                }

                // Quake 2 demos are normally under demos/.
                if (entryOffset < 0 ||
                    entryLength <= 0 ||
                    entryOffset > stream.Length ||
                    entryLength >
                        stream.Length - entryOffset)
                {
                    continue;
                }

                if (!seenDemoNames.Add(
                        demoFileName))
                {
                    continue;
                }

                string? title =
                    GetQuake2DemoTitleFromPakEntry(
                        pakFile,
                        entryOffset,
                        entryLength);

                string mapName =
                    Path.GetFileNameWithoutExtension(
                        demoFileName);

                if (string.IsNullOrWhiteSpace(title) &&
                    mapTitles.TryGetValue(
                        mapName,
                        out string? mapTitle))
                {
                    title = mapTitle;
                }

                demos.Add(
                    new Demo
                    {
                        Name =
                            string.IsNullOrWhiteSpace(title)
                                ? demoFileName
                                : title,
                        FileName =
                            demoFileName,
                        GameDirectory =
                            Path.GetFileName(
                                gameFolder),
                        ResourceType =
                            DemoResourceType.Pak,
                        ResourcePath =
                            pakFile,
                        MapTitle =
                            title ?? ""
                    });
            }
        }
        catch
        {
            // Ignore invalid/inaccessible PAK files.
        }
    }

    private static void AddQuake2ZipDemos(
        List<Demo> demos,
        HashSet<string> seenDemoNames,
        string archiveFile,
        string gameFolder,
        Dictionary<string, string> mapTitles,
        HashSet<string>? allowedDemoNames)
    {
        try
        {
            using ZipArchive archive =
                ZipFile.OpenRead(
                    archiveFile);

            foreach (ZipArchiveEntry entry
                     in archive.Entries)
            {
                string entryPath =
                    entry.FullName.Replace(
                        '\\',
                        '/');

                string demoFileName =
                    Path.GetFileName(
                        entryPath);

                if (string.IsNullOrWhiteSpace(demoFileName) ||
                    !demoFileName.EndsWith(
                        ".dm2",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (allowedDemoNames != null &&
                    !allowedDemoNames.Contains(demoFileName))
                {
                    continue;
                }

                if (!seenDemoNames.Add(
                        demoFileName))
                {
                    continue;
                }

                string? title =
                    GetQuake2DemoTitleFromZipEntry(
                        entry);

                string mapName =
                    Path.GetFileNameWithoutExtension(
                        demoFileName);

                if (string.IsNullOrWhiteSpace(title) &&
                    mapTitles.TryGetValue(
                        mapName,
                        out string? mapTitle))
                {
                    title = mapTitle;
                }

                demos.Add(
                    new Demo
                    {
                        Name =
                            string.IsNullOrWhiteSpace(title)
                                ? demoFileName
                                : title,
                        FileName =
                            demoFileName,
                        GameDirectory =
                            Path.GetFileName(
                                gameFolder),
                        ResourceType =
                            DemoResourceType.Pk3,
                        ResourcePath =
                            archiveFile,
                        MapTitle =
                            title ?? ""
                    });
            }
        }
        catch
        {
            // Ignore invalid/inaccessible ZIP/PK3 files.
        }
    }

    private static string? GetQuake2DemoTitleFromFile(
        string demoFile)
    {
        try
        {
            using FileStream stream =
                File.OpenRead(
                    demoFile);

            return GetQuake2DemoTitleFromStream(
                stream);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetQuake2DemoTitleFromPakEntry(
        string pakFile,
        int entryOffset,
        int entryLength)
    {
        try
        {
            using FileStream stream =
                File.OpenRead(
                    pakFile);

            if (entryOffset < 0 ||
                entryLength <= 0 ||
                entryOffset > stream.Length ||
                entryLength >
                    stream.Length - entryOffset)
            {
                return null;
            }

            stream.Position =
                entryOffset;

            int bytesToRead =
                Math.Min(
                    entryLength,
                    1024 * 1024);

            byte[] data =
                new byte[bytesToRead];

            int totalRead = 0;

            while (totalRead < bytesToRead)
            {
                int read =
                    stream.Read(
                        data,
                        totalRead,
                        bytesToRead - totalRead);

                if (read <= 0)
                {
                    break;
                }

                totalRead += read;
            }

            using MemoryStream demoStream =
                new(
                    data,
                    0,
                    totalRead,
                    false);

            return GetQuake2DemoTitleFromStream(
                demoStream);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetQuake2DemoTitleFromZipEntry(
        ZipArchiveEntry entry)
    {
        try
        {
            using Stream input =
                entry.Open();

            using MemoryStream demoStream =
                new();

            input.CopyTo(
                demoStream,
                1024 * 1024);

            demoStream.Position = 0;

            return GetQuake2DemoTitleFromStream(
                demoStream);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetQuake2DemoTitleFromStream(
        Stream stream)
    {
        try
        {
            if (stream.Length < 5)
            {
                return null;
            }

            long originalPosition =
                stream.Position;

            stream.Position = 0;

            using BinaryReader reader =
                new(
                    stream,
                    System.Text.Encoding.ASCII,
                    true);

            // The first message contains svc_serverdata. The level title
            // itself is normally supplied by CS_NAME (configstring 0).
            // Search the early messages for that configstring instead of
            // assuming the level-string field is already the worldspawn
            // title. This works across the standard Q2 demo protocols.
            const int MaxBytesToInspect =
                1024 * 1024;

            while (reader.BaseStream.Position + 4 <=
                   reader.BaseStream.Length &&
                   reader.BaseStream.Position <
                   MaxBytesToInspect)
            {
                int messageLength =
                    reader.ReadInt32();

                if (messageLength <= 0 ||
                    messageLength >
                        reader.BaseStream.Length -
                        reader.BaseStream.Position)
                {
                    break;
                }

                long messageEnd =
                    reader.BaseStream.Position +
                    messageLength;

                if (messageLength >
                    MaxBytesToInspect)
                {
                    messageLength =
                        (int)Math.Min(
                            messageLength,
                            MaxBytesToInspect);
                }

                byte[] message =
                    reader.ReadBytes(
                        messageLength);

                if (message.Length == 0)
                {
                    break;
                }

                string? title =
                    TryReadQuake2ConfigStringName(
                        message);

                if (!string.IsNullOrWhiteSpace(title))
                {
                    return title;
                }

                // If this is the serverdata message, its level-string is a
                // useful fallback when CS_NAME is absent.
                string? levelName =
                    TryReadQuake2ServerDataLevelName(
                        message);

                if (!string.IsNullOrWhiteSpace(levelName) &&
                    !levelName.EndsWith(
                        ".bsp",
                        StringComparison.OrdinalIgnoreCase))
                {
                    // Do not return immediately: a later CS_NAME can provide
                    // the actual display title.
                }

                // Continue at the next demo message.
                reader.BaseStream.Position =
                    messageEnd;
            }

            // Last-resort fallback: read the serverdata level string.
            stream.Position = 0;

            using BinaryReader fallbackReader =
                new(
                    stream,
                    System.Text.Encoding.ASCII,
                    true);

            if (fallbackReader.BaseStream.Length >= 4)
            {
                int messageLength =
                    fallbackReader.ReadInt32();

                if (messageLength > 0 &&
                    messageLength <=
                        fallbackReader.BaseStream.Length -
                        fallbackReader.BaseStream.Position)
                {
                    byte[] message =
                        fallbackReader.ReadBytes(
                            messageLength);

                    return TryReadQuake2ServerDataLevelName(
                        message);
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryReadQuake2ConfigStringName(
        byte[] message)
    {
        try
        {
            // CS_NAME is configstring 0.
            for (int position = 0;
                 position + 3 < message.Length;
                 position++)
            {
                // svc_configstring
                if (message[position] != 13)
                {
                    continue;
                }

                int index =
                    BitConverter.ToInt16(
                        message,
                        position + 1);

                if (index != 0)
                {
                    continue;
                }

                int stringPosition =
                    position + 3;

                string value =
                    ReadNullTerminatedAscii(
                        message,
                        ref stringPosition);

                if (string.IsNullOrWhiteSpace(value) ||
                    value.Length > 256)
                {
                    continue;
                }

                bool hasUsefulCharacter =
                    value.Any(
                        character =>
                            !char.IsControl(character) &&
                            !char.IsWhiteSpace(character));

                if (hasUsefulCharacter)
                {
                    return value.Trim();
                }
            }
        }
        catch
        {
            // Ignore malformed demo messages.
        }

        return null;
    }

    private static string? TryReadQuake2ServerDataLevelName(
        byte[] message)
    {
        try
        {
            if (message.Length < 2 ||

                // svc_serverdata
                message[0] != 12)
            {
                return null;
            }

            int position = 1;

            if (position + 4 > message.Length)
            {
                return null;
            }

            // protocol
            position += 4;

            if (position + 4 > message.Length)
            {
                return null;
            }

            // server count
            position += 4;

            if (position >= message.Length)
            {
                return null;
            }

            // attract loop
            position++;

            // game directory
            ReadNullTerminatedAscii(
                message,
                ref position);

            if (position + 2 > message.Length)
            {
                return null;
            }

            // player number
            position += 2;

            return ReadNullTerminatedAscii(
                message,
                ref position);
        }
        catch
        {
            return null;
        }
    }

    private static string ReadNullTerminatedAscii(
        byte[] data,
        ref int position)
    {
        int start =
            position;

        while (position < data.Length)
        {
            if (data[position++] == 0)
            {
                int length =
                    position - start - 1;

                if (length <= 0)
                {
                    return "";
                }

                return System.Text.Encoding.UTF8.GetString(
                    data,
                    start,
                    length);
            }
        }

        return "";
    }

    private static string DecodePakCString(byte[] bytes)
    {
        int length =
            Array.IndexOf(bytes, (byte)0);

        if (length < 0)
        {
            length = bytes.Length;
        }

        return System.Text.Encoding.ASCII.GetString(
            bytes,
            0,
            length);
    }
}