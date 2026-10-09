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
using LauncherProfiles = TinyQuakeLauncher.Profiles.Profiles;
using TinyQuakeLauncher.Profiles;

namespace TinyQuakeLauncher;

public class LauncherSettings
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

    public int? Difficulty { get; set; }

    public bool DifficultySelectionCleared { get; set; }

    public string DemoFileName { get; set; } = "";

    public bool CloseAfterLaunch { get; set; }

    public string ExtraArguments { get; set; } = "";
}

public sealed class MapInfoDisplayConverter : System.Windows.Data.IValueConverter
{
    public object Convert(
        object value,
        Type targetType,
        object parameter,
        System.Globalization.CultureInfo culture)
    {
        if (value is not MapInfo map)
        {
            return value?.ToString() ?? "";
        }

        if (string.Equals(map.FileName, "?", StringComparison.Ordinal))
        {
            return "Random";
        }

        if (string.IsNullOrWhiteSpace(map.FileName))
        {
            return string.IsNullOrWhiteSpace(map.Title)
                ? ""
                : map.Title;
        }

        return $"{map.FileName} | {map.Title}";
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        System.Globalization.CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public partial class MainWindow : Window
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

    private readonly DemoDetector2 demoDetector2 = new();

    private readonly DemoDetector3 demoDetector3 = new();

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
            "TQLauncherTab1.json");

    private bool restoreMapSelectionCleared;
    private bool restoreDifficultySelectionCleared;
    private bool restoringSavedSelections;
    private bool demoSelectionActive;
    private bool updatingCommandArguments;
    private bool commandArgumentsEdited;
    private string lastAcceptedQuakeFolder = "";
    private bool suppressNoEpisodeWarning;
    private readonly LauncherProfiles profiles = LauncherProfiles.Load("TQLauncherTab1Profiles.json");
    private bool loadingProfiles;

