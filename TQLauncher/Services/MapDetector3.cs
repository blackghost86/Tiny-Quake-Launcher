using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TinyQuakeLauncher.Models;

namespace TinyQuakeLauncher.Services;

public class MapDetector3
{
    public List<MapInfo> DetectMaps(string gameFolder)
    {
        List<MapInfo> maps = new();

        if (string.IsNullOrWhiteSpace(gameFolder) ||
            !Directory.Exists(gameFolder))
        {
            return maps;
        }

        HashSet<string> seenMaps =
            new(StringComparer.OrdinalIgnoreCase);

        Dictionary<string, string> mapTitles =
            new(StringComparer.OrdinalIgnoreCase);

        // Q3 map display names are normally stored in scripts/*.arena.
        // Read these first so loose and zip maps use the same title.
        ReadArenaTitles(
            gameFolder,
            mapTitles);

        // Loose BSP files.
        try
        {
            foreach (string mapFile in Directory.GetFiles(
                gameFolder,
                "*.bsp",
                SearchOption.AllDirectories))
            {
                string fileName =
                    Path.GetFileName(mapFile);

                string mapName =
                    Path.GetFileNameWithoutExtension(fileName);

                if (string.IsNullOrWhiteSpace(mapName) ||
                    IsExcludedMap(mapName) ||
                    !seenMaps.Add(mapName))
                {
                    continue;
                }

                maps.Add(
                    CreateMapInfo(
                        fileName,
                        mapName,
                        mapTitles));
            }
        }
        catch
        {
            // Ignore inaccessible directories/files.
        }

        // Q3 content is commonly stored in PK3 files. ZIP is also supported
        // because PK3 is a ZIP-compatible archive and custom installations
        // sometimes use the .zip extension.
        try
        {
            foreach (string archiveFile in Directory.GetFiles(
                gameFolder,
                "*",
                SearchOption.AllDirectories))
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

                AddArchiveMaps(
                    maps,
                    seenMaps,
                    archiveFile,
                    mapTitles);
            }
        }
        catch
        {
            // Ignore inaccessible directories/archives.
        }

