using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TinyQuakeLauncher.Models;

namespace TinyQuakeLauncher.Services;

public class PakMapDetector
{
    private const int PakHeaderSize = 12;
    private const int PakDirectoryEntrySize = 64;
    private const int QuakeBspHeaderSize = 124;
    private const int QuakeBspVersion = 29;
    private const int QuakeBsp2Version2Psb =
        ('B' << 24) | ('S' << 16) | ('P' << 8) | '2';
    private const int QuakeBsp2VersionBsp2 =
        ('B' << 0) | ('S' << 8) | ('P' << 16) | ('2' << 24);

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

        foreach (string pakFile in FindPakFiles(gameFolder))
        {
            ReadPakMaps(
                pakFile,
                maps,
                foundMaps);
        }

        AddLooseBspFiles(
            gameFolder,
            maps,
            foundMaps);

        return maps
            .OrderBy(
                map => map.FileName,
                new NaturalMapNameComparer())
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
                // A map is specifically:
                //
                // maps/<name>.bsp
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
                // Read BSP title.
                //
                // If the title cannot be read, the map is still
                // valid and the filename is used.
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

            // -------------------------------------------------
            // Avoid duplicate map names.
            // -------------------------------------------------

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
            // Quake 1 BSP version
            // -------------------------------------------------

            int version =
                reader.ReadInt32();

            if (version != QuakeBspVersion &&
                version != QuakeBsp2Version2Psb &&
                version != QuakeBsp2VersionBsp2)
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