    public MainWindow()
    {
        InitializeComponent();

        ProfileComboBox.DisplayMemberPath =
            nameof(LauncherProfile.Name);

        ProfileComboBox.SelectionChanged +=
            ProfileComboBox_SelectionChanged;

        SaveProfileButton.Click +=
            SaveProfileButton_Click;

        DeleteProfileButton.Click +=
            DeleteProfileButton_Click;

        LoadProfiles();

        FrameworkElementFactory mapText =
            new FrameworkElementFactory(typeof(TextBlock));
        mapText.SetBinding(
            TextBlock.TextProperty,
            new System.Windows.Data.Binding
            {
                Converter = new MapInfoDisplayConverter()
            });

        mapText.SetBinding(
            TextBlock.ForegroundProperty,
            new System.Windows.Data.Binding(nameof(MapInfo.Foreground)));

        MapComboBox.ItemTemplate =
            new DataTemplate(typeof(MapInfo))
            {
                VisualTree = mapText
            };

        QuakeFolderTextBox.Padding =
            new Thickness(3,
                QuakeFolderTextBox.Padding.Top,
                QuakeFolderTextBox.Padding.Right,
                QuakeFolderTextBox.Padding.Bottom);

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

        MissionComboBox.SizeChanged +=
            MissionComboBox_SizeChanged;

        restoringSavedSelections = true;
        SetupResolutions();
        restoringSavedSelections = false;

        ClearResolutionButton.IsEnabled = false;
        RefreshEnginesButton.IsEnabled = false;
        RefreshEpisodesButton.IsEnabled = false;

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
                    "Singleplayer warning",
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

            Resolution? selectedResolution =
                ResolutionComboBox.SelectedItem as Resolution;

            settings.Resolution =
                selectedResolution != null &&
                !selectedResolution.IsDefault
                    ? selectedResolution.DisplayName
                    : "";

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
                (string.IsNullOrWhiteSpace(selectedMap.FileName) &&
                 !string.Equals(selectedMap.Title, "Random",
                     StringComparison.OrdinalIgnoreCase));

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
        if (!string.IsNullOrWhiteSpace(settings.Resolution))
        {
            Resolution? resolution =
                ResolutionComboBox.Items
                    .OfType<Resolution>()
                    .FirstOrDefault(
                        item => string.Equals(
                            item.DisplayName,
                            settings.Resolution,
                            StringComparison.OrdinalIgnoreCase));

            if (resolution != null)
            {
                ResolutionComboBox.SelectedItem =
                    resolution;
            }
        }

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

                // Rebuild resolutions for the saved engine even while the
                // normal selection-change handler is suppressed.
                SetupResolutions();

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
        else if (string.Equals(
            settings.MapFileName,
            "?",
            StringComparison.Ordinal))
        {
            MapComboBox.SelectedIndex = 1;
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

    private void LoadProfiles()
    {
        loadingProfiles = true;

        try
        {
            ProfileComboBox.Items.Clear();

            foreach (LauncherProfile profile in profiles.Items)
            {
                ProfileComboBox.Items.Add(profile);
            }
        }
        finally
        {
            loadingProfiles = false;
        }
    }

    private void ProfileComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (loadingProfiles)
        {
            return;
        }

        if (ProfileComboBox.SelectedItem is LauncherProfile profile)
        {
            ApplyProfile(profile);
        }
    }

    private void SaveProfileButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string? defaultName =
            (ProfileComboBox.SelectedItem as LauncherProfile)?.Name;

        string? profileName =
            PromptForProfileName(defaultName);

        if (string.IsNullOrWhiteSpace(profileName))
        {
            return;
        }

        LauncherProfile profile =
            CreateCurrentProfile(profileName.Trim());

        LauncherProfile? existing =
            profiles.Find(profile.Name);

        if (existing != null)
        {
            MessageBoxResult result =
                System.Windows.MessageBox.Show(
                    $"A profile named {profile.Name} already exists. Do you want to replace it?",
                    "Save profile",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        profiles.AddOrReplace(profile);
        profiles.Save("TQLauncherTab1Profiles.json");

        LoadProfiles();

        ProfileComboBox.SelectedItem =
            ProfileComboBox.Items
                .OfType<LauncherProfile>()
                .FirstOrDefault(
                    item => string.Equals(
                        item.Name,
                        profile.Name,
                        StringComparison.OrdinalIgnoreCase));

        StatusText.Text =
            $"Profile {profile.Name} saved.";
    }

    private void DeleteProfileButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (ProfileComboBox.SelectedItem is not LauncherProfile profile)
        {
            return;
        }

        MessageBoxResult result =
            System.Windows.MessageBox.Show(
                $"Delete profile {profile.Name}?",
                "Delete profile",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        if (profiles.Remove(profile.Name))
        {
            profiles.Save("TQLauncherTab1Profiles.json");
            LoadProfiles();
            ProfileComboBox.SelectedIndex = -1;

            StatusText.Text =
                $"Profile {profile.Name} deleted.";
        }
    }

    private LauncherProfile CreateCurrentProfile(
        string name)
    {
        Resolution? selectedResolution =
            ResolutionComboBox.SelectedItem as Resolution;

        Engine? selectedEngine =
            EngineComboBox.SelectedItem as Engine;

        MissionPack? selectedMissionPack =
            MissionComboBox.SelectedItem as MissionPack;

        MapInfo? selectedMap =
            MapComboBox.SelectedItem as MapInfo;

        Difficulty? selectedDifficulty =
            DifficultyComboBox.SelectedItem as Difficulty;

        return new LauncherProfile
        {
            Name = name,
            QuakeFolder = QuakeFolderTextBox.Text.Trim(),
            EnginePath = selectedEngine?.ExecutablePath ?? "",
            EngineGame = selectedEngine?.Game ?? QuakeGame.Quake1,
            Resolution =
                selectedResolution != null &&
                !selectedResolution.IsDefault
                    ? selectedResolution.DisplayName
                    : "",
            MissionPackDirectory =
                selectedMissionPack?.GameDirectory ?? "",
            MapFileName =
                selectedMap?.FileName ?? "",
            Difficulty =
                selectedDifficulty != null &&
                selectedDifficulty != Difficulty.None
                    ? selectedDifficulty.Value
                    : null,
            Mode = null,
            ModeSelectionCleared = false,
            FragLimitEnabled = false,
            FlagLimitEnabled = false,
            TimeLimitEnabled = false,
            MaxPlayersEnabled = false,
            ExtraArguments = ExtraArgumentsTextBox.Text
        };
    }

    private void ApplyProfile(
        LauncherProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.QuakeFolder))
        {
            System.Windows.MessageBox.Show(
                "The selected profile does not contain a game folder.",
                "Load profile",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!Directory.Exists(profile.QuakeFolder))
        {
            System.Windows.MessageBox.Show(
                "The game folder stored in this profile was moved or deleted.",
                "Load profile",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        loadingProfiles = true;
        restoringSavedSelections = true;

        try
        {
            string quakeFolder =
                profile.QuakeFolder.Trim();

            QuakeFolderTextBox.Text =
                quakeFolder;

            lastAcceptedQuakeFolder =
                quakeFolder;

            restoreMapSelectionCleared = false;
            restoreDifficultySelectionCleared = false;

            DetectQuakeInstallation(quakeFolder);

            Engine? engine =
                EngineComboBox.Items
                    .OfType<Engine>()
                    .FirstOrDefault(
                        item =>
                            string.Equals(
                                item.ExecutablePath,
                                profile.EnginePath,
                                StringComparison.OrdinalIgnoreCase) &&
                            item.Game == profile.EngineGame);

            if (engine != null)
            {
                EngineComboBox.SelectedItem = engine;
                SetupResolutions();
            }

            DetectMissionPacks(quakeFolder);

            MissionPack? missionPack =
                MissionComboBox.Items
                    .OfType<MissionPack>()
                    .FirstOrDefault(
                        item =>
                            string.Equals(
                                item.GameDirectory,
                                profile.MissionPackDirectory,
                                StringComparison.OrdinalIgnoreCase));

            if (missionPack != null)
            {
                MissionComboBox.SelectedItem = missionPack;
            }

            Resolution? resolution =
                ResolutionComboBox.Items
                    .OfType<Resolution>()
                    .FirstOrDefault(
                        item =>
                            !item.IsDefault &&
                            string.Equals(
                                item.DisplayName,
                                profile.Resolution,
                                StringComparison.OrdinalIgnoreCase));

            ResolutionComboBox.SelectedItem =
                resolution ?? ResolutionComboBox.Items
                    .OfType<Resolution>()
                    .FirstOrDefault(item => item.IsDefault);

            if (string.IsNullOrWhiteSpace(profile.MapFileName))
            {
                MapComboBox.SelectedIndex = 0;
            }
            else if (string.Equals(
                profile.MapFileName,
                "?",
                StringComparison.Ordinal))
            {
                MapComboBox.SelectedIndex = 1;
            }
            else
            {
                MapInfo? map =
                    MapComboBox.Items
                        .OfType<MapInfo>()
                        .FirstOrDefault(
                            item =>
                                string.Equals(
                                    item.FileName,
                                    profile.MapFileName,
                                    StringComparison.OrdinalIgnoreCase));

                if (map != null)
                {
                    MapComboBox.SelectedItem = map;
                }
            }

            if (profile.Difficulty.HasValue)
            {
                Difficulty? difficulty =
                    DifficultyComboBox.Items
                        .OfType<Difficulty>()
                        .FirstOrDefault(
                            item =>
                                item.Value ==
                                profile.Difficulty.Value);

                DifficultyComboBox.SelectedItem =
                    difficulty ??
                    DifficultyComboBox.Items
                        .OfType<Difficulty>()
                        .FirstOrDefault(
                            item => item == Difficulty.None);
            }
            else
            {
                DifficultyComboBox.SelectedIndex = 0;
            }

            ExtraArgumentsTextBox.Text =
                profile.ExtraArguments ?? "";

            ClearResolutionButton.IsEnabled =
                ResolutionComboBox.SelectedItem is Resolution selectedResolution &&
                !selectedResolution.IsDefault;

            ClearMapButton.IsEnabled =
                !demoSelectionActive &&
                MapComboBox.SelectedIndex > 0;

            UpdateDifficultyControlsState();
            UpdateCommandArguments();

            // Status text for loading a custom singleplayer profile.
            StatusText.Text =
                $"Profile {profile.Name} loaded.";
        }
        finally
        {
            restoringSavedSelections = false;
            loadingProfiles = false;
        }
    }

    private static string? PromptForProfileName(
        string? defaultName)
    {
        System.Windows.Window dialog =
            new()
            {
                Title = "Save profile",
                Width = 360,
                Height = 150,
                WindowStartupLocation =
                    WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false
            };

        System.Windows.Controls.StackPanel panel =
            new()
            {
                Margin = new Thickness(12)
            };

        panel.Children.Add(
            new System.Windows.Controls.TextBlock
            {
                Text = "Profile name:",
                Margin = new Thickness(0, 0, 0, 6)
            });

        System.Windows.Controls.TextBox textBox =
            new()
            {
                Text = defaultName ?? "",
                Height = 26,
                VerticalContentAlignment =
                    VerticalAlignment.Center
            };

        panel.Children.Add(textBox);

        System.Windows.Controls.StackPanel buttons =
            new()
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

        System.Windows.Controls.Button cancelButton =
            new()
            {
                Content = "Cancel",
                Width = 75,
                Height = 26,
                Margin = new Thickness(0, 0, 6, 0)
            };

        System.Windows.Controls.Button saveButton =
            new()
            {
                Content = "Save",
                Width = 75,
                Height = 26,
                IsDefault = true
            };

        cancelButton.Click +=
            (_, _) =>
            {
                dialog.DialogResult = false;
                dialog.Close();
            };

        saveButton.Click +=
            (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(textBox.Text))
                {
                    System.Windows.MessageBox.Show(
                        dialog,
                        "Enter a profile name.",
                        "Save profile",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                dialog.DialogResult = true;
                dialog.Close();
            };

        buttons.Children.Add(cancelButton);
        buttons.Children.Add(saveButton);
        panel.Children.Add(buttons);

        dialog.Content = panel;
        dialog.Owner =
            System.Windows.Application.Current?.Windows
                .OfType<System.Windows.Window>()
                .FirstOrDefault(window =>
                    window is MainWindow);

        textBox.Focus();
        textBox.SelectAll();

        return dialog.ShowDialog() == true
            ? textBox.Text.Trim()
            : null;
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

            // Default difficulty is always set to Normal.
            if (DifficultyComboBox.Items.Count > 1)
            {
                DifficultyComboBox.SelectedIndex = 3;
            }

            UpdateDifficultyControlsState();
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
            DifficultyComboBox.SelectedIndex = 3;
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

    private void MissionComboBox_SizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        UpdateMissionToolTip();
    }

    private void UpdateMissionToolTip()
    {
        MissionPack? selectedMissionPack =
            MissionComboBox.SelectedItem as MissionPack;

        if (selectedMissionPack == null ||
            string.IsNullOrWhiteSpace(selectedMissionPack.Name) ||
            MissionComboBox.ActualWidth <= 0)
        {
            MissionComboBox.ToolTip = null;
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
                    MissionComboBox.FontFamily,
                    MissionComboBox.FontStyle,
                    MissionComboBox.FontWeight,
                    MissionComboBox.FontStretch),
                MissionComboBox.FontSize,
                System.Windows.Media.Brushes.Black,
                VisualTreeHelper.GetDpi(
                    MissionComboBox).PixelsPerDip);

        // Leave room for the ComboBox border, padding and drop-down arrow.
        double availableWidth =
            MissionComboBox.ActualWidth -
            MissionComboBox.Padding.Left -
            MissionComboBox.Padding.Right -
            35;

        if (formattedText.Width > availableWidth)
        {
            MissionComboBox.ToolTip = displayText;
        }
        else
        {
            MissionComboBox.ToolTip = null;
        }
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

    private void SetupResolutions()
    {
        ResolutionComboBox.Items.Clear();

        ResolutionComboBox.Items.Add(
            Resolution.Default);

        Engine? engine =
            EngineComboBox.SelectedItem as Engine;

        if (engine?.Game == QuakeGame.Quake2 ||
            engine?.Game == QuakeGame.Quake3)
        {
            System.Windows.Forms.Screen? primaryScreen =
                System.Windows.Forms.Screen.PrimaryScreen;

            if (primaryScreen is not null)
            {
                System.Drawing.Rectangle workingArea =
                    primaryScreen.WorkingArea;

                System.Drawing.Rectangle screenBounds =
                    primaryScreen.Bounds;

                // Show modes smaller than the primary screen's working area,
                // plus the exact current fullscreen resolution.
                IEnumerable<(int Mode, int Width, int Height)> videoModes =
                    engine.Game == QuakeGame.Quake2
                        ? GetQuake2VideoModes()
                        : GetQuake3VideoModes();

                foreach ((int mode, int width, int height) in
                         videoModes.Where(videoMode =>
                             (videoMode.Width < workingArea.Width &&
                              videoMode.Height < workingArea.Height) ||
                             (videoMode.Width == screenBounds.Width &&
                              videoMode.Height == screenBounds.Height)))
                {
                    ResolutionComboBox.Items.Add(
                        new Resolution(width, height, false));
                }
            }
        }
        else
        {
            foreach (Resolution resolution in
                     Resolution.GetAvailableResolutions())
            {
                ResolutionComboBox.Items.Add(resolution);
            }
        }

        ResolutionComboBox.SelectedIndex = 0;
        ClearResolutionButton.IsEnabled = false;
    }

    private static IEnumerable<(int Mode, int Width, int Height)>
        GetQuake2VideoModes()
    {
        // Quake 2 resolutions using a fixed r_mode list.
        return new[]
        {
            (1, 5120, 2880),
            (2, 3840, 2400),
            (3, 3840, 2160),
            (4, 3440, 1440),
            (5, 3200, 1800),
            (6, 2560, 1600),
            (7, 2560, 1440),
            (8, 2560, 1080),
            (9, 2048, 1536),
            (10, 1920, 1440),
            (11, 1920, 1200),
            (12, 1920, 1080),
            (13, 1680, 1050),
            (14, 1600, 1200),
            (15, 1600, 1024),
            (16, 1600, 900),
            (17, 1400, 1050),
            (18, 1440, 900),
            (19, 1366, 768),
            (20, 1360, 768),
            (21, 1280, 1024),
            (22, 1280, 960),
            (23, 1280, 800),
            (24, 1280, 768),
            (25, 1280, 720),
            (26, 1152, 864),
            (27, 1024, 768),
            (28, 1024, 600),
            (29, 960, 720),
            (30, 856, 480),
            (31, 800, 600),
            (32, 800, 480),
            (33, 640, 480)
        };
    }

    private static IEnumerable<(int Mode, int Width, int Height)>
        GetQuake3VideoModes()
    {
        // Quake 3 fixed resolution list.
        return new[]
        {
            (1, 5120, 2880),
            (2, 3840, 2400),
            (3, 3840, 2160),
            (4, 3440, 1440),
            (5, 3200, 1800),
            (6, 2560, 1600),
            (7, 2560, 1440),
            (8, 2560, 1080),
            (9, 2048, 1536),
            (10, 1920, 1440),
            (11, 1920, 1200),
            (12, 1920, 1080),
            (13, 1680, 1050),
            (14, 1600, 1200),
            (15, 1600, 1024),
            (16, 1600, 900),
            (17, 1400, 1050),
            (18, 1440, 900),
            (19, 1366, 768),
            (20, 1360, 768),
            (21, 1280, 1024),
            (22, 1280, 960),
            (23, 1280, 800),
            (24, 1280, 768),
            (25, 1280, 720),
            (26, 1152, 864),
            (27, 1024, 768),
            (28, 1024, 600),
            (29, 960, 720),
            (30, 856, 480),
            (31, 800, 600),
            (32, 800, 480),
            (33, 640, 480),
            (34, 512, 384),
            (35, 400, 300),
            (36, 320, 240)
        };
    }

    private void ResolutionComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        bool hasResolution =
            ResolutionComboBox.SelectedItem is Resolution resolution &&
            !resolution.IsDefault;

        ClearResolutionButton.IsEnabled =
            hasResolution;

        if (!restoringSavedSelections)
        {
            SaveCurrentSettings();
        }

        UpdateCommandArguments();
    }

    private void ClearResolutionButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ResolutionComboBox.SelectedIndex = 0;
        ClearResolutionButton.IsEnabled = false;

        StatusText.Text =
            "Cleared resolution selection.";

        SaveCurrentSettings();
        UpdateCommandArguments();
    }

    private List<string> BuildResolutionArguments()
    {
        Resolution? resolution =
            ResolutionComboBox.SelectedItem as Resolution;

        if (resolution == null ||
            resolution.IsDefault)
        {
            return new List<string>();
        }

        Engine? engine =
            EngineComboBox.SelectedItem as Engine;

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

        if (engine?.Game == QuakeGame.Quake3)
        {
            return new List<string>
            {
                "+seta",
                "r_mode",
                "-1",
                "+seta",
                "r_customwidth",
                resolution.Width.ToString(),
                "+seta",
                "r_customheight",
                resolution.Height.ToString()
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

        LauncherSettings settings =
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

        engines.AddRange(
            engineDetector3.DetectEngines(quakeFolder));

        foreach (Engine engine in engines)
        {
            EngineComboBox.Items.Add(engine);
        }

        if (EngineComboBox.Items.Count == 0)
        {
            // No engine: resolution must remain an empty selector.
            ResolutionComboBox.Items.Clear();
            ResolutionComboBox.SelectedIndex = -1;
            ClearResolutionButton.IsEnabled = false;

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
            RefreshEnginesButton.IsEnabled = false;
            RefreshEpisodesButton.IsEnabled = false;
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
                "No engine(s) detected inside selected folder(s).";

            System.Windows.MessageBox.Show(
                "No engine(s) detected inside selected folder(s).",
                "Singleplayer",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        // Select the first engine before building the resolution list so
        // the initial resolution set matches the selected engine.
        EngineComboBox.SelectedIndex = 0;
        RefreshEnginesButton.IsEnabled = true;

        // Rebuild resolution and difficulty selectors.
        SetupResolutions();
        SetupDifficultyOptions();
        RefreshEpisodesButton.IsEnabled = true;

        // Do not force a Demo foreground here. The Demo controls use the
        // same normal WPF enabled/disabled styling as Map and Difficulty.
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

        // Quakespasm and Quakespasm-Spiked may use the selected parent folder
        // as their game-data root. If that parent also contains separate
        // Quake installations, those installation folders are not episodes
        // for QS/QSS and must not appear in the drop-down.
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
            MissionComboBox.Items.Add(missionPack);
        }

        if (MissionComboBox.Items.Count > 0)
        {
            MissionComboBox.SelectedIndex = 0;

            // An episode was detected, so restore the normal difficulty
            // options and use Normal difficulty as the default.
            if (DifficultyComboBox.Items.Count > 3)
            {
                DifficultyComboBox.SelectedIndex = 3;
            }
            else
            {
                SetupDifficultyOptions();
            }

            StatusText.Text =
                $"Found {EngineComboBox.Items.Count} engine(s) and " +
                $"{MissionComboBox.Items.Count} episode(s).";
        }
        else
        {
            StatusText.Text =
                "No episode(s) detected inside selected folder(s).";

            // Apply the no-episode UI state before showing the warning so the
            // refresh button and difficulty selector are already empty
            // while the warning is being displayed.
            RefreshEpisodesButton.IsEnabled = false;
            ClearMapButton.IsEnabled = false;
            ClearDifficultyButton.IsEnabled = false;
            ClearDemoButton.IsEnabled = false;

            MapComboBox.Items.Clear();
            MapComboBox.SelectedIndex = -1;
            MapComboBox.ToolTip = null;

            DemoComboBox.Items.Clear();
            DemoComboBox.SelectedIndex = -1;
            DemoComboBox.ToolTip = null;

            DifficultyComboBox.Items.Clear();
            DifficultyComboBox.SelectedIndex = -1;
            DifficultyComboBox.IsEnabled = true;
            ClearDifficultyButton.IsEnabled = false;
            DifficultyLabel.Foreground =
                System.Windows.SystemColors.ControlTextBrush;
            UpdateDifficultyControlsState();
            UpdateCommandArguments();

            if (!suppressNoEpisodeWarning)
            {
                System.Windows.MessageBox.Show(
                    "No episode(s) detected inside selected folder(s).",
                    "Singleplayer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        if (MissionComboBox.Items.Count == 0)
        {
            MapComboBox.Items.Clear();
            MapComboBox.SelectedIndex = -1;
            MapComboBox.ToolTip = null;

            DemoComboBox.Items.Clear();
            DemoComboBox.SelectedIndex = -1;
            DemoComboBox.ToolTip = null;

            DifficultyComboBox.Items.Clear();
            DifficultyComboBox.SelectedIndex = -1;
            DifficultyComboBox.IsEnabled = true;
            ClearDifficultyButton.IsEnabled = false;
            DifficultyLabel.Foreground =
                System.Windows.SystemColors.ControlTextBrush;
            UpdateDifficultyControlsState();
            UpdateCommandArguments();
            return;
        }

        DetectMaps();
        DetectDemos();

        if (MissionComboBox.Items.Count == 0)
        {
            ClearMapButton.IsEnabled = false;
            ClearDifficultyButton.IsEnabled = false;
            ClearDemoButton.IsEnabled = false;
            RefreshEpisodesButton.IsEnabled = false;
        }
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
        // Do not add any value or select a synthetic "None" entry.
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
        MapComboBox.Items.Add(
            new MapInfo
            {
                FileName = "?",
                Title = "Random map",
                Foreground = System.Windows.Media.Brushes.Black
            });

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

        bool isQuake3 =
            engine.Game == QuakeGame.Quake3;

        List<MapInfo> maps;

        // Quake 2 map detector.
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
        // Quake map detector.
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

            MapComboBox.Items.Add(map);
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
                    defaultIndex = mapIndex + 2;
                }
            }

            if (defaultIndex > 0)
            {
                MapComboBox.SelectedIndex =
                    defaultIndex;
            }
            else if (MapComboBox.Items.Count > 2)
            {
                MapComboBox.SelectedIndex = 2;
            }
            else
            {
                MapComboBox.SelectedIndex = 0;
            }
        }

        UpdateMapToolTip();
        UpdateCommandArguments();
    }

    private bool IsQuake3Game()
    {
        Engine? engine =
            EngineComboBox.SelectedItem as Engine;

        return engine?.Game == QuakeGame.Quake3;
    }

    private void UpdateDifficultyControlsState()
    {
        // Quake 3 difficulty and demo selection disable the difficulty
        // selector. When an engine is present but no episode detected,
        // keep the difficulty selector available but empty.
        bool noEpisodes =
            MissionComboBox.Items.Count == 0;

        bool disabled =
            IsQuake3Game() ||
            demoSelectionActive;

        if (IsQuake3Game() &&
            DifficultyComboBox.SelectedIndex != 0)
        {
            DifficultyComboBox.SelectedIndex = 0;
        }

        DifficultyComboBox.IsEnabled =
            noEpisodes || !disabled;
        ClearDifficultyButton.IsEnabled =
            !noEpisodes &&
            !disabled &&
            DifficultyComboBox.SelectedIndex > 0;

        DifficultyLabel.Foreground =
            disabled && !noEpisodes
                ? System.Windows.Media.Brushes.DarkGray
                : System.Windows.SystemColors.ControlTextBrush;
    }

    private void DetectDemos()
    {
        demoSelectionActive = false;
        MapComboBox.IsEnabled = true;

        DemoComboBox.Items.Clear();
        DemoComboBox.SelectedIndex = -1;

        MissionPack? missionPack =
            MissionComboBox.SelectedItem as MissionPack;

        Engine? engine =
            EngineComboBox.SelectedItem as Engine;

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
            // A root-level PK3/ZIP mission pack has no physical episode
            // directory. Fall back to the resolved engine game root.
            if (string.IsNullOrWhiteSpace(missionPack.GameDirectory))
            {
                gameFolder =
                    GetEngineGameFolder(
                        engine,
                        QuakeFolderTextBox.Text.Trim());
            }
            else
            {
                DemoComboBox.SelectedIndex = 0;
                UpdateDemoControlsState();
                return;
            }
        }

        List<Demo> demos =
            engine.Game == QuakeGame.Quake2
                ? quake2Handler.DetectDemosForEpisode(
                    gameFolder,
                    engine,
                    missionPack)
                : engine.Game == QuakeGame.Quake3
                    ? demoDetector3.DetectDemos(gameFolder)
                    : quakeHandler.DetectQuake1Demos(gameFolder);

        // A Quake 2 demo may be returned by another detector with its name
        // already formatted as "filename | title". Do not format that
        // string again. Demo model's MapTitle is the title field.
        foreach (Demo demo in demos)
        {
            if (!string.IsNullOrWhiteSpace(demo.FileName))
            {
                string demoFileTitle =
                    RemoveQuake3DemoExtension(
                        demo.FileName);

                string title =
                    !string.IsNullOrWhiteSpace(demo.MapTitle)
                        ? engine.Game == QuakeGame.Quake1
                            ? RemoveQuake1MapExtension(
                                demo.MapTitle)
                            : RemoveQuake3DemoExtension(
                                demo.MapTitle)
                        : demoFileTitle;

                // Quake 3 demo titles are always capitalized.
                title =
                    CapitalizeFirstLetter(
                        title);

                // Keep the complete filename, including its extension, before
                // the separator. Only the demo title after "|" hides it.
                demo.Name =
                    $"{demo.FileName} | {title}";
            }

            DemoComboBox.Items.Add(demo);
        }

        DemoComboBox.SelectedIndex = 0;
        UpdateDemoControlsState();
        UpdateDemoToolTip();
        UpdateCommandArguments();
    }

    private static string RemoveQuake1MapExtension(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        string fileName =
            Path.GetFileName(value);

        if (fileName.EndsWith(
                ".bsp",
                StringComparison.OrdinalIgnoreCase))
        {
            return fileName[..^4];
        }

        return fileName;
    }

    private static string RemoveQuake3DemoExtension(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        string fileName =
            Path.GetFileName(value);

        string[] extensions =
        {
            ".dm3",
            ".dm_48",
            ".dm_66",
            ".dm_68"
        };

        foreach (string extension in extensions)
        {
            if (fileName.EndsWith(
                    extension,
                    StringComparison.OrdinalIgnoreCase))
            {
                return fileName[..^extension.Length];
            }
        }

        return fileName;
    }

    private static string LowercaseFirstLetter(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return char.ToLowerInvariant(value[0]) +
               value[1..];
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
                "No engine(s) detected inside selected folder(s).";
        }


        // Refreshing engines can leave the same engine selected so the selection-changed
        // event may not fire. Re-detect episodes explicitly so the episode refresh
        // button and empty difficulty state stay in sync.
        if (EngineComboBox.Items.Count > 0)
        {
            DetectMissionPacks(quakeFolder);
            RefreshEpisodesButton.IsEnabled =
                MissionComboBox.Items.Count > 0;
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
                "No episode(s) detected inside selected folder(s).";
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

        // Resolution modes are engine-specific. Rebuild the selector whenever
        // the selected engine changes so Quake 2 does not show Quake modes.
        SetupResolutions();

        if (!restoringSavedSelections)
        {
            DetectMissionPacks(
                QuakeFolderTextBox.Text.Trim());
        }
        else
        {
            UpdateCommandArguments();
        }

        UpdateDifficultyControlsState();
    }

    private void MissionComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        UpdateMissionToolTip();

        if (MissionComboBox.SelectedItem is not MissionPack)
        {
            ClearMapButton.IsEnabled = false;
            ClearDifficultyButton.IsEnabled = false;
            ClearDemoButton.IsEnabled = false;

            MapComboBox.Items.Clear();
            MapComboBox.SelectedIndex = -1;

            DemoComboBox.Items.Clear();
            DemoComboBox.SelectedIndex = -1;

            DifficultyComboBox.Items.Clear();
            DifficultyComboBox.SelectedIndex = -1;
            ClearDifficultyButton.IsEnabled = false;
            UpdateDifficultyControlsState();
            UpdateCommandArguments();
            return;
        }

        DetectMaps();
        DetectDemos();

        if (!restoreMapSelectionCleared &&
            !restoreDifficultySelectionCleared &&
            DifficultyComboBox.Items.Count > 3)
        {
            DifficultyComboBox.SelectedIndex = 3;
        }

        UpdateDifficultyControlsState();
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

        UpdateMapSelectionVisual();

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
                Name = "Random",
                Value = -2,
                Foreground = System.Windows.Media.Brushes.Black
            });

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

        // Normal is the default difficulty whenever a supported engine is available.
        DifficultyComboBox.SelectedIndex = 3;
        UpdateDifficultyControlsState();
    }

    private void DifficultyComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        UpdateDifficultyControlsState();
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

        ClearMapButton.IsEnabled =
            !demoSelectionActive &&
            MapComboBox.SelectedIndex > 0;

        MapLabel.Foreground =
            demoSelectionActive
                ? System.Windows.Media.Brushes.DarkGray
                : System.Windows.SystemColors.ControlTextBrush;

        UpdateMapSelectionVisual();

        UpdateDifficultyControlsState();

        if (!restoringSavedSelections &&
            DemoComboBox.SelectedItem is Demo)
        {
            SaveCurrentSettings();
        }

        UpdateDemoToolTip();
        UpdateCommandArguments();
    }

