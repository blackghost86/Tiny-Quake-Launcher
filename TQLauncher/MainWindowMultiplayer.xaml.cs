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

public class MultiplayerLauncherSettings
{
    public string QuakeFolder { get; set; } = "";

    public string Resolution { get; set; } = "";

    public string EnginePath { get; set; } = "";

    public QuakeGame EngineGame { get; set; } = QuakeGame.Quake1;

    public string MissionPackDirectory { get; set; } = "";

    public string MapFileName { get; set; } = "";

    public bool MapSelectionCleared { get; set; }

    public bool DontShowMapClearedWarning { get; set; }

    public bool DontShowMainQuakeFolderWarning { get; set; }

    public int? Mode { get; set; }

    public bool ModeSelectionCleared { get; set; }

    public bool FragLimitEnabled { get; set; }

    public bool FlagLimitEnabled { get; set; }

    public bool TimeLimitEnabled { get; set; }

    public bool MaxPlayersEnabled { get; set; }

    public bool CloseAfterLaunch { get; set; }

    public string ExtraArguments { get; set; } = "";
}

public partial class MainWindowMultiplayer : System.Windows.Controls.UserControl
{
    private readonly EngineDetector engineDetector = new();

    private readonly EngineDetector2 engineDetector2 = new();

    private readonly EngineDetector3 engineDetector3 = new();

    private readonly MissionPackDetector missionPackDetector = new();

    private readonly MissionPackDetector2 missionPackDetector2 = new();

    private readonly MissionPackDetector3 missionPackDetector3 = new();

    private readonly MapDetector MapDetector = new();

    private readonly MapDetector2 MapDetector2 = new();

    private readonly MapDetector3 MapDetector3 = new();

    private readonly QuakeHandler quakeHandler = new();

    private readonly Quake2Handler quake2Handler = new();
    private readonly Quake3Handler quake3Handler = new();

