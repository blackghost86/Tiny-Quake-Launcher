using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using TinyQuakeLauncher.Data;
using TinyQuakeLauncher.Models;

namespace TinyQuakeLauncher.Services;

public class DemoDetector3
{
    // Quake 3 Arena and Team Arena demo files have several extensions.
    private static readonly string[] DemoExtensions =
    {
        ".dm3",
        ".dm_48",
        ".dm_66",
        ".dm_68"
    };

    // Quake 3 server-message opcodes.
    private const int SvcGamestate = 5;

    // Configstring 0 is CS_SERVERINFO. It contains the mapname key.
    private const int CsServerInfo = 0;

    public List<Demo> DetectDemos(string folder)
    {
        List<Demo> demos = new();

        if (!Directory.Exists(folder))
        {
            return demos;
        }

        // ---------------------------------------------------------
        // 1. Loose DM3 files in the game directory.
        // ---------------------------------------------------------

        AddLooseDemos(
            folder,
            demos);

        // ---------------------------------------------------------
        // 2. Loose DM3 files in a standard demos directory.
        // ---------------------------------------------------------

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
        // 3. DM3 files inside PK3/ZIP archives.
        // ---------------------------------------------------------

        AddPk3Demos(
            folder,
            demos);

        // ---------------------------------------------------------
        // Remove exact duplicates and sort.
        // ---------------------------------------------------------

        return demos
            .GroupBy(
                demo =>
                    $"{demo.FileName}|{demo.MapFileName}|{demo.MapTitle}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
                group
                    .OrderBy(GetResourcePriority)
                    .First())
            .OrderBy(GetDemoExtensionPriority)
            .ThenBy(
                demo => demo.Name,
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // =============================================================
    // Loose demos
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
                    SearchOption.AllDirectories)
                .Where(IsQuake3DemoFile)
                .ToArray();
        }
        catch
        {
            return;
        }

