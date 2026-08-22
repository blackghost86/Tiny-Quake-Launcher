using System.IO;
using TinyQuakeLauncher.Models;

namespace TinyQuakeLauncher.Services;

public class MissionPackDetector
{
    private readonly List<MissionPack> knownMissionPacks =
        new()
        {
            new MissionPack
            {
                Name = "Vanilla Quake",
                GameDirectory = "id1"
            },

            new MissionPack
            {
                Name = "Scourge of Armagon",
                GameDirectory = "hipnotic"
            },

            new MissionPack
            {
                Name = "Dissolution of Eternity",
                GameDirectory = "rogue"
            },

            new MissionPack
            {
                Name = "Arcane Dimensions",
                GameDirectory = "ad"
            },

            new MissionPack
            {
                Name = "Capture the Flag",
                GameDirectory = "ctf"
            },

            new MissionPack
            {
                Name = "Honey",
                GameDirectory = "honey"
            },

            new MissionPack
            {
                Name = "Dimension of the Past",
                GameDirectory = "dopa"
            },

            new MissionPack
            {
                Name = "Dimension of the Machine",
                GameDirectory = "mg1"
            },

            new MissionPack
            {
                Name = "Dawn of the Machine",
                GameDirectory = "mg3"
            }
        };

    public List<MissionPack> DetectMissionPacks(string quakeFolder)
    {
        List<MissionPack> installedPacks = new();

        if (!Directory.Exists(quakeFolder))
        {
            return installedPacks;
        }

        foreach (MissionPack missionPack in knownMissionPacks)
        {
            string gamePath = Path.Combine(
                quakeFolder,
                missionPack.GameDirectory);

            if (Directory.Exists(gamePath))
            {
                installedPacks.Add(missionPack);
            }
        }

        return installedPacks;
    }
}