        return maps
            .OrderBy(map => GetQuake3MapSortKey(map.FileName), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsExcludedMap(string mapName)
    {
        // Exclude maps that are not intended for normal gameplay.
        return string.Equals(
                   mapName,
                   "test_bigbox",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   mapName,
                   "texturegrab",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string GetQuake3MapSortKey(string fileName)
    {
        string mapName = Path.GetFileNameWithoutExtension(fileName);
        Match match = Regex.Match(mapName, @"^(.*?)(\d+)$", RegexOptions.CultureInvariant);

        if (!match.Success || !int.TryParse(match.Groups[2].Value, out int number))
        {
            return mapName;
        }

        return $"{match.Groups[1].Value}\u0001{number:D10}";
    }

    private static void AddArchiveMaps(
        List<MapInfo> maps,
        HashSet<string> seenMaps,
        string archiveFile,
        Dictionary<string, string> mapTitles)
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

                if (!entryPath.StartsWith(
                        "maps/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!entryPath.EndsWith(
                        ".bsp",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string fileName =
                    Path.GetFileName(entryPath);

                string mapName =
                    Path.GetFileNameWithoutExtension(fileName);

                if (string.IsNullOrWhiteSpace(mapName) ||
                    IsExcludedMap(mapName) ||
                    !seenMaps.Add(mapName))
                {
                    continue;
                }

                maps.Add(
                    CreateMapInfo(
                        fileName,
                        mapName,
                        mapTitles));
            }
        }
        catch
        {
            // Ignore invalid/inaccessible PK3/ZIP files.
        }
    }

    private static MapInfo CreateMapInfo(
        string fileName,
        string mapName,
        Dictionary<string, string> mapTitles)
    {
        string title =
            mapTitles.TryGetValue(
                mapName,
                out string? arenaTitle) &&
            !string.IsNullOrWhiteSpace(arenaTitle)
                ? arenaTitle
                : mapName;

        return new MapInfo
        {
            FileName = fileName,
            Title = title
        };
    }

    private static void ReadArenaTitles(
        string gameFolder,
        Dictionary<string, string> mapTitles)
    {
        try
        {
            // Quake 3 Arena's original data uses scripts/arenas.txt for
            // the stock maps, while custom maps commonly use separate
            // scripts/*.arena files. Read both formats.
            foreach (string arenaFile in Directory.GetFiles(
                gameFolder,
                "*",
                SearchOption.AllDirectories))
            {
                string fileName =
                    Path.GetFileName(arenaFile);

                string relativePath =
                    Path.GetRelativePath(
                        gameFolder,
                        arenaFile)
                    .Replace('\\', '/');

                bool isArenaFile =
                    fileName.EndsWith(
                        ".arena",
                        StringComparison.OrdinalIgnoreCase);

                bool isArenasTxt =
                    string.Equals(
                        fileName,
                        "arenas.txt",
                        StringComparison.OrdinalIgnoreCase) &&
                    (relativePath.StartsWith(
                        "scripts/",
                        StringComparison.OrdinalIgnoreCase) ||
                     relativePath.Contains(
                        "/scripts/",
                        StringComparison.OrdinalIgnoreCase));

                if (!isArenaFile && !isArenasTxt)
                {
                    continue;
                }

                try
                {
                    string text =
                        File.ReadAllText(
                            arenaFile,
                            Encoding.UTF8);

                    ParseArenaText(
                        text,
                        mapTitles);
                }
                catch
                {
                    // Ignore invalid/inaccessible arena files.
                }
            }
        }
        catch
        {
            // Ignore inaccessible directories.
        }

        // Read arena files from PK3 and ZIP archives. This is kept separate from
        // map enumeration so titles are available even after another archive.
        try
        {
            foreach (string archiveFile in Directory.GetFiles(
                gameFolder,
                "*",
                SearchOption.AllDirectories))
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

                        bool isArenaFile =
                            entryPath.EndsWith(
                                ".arena",
                                StringComparison.OrdinalIgnoreCase);

                        bool isArenasTxt =
                            entryPath.EndsWith(
                                "scripts/arenas.txt",
                                StringComparison.OrdinalIgnoreCase);

                        if (!isArenaFile && !isArenasTxt)
                        {
                            continue;
                        }

                        using StreamReader reader =
                            new(
                                entry.Open(),
                                Encoding.UTF8,
                                true);

                        ParseArenaText(
                            reader.ReadToEnd(),
                            mapTitles);
                    }
                }
                catch
                {
                    // Ignore invalid/inaccessible archives.
                }
            }
        }
        catch
        {
            // Ignore inaccessible directories/archives.
        }
    }

    private static void ParseArenaText(
        string text,
        Dictionary<string, string> mapTitles)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        // Q3 .arena files use key/value pairs such as: map "q3dm1" longname "Arena Gate 1".
        // A small tokenizer is used instead of a simple line parser because the keys
        // can appear on different lines or in a different order.
        List<string> tokens =
            Tokenize(text);

        string? mapName = null;
        string? longName = null;

        for (int i = 0; i < tokens.Count; i++)
        {
            if (string.Equals(
                    tokens[i],
                    "map",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < tokens.Count)
                {
                    mapName = tokens[++i];
                }
            }
            else if (string.Equals(
                         tokens[i],
                         "longname",
                         StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < tokens.Count)
                {
                    longName = tokens[++i];
                }
            }

            // Arena entries are normally enclosed in braces. If another map
            // starts before a longname was found, keep the map name and
            // let the next key/value pair provide the title.
            if (string.Equals(
                    tokens[i],
                    "}",
                    StringComparison.Ordinal))
            {
                AddArenaTitle(
                    mapTitles,
                    mapName,
                    longName);

                mapName = null;
                longName = null;
            }
        }

        // Also handle a final entry without a closing brace.
        AddArenaTitle(
            mapTitles,
            mapName,
            longName);
    }

    private static void AddArenaTitle(
        Dictionary<string, string> mapTitles,
        string? mapName,
        string? longName)
    {
        if (string.IsNullOrWhiteSpace(mapName) ||
            string.IsNullOrWhiteSpace(longName))
        {
            return;
        }

        mapName =
            Path.GetFileNameWithoutExtension(
                mapName.Trim());

        longName =
            longName.Trim();

        if (string.IsNullOrWhiteSpace(mapName) ||
            string.IsNullOrWhiteSpace(longName))
        {
            return;
        }

        mapTitles[mapName] = longName;
    }

    private static List<string> Tokenize(string text)
    {
        List<string> tokens = new();
        StringBuilder token = new();
        bool inQuotes = false;
        bool escaping = false;

        foreach (char character in text)
        {
            if (escaping)
            {
                token.Append(character);
                escaping = false;
                continue;
            }

            if (character == '\\' && inQuotes)
            {
                escaping = true;
                continue;
            }

            if (character == '"')
            {
                if (inQuotes)
                {
                    tokens.Add(token.ToString());
                    token.Clear();
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (inQuotes)
            {
                token.Append(character);
                continue;
            }

            if (char.IsWhiteSpace(character) ||
                character == '{' ||
                character == '}')
            {
                if (token.Length > 0)
                {
                    tokens.Add(token.ToString());
                    token.Clear();
                }

                if (character == '{' ||
                    character == '}')
                {
                    tokens.Add(character.ToString());
                }

                continue;
            }

            token.Append(character);
        }

        if (token.Length > 0)
        {
            tokens.Add(token.ToString());
        }

        return tokens;
    }
}