            return ExtractMapTitle(
                entityData);
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
        byte[] entityData)
    {
        int position = 0;

        while (position < entityData.Length)
        {
            int entityStart =
                FindByte(
                    entityData,
                    (byte)'{',
                    position);

            if (entityStart < 0)
            {
                return "";
            }

            int entityEnd =
                FindByte(
                    entityData,
                    (byte)'}',
                    entityStart + 1);

            if (entityEnd < 0)
            {
                return "";
            }

            // -------------------------------------------------
            // Check for worldspawn.
            // -------------------------------------------------

            string classname =
                ExtractEntityValue(
                    entityData,
                    entityStart,
                    entityEnd,
                    "classname");

            if (string.Equals(
                classname,
                "worldspawn",
                StringComparison.OrdinalIgnoreCase))
            {
                // -------------------------------------------------
                // Normal Quake map title.
                // -------------------------------------------------

                string title =
                    ExtractEncodedEntityValue(
                        entityData,
                        entityStart,
                        entityEnd,
                        "message");

                if (!string.IsNullOrWhiteSpace(
                    title))
                {
                    return title;
                }

                // -------------------------------------------------
                // Some maps use netname.
                // -------------------------------------------------

                title =
                    ExtractEncodedEntityValue(
                        entityData,
                        entityStart,
                        entityEnd,
                        "netname");

                if (!string.IsNullOrWhiteSpace(
                    title))
                {
                    return title;
                }

                return "";
            }

            position =
                entityEnd + 1;
        }

        return "";
    }

    // =========================================================
    // FIND BYTE
    // =========================================================

    private int FindByte(
        byte[] data,
        byte value,
        int start)
    {
        for (int i = start;
             i < data.Length;
             i++)
        {
            if (data[i] == value)
            {
                return i;
            }
        }

        return -1;
    }

    // =========================================================
    // ENTITY VALUE
    // =========================================================

    private string ExtractEntityValue(
        byte[] data,
        int entityStart,
        int entityEnd,
        string key)
    {
        byte[] keyBytes =
            Encoding.ASCII.GetBytes(
                "\"" + key + "\"");

        int keyPosition =
            FindSequence(
                data,
                keyBytes,
                entityStart,
                entityEnd);

        if (keyPosition < 0)
        {
            return "";
        }

        int valueStart =
            FindByte(
                data,
                (byte)'"',
                keyPosition +
                keyBytes.Length);

        if (valueStart < 0 ||
            valueStart >= entityEnd)
        {
            return "";
        }

        valueStart++;

        int valueEnd =
            FindClosingQuote(
                data,
                valueStart,
                entityEnd);

        if (valueEnd < 0)
        {
            return "";
        }

        return Encoding.ASCII
            .GetString(
                data,
                valueStart,
                valueEnd - valueStart)
            .Trim();
    }

    // =========================================================
    // ENCODED ENTITY VALUE
    // =========================================================

    private string ExtractEncodedEntityValue(
        byte[] data,
        int entityStart,
        int entityEnd,
        string key)
    {
        byte[] keyBytes =
            Encoding.ASCII.GetBytes(
                "\"" + key + "\"");

        int keyPosition =
            FindSequence(
                data,
                keyBytes,
                entityStart,
                entityEnd);

        if (keyPosition < 0)
        {
            return "";
        }

        int valueStart =
            FindByte(
                data,
                (byte)'"',
                keyPosition +
                keyBytes.Length);

        if (valueStart < 0 ||
            valueStart >= entityEnd)
        {
            return "";
        }

        valueStart++;

        int valueEnd =
            FindClosingQuote(
                data,
                valueStart,
                entityEnd);

        if (valueEnd < 0)
        {
            return "";
        }

        return DecodeQuakeTitle(
            data,
            valueStart,
            valueEnd);
    }

    // =========================================================
    // FIND BYTE SEQUENCE
    // =========================================================

    private int FindSequence(
        byte[] data,
        byte[] sequence,
        int start,
        int end)
    {
        if (sequence.Length == 0)
        {
            return -1;
        }

        int last =
            end -
            sequence.Length +
            1;

        for (int i = start;
             i < last;
             i++)
        {
            bool match = true;

            for (int j = 0;
                 j < sequence.Length;
                 j++)
            {
                if (data[i + j] !=
                    sequence[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return i;
            }
        }

        return -1;
    }

    // =========================================================
    // FIND CLOSING QUOTE
    // =========================================================

    private int FindClosingQuote(
        byte[] data,
        int start,
        int end)
    {
        for (int i = start;
             i < end;
             i++)
        {
            if (data[i] != '"')
            {
                continue;
            }

            // -------------------------------------------------
            // Ignore escaped quotes.
            // -------------------------------------------------

            int backslashes = 0;
            int p = i - 1;

            while (p >= start &&
                   data[p] == '\\')
            {
                backslashes++;
                p--;
            }

            if ((backslashes & 1) == 0)
            {
                return i;
            }
        }

        return -1;
    }

    // =========================================================
    // DECODE QUAKE TITLE
    // =========================================================

    private string DecodeQuakeTitle(
        byte[] data,
        int start,
        int end)
    {
        StringBuilder result =
            new();

        byte previous = 0;

        for (int i = start;
             i < end;
             i++)
        {
            byte current =
                data[i];

            // -------------------------------------------------
            // Stop on null.
            // -------------------------------------------------

            if (current == 0)
            {
                break;
            }

            // -------------------------------------------------
            // Quake entity newline:
            //
            // "\n"
            //
            // Replace with a normal space.
            // -------------------------------------------------

            if (current == (byte)'n' &&
                previous == (byte)'\\')
            {
                previous = current;

                if (result.Length > 0)
                {
                    result.Remove(
                        result.Length - 1,
                        1);
                }

                result.Append(' ');
                continue;
            }

            // -------------------------------------------------
            // Avoid duplicate spaces.
            // -------------------------------------------------

            if (!(previous == 32 &&
                  current == 32))
            {
                result.Append(
                    GetQuakeCharacter(
                        current));
            }

            previous =
                current;
        }

        return result
            .ToString()
            .Trim();
    }

    // =========================================================
    // NATURAL MAP NAME SORTING
    // =========================================================

    private sealed class NaturalMapNameComparer :
        IComparer<string>
    {
        public int Compare(
            string? x,
            string? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            int ix = 0;
            int iy = 0;

            while (ix < x.Length &&
                   iy < y.Length)
            {
                char cx = x[ix];
                char cy = y[iy];

                if (char.IsDigit(cx) &&
                    char.IsDigit(cy))
                {
                    int startX = ix;
                    int startY = iy;

                    while (ix < x.Length &&
                           char.IsDigit(x[ix]))
                    {
                        ix++;
                    }

                    while (iy < y.Length &&
                           char.IsDigit(y[iy]))
                    {
                        iy++;
                    }

                    string numberX =
                        x.Substring(
                            startX,
                            ix - startX);

                    string numberY =
                        y.Substring(
                            startY,
                            iy - startY);

                    if (long.TryParse(
                            numberX,
                            out long valueX) &&
                        long.TryParse(
                            numberY,
                            out long valueY))
                    {
                        int numericResult =
                            valueX.CompareTo(valueY);

                        if (numericResult != 0)
                        {
                            return numericResult;
                        }
                    }
                    else
                    {
                        int lengthResult =
                            numberX.Length.CompareTo(
                                numberY.Length);

                        if (lengthResult != 0)
                        {
                            return lengthResult;
                        }
                    }

                    // Same numeric value: continue comparing
                    // the rest of the map name.
                    continue;
                }

                int charResult =
                    char.ToUpperInvariant(cx)
                        .CompareTo(
                            char.ToUpperInvariant(cy));

                if (charResult != 0)
                {
                    return charResult;
                }

                ix++;
                iy++;
            }

            return x.Length.CompareTo(y.Length);
        }
    }

    // =========================================================
    // QUAKE CHARACTER
    // =========================================================

    private string GetQuakeCharacter(
        byte value)
    {
        // -----------------------------------------------------
        // Use the separate Quake character map.
        // -----------------------------------------------------

        if (value <
            QuakeCharMap.Characters.Length)
        {
            string mapped =
                QuakeCharMap.Characters[value];

            if (!string.IsNullOrEmpty(
                mapped))
            {
                return mapped;
            }
        }

        // -----------------------------------------------------
        // Keep ordinary printable ASCII intact.
        // -----------------------------------------------------

        if (value >= 32 &&
            value <= 126)
        {
            return ((char)value).ToString();
        }

        return "";
    }
}