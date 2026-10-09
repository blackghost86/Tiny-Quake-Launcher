using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TinyQuakeLauncher.Games;

namespace TinyQuakeLauncher.Profiles;

public sealed class LauncherProfile
{
    public string Name { get; set; } = "";

    public string QuakeFolder { get; set; } = "";

    public string EnginePath { get; set; } = "";

    public QuakeGame EngineGame { get; set; } = QuakeGame.Quake1;

    public string Resolution { get; set; } = "";

    public string MissionPackDirectory { get; set; } = "";

    public string MapFileName { get; set; } = "";

    public int? Difficulty { get; set; }

    public int? Mode { get; set; }
    public bool ModeSelectionCleared { get; set; }

    public bool FragLimitEnabled { get; set; }

    public bool FlagLimitEnabled { get; set; }

    public bool TimeLimitEnabled { get; set; }

    public bool MaxPlayersEnabled { get; set; }

    public string ExtraArguments { get; set; } = "";
}

public sealed class Profiles
{
    private static readonly string ProfilesFolder =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "TinyQuakeLauncher");

    private static string GetProfilesFile(string fileName)
    {
        return Path.Combine(
            ProfilesFolder,
            fileName);
    }

    public List<LauncherProfile> Items { get; set; } = new();

    public static Profiles Load(string fileName)
    {
        string profilesFile = GetProfilesFile(fileName);

        try
        {
            if (File.Exists(profilesFile))
            {
                string json =
                    File.ReadAllText(profilesFile);

                Profiles? profiles =
                    JsonSerializer.Deserialize<Profiles>(json);

                if (profiles != null)
                {
                    profiles.Items ??= new List<LauncherProfile>();
                    return profiles;
                }
            }
        }
        catch
        {
            // Fall back to an empty profile collection.
        }

        return new Profiles();
    }

    public void Save(string fileName)
    {
        string profilesFile = GetProfilesFile(fileName);

        Directory.CreateDirectory(
            ProfilesFolder);

        JsonSerializerOptions options =
            new()
            {
                WriteIndented = true
            };

        File.WriteAllText(
            profilesFile,
            JsonSerializer.Serialize(
                this,
                options));
    }

    public LauncherProfile? Find(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return Items.FirstOrDefault(
            profile =>
                string.Equals(
                    profile.Name,
                    name,
                    StringComparison.OrdinalIgnoreCase));
    }

    public void AddOrReplace(LauncherProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        LauncherProfile? existing =
            Find(profile.Name);

        if (existing == null)
        {
            Items.Add(profile);
            return;
        }

        int index = Items.IndexOf(existing);

        Items[index] = profile;
    }

    public bool Remove(string name)
    {
        // Remove the profile with the specified name.
        LauncherProfile? profile =
            Find(name);

        if (profile == null)
        {
            return false;
        }

        return Items.Remove(profile);
    }
}