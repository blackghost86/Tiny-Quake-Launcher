using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TinyQuakeLauncher.Models;
using TinyQuakeLauncher.Services;

namespace TinyQuakeLauncher;

public class Quake3Handler
{
    public static bool IsQuake3Executable(
        string executablePath)
    {
        string executableName =
            Path.GetFileName(executablePath);

        return
            string.Equals(
                executableName,
                "quake3.exe",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                executableName,
                "quake3e.exe",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                executableName,
                "ioquake3.exe",
                StringComparison.OrdinalIgnoreCase);
    }

    public static List<MissionPack> DetectClassicQuake3Folders(
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
            "baseq3",
            "missionpack"
        };

        string[] knownNames =
        {
            "Quake III: Arena",
            "Quake III: Team Arena"
        };

        // Only these known Quake 3 game folders are displayed.
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

    public List<MissionPack> DetectMissionPacks(
        Engine engine,
        string detectionFolder,
        MissionPackDetector3 missionPackDetector3)
    {
        if (IsQuake3Executable(engine.ExecutablePath))
        {
            return DetectClassicQuake3Folders(
                detectionFolder);
        }

        return missionPackDetector3
            .DetectMissionPacks(detectionFolder);
    }

    public string GetDefaultMap(
        MissionPack missionPack)
    {
        if (string.Equals(
                missionPack?.Name,
                "Quake III: Team Arena",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                missionPack?.DetectedDirectory,
                "missionpack",
                StringComparison.OrdinalIgnoreCase))
        {
            return "mpteam1";
        }

        return "q3dm0";
    }

    public List<Demo> DetectQuake3Demos(
        string gameFolder)
    {
        if (string.IsNullOrWhiteSpace(gameFolder) ||
            !Directory.Exists(gameFolder))
        {
            return new List<Demo>();
        }

        return new DemoDetector3()
            .DetectDemos(gameFolder);
    }
}