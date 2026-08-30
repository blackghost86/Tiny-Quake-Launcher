using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using TinyQuakeLauncher.Data;
using TinyQuakeLauncher.Models;

namespace TinyQuakeLauncher.Services;

public class DemoDetector
{
    private const int ProtocolNetQuake = 15;
    private const int ProtocolFitzQuake = 666;
    private const int ProtocolRmq = 999;

    private const int ProtocolFte =
        ('F' << 0) +
        ('T' << 8) +
        ('E' << 16) +
        ('X' << 24);

    private const int ProtocolFte2 =
        ('F' << 0) +
        ('T' << 8) +
        ('E' << 16) +
        ('2' << 24);

    private const int GameCoop = 0;
    private const int GameDeathmatch = 1;

    private const int SvcPrint = 8;
    private const int SvcStuffText = 9;
    private const int SvcServerInfo = 11;
    private const int SvcCdTrack = 32;
    private const int SvcModelList = 45;
    private const int SvcSoundList = 46;

    private static readonly HashSet<int> QuakeWorldProtocols =
        new()
        {
            24,
            25,
            26,
            27,
            28
        };

    private static readonly string[] SupportedExtensions =
    {
        ".dem",
        ".mvd",
        ".qwd"
    };

    public List<Demo> DetectDemos(string folder)
    {
        List<Demo> demos = new();

        if (!Directory.Exists(folder))
        {
            return demos;
        }

        // ---------------------------------------------------------
        // Loose demos
        // ---------------------------------------------------------

        AddLooseDemos(
            folder,
            demos);

        string demosFolder =
            Path.Combine(
                folder,
                "demos");

        if (Directory.Exists(demosFolder))
        {
            AddLooseDemos(
                demosFolder,
                demos);
        }

        // ---------------------------------------------------------
        // Demos stored in PAK files.
        // ---------------------------------------------------------

        AddPakDemos(
            folder,
            demos);

        // ---------------------------------------------------------
        // Demos stored in PK3/ZIP files.
        // ---------------------------------------------------------

        AddZipDemos(
            folder,
            demos);

        // ---------------------------------------------------------
        // Remove duplicate entries.
        //
        // The original launcher effectively de-duplicates demos
        // based on their map path/title information.
        // ---------------------------------------------------------

        Dictionary<string, Demo> unique =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (Demo demo in demos)
        {
            string key =
                BuildDuplicateKey(demo);

            if (!unique.ContainsKey(key))
            {
                unique.Add(key, demo);
            }
        }

        return unique.Values
            .OrderBy(
                demo => demo.Name,
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // =============================================================
    // LOOSE FILES
    // =============================================================

    private void AddLooseDemos(
        string folder,
        List<Demo> demos)
    {
        string[] files;

        try
        {
            files =
                Directory.GetFiles(
                    folder,
                    "*",
                    SearchOption.TopDirectoryOnly);
        }
        catch
        {
            return;
        }

        foreach (string file in files)
        {
            if (!IsSupportedDemo(file))
            {
                continue;
            }

            Demo? demo =
                ParseFileDemo(
                    file,
                    folder);

            if (demo != null)
            {
                demos.Add(demo);
            }
        }
    }

    private Demo? ParseFileDemo(
        string file,
        string gameDirectory)
    {
        try
        {
            using FileStream stream =
                File.OpenRead(file);

            using BinaryReader reader =
                new(
                    stream,
                    Encoding.ASCII,
                    leaveOpen: false);

            DemoInfo? info =
                ParseDemo(
                    Path.GetFileName(file),
                    reader);

            if (info == null)
            {
                return null;
            }

            string fileName =
                Path.GetFileName(file);

            return CreateDemo(
                fileName,
                gameDirectory,
                info,
                DemoResourceType.Folder,
                file);
        }
        catch
        {
            return null;
        }
    }

    // =============================================================
    // PAK FILES
    // =============================================================

    private void AddPakDemos(
        string folder,
        List<Demo> demos)
    {
        string[] pakFiles;

        try
        {
            pakFiles =
                Directory.GetFiles(
                    folder,
                    "*.pak",
                    SearchOption.TopDirectoryOnly);
        }
        catch
        {
            return;
        }

        foreach (string pakFile in pakFiles)
        {
            ReadPakFile(
                pakFile,
                folder,
                demos);
        }
    }

    private void ReadPakFile(
        string pakFile,
        string gameDirectory,
        List<Demo> demos)
    {
        try
        {
            using FileStream stream =
                File.OpenRead(pakFile);

            using BinaryReader reader =
                new(
                    stream,
                    Encoding.ASCII,
                    leaveOpen: false);

            // PAK header:
            //
            // char[4] "PACK"
            // int32 directory offset
            // int32 directory length

            if (stream.Length < 12)
            {
                return;
            }

            string magic =
                Encoding.ASCII.GetString(
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
                directoryOffset > stream.Length ||
                directoryLength > stream.Length - directoryOffset)
            {
                return;
            }

            if (directoryLength % 64 != 0)
            {
                return;
            }

            int entryCount =
                directoryLength / 64;

            stream.Position =
                directoryOffset;

            for (int i = 0;
                 i < entryCount;
                 i++)
            {
                byte[] nameBytes =
                    reader.ReadBytes(56);

                if (nameBytes.Length != 56)
                {
                    return;
                }

                string entryName =
                    DecodeCString(
                        nameBytes);

                int entryOffset =
                    reader.ReadInt32();

                int entryLength =
                    reader.ReadInt32();

                if (entryOffset < 0 ||
                    entryLength < 0 ||
                    entryOffset > stream.Length ||
                    entryLength > stream.Length - entryOffset)
                {
                    continue;
                }

                if (!IsSupportedDemoPath(entryName))
                {
                    continue;
                }

                long savedPosition =
                    stream.Position;

                try
                {
                    stream.Position =
                        entryOffset;

                    byte[] data =
                        reader.ReadBytes(entryLength);

                    if (data.Length != entryLength)
                    {
                        continue;
                    }

                    using MemoryStream demoStream =
                        new(data);

                    using BinaryReader demoReader =
                        new(
                            demoStream,
                            Encoding.ASCII,
                            leaveOpen: false);

                    DemoInfo? info =
                        ParseDemo(
                            Path.GetFileName(entryName),
                            demoReader);

                    if (info == null)
                    {
                        continue;
                    }

                    Demo demo =
                        CreateDemo(
                            Path.GetFileName(entryName),
                            gameDirectory,
                            info,
                            DemoResourceType.Pak,
                            pakFile);

                    demos.Add(demo);
                }
                finally
                {
                    stream.Position =
                        savedPosition;
                }
            }
        }
        catch
        {
            // Ignore invalid PAK files.
        }
    }

    // =============================================================
    // PK3 / ZIP FILES
    // =============================================================

    private void AddZipDemos(
        string folder,
        List<Demo> demos)
    {
        string[] files;

        try
        {
            files =
                Directory.GetFiles(
                    folder,
                    "*",
                    SearchOption.TopDirectoryOnly);
        }
        catch
        {
            return;
        }

        foreach (string file in files)
        {
            string extension =
                Path.GetExtension(file);

            if (!extension.Equals(
                    ".pk3",
                    StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(
                    ".zip",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ReadZipFile(
                file,
                folder,
                demos);
        }
    }

    private void ReadZipFile(
        string archiveFile,
        string gameDirectory,
        List<Demo> demos)
    {
        try
        {
            using ZipArchive archive =
                ZipFile.OpenRead(archiveFile);

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (!IsSupportedDemoPath(
                        entry.FullName))
                {
                    continue;
                }

                using Stream stream =
                    entry.Open();

                using MemoryStream memory =
                    new();

                stream.CopyTo(memory);

                memory.Position = 0;

                using BinaryReader reader =
                    new(
                        memory,
                        Encoding.ASCII,
                        leaveOpen: false);

                DemoInfo? info =
                    ParseDemo(
                        Path.GetFileName(entry.FullName),
                        reader);

                if (info == null)
                {
                    continue;
                }

                demos.Add(
                    CreateDemo(
                        Path.GetFileName(entry.FullName),
                        gameDirectory,
                        info,
                        DemoResourceType.Pk3,
                        archiveFile));
            }
        }
        catch
        {
            // Ignore invalid ZIP/PK3 files.
        }
    }

    // =============================================================
    // DEMO FORMAT DISPATCH
    // =============================================================

    private DemoInfo? ParseDemo(
        string fileName,
        BinaryReader reader)
    {
        string extension =
            Path.GetExtension(fileName);

        if (extension.Equals(
                ".dem",
                StringComparison.OrdinalIgnoreCase))
        {
            return ParseDem(
                reader);
        }

        if (extension.Equals(
                ".mvd",
                StringComparison.OrdinalIgnoreCase))
        {
            return ParseMvd(
                reader);
        }

        if (extension.Equals(
                ".qwd",
                StringComparison.OrdinalIgnoreCase))
        {
            return ParseQwd(
                reader);
        }

        return null;
    }

    // =============================================================
    // STANDARD QUAKE .DEM
    // =============================================================

    private DemoInfo? ParseDem(
        BinaryReader reader)
    {
        // The original reader first skips the 13-byte CD-track
        // string terminated by '\n'.
        if (!SkipUntil(
                reader,
                (byte)'\n',
                13))
        {
            return null;
        }

        string mapTitle = "";
        string mapFileName = "";

        while (
            reader.BaseStream.Position <
            reader.BaseStream.Length)
        {
            if (reader.BaseStream.Length -
                    reader.BaseStream.Position <
                16)
            {
                break;
            }

            int blockLength =
                reader.ReadInt32();

            if (blockLength < 12)
            {
                return null;
            }

            long blockEnd =
                reader.BaseStream.Position +
                blockLength;

            if (blockEnd >
                reader.BaseStream.Length)
            {
                return null;
            }

            // Camera angles.
            if (!CanRead(
                    reader,
                    12,
                    blockEnd))
            {
                return null;
            }

            reader.BaseStream.Position += 12;

            while (
                reader.BaseStream.Position <
                blockEnd)
            {
                int message =
                    reader.ReadByte();

                switch (message)
                {
                    case SvcServerInfo:
                        {
                            int protocol =
                                reader.ReadInt32();

                            // FTE protocol extensions.
                            if (protocol == ProtocolFte ||
                                protocol == ProtocolFte2)
                            {
                                if (!CanRead(
                                        reader,
                                        4,
                                        blockEnd))
                                {
                                    return null;
                                }

                                reader.BaseStream.Position += 4;

                                protocol =
                                    reader.ReadInt32();

                                if (protocol == ProtocolFte2)
                                {
                                    if (!CanRead(
                                            reader,
                                            4,
                                            blockEnd))
                                    {
                                        return null;
                                    }

                                    reader.BaseStream.Position += 4;

                                    protocol =
                                        reader.ReadInt32();
                                }

                                if (!SkipNullString(
                                        reader,
                                        blockEnd,
                                        1024))
                                {
                                    return null;
                                }
                            }

                            if (protocol != ProtocolNetQuake &&
                                protocol != ProtocolFitzQuake &&
                                protocol != ProtocolRmq)
                            {
                                return null;
                            }

                            // RMQ protocol flags.
                            if (protocol == ProtocolRmq)
                            {
                                if (!CanRead(
                                        reader,
                                        4,
                                        blockEnd))
                                {
                                    return null;
                                }

                                reader.BaseStream.Position += 4;
                            }

                            if (!CanRead(
                                    reader,
                                    2,
                                    blockEnd))
                            {
                                return null;
                            }

                            int maxClients =
                                reader.ReadByte();

                            if (maxClients < 1 ||
                                maxClients > 16)
                            {
                                return null;
                            }

                            int gameType =
                                reader.ReadByte();

                            if (gameType != GameCoop &&
                                gameType != GameDeathmatch)
                            {
                                return null;
                            }

                            string? title =
                                ReadQuakeTitle(
                                    reader,
                                    blockEnd);

                            string? map =
                                ReadNullString(
                                    reader,
                                    blockEnd,
                                    2048);

                            if (string.IsNullOrWhiteSpace(map))
                            {
                                return null;
                            }

                            mapTitle =
                                string.IsNullOrWhiteSpace(title)
                                    ? Path.GetFileName(
                                        map)
                                    : title;

                            mapFileName =
                                NormalizeMapPath(
                                    map);

                            return new DemoInfo(
                                mapFileName,
                                mapTitle);
                        }

                    case SvcPrint:
                        if (!SkipNullString(
                                reader,
                                blockEnd,
                                2048))
                        {
                            return null;
                        }

                        break;

                    default:
                        return null;
                }
            }
        }

        return null;
    }

    // =============================================================
    // QUAKEWORLD .QWD
    // =============================================================

    private DemoInfo? ParseQwd(
        BinaryReader reader)
    {
        string game = "";
        string mapTitle = "";
        string mapFileName = "";

        int protocol = 0;

        while (
            reader.BaseStream.Position <
            reader.BaseStream.Length)
        {
            if (!CanRead(
                    reader,
                    9,
                    reader.BaseStream.Length))
            {
                break;
            }

            // float time
            reader.BaseStream.Position += 4;

            int code =
                reader.ReadByte();

            if (code != 1)
            {
                return null;
            }

            int blockLength =
                reader.ReadInt32();

            if (blockLength < 8)
            {
                return null;
            }

            long blockEnd =
                reader.BaseStream.Position +
                blockLength;

            if (blockEnd >
                reader.BaseStream.Length)
            {
                return null;
            }

            uint serverBlockType =
                reader.ReadUInt32();

            if (serverBlockType ==
                uint.MaxValue)
            {
                return null;
            }

            // seq_rel_2
            if (!CanRead(
                    reader,
                    4,
                    blockEnd))
            {
                return null;
            }

            reader.BaseStream.Position += 4;

            while (
                reader.BaseStream.Position <
                blockEnd)
            {
                int message =
                    reader.ReadByte();

                switch (message)
                {
                    case SvcServerInfo:
                        {
                            protocol =
                                reader.ReadInt32();

                            if (!QuakeWorldProtocols.Contains(
                                    protocol))
                            {
                                return null;
                            }

                            if (!CanRead(
                                    reader,
                                    4,
                                    blockEnd))
                            {
                                return null;
                            }

                            // age
                            reader.BaseStream.Position += 4;

                            game =
                                ReadNullString(
                                    reader,
                                    blockEnd,
                                    1024) ?? "";

                            // client
                            if (!CanRead(
                                    reader,
                                    1,
                                    blockEnd))
                            {
                                return null;
                            }

                            reader.BaseStream.Position += 1;

                            mapTitle =
                                ReadQuakeTitle(
                                    reader,
                                    blockEnd) ?? "";

                            if (protocol > 24)
                            {
                                if (!CanRead(
                                        reader,
                                        40,
                                        blockEnd))
                                {
                                    return null;
                                }

                                reader.BaseStream.Position += 40;
                            }

                            break;
                        }

                    case SvcCdTrack:
                        if (!CanRead(
                                reader,
                                1,
                                blockEnd))
                        {
                            return null;
                        }

                        reader.BaseStream.Position += 1;
                        break;

                    case SvcStuffText:
                        if (!SkipNullString(
                                reader,
                                blockEnd,
                                2048))
                        {
                            return null;
                        }

                        break;

                    case SvcModelList:
                        {
                            if (protocol > 25)
                            {
                                if (!CanRead(
                                        reader,
                                        1,
                                        blockEnd))
                                {
                                    return null;
                                }

                                reader.BaseStream.Position += 1;
                            }

                            for (int i = 0; i < 256; i++)
                            {
                                string? model =
                                    ReadNullString(
                                        reader,
                                        blockEnd,
                                        1024);

                                if (string.IsNullOrEmpty(model))
                                {
                                    break;
                                }

                                if (model.EndsWith(
                                        ".bsp",
                                        StringComparison.OrdinalIgnoreCase))
                                {
                                    mapFileName =
                                        NormalizeMapPath(
                                            model);

                                    break;
                                }
                            }

                            if (protocol > 25)
                            {
                                if (!CanRead(
                                        reader,
                                        1,
                                        blockEnd))
                                {
                                    return null;
                                }

                                reader.BaseStream.Position += 1;
                            }

                            break;
                        }

                    case SvcSoundList:
                        {
                            if (protocol > 25)
                            {
                                if (!CanRead(
                                        reader,
                                        1,
                                        blockEnd))
                                {
                                    return null;
                                }

                                reader.BaseStream.Position += 1;
                            }

                            for (int i = 0; i < 256; i++)
                            {
                                string? sound =
                                    ReadNullString(
                                        reader,
                                        blockEnd,
                                        1024);

                                if (string.IsNullOrEmpty(sound))
                                {
                                    break;
                                }
                            }

                            if (protocol > 25)
                            {
                                if (!CanRead(
                                        reader,
                                        1,
                                        blockEnd))
                                {
                                    return null;
                                }

                                reader.BaseStream.Position += 1;
                            }

                            break;
                        }

                    default:
                        return null;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(
                mapFileName))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(
                mapTitle))
        {
            mapTitle =
                Path.GetFileName(
                    mapFileName);
        }

        return new DemoInfo(
            mapFileName,
            mapTitle);
    }

    // =============================================================
    // MVD
    // =============================================================

    private DemoInfo? ParseMvd(
        BinaryReader reader)
    {
        string game = "";
        string mapTitle = "";
        string mapFileName = "";

        int protocol = 0;

        while (
            reader.BaseStream.Position <
            reader.BaseStream.Length)
        {
            if (!CanRead(
                    reader,
                    6,
                    reader.BaseStream.Length))
            {
                break;
            }

            // Original reader skips two unknown bytes.
            reader.BaseStream.Position += 2;

            int blockLength =
                reader.ReadInt32();

            if (blockLength < 0)
            {
                return null;
            }

            long blockEnd =
                reader.BaseStream.Position +
                blockLength;

            if (blockEnd >
                reader.BaseStream.Length)
            {
                return null;
            }

            while (
                reader.BaseStream.Position <
                blockEnd)
            {
                int message =
                    reader.ReadByte();

                switch (message)
                {
                    case SvcServerInfo:
                        {
                            protocol =
                                reader.ReadInt32();

                            if (!QuakeWorldProtocols.Contains(
                                    protocol))
                            {
                                return null;
                            }

                            if (!CanRead(
                                    reader,
                                    4,
                                    blockEnd))
                            {
                                return null;
                            }

                            // age
                            reader.BaseStream.Position += 4;

                            game =
                                ReadNullString(
                                    reader,
                                    blockEnd,
                                    1024) ?? "";

                            if (!CanRead(
                                    reader,
                                    4,
                                    blockEnd))
                            {
                                return null;
                            }

                            reader.BaseStream.Position += 4;

                            mapTitle =
                                ReadQuakeTitle(
                                    reader,
                                    blockEnd) ?? "";

                            if (protocol > 24)
                            {
                                if (!CanRead(
                                        reader,
                                        40,
                                        blockEnd))
                                {
                                    return null;
                                }

                                reader.BaseStream.Position += 40;
                            }

                            break;
                        }

                    case SvcCdTrack:
                        if (!CanRead(
                                reader,
                                1,
                                blockEnd))
                        {
                            return null;
                        }

                        reader.BaseStream.Position += 1;
                        break;

                    case SvcStuffText:
                        if (!SkipNullString(
                                reader,
                                blockEnd,
                                2048))
                        {
                            return null;
                        }

                        break;

                    case SvcModelList:
                        {
                            if (protocol > 25)
                            {
                                if (!CanRead(
                                        reader,
                                        1,
                                        blockEnd))
                                {
                                    return null;
                                }

                                reader.BaseStream.Position += 1;
                            }

                            for (int i = 0; i < 256; i++)
                            {
                                string? model =
                                    ReadNullString(
                                        reader,
                                        blockEnd,
                                        1024);

                                if (string.IsNullOrEmpty(model))
                                {
                                    break;
                                }

                                if (model.EndsWith(
                                        ".bsp",
                                        StringComparison.OrdinalIgnoreCase))
                                {
                                    mapFileName =
                                        NormalizeMapPath(
                                            model);

                                    break;
                                }
                            }

                            if (protocol > 25)
                            {
                                if (!CanRead(
                                        reader,
                                        1,
                                        blockEnd))
                                {
                                    return null;
                                }

                                reader.BaseStream.Position += 1;
                            }

                            break;
                        }

                    case SvcSoundList:
                        {
                            if (protocol > 25)
                            {
                                if (!CanRead(
                                        reader,
                                        1,
                                        blockEnd))
                                {
                                    return null;
                                }

                                reader.BaseStream.Position += 1;
                            }

                            for (int i = 0; i < 256; i++)
                            {
                                string? sound =
                                    ReadNullString(
                                        reader,
                                        blockEnd,
                                        1024);

                                if (string.IsNullOrEmpty(sound))
                                {
                                    break;
                                }
                            }

                            if (protocol > 25)
                            {
                                if (!CanRead(
                                        reader,
                                        1,
                                        blockEnd))
                                {
                                    return null;
                                }

                                reader.BaseStream.Position += 1;
                            }

                            break;
                        }

                    default:
                        return null;
                }
            }

            if (!string.IsNullOrWhiteSpace(
                    mapFileName))
            {
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(
                mapFileName))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(
                mapTitle))
        {
            mapTitle =
                Path.GetFileName(
                    mapFileName);
        }

        return new DemoInfo(
            mapFileName,
            mapTitle);
    }

    // =============================================================
    // DEMO -> MODEL
    // =============================================================

    private static Demo CreateDemo(
    string fileName,
    string gameDirectory,
    DemoInfo info,
    DemoResourceType resourceType,
    string resourcePath)
    {
        string title =
            string.IsNullOrWhiteSpace(
                info.MapTitle)
                ? fileName
                : CapitalizeFirstLetter(info.MapTitle);

        return new Demo
        {
            Name =
                $"{fileName} | {title}",

            FileName =
                fileName,

            GameDirectory =
                gameDirectory,

            MapFileName =
                info.MapFileName,

            MapTitle =
                CapitalizeFirstLetter(info.MapTitle),

            ResourceType =
                resourceType,

            ResourcePath =
                resourcePath
        };
    }

    // =============================================================
    // STRING / BOUNDS HELPERS
    // =============================================================

    private static string? ReadNullString(
        BinaryReader reader,
        long endPosition,
        int maximumLength)
    {
        StringBuilder result =
            new();

        while (
            reader.BaseStream.Position <
            endPosition &&
            result.Length <
            maximumLength)
        {
            byte value =
                reader.ReadByte();

            if (value == 0)
            {
                return result.ToString();
            }

            result.Append(
                (char)value);
        }

        return null;
    }

    private static bool SkipNullString(
        BinaryReader reader,
        long endPosition,
        int maximumLength)
    {
        return ReadNullString(
                   reader,
                   endPosition,
                   maximumLength) != null;
    }

    private static bool SkipUntil(
        BinaryReader reader,
        byte terminator,
        int maximumLength)
    {
        for (int i = 0;
             i < maximumLength;
             i++)
        {
            if (reader.BaseStream.Position >=
                reader.BaseStream.Length)
            {
                return false;
            }

            if (reader.ReadByte() == terminator)
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanRead(
        BinaryReader reader,
        long count,
        long endPosition)
    {
        return count >= 0 &&
               reader.BaseStream.Position <=
                   endPosition &&
               count <=
                   endPosition -
                   reader.BaseStream.Position;
    }

    // =============================================================
    // QUAKE TITLE DECODING
    // =============================================================

    private static string? ReadQuakeTitle(
        BinaryReader reader,
        long endPosition)
    {
        StringBuilder result =
            new();

        byte previous = 0;

        for (int i = 0;
             i < 1024 &&
             reader.BaseStream.Position <
                 endPosition;
             i++)
        {
            byte current =
                reader.ReadByte();

            if (current == 0)
            {
                break;
            }

            // Quake entity strings can contain
            // "\n". Display it as a space.
            if (current == (byte)'n' &&
                previous == (byte)'\\')
            {
                if (result.Length > 0)
                {
                    result.Remove(
                        result.Length - 1,
                        1);
                }

                result.Append(' ');

                previous =
                    current;

                continue;
            }

            if (!(previous == 32 &&
                  current == 32))
            {
                result.Append(
                    DecodeQuakeCharacter(
                        current));
            }

            previous =
                current;
        }

        string value =
            result
                .ToString()
                .Trim();

        return value;
    }

    private static string DecodeQuakeCharacter(
        byte value)
    {
        // Printable ASCII.
        if (value >= 32 &&
            value <= 126)
        {
            return ((char)value)
                .ToString();
        }

        // Title encoding uses values
        // outside normal ASCII.
        if (value >= 128)
        {
            return ((char)value)
                .ToString();
        }

        return " ";
    }

    private static string CapitalizeFirstLetter(
    string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return char.ToUpperInvariant(value[0]) +
               value[1..];
    }

    // =============================================================
    // PATH / FILE HELPERS
    // =============================================================

    private static bool IsSupportedDemo(
        string file)
    {
        return SupportedExtensions.Contains(
            Path.GetExtension(file),
            StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsSupportedDemoPath(
        string path)
    {
        path =
            path.Replace(
                '\\',
                '/');

        return SupportedExtensions.Contains(
            Path.GetExtension(path),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeMapPath(
        string path)
    {
        return path
            .Replace(
                '\\',
                '/')
            .TrimStart('/');
    }

    private static string DecodeCString(
        byte[] data)
    {
        int length = 0;

        while (length < data.Length &&
               data[length] != 0)
        {
            length++;
        }

        return Encoding.ASCII.GetString(
            data,
            0,
            length);
    }

    private static string BuildDuplicateKey(
        Demo demo)
    {
        return
            (demo.MapFileName ?? "") +
            "|" +
            (demo.MapTitle ?? "");
    }

    // =============================================================
    // INTERNAL PARSER RESULT
    // =============================================================

    private sealed record DemoInfo(
        string MapFileName,
        string MapTitle);
}