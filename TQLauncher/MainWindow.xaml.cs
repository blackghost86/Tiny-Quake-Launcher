using System.Diagnostics;
using System.IO;
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

        UpdateCommandArguments();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        using System.Windows.Forms.FolderBrowserDialog dialog =
            new System.Windows.Forms.FolderBrowserDialog();

        dialog.Description =
            "Select your Quake installation folder";

        if (dialog.ShowDialog() ==
            System.Windows.Forms.DialogResult.OK)
        {
            QuakeFolderTextBox.Text =
                dialog.SelectedPath;

            DetectQuakeInstallation(
                dialog.SelectedPath);
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
                $"Status: Found {EngineComboBox.Items.Count} engine(s) and " +
                $"{MissionComboBox.Items.Count} game(s).";
        }
        else
        {
            StatusText.Text =
                "Status: No Quake game directories found.";
        }

        DetectMaps();
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

        string quakeFolder =
            QuakeFolderTextBox.Text.Trim();

        if (!Directory.Exists(quakeFolder))
        {
            UpdateCommandArguments();
            return;
        }

        string gameFolder =
            Path.Combine(
                quakeFolder,
                missionPack.GameDirectory);

        List<string> maps =
            pakMapDetector.DetectMaps(gameFolder);

        foreach (string map in maps)
        {
            MapComboBox.Items.Add(map);
        }

        if (MapComboBox.Items.Count > 0)
        {
            // Prefer start.bsp when it exists.
            int startMapIndex =
                maps.FindIndex(
                    map => string.Equals(
                        map,
                        "start.bsp",
                        StringComparison.OrdinalIgnoreCase));

            if (startMapIndex >= 0)
            {
                MapComboBox.SelectedIndex =
                    startMapIndex;
            }
            else
            {
                // Otherwise select the first map.
                MapComboBox.SelectedIndex = 0;
            }
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

        UpdateCommandArguments();
    }

    private void MapComboBox_SelectionChanged(
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

        string? map =
            MapComboBox.SelectedItem as string;

        if (engine == null)
        {
            CommandArgumentsTextBox.Text = "";
            return;
        }

        List<string> arguments = new();

        if (missionPack != null &&
            !string.IsNullOrWhiteSpace(
                missionPack.GameDirectory))
        {
            arguments.Add(
                $"-game {missionPack.GameDirectory}");
        }

        if (!string.IsNullOrWhiteSpace(map))
        {
            arguments.Add(
                $"+map {map}");
        }

        string argumentString =
            string.Join(" ", arguments);

        CommandArgumentsTextBox.Text =
            $"{Path.GetFileName(engine.ExecutablePath)} " +
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

        string? map =
            MapComboBox.SelectedItem as string;

        string quakeFolder =
            QuakeFolderTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(quakeFolder))
        {
            StatusText.Text =
                "Status: Please select your Quake folder.";

            System.Windows.MessageBox.Show(
                "Please select your Quake installation folder first.",
                "Tiny Quake Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        if (!Directory.Exists(quakeFolder))
        {
            StatusText.Text =
                "Status: Quake folder not found.";

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
                "Status: Please select an engine.";

            System.Windows.MessageBox.Show(
                "Please select a Quake engine before launching.",
                "Tiny Quake Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        if (!File.Exists(engine.ExecutablePath))
        {
            StatusText.Text =
                "Status: Engine executable not found.";

            System.Windows.MessageBox.Show(
                "The selected Quake engine executable could not be found.\n\n" +
                $"Executable:\n{engine.ExecutablePath}\n\n" +
                "Try selecting the Quake folder again.",
                "Tiny Quake Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return;
        }

        List<string> arguments = new();

        if (missionPack != null &&
            !string.IsNullOrWhiteSpace(
                missionPack.GameDirectory))
        {
            arguments.Add(
                $"-game {missionPack.GameDirectory}");
        }

        if (!string.IsNullOrWhiteSpace(map))
        {
            arguments.Add(
                $"+map {map}");
        }

        string argumentString =
            string.Join(" ", arguments);

        try
        {
            ProcessStartInfo startInfo =
                new ProcessStartInfo
                {
                    FileName = engine.ExecutablePath,
                    Arguments = argumentString,
                    WorkingDirectory = quakeFolder,
                    UseShellExecute = true
                };

            Process.Start(startInfo);

            StatusText.Text =
                $"Status: Started {engine.Name} with custom settings.";
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            StatusText.Text =
                "Status: Could not start the engine.";

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
                "Status: An unexpected error occurred.";

            System.Windows.MessageBox.Show(
                "An unexpected error occurred while starting Quake.\n\n" +
                $"Error:\n{ex.Message}",
                "Tiny Quake Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}