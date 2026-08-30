using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
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

    public MainWindow()
    {
        InitializeComponent();

        ClearExtraArgumentsButton.IsEnabled = false;

        Closing += MainWindow_Closing;

        QuakeFolderTextBox.TextChanged +=
            QuakeFolderTextBox_TextChanged;

        QuakeFolderTextBox.SizeChanged +=
            QuakeFolderTextBox_SizeChanged;

        MapComboBox.SizeChanged +=
            MapComboBox_SizeChanged;

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

            restoreMapSelectionCleared =
                settings.MapSelectionCleared;

            restoreDifficultySelectionCleared =
                settings.DifficultySelectionCleared;

            DetectQuakeInstallation(
                settings.QuakeFolder);

            restoringSavedSelections = true;
            RestoreSavedSelections(settings);
            restoringSavedSelections = false;

            CloseAfterLaunchCheckBox.IsChecked =
                settings.CloseAfterLaunch;

            // The saved "map cleared" state only applies to
            // the initial startup restore.
            restoreMapSelectionCleared = false;
            restoreDifficultySelectionCleared = false;
        }
        catch
        {
            // Ignore settings errors and let the user
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
            // Ignore settings errors.
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
            // Ignore settings errors.
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
            QuakeFolderTextBox.Text =
                dialog.SelectedPath;

            SaveQuakeFolder(
                dialog.SelectedPath);

            DetectQuakeInstallation(
                dialog.SelectedPath);

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

        // Leave room for the ComboBox border, padding,
        // and drop-down arrow.
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

            CommandArgumentsTextBox.Text = "";

            StatusText.Text =
                "No supported Quake engine was found.";

            System.Windows.MessageBox.Show(
                "No supported Quake engine was found.",
                "Tiny Quake Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        SetupDifficultyOptions();

        EngineComboBox.SelectedIndex = 0;
    }

    private void DetectMissionPacks(string quakeFolder)
    {
        MissionComboBox.Items.Clear();

        Engine? engine =
            EngineComboBox.SelectedItem as Engine;

        List<MissionPack> missionPacks;

        if (engine?.Game == QuakeGame.Quake2)
        {
            missionPacks =
                missionPackDetector2
                    .DetectMissionPacks(quakeFolder);
        }
        else
        {
            missionPacks =
                missionPackDetector
                    .DetectMissionPacks(quakeFolder);
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
                $"{MissionComboBox.Items.Count} game(s).";
        }
        else
        {
            StatusText.Text =
                "No Quake game directories found.";
        }

        DetectMaps();
        DetectDemos();
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
                gameFolder = QuakeFolderTextBox.Text.Trim();
            }
            else
            {
                StatusText.Text =
                    $"Episode folder not found:\n{gameFolder}";

                UpdateCommandArguments();
                return;
            }
        }

        Engine? engine =
            EngineComboBox.SelectedItem as Engine;

        bool isQuake2 =
            engine?.Game == QuakeGame.Quake2;

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
            map.Foreground =
                IsMultiplayerMap(map.FileName)
                    ? System.Windows.Media.Brushes.Purple
                    : System.Windows.Media.Brushes.Black;

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
                    // Index 0 is "None", so real maps start at index 1.
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
            else if (MapComboBox.Items.Count > 1)
            {
                MapComboBox.SelectedIndex = 1;
            }
            else
            {
                MapComboBox.SelectedIndex = 0;
            }
        }

        UpdateMapToolTip();
        UpdateCommandArguments();
    }

    private void DetectDemos()
    {
        demoSelectionActive = false;
        MapComboBox.IsEnabled = true;
        DifficultyComboBox.IsEnabled = true;

        DemoComboBox.Items.Clear();

        DemoComboBox.Items.Add(
            new Demo
            {
                Name = "None",
                FileName = "",
                GameDirectory = ""
            });

        MissionPack? missionPack =
            MissionComboBox.SelectedItem as MissionPack;

        Engine? engine =
            EngineComboBox.SelectedItem as Engine;

        if (missionPack == null || engine == null)
        {
            DemoComboBox.SelectedIndex = 0;
            return;
        }

        string gameFolder =
            GetEpisodeFolder(missionPack);

        if (string.IsNullOrWhiteSpace(gameFolder))
        {
            DemoComboBox.SelectedIndex = 0;
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
        UpdateCommandArguments();
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

        ClearDemoButton.IsEnabled =
            demoSelectionActive;

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
    }

    private void ClearMapButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        MapComboBox.SelectedIndex = 0;

        MapComboBox.ToolTip = null;

        DifficultyComboBox.SelectedIndex = 0;

        SaveCurrentSettings();

        UpdateCommandArguments();
    }

    private void ClearDifficultyButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DifficultyComboBox.SelectedIndex = 0;

        ClearDifficultyButton.IsEnabled = false;

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

        ClearMapButton.IsEnabled = true;
        ClearDifficultyButton.IsEnabled = true;

        MapLabel.Foreground =
            System.Windows.SystemColors.ControlTextBrush;

        DifficultyLabel.Foreground =
            System.Windows.SystemColors.ControlTextBrush;

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
                "Tiny Quake Launcher",
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

        if (engine?.Game == QuakeGame.Quake2)
        {
            return BuildQuake2LaunchArguments();
        }

        return BuildQuake1LaunchArguments();
    }

    private List<string> BuildQuake1LaunchArguments()
    {
        MissionPack? missionPack =
            MissionComboBox.SelectedItem as MissionPack;

        Demo? selectedDemo =
            GetSelectedDemo();

        List<string> arguments = new();

        if (missionPack != null &&
            !string.IsNullOrWhiteSpace(
                missionPack.GameDirectory))
        {
            arguments.Add("-game");
            arguments.Add(missionPack.GameDirectory.Trim());
        }

        if (selectedDemo != null)
        {
            arguments.Add("+playdemo");
            arguments.Add(selectedDemo.FileName);

            arguments.AddRange(
                ParseExtraArguments(
                    ExtraArgumentsTextBox.Text));

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

        //Quake difficulty
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

        arguments.AddRange(
            ParseExtraArguments(
                ExtraArgumentsTextBox.Text));

        return arguments;
    }

    private List<string> BuildQuake2LaunchArguments()
    {
        MissionPack? missionPack =
            MissionComboBox.SelectedItem as MissionPack;

        Demo? selectedDemo =
            GetSelectedDemo();

        List<string> arguments = new();

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

            arguments.AddRange(
                ParseExtraArguments(
                    ExtraArgumentsTextBox.Text));

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

        //Quake 2 difficulty
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

        arguments.AddRange(
            ParseExtraArguments(
                ExtraArgumentsTextBox.Text));

        return arguments;
    }

    private void UpdateCommandArguments()
    {
        Engine? engine =
            EngineComboBox.SelectedItem as Engine;

        if (engine == null)
        {
            CommandArgumentsTextBox.Text = "";
            return;
        }

        List<string> arguments =
            BuildLaunchArguments();

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

        if (selectedDemo == null &&
            (selectedMap == null ||
             string.IsNullOrWhiteSpace(selectedMap.FileName)))
        {
            MessageBoxResult result =
                System.Windows.MessageBox.Show(
                    "Map selection was cleared and game will start with default settings. Do you want to continue?",
                    "Warning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
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