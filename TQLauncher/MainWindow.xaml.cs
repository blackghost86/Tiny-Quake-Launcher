using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TinyQuakeLauncher.Models;
using TinyQuakeLauncher.Services;

namespace TinyQuakeLauncher;

public partial class MainWindow : Window
{
    private readonly EngineDetector engineDetector = new();

    private readonly MissionPackDetector missionPackDetector = new();

    private readonly PakMapDetector pakMapDetector = new();

    public MainWindow()
    {
        InitializeComponent();

        SetupDifficultyOptions();

        UpdateCommandArguments();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        using System.Windows.Forms.FolderBrowserDialog dialog =
            new System.Windows.Forms.FolderBrowserDialog();

        dialog.Description =
            "";

        if (dialog.ShowDialog() ==
            System.Windows.Forms.DialogResult.OK)
        {
            QuakeFolderTextBox.Text =
                dialog.SelectedPath;

            DetectQuakeInstallation(
                dialog.SelectedPath);

            // Default difficulty is set to Normal.
            DifficultyComboBox.SelectedIndex = 1;
        }
    }

    private void DetectQuakeInstallation(
        string quakeFolder)
    {
        DetectEngines(quakeFolder);

        DetectMissionPacks(quakeFolder);
    }

    private void DetectEngines(string quakeFolder)
    {
        EngineComboBox.Items.Clear();

        List<Engine> engines =
            engineDetector.DetectEngines(quakeFolder);

        foreach (Engine engine in engines)
        {
            EngineComboBox.Items.Add(engine);
        }

        if (EngineComboBox.Items.Count > 0)
        {
            EngineComboBox.SelectedIndex = 0;
        }
    }

    private void DetectMissionPacks(string quakeFolder)
    {
        MissionComboBox.Items.Clear();

        List<MissionPack> missionPacks =
            missionPackDetector
                .DetectMissionPacks(quakeFolder);

        foreach (MissionPack missionPack in missionPacks)
        {
            MissionComboBox.Items.Add(missionPack);
        }

        if (MissionComboBox.Items.Count > 0)
        {
            MissionComboBox.SelectedIndex = 0;

            StatusText.Text =
                $"Found {EngineComboBox.Items.Count} engine(s) and " +
                $"{MissionComboBox.Items.Count} game(s).";
        }
        else
        {
            StatusText.Text =
                "No Quake game directories found.";
        }

        DetectMaps();
    }

    private string GetEpisodeFolder(MissionPack missionPack)
    {
        string quakeFolder =
            QuakeFolderTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(quakeFolder))
        {
            return "";
        }

        string gameDirectory =
            missionPack.GameDirectory?.Trim() ?? "";

        // Vanilla Quake.
        if (string.IsNullOrWhiteSpace(gameDirectory))
        {
            string id1Folder =
                Path.Combine(
                    quakeFolder,
                    "id1");

            if (Directory.Exists(id1Folder))
            {
                return id1Folder;
            }

            return quakeFolder;
        }

        // Some detectors may return an absolute path.
        if (Path.IsPathRooted(gameDirectory))
        {
            return gameDirectory;
        }

