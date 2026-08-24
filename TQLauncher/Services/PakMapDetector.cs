using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TinyQuakeLauncher.Models;

namespace TinyQuakeLauncher.Services;

public class PakMapDetector
{
    private const int PakHeaderSize = 12;
    private const int PakDirectoryEntrySize = 64;
    private const int QuakeBspHeaderSize = 124;
    private const int QuakeBspVersion = 29;

    public List<MapInfo> DetectMaps(string gameFolder)
    {
        List<MapInfo> maps = new();

        if (string.IsNullOrWhiteSpace(gameFolder))
        {
            return maps;
        }

        if (!Directory.Exists(gameFolder))
        {
            return maps;
        }

        HashSet<string> foundMaps =
            new(StringComparer.OrdinalIgnoreCase);

        // -----------------------------------------------------
        // PAK files
        // -----------------------------------------------------

        foreach (string pakFile in FindPakFiles(gameFolder))
        {
            ReadPakMaps(
                pakFile,
                maps,
                foundMaps);
        }

        // -----------------------------------------------------
        // Loose BSP files
        // -----------------------------------------------------

        AddLooseBspFiles(
            gameFolder,
            maps,
            foundMaps);

        // -----------------------------------------------------
        // Sort
        // -----------------------------------------------------

        return maps
            .OrderBy(
                map => map.FileName,
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // =========================================================
    // FIND PAK FILES
    // =========================================================

    private List<string> FindPakFiles(
        string folder)
    {
        List<string> result = new();

        try
        {
            foreach (string file in Directory.GetFiles(
                folder,
                "*.pak",
                SearchOption.AllDirectories))
            {
                if (!result.Contains(
                    file,
                    StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(file);
                }
            }
        }
        catch
        {
            // Ignore inaccessible folders.
        }

        return result;
    }

    // =========================================================
    // READ PAK MAPS
    // =========================================================

    private void ReadPakMaps(
        string pakFile,
        List<MapInfo> maps,
        HashSet<string> foundMaps)
    {
        try
        {
            using FileStream stream =
                File.OpenRead(pakFile);

            using BinaryReader reader =
                new(
                    stream,
                    Encoding.ASCII,
                    true);

            if (stream.Length < PakHeaderSize)
            {
                return;
            }

            // -------------------------------------------------
            // PAK header
            //
            // 0-3   = PACK
            // 4-7   = directory offset
            // 8-11  = directory length
            // -------------------------------------------------

            string id =
                ReadFixedString(
                    reader,
                    4);

            if (!string.Equals(
                id,
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
                directoryLength <= 0)
            {
                return;
            }

            long directoryEnd =
                (long)directoryOffset +
                directoryLength;

            if (directoryEnd > stream.Length)
            {
                return;
            }

            if (directoryLength %
                PakDirectoryEntrySize != 0)
            {
                return;
            }

            int entryCount =
                directoryLength /
                PakDirectoryEntrySize;

            // -------------------------------------------------
            // Go to file table.
            // -------------------------------------------------

            stream.Position =
                directoryOffset;

            // -------------------------------------------------
            // Read directory entries.
            // -------------------------------------------------

            for (int i = 0;
                 i < entryCount;
                 i++)
            {
                string entryName =
                    ReadFixedString(
                        reader,
                        56);

                int fileOffset =
                    reader.ReadInt32();

                int fileSize =
                    reader.ReadInt32();

                if (string.IsNullOrWhiteSpace(
                    entryName))
                {
                    continue;
                }

                entryName =
                    entryName.Replace(
                        '\\',
                        '/');

                // -------------------------------------------------
                // The reference launcher considers maps to be:
                //
                // maps/<name>.bsp
                //
                // Do the same here.
                // -------------------------------------------------

                if (!IsMapEntry(entryName))
                {
                    continue;
                }

                string mapName =
                    Path.GetFileNameWithoutExtension(
                        entryName);

                if (string.IsNullOrWhiteSpace(
                    mapName))
                {
                    continue;
                }

                string fileName =
                    mapName + ".bsp";

                // -------------------------------------------------
                // Avoid duplicate map names.
                // -------------------------------------------------

                if (!foundMaps.Add(fileName))
                {
                    continue;
                }

                // -------------------------------------------------
                // Try to read the BSP worldspawn message.
                //
                // If that fails, the map is still valid and the
                // filename will be used as its title.
                // -------------------------------------------------

                string title = "";

                if (fileOffset >= 0 &&
                    fileSize > 0 &&
                    (long)fileOffset +
                    fileSize <=
                    stream.Length)
                {
                    title =
                        ReadPakBspTitle(
                            stream,
                            fileOffset,
                            fileSize);
                }

                if (string.IsNullOrWhiteSpace(
                    title))
                {
                    title = mapName;
                }

                maps.Add(
                    new MapInfo
                    {
                        FileName = fileName,
                        Title = title
                    });
            }
        }
        catch
        {
            // Ignore this PAK and continue with other files.
        }
    }

    // =========================================================
    // PAK ENTRY TEST
    // =========================================================

    private bool IsMapEntry(
        string entryName)
    {
        string normalized =
            entryName.Replace(
                '\\',
                '/');

        int slash =
            normalized.LastIndexOf('/');

        if (slash < 0)
        {
            return false;
        }

        string directory =
            normalized.Substring(
                0,
                slash);

        string fileName =
            normalized.Substring(
                slash + 1);

        if (!string.Equals(
            directory,
            "maps",
            StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return fileName.EndsWith(
            ".bsp",
            StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================
    // FIXED-LENGTH STRING
    // =========================================================

    private string ReadFixedString(
        BinaryReader reader,
        int length)
    {
        byte[] bytes =
            reader.ReadBytes(length);

        if (bytes.Length != length)
        {
            throw new EndOfStreamException();
        }

        int zero =
            Array.IndexOf(
                bytes,
                (byte)0);

        if (zero >= 0)
        {
            length = zero;
        }

        return Encoding.ASCII
            .GetString(
                bytes,
                0,
                length)
            .Trim();
    }

    // =========================================================
    // LOOSE BSP FILES
    // =========================================================

    private void AddLooseBspFiles(
        string gameFolder,
        List<MapInfo> maps,
        HashSet<string> foundMaps)
    {
        try
        {
            foreach (string bspFile in Directory.GetFiles(
                gameFolder,
                "*.bsp",
                SearchOption.AllDirectories))
            {
                AddLooseBsp(
                    bspFile,
                    maps,
                    foundMaps);
            }
        }
        catch
        {
            // Ignore inaccessible folders.
        }
    }

    private void AddLooseBsp(
        string bspFile,
        List<MapInfo> maps,
        HashSet<string> foundMaps)
    {
        try
        {
            string normalized =
                bspFile.Replace(
                    '\\',
                    '/');

            int mapsIndex =
                normalized.LastIndexOf(
                    "/maps/",
                    StringComparison.OrdinalIgnoreCase);

            // -------------------------------------------------
            // Only loose BSPs inside a maps directory.
            // -------------------------------------------------

            if (mapsIndex < 0)
            {
                return;
            }

            string fileName =
                Path.GetFileName(bspFile);

            if (string.IsNullOrWhiteSpace(
                fileName))
            {
                return;
            }

            if (!fileName.EndsWith(
                ".bsp",
                StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!foundMaps.Add(fileName))
            {
                return;
            }

            string title =
                ReadLooseBspTitle(
                    bspFile);

            if (string.IsNullOrWhiteSpace(
                title))
            {
                title =
                    Path.GetFileNameWithoutExtension(
                        fileName);
            }

            maps.Add(
                new MapInfo
                {
                    FileName = fileName,
                    Title = title
                });
        }
        catch
        {
            // Ignore individual BSP files.
        }
    }

    // =========================================================
    // LOOSE BSP TITLE
    // =========================================================

    private string ReadLooseBspTitle(
        string bspFile)
    {
        try
        {
            using FileStream stream =
                File.OpenRead(bspFile);

            return ReadBspTitle(
                stream,
                0,
                stream.Length);
        }
        catch
        {
            return "";
        }
    }

    // =========================================================
    // PAK BSP TITLE
    // =========================================================

    private string ReadPakBspTitle(
        FileStream stream,
        int fileOffset,
        int fileSize)
    {
        try
        {
            if (fileOffset < 0 ||
                fileSize <= 0)
            {
                return "";
            }

            if ((long)fileOffset +
                fileSize >
                stream.Length)
            {
                return "";
            }

            return ReadBspTitle(
                stream,
                fileOffset,
                fileSize);
        }
        catch
        {
            return "";
        }
    }

    // =========================================================
    // BSP TITLE
    // =========================================================

    private string ReadBspTitle(
        FileStream stream,
        long bspOffset,
        long bspSize)
    {
        long originalPosition =
            stream.Position;

        try
        {
            if (bspSize < QuakeBspHeaderSize)
            {
                return "";
            }

            stream.Position =
                bspOffset;

            using BinaryReader reader =
                new(
                    stream,
                    Encoding.ASCII,
                    true);

            // -------------------------------------------------
            // BSP version
            // -------------------------------------------------

            int version =
                reader.ReadInt32();

            if (version != QuakeBspVersion)
            {
                return "";
            }

            // -------------------------------------------------
            // Lump 0 = entities
            // -------------------------------------------------

            int entityOffset =
                reader.ReadInt32();

            int entityLength =
                reader.ReadInt32();

            if (entityOffset < 0 ||
                entityLength <= 0)
            {
                return "";
            }

            if ((long)entityOffset +
                entityLength >
                bspSize)
            {
                return "";
            }

            stream.Position =
                bspOffset +
                entityOffset;

            byte[] entityData =
                reader.ReadBytes(
                    entityLength);

            if (entityData.Length == 0)
            {
                return "";
            }

            string entities =
                Encoding.ASCII.GetString(
                    entityData);

            return ExtractMapTitle(
                entities);
        }
        catch
        {
            return "";
        }
        finally
        {
            try
            {
                stream.Position =
                    originalPosition;
            }
            catch
            {
            }
        }
    }

    // =========================================================
    // EXTRACT MAP TITLE
    // =========================================================

    private string ExtractMapTitle(
        string entities)
    {
        int position = 0;

        while (true)
        {
            int entityStart =
                entities.IndexOf(
                    '{',
                    position);

            if (entityStart < 0)
            {
                return "";
            }

            int entityEnd =
                entities.IndexOf(
                    '}',
                    entityStart + 1);

            if (entityEnd < 0)
            {
                return "";
            }

            string entity =
                entities.Substring(
                    entityStart,
                    entityEnd -
                    entityStart +
                    1);

            string classname =
                ExtractEntityValue(
                    entity,
                    "classname");

            if (string.Equals(
                classname,
                "worldspawn",
                StringComparison.OrdinalIgnoreCase))
            {
                string message =
                    ExtractEntityValue(
                        entity,
                        "message");

                if (!string.IsNullOrWhiteSpace(
                    message))
                {
                    return CleanTitle(
                        message);
                }

                // -------------------------------------------------
                // Some Quake maps can use netname.
                // -------------------------------------------------

                string netname =
                    ExtractEntityValue(
                        entity,
                        "netname");

                if (!string.IsNullOrWhiteSpace(
                    netname))
                {
                    return CleanTitle(
                        netname);
                }

                return "";
            }

            position =
                entityEnd + 1;
        }
    }

    // =========================================================
    // ENTITY VALUE
    // =========================================================

    private string ExtractEntityValue(
        string entity,
        string key)
    {
        string search =
            "\"" + key + "\"";

        int keyPosition =
            entity.IndexOf(
                search,
                StringComparison.OrdinalIgnoreCase);

        if (keyPosition < 0)
        {
            return "";
        }

        int valueStart =
            entity.IndexOf(
                '"',
                keyPosition +
                search.Length);

        if (valueStart < 0)
        {
            return "";
        }

        valueStart++;

        int valueEnd =
            valueStart;

        while (valueEnd < entity.Length)
        {
            if (entity[valueEnd] == '"' &&
                entity[valueEnd - 1] != '\\')
            {
                break;
            }

            valueEnd++;
        }

        if (valueEnd >= entity.Length)
        {
            return "";
        }

        return entity.Substring(
                valueStart,
                valueEnd - valueStart)
            .Trim();
    }

    // =========================================================
    // CLEAN TITLE
    // =========================================================

    private string CleanTitle(
        string title)
    {
        return title
            .Replace(
                "\\n",
                " ")
            .Replace(
                "\r",
                " ")
            .Replace(
                "\n",
                " ")
            .Trim();
    }
}