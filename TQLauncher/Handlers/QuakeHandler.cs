using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using TinyQuakeLauncher.Models;
using TinyQuakeLauncher.Services;

namespace TinyQuakeLauncher;

public class QuakeHandler
{
    private readonly DemoDetector demoDetector = new();

    public static List<MissionPack> DetectClassicQuakeFolders(
        string quakeFolder)
    {
        List<MissionPack> missionPacks =
            new();

        if (!Directory.Exists(quakeFolder))
        {
            return missionPacks;
        }

        string[] knownDirectories =
        {
            "id1",
            "hipnotic",
            "rogue",
            "ctf",
            "quoth",
            "nehahra"
        };

        string[] knownNames =
        {
            "Quake",
            "Scourge of Armagon",
            "Dissolution of Eternity",
            "Capture the Flag",
            "Quoth",
            "Nehara"
        };

        // Only these known Quake folders are displayed.
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

    public static void AddQuakeSteamEnhancedMissionPacks(
        List<MissionPack> missionPacks,
        string quakeFolder)
    {
        string rereleaseFolder =
            Path.Combine(
                quakeFolder,
                "rerelease");

        if (!Directory.Exists(rereleaseFolder))
        {
            return;
        }

        string[] enhancedDirectories =
        {
            "id1",
            "hipnotic",
            "rogue",
            "dopa",
            "mg1",
            "mg3",
            "ctf"
        };

        string[] enhancedNames =
        {
            "Quake",
            "Scourge of Armagon",
            "Dissolution of Eternity",
            "Dimension of the Past",
            "Dimension of the Machine",
            "Dawn of the Machine",
            "Capture the Flag"
        };

        List<MissionPack> enhancedMissionPacks = new();

        for (int i = 0; i < enhancedDirectories.Length; i++)
        {
            string directory =
                enhancedDirectories[i];

            string enhancedPath =
                Path.Combine(
                    rereleaseFolder,
                    directory);

            if (!Directory.Exists(enhancedPath))
            {
                continue;
            }

            enhancedMissionPacks.Add(
                new MissionPack
                {
                    Name = enhancedNames[i],
                    DetectedDirectory =
                        Path.Combine(
                            "rerelease",
                            directory)
                });
        }

        // Put the Enhanced episodes first, in the official order.
        missionPacks.InsertRange(
            0,
            enhancedMissionPacks);
    }

    public static bool IsClassicQuakeExecutable(
        string executablePath)
    {
        string executableName =
            Path.GetFileName(
                executablePath);

        return
            string.Equals(
                executableName,
                "quake.exe",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                executableName,
                "glquake.exe",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                executableName,
                "qwcl.exe",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                executableName,
                "Winquake.exe",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                executableName,
                "glqwcl.exe",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                executableName,
                "quake2.exe",
                StringComparison.OrdinalIgnoreCase);
    }

    public List<MissionPack> DetectMissionPacks(
        Engine engine,
        string detectionFolder,
        MissionPackDetector missionPackDetector)
    {
        List<MissionPack> missionPacks;

        if (IsClassicQuakeExecutable(engine.ExecutablePath))
        {
            missionPacks =
                DetectClassicQuakeFolders(detectionFolder);
        }
        else
        {
            missionPacks =
                missionPackDetector
                    .DetectMissionPacks(detectionFolder);

            AddQuakeSteamEnhancedMissionPacks(
                missionPacks,
                detectionFolder);
        }

        string rereleaseFolder =
            Path.Combine(
                detectionFolder,
                "rerelease");

        bool hasRerelease =
            Directory.Exists(rereleaseFolder);

        // For Quake GOG, display only the rerelease folder.
        bool isQuake1Gog =
            string.Equals(
                engine.Name,
                "Quake GOG",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                engine.Name,
                "Quake 1 GOG",
                StringComparison.OrdinalIgnoreCase);

        if (isQuake1Gog && hasRerelease)
        {
            missionPacks =
                missionPacks
                    .Where(
                        missionPack =>
                            missionPack.DetectedDirectory?
                                .StartsWith(
                                    "rerelease" + Path.DirectorySeparatorChar,
                                    StringComparison.OrdinalIgnoreCase) == true ||
                            missionPack.DetectedDirectory?
                                .StartsWith(
                                    "rerelease/",
                                    StringComparison.OrdinalIgnoreCase) == true ||
                            missionPack.DetectedDirectory?
                                .StartsWith(
                                    "rerelease\\",
                                    StringComparison.OrdinalIgnoreCase) == true)
                    .ToList();
        }

        missionPacks.RemoveAll(
            missionPack =>
                string.Equals(
                    missionPack.Name,
                    "rerelease",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    missionPack.DetectedDirectory,
                    "rerelease",
                    StringComparison.OrdinalIgnoreCase));

        MissionPack? captureTheFlag =
            missionPacks.FirstOrDefault(
                missionPack =>
                    string.Equals(
                        missionPack.DetectedDirectory,
                        "ctf",
                        StringComparison.OrdinalIgnoreCase));

        if (captureTheFlag != null)
        {
            captureTheFlag.Name =
                "Capture the Flag";

            missionPacks.Remove(captureTheFlag);

            int lastKnownEpisodeIndex =
                missionPacks.FindLastIndex(
                    missionPack =>
                        string.Equals(
                            missionPack.DetectedDirectory,
                            "mg3",
                            StringComparison.OrdinalIgnoreCase));

            if (lastKnownEpisodeIndex >= 0)
            {
                missionPacks.Insert(
                    lastKnownEpisodeIndex + 1,
                    captureTheFlag);
            }
            else
            {
                missionPacks.Add(captureTheFlag);
            }
        }

        return missionPacks;
    }

    public List<Demo> DetectQuake1Demos(string gameFolder)
    {
        if (string.IsNullOrWhiteSpace(gameFolder) ||
            !Directory.Exists(gameFolder))
        {
            return new List<Demo>();
        }

        return demoDetector.DetectDemos(gameFolder);
    }
}