    private static readonly string SettingsFolder =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "TinyQuakeLauncher");

    private static readonly string SettingsFile =
        Path.Combine(
            SettingsFolder,
            "TQLauncherTab2.json");

    private bool restoreMapSelectionCleared;
    private bool restoreModeSelectionCleared;
    private bool restoringSavedSelections;
    private bool updatingCommandArguments;
    private bool commandArgumentsEdited;
    private string lastAcceptedQuakeFolder = "";
    private bool suppressNoEpisodeWarning;


    public MainWindowMultiplayer()
    {
        InitializeComponent();


        QuakeFolderTextBoxMultiplayer.Padding =
            new Thickness(3,
                QuakeFolderTextBoxMultiplayer.Padding.Top,
                QuakeFolderTextBoxMultiplayer.Padding.Right,
                QuakeFolderTextBoxMultiplayer.Padding.Bottom);

        CommandArgumentsTextBoxMultiplayer.ToolTip =
            "Command line arguments based on preferred settings";

        // The command line preview can also be edited directly. Manual
        // changes are used for the next launch/shortcut until another
        // launcher setting refreshes the generated command line.
        CommandArgumentsTextBoxMultiplayer.IsReadOnly = false;
        CommandArgumentsTextBoxMultiplayer.TextChanged +=
            CommandArgumentsTextBox_TextChanged;

        ClearExtraArgumentsButtonMultiplayer.IsEnabled = false;

        Unloaded += MainWindowMultiplayer_Unloaded;

        QuakeFolderTextBoxMultiplayer.TextChanged +=
            QuakeFolderTextBox_TextChanged;

        // Handle Enter directly in code so pressing Enter in the
        // game address bar always refreshes the entered path.
        QuakeFolderTextBoxMultiplayer.KeyDown +=
            QuakeFolderTextBox_KeyDown;

        RefreshEnginesButtonMultiplayer.Click +=
            RefreshEnginesButton_Click;

        RefreshEpisodesButtonMultiplayer.Click +=
            RefreshEpisodesButton_Click;

        QuakeFolderTextBoxMultiplayer.SizeChanged +=
            QuakeFolderTextBox_SizeChanged;

        MapComboBoxMultiplayer.SizeChanged +=
            MapComboBox_SizeChanged;

        MissionComboBoxMultiplayer.SizeChanged +=
            MissionComboBox_SizeChanged;

        restoringSavedSelections = true;
        SetupResolutions();
        restoringSavedSelections = false;

        ClearResolutionButtonMultiplayer.IsEnabled = false;
        RefreshEnginesButtonMultiplayer.IsEnabled = false;
        RefreshEpisodesButtonMultiplayer.IsEnabled = false;

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

            MultiplayerLauncherSettings? settings =
                JsonSerializer.Deserialize<MultiplayerLauncherSettings>(json);

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

            QuakeFolderTextBoxMultiplayer.Text =
                settings.QuakeFolder;

            lastAcceptedQuakeFolder =
                settings.QuakeFolder.Trim();

            restoreMapSelectionCleared =
                settings.MapSelectionCleared;

            restoreModeSelectionCleared =
                settings.ModeSelectionCleared;

            DetectQuakeInstallation(
                settings.QuakeFolder);

            restoringSavedSelections = true;
            RestoreSavedSelections(settings);
            restoringSavedSelections = false;

            if (EngineComboBoxMultiplayer.Items.Count == 0)
            {
                // Saved settings may have triggered selection-change handlers
                // while being restored. Force all Clear buttons back to the
                // unavailable state when no supported engine was found.
                ClearMapButtonMultiplayer.IsEnabled = false;
                ClearModeButtonMultiplayer.IsEnabled = false;
                ClearExtraArgumentsButtonMultiplayer.IsEnabled = false;
            }

            CloseAfterLaunchCheckBoxMultiplayer.IsChecked =
                settings.CloseAfterLaunch;

            // The saved "map cleared" state only applies to
            // the initial startup restore.
            restoreMapSelectionCleared = false;
            restoreModeSelectionCleared = false;
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

            MultiplayerLauncherSettings settings =
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

    private MultiplayerLauncherSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string json =
                    File.ReadAllText(SettingsFile);

                MultiplayerLauncherSettings? settings =
                    JsonSerializer.Deserialize<MultiplayerLauncherSettings>(json);

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

        return new MultiplayerLauncherSettings();
    }

    private void SaveSettings(
        MultiplayerLauncherSettings settings)
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
            MultiplayerLauncherSettings settings =
                LoadSettings();

            settings.QuakeFolder =
                QuakeFolderTextBoxMultiplayer.Text.Trim();

            Resolution? selectedResolution =
                ResolutionComboBoxMultiplayer.SelectedItem as Resolution;

            settings.Resolution =
                selectedResolution != null &&
                !selectedResolution.IsDefault
                    ? selectedResolution.DisplayName
                    : "";

            Engine? engine =
                EngineComboBoxMultiplayer.SelectedItem as Engine;

            settings.EnginePath =
                engine?.ExecutablePath ?? "";

            settings.EngineGame =
                engine?.Game ?? QuakeGame.Quake1;

            MissionPack? missionPack =
                MissionComboBoxMultiplayer.SelectedItem as MissionPack;

            settings.MissionPackDirectory =
                missionPack?.GameDirectory ?? "";

            MapInfo? selectedMap =
                MapComboBoxMultiplayer.SelectedItem as MapInfo;

            settings.MapFileName =
                selectedMap?.FileName ?? "";

            settings.MapSelectionCleared =
                selectedMap == null ||
                string.IsNullOrWhiteSpace(selectedMap.FileName);

            if (ModeComboBoxMultiplayer.SelectedItem
                is MultiplayerMode mode)
            {
                settings.Mode =
                    mode.Value;

                settings.ModeSelectionCleared = false;
            }
            else
            {
                settings.Mode = null;

                settings.ModeSelectionCleared = true;
            }

            settings.FragLimitEnabled =
                FragLimitCheckBoxMultiplayer.IsChecked == true;

            settings.FlagLimitEnabled =
                FlagLimitCheckBoxMultiplayer.IsChecked == true;

            settings.TimeLimitEnabled =
                TimeLimitCheckBoxMultiplayer.IsChecked == true;

            settings.MaxPlayersEnabled =
                MaxPlayersCheckBoxMultiplayer.IsChecked == true;

            settings.CloseAfterLaunch =
                CloseAfterLaunchCheckBoxMultiplayer.IsChecked == true;

            settings.ExtraArguments =
                ExtraArgumentsTextBoxMultiplayer.Text;

            SaveSettings(settings);
        }
        catch
        {
            // Ignore errors.
        }
    }

    private void RestoreSavedSelections(
        MultiplayerLauncherSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.Resolution))
        {
            Resolution? resolution =
                ResolutionComboBoxMultiplayer.Items
                    .OfType<Resolution>()
                    .FirstOrDefault(
                        item => string.Equals(
                            item.DisplayName,
                            settings.Resolution,
                            StringComparison.OrdinalIgnoreCase));

            if (resolution != null)
            {
                ResolutionComboBoxMultiplayer.SelectedItem =
                    resolution;
            }
        }

        if (!string.IsNullOrWhiteSpace(settings.EnginePath))
        {
            Engine? engine =
                EngineComboBoxMultiplayer.Items
                    .OfType<Engine>()
                    .FirstOrDefault(
                        item => string.Equals(
                            item.ExecutablePath,
                            settings.EnginePath,
                            StringComparison.OrdinalIgnoreCase) &&
                        item.Game == settings.EngineGame);

            if (engine != null)
            {
                EngineComboBoxMultiplayer.SelectedItem =
                    engine;

                // Rebuild resolutions for the saved engine even while the normal
                // selection-change handler is suppressed.
                SetupResolutions();

                // Rebuild the episode list for the saved engine before restoring
                // the saved episode. This is required when the initially
                // detected engine differs from the saved engine.
                DetectMissionPacks(
                    QuakeFolderTextBoxMultiplayer.Text.Trim());
            }
        }

        if (!string.IsNullOrWhiteSpace(
            settings.MissionPackDirectory))
        {
            MissionPack? missionPack =
                MissionComboBoxMultiplayer.Items
                    .OfType<MissionPack>()
                    .FirstOrDefault(
                        item => string.Equals(
                            item.GameDirectory,
                            settings.MissionPackDirectory,
                            StringComparison.OrdinalIgnoreCase));

            if (missionPack != null)
            {
                MissionComboBoxMultiplayer.SelectedItem =
                    missionPack;
            }
        }

        if (settings.MapSelectionCleared)
        {
            MapComboBoxMultiplayer.SelectedIndex = 0;
        }
        else if (!string.IsNullOrWhiteSpace(
            settings.MapFileName))
        {
            MapInfo? map =
                MapComboBoxMultiplayer.Items
                    .OfType<MapInfo>()
                    .FirstOrDefault(
                        item => string.Equals(
                            item.FileName,
                            settings.MapFileName,
                            StringComparison.OrdinalIgnoreCase));

            if (map != null)
            {
                MapComboBoxMultiplayer.SelectedItem =
                    map;
            }
        }

        if (settings.ModeSelectionCleared)
        {
            ModeComboBoxMultiplayer.SelectedIndex = 0;
        }
        else if (settings.Mode.HasValue)
        {
            MultiplayerMode? mode =
                ModeComboBoxMultiplayer.Items
                    .OfType<MultiplayerMode>()
                    .FirstOrDefault(
                        item => item.Value ==
                            settings.Mode.Value);

            if (mode != null)
            {
                ModeComboBoxMultiplayer.SelectedItem =
                    mode;
            }
        }

        FragLimitCheckBoxMultiplayer.IsChecked =
            settings.FragLimitEnabled;

        FlagLimitCheckBoxMultiplayer.IsChecked =
            settings.FlagLimitEnabled;

        TimeLimitCheckBoxMultiplayer.IsChecked =
            settings.TimeLimitEnabled;

        MaxPlayersCheckBoxMultiplayer.IsChecked =
            settings.MaxPlayersEnabled;

        ExtraArgumentsTextBoxMultiplayer.Text =
            settings.ExtraArguments ?? "";

        UpdateCommandArguments();
    }

    private void LimitCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdateCommandArguments();
        SaveCurrentSettings();
    }

    private void MainWindowMultiplayer_Unloaded(
        object? sender,
        System.Windows.RoutedEventArgs e)
    {
        SaveCurrentSettings();
    }

    private void Multiplayer_BrowseButton_Click(object sender, RoutedEventArgs e)
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
                QuakeFolderTextBoxMultiplayer.Text =
                    lastAcceptedQuakeFolder;

                return;
            }

            QuakeFolderTextBoxMultiplayer.Text =
                selectedFolder;

            lastAcceptedQuakeFolder =
                selectedFolder;

            SaveQuakeFolder(
                selectedFolder);

            DetectQuakeInstallation(
                selectedFolder);

            if (ModeComboBoxMultiplayer.Items.Count > 1)
            {
                ModeComboBoxMultiplayer.SelectedIndex = 0;
            }

            UpdateModeControlsState();
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
        string folder = QuakeFolderTextBoxMultiplayer.Text.Trim();

        if (string.IsNullOrWhiteSpace(folder))
        {
            StatusTextMultiplayer.Text = "Enter a Quake game folder.";
            return;
        }

        if (!Directory.Exists(folder))
        {
            StatusTextMultiplayer.Text =
                $"Game folder not found:\n{folder}";
            return;
        }

        if (!ConfirmQuakeFolderSelection(folder))
        {
            QuakeFolderTextBoxMultiplayer.Text =
                lastAcceptedQuakeFolder;

            return;
        }

        QuakeFolderTextBoxMultiplayer.Text = folder;
        lastAcceptedQuakeFolder = folder;
        SaveQuakeFolder(folder);
        DetectQuakeInstallation(folder);

        if (ModeComboBoxMultiplayer.Items.Count > 1)
        {
            ModeComboBoxMultiplayer.SelectedIndex = 0;
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
            QuakeFolderTextBoxMultiplayer.Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            QuakeFolderTextBoxMultiplayer.ToolTip = null;
            return;
        }

        FormattedText formattedText =
            new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface(
                    QuakeFolderTextBoxMultiplayer.FontFamily,
                    QuakeFolderTextBoxMultiplayer.FontStyle,
                    QuakeFolderTextBoxMultiplayer.FontWeight,
                    QuakeFolderTextBoxMultiplayer.FontStretch),
                QuakeFolderTextBoxMultiplayer.FontSize,
                System.Windows.Media.Brushes.Black,
                VisualTreeHelper.GetDpi(
                    QuakeFolderTextBoxMultiplayer).PixelsPerDip);

        double availableWidth =
            QuakeFolderTextBoxMultiplayer.ActualWidth -
            QuakeFolderTextBoxMultiplayer.Padding.Left -
            QuakeFolderTextBoxMultiplayer.Padding.Right -
            10;

        if (formattedText.Width > availableWidth)
        {
            QuakeFolderTextBoxMultiplayer.ToolTip = text;
        }
        else
        {
            QuakeFolderTextBoxMultiplayer.ToolTip = null;
        }
    }

    private void MapComboBox_SizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        UpdateMapToolTip();
    }

    private void MissionComboBox_SizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        UpdateMissionToolTip();
    }

    private void UpdateMissionToolTip()
    {
        MissionPack? selectedMissionPack =
            MissionComboBoxMultiplayer.SelectedItem as MissionPack;

        if (selectedMissionPack == null ||
            string.IsNullOrWhiteSpace(selectedMissionPack.Name) ||
            MissionComboBoxMultiplayer.ActualWidth <= 0)
        {
            MissionComboBoxMultiplayer.ToolTip = null;
            return;
        }

        string displayText =
            selectedMissionPack.Name;

        FormattedText formattedText =
            new FormattedText(
                displayText,
                System.Globalization.CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface(
                    MissionComboBoxMultiplayer.FontFamily,
                    MissionComboBoxMultiplayer.FontStyle,
                    MissionComboBoxMultiplayer.FontWeight,
                    MissionComboBoxMultiplayer.FontStretch),
                MissionComboBoxMultiplayer.FontSize,
                System.Windows.Media.Brushes.Black,
                VisualTreeHelper.GetDpi(
                    MissionComboBoxMultiplayer).PixelsPerDip);

        // Leave room for the ComboBox border, padding and drop-down arrow.
        double availableWidth =
            MissionComboBoxMultiplayer.ActualWidth -
            MissionComboBoxMultiplayer.Padding.Left -
            MissionComboBoxMultiplayer.Padding.Right -
            35;

        if (formattedText.Width > availableWidth)
        {
            MissionComboBoxMultiplayer.ToolTip = displayText;
        }
        else
        {
            MissionComboBoxMultiplayer.ToolTip = null;
        }
    }

    private void UpdateMapToolTip()
    {
        MapInfo? selectedMap =
            MapComboBoxMultiplayer.SelectedItem as MapInfo;

        if (selectedMap == null)
        {
            MapComboBoxMultiplayer.ToolTip = null;
            return;
        }

        string displayText =
            $"{selectedMap.FileName} | {selectedMap.Title}";

        if (MapComboBoxMultiplayer.ActualWidth <= 0)
        {
            MapComboBoxMultiplayer.ToolTip = null;
            return;
        }

        FormattedText formattedText =
            new FormattedText(
                displayText,
                System.Globalization.CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface(
                    MapComboBoxMultiplayer.FontFamily,
                    MapComboBoxMultiplayer.FontStyle,
                    MapComboBoxMultiplayer.FontWeight,
                    MapComboBoxMultiplayer.FontStretch),
                MapComboBoxMultiplayer.FontSize,
                System.Windows.Media.Brushes.Black,
                VisualTreeHelper.GetDpi(
                    MapComboBoxMultiplayer).PixelsPerDip);

        // Leave room for the ComboBox border, padding and drop-down arrow.
        double availableWidth =
            MapComboBoxMultiplayer.ActualWidth -
            MapComboBoxMultiplayer.Padding.Left -
            MapComboBoxMultiplayer.Padding.Right -
            35;

        if (formattedText.Width > availableWidth)
        {
            MapComboBoxMultiplayer.ToolTip = displayText;
        }
        else
        {
            MapComboBoxMultiplayer.ToolTip = null;
        }
    }

    private void NumericLimitTextBox_PreviewTextInput(
object sender,
System.Windows.Input.TextCompositionEventArgs e)
    {
        e.Handled =
            !e.Text.All(char.IsDigit);
    }

    private void LimitTextBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (!restoringSavedSelections)
        {
            SaveCurrentSettings();
        }

        UpdateCommandArguments();
    }

    private void SetupResolutions()
    {
        ResolutionComboBoxMultiplayer.Items.Clear();

        ResolutionComboBoxMultiplayer.Items.Add(
            Resolution.Default);

        Engine? engine =
            EngineComboBoxMultiplayer.SelectedItem as Engine;

        if (engine?.Game == QuakeGame.Quake2)
        {
            foreach ((int mode, int width, int height) in
                     GetQuake2VideoModes())
            {
                ResolutionComboBoxMultiplayer.Items.Add(
                    new Resolution(width, height, false));
            }
        }
        else
        {
            foreach (Resolution resolution in
                     Resolution.GetAvailableResolutions())
            {
                ResolutionComboBoxMultiplayer.Items.Add(resolution);
            }
        }

        ResolutionComboBoxMultiplayer.SelectedIndex = 0;
        ClearResolutionButtonMultiplayer.IsEnabled = false;
    }

    private static IEnumerable<(int Mode, int Width, int Height)>
        GetQuake2VideoModes()
    {
        // Quake 2 resolution list. The r_mode number is kept with each
        // resolution so the launch arguments use the same value.
        return new[]
        {
            (1, 1920, 1200),
            (2, 1920, 1080),
            (3, 1680, 1050),
            (4, 1600, 1024),
            (4, 1600, 900),
            (5, 1440, 900),
            (6, 1366, 768),
            (7, 1360, 768),
            (8, 1280, 1024),
            (9, 1280, 960),
            (10, 1280, 800),
            (11, 1280, 768),
            (12, 1280, 720),
            (13, 1152, 864),
            (14, 1024, 768),
            (15, 1024, 600),
            (16, 960, 720),
            (17, 856, 480),
            (18, 800, 600),
            (19, 800, 480),
            (20, 640, 480)
        };
    }

    private void Multiplayer_ResolutionComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        bool hasResolution =
            ResolutionComboBoxMultiplayer.SelectedItem is Resolution resolution &&
            !resolution.IsDefault;

        ClearResolutionButtonMultiplayer.IsEnabled =
            hasResolution;

        if (!restoringSavedSelections)
        {
            SaveCurrentSettings();
        }

        UpdateCommandArguments();
    }

    private void Multiplayer_ClearResolutionButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ResolutionComboBoxMultiplayer.SelectedIndex = 0;
        ClearResolutionButtonMultiplayer.IsEnabled = false;

        StatusTextMultiplayer.Text =
            "Cleared resolution selection.";

        SaveCurrentSettings();
        UpdateCommandArguments();
    }

    private List<string> BuildResolutionArguments()
    {
        Resolution? resolution =
            ResolutionComboBoxMultiplayer.SelectedItem as Resolution;

        if (resolution == null ||
            resolution.IsDefault)
        {
            return new List<string>();
        }

        Engine? engine =
            EngineComboBoxMultiplayer.SelectedItem as Engine;

        if (engine?.Game == QuakeGame.Quake2)
        {
            int mode = GetQuake2VideoMode(resolution);

            if (mode < 0)
            {
                return new List<string>();
            }

            return new List<string>
            {
                "+vid_fullscreen",
                "1",
                "+set",
                "r_mode",
                mode.ToString()
            };
        }

        return new List<string>
        {
            "-width",
            resolution.Width.ToString(),
            "-height",
            resolution.Height.ToString()
        };
    }

    private static int GetQuake2VideoMode(
        Resolution resolution)
    {
        foreach ((int mode, int width, int height) in
                 GetQuake2VideoModes())
        {
            if (width == resolution.Width &&
                height == resolution.Height)
            {
                return mode;
            }
        }

        return -1;
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
            return quake2Handler.GetEngineGameFolder(
                engine,
                quakeFolder);
        }

        // Quakespasm and Quakespasm-Spiked are special because the selected
        // folder can be a parent such as "Games". Prefer episodes in the
        // QS/QSS folder itself; if none, use exactly one folder above.
        if (IsQuakespasmEngine(engine))
        {
            return GetQuakespasmGameFolder(
                engine,
                quakeFolder);
        }

        // Ironwail has its own handler-specific game-root resolution.
        if (string.Equals(
                Path.GetFileName(engine.ExecutablePath),
                "ironwail.exe",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                engine.Name,
                "Ironwail",
                StringComparison.OrdinalIgnoreCase))
        {
            return quakeHandler.GetIronwailGameFolder(
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

            // No standard game directory was found.
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

    private static bool IsQuakespasmEngine(
        Engine engine)
    {
        string executableName =
            Path.GetFileName(engine.ExecutablePath);

        return string.Equals(
                   executableName,
                   "quakespasm.exe",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   executableName,
                   "quakespasm-sdl12.exe",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   executableName,
                   "quakespasm-spiked-win32.exe",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   executableName,
                   "quakespasm-spiked-win64.exe",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   engine.Name,
                   "Quakespasm",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   engine.Name,
                   "Quakespasm SDL",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   engine.Name,
                   "Quakespasm-Spiked",
                   StringComparison.OrdinalIgnoreCase);
    }

    private string GetQuakespasmGameFolder(
        Engine engine,
        string quakeFolder)
    {
        string? engineDirectory =
            Path.GetDirectoryName(
                engine.ExecutablePath);

        if (string.IsNullOrWhiteSpace(engineDirectory) ||
            !Directory.Exists(engineDirectory))
        {
            return quakeFolder;
        }

        // If QS/QSS have their own Quake data/episodes, prefer that folder.
        if (ContainsGameDirectory(
                engineDirectory,
                engine.Game) ||
            HasQuakespasmEpisodes(
                engineDirectory))
        {
            return engineDirectory;
        }

        // QS/QSS is installed inside a Quake folder. If the engine folder
        // itself has no episodes, use one directory level above it.
        DirectoryInfo? parent =
            Directory.GetParent(engineDirectory);

        if (parent != null &&
            Directory.Exists(parent.FullName))
        {
            string parentFolder =
                parent.FullName
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);

            if (ContainsGameDirectory(
                    parentFolder,
                    engine.Game) ||
                HasQuakespasmEpisodes(
                    parentFolder))
            {
                return parentFolder;
            }
        }

        // If neither QS/QSS nor its immediate parent contains usable Quake
        // data, keep the selected folder as the final fallback.
        return quakeFolder;
    }

    private bool HasQuakespasmEpisodes(
        string folder)
    {
        if (!Directory.Exists(folder))
        {
            return false;
        }

        List<MissionPack> detected =
            missionPackDetector
                .DetectMissionPacks(folder);

        // These names can represent separate Quake installations when a
        // parent folder such as "Games" contains multiple Quake folders.
        // They are not QS/QSS episodes.
        detected.RemoveAll(
            missionPack =>
                string.Equals(
                    missionPack.DetectedDirectory,
                    "Quake",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    missionPack.DetectedDirectory,
                    "Quake GOG",
                    StringComparison.OrdinalIgnoreCase));

        return detected.Count > 0;
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
                : game == QuakeGame.Quake3
                    ? new[]
                    {
                        "baseq3"
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
        // display the dedicated "No Quake engine detected" warning instead.
        // The root folder warning is relevant when an engine was found.
        if (!HasSupportedEngine(quakeFolder))
        {
            return true;
        }

        if (IsMainQuakeFolder(quakeFolder))
        {
            return true;
        }

        MultiplayerLauncherSettings settings =
            LoadSettings();

        if (settings.DontShowMainQuakeFolderWarning)
        {
            return true;
        }

        bool dontShowAgain;

        MessageBoxResult result =
            ShowMainQuakeFolderWarning(
                out dontShowAgain);

        if (dontShowAgain)
        {
            settings.DontShowMainQuakeFolderWarning = true;
            SaveSettings(settings);
        }

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
            new List<Engine>();

        try
        {
            engines.AddRange(
                engineDetector.DetectEngines(quakeFolder));
        }
        catch (UnauthorizedAccessException)
        {
            // Ignore protected folders encountered by an engine detector.
        }
        catch (IOException)
        {
            // Ignore folders that cannot be read.
        }

        try
        {
            engines.AddRange(
                engineDetector2.DetectEngines(quakeFolder));
        }
        catch (UnauthorizedAccessException)
        {
            // Ignore protected folders encountered by an engine detector.
        }
        catch (IOException)
        {
            // Ignore folders that cannot be read.
        }

        try
        {
            engines.AddRange(
                engineDetector3.DetectEngines(quakeFolder));
        }
        catch (UnauthorizedAccessException)
        {
            // Ignore protected folders encountered by an engine detector.
        }
        catch (IOException)
        {
            // Ignore folders that cannot be read.
        }

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
                   QuakeGame.Quake2) ||
               ContainsGameDirectory(
                   folder,
                   QuakeGame.Quake3);
    }

    private void DetectQuakeInstallation(
        string quakeFolder)
    {
        suppressNoEpisodeWarning = true;

        try
        {
            DetectEngines(quakeFolder);
        }
        finally
        {
            suppressNoEpisodeWarning = false;
        }

        if (EngineComboBoxMultiplayer.Items.Count == 0)
        {
            return;
        }

        DetectMissionPacks(quakeFolder);
    }

    private void DetectEngines(string quakeFolder)
    {
        EngineComboBoxMultiplayer.Items.Clear();

        ModeComboBoxMultiplayer.Items.Clear();
        ModeComboBoxMultiplayer.SelectedIndex = -1;

        List<Engine> engines =
            engineDetector.DetectEngines(quakeFolder);

        engines.AddRange(
            engineDetector2.DetectEngines(quakeFolder));

        engines.AddRange(
            engineDetector3.DetectEngines(quakeFolder));

        foreach (Engine engine in engines)
        {
            EngineComboBoxMultiplayer.Items.Add(engine);
        }

        if (EngineComboBoxMultiplayer.Items.Count == 0)
        {
            // No engine: resolution must remain an empty selector.
            ResolutionComboBoxMultiplayer.Items.Clear();
            ResolutionComboBoxMultiplayer.SelectedIndex = -1;
            ClearResolutionButtonMultiplayer.IsEnabled = false;

            // No supported engine means the current folder cannot
            // provide valid episode/map selections either.
            MissionComboBoxMultiplayer.Items.Clear();
            MissionComboBoxMultiplayer.SelectedIndex = -1;

            MapComboBoxMultiplayer.Items.Clear();
            MapComboBoxMultiplayer.SelectedIndex = -1;
            MapComboBoxMultiplayer.ToolTip = null;

            ModeComboBoxMultiplayer.Items.Clear();
            ModeComboBoxMultiplayer.SelectedIndex = -1;


            ClearMapButtonMultiplayer.IsEnabled = false;
            ClearModeButtonMultiplayer.IsEnabled = false;
            ClearExtraArgumentsButtonMultiplayer.IsEnabled = false;
            RefreshEnginesButtonMultiplayer.IsEnabled = false;
            RefreshEpisodesButtonMultiplayer.IsEnabled = false;
            CommandArgumentsTextBoxMultiplayer.Document.Blocks.Clear();

            StatusTextMultiplayer.Text =
                "No engine(s) detected inside selected folder(s).";

            System.Windows.MessageBox.Show(
                "No engine(s) detected inside selected folder(s).",
                "Multiplayer",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        // Select the first engine before building the resolution list so
        // the initial resolution set matches the selected engine.
        EngineComboBoxMultiplayer.SelectedIndex = 0;
        RefreshEnginesButtonMultiplayer.IsEnabled = true;

        // Rebuild resolution and mode selectors.
        SetupResolutions();
        SetupModeOptions();
        RefreshEpisodesButtonMultiplayer.IsEnabled = true;

    }

    private void DetectMissionPacks(string quakeFolder)
    {
        MissionComboBoxMultiplayer.Items.Clear();

        Engine? engine =
            EngineComboBoxMultiplayer.SelectedItem as Engine;

        List<MissionPack> missionPacks;

        string detectionFolder =
            engine == null
                ? quakeFolder
                : GetEngineGameFolder(
                    engine,
                    quakeFolder);

        if (engine?.Game == QuakeGame.Quake1)
        {
            missionPacks =
                quakeHandler.DetectMissionPacks(
                    engine,
                    detectionFolder,
                    missionPackDetector);
        }
        else if (engine?.Game == QuakeGame.Quake2)
        {
            missionPacks =
                quake2Handler.DetectMissionPacks(
                    engine,
                    detectionFolder,
                    missionPackDetector2);
        }
        else if (engine?.Game == QuakeGame.Quake3)
        {
            missionPacks =
                quake3Handler.DetectMissionPacks(
                    engine,
                    detectionFolder,
                    missionPackDetector3);
        }
        else
        {
            missionPacks =
                missionPackDetector
                    .DetectMissionPacks(detectionFolder);
        }

        // Quakespasm and Quakespasm-Spiked may use the selected parent
        // folder as their game-data root. If that parent also contains
        // separate Quake installations, those installation folders are not
        // episodes for QS/QSS and must not appear in the drop-down.
        if (engine != null &&
            engine.Game == QuakeGame.Quake1 &&
            IsQuakespasmEngine(engine))
        {
            missionPacks.RemoveAll(
                missionPack =>
                    string.Equals(
                        missionPack.DetectedDirectory,
                        "Quake",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        missionPack.DetectedDirectory,
                        "Quake GOG",
                        StringComparison.OrdinalIgnoreCase));
        }

        foreach (MissionPack missionPack in missionPacks)
        {
            MissionComboBoxMultiplayer.Items.Add(missionPack);
        }

        if (MissionComboBoxMultiplayer.Items.Count > 0)
        {
            MissionComboBoxMultiplayer.SelectedIndex = 0;

            // An episode was detected, so restore the normal mode
            // options and use Normal as the default.
            if (ModeComboBoxMultiplayer.Items.Count > 2)
            {
                ModeComboBoxMultiplayer.SelectedIndex = 0;
            }
            else
            {
                SetupModeOptions();
            }

            StatusTextMultiplayer.Text =
                $"Found {EngineComboBoxMultiplayer.Items.Count} engine(s) and " +
                $"{MissionComboBoxMultiplayer.Items.Count} episode(s).";
        }
        else
        {
            StatusTextMultiplayer.Text =
                "No episode(s) detected inside selected folder(s).";

            // Apply the no-episode UI state before showing the warning so the
            // refresh button and mode selector are already empty
            // while the warning is being displayed.
            RefreshEpisodesButtonMultiplayer.IsEnabled = false;
            ClearMapButtonMultiplayer.IsEnabled = false;
            ClearModeButtonMultiplayer.IsEnabled = false;

            MapComboBoxMultiplayer.Items.Clear();
            MapComboBoxMultiplayer.SelectedIndex = -1;
            MapComboBoxMultiplayer.ToolTip = null;

            ModeComboBoxMultiplayer.Items.Clear();
            ModeComboBoxMultiplayer.SelectedIndex = -1;
            ModeComboBoxMultiplayer.IsEnabled =
                EngineComboBoxMultiplayer.SelectedItem is Engine selectedEngine &&
                selectedEngine.Game == QuakeGame.Quake1;
            ClearModeButtonMultiplayer.IsEnabled = false;
            ModeLabelMultiplayer.Foreground =
                System.Windows.SystemColors.ControlTextBrush;
            UpdateCommandArguments();

            if (!suppressNoEpisodeWarning)
            {
                System.Windows.MessageBox.Show(
                    "No episode(s) detected inside selected folder(s).",
                    "Multiplayer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        if (MissionComboBoxMultiplayer.Items.Count == 0)
        {
            MapComboBoxMultiplayer.Items.Clear();
            MapComboBoxMultiplayer.SelectedIndex = -1;
            MapComboBoxMultiplayer.ToolTip = null;

            ModeComboBoxMultiplayer.Items.Clear();
            ModeComboBoxMultiplayer.SelectedIndex = -1;
            ModeComboBoxMultiplayer.IsEnabled =
                EngineComboBoxMultiplayer.SelectedItem is Engine selectedEngine &&
                selectedEngine.Game == QuakeGame.Quake1;
            ClearModeButtonMultiplayer.IsEnabled = false;
            ModeLabelMultiplayer.Foreground =
                System.Windows.SystemColors.ControlTextBrush;
            UpdateCommandArguments();
            return;
        }

        DetectMaps();

        if (MissionComboBoxMultiplayer.Items.Count == 0)
        {
            ClearMapButtonMultiplayer.IsEnabled = false;
            ClearModeButtonMultiplayer.IsEnabled = false;
            RefreshEpisodesButtonMultiplayer.IsEnabled = false;
        }
    }

    private string GetEpisodeFolder(MissionPack missionPack)
    {
        string quakeFolder =
            QuakeFolderTextBoxMultiplayer.Text.Trim();

        if (string.IsNullOrWhiteSpace(quakeFolder))
        {
            return "";
        }

        string gameDirectory =
            missionPack.GameDirectory?.Trim() ?? "";

        Engine? engine =
            EngineComboBoxMultiplayer.SelectedItem as Engine;

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
        MapComboBoxMultiplayer.Items.Clear();
        MapComboBoxMultiplayer.SelectedIndex = -1;
        MapComboBoxMultiplayer.ToolTip = null;

        Engine? engine =
            EngineComboBoxMultiplayer.SelectedItem as Engine;

        // With no engine detected, keep Map as an empty, usable drop-down.
        // Do not add any value or select a synthetic "None" entry.
        if (engine == null)
        {
            MapComboBoxMultiplayer.IsEnabled = true;
            ClearMapButtonMultiplayer.IsEnabled = false;
            MapLabelMultiplayer.IsEnabled = true;
            MapComboBoxMultiplayer.Foreground =
                System.Windows.SystemColors.ControlTextBrush;
            MapLabelMultiplayer.Foreground =
                System.Windows.SystemColors.ControlTextBrush;

            UpdateCommandArguments();
            return;
        }

        MapComboBoxMultiplayer.Items.Add(MapInfo.None);

        MissionPack? missionPack =
            MissionComboBoxMultiplayer.SelectedItem as MissionPack;

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
                    QuakeFolderTextBoxMultiplayer.Text.Trim());
            }
            else
            {
                StatusTextMultiplayer.Text =
                    $"Episode folder not found:\n{gameFolder}";

                UpdateCommandArguments();
                return;
            }
        }

        bool isQuake2 =
            engine.Game == QuakeGame.Quake2;

        bool isQuake3 =
            engine.Game == QuakeGame.Quake3;

        List<MapInfo> maps;

        // Detect all maps first, then keep only multiplayer map names.
        if (isQuake2)
        {
            maps =
                MapDetector2.DetectMaps(gameFolder);
        }
        // Quake 3 map detector.
        else if (isQuake3)
        {
            maps =
                MapDetector3.DetectMaps(gameFolder);
        }
        // Quake 1 map detector.
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

        // Multiplayer only: show maps containing dm, ctf, tourney or team.
        maps = maps
            .Where(
                map =>
                    IsMultiplayerMap(map.FileName))
            .ToList();

        foreach (MapInfo map in maps)
        {
            string mapFileName =
                Path.GetFileNameWithoutExtension(
                    map.FileName)?.Trim() ?? "";

            string mapTitle =
                map.Title?.Trim() ?? "";

            if (!string.Equals(
                    mapFileName,
                    mapTitle,
                    StringComparison.OrdinalIgnoreCase))
            {
                map.Title =
                    CapitalizeFirstLetter(
                        map.Title);
            }

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

            MapComboBoxMultiplayer.Items.Add(map);
        }

        if (!restoreMapSelectionCleared)
        {
            string? defaultMap =
                isQuake2
                    ? quake2Handler.GetDefaultMap(
                        missionPack,
                        engine)
                    : isQuake3
                        ? quake3Handler.GetDefaultMap(
                            missionPack)
                        : quakeHandler.GetDefaultMap(
                            missionPack);

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
                MapComboBoxMultiplayer.SelectedIndex =
                    defaultIndex;
            }
            else if (MapComboBoxMultiplayer.Items.Count > 1)
            {
                MapComboBoxMultiplayer.SelectedIndex = 1;
            }
            else
            {
                MapComboBoxMultiplayer.SelectedIndex = 0;
            }
        }

        UpdateMapToolTip();
        UpdateCommandArguments();
    }

    private bool IsQuake3Game()
    {
        Engine? engine =
            EngineComboBoxMultiplayer.SelectedItem as Engine;

        return engine?.Game == QuakeGame.Quake3;
    }

    private void UpdateModeControlsState()
    {
        Engine? selectedEngine =
            EngineComboBoxMultiplayer.SelectedItem as Engine;

        bool modeAvailable =
            selectedEngine != null &&
            selectedEngine.Game == QuakeGame.Quake1;

        ModeComboBoxMultiplayer.IsEnabled =
            modeAvailable;

        ClearModeButtonMultiplayer.IsEnabled =
            modeAvailable &&
            ModeComboBoxMultiplayer.SelectedIndex >= 0;

        ModeLabelMultiplayer.Foreground =
            modeAvailable
                ? System.Windows.SystemColors.ControlTextBrush
                : System.Windows.Media.Brushes.DarkGray;
    }


    private static string CapitalizeFirstLetter(
    string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value ?? "";
        }

        string text = value.Trim();

        return char.ToUpperInvariant(text[0]) +
               text[1..];
    }

    private static IEnumerable<string> EnumerateFilesSafe(
    string rootFolder)
    {
        if (!Directory.Exists(rootFolder))
        {
            yield break;
        }

        Stack<string> folders =
            new();

        folders.Push(rootFolder);

        while (folders.Count > 0)
        {
            string currentFolder =
                folders.Pop();

            string folderName =
                Path.GetFileName(
                    currentFolder.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar));

            // Windows system/protected folders that should never be scanned.
            // Sometimes these folders can be present or hidden.
            if (string.Equals(
                    folderName,
                    "$Recycle.Bin",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    folderName,
                    "Config.Msi",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    folderName,
                    "PerfLogs",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    folderName,
                    "System Volume Information",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string[] files;

            try
            {
                files =
                    Directory.GetFiles(
                        currentFolder,
                        "*",
                        SearchOption.TopDirectoryOnly);
            }
            catch (UnauthorizedAccessException)
            {
                // Skip folders the launcher is not allowed to read.
                continue;
            }
            catch (IOException)
            {
                // Skip folders that disappear or otherwise cannot be read.
                continue;
            }

            foreach (string file in files)
            {
                yield return file;
            }

            string[] subdirectories;

            try
            {
                subdirectories =
                    Directory.GetDirectories(
                        currentFolder,
                        "*",
                        SearchOption.TopDirectoryOnly);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            foreach (string subdirectory in subdirectories)
            {
                string subdirectoryName =
                    Path.GetFileName(
                        subdirectory.TrimEnd(
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar));

                // Exclude Windows system/protected folders.
                if (string.Equals(
                        subdirectoryName,
                        "$Recycle.Bin",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        subdirectoryName,
                        "Config.Msi",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        subdirectoryName,
                        "PerfLogs",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        subdirectoryName,
                        "System Volume Information",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                folders.Push(subdirectory);
            }
        }
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
            foreach (string archiveFile in EnumerateFilesSafe(
                gameFolder))
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
                   "ctf",
                   StringComparison.OrdinalIgnoreCase)
               || mapName.Contains(
                   "tourney",
                   StringComparison.OrdinalIgnoreCase)
               || mapName.Contains(
                   "team",
                   StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshEnginesButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string quakeFolder =
            QuakeFolderTextBoxMultiplayer.Text.Trim();

        if (string.IsNullOrWhiteSpace(quakeFolder) ||
            !Directory.Exists(quakeFolder))
        {
            StatusTextMultiplayer.Text =
                "Select a valid Quake game folder first.";
            return;
        }

        Engine? currentEngine =
            EngineComboBoxMultiplayer.SelectedItem as Engine;

        string currentEnginePath =
            currentEngine?.ExecutablePath ?? "";

        QuakeGame currentEngineGame =
            currentEngine?.Game ?? QuakeGame.Quake1;

        suppressNoEpisodeWarning = true;

        try
        {
            DetectEngines(quakeFolder);
        }
        finally
        {
            suppressNoEpisodeWarning = false;
        }

        Engine? refreshedEngine =
            EngineComboBoxMultiplayer.Items
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
            EngineComboBoxMultiplayer.SelectedItem =
                refreshedEngine;

            StatusTextMultiplayer.Text =
                $"Found {EngineComboBoxMultiplayer.Items.Count} engine(s).";
        }
        else if (EngineComboBoxMultiplayer.Items.Count > 0)
        {
            StatusTextMultiplayer.Text =
                $"Found {EngineComboBoxMultiplayer.Items.Count} engine(s).";
        }
        else
        {
            StatusTextMultiplayer.Text =
                "No engine(s) detected inside selected folder(s).";
        }

        if (EngineComboBoxMultiplayer.Items.Count > 0)
        {
            DetectMissionPacks(quakeFolder);
            RefreshEpisodesButtonMultiplayer.IsEnabled =
                MissionComboBoxMultiplayer.Items.Count > 0;
        }
    }

    private void RefreshEpisodesButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string quakeFolder =
            QuakeFolderTextBoxMultiplayer.Text.Trim();

        if (string.IsNullOrWhiteSpace(quakeFolder) ||
            !Directory.Exists(quakeFolder))
        {
            StatusTextMultiplayer.Text =
                "Select a valid Quake game folder first.";
            return;
        }

        MissionPack? currentMissionPack =
            MissionComboBoxMultiplayer.SelectedItem as MissionPack;

        string currentMissionDirectory =
            currentMissionPack?.GameDirectory ??
            currentMissionPack?.DetectedDirectory ?? "";

        DetectMissionPacks(quakeFolder);

        MissionPack? refreshedMissionPack =
            MissionComboBoxMultiplayer.Items
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
            MissionComboBoxMultiplayer.SelectedItem =
                refreshedMissionPack;

            StatusTextMultiplayer.Text =
                $"Found {MissionComboBoxMultiplayer.Items.Count} episode(s).";
        }
        else if (MissionComboBoxMultiplayer.Items.Count > 0)
        {
            StatusTextMultiplayer.Text =
                $"Found {MissionComboBoxMultiplayer.Items.Count} episode(s).";
        }
        else
        {
            StatusTextMultiplayer.Text =
                "No episode(s) detected inside selected folder(s).";
        }
    }

    private void Multiplayer_EngineComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (EngineComboBoxMultiplayer.SelectedItem is not Engine)
        {
            return;
        }

        // Resolution modes are engine-specific. Rebuild the selector whenever
        // the selected engine changes so Quake 2 does not show Quake modes.
        SetupResolutions();

        if (!restoringSavedSelections)
        {
            DetectMissionPacks(
                QuakeFolderTextBoxMultiplayer.Text.Trim());
        }
        else
        {
            UpdateCommandArguments();
        }

        UpdateModeControlsState();
    }

    private void Multiplayer_MissionComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        UpdateMissionToolTip();

        if (MissionComboBoxMultiplayer.SelectedItem is not MissionPack)
        {
            ClearMapButtonMultiplayer.IsEnabled = false;
            ClearModeButtonMultiplayer.IsEnabled = false;

            MapComboBoxMultiplayer.Items.Clear();
            MapComboBoxMultiplayer.SelectedIndex = -1;

            ModeComboBoxMultiplayer.Items.Clear();
            ModeComboBoxMultiplayer.SelectedIndex = -1;
            ModeComboBoxMultiplayer.IsEnabled =
                EngineComboBoxMultiplayer.SelectedItem is Engine selectedEngine &&
                selectedEngine.Game == QuakeGame.Quake1;
            ClearModeButtonMultiplayer.IsEnabled = false;
            ModeLabelMultiplayer.Foreground =
                System.Windows.SystemColors.ControlTextBrush;
            UpdateCommandArguments();
            return;
        }

        DetectMaps();

        if (!restoreMapSelectionCleared &&
            !restoreModeSelectionCleared &&
            ModeComboBoxMultiplayer.Items.Count > 0)
        {
            ModeComboBoxMultiplayer.SelectedIndex = 0;
        }

        UpdateModeControlsState();
    }

    private void Multiplayer_MapComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (MapComboBoxMultiplayer.SelectedItem is MapInfo)
        {
            SaveCurrentSettings();
        }

        UpdateMapToolTip();

        ClearMapButtonMultiplayer.IsEnabled =
            MapComboBoxMultiplayer.SelectedIndex > 0;

        UpdateCommandArguments();
    }

    private sealed class MultiplayerMode
    {
        public string Name { get; set; } = "";
        public int Value { get; set; }
        public System.Windows.Media.Brush Foreground { get; set; } =
            System.Windows.Media.Brushes.Black;
    }

    private static System.Windows.Media.Brush HexBrush(string hex)
    {
        return new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)
                System.Windows.Media.ColorConverter.ConvertFromString(hex));
    }

    private void SetupModeOptions()
    {
        ModeComboBoxMultiplayer.Items.Clear();

        Engine? engine =
            EngineComboBoxMultiplayer.SelectedItem as Engine;

        if (engine == null ||
            engine.Game != QuakeGame.Quake1)
        {
            ModeComboBoxMultiplayer.SelectedIndex = -1;
            UpdateModeControlsState();
            return;
        }

        ModeComboBoxMultiplayer.Items.Add(
            new MultiplayerMode
            {
                Name = "Standard Mode",
                Value = 1,
                Foreground = HexBrush("#000000")
            });

        ModeComboBoxMultiplayer.Items.Add(
            new MultiplayerMode
            {
                Name = "Weapons Stay",
                Value = 2,
                Foreground = HexBrush("#000000")
            });

        ModeComboBoxMultiplayer.Items.Add(
            new MultiplayerMode
            {
                Name = "Combined",
                Value = 3,
                Foreground = HexBrush("#000000")
            });

        ModeComboBoxMultiplayer.SelectedIndex = 0;
        UpdateModeControlsState();
    }


    private void Multiplayer_ModeComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        UpdateModeControlsState();
        UpdateCommandArguments();
    }

    private void CloseAfterLaunchCheckBox_Click(
    object sender,
    RoutedEventArgs e)
    {
        SaveCurrentSettings();
    }

    private void Multiplayer_ExtraArgumentsTextBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        ClearExtraArgumentsButtonMultiplayer.IsEnabled =
            !string.IsNullOrWhiteSpace(
                ExtraArgumentsTextBoxMultiplayer.Text);

        UpdateCommandArguments();
    }

    private void Multiplayer_ClearExtraArgumentsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ExtraArgumentsTextBoxMultiplayer.Clear();

        StatusTextMultiplayer.Text =
            "Cleared extra arguments.";
    }

    private void Multiplayer_ClearMapButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        MapComboBoxMultiplayer.SelectedIndex = 0;

        MapComboBoxMultiplayer.ToolTip = null;

        ModeComboBoxMultiplayer.SelectedIndex = 0;

        // Both selections are now cleared so their Clear buttons must
        // remain unavailable until the user selects them again.
        ClearMapButtonMultiplayer.IsEnabled = false;
        ClearModeButtonMultiplayer.IsEnabled = false;

        StatusTextMultiplayer.Text =
            "Cleared map and mode selection.";

        SaveCurrentSettings();

        UpdateCommandArguments();
    }

    private void Multiplayer_ClearModeButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ModeComboBoxMultiplayer.SelectedIndex = 0;

        ClearModeButtonMultiplayer.IsEnabled = false;

        StatusTextMultiplayer.Text =
            "Cleared mode selection.";

        SaveCurrentSettings();

        UpdateCommandArguments();
    }

    private void Multiplayer_CreateDesktopShortcutButton_Click(
    object sender,
    RoutedEventArgs e)
    {
        Engine? engine =
            EngineComboBoxMultiplayer.SelectedItem as Engine;

        MissionPack? missionPack =
            MissionComboBoxMultiplayer.SelectedItem as MissionPack;

        MapInfo? selectedMap =
            MapComboBoxMultiplayer.SelectedItem as MapInfo;

        if (engine == null)
        {
            System.Windows.MessageBox.Show(
                "Please select an engine first.",
                "Warning",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        if (missionPack == null)
        {
            System.Windows.MessageBox.Show(
                "Please select an episode first.",
                "Warning",
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

            string mapTitle =
                selectedMap == null ||
                string.IsNullOrWhiteSpace(selectedMap.FileName)
                    ? ""
                    : selectedMap.Title?.Trim() ?? "";

            // Desktop shortcut name format.
            string shortcutName =
                string.IsNullOrWhiteSpace(mapTitle)
                    ? $"Launch {engine.Name} - {missionPack.Name}.lnk"
                    : $"Launch {engine.Name} - {missionPack.Name} - {mapTitle}.lnk";

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
                QuakeFolderTextBoxMultiplayer.Text.Trim();

            shortcut.Arguments =
                shortcutArguments;

            shortcut.Description =
                "Tiny Quake Launcher command line shortcut";

            shortcut.IconLocation =
                engine.ExecutablePath + ",0";

            shortcut.Save();

            StatusTextMultiplayer.Text =
                "Desktop shortcut created successfully.";

            System.Windows.MessageBox.Show(
                "Desktop shortcut created successfully.",
                "Information",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (System.Exception ex)
        {
            StatusTextMultiplayer.Text =
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

    private void AddLimitArguments(List<string> arguments)
    {
        if (FragLimitCheckBoxMultiplayer.IsChecked == true)
        {
            arguments.Add("+fraglimit");
            arguments.Add("20");
        }

        if (FlagLimitCheckBoxMultiplayer.IsChecked == true)
        {
            arguments.Add("+flaglimit");
            arguments.Add("0");
        }

        if (TimeLimitCheckBoxMultiplayer.IsChecked == true)
        {
            arguments.Add("+timelimit");
            arguments.Add("10");
        }

        if (MaxPlayersCheckBoxMultiplayer.IsChecked == true)
        {
            arguments.Add("-listen");
            arguments.Add("8");
        }
    }

    private List<string> BuildLaunchArguments()
    {
        Engine? engine =
            EngineComboBoxMultiplayer.SelectedItem as Engine;

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
                        CommandArgumentsTextBoxMultiplayer.Document.ContentStart,
                        CommandArgumentsTextBoxMultiplayer.Document.ContentEnd).Text);

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
        else if (engine.Game == QuakeGame.Quake3)
        {
            arguments = BuildQuake3AutomaticLaunchArguments();
        }
        else
        {
            arguments = BuildQuake1AutomaticLaunchArguments();
        }

        AddLimitArguments(arguments);

        // Extra arguments are added here ONLY for the actual launch command.
        // They are deliberately not part of the automatic argument builders,
        // so the command preview can display them separately in blue.
        arguments.AddRange(
            ParseExtraArguments(
                ExtraArgumentsTextBoxMultiplayer.Text));

        return arguments;
    }

    private List<string> BuildQuake1AutomaticLaunchArguments()
    {
        MissionPack? missionPack =
            MissionComboBoxMultiplayer.SelectedItem as MissionPack;

        Engine? engine =
            EngineComboBoxMultiplayer.SelectedItem as Engine;

        List<string> arguments = new();

        arguments.AddRange(
            BuildResolutionArguments());

        if (ModeComboBoxMultiplayer.SelectedItem
            is MultiplayerMode mode)
        {
            arguments.Add("+deathmatch");
            arguments.Add(mode.Value.ToString());
        }

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

        MapInfo? selectedMap =
            MapComboBoxMultiplayer.SelectedItem as MapInfo;

        string? mapName = null;

        if (selectedMap != null)
        {
            mapName =
                Path.GetFileNameWithoutExtension(
                    selectedMap.FileName);
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
            MissionComboBoxMultiplayer.SelectedItem as MissionPack;

        List<string> arguments = new();

        arguments.AddRange(
            BuildResolutionArguments());

        // The engine runs with its own folder as the working directory.
        // Quake 2 uses +set game for mission packs/mods.
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

        MapInfo? selectedMap =
            MapComboBoxMultiplayer.SelectedItem as MapInfo;

        string? mapName = null;

        if (selectedMap != null)
        {
            mapName =
                Path.GetFileNameWithoutExtension(
                    selectedMap.FileName);
        }


        if (!string.IsNullOrWhiteSpace(mapName))
        {
            arguments.Add("+map");
            arguments.Add(mapName);
        }

        return arguments;
    }

    private List<string> BuildQuake3AutomaticLaunchArguments()
    {
        MissionPack? missionPack =
            MissionComboBoxMultiplayer.SelectedItem as MissionPack;

        List<string> arguments = new();

        arguments.AddRange(
            BuildResolutionArguments());

        // ioquake3 uses +set fs_game for Team Arena and other
        // game/mod directories. baseq3 is the default folder.
        if (missionPack != null &&
            !string.IsNullOrWhiteSpace(
                missionPack.GameDirectory) &&
            !string.Equals(
                missionPack.GameDirectory,
                "baseq3",
                StringComparison.OrdinalIgnoreCase))
        {
            arguments.Add("+set");
            arguments.Add("fs_game");
            arguments.Add(
                missionPack.GameDirectory.Trim());
        }

        // Quake 3 needs 512 MB of hunk memory.
        arguments.Add("+set");
        arguments.Add("com_hunkmegs");
        arguments.Add("512");

        MapInfo? selectedMap =
            MapComboBoxMultiplayer.SelectedItem as MapInfo;

        string? mapName = null;

        if (selectedMap != null)
        {
            mapName =
                Path.GetFileNameWithoutExtension(
                    selectedMap.FileName);
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

        CommandArgumentsTextBoxMultiplayer.Document.Blocks.Clear();

        Engine? engine =
            EngineComboBoxMultiplayer.SelectedItem as Engine;

        // Manual arguments must remain visible when no engine is detected.
        // Build automatic arguments only when an engine is available.
        List<string> automaticArguments =
            engine == null
                ? new List<string>()
                : engine.Game == QuakeGame.Quake2
                    ? BuildQuake2AutomaticLaunchArguments()
                    : engine.Game == QuakeGame.Quake3
                        ? BuildQuake3AutomaticLaunchArguments()
                        : BuildQuake1AutomaticLaunchArguments();

        AddLimitArguments(automaticArguments);

        List<string> extraArguments =
            ParseExtraArguments(
                ExtraArgumentsTextBoxMultiplayer.Text);

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

        // Only manual extra arguments are blue.
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

        CommandArgumentsTextBoxMultiplayer.Document.Blocks.Add(
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

    private void Multiplayer_LaunchButton_Click(
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

    private MessageBoxResult ShowMainQuakeFolderWarning(
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
                    "TQLauncher did not detect a main Quake folder so episode and\n" +
                    "map detection could be problematic. Continue anyway?",
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
                Owner = System.Windows.Window.GetWindow(this),
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
                Owner = System.Windows.Window.GetWindow(this),
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
            EngineComboBoxMultiplayer.SelectedItem as Engine;

        MissionPack? missionPack =
            MissionComboBoxMultiplayer.SelectedItem as MissionPack;

        MapInfo? selectedMap =
            MapComboBoxMultiplayer.SelectedItem as MapInfo;

        // An engine is required before any map-related launch warning.
        if (engine == null)
        {
            StatusTextMultiplayer.Text =
                "Game cannot run with no supported engine.";

            System.Windows.MessageBox.Show(
                "Game cannot run with no supported engine.",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return;
        }

        if (selectedMap == null ||
            string.IsNullOrWhiteSpace(selectedMap.FileName))
        {
            MultiplayerLauncherSettings settings =
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
            QuakeFolderTextBoxMultiplayer.Text.Trim();

        if (string.IsNullOrWhiteSpace(quakeFolder))
        {
            StatusTextMultiplayer.Text =
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
            StatusTextMultiplayer.Text =
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
            StatusTextMultiplayer.Text =
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

            StatusTextMultiplayer.Text =
                $"{engine.Name} is set to run with custom settings.";

            if (CloseAfterLaunchCheckBoxMultiplayer.IsChecked == true)
            {
                System.Windows.Window.GetWindow(this)?.Close();
            }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            StatusTextMultiplayer.Text =
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
            StatusTextMultiplayer.Text =
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