        // Normal mission pack directory.
        return Path.Combine(
            quakeFolder,
            gameDirectory);
    }

    private void DetectMaps()
    {
        MapComboBox.Items.Clear();

        MissionPack? missionPack =
            MissionComboBox.SelectedItem as MissionPack;

        if (missionPack == null)
        {
            UpdateCommandArguments();
            return;
        }

        string gameFolder =
            GetEpisodeFolder(missionPack);

        if (string.IsNullOrWhiteSpace(gameFolder))
        {
            UpdateCommandArguments();
            return;
        }

        if (!Directory.Exists(gameFolder))
        {
            StatusText.Text =
                $"Episode folder not found:\n{gameFolder}";

            UpdateCommandArguments();
            return;
        }

        // Detect maps from the selected episode folder.
        List<MapInfo> maps =
            pakMapDetector.DetectMaps(gameFolder);

        // Exclude test maps from every episode/mod.
        maps = maps
            .Where(
                map =>
                {
                    string mapName =
                        Path.GetFileNameWithoutExtension(
                            map.FileName);

                    return !mapName.StartsWith(
                               "b_",
                               StringComparison.OrdinalIgnoreCase)
                        && !mapName.StartsWith(
                               "test_",
                               StringComparison.OrdinalIgnoreCase);
                })
            .ToList();

        foreach (MapInfo map in maps)
        {
            MapComboBox.Items.Add(map);
        }

        // Prefer start.bsp.
        int startIndex =
            maps.FindIndex(
                map => string.Equals(
                    map.FileName,
                    "start.bsp",
                    StringComparison.OrdinalIgnoreCase));

        if (startIndex >= 0)
        {
            MapComboBox.SelectedIndex =
                startIndex;
        }
        else if (MapComboBox.Items.Count > 0)
        {
            MapComboBox.SelectedIndex = 0;
        }

        UpdateCommandArguments();
    }

    private void EngineComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        UpdateCommandArguments();
    }

    private void MissionComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        DetectMaps();
    }

    private void MapComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        UpdateCommandArguments();
    }

    private static System.Windows.Media.Brush HexBrush(string hex)
    {
        return new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)
                System.Windows.Media.ColorConverter.ConvertFromString(hex));
    }

    private void SetupDifficultyOptions()
    {
        DifficultyComboBox.Items.Clear();

        DifficultyComboBox.Items.Add(
            new Difficulty
            {
                Name = "Easy",
                Value = 0,
                Foreground = HexBrush("#000000")
            });

        DifficultyComboBox.Items.Add(
            new Difficulty
            {
                Name = "Normal",
                Value = 1,
                Foreground = HexBrush("#000000")
            });

        DifficultyComboBox.Items.Add(
            new Difficulty
            {
                Name = "Hard",
                Value = 2,
                Foreground = HexBrush("#A64B00")
            });

        DifficultyComboBox.Items.Add(
            new Difficulty
            {
                Name = "Nightmare",
                Value = 3,
                Foreground = HexBrush("#990000")
            });

        DifficultyComboBox.SelectedIndex = -1;
    }

    private void DifficultyComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        UpdateCommandArguments();
    }

    private void UpdateCommandArguments()
    {
        Engine? engine =
            EngineComboBox.SelectedItem as Engine;

        MissionPack? missionPack =
            MissionComboBox.SelectedItem as MissionPack;

        MapInfo? selectedMap =
            MapComboBox.SelectedItem as MapInfo;

        string? mapName = null;

        if (selectedMap != null)
        {
            mapName =
                Path.GetFileNameWithoutExtension(
                    selectedMap.FileName);
        }

        if (engine == null)
        {
            CommandArgumentsTextBox.Text = "";
            return;
        }

        List<string> arguments = new();

        // Episode
        if (missionPack != null &&
            !string.IsNullOrWhiteSpace(
                missionPack.GameDirectory))
        {
            arguments.Add(
                "-game " + missionPack.GameDirectory);
        }

        // Difficulty
        if (DifficultyComboBox.SelectedItem is Difficulty difficulty)
        {
            arguments.Add(
                "+skill " + difficulty.Value);
        }

        // Map
        if (!string.IsNullOrWhiteSpace(mapName))
        {
            arguments.Add(
                "+map " + mapName);
        }

        string argumentString =
            string.Join(" ", arguments);

        CommandArgumentsTextBox.Text =
            Path.GetFileName(engine.ExecutablePath) +
            " " +
            argumentString;
    }

    private void LaunchButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        LaunchQuake();
    }

    private void LaunchQuake()
    {
        Engine? engine =
            EngineComboBox.SelectedItem as Engine;

        MissionPack? missionPack =
            MissionComboBox.SelectedItem as MissionPack;

        MapInfo? selectedMap =
            MapComboBox.SelectedItem as MapInfo;

        string? mapName = null;

        if (selectedMap != null)
        {
            mapName =
                Path.GetFileNameWithoutExtension(
                    selectedMap.FileName);
        }

        string quakeFolder =
            QuakeFolderTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(quakeFolder))
        {
            StatusText.Text =
                "Please select your Quake folder.";

            System.Windows.MessageBox.Show(
                "Please select your Quake folder first.",
                "Tiny Quake Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        if (!Directory.Exists(quakeFolder))
        {
            StatusText.Text =
                "Quake folder not found.";

            System.Windows.MessageBox.Show(
                "The selected Quake folder could not be found.\n\n" +
                $"Folder:\n{quakeFolder}",
                "Tiny Quake Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return;
        }

        if (engine == null)
        {
            StatusText.Text =
                "Please select an engine.";

            System.Windows.MessageBox.Show(
                "Please select an engine before launching.",
                "Tiny Quake Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        if (!File.Exists(engine.ExecutablePath))
        {
            StatusText.Text =
                "Engine executable not found.";

            System.Windows.MessageBox.Show(
                "The selected Quake engine could not be found.\n\n" +
                $"Executable:\n{engine.ExecutablePath}\n\n" +
                "Try selecting the Quake folder again.",
                "Tiny Quake Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return;
        }

        List<string> arguments = new();

        // -----------------------------------------
        // Episode
        // -----------------------------------------

        if (missionPack != null &&
            !string.IsNullOrWhiteSpace(
                missionPack.GameDirectory))
        {
            arguments.Add("-game");
            arguments.Add(missionPack.GameDirectory);
        }

        // -----------------------------------------
        // Difficulty
        // -----------------------------------------

        if (DifficultyComboBox.SelectedItem is string difficulty)
        {
            int skill = 1;

            if (difficulty == "Easy")
            {
                skill = 0;
            }
            else if (difficulty == "Normal")
            {
                skill = 1;
            }
            else if (difficulty == "Hard")
            {
                skill = 2;
            }
            else if (difficulty == "Nightmare")
            {
                skill = 3;
            }

            arguments.Add("+skill");
            arguments.Add(skill.ToString());
        }

        // -----------------------------------------
        // Map
        // -----------------------------------------

        if (!string.IsNullOrWhiteSpace(mapName))
        {
            arguments.Add("+map");
            arguments.Add(mapName);
        }

        try
        {
            ProcessStartInfo startInfo =
                new ProcessStartInfo
                {
                    FileName = engine.ExecutablePath,
                    WorkingDirectory = quakeFolder,
                    UseShellExecute = true
                };

            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            Process.Start(startInfo);

            StatusText.Text =
                $"Running {engine.Name} with custom settings.";
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            StatusText.Text =
                "Could not start the engine.";

            System.Windows.MessageBox.Show(
                "Tiny Quake Launcher could not start the selected engine.\n\n" +
                $"Engine:\n{engine.ExecutablePath}\n\n" +
                $"Error:\n{ex.Message}",
                "Tiny Quake Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch (System.Exception ex)
        {
            StatusText.Text =
                "An unexpected error occurred.";

            System.Windows.MessageBox.Show(
                "An unexpected error occurred while starting Quake.\n\n" +
                $"Error:\n{ex.Message}",
                "Tiny Quake Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}