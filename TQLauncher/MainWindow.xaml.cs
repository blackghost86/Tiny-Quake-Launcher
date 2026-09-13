using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using TinyQuakeLauncher.Data;
using TinyQuakeLauncher.Games;
using TinyQuakeLauncher.Models;
using TinyQuakeLauncher.Services;

namespace TinyQuakeLauncher;

public class LauncherSettings
{
    public string QuakeFolder { get; set; } = "";

    public string EnginePath { get; set; } = "";

    public QuakeGame EngineGame { get; set; } = QuakeGame.Quake1;

    public string MissionPackDirectory { get; set; } = "";

    public string MapFileName { get; set; } = "";

    public bool MapSelectionCleared { get; set; }

    public bool DontShowMapClearedWarning { get; set; }

    public int? Difficulty { get; set; }

    public bool DifficultySelectionCleared { get; set; }

    public string DemoFileName { get; set; } = "";

    public bool CloseAfterLaunch { get; set; }

    public string ExtraArguments { get; set; } = "";
}

public partial class MainWindow : Window
{
    private readonly EngineDetector engineDetector = new();

    private readonly EngineDetector2 engineDetector2 = new();

    private readonly MissionPackDetector missionPackDetector = new();

    private readonly MissionPackDetector2 missionPackDetector2 = new();

    private readonly MapDetector MapDetector = new();

    private readonly MapDetector2 MapDetector2 = new();

    private readonly DemoDetector demoDetector = new();
    private readonly DemoDetector2 demoDetector2 = new();

