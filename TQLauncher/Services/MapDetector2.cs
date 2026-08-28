using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using TinyQuakeLauncher.Models;

namespace TinyQuakeLauncher.Services;

public class MapDetector2
{
    private const int PakHeaderSize = 12;
    private const int PakDirectoryEntrySize = 64;
    private const string Quake2BspMagic = "IBSP";
    private const int Quake2BspVersion = 38;

    // Quake II BSP header:
    // 4 bytes  = "IBSP"
    // 4 bytes  = version
    // 19 lumps * 8 bytes
    // Lump 0 is the entity lump.

    private const int Quake2BspHeaderSize = 8 + (19 * 8);

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

        foreach (string pk3File in FindPk3Files(gameFolder))
        {
            ReadPk3Maps(
                pk3File,
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

    private List<string> FindPakFiles(string folder)
    {
        List<string> result = new();

        try
        {
            foreach (string file in Directory.GetFiles(
                folder,
                "*.pak",
                SearchOption.TopDirectoryOnly))
            {
                result.Add(file);
            }
        }
        catch
        {
            // Ignore inaccessible files.
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

                if (!foundMaps.Add(fileName))
                {
                    continue;
                }

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

                if (string.IsNullOrWhiteSpace(title))
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
    // FIND PK3 FILES
    // =========================================================

    private List<string> FindPk3Files(string folder)
    {
        List<string> result = new();

        try
        {
            foreach (string file in Directory.GetFiles(
                folder,
                "*.pk3",
                SearchOption.TopDirectoryOnly))
            {
                result.Add(file);
            }
        }
        catch
        {
            // Ignore inaccessible files.
        }

        return result;
    }

    // =========================================================
    // READ PK3 MAPS
    // =========================================================

    private void ReadPk3Maps(
        string pk3File,
        List<MapInfo> maps,
        HashSet<string> foundMaps)
    {
        try
        {
            using ZipArchive archive =
                ZipFile.OpenRead(pk3File);

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string entryName =
                    entry.FullName.Replace(
                        '\\',
                        '/');

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

                if (!foundMaps.Add(fileName))
                {
                    continue;
                }

                string title = "";

                // Copy the PK3 entry into a seekable MemoryStream.
                using Stream entryStream =
                    entry.Open();

                using MemoryStream bspStream =
                    new();

                entryStream.CopyTo(bspStream);
                bspStream.Position = 0;

                title =
                    ReadBspTitle(
                        bspStream,
                        bspStream.Length);

                if (string.IsNullOrWhiteSpace(title))
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
            // Ignore this PK3 and continue with other files.
        }
    }

    // =========================================================
    // MAP ENTRY TEST
    // =========================================================

    private bool IsMapEntry(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
        {
            return false;
        }

        string normalized =
            entryName
                .Replace('\\', '/')
                .TrimStart('/');

        // Remove a leading "./" if the archive uses one.
        while (normalized.StartsWith(
            "./",
            StringComparison.Ordinal))
        {
            normalized =
                normalized.Substring(2);
        }

        // Accept the normal Quake II layout:
        //
        // maps/mapname.bsp
        //
        // and archives that contain the maps directory
        // below another directory:
        //
        // somefolder/maps/mapname.bsp
        int mapsIndex =
            normalized.LastIndexOf(
                "/maps/",
                StringComparison.OrdinalIgnoreCase);

        if (mapsIndex >= 0)
        {
            string fileName =
                normalized.Substring(
                    mapsIndex + 6);

            return fileName.Length > 4 &&
                   fileName.EndsWith(
                       ".bsp",
                       StringComparison.OrdinalIgnoreCase) &&
                   fileName.IndexOf('/') < 0;
        }

        if (normalized.StartsWith(
                "maps/",
                StringComparison.OrdinalIgnoreCase))
        {
            string fileName =
                normalized.Substring(5);

            return fileName.Length > 4 &&
                   fileName.EndsWith(
                       ".bsp",
                       StringComparison.OrdinalIgnoreCase) &&
                   fileName.IndexOf('/') < 0;
        }

        return false;
    }

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

            if (string.IsNullOrWhiteSpace(title))
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

            long originalPosition =
                stream.Position;

            try
            {
                stream.Position =
                    fileOffset;

                using Stream limitedStream =
                    new LimitedStream(
                        stream,
                        fileSize);

                return ReadBspTitle(
                    limitedStream,
                    fileSize);
            }
            finally
            {
                stream.Position =
                    originalPosition;
            }
        }
        catch
        {
            return "";
        }
    }

    // =========================================================
    // QUAKE II BSP TITLE
    // =========================================================

    private string ReadBspTitle(
        Stream stream,
        long bspSize)
    {
        try
        {
            if (bspSize < Quake2BspHeaderSize)
            {
                return "";
            }

            using BinaryReader reader =
                new(
                    stream,
                    Encoding.ASCII,
                    true);

            string magic =
                ReadFixedString(
                    reader,
                    4);

            int version =
                reader.ReadInt32();

            if (!string.Equals(
                magic,
                Quake2BspMagic,
                StringComparison.Ordinal) ||
                version != Quake2BspVersion)
            {
                return "";
            }

            // Lump 0 = entities.
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
                string title =
                    ExtractEntityValue(
                        entityData,
                        entityStart,
                        entityEnd,
                        "message");

                if (!string.IsNullOrWhiteSpace(title))
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

        return DecodeQuake2Title(
            data,
            valueStart,
            valueEnd);
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
    // DECODE QUAKE II TITLE
    // =========================================================

    private string DecodeQuake2Title(
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

            if (current == 0)
            {
                break;
            }

            // Quake entity newline: "\n"
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

            if (!(previous == 32 &&
                  current == 32))
            {
                if (current >= 32 &&
                    current <= 126)
                {
                    result.Append(
                        (char)current);
                }
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
                            valueX.CompareTo(
                                valueY);

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

            return x.Length.CompareTo(
                y.Length);
        }
    }

    // =========================================================
    // LIMITED STREAM
    // =========================================================

    private sealed class LimitedStream : Stream
    {
        private readonly Stream innerStream;
        private readonly long startPosition;
        private readonly long length;

        private long position;

        public LimitedStream(
            Stream innerStream,
            long length)
        {
            this.innerStream = innerStream;
            this.length = length;
            this.startPosition =
                innerStream.Position;
            this.position = 0;
        }

        public override bool CanRead =>
            innerStream.CanRead;

        public override bool CanSeek =>
            innerStream.CanSeek;

        public override bool CanWrite =>
            false;

        public override long Length =>
            length;

        public override long Position
        {
            get => position;
            set => Seek(
                value,
                SeekOrigin.Begin);
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count)
        {
            long remaining =
                length - position;

            if (remaining <= 0)
            {
                return 0;
            }

            int allowed =
                (int)Math.Min(
                    count,
                    remaining);

            innerStream.Position =
                startPosition +
                position;

            int read =
                innerStream.Read(
                    buffer,
                    offset,
                    allowed);

            position += read;

            return read;
        }

        public override long Seek(
            long offset,
            SeekOrigin origin)
        {
            long newPosition;

            switch (origin)
            {
                case SeekOrigin.Begin:
                    newPosition = offset;
                    break;

                case SeekOrigin.Current:
                    newPosition =
                        position + offset;
                    break;

                case SeekOrigin.End:
                    newPosition =
                        length + offset;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(origin));
            }

            if (newPosition < 0 ||
                newPosition > length)
            {
                throw new IOException(
                    "Attempted to seek outside the limited stream.");
            }

            position = newPosition;
            return position;
        }

        public override void Flush()
        {
        }

        public override void SetLength(
            long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(
            byte[] buffer,
            int offset,
            int count)
        {
            throw new NotSupportedException();
        }
    }
}