    private void UpdateMapSelectionVisual()
    {
        MapComboBox.ApplyTemplate();

        ContentPresenter? contentPresenter =
            FindVisualChild<ContentPresenter>(MapComboBox);

        if (contentPresenter == null)
        {
            return;
        }

        TextBlock? textBlock =
            FindVisualChild<TextBlock>(contentPresenter);

        if (textBlock == null)
        {
            return;
        }

        textBlock.Foreground =
            MapComboBox.IsEnabled
                ? (MapComboBox.SelectedItem is MapInfo selectedMap
                    ? selectedMap.Foreground
                    : System.Windows.SystemColors.ControlTextBrush)
                : System.Windows.Media.Brushes.DarkGray;
    }

    private static T? FindVisualChild<T>(
        DependencyObject parent)
        where T : DependencyObject
    {
        for (int i = 0;
             i < VisualTreeHelper.GetChildrenCount(parent);
             i++)
        {
            DependencyObject child =
                VisualTreeHelper.GetChild(parent, i);

            if (child is T match)
            {
                return match;
            }

            T? descendant =
                FindVisualChild<T>(child);

            if (descendant != null)
            {
                return descendant;
            }
        }

        return null;
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

        // Re-evaluate all difficulty state after the demo is cleared.
        // Quake 3 difficulty remains disabled and set to None.
        ClearMapButton.IsEnabled =
            MapComboBox.SelectedIndex > 0;

        UpdateDemoControlsState();

        MapLabel.Foreground =
            System.Windows.SystemColors.ControlTextBrush;

        UpdateDifficultyControlsState();

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

        if (selectedMap == null ||
            string.IsNullOrWhiteSpace(selectedMap.FileName) ||
            string.Equals(
                selectedMap.FileName,
                "?",
                StringComparison.Ordinal))
        {
            System.Windows.MessageBox.Show(
                "Please select a valid map.",
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
                "The selected archive is not a valid PAK file.");
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
                    "The selected demo could not be read completely from the PAK file.");
            }