        foreach (string file in files)
        {
            try
            {
                Demo? demo =
                    ParseFileDemo(
                        file,
                        folder);

                // A valid Q3 DM3 file can still be detected even when
                // the optional metadata parser cannot decode its first
                // gamestate message. Detection must not depend on
                // successfully extracting the map name.
                demos.Add(
                    demo ??
                    CreateFallbackDemo(
                        Path.GetFileName(file),
                        folder,
                        DemoResourceType.Folder,
                        file));
            }
            catch
            {
                // Ignore invalid or unreadable demos.
            }
        }
    }

    private static bool IsQuake3DemoFile(string fileName)
    {
        string extension =
            Path.GetExtension(fileName);

        return DemoExtensions.Any(
            demoExtension =>
                extension.Equals(
                    demoExtension,
                    StringComparison.OrdinalIgnoreCase));
    }

    private Demo? ParseFileDemo(
        string file,
        string gameDirectory)
    {
        using FileStream stream =
            File.OpenRead(file);

        using BinaryReader reader =
            new(
                stream,
                Encoding.ASCII);

        DemoInfo? info =
            ParseDm3(
                reader);

        if (info == null)
        {
            return null;
        }

        return CreateDemo(
            Path.GetFileName(file),
            gameDirectory,
            info,
            DemoResourceType.Folder,
            file);
    }

    // =============================================================
    // PK3 / ZIP demos
    // =============================================================

    private void AddPk3Demos(
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
                    SearchOption.AllDirectories);
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

            ReadPk3File(
                file,
                folder,
                demos);
        }
    }

    private void ReadPk3File(
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
                if (!IsQuake3DemoFile(entry.FullName))
                {
                    continue;
                }

                if (entry.Length <= 0)
                {
                    continue;
                }

                using Stream entryStream =
                    entry.Open();

                using MemoryStream memory =
                    new();

                entryStream.CopyTo(memory);
                memory.Position = 0;

                using BinaryReader reader =
                    new(
                        memory,
                        Encoding.ASCII);

                DemoInfo? info =
                    ParseDm3(
                        reader);

                // The demo is still a valid launcher resource even if
                // metadata extraction fails. Keep it visible so it
                // can be selected and launched.
                demos.Add(
                    info != null
                        ? CreateDemo(
                            Path.GetFileName(entry.FullName),
                            gameDirectory,
                            info,
                            DemoResourceType.Pk3,
                            archiveFile)
                        : CreateFallbackDemo(
                            Path.GetFileName(entry.FullName),
                            gameDirectory,
                            DemoResourceType.Pk3,
                            archiveFile));
            }
        }
        catch
        {
            // Ignore invalid or unreadable PK3 files.
        }
    }

    // =============================================================
    // Quake 3 DM3 parser
    // =============================================================

    private static DemoInfo? ParseDm3(
        BinaryReader reader)
    {
        Stream stream =
            reader.BaseStream;

        if (stream.Length < 8)
        {
            return null;
        }

        // A Quake 3 (Q3A or Q3TA) demo is a sequence of:
        //
        //   int32 serverMessageSequence
        //   int32 messageLength
        //   byte[] message
        //
        // The first message normally contains svc_gamestate,
        // including CS_SERVERINFO where the map name is stored.
        //
        // We inspect the early messages for CS_SERVERINFO rather
        // than depending on a particular protocol number.

        stream.Position = 0;

        const int MaxMessagesToInspect = 16;
        const int MaxMessageSize = 16 * 1024 * 1024;

        for (int messageIndex = 0;
             messageIndex < MaxMessagesToInspect &&
             stream.Position + 8 <= stream.Length;
             messageIndex++)
        {
            int sequence =
                reader.ReadInt32();

            int messageLength =
                reader.ReadInt32();

            _ = sequence;

            if (messageLength < 0 ||
                messageLength > MaxMessageSize ||
                stream.Position + messageLength > stream.Length)
            {
                return null;
            }

            byte[] message =
                reader.ReadBytes(messageLength);

            if (message.Length != messageLength)
            {
                return null;
            }

            string? mapName =
                FindMapNameInGameState(
                    message);

            if (!string.IsNullOrWhiteSpace(mapName))
            {
                string normalizedMap =
                    NormalizeMapPath(mapName);

                return new DemoInfo(
                    normalizedMap,
                    CreateMapTitle(normalizedMap));
            }
        }

        return null;
    }

    private static string? FindMapNameInGameState(
        byte[] message)
    {
        // Look for svc_gamestate and then walk the configstrings contained
        // in that gamestate. A Q3 configstring command is:
        //
        //   svc_configstring
        //   int16 index
        //   char[] value terminated by '\0'
        //
        // We only need configstring 0 (CS_SERVERINFO).
        //
        // The exact placement of the gamestate payload can vary
        // slightly between engine builds, so the parser is
        // deliberately bounded and conservative.

        for (int start = 0;
             start < message.Length;
             start++)
        {
            if (message[start] != SvcGamestate)
            {
                continue;
            }

            int position = start + 1;

            // Q3 gamestate begins with a clientCommandSequence.
            if (!CanRead(
                    message,
                    position,
                    4))
            {
                continue;
            }

            position += 4;

            while (position < message.Length)
            {
                byte command =
                    message[position++];

                // svc_configstring
                if (command == 6)
                {
                    if (!CanRead(
                            message,
                            position,
                            2))
                    {
                        break;
                    }

                    short index =
                        BitConverter.ToInt16(
                            message,
                            position);

                    position += 2;

                    string? value =
                        ReadNullString(
                            message,
                            ref position);

                    if (value == null)
                    {
                        break;
                    }

                    if (index == CsServerInfo)
                    {
                        string? mapName =
                            GetInfoValue(
                                value,
                                "mapname");

                        if (!string.IsNullOrWhiteSpace(mapName))
                        {
                            return mapName;
                        }
                    }

                    continue;
                }

                // svc_baseline
                if (command == 7)
                {
                    // Baseline data follows and is variable-length,
                    // so it's not safe to infer a complete message
                    // layout here. Stop this gamestate scan.
                    break;
                }

                // svc_EOF terminates the gamestate.
                if (command == 8)
                {
                    break;
                }

                // Unknown command: this is not a reliable gamestate
                // candidate.
                break;
            }
        }

        return null;
    }

    // =============================================================
    // Info-string helpers
    // =============================================================

    private static string? GetInfoValue(
        string info,
        string key)
    {
        if (string.IsNullOrWhiteSpace(info))
        {
            return null;
        }

        string[] parts =
            info.Split(
                '\\',
                StringSplitOptions.None);

        for (int i = 0;
             i + 1 < parts.Length;
             i++)
        {
            if (string.Equals(
                    parts[i],
                    key,
                    StringComparison.OrdinalIgnoreCase))
            {
                return parts[i + 1];
            }
        }

        return null;
    }

    private static string? ReadNullString(
        byte[] data,
        ref int position)
    {
        int start = position;

        while (position < data.Length)
        {
            if (data[position++] == 0)
            {
                return Encoding.ASCII.GetString(
                    data,
                    start,
                    position - start - 1);
            }
        }

        return null;
    }

    // =============================================================
    // Demo -> model
    // =============================================================

    private static Demo CreateFallbackDemo(
        string fileName,
        string gameDirectory,
        DemoResourceType resourceType,
        string resourcePath)
    {
        string title =
            Path.GetFileNameWithoutExtension(fileName);

        if (string.IsNullOrWhiteSpace(title))
        {
            title = fileName;
        }

        title =
            CapitalizeFirstLetter(title);

        return new Demo
        {
            Name =
                $"{fileName} | {title}",

            FileName = fileName,
            GameDirectory = gameDirectory,
            MapFileName = string.Empty,
            MapTitle = title,
            ResourceType = resourceType,
            ResourcePath = resourcePath
        };
    }

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
                // Q3 demo titles are always capitalized.
                : CapitalizeFirstLetter(
                    info.MapTitle);

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
                CapitalizeFirstLetter(
                    info.MapTitle),

            ResourceType =
                resourceType,

            ResourcePath =
                resourcePath
        };
    }

    private static string CreateMapTitle(
        string mapFileName)
    {
        string title =
            Path.GetFileNameWithoutExtension(
                mapFileName);

        return string.IsNullOrWhiteSpace(title)
            ? mapFileName
            : title;
    }

    // =============================================================
    // Display / duplicate helpers
    // =============================================================

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

    private static int GetDemoExtensionPriority(
        Demo demo)
    {
        string extension =
            Path.GetExtension(
                demo.FileName);

        return extension.ToLowerInvariant() switch
        {
            ".dm3" => 0,
            ".dm_48" => 1,
            ".dm_66" => 2,
            ".dm_68" => 3,
            _ => 4
        };
    }

    private static int GetResourcePriority(
        Demo demo)
    {
        return demo.ResourceType switch
        {
            DemoResourceType.Folder => 0,
            DemoResourceType.Pk3 => 1,
            _ => 2
        };
    }

    // =============================================================
    // Binary / path helpers
    // =============================================================

    private static bool CanRead(
        byte[] data,
        int position,
        int count)
    {
        return position >= 0 &&
               count >= 0 &&
               position <= data.Length &&
               count <= data.Length - position;
    }

    private static string NormalizeMapPath(
        string path)
    {
        return path
            .Replace('\\', '/')
            .TrimStart('/');
    }

    private sealed record DemoInfo(
        string MapFileName,
        string MapTitle);
}