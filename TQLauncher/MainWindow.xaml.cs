using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TinyQuakeLauncher.Models;
using TinyQuakeLauncher.Services;

namespace TinyQuakeLauncher;

public class LauncherSettings
{
    public string QuakeFolder { get; set; } = "";

    public string EnginePath { get; set; } = "";

    public string MissionPackDirectory { get; set; } = "";

    public string MapFileName { get; set; } = "";

    public bool MapSelectionCleared { get; set; }

    public int? Difficulty { get; set; }

    public bool DifficultySelectionCleared { get; set; }

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

    public MainWindow()
    {
        InitializeComponent();

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

            RestoreSavedSelections(settings);

            // The saved "map cleared" state only applies to
            // the initial startup restore. After startup,
            // selecting another episode should use the normal
            // default map selection again.
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

            MissionPack? missionPack =
                MissionComboBox.SelectedItem as MissionPack;

            settings.MissionPackDirectory =
                missionPack?.GameDirectory ?? "";

            MapInfo? selectedMap =
                MapComboBox.SelectedItem as MapInfo;

            settings.MapFileName =
                selectedMap?.FileName ?? "";

            settings.MapSelectionCleared =
                selectedMap == null;

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
                            StringComparison.OrdinalIgnoreCase));

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
            MapComboBox.SelectedIndex = -1;
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
            DifficultyComboBox.SelectedIndex = -1;
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
                DifficultyComboBox.SelectedIndex = 1;
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
            // provide valid episode/map selections either.
            MissionComboBox.Items.Clear();
            MissionComboBox.SelectedIndex = -1;

            MapComboBox.Items.Clear();
            MapComboBox.SelectedIndex = -1;
            MapComboBox.ToolTip = null;

            DifficultyComboBox.Items.Clear();
            DifficultyComboBox.SelectedIndex = -1;

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

        if (IsQuake2Engine(engine))
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
    }

    private static bool IsQuake2Engine(Engine? engine)
    {
        if (engine == null)
        {
            return false;
        }

        return engine.Name.Equals(
                   "Yamagi Quake II",
                   StringComparison.OrdinalIgnoreCase)
            || engine.Name.Equals(
                   "Q2Pro",
                   StringComparison.OrdinalIgnoreCase)
            || engine.Name.Equals(
                   "KMQuake II",
                   StringComparison.OrdinalIgnoreCase)
            || engine.Name.Equals(
                   "Quake II RTX",
                   StringComparison.OrdinalIgnoreCase);
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

        Engine? engine =
            EngineComboBox.SelectedItem as Engine;

        bool isQuake2 =
            IsQuake2Engine(engine);

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
                    MapComboBox.SelectedIndex =
                        startIndex;
                }
                else if (MapComboBox.Items.Count > 0)
                {
                    MapComboBox.SelectedIndex = 0;
                }
            }
            else if (MapComboBox.Items.Count > 0)
            {
                MapComboBox.SelectedIndex = 0;
            }
        }

        UpdateMapToolTip();
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
        UpdateCommandArguments();
    }

    private void MissionComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        DetectMaps();

        if (!restoreMapSelectionCleared &&
            !restoreDifficultySelectionCleared &&
            DifficultyComboBox.Items.Count > 1)
        {
            DifficultyComboBox.SelectedIndex = 1;
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
    }

    private void DifficultyComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        UpdateCommandArguments();
    }

    private void ExtraArgumentsTextBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
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
        MapComboBox.SelectedIndex = -1;

        MapComboBox.ToolTip = null;

        SaveCurrentSettings();

        UpdateCommandArguments();
    }

    private void ClearDifficultyButton_Click(
    object sender,
    RoutedEventArgs e)
    {
        DifficultyComboBox.SelectedIndex = -1;

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
                "Desktop shortcut created.";

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

    private List<string> BuildLaunchArguments()
    {
        Engine? engine =
            EngineComboBox.SelectedItem as Engine;

        if (IsQuake2Engine(engine))
        {
            return BuildQuake2LaunchArguments();
        }

        return BuildQuake1LaunchArguments();
    }

    private List<string> BuildQuake1LaunchArguments()
    {
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

        List<string> arguments = new();

        if (missionPack != null &&
            !string.IsNullOrWhiteSpace(
                missionPack.GameDirectory))
        {
            arguments.Add("-game");
            arguments.Add(missionPack.GameDirectory);
        }

        if (DifficultyComboBox.SelectedItem is Difficulty difficulty)
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

        MapInfo? selectedMap =
            MapComboBox.SelectedItem as MapInfo;

        string? mapName = null;

        if (selectedMap != null)
        {
            mapName =
                Path.GetFileNameWithoutExtension(
                    selectedMap.FileName);
        }

        List<string> arguments = new();

        // Quake II uses +set game for mission packs/mods.
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
            arguments.Add(missionPack.GameDirectory);
        }

        if (DifficultyComboBox.SelectedItem is Difficulty difficulty)
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

        // Manual extra arguments remain at the end.
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

        List<string> arguments =
            BuildLaunchArguments();

        try
        {
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