            File.WriteAllBytes(destination, data);
            return;
        }

        throw new FileNotFoundException(
            "The selected demo could not be found inside the PAK file.",
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
        else if (engine.Game == QuakeGame.Quake3)
        {
            arguments = BuildQuake3AutomaticLaunchArguments();
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

        arguments.AddRange(
            BuildResolutionArguments());

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

        // Quake difficulty.
        if (DifficultyComboBox.SelectedItem is Difficulty difficulty)
        {
            if (IsRandomDifficultySelected(difficulty))
            {
                arguments.Add("+skill");
                arguments.Add("?");
            }
            else if (difficulty.Value >= 0)
            {
                arguments.Add("+skill");
                arguments.Add(difficulty.Value.ToString());
            }
        }

        if (IsRandomMapSelected(selectedMap))
        {
            arguments.Add("+map");
            arguments.Add("?");
        }
        else if (!string.IsNullOrWhiteSpace(mapName))
        {
            arguments.Add("+map");
            arguments.Add(mapName);
        }

        return arguments;
    }

    private static bool IsRandomDifficultySelected(Difficulty? difficulty)
    {
        return difficulty != null && difficulty.Value == -2;
    }

    private static bool IsRandomMapSelected(MapInfo? map)
    {
        return map != null &&
            string.Equals(
                map.FileName,
                "?",
                StringComparison.Ordinal);
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

        // Quake 2 difficulty.
        if (DifficultyComboBox.SelectedItem is Difficulty difficulty)
        {
            if (IsRandomDifficultySelected(difficulty))
            {
                arguments.Add("+set");
                arguments.Add("skill");
                arguments.Add("?");
            }
            else if (difficulty.Value >= 0)
            {
                arguments.Add("+set");
                arguments.Add("skill");
                arguments.Add(difficulty.Value.ToString());
            }
        }

        if (IsRandomMapSelected(selectedMap))
        {
            arguments.Add("+map");
            arguments.Add("?");
        }
        else if (!string.IsNullOrWhiteSpace(mapName))
        {
            arguments.Add("+map");
            arguments.Add(mapName);
        }

        return arguments;
    }

    private List<string> BuildQuake3AutomaticLaunchArguments()
    {
        MissionPack? missionPack =
            MissionComboBox.SelectedItem as MissionPack;

        Demo? selectedDemo =
            GetSelectedDemo();

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

        if (selectedDemo != null)
        {
            arguments.Add("+demo");
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

        if (IsRandomMapSelected(selectedMap))
        {
            arguments.Add("+map");
            arguments.Add("?");
        }
        else if (!string.IsNullOrWhiteSpace(mapName))
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
                    : engine.Game == QuakeGame.Quake3
                        ? BuildQuake3AutomaticLaunchArguments()
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

        // Only manual extra arguments are always blue.
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

        System.Windows.Window dialog =
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

        System.Windows.Window dialog =
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

            // Keep the Random difficulty in the preview, but resolve it at launch.
            if (selectedDemo == null &&
                DifficultyComboBox.SelectedItem is Difficulty selectedDifficulty &&
                IsRandomDifficultySelected(selectedDifficulty))
            {
                int skillArgumentIndex =
                    arguments.FindIndex(argument =>
                        string.Equals(argument, "+skill", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(argument, "skill", StringComparison.OrdinalIgnoreCase));

                if (skillArgumentIndex >= 0 &&
                    skillArgumentIndex + 1 < arguments.Count &&
                    string.Equals(arguments[skillArgumentIndex + 1], "?", StringComparison.Ordinal))
                {
                    arguments[skillArgumentIndex + 1] =
                        new Random().Next(0, 4).ToString();
                }
            }

            // Resolve the Random map only for the actual launch. Keep the command
            // preview as "+map ?" and pass an actual detected map to the engine.
            if (selectedDemo == null &&
                IsRandomMapSelected(selectedMap))
            {
                List<MapInfo> availableMaps =
                    MapComboBox.Items
                        .OfType<MapInfo>()
                        .Where(map =>
                            !string.IsNullOrWhiteSpace(map.FileName) &&
                            !IsRandomMapSelected(map))
                        .ToList();

                if (availableMaps.Count == 0)
                {
                    throw new InvalidOperationException(
                        "No maps are available for the Random selection.");
                }

                MapInfo randomMap =
                    availableMaps[
                        new Random().Next(availableMaps.Count)];

                int mapArgumentIndex =
                    arguments.FindIndex(
                        argument => string.Equals(
                            argument,
                            "+map",
                            StringComparison.OrdinalIgnoreCase));

                if (mapArgumentIndex >= 0 &&
                    mapArgumentIndex + 1 < arguments.Count &&
                    string.Equals(
                        arguments[mapArgumentIndex + 1],
                        "?",
                        StringComparison.Ordinal))
                {
                    arguments[mapArgumentIndex + 1] =
                        Path.GetFileNameWithoutExtension(
                            randomMap.FileName);
                }
            }

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
                $"{engine.Name} is set to run with custom settings.";

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