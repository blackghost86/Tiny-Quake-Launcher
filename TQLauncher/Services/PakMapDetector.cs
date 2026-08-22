using System.IO;
using System.Text;

namespace TinyQuakeLauncher.Services;

public class PakMapDetector
{
    private const int PakHeaderSize = 12;
    private const int PakDirectoryEntrySize = 64;

    public List<string> DetectMaps(string gameFolder)
    {
        HashSet<string> maps =
            new(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(gameFolder))
        {
            return maps.OrderBy(map => map).ToList();
        }

        // 1. Look for maps inside PAK files.
        string[] pakFiles = Directory.GetFiles(
            gameFolder,
            "*.pak",
            SearchOption.TopDirectoryOnly);

        foreach (string pakFile in pakFiles)
        {
            ReadPakMaps(pakFile, maps);
        }

        // 2. Look for loose BSP files in the maps folder.
        string mapsFolder = Path.Combine(
            gameFolder,
            "maps");

        if (Directory.Exists(mapsFolder))
        {
            string[] bspFiles = Directory.GetFiles(
                mapsFolder,
                "*.bsp",
                SearchOption.TopDirectoryOnly);

            foreach (string bspFile in bspFiles)
            {
                string mapName =
                    Path.GetFileName(bspFile);

                if (!string.IsNullOrWhiteSpace(mapName))
                {
                    maps.Add(mapName);
                }
            }
        }

        return maps
            .OrderBy(map => map)
            .ToList();
    }

    private void ReadPakMaps(
        string pakFile,
        HashSet<string> maps)
    {
        try
        {
            using FileStream stream =
                File.OpenRead(pakFile);

            using BinaryReader reader =
                new(stream, Encoding.ASCII);

            if (stream.Length < PakHeaderSize)
            {
                return;
            }

            string magic =
                Encoding.ASCII.GetString(
                    reader.ReadBytes(4));

            if (magic != "PACK")
            {
                return;
            }

            int directoryOffset =
                reader.ReadInt32();

            int directoryLength =
                reader.ReadInt32();

            if (directoryOffset < 0 ||
                directoryLength < 0 ||
                directoryOffset + directoryLength >
                    stream.Length)
            {
                return;
            }

            int entryCount =
                directoryLength /
                PakDirectoryEntrySize;

            stream.Seek(
                directoryOffset,
                SeekOrigin.Begin);

            for (int i = 0; i < entryCount; i++)
            {
                byte[] nameBytes =
                    reader.ReadBytes(56);

                // Skip file offset and file size.
                reader.ReadInt32();
                reader.ReadInt32();

                string entryName =
                    Encoding.ASCII
                        .GetString(nameBytes)
                        .TrimEnd('\0');

                if (!entryName.EndsWith(
                        ".bsp",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string mapName =
                    Path.GetFileName(entryName);

                if (!string.IsNullOrWhiteSpace(mapName))
                {
                    maps.Add(mapName);
                }
            }
        }
        catch (IOException)
        {
            // Ignore unreadable PAK files.
        }
        catch (UnauthorizedAccessException)
        {
            // Ignore inaccessible PAK files.
        }
    }
}