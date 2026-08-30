using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using TinyQuakeLauncher.Data;
using TinyQuakeLauncher.Models;

namespace TinyQuakeLauncher.Services;

public class DemoDetector2
{
    private const int ProtocolKmq = 56;
    private const int ProtocolR1Q2 = 35;

    private static readonly HashSet<int> Quake2Protocols =
        new()
        {
            25,
            26,
            27,
            28,
            30,
            31,
            32,
            33,
            34
        };

    private const int ServerInfo = 12;
    private const int ConfigString = 13;

    public List<Demo> DetectDemos(string folder)
    {
        List<Demo> demos = new();

        if (!Directory.Exists(folder))
        {
            return demos;
        }

        // ---------------------------------------------------------
        // 1. Loose DM2 files in the game directory.
        // ---------------------------------------------------------

        AddLooseDemos(
            folder,
            demos);

        // ---------------------------------------------------------
        // 2. Loose DM2 files in a standard demos directory.
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
        // 3. DM2 files inside PAK archives.
        // ---------------------------------------------------------

        AddPakDemos(
            folder,
            demos);

        // ---------------------------------------------------------
        // 4. DM2 files inside PK3/ZIP archives.
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
            .OrderBy(
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
                    "*.dm2",
                    SearchOption.TopDirectoryOnly);
        }
        catch
        {
            return;
        }