    private static readonly string SettingsFolder =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "TinyQuakeLauncher");

    private static readonly string SettingsFile =
        Path.Combine(
            SettingsFolder,
            "TQLauncher.json");

    private bool restoreMapSelectionCleared;
    private bool restoreDifficultySelectionCleared;
    private bool restoringSavedSelections;
    private bool demoSelectionActive;
    private bool updatingCommandArguments;
    private bool commandArgumentsEdited;
    private string lastAcceptedQuakeFolder = "";

    public MainWindow()
    {
        InitializeComponent();

        CommandArgumentsTextBox.ToolTip =
            "Command line arguments based on preferred settings";

        // The command line preview can also be edited directly. Manual
        // changes are used for the next launch/shortcut until another
        // launcher setting refreshes the generated command line.
        CommandArgumentsTextBox.IsReadOnly = false;
        CommandArgumentsTextBox.TextChanged +=
            CommandArgumentsTextBox_TextChanged;

        ClearExtraArgumentsButton.IsEnabled = false;

        Closing += MainWindow_Closing;

        QuakeFolderTextBox.TextChanged +=
            QuakeFolderTextBox_TextChanged;

        // Handle Enter directly in code so pressing Enter in the
        // game address bar always refreshes the entered path.
        QuakeFolderTextBox.KeyDown +=
            QuakeFolderTextBox_KeyDown;

        RefreshEnginesButton.Click +=
            RefreshEnginesButton_Click;

        RefreshEpisodesButton.Click +=
            RefreshEpisodesButton_Click;

        QuakeFolderTextBox.SizeChanged +=
            QuakeFolderTextBox_SizeChanged;

        MapComboBox.SizeChanged +=
            MapComboBox_SizeChanged;

        DemoComboBox.SizeChanged +=
            DemoComboBox_SizeChanged;

        LoadSavedQuakeFolder();

        UpdateCommandArguments();
    }

    private void LoadSavedQuakeFolder()
    {
        try
        {
            if (!File.Exists(SettingsFile))
            {
                return;
            }

            string json =
                File.ReadAllText(SettingsFile);

            LauncherSettings? settings =
                JsonSerializer.Deserialize<LauncherSettings>(json);

            if (settings == null ||
                string.IsNullOrWhiteSpace(settings.QuakeFolder))
            {
                return;
            }

            if (!Directory.Exists(settings.QuakeFolder))
            {
                System.Windows.MessageBox.Show(
                    "Quake folder was moved or deleted.",
                    "Tiny Quake Launcher",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            QuakeFolderTextBox.Text =
                settings.QuakeFolder;

            lastAcceptedQuakeFolder =
                settings.QuakeFolder.Trim();

            restoreMapSelectionCleared =
                settings.MapSelectionCleared;

            restoreDifficultySelectionCleared =
                settings.DifficultySelectionCleared;

            DetectQuakeInstallation(
                settings.QuakeFolder);

            restoringSavedSelections = true;
            RestoreSavedSelections(settings);
            restoringSavedSelections = false;

            if (EngineComboBox.Items.Count == 0)
            {
                // Saved settings may have triggered selection-change handlers
                // while being restored. Force all Clear buttons back to the
                // unavailable state when no supported engine was found.
                ClearMapButton.IsEnabled = false;
                ClearDifficultyButton.IsEnabled = false;
                ClearDemoButton.IsEnabled = false;
                ClearExtraArgumentsButton.IsEnabled = false;
            }

            CloseAfterLaunchCheckBox.IsChecked =
                settings.CloseAfterLaunch;

            // The saved "map cleared" state only applies to
            // the initial startup restore.
            restoreMapSelectionCleared = false;
            restoreDifficultySelectionCleared = false;
        }
        catch
        {
            // Ignore errors and let the user
            // select a folder manually.
        }
    }

    private void SaveQuakeFolder(
        string folder)
    {
        try
        {
            Directory.CreateDirectory(
                SettingsFolder);

            LauncherSettings settings =
                LoadSettings();

            settings.QuakeFolder =
                folder;

            SaveSettings(settings);
        }
        catch
        {
            // Ignore errors.
        }
    }

    private LauncherSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string json =
                    File.ReadAllText(SettingsFile);

                LauncherSettings? settings =
                    JsonSerializer.Deserialize<LauncherSettings>(json);

                if (settings != null)
                {
                    return settings;
                }
            }
        }
        catch
        {
            // Fall back to default settings.
        }

        return new LauncherSettings();
    }

    private void SaveSettings(
        LauncherSettings settings)
    {
        Directory.CreateDirectory(
            SettingsFolder);

        JsonSerializerOptions options =
            new()
            {
                WriteIndented = true
            };

        File.WriteAllText(
            SettingsFile,
            JsonSerializer.Serialize(
                settings,
                options));
    }

    private void SaveCurrentSettings()
    {
        try
        {
            LauncherSettings settings =
                LoadSettings();

            settings.QuakeFolder =
                QuakeFolderTextBox.Text.Trim();

            Engine? engine =
                EngineComboBox.SelectedItem as Engine;

            settings.EnginePath =
                engine?.ExecutablePath ?? "";

            settings.EngineGame =
                engine?.Game ?? QuakeGame.Quake1;

            MissionPack? missionPack =
                MissionComboBox.SelectedItem as MissionPack;

            settings.MissionPackDirectory =
                missionPack?.GameDirectory ?? "";

            MapInfo? selectedMap =
                MapComboBox.SelectedItem as MapInfo;

            settings.MapFileName =
                selectedMap?.FileName ?? "";

            settings.MapSelectionCleared =
                selectedMap == null ||
                string.IsNullOrWhiteSpace(selectedMap.FileName);

            if (DifficultyComboBox.SelectedItem is Difficulty difficulty)
            {
                settings.Difficulty =
                    difficulty.Value;

                settings.DifficultySelectionCleared = false;
            }
            else
            {
                settings.Difficulty = null;

                settings.DifficultySelectionCleared = true;
            }

            Demo? selectedDemo =
                DemoComboBox.SelectedItem as Demo;

            settings.DemoFileName =
                selectedDemo?.FileName ?? "";

            settings.CloseAfterLaunch =
                CloseAfterLaunchCheckBox.IsChecked == true;

            settings.ExtraArguments =
                ExtraArgumentsTextBox.Text;

            SaveSettings(settings);
        }
        catch
        {
            // Ignore errors.
        }
    }

    private void RestoreSavedSelections(
        LauncherSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.EnginePath))
        {
            Engine? engine =
                EngineComboBox.Items
                    .OfType<Engine>()
                    .FirstOrDefault(
                        item => string.Equals(
                            item.ExecutablePath,
                            settings.EnginePath,
                            StringComparison.OrdinalIgnoreCase) &&
                        item.Game == settings.EngineGame);

            if (engine != null)
            {
                EngineComboBox.SelectedItem =
                    engine;

                // Rebuild the episode list for the saved engine before restoring
                // the saved episode. This is required when the initially
                // detected engine differs from the saved engine.
                DetectMissionPacks(
                    QuakeFolderTextBox.Text.Trim());
            }
        }

        if (!string.IsNullOrWhiteSpace(
            settings.MissionPackDirectory))
        {
            MissionPack? missionPack =
                MissionComboBox.Items
                    .OfType<MissionPack>()
                    .FirstOrDefault(
                        item => string.Equals(
                            item.GameDirectory,
                            settings.MissionPackDirectory,
                            StringComparison.OrdinalIgnoreCase));

            if (missionPack != null)
            {
                MissionComboBox.SelectedItem =
                    missionPack;
            }
        }

        if (settings.MapSelectionCleared)
        {
            MapComboBox.SelectedIndex = 0;
        }
        else if (!string.IsNullOrWhiteSpace(
            settings.MapFileName))
        {
            MapInfo? map =
                MapComboBox.Items
                    .OfType<MapInfo>()
                    .FirstOrDefault(
                        item => string.Equals(
                            item.FileName,
                            settings.MapFileName,
                            StringComparison.OrdinalIgnoreCase));

            if (map != null)
            {
                MapComboBox.SelectedItem =
                    map;
            }
        }

        if (settings.DifficultySelectionCleared)
        {
            DifficultyComboBox.SelectedIndex = 0;
        }
        else if (settings.Difficulty.HasValue)
        {
            Difficulty? difficulty =
                DifficultyComboBox.Items
                    .OfType<Difficulty>()
                    .FirstOrDefault(
                        item => item.Value ==
                            settings.Difficulty.Value);

            if (difficulty != null)
            {
                DifficultyComboBox.SelectedItem =
                    difficulty;
            }
        }

        if (!string.IsNullOrWhiteSpace(settings.DemoFileName))
        {
            Demo? demo =
                DemoComboBox.Items
                    .OfType<Demo>()
                    .FirstOrDefault(
                        item => string.Equals(
                            item.FileName,
                            settings.DemoFileName,
                            StringComparison.OrdinalIgnoreCase));

            if (demo != null)
            {
                DemoComboBox.SelectedItem = demo;
            }
        }

        ExtraArgumentsTextBox.Text =
            settings.ExtraArguments ?? "";

        UpdateCommandArguments();
    }

    private void MainWindow_Closing(
        object? sender,
        System.ComponentModel.CancelEventArgs e)
    {
        SaveCurrentSettings();
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
            string selectedFolder =
                dialog.SelectedPath;

            if (!ConfirmQuakeFolderSelection(
                    selectedFolder))
            {
                QuakeFolderTextBox.Text =
                    lastAcceptedQuakeFolder;

                return;
            }

            QuakeFolderTextBox.Text =
                selectedFolder;

            lastAcceptedQuakeFolder =
                selectedFolder;

            SaveQuakeFolder(
                selectedFolder);

            DetectQuakeInstallation(
                selectedFolder);

            // Default difficulty is set to Normal.
            if (DifficultyComboBox.Items.Count > 1)
            {
                DifficultyComboBox.SelectedIndex = 2;
            }
        }
    }

    private void QuakeFolderTextBox_TextChanged(
    object sender,
    TextChangedEventArgs e)
    {
        UpdateQuakeFolderToolTip();
    }

    private void QuakeFolderTextBox_KeyDown(
        object sender,
        System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter ||
            e.Key == System.Windows.Input.Key.Return)
        {
            RefreshQuakeFolderFromTextBox();
            e.Handled = true;
        }
    }

    private void RefreshQuakeFolderFromTextBox()
    {
        string folder = QuakeFolderTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(folder))
        {
            StatusText.Text = "Enter a Quake game folder.";
            return;
        }

        if (!Directory.Exists(folder))
        {
            StatusText.Text =
                $"Game folder not found:\n{folder}";
            return;
        }

        if (!ConfirmQuakeFolderSelection(folder))
        {
            QuakeFolderTextBox.Text =
                lastAcceptedQuakeFolder;

            return;
        }

        QuakeFolderTextBox.Text = folder;
        lastAcceptedQuakeFolder = folder;
        SaveQuakeFolder(folder);
        DetectQuakeInstallation(folder);

        if (DifficultyComboBox.Items.Count > 1)
        {
            DifficultyComboBox.SelectedIndex = 2;
        }
    }

    private void QuakeFolderTextBox_SizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        UpdateQuakeFolderToolTip();
    }

    private void UpdateQuakeFolderToolTip()
    {
        string text =
            QuakeFolderTextBox.Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            QuakeFolderTextBox.ToolTip = null;
            return;
        }

        FormattedText formattedText =
            new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface(
                    QuakeFolderTextBox.FontFamily,
                    QuakeFolderTextBox.FontStyle,
                    QuakeFolderTextBox.FontWeight,
                    QuakeFolderTextBox.FontStretch),
                QuakeFolderTextBox.FontSize,
                System.Windows.Media.Brushes.Black,
                VisualTreeHelper.GetDpi(
                    QuakeFolderTextBox).PixelsPerDip);

        double availableWidth =
            QuakeFolderTextBox.ActualWidth -
            QuakeFolderTextBox.Padding.Left -
            QuakeFolderTextBox.Padding.Right -
            10;

        if (formattedText.Width > availableWidth)
        {
            QuakeFolderTextBox.ToolTip = text;
        }
        else
        {
            QuakeFolderTextBox.ToolTip = null;
        }
    }

    private void MapComboBox_SizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        UpdateMapToolTip();
    }

    private void UpdateMapToolTip()
    {
        MapInfo? selectedMap =
            MapComboBox.SelectedItem as MapInfo;

        if (selectedMap == null)
        {
            MapComboBox.ToolTip = null;
            return;
        }

        string displayText =
            $"{selectedMap.FileName} | {selectedMap.Title}";

        if (MapComboBox.ActualWidth <= 0)
        {
            MapComboBox.ToolTip = null;
            return;
        }

        FormattedText formattedText =
            new FormattedText(
                displayText,
                System.Globalization.CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface(
                    MapComboBox.FontFamily,
                    MapComboBox.FontStyle,
                    MapComboBox.FontWeight,
                    MapComboBox.FontStretch),
                MapComboBox.FontSize,
                System.Windows.Media.Brushes.Black,
                VisualTreeHelper.GetDpi(
                    MapComboBox).PixelsPerDip);

        // Leave room for the ComboBox border, padding and drop-down arrow.
        double availableWidth =
            MapComboBox.ActualWidth -
            MapComboBox.Padding.Left -
            MapComboBox.Padding.Right -
            35;

        if (formattedText.Width > availableWidth)
        {
            MapComboBox.ToolTip = displayText;
        }
        else
        {
            MapComboBox.ToolTip = null;
        }
    }

    private void DemoComboBox_SizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        UpdateDemoToolTip();
    }

    private void UpdateDemoToolTip()
    {
        Demo? selectedDemo =
            DemoComboBox.SelectedItem as Demo;

        if (selectedDemo == null ||
            string.IsNullOrWhiteSpace(selectedDemo.FileName))
        {
            DemoComboBox.ToolTip = null;
            return;
        }

        string displayText =
            selectedDemo.Name;

        if (string.IsNullOrWhiteSpace(displayText) ||
            DemoComboBox.ActualWidth <= 0)
        {
            DemoComboBox.ToolTip = null;
            return;
        }

        FormattedText formattedText =
            new FormattedText(
                displayText,
                System.Globalization.CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface(
                    DemoComboBox.FontFamily,
                    DemoComboBox.FontStyle,
                    DemoComboBox.FontWeight,
                    DemoComboBox.FontStretch),
                DemoComboBox.FontSize,
                System.Windows.Media.Brushes.Black,
                VisualTreeHelper.GetDpi(
                    DemoComboBox).PixelsPerDip);

        // Leave room for the ComboBox border, padding and drop-down arrow.
        double availableWidth =
            DemoComboBox.ActualWidth -
            DemoComboBox.Padding.Left -
            DemoComboBox.Padding.Right -
            35;

        if (formattedText.Width > availableWidth)
        {
            DemoComboBox.ToolTip = displayText;
        }
        else
        {
            DemoComboBox.ToolTip = null;
        }
    }

    private string GetEngineGameFolder(
        Engine engine,
        string quakeFolder)
    {
        // Quake 2 gets its own root resolver so selecting a parent folder
        // containing multiple Quake installations does not mix their maps
        // or miss their episode directories. Quake 1 remains unchanged.
        if (engine.Game == QuakeGame.Quake2)
        {
            return GetQuake2EngineGameFolder(
                engine,
                quakeFolder);
        }

        string? engineDirectory =
            Path.GetDirectoryName(
                engine.ExecutablePath);

        if (string.IsNullOrWhiteSpace(engineDirectory) ||
            !Directory.Exists(engineDirectory))
        {
            return quakeFolder;
        }

        string rootFolder =
            Path.GetFullPath(quakeFolder)
                .TrimEnd(Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

        string currentFolder =
            Path.GetFullPath(engineDirectory)
                .TrimEnd(Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

        // If the executable is below the selected folder, identify the
        // actual Quake installation folder containing that executable.
        // This allows selecting a parent folder containing separate
        // Quake 1 and Quake 2 installations.
        if (currentFolder.StartsWith(
                rootFolder + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase) ||
            currentFolder.StartsWith(
                rootFolder + Path.AltDirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            string candidate = currentFolder;

            while (!string.Equals(
                       candidate,
                       rootFolder,
                       StringComparison.OrdinalIgnoreCase))
            {
                if (ContainsGameDirectory(
                        candidate,
                        engine.Game))
                {
                    return candidate;
                }

                DirectoryInfo? parent =
                    Directory.GetParent(candidate);

                if (parent == null)
                {
                    break;
                }

                candidate =
                    parent.FullName
                        .TrimEnd(
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar);
            }

            // No standard game directory was found. Use the first folder
            // below the selected parent, which covers installations whose
            // game data is detected by the generic detectors.
            candidate = currentFolder;

            while (true)
            {
                DirectoryInfo? parent =
                    Directory.GetParent(candidate);

                if (parent == null ||
                    string.Equals(
                        parent.FullName.TrimEnd(
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar),
                        rootFolder,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }

                candidate =
                    parent.FullName
                        .TrimEnd(
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar);
            }
        }

        // The engine is installed outside the selected Quake folder.
        // Keep the selected folder as the game-data root.
        return quakeFolder;
    }

    private static string GetQuake2EngineGameFolder(
        Engine engine,
        string quakeFolder)
    {
        string? engineDirectory =
            Path.GetDirectoryName(
                engine.ExecutablePath);

        if (string.IsNullOrWhiteSpace(engineDirectory) ||
            !Directory.Exists(engineDirectory) ||
            !Directory.Exists(quakeFolder))
        {
            return quakeFolder;
        }

        string rootFolder =
            Path.GetFullPath(quakeFolder)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

        string currentFolder =
            Path.GetFullPath(engineDirectory)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

        // If the engine executable is outside the selected folder, the
        // selected folder remains the game-data root.
        bool isBelowSelectedFolder =
            string.Equals(
                currentFolder,
                rootFolder,
                StringComparison.OrdinalIgnoreCase) ||
            currentFolder.StartsWith(
                rootFolder + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase) ||
            currentFolder.StartsWith(
                rootFolder + Path.AltDirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);

        if (!isBelowSelectedFolder)
        {
            return quakeFolder;
        }

        // Walk upward from the engine executable directory and use the
        // nearest folder containing a recognized Quake 2 game directory.
        // This resolves each engine to its own installation when the user
        // selects a parent folder containing multiple Quake 2 installs.
        // Unlike the old fallback, never guess an arbitrary child folder.
        string candidate = currentFolder;

        while (true)
        {
            if (ContainsGameDirectory(
                    candidate,
                    QuakeGame.Quake2))
            {
                return candidate;
            }

            if (string.Equals(
                    candidate,
                    rootFolder,
                    StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            DirectoryInfo? parent =
                Directory.GetParent(candidate);

            if (parent == null)
            {
                break;
            }

            string parentFolder =
                parent.FullName
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);

            // Do not walk above the folder selected by the user.
            if (!string.Equals(
                    parentFolder,
                    rootFolder,
                    StringComparison.OrdinalIgnoreCase) &&
                !parentFolder.StartsWith(
                    rootFolder + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) &&
                !parentFolder.StartsWith(
                    rootFolder + Path.AltDirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            candidate = parentFolder;
        }

        // No recognized Quake 2 game directory was found. Keep the selected
        // folder rather than guessing, so map/episode detection cannot
        // accidentally combine unrelated installations.
        return quakeFolder;
    }


    private static bool ContainsGameDirectory(
        string folder,
        QuakeGame game)
    {
        string[] directories =
            game == QuakeGame.Quake2
                ? new[]
                {
                    "baseq2",
                    "ctf",
                    "xatrix",
                    "rogue"
                }
                : new[]
                {
                    "id1",
                    "hipnotic",
                    "rogue",
                    "ctf",
                    "rerelease"
                };

        return directories.Any(
            directory =>
                Directory.Exists(
                    Path.Combine(folder, directory)));
    }

    private bool ConfirmQuakeFolderSelection(
        string quakeFolder)
    {
        // If there is no supported engine at all, let DetectQuakeInstallation()
        // display the dedicated "No supported Quake engine" warning instead.
        // The root folder warning is relevant when an engine was found.
        if (!HasSupportedEngine(quakeFolder))
        {
            return true;
        }

        if (IsMainQuakeFolder(quakeFolder))
        {
            return true;
        }

        MessageBoxResult result =
            System.Windows.MessageBox.Show(
                "TQLauncher did not detect a main Quake folder so episode\n" +
                "and map detection could be problematic. Do you want to\n" +
                "continue?",
                "Warning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

        return result == MessageBoxResult.Yes;
    }

    private bool HasSupportedEngine(
        string quakeFolder)
    {
        if (!Directory.Exists(quakeFolder))
        {
            return false;
        }

        List<Engine> engines =
            engineDetector.DetectEngines(quakeFolder);

        engines.AddRange(
            engineDetector2.DetectEngines(quakeFolder));

        // Quakespasm-Spiked can be stored in a special qss folder.
        AddQuakeSpasmSpikedEngine(
            engines,
            quakeFolder);

        return engines.Count > 0;
    }

    private static bool IsMainQuakeFolder(
        string folder)
    {
        if (!Directory.Exists(folder))
        {
            return false;
        }

        return ContainsGameDirectory(
                   folder,
                   QuakeGame.Quake1) ||
               ContainsGameDirectory(
                   folder,
                   QuakeGame.Quake2);
    }

    private void DetectQuakeInstallation(
        string quakeFolder)
    {
        DetectEngines(quakeFolder);

        if (EngineComboBox.Items.Count == 0)
        {
            return;
        }

        DetectMissionPacks(quakeFolder);
    }

    private void DetectEngines(string quakeFolder)
    {
        EngineComboBox.Items.Clear();

        DifficultyComboBox.Items.Clear();
        DifficultyComboBox.SelectedIndex = -1;

        List<Engine> engines =
            engineDetector.DetectEngines(quakeFolder);

        engines.AddRange(
            engineDetector2.DetectEngines(quakeFolder));

        // Quakespasm-Spiked could be inside a "qss" folder.
        AddQuakeSpasmSpikedEngine(
            engines,
            quakeFolder);

        foreach (Engine engine in engines)
        {
            EngineComboBox.Items.Add(engine);
        }

        if (EngineComboBox.Items.Count == 0)
        {
            // No supported engine means the current folder cannot
            // provide valid episode/map/demo selections either.
            MissionComboBox.Items.Clear();
            MissionComboBox.SelectedIndex = -1;

            MapComboBox.Items.Clear();
            MapComboBox.SelectedIndex = -1;
            MapComboBox.ToolTip = null;

            DifficultyComboBox.Items.Clear();
            DifficultyComboBox.SelectedIndex = -1;

            DemoComboBox.Items.Clear();
            DemoComboBox.SelectedItem = null;
            DemoComboBox.SelectedIndex = -1;
            DemoComboBox.ToolTip = null;

            demoSelectionActive = false;

            ClearMapButton.IsEnabled = false;
            ClearDifficultyButton.IsEnabled = false;
            ClearDemoButton.IsEnabled = false;
            ClearExtraArgumentsButton.IsEnabled = false;
            UpdateDemoControlsState();

            // No engine: Demo remains an available empty selector.
            // There is no "None" item when no engine exists.
            DemoComboBox.IsEnabled = true;
            DemoLabel.IsEnabled = true;
            ClearDemoButton.IsEnabled = false;

            // Match the normal enabled text appearance.
            DemoLabel.Foreground =
                System.Windows.SystemColors.ControlTextBrush;
            DemoComboBox.Foreground =
                System.Windows.SystemColors.ControlTextBrush;

            CommandArgumentsTextBox.Document.Blocks.Clear();

            StatusText.Text =
                "No supported Quake engine was found.";

            System.Windows.MessageBox.Show(
                "No supported Quake engine was found.",
                "Warning",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        SetupDifficultyOptions();

        // Do not force a Demo foreground here. The Demo controls use the
        // same normal WPF enabled/disabled styling as Map and Difficulty.
        EngineComboBox.SelectedIndex = 0;
    }

    private static void AddQuakeSpasmSpikedEngine(
        List<Engine> engines,
        string quakeFolder)
    {
        if (!Directory.Exists(quakeFolder))
        {
            return;
        }

        string qssFolder =
            Path.Combine(
                quakeFolder,
                "qss");

        if (!Directory.Exists(qssFolder))
        {
            return;
        }

        string? executablePath =
            Directory.GetFiles(
                    qssFolder,
                    "*.exe",
                    SearchOption.TopDirectoryOnly)
                .FirstOrDefault(
                    path =>
                        Path.GetFileNameWithoutExtension(path)
                            .StartsWith(
                                "quakespasm-spiked",
                                StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return;
        }

        if (engines.Any(
                engine =>
                    string.Equals(
                        engine.ExecutablePath,
                        executablePath,
                        StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        engines.Add(
            new Engine
            {
                Name = "Quakespasm-Spiked",
                ExecutablePath = executablePath,
                Game = QuakeGame.Quake1
            });
    }

    private static bool IsClassicQuakeExecutable(
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

    private static bool IsQuake2Executable(
        string executablePath)
    {
        return string.Equals(
            Path.GetFileName(executablePath),
            "quake2.exe",
            StringComparison.OrdinalIgnoreCase);
    }

    private static List<MissionPack> DetectClassicQuakeFolders(
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
            "ctf"
        };

        string[] knownNames =
        {
            "Quake",
            "Scourge of Armagon",
            "Dissolution of Eternity",
            "Capture the Flag"
        };

        // Only these four known Quake folders are displayed.
        // All other folders, like rerelease, are excluded.
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

    private static List<MissionPack> DetectClassicQuake2Folders(
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
            "baseq2",
            "ctf",
            "xatrix",
            "rogue"
        };

        string[] knownNames =
        {
            "Quake II",
            "Capture the Flag",
            "The Reckoning",
            "Ground Zero"
        };

        // Only these four known Quake 2 folders are displayed.
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

    private void DetectMissionPacks(string quakeFolder)
    {
        MissionComboBox.Items.Clear();

        Engine? engine =
            EngineComboBox.SelectedItem as Engine;

        List<MissionPack> missionPacks;

        string detectionFolder =
            engine == null
                ? quakeFolder
                : GetEngineGameFolder(
                    engine,
                    quakeFolder);

        if (engine?.Game == QuakeGame.Quake1 &&
            IsClassicQuakeExecutable(engine.ExecutablePath))
        {
            missionPacks =
                DetectClassicQuakeFolders(detectionFolder);
        }
        else if (engine?.Game == QuakeGame.Quake2 &&
                 IsQuake2Executable(engine.ExecutablePath))
        {
            missionPacks =
                DetectClassicQuake2Folders(detectionFolder);
        }
        else if (engine?.Game == QuakeGame.Quake2)
        {
            if (string.Equals(
                engine.Name,
                "Quake II GOG",
                StringComparison.OrdinalIgnoreCase))
            {
                missionPacks =
                    missionPackDetector2
                        .DetectGogMissionPacks(detectionFolder);
            }
            else
            {
                missionPacks =
                    missionPackDetector2
                        .DetectMissionPacks(detectionFolder);
            }
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

        if (engine?.Game == QuakeGame.Quake1)
        {
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
        }

        foreach (MissionPack missionPack in missionPacks)
        {
            MissionComboBox.Items.Add(missionPack);
        }

        if (MissionComboBox.Items.Count > 0)
        {
            MissionComboBox.SelectedIndex = 0;

            StatusText.Text =
                $"Found {EngineComboBox.Items.Count} engine(s) and " +
                $"{MissionComboBox.Items.Count} episode(s).";
        }
        else
        {
            StatusText.Text =
                "No Quake game directories found.";
        }

        DetectMaps();
        DetectDemos();
    }

    private static void AddQuakeSteamEnhancedMissionPacks(
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
            "Quake (Enhanced)",
            "Scourge of Armagon (Enhanced)",
            "Dissolution of Eternity (Enhanced)",
            "Dimension of the Past (Enhanced)",
            "Dimension of the Machine (Enhanced)",
            "Dawn of the Machine (Enhanced)",
            "Capture the Flag (Enhanced)"
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

        Engine? engine =
            EngineComboBox.SelectedItem as Engine;

        string engineGameFolder =
            engine == null
                ? quakeFolder
                : GetEngineGameFolder(
                    engine,
                    quakeFolder);

        // Vanilla Quake.
        if (string.IsNullOrWhiteSpace(gameDirectory))
        {
            string id1Folder =
                Path.Combine(
                    engineGameFolder,
                    "id1");

            if (Directory.Exists(id1Folder))
            {
                return id1Folder;
            }

            return engineGameFolder;
        }

        // Some detectors may return an absolute path.
        if (Path.IsPathRooted(gameDirectory))
        {
            return gameDirectory;
        }

        // Normal mission pack directory.
        return Path.Combine(
            engineGameFolder,
            gameDirectory);
    }

    private void DetectMaps()
    {
        MapComboBox.Items.Clear();
        MapComboBox.SelectedIndex = -1;
        MapComboBox.ToolTip = null;

        Engine? engine =
            EngineComboBox.SelectedItem as Engine;

        // With no engine detected, keep Map as an empty, usable drop-down.
        // Do not add or select a synthetic "None" entry.
        if (engine == null)
        {
            MapComboBox.IsEnabled = true;
            ClearMapButton.IsEnabled = false;
            MapLabel.IsEnabled = true;
            MapComboBox.Foreground =
                System.Windows.SystemColors.ControlTextBrush;
            MapLabel.Foreground =
                System.Windows.SystemColors.ControlTextBrush;

            UpdateCommandArguments();
            return;
        }

        MapComboBox.Items.Add(MapInfo.None);

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
            // A root-level PK3/ZIP mission pack has no game directory.
            // Read its maps from the main Quake folder.
            if (string.IsNullOrWhiteSpace(missionPack.GameDirectory))
            {
                gameFolder = GetEngineGameFolder(
                    engine,
                    QuakeFolderTextBox.Text.Trim());
            }
            else
            {
                StatusText.Text =
                    $"Episode folder not found:\n{gameFolder}";

                UpdateCommandArguments();
                return;
            }
        }

        bool isQuake2 =
            engine.Game == QuakeGame.Quake2;

        List<MapInfo> maps;

        if (isQuake2)
        {
            maps =
                MapDetector2.DetectMaps(gameFolder);
        }
        else
        {
            maps =
                MapDetector.DetectMaps(gameFolder);

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
        }

        foreach (MapInfo map in maps)
        {
            if (MapIsInsidePk3OrZip(
                map.FileName,
                gameFolder))
            {
                map.Foreground =
                    HexBrush("#7C3F00");
            }
            else
            {
                map.Foreground =
                    IsMultiplayerMap(map.FileName)
                        ? System.Windows.Media.Brushes.Purple
                        : System.Windows.Media.Brushes.Black;
            }

            MapComboBox.Items.Add(map);
        }

        if (!restoreMapSelectionCleared)
        {
            if (!isQuake2)
            {
                int startIndex =
                    maps.FindIndex(
                        map => string.Equals(
                            map.FileName,
                            "start.bsp",
                            StringComparison.OrdinalIgnoreCase));

                if (startIndex >= 0)
                {
                    // INDEX 0 is "None" so real maps start at INDEX 1.
                    MapComboBox.SelectedIndex =
                        startIndex + 1;
                }
                else if (MapComboBox.Items.Count > 1)
                {
                    MapComboBox.SelectedIndex = 1;
                }
                else
                {
                    MapComboBox.SelectedIndex = 0;
                }
            }
            else
            {
                string? defaultMap =
                    GetQuake2GogDefaultMap(
                        missionPack,
                        engine);

                int defaultIndex = -1;

                if (!string.IsNullOrWhiteSpace(defaultMap))
                {
                    int mapIndex =
                        maps.FindIndex(
                            map => string.Equals(
                                Path.GetFileNameWithoutExtension(
                                    map.FileName),
                                Path.GetFileNameWithoutExtension(
                                    defaultMap),
                                StringComparison.OrdinalIgnoreCase));

                    if (mapIndex >= 0)
                    {
                        defaultIndex = mapIndex + 1;
                    }
                }

                if (defaultIndex > 0)
                {
                    MapComboBox.SelectedIndex =
                        defaultIndex;
                }
                else if (MapComboBox.Items.Count > 1)
                {
                    MapComboBox.SelectedIndex = 1;
                }
                else
                {
                    MapComboBox.SelectedIndex = 0;
                }
            }
        }

        UpdateMapToolTip();
        UpdateCommandArguments();
    }

    private string? GetQuake2GogDefaultMap(
        MissionPack missionPack,
        Engine? engine)
    {
        if (engine?.Game != QuakeGame.Quake2)
        {
            return null;
        }

        // Q2Pro-NG starts on Outer Base.
        if (string.Equals(
                engine.Name,
                "Q2Pro-NG",
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                missionPack.Name,
                "Quake II",
                StringComparison.OrdinalIgnoreCase))
        {
            return "base1.bsp";
        }

        if (!string.Equals(
                engine.Name,
                "Quake II GOG",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return missionPack.Name switch
        {
            "Quake II" => "base1.bsp",
            "The Reckoning" => "badlands.bsp",
            "Ground Zero" => "rammo1.bsp",
            "Quake II 64" => "outpost.bsp",
            "Call of the Machine" => "mguhub.bsp",
            "Call of the Void" => "voidhub.bsp",
            _ => null
        };
    }

    private void DetectDemos()
    {
        demoSelectionActive = false;
        MapComboBox.IsEnabled = true;
        DifficultyComboBox.IsEnabled = true;

        DemoComboBox.Items.Clear();
        DemoComboBox.SelectedIndex = -1;

        MissionPack? missionPack =
            MissionComboBox.SelectedItem as MissionPack;

        Engine? engine =
            EngineComboBox.SelectedItem as Engine;

        // When no engine is detected, keep the Demo selector in the same
        // neutral state used by DetectEngines: enabled, normal text and
        // no forced "None" selection.
        if (engine == null)
        {
            DemoComboBox.IsEnabled = true;
            ClearDemoButton.IsEnabled = false;
            DemoLabel.IsEnabled = true;

            DemoComboBox.Foreground =
                System.Windows.SystemColors.ControlTextBrush;

            DemoLabel.Foreground =
                System.Windows.SystemColors.ControlTextBrush;

            demoSelectionActive = false;
            UpdateCommandArguments();
            return;
        }

        DemoComboBox.Items.Add(
            new Demo
            {
                Name = "None",
                FileName = "",
                GameDirectory = ""
            });

        if (missionPack == null)
        {
            DemoComboBox.SelectedIndex = 0;
            UpdateDemoControlsState();
            return;
        }

        string gameFolder =
            GetEpisodeFolder(missionPack);

        if (string.IsNullOrWhiteSpace(gameFolder))
        {
            DemoComboBox.SelectedIndex = 0;
            UpdateDemoControlsState();
            return;
        }

        if (!Directory.Exists(gameFolder))
        {
            if (string.IsNullOrWhiteSpace(missionPack.GameDirectory))
            {
                gameFolder = QuakeFolderTextBox.Text.Trim();
            }
            else
            {
                DemoComboBox.SelectedIndex = 0;
                UpdateDemoControlsState();
                return;
            }
        }

        List<Demo> demos =
            engine?.Game == QuakeGame.Quake2
                ? demoDetector2.DetectDemos(gameFolder)
                : demoDetector.DetectDemos(gameFolder);

        foreach (Demo demo in demos)
        {
            DemoComboBox.Items.Add(demo);
        }

        DemoComboBox.SelectedIndex = 0;
        UpdateDemoControlsState();
        UpdateDemoToolTip();
        UpdateCommandArguments();
    }

    private void UpdateDemoControlsState()
    {
        bool hasDemos =
            DemoComboBox.Items
                .OfType<Demo>()
                .Any(demo =>
                    !string.IsNullOrWhiteSpace(
                        demo.FileName));

        DemoComboBox.IsEnabled =
            hasDemos;
        ClearDemoButton.IsEnabled =
            hasDemos && demoSelectionActive;

        DemoLabel.IsEnabled =
            hasDemos;

        // Keep the entire Demo row visually consistent with the
        // unavailable Map/Difficulty controls.
        DemoComboBox.Foreground =
            hasDemos
                ? System.Windows.SystemColors.ControlTextBrush
                : System.Windows.Media.Brushes.DarkGray;

        DemoLabel.Foreground =
            hasDemos
                ? System.Windows.SystemColors.ControlTextBrush
                : System.Windows.Media.Brushes.DarkGray;
    }

    private static bool MapIsInsidePk3OrZip(
        string fileName,
        string gameFolder)
    {
        if (string.IsNullOrWhiteSpace(fileName) ||
            string.IsNullOrWhiteSpace(gameFolder) ||
            !Directory.Exists(gameFolder))
        {
            return false;
        }

        string targetMap =
            Path.GetFileName(fileName);

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

                        if (!entryPath.StartsWith(
                            "maps/",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (string.Equals(
                            Path.GetFileName(entryPath),
                            targetMap,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
                catch
                {
                    // Ignore invalid or inaccessible archives.
                }
            }
        }
        catch
        {
            // Ignore inaccessible folders.
        }

        return false;
    }

    private static bool IsMultiplayerMap(
        string fileName)
    {
        string mapName =
            Path.GetFileNameWithoutExtension(fileName);

        return mapName.Contains(
                   "dm",
                   StringComparison.OrdinalIgnoreCase)
               || mapName.Contains(
                   "base32",
                   StringComparison.OrdinalIgnoreCase)
               || mapName.Contains(
                   "death32",
                   StringComparison.OrdinalIgnoreCase)
               || mapName.Contains(
                   "ctf",
                   StringComparison.OrdinalIgnoreCase)
               || mapName.Contains(
                   "horde",
                   StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshEnginesButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string quakeFolder =
            QuakeFolderTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(quakeFolder) ||
            !Directory.Exists(quakeFolder))
        {
            StatusText.Text =
                "Select a valid Quake game folder first.";
            return;
        }

        Engine? currentEngine =
            EngineComboBox.SelectedItem as Engine;

        string currentEnginePath =
            currentEngine?.ExecutablePath ?? "";

        QuakeGame currentEngineGame =
            currentEngine?.Game ?? QuakeGame.Quake1;

        DetectEngines(quakeFolder);

        Engine? refreshedEngine =
            EngineComboBox.Items
                .OfType<Engine>()
                .FirstOrDefault(
                    engine =>
                        string.Equals(
                            engine.ExecutablePath,
                            currentEnginePath,
                            StringComparison.OrdinalIgnoreCase) &&
                        engine.Game == currentEngineGame);

        if (refreshedEngine != null)
        {
            EngineComboBox.SelectedItem =
                refreshedEngine;

            StatusText.Text =
                $"Found {EngineComboBox.Items.Count} engine(s).";
        }
        else if (EngineComboBox.Items.Count > 0)
        {
            StatusText.Text =
                $"Found {EngineComboBox.Items.Count} engine(s).";
        }
        else
        {
            StatusText.Text =
                "No supported Quake engine was found.";
        }
    }

    private void RefreshEpisodesButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string quakeFolder =
            QuakeFolderTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(quakeFolder) ||
            !Directory.Exists(quakeFolder))
        {
            StatusText.Text =
                "Select a valid Quake game folder first.";
            return;
        }

        MissionPack? currentMissionPack =
            MissionComboBox.SelectedItem as MissionPack;

        string currentMissionDirectory =
            currentMissionPack?.GameDirectory ??
            currentMissionPack?.DetectedDirectory ?? "";

        DetectMissionPacks(quakeFolder);

        MissionPack? refreshedMissionPack =
            MissionComboBox.Items
                .OfType<MissionPack>()
                .FirstOrDefault(
                    missionPack =>
                        string.Equals(
                            missionPack.GameDirectory,
                            currentMissionDirectory,
                            StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(
                            missionPack.DetectedDirectory,
                            currentMissionDirectory,
                            StringComparison.OrdinalIgnoreCase));

        if (refreshedMissionPack != null)
        {
            MissionComboBox.SelectedItem =
                refreshedMissionPack;

            StatusText.Text =
                $"Found {MissionComboBox.Items.Count} episode(s).";
        }
        else if (MissionComboBox.Items.Count > 0)
        {
            StatusText.Text =
                $"Found {MissionComboBox.Items.Count} episode(s).";
        }
        else
        {
            StatusText.Text =
                "No Quake game directories found.";
        }
    }

    private void EngineComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (EngineComboBox.SelectedItem is not Engine)
        {
            return;
        }

        if (!restoringSavedSelections)
        {
            DetectMissionPacks(
                QuakeFolderTextBox.Text.Trim());
        }
        else
        {
            UpdateCommandArguments();
        }
    }

    private void MissionComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        DetectMaps();
        DetectDemos();

        if (!restoreMapSelectionCleared &&
            !restoreDifficultySelectionCleared &&
            DifficultyComboBox.Items.Count > 2)
        {
            DifficultyComboBox.SelectedIndex = 2;
        }
    }

    private void MapComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (MapComboBox.SelectedItem is MapInfo)
        {
            SaveCurrentSettings();
        }

        UpdateMapToolTip();

        ClearMapButton.IsEnabled =
            !demoSelectionActive &&
            MapComboBox.SelectedIndex > 0;

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
        DifficultyComboBox.Items.Add(Difficulty.None);

        DifficultyComboBox.Items.Add(
            new Difficulty
            {
                Name = "Easy",
                Value = 0,
                Foreground = HexBrush("#3C3CE8")
            });

        DifficultyComboBox.Items.Add(
            new Difficulty
            {
                Name = "Normal",
                Value = 1,
                Foreground = HexBrush("#006400")
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
        ClearDifficultyButton.IsEnabled = false;
    }

    private void DifficultyComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        ClearDifficultyButton.IsEnabled =
            !demoSelectionActive &&
            DifficultyComboBox.SelectedIndex > 0;

        UpdateCommandArguments();
    }

    private void DemoComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        demoSelectionActive =
            DemoComboBox.SelectedItem is Demo selectedDemo &&
            !string.IsNullOrWhiteSpace(selectedDemo.FileName);

        UpdateDemoControlsState();

        MapComboBox.IsEnabled =
            !demoSelectionActive;

        DifficultyComboBox.IsEnabled =
            !demoSelectionActive;

        ClearMapButton.IsEnabled =
            !demoSelectionActive &&
            MapComboBox.SelectedIndex > 0;

        ClearDifficultyButton.IsEnabled =
            !demoSelectionActive &&
            DifficultyComboBox.SelectedIndex > 0;

        MapLabel.Foreground =
            demoSelectionActive
                ? System.Windows.Media.Brushes.DarkGray
                : System.Windows.SystemColors.ControlTextBrush;

        DifficultyLabel.Foreground =
            demoSelectionActive
                ? System.Windows.Media.Brushes.DarkGray
                : System.Windows.SystemColors.ControlTextBrush;

        if (!restoringSavedSelections &&
            DemoComboBox.SelectedItem is Demo)
        {
            SaveCurrentSettings();
        }

        UpdateDemoToolTip();
        UpdateCommandArguments();
    }

    private void CloseAfterLaunchCheckBox_Click(
        object sender,
        RoutedEventArgs e)
    {
        SaveCurrentSettings();
    }

    private void ExtraArgumentsTextBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        ClearExtraArgumentsButton.IsEnabled =
            !string.IsNullOrWhiteSpace(
                ExtraArgumentsTextBox.Text);

        UpdateCommandArguments();
    }

    private void ClearExtraArgumentsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ExtraArgumentsTextBox.Clear();

        StatusText.Text =
            "Cleared extra arguments.";
    }

    private void ClearMapButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        MapComboBox.SelectedIndex = 0;

        MapComboBox.ToolTip = null;

        DifficultyComboBox.SelectedIndex = 0;

        // Both selections are now cleared so their Clear buttons must
        // remain unavailable until the user selects them again.
        ClearMapButton.IsEnabled = false;
        ClearDifficultyButton.IsEnabled = false;

        StatusText.Text =
            "Cleared map and difficulty selection.";

        SaveCurrentSettings();

        UpdateCommandArguments();
    }

    private void ClearDifficultyButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DifficultyComboBox.SelectedIndex = 0;

        ClearDifficultyButton.IsEnabled = false;

        StatusText.Text =
            "Cleared difficulty selection.";

        SaveCurrentSettings();

        UpdateCommandArguments();
    }

    private void ClearDemoButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DemoComboBox.SelectedIndex = 0;

        demoSelectionActive = false;

        MapComboBox.IsEnabled = true;
        DifficultyComboBox.IsEnabled = true;

        // Re-evaluate the Clear buttons from the actual selections.
        ClearMapButton.IsEnabled =
            MapComboBox.SelectedIndex > 0;

        ClearDifficultyButton.IsEnabled =
            DifficultyComboBox.SelectedIndex > 0;

        UpdateDemoControlsState();

        MapLabel.Foreground =
            System.Windows.SystemColors.ControlTextBrush;

        DifficultyLabel.Foreground =
            System.Windows.SystemColors.ControlTextBrush;

        StatusText.Text =
            "Cleared demo selection.";

        SaveCurrentSettings();

        UpdateCommandArguments();
    }

    private void CreateDesktopShortcutButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Engine? engine =
            EngineComboBox.SelectedItem as Engine;

        MissionPack? missionPack =
            MissionComboBox.SelectedItem as MissionPack;

        MapInfo? selectedMap =
            MapComboBox.SelectedItem as MapInfo;

        if (engine == null)
        {
            System.Windows.MessageBox.Show(
                "Please select an engine first.",
                "Tiny Quake Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        if (missionPack == null)
        {
            System.Windows.MessageBox.Show(
                "Please select an episode first.",
                "Tiny Quake Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        if (!File.Exists(engine.ExecutablePath))
        {
            System.Windows.MessageBox.Show(
                "The selected Quake engine could not be found.\n\n" +
                $"Executable:\n{engine.ExecutablePath}",
                "Tiny Quake Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return;
        }

        try
        {
            List<string> arguments =
                BuildLaunchArguments();

            string shortcutArguments =
                string.Join(
                    " ",
                    arguments.Select(QuoteShortcutArgument));

            string desktopPath =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.DesktopDirectory);

            string shortcutName =
                $"Launch {engine.Name} - {missionPack.Name}.lnk";

            shortcutName =
                SanitizeFileName(shortcutName);

            string shortcutPath =
                Path.Combine(
                    desktopPath,
                    shortcutName);

            Type? shellType =
                Type.GetTypeFromProgID("WScript.Shell");

            if (shellType == null)
            {
                throw new InvalidOperationException(
                    "Windows Script Host is not available.");
            }

            dynamic shell =
                Activator.CreateInstance(shellType)!;

            dynamic shortcut =
                shell.CreateShortcut(shortcutPath);

            shortcut.TargetPath =
                engine.ExecutablePath;

            shortcut.WorkingDirectory =
                Path.GetDirectoryName(
                    engine.ExecutablePath) ??
                QuakeFolderTextBox.Text.Trim();

            shortcut.Arguments =
                shortcutArguments;

            shortcut.Description =
                "Tiny Quake Launcher command line shortcut";

            shortcut.IconLocation =
                engine.ExecutablePath + ",0";

            shortcut.Save();

            StatusText.Text =
                "Desktop shortcut created successfully.";

            System.Windows.MessageBox.Show(
                "Desktop shortcut created successfully.",
                "Information",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (System.Exception ex)
        {
            StatusText.Text =
                "Could not create desktop shortcut.";

            System.Windows.MessageBox.Show(
                "Tiny Quake Launcher could not create the desktop shortcut.\n\n" +
                $"Error:\n{ex.Message}",
                "Tiny Quake Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(
                invalidChar.ToString(),
                "");
        }

        return name.Trim();
    }

    private static string QuoteShortcutArgument(string argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            return "\"\"";
        }

        if (!argument.Any(char.IsWhiteSpace) &&
            !argument.Contains('"'))
        {
            return argument;
        }

        return "\"" +
               argument.Replace("\\", "\\\\")
                       .Replace("\"", "\\\"") +
               "\"";
    }

    private Demo? GetSelectedDemo()
    {
        if (!demoSelectionActive)
        {
            return null;
        }

        Demo? demo =
            DemoComboBox.SelectedItem as Demo;

        if (demo == null ||
            string.IsNullOrWhiteSpace(demo.FileName))
        {
            return null;
        }

        return demo;
    }

    private string GetDemoGameFolder(
        MissionPack? missionPack)
    {
        if (missionPack != null)
        {
            string folder =
                GetEpisodeFolder(missionPack);

            if (!string.IsNullOrWhiteSpace(folder) &&
                Directory.Exists(folder))
            {
                return folder;
            }
        }

        return QuakeFolderTextBox.Text.Trim();
    }

    private void PrepareDemoForLaunch(
        Demo demo,
        string gameFolder)
    {
        if (demo.ResourceType ==
            DemoResourceType.Folder)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(demo.ResourcePath))
        {
            throw new InvalidOperationException(
                "The selected demo has no source archive path.");
        }

        if (!File.Exists(demo.ResourcePath))
        {
            throw new FileNotFoundException(
                "The archive containing the selected demo could not be found.",
                demo.ResourcePath);
        }

        string demosFolder =
            Path.Combine(gameFolder, "demos");

        Directory.CreateDirectory(demosFolder);

        string destination =
            Path.Combine(demosFolder, demo.FileName);

        if (demo.ResourceType == DemoResourceType.Pk3)
        {
            ExtractDemoFromZip(
                demo.ResourcePath,
                demo.FileName,
                destination);
        }
        else if (demo.ResourceType == DemoResourceType.Pak)
        {
            ExtractDemoFromPak(
                demo.ResourcePath,
                demo.FileName,
                destination);
        }
        else
        {
            throw new InvalidOperationException(
                "Unknown demo resource type.");
        }
    }

    private static void ExtractDemoFromZip(
        string archivePath,
        string fileName,
        string destination)
    {
        using ZipArchive archive =
            ZipFile.OpenRead(archivePath);

        ZipArchiveEntry? entry =
            archive.Entries.FirstOrDefault(
                item =>
                    string.Equals(
                        Path.GetFileName(item.FullName),
                        fileName,
                        StringComparison.OrdinalIgnoreCase));

        if (entry == null)
        {
            throw new FileNotFoundException(
                "The selected demo could not be found inside the PK3/ZIP archive.",
                fileName);
        }

        using Stream input = entry.Open();
        using FileStream output = File.Create(destination);
        input.CopyTo(output);
    }

    private static void ExtractDemoFromPak(
        string pakPath,
        string fileName,
        string destination)
    {
        using FileStream stream = File.OpenRead(pakPath);
        using BinaryReader reader = new(stream);

        if (stream.Length < 12)
        {
            throw new InvalidDataException(
                "The PAK file is too small.");
        }

        string magic =
            System.Text.Encoding.ASCII.GetString(
                reader.ReadBytes(4));

        if (!string.Equals(
            magic,
            "PACK",
            StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The selected archive is not a valid Quake PAK file.");
        }

        int directoryOffset = reader.ReadInt32();
        int directoryLength = reader.ReadInt32();

        if (directoryOffset < 0 ||
            directoryLength < 0 ||
            directoryLength % 64 != 0 ||
            directoryOffset > stream.Length ||
            directoryLength > stream.Length - directoryOffset)
        {
            throw new InvalidDataException(
                "The PAK directory is invalid.");
        }

        stream.Position = directoryOffset;

        int entryCount = directoryLength / 64;

        for (int i = 0; i < entryCount; i++)
        {
            byte[] nameBytes = reader.ReadBytes(56);

            if (nameBytes.Length != 56)
            {
                break;
            }

            string entryName = DecodePakCString(nameBytes);
            int entryOffset = reader.ReadInt32();
            int entryLength = reader.ReadInt32();

            if (!string.Equals(
                Path.GetFileName(entryName),
                fileName,
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (entryOffset < 0 ||
                entryLength <= 0 ||
                entryOffset > stream.Length ||
                entryLength > stream.Length - entryOffset)
            {
                throw new InvalidDataException(
                    "The selected demo entry is invalid.");
            }

            stream.Position = entryOffset;
            byte[] data = reader.ReadBytes(entryLength);

            if (data.Length != entryLength)
            {
                throw new EndOfStreamException(
                    "The selected demo could not be read completely from the PAK.");
            }

            File.WriteAllBytes(destination, data);
            return;
        }

        throw new FileNotFoundException(
            "The selected demo could not be found inside the PAK archive.",
            fileName);
    }

    private static string DecodePakCString(byte[] bytes)
    {
        int length = 0;

        while (length < bytes.Length && bytes[length] != 0)
        {
            length++;
        }

        return System.Text.Encoding.ASCII.GetString(
            bytes,
            0,
            length);
    }

    private List<string> BuildLaunchArguments()
    {
        Engine? engine =
            EngineComboBox.SelectedItem as Engine;

        if (engine == null)
        {
            return new List<string>();
        }

        // If the user edited the command line directly, use the edited
        // arguments instead of rebuilding them from the controls.
        if (commandArgumentsEdited)
        {
            List<string> editedCommand =
                ParseExtraArguments(
                    new TextRange(
                        CommandArgumentsTextBox.Document.ContentStart,
                        CommandArgumentsTextBox.Document.ContentEnd).Text);

            // The first token in the preview is the engine executable
            // name, not an argument passed to the engine.
            if (editedCommand.Count > 0)
            {
                editedCommand.RemoveAt(0);
            }

            return editedCommand;
        }

        List<string> arguments;

        if (engine.Game == QuakeGame.Quake2)
        {
            arguments = BuildQuake2AutomaticLaunchArguments();
        }
        else
        {
            arguments = BuildQuake1AutomaticLaunchArguments();
        }

        // Extra arguments are added here ONLY for the actual launch command.
        // They are deliberately not part of the automatic argument builders,
        // so the command preview can display them separately in blue.
        arguments.AddRange(
            ParseExtraArguments(
                ExtraArgumentsTextBox.Text));

        return arguments;
    }


    private List<string> BuildQuake1AutomaticLaunchArguments()
    {
        MissionPack? missionPack =
            MissionComboBox.SelectedItem as MissionPack;

        Demo? selectedDemo =
            GetSelectedDemo();

        Engine? engine =
            EngineComboBox.SelectedItem as Engine;

        List<string> arguments = new();

        if (missionPack != null &&
            !string.IsNullOrWhiteSpace(
                missionPack.GameDirectory))
        {
            string gameDirectory =
                missionPack.GameDirectory.Trim();

            // Chocolate Quake uses the classic "-<folder>" syntax.
            if (IsChocolateQuakeEngine(engine))
            {
                arguments.Add(
                    "-" + gameDirectory);
            }
            else
            {
                arguments.Add("-game");
                arguments.Add(gameDirectory);
            }
        }

        if (selectedDemo != null)
        {
            arguments.Add("+playdemo");
            arguments.Add(selectedDemo.FileName);
            return arguments;
        }

        MapInfo? selectedMap =
            MapComboBox.SelectedItem as MapInfo;

        string? mapName = null;

        if (selectedMap != null)
        {
            mapName =
                Path.GetFileNameWithoutExtension(
                    selectedMap.FileName);
        }

        // Quake difficulty
        if (DifficultyComboBox.SelectedItem is Difficulty difficulty &&
            difficulty.Value >= 0)
        {
            arguments.Add("+skill");
            arguments.Add(difficulty.Value.ToString());
        }

        if (!string.IsNullOrWhiteSpace(mapName))
        {
            arguments.Add("+map");
            arguments.Add(mapName);
        }

        return arguments;
    }

    private static bool IsChocolateQuakeEngine(
        Engine? engine)
    {
        if (engine == null)
        {
            return false;
        }

        return string.Equals(
                   engine.Name,
                   "Chocolate Quake",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   engine.Name,
                   "Chocolate-Quake",
                   StringComparison.OrdinalIgnoreCase) ||
               Path.GetFileNameWithoutExtension(
                       engine.ExecutablePath)
                   .Replace("-", "", StringComparison.Ordinal)
                   .Replace("_", "", StringComparison.Ordinal)
                   .Equals(
                       "chocolatequake",
                       StringComparison.OrdinalIgnoreCase);
    }

    private List<string> BuildQuake2AutomaticLaunchArguments()
    {
        MissionPack? missionPack =
            MissionComboBox.SelectedItem as MissionPack;

        Demo? selectedDemo =
            GetSelectedDemo();

        List<string> arguments = new();

        // The engine runs with its own folder as the working directory.

        // Quake 2 uses +set game for mission packs/mods.
        // baseq2 is the default game directory.
        if (missionPack != null &&
            !string.IsNullOrWhiteSpace(
                missionPack.GameDirectory) &&
            !string.Equals(
                missionPack.GameDirectory,
                "baseq2",
                StringComparison.OrdinalIgnoreCase))
        {
            arguments.Add("+set");
            arguments.Add("game");
            arguments.Add(missionPack.GameDirectory.Trim());
        }

        // Quake 2 uses +map for demos.
        if (selectedDemo != null)
        {
            arguments.Add("+map");
            arguments.Add(selectedDemo.FileName);
            return arguments;
        }

        MapInfo? selectedMap =
            MapComboBox.SelectedItem as MapInfo;

        string? mapName = null;

        if (selectedMap != null)
        {
            mapName =
                Path.GetFileNameWithoutExtension(
                    selectedMap.FileName);
        }

        // Quake 2 difficulty
        if (DifficultyComboBox.SelectedItem is Difficulty difficulty &&
            difficulty.Value >= 0)
        {
            arguments.Add("+set");
            arguments.Add("skill");
            arguments.Add(difficulty.Value.ToString());
        }

        if (!string.IsNullOrWhiteSpace(mapName))
        {
            arguments.Add("+map");
            arguments.Add(mapName);
        }

        return arguments;
    }

    private void UpdateCommandArguments()
    {
        updatingCommandArguments = true;
        commandArgumentsEdited = false;

        CommandArgumentsTextBox.Document.Blocks.Clear();

        Engine? engine =
            EngineComboBox.SelectedItem as Engine;

        // Manual arguments must remain visible when no engine is detected.
        // Build automatic arguments only when an engine is available.
        List<string> automaticArguments =
            engine == null
                ? new List<string>()
                : engine.Game == QuakeGame.Quake2
                    ? BuildQuake2AutomaticLaunchArguments()
                    : BuildQuake1AutomaticLaunchArguments();

        List<string> extraArguments =
            ParseExtraArguments(
                ExtraArgumentsTextBox.Text);

        Paragraph paragraph = new();

        // Engine executable name is black when an engine is available.
        if (engine != null)
        {
            paragraph.Inlines.Add(
                new Run(
                    Path.GetFileName(engine.ExecutablePath))
                {
                    Foreground =
                        System.Windows.Media.Brushes.Black
                });
        }

        // Automatically generated arguments are black.
        foreach (string argument in automaticArguments)
        {
            paragraph.Inlines.Add(
                new Run(" ")
                {
                    Foreground =
                        System.Windows.Media.Brushes.Black
                });

            paragraph.Inlines.Add(
                new Run(argument)
                {
                    Foreground =
                        System.Windows.Media.Brushes.Black
                });
        }

        // ONLY manually entered extra arguments are blue.
        bool hasExistingArguments =
            automaticArguments.Count > 0;

        foreach (string argument in extraArguments)
        {
            if (hasExistingArguments)
            {
                paragraph.Inlines.Add(
                    new Run(" ")
                    {
                        Foreground =
                            System.Windows.Media.Brushes.Blue
                    });
            }

            paragraph.Inlines.Add(
                new Run(argument)
                {
                    Foreground =
                        System.Windows.Media.Brushes.Blue
                });

            hasExistingArguments = true;
        }

        CommandArgumentsTextBox.Document.Blocks.Add(
            paragraph);

        updatingCommandArguments = false;
        commandArgumentsEdited = false;
    }

    private void CommandArgumentsTextBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (!updatingCommandArguments)
        {
            commandArgumentsEdited = true;
        }
    }

    private void LaunchButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        LaunchQuake();
    }

    private static List<string> ParseExtraArguments(
        string text)
    {
        List<string> result = new();

        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        bool inQuotes = false;
        bool escaped = false;
        string current = "";

        foreach (char character in text)
        {
            if (escaped)
            {
                current += character;
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
                current += character;
                continue;
            }

            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    result.Add(current);
                    current = "";
                }

                continue;
            }

            current += character;
        }

        if (current.Length > 0)
        {
            result.Add(current);
        }

        return result;
    }

    private MessageBoxResult ShowMapClearedWarning(
        out bool dontShowAgain)
    {
        System.Windows.Controls.CheckBox checkBox =
            new System.Windows.Controls.CheckBox
            {
                Content = "Don't show this message again",
                Margin = new Thickness(0, 12, 0, 0)
            };

        System.Windows.Controls.TextBlock message =
            new System.Windows.Controls.TextBlock
            {
                Text =
                    "Map selection was cleared and game will start with\n" +
                    "default settings. Do you want to continue?",
                TextWrapping = TextWrapping.Wrap
            };

        System.Windows.Controls.Button yesButton =
            new System.Windows.Controls.Button
            {
                Content = "Yes",
                Width = 75,
                IsDefault = true,
                Margin = new Thickness(0, 0, 8, 0)
            };

        System.Windows.Controls.Button noButton =
            new System.Windows.Controls.Button
            {
                Content = "No",
                Width = 75,
                IsCancel = true
            };

        System.Windows.Controls.StackPanel buttons =
            new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment =
                    System.Windows.HorizontalAlignment.Right,
                Margin = new Thickness(0, 18, 0, 0)
            };

        buttons.Children.Add(yesButton);
        buttons.Children.Add(noButton);

        System.Windows.Controls.StackPanel content =
            new System.Windows.Controls.StackPanel
            {
                Margin = new Thickness(20)
            };

        content.Children.Add(message);
        content.Children.Add(checkBox);
        content.Children.Add(buttons);

        Window dialog =
            new Window
            {
                Title = "Warning",
                Content = content,
                Owner = this,
                WindowStartupLocation =
                    WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                SizeToContent = SizeToContent.WidthAndHeight,
                ShowInTaskbar = false
            };

        MessageBoxResult result =
            MessageBoxResult.No;

        yesButton.Click +=
            (_, _) =>
            {
                result = MessageBoxResult.Yes;
                dialog.DialogResult = true;
            };

        noButton.Click +=
            (_, _) =>
            {
                result = MessageBoxResult.No;
                dialog.DialogResult = false;
            };

        dialog.ShowDialog();

        dontShowAgain =
            checkBox.IsChecked == true;

        return result;
    }

    private void LaunchQuake()
    {
        Engine? engine =
            EngineComboBox.SelectedItem as Engine;

        MissionPack? missionPack =
            MissionComboBox.SelectedItem as MissionPack;

        MapInfo? selectedMap =
            MapComboBox.SelectedItem as MapInfo;

        Demo? selectedDemo =
            GetSelectedDemo();

        // An engine is required before any map-related launch warning.
        if (engine == null)
        {
            StatusText.Text =
                "Game cannot run with no supported engine.";

            System.Windows.MessageBox.Show(
                "Game cannot run with no supported engine.",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return;
        }

        if (selectedDemo == null &&
            (selectedMap == null ||
             string.IsNullOrWhiteSpace(selectedMap.FileName)))
        {
            LauncherSettings settings =
                LoadSettings();

            if (!settings.DontShowMapClearedWarning)
            {
                bool dontShowAgain;

                MessageBoxResult result =
                    ShowMapClearedWarning(
                        out dontShowAgain);

                if (dontShowAgain)
                {
                    settings.DontShowMapClearedWarning = true;
                    SaveSettings(settings);
                }

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }
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

        try
        {
            if (selectedDemo != null)
            {
                string demoGameFolder =
                    GetDemoGameFolder(missionPack);

                if (string.IsNullOrWhiteSpace(demoGameFolder) ||
                    !Directory.Exists(demoGameFolder))
                {
                    throw new DirectoryNotFoundException(
                        "The game directory for the selected demo could not be found.");
                }

                PrepareDemoForLaunch(
                    selectedDemo,
                    demoGameFolder);
            }

            List<string> arguments =
                BuildLaunchArguments();

            // The engine's working directory is always the folder containing
            // its executable. This keeps engine-created files (history.txt,
            // configs, logs, etc.) beside the engine, even when the engine is
            // installed separately from the selected Quake folder.
            string engineDirectory =
                Path.GetDirectoryName(
                    engine.ExecutablePath) ??
                quakeFolder;

            ProcessStartInfo startInfo =
                new ProcessStartInfo
                {
                    FileName = engine.ExecutablePath,
                    WorkingDirectory = engineDirectory,
                    UseShellExecute = true
                };

            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            Process.Start(startInfo);

            StatusText.Text =
                $"{engine.Name} is ready with custom settings.";

            if (CloseAfterLaunchCheckBox.IsChecked == true)
            {
                Close();
            }
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