        foreach (string file in files)
        {
            try
            {
                using FileStream stream =
                    File.OpenRead(file);

                using BinaryReader reader =
                    new(
                        stream,
                        Encoding.ASCII);

                Demo? demo =
                    ReadDm2Info(reader);

                if (demo == null)
                {
                    continue;
                }

                demo.FileName =
                    Path.GetFileName(file);

                demo.GameDirectory =
                    folder;

                demo.ResourceType =
                    DemoResourceType.Folder;

                demo.ResourcePath =
                    file;

                SetDemoName(demo);

                demos.Add(demo);
            }
            catch
            {
                // Ignore invalid or unreadable demos.
            }
        }
    }

    // =============================================================
    // PAK demos
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
                    Encoding.ASCII);

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
                    return;
                }

                string entryName =
                    DecodeCString(nameBytes);

                int entryOffset =
                    reader.ReadInt32();

                int entryLength =
                    reader.ReadInt32();

                if (!entryName.EndsWith(
                    ".dm2",
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (entryOffset < 0 ||
                    entryLength <= 0 ||
                    entryOffset > stream.Length ||
                    entryLength >
                        stream.Length - entryOffset)
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
                        reader.ReadBytes(
                            entryLength);

                    if (data.Length != entryLength)
                    {
                        continue;
                    }

                    using MemoryStream demoStream =
                        new(data);

                    using BinaryReader demoReader =
                        new(
                            demoStream,
                            Encoding.ASCII);

                    Demo? demo =
                        ReadDm2Info(
                            demoReader);

                    if (demo == null)
                    {
                        continue;
                    }

                    demo.FileName =
                        Path.GetFileName(entryName);

                    demo.GameDirectory =
                        gameDirectory;

                    demo.ResourceType =
                        DemoResourceType.Pak;

                    demo.ResourcePath =
                        pakFile;

                    SetDemoName(demo);

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
            // Ignore invalid or unreadable PAK files.
        }
    }

    // =============================================================
    // PK3 / ZIP demos
    // =============================================================

    private void AddPk3Demos(
        string folder,
        List<Demo> demos)
    {
        string[] archiveFiles;

        try
        {
            archiveFiles =
                Directory.GetFiles(
                    folder,
                    "*",
                    SearchOption.TopDirectoryOnly);
        }
        catch
        {
            return;
        }

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

            ReadPk3File(
                archiveFile,
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
                if (!entry.FullName.EndsWith(
                    ".dm2",
                    StringComparison.OrdinalIgnoreCase))
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

                Demo? demo =
                    ReadDm2Info(reader);

                if (demo == null)
                {
                    continue;
                }

                demo.FileName =
                    Path.GetFileName(entry.FullName);

                demo.GameDirectory =
                    gameDirectory;

                demo.ResourceType =
                    DemoResourceType.Pk3;

                demo.ResourcePath =
                    archiveFile;

                SetDemoName(demo);

                demos.Add(demo);
            }
        }
        catch
        {
            // Ignore invalid or unreadable PK3 files.
        }
    }

    // =============================================================
    // Demo naming
    // =============================================================

    private static void SetDemoName(
        Demo demo)
    {
        demo.MapTitle =
            CapitalizeFirstLetter(demo.MapTitle);

        demo.Name =
            string.IsNullOrWhiteSpace(
                demo.MapTitle)
                ? demo.FileName
                : $"{demo.FileName} | {demo.MapTitle}";
    }

    // =============================================================
    // Quake 2 DM2 reader
    // =============================================================

    private Demo? ReadDm2Info(
        BinaryReader reader)
    {
        if (reader.BaseStream.Length -
            reader.BaseStream.Position < 5)
        {
            return null;
        }

        int blockLength =
            reader.ReadInt32();

        if (blockLength < 0 ||
            reader.BaseStream.Position +
                blockLength >
            reader.BaseStream.Length)
        {
            return null;
        }

        long blockEnd =
            reader.BaseStream.Position +
            blockLength;

        int messageType =
            reader.ReadByte();

        if (messageType != ServerInfo)
        {
            return null;
        }

        int serverVersion =
            reader.ReadInt32();

        if (serverVersion != ProtocolKmq &&
            serverVersion != ProtocolR1Q2 &&
            !Quake2Protocols.Contains(
                serverVersion))
        {
            return null;
        }

        // Server info fields.

        if (!CanRead(
                reader,
                4,
                blockEnd))
        {
            return null;
        }

        // Key.
        reader.ReadInt32();

        if (!CanRead(
                reader,
                1,
                blockEnd))
        {
            return null;
        }

        // RECORD_CLIENT.
        if (reader.ReadByte() != 1)
        {
            return null;
        }

        // Game directory.
        // Empty means baseq2.
        ReadNullTerminatedString(
            reader,
            blockEnd);

        // Player number.
        if (!CanRead(
                reader,
                2,
                blockEnd))
        {
            return null;
        }

        reader.ReadInt16();

        string mapTitle =
            ReadQuake2MapTitle(
                reader,
                blockEnd);

        string mapFileName =
            "";

        while (
            reader.BaseStream.Position <
            blockEnd)
        {
            messageType =
                reader.ReadByte();

            if (messageType != ConfigString)
            {
                return null;
            }

            if (!CanRead(
                    reader,
                    2,
                    blockEnd))
            {
                return null;
            }

            // Config string type.
            reader.ReadInt16();

            string data =
                ReadNullTerminatedString(
                    reader,
                    blockEnd);

            if (data.EndsWith(
                ".bsp",
                StringComparison.OrdinalIgnoreCase))
            {
                mapFileName =
                    data;

                break;
            }
        }

        if (string.IsNullOrWhiteSpace(
                mapTitle) ||
            string.IsNullOrWhiteSpace(
                mapFileName))
        {
            return null;
        }

        return new Demo
        {
            MapFileName =
                mapFileName,

            MapTitle =
                mapTitle
        };
    }

    // =============================================================
    // Quake 2 map title
    // =============================================================

    private static string ReadQuake2MapTitle(
        BinaryReader reader,
        long blockEnd)
    {
        List<byte> bytes =
            new();

        while (
            reader.BaseStream.Position <
            blockEnd)
        {
            byte value =
                reader.ReadByte();

            if (value == 0)
            {
                break;
            }

            bytes.Add(value);

            if (bytes.Count >= 1024)
            {
                break;
            }
        }

        StringBuilder result =
            new();

        foreach (byte value in bytes)
        {
            if (value <
                Quake2CharMap.Characters.Length)
            {
                string character =
                    Quake2CharMap.Characters[value];

                if (!string.IsNullOrEmpty(
                    character))
                {
                    result.Append(
                        character);
                }
            }
            else
            {
                result.Append(
                    (char)value);
            }
        }

        return result
            .ToString()
            .Trim();
    }

    // =============================================================
    // Display helpers
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

    private static int GetResourcePriority(
    Demo demo)
    {
        return demo.ResourceType switch
        {
            DemoResourceType.Folder => 0,
            DemoResourceType.Pak => 1,
            DemoResourceType.Pk3 => 2,
            _ => 3
        };
    }

    // =============================================================
    // Binary helpers
    // =============================================================

    private static string ReadNullTerminatedString(
        BinaryReader reader,
        long endPosition)
    {
        StringBuilder result =
            new();

        while (
            reader.BaseStream.Position <
            endPosition)
        {
            byte value =
                reader.ReadByte();

            if (value == 0)
            {
                break;
            }

            result.Append(
                (char)value);

            if (result.Length >= 2048)
            {
                break;
            }
        }

        return result.ToString();
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

    private static string DecodeCString(
        byte[] bytes)
    {
        int length = 0;

        while (
            length < bytes.Length &&
            bytes[length] != 0)
        {
            length++;
        }

        return Encoding.ASCII.GetString(
            bytes,
            0,
            length);
    }
}