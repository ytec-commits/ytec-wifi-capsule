using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Ytec.WifiCapsule.Core.Models;
using Ytec.WifiCapsule.Core.Services;
using Ytec.WifiCapsule.Windows;

namespace Ytec.WifiCapsule.App;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<SelectableProfileRow>
        _backupRows = new();
    private readonly ObservableCollection<SelectableProfileRow>
        _restoreRows = new();
    private readonly WifiCapsuleService _service;
    private readonly bool _officialKeyBuild;
#if UI_TEST_CAPTURE_WIDE || UI_TEST_CAPTURE_NARROW
    private readonly IWifiProfileStore _profileStore;
#endif
    private readonly bool _testMode;
    private WifiCapsuleDocument? _openedDocument;
    private bool _busy;
    private bool _loadingAdapters;

    public MainWindow(bool testMode)
    {
        InitializeComponent();
        _testMode = testMode;
        _officialKeyBuild = ApplicationWifiKey.IsOfficialBuild;
        IWifiProfileStore profileStore = testMode
            ? new DemoWifiProfileStore()
            : new NativeWifiProfileStore();
#if UI_TEST_CAPTURE_WIDE || UI_TEST_CAPTURE_NARROW
        _profileStore = profileStore;
#endif
        _service = new WifiCapsuleService(
            profileStore,
            ApplicationWifiKey.GetKey);
        BackupProfileList.ItemsSource = _backupRows;
        RestoreProfileList.ItemsSource = _restoreRows;
        TestModeBanner.Visibility = testMode
            ? Visibility.Visible
            : Visibility.Collapsed;
        DevelopmentKeyBanner.Visibility = !_officialKeyBuild
            ? Visibility.Visible
            : Visibility.Collapsed;
        UpdateDevelopmentKeyBanner();
        UpdateSelectionCounts();
    }

    private async void Window_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        ApplyResponsiveLayout();
        await RefreshAdaptersAsync();
    }

    private void Window_SizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout();
    }

    private void ApplyResponsiveLayout()
    {
        var compact = ActualWidth < 620;
        HeaderLogo.Visibility = compact
            ? Visibility.Collapsed
            : Visibility.Visible;
        HeaderSubtitle.Visibility = compact
            ? Visibility.Collapsed
            : Visibility.Visible;
        HeaderActions.Visibility = compact
            ? Visibility.Collapsed
            : Visibility.Visible;
        CompactActions.Visibility = compact
            ? Visibility.Visible
            : Visibility.Collapsed;
        Grid.SetColumn(BackupActionButton, compact ? 0 : 1);
        Grid.SetRow(BackupActionButton, compact ? 2 : 0);
        Grid.SetRowSpan(BackupActionButton, compact ? 1 : 2);
        BackupActionButton.Margin = compact
            ? new Thickness(0, 10, 0, 0)
            : new Thickness(14, 0, 0, 0);
        BackupActionButton.HorizontalAlignment = compact
            ? HorizontalAlignment.Left
            : HorizontalAlignment.Right;
        ConfigureSelectionHeader(
            BackupSelectionActions,
            compact);
        ConfigureSelectionHeader(
            RestoreSelectionActions,
            compact);
    }

    private static void ConfigureSelectionHeader(
        WrapPanel actions,
        bool compact)
    {
        Grid.SetRow(actions, compact ? 1 : 0);
        Grid.SetColumn(actions, compact ? 0 : 1);
        Grid.SetColumnSpan(actions, compact ? 2 : 1);
        actions.Margin = compact
            ? new Thickness(0, 8, 0, 0)
            : new Thickness(0);
        actions.HorizontalAlignment = compact
            ? HorizontalAlignment.Left
            : HorizontalAlignment.Right;
    }

    private void Window_Closing(
        object? sender,
        CancelEventArgs e)
    {
        if (_busy)
        {
            MessageBox.Show(
                UiLanguage.Text("ClosingBusy"),
                "Y-TEC Wi-Fi Capsule",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            e.Cancel = true;
            return;
        }

        _openedDocument?.Dispose();
        _openedDocument = null;
    }

    private async void RefreshAdapters_Click(
        object sender,
        RoutedEventArgs e)
    {
        await RefreshAdaptersAsync();
    }

    private async void SwitchLanguage_Click(
        object sender,
        RoutedEventArgs e)
    {
        var backupAdapterId =
            (BackupAdapterCombo.SelectedItem as WifiAdapterInfo)?.Id;
        var restoreAdapterId =
            (RestoreAdapterCombo.SelectedItem as WifiAdapterInfo)?.Id;
        var selectedBackupNames = new HashSet<string>(
            _backupRows
                .Where(row => row.IsSelected)
                .Select(row => row.Name),
            StringComparer.Ordinal);

        UiLanguage.Toggle();
        UpdateDevelopmentKeyBanner();
        UpdateOpenedBackupSummary();
        await RefreshAdaptersAsync(
            backupAdapterId,
            restoreAdapterId);
        foreach (var row in _backupRows)
        {
            row.IsSelected = selectedBackupNames.Contains(row.Name);
        }

        UpdateSelectionCounts();
    }

    private async void BackupAdapterCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!_loadingAdapters)
        {
            await RefreshBackupProfilesAsync();
        }
    }

    private async void RestoreAdapterCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!_loadingAdapters)
        {
            await UpdateRestoreStatesAsync();
        }
    }

    private void SelectAllBackup_Click(
        object sender,
        RoutedEventArgs e)
    {
        SetSelection(_backupRows, true);
    }

    private void ClearBackupSelection_Click(
        object sender,
        RoutedEventArgs e)
    {
        SetSelection(_backupRows, false);
    }

    private void SelectAllRestore_Click(
        object sender,
        RoutedEventArgs e)
    {
        SetSelection(_restoreRows, true);
    }

    private void ClearRestoreSelection_Click(
        object sender,
        RoutedEventArgs e)
    {
        SetSelection(_restoreRows, false);
    }

    private async void CreateBackup_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (BackupAdapterCombo.SelectedItem is not
            WifiAdapterInfo adapter)
        {
            ShowInformation(
                UiLanguage.Text("SelectAdapter"));
            return;
        }

        var selected = _backupRows
            .Where(row => row.IsSelected)
            .Select(row => row.Name)
            .ToArray();
        if (selected.Length == 0)
        {
            ShowInformation(
                UiLanguage.Text("SelectBackupProfiles"));
            return;
        }

        var confirmation = MessageBox.Show(
            UiLanguage.Format(
                "BackupConfirmation",
                selected.Length),
            UiLanguage.Text("BackupConfirmationTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = UiLanguage.Text("BackupSaveTitle"),
            Filter = UiLanguage.Text("BackupFileFilter"),
            DefaultExt = WifiCapsuleService.BackupExtension,
            AddExtension = true,
            OverwritePrompt = false,
            CheckPathExists = true,
            FileName =
                $"Y-TEC-WiFi-Capsule_{DateTime.Now:yyyyMMdd_HHmmss}.ywcwifi",
        };
        if (dialog.ShowDialog(this) != true)
        {
            SetStatus(UiLanguage.Text("BackupCanceled"));
            return;
        }

        await RunBusyAsync(
            UiLanguage.Text("BackupBusy"),
            async () =>
            {
                var result = await Task.Run(
                    () => _service.CreateBackup(
                        adapter.Id,
                        selected,
                        dialog.FileName));
                SetStatus(
                    UiLanguage.Format(
                        "BackupCompletedStatus",
                        result.ProfileCount));
                MessageBox.Show(
                    UiLanguage.Format(
                        "BackupCompletedMessage",
                        result.ProfileCount,
                        Path.GetFileName(result.OutputPath)),
                    UiLanguage.Text("BackupCompletedTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            });
    }

    private async void OpenBackup_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = UiLanguage.Text("OpenBackupTitle"),
            Filter = UiLanguage.Text("BackupFileFilter"),
            DefaultExt = WifiCapsuleService.BackupExtension,
            CheckFileExists = true,
            Multiselect = false,
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        await RunBusyAsync(
            UiLanguage.Text("OpenBackupBusy"),
            async () =>
            {
                var document = await Task.Run(
                    () => _service.OpenBackup(dialog.FileName));
                _openedDocument?.Dispose();
                _openedDocument = document;
                ReplaceRows(
                    _restoreRows,
                    document.Profiles.Select(
                        profile => new SelectableProfileRow(
                            profile.Name,
                            UiLanguage.Text("Restorable"))));
                OpenedBackupText.Text =
                    Path.GetFileName(dialog.FileName);
                UpdateOpenedBackupSummary();
                await UpdateRestoreStatesAsync();
                SetStatus(
                    UiLanguage.Format(
                        "OpenedBackupStatus",
                        document.Profiles.Count));
            });
    }

    private async void RestoreProfiles_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_openedDocument is null)
        {
            ShowInformation(
                UiLanguage.Text("SelectBackupFile"));
            return;
        }

        if (RestoreAdapterCombo.SelectedItem is not
            WifiAdapterInfo adapter)
        {
            ShowInformation(
                UiLanguage.Text("SelectRestoreAdapter"));
            return;
        }

        var selected = _restoreRows
            .Where(row => row.IsSelected)
            .Select(row => row.Name)
            .ToArray();
        if (selected.Length == 0)
        {
            ShowInformation(
                UiLanguage.Text("SelectRestoreProfiles"));
            return;
        }

        var overwrite = OverwriteExistingCheckBox.IsChecked == true;
        var warning = overwrite
            ? UiLanguage.Text("RestoreOverwriteWarning")
            : UiLanguage.Text("RestoreSkipWarning");
        var confirmation = MessageBox.Show(
            UiLanguage.Format(
                "RestoreConfirmation",
                selected.Length,
                warning),
            UiLanguage.Text("RestoreConfirmationTitle"),
            MessageBoxButton.YesNo,
            overwrite
                ? MessageBoxImage.Warning
                : MessageBoxImage.Question,
            MessageBoxResult.No);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        await RunBusyAsync(
            UiLanguage.Text("RestoreBusy"),
            async () =>
            {
                var result = await Task.Run(
                    () => _service.Restore(
                        adapter.Id,
                        _openedDocument,
                        selected,
                        overwrite));
                await RefreshBackupProfilesAsync();
                await UpdateRestoreStatesAsync();
                SetStatus(
                    UiLanguage.Format(
                        "RestoreCompletedStatus",
                        result.RestoredProfiles,
                        result.SkippedProfiles,
                        result.FailedProfiles));
                MessageBox.Show(
                    UiLanguage.Format(
                        "RestoreCompletedMessage",
                        result.RestoredProfiles,
                        result.SkippedProfiles,
                        result.FailedProfiles),
                    UiLanguage.Text("RestoreCompletedTitle"),
                    MessageBoxButton.OK,
                    result.FailedProfiles == 0
                        ? MessageBoxImage.Information
                        : MessageBoxImage.Warning);
            });
    }

    private void OpenManual_Click(
        object sender,
        RoutedEventArgs e)
    {
        var manualPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            UiLanguage.IsJapanese
                ? "操作マニュアル"
                : "User Manual",
            "index.html");
        if (!File.Exists(manualPath))
        {
            ShowInformation(
                UiLanguage.Text("ManualMissing"));
            return;
        }

        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = manualPath,
                    UseShellExecute = true,
                });
        }
        catch (Win32Exception)
        {
            ShowInformation(
                UiLanguage.Text("ManualOpenFailed"));
        }
    }

    private void ShowAbout_Click(
        object sender,
        RoutedEventArgs e)
    {
        MessageBox.Show(
            "Y-TEC Wi-Fi Capsule 1.1.0\n\n" +
            UiLanguage.Text("AboutPlatform") + "\n" +
            "AES-256-CBC + HMAC-SHA-256\n\n" +
            (_officialKeyBuild
                ? UiLanguage.Text("AboutOfficialKey") + "\n"
                : ApplicationWifiKey.IsCustomKeyBuild
                    ? UiLanguage.Text("AboutCustomKey") + "\n"
                    : UiLanguage.Text("AboutPublicKey") + "\n") +
            UiLanguage.Text("AboutSecurity") + "\n\n" +
            "Copyright © 2026 Y-TEC",
            UiLanguage.Text("AboutTitle"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async Task RefreshAdaptersAsync(
        Guid? preferredBackupAdapterId = null,
        Guid? preferredRestoreAdapterId = null)
    {
        await RunBusyAsync(
            UiLanguage.Text("CheckingAdapters"),
            async () =>
            {
                var adapters = await Task.Run(
                    () => _service.GetAdapters());
                var localizedAdapters = adapters
                    .Select(LocalizeAdapter)
                    .ToArray();
                _loadingAdapters = true;
                try
                {
                    BackupAdapterCombo.ItemsSource = localizedAdapters;
                    RestoreAdapterCombo.ItemsSource = localizedAdapters;
                    var preferred = localizedAdapters.FirstOrDefault(
                            adapter => adapter.IsConnected)
                        ?? localizedAdapters.FirstOrDefault();
                    BackupAdapterCombo.SelectedItem =
                        localizedAdapters.FirstOrDefault(
                            adapter => adapter.Id ==
                                preferredBackupAdapterId)
                        ?? preferred;
                    RestoreAdapterCombo.SelectedItem =
                        localizedAdapters.FirstOrDefault(
                            adapter => adapter.Id ==
                                preferredRestoreAdapterId)
                        ?? preferred;
                }
                finally
                {
                    _loadingAdapters = false;
                }

                if (adapters.Count == 0)
                {
                    ReplaceRows(
                        _backupRows,
                        Array.Empty<SelectableProfileRow>());
                    SetStatus(
                        UiLanguage.Text("NoAdapters"));
                    return;
                }

                await RefreshBackupProfilesAsync();
                await UpdateRestoreStatesAsync();
            });
    }

    private async Task RefreshBackupProfilesAsync()
    {
        if (BackupAdapterCombo.SelectedItem is not
            WifiAdapterInfo adapter)
        {
            ReplaceRows(
                _backupRows,
                Array.Empty<SelectableProfileRow>());
            return;
        }

        var profiles = await Task.Run(
            () => _service.GetProfiles(adapter.Id));
        ReplaceRows(
            _backupRows,
            profiles.Select(
                profile => new SelectableProfileRow(
                    profile.Name,
                    profile.IsGroupPolicy
                        ? UiLanguage.Text("GroupPolicy")
                        : profile.IsCurrentUser
                            ? UiLanguage.Text("CurrentUser")
                            : UiLanguage.Text("AllUsers"))));
        SetStatus(
            profiles.Count == 0
                ? UiLanguage.Text("NoStoredProfiles")
                : UiLanguage.Format(
                    "StoredProfilesFound",
                    profiles.Count));
    }

    private async Task UpdateRestoreStatesAsync()
    {
        if (_restoreRows.Count == 0 ||
            RestoreAdapterCombo.SelectedItem is not
                WifiAdapterInfo adapter)
        {
            return;
        }

        var currentProfiles = await Task.Run(
            () => _service.GetProfiles(adapter.Id));
        var currentNames = new HashSet<string>(
            currentProfiles.Select(profile => profile.Name),
            StringComparer.Ordinal);
        foreach (var row in _restoreRows)
        {
            row.Detail = currentNames.Contains(row.Name)
                ? UiLanguage.Text("AlreadyRegistered")
                : UiLanguage.Text("NewProfile");
        }
    }

    private async Task RunBusyAsync(
        string message,
        Func<Task> operation)
    {
        if (_busy)
        {
            return;
        }

        SetBusy(true, message);
        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            var userMessage = GetUserMessage(exception);
            SetStatus(userMessage);
            MessageBox.Show(
                userMessage,
                "Y-TEC Wi-Fi Capsule",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false, StatusText.Text);
        }
    }

    private void SetBusy(bool busy, string message)
    {
        _busy = busy;
        MainTabs.IsEnabled = !busy;
        HeaderActions.IsEnabled = !busy;
        CompactActions.IsEnabled = !busy;
        BusyProgress.Visibility = busy
            ? Visibility.Visible
            : Visibility.Collapsed;
        BusyProgress.IsIndeterminate = busy;
        SetStatus(message);
    }

    private void ReplaceRows(
        ObservableCollection<SelectableProfileRow> target,
        IEnumerable<SelectableProfileRow> rows)
    {
        foreach (var existing in target)
        {
            existing.PropertyChanged -= ProfileRow_PropertyChanged;
        }

        target.Clear();
        foreach (var row in rows)
        {
            row.PropertyChanged += ProfileRow_PropertyChanged;
            target.Add(row);
        }

        UpdateSelectionCounts();
    }

    private void SetSelection(
        IEnumerable<SelectableProfileRow> rows,
        bool selected)
    {
        foreach (var row in rows)
        {
            row.IsSelected = selected;
        }

        UpdateSelectionCounts();
    }

    private void ProfileRow_PropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(
                SelectableProfileRow.IsSelected))
        {
            UpdateSelectionCounts();
        }
    }

    private void UpdateSelectionCounts()
    {
        BackupSelectionCountText.Text =
            UiLanguage.Format(
                "SelectionCount",
                _backupRows.Count(row => row.IsSelected));
        RestoreSelectionCountText.Text =
            UiLanguage.Format(
                "SelectionCount",
                _restoreRows.Count(row => row.IsSelected));
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    private void ShowInformation(string message)
    {
        SetStatus(message);
        MessageBox.Show(
            message,
            "Y-TEC Wi-Fi Capsule",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static string GetUserMessage(Exception exception)
    {
        if (exception is AggregateException aggregate &&
            aggregate.InnerExceptions.Count == 1)
        {
            return GetUserMessage(aggregate.InnerExceptions[0]);
        }

        if (UiLanguage.IsJapanese &&
            (exception is InvalidDataException ||
             exception is InvalidOperationException ||
             exception is ArgumentException))
        {
            return exception.Message;
        }

        if (exception is InvalidDataException)
        {
            return UiLanguage.Text("InvalidDataError");
        }

        if (exception is InvalidOperationException)
        {
            return UiLanguage.Text("InvalidOperationError");
        }

        if (exception is ArgumentException)
        {
            return UiLanguage.Text("ArgumentError");
        }

        if (exception is UnauthorizedAccessException)
        {
            return UiLanguage.Text("UnauthorizedError");
        }

        if (exception is IOException)
        {
            return UiLanguage.Text("IoError");
        }

        return UiLanguage.Text("GenericError");
    }

    private void UpdateDevelopmentKeyBanner()
    {
        DevelopmentKeyBannerText.Text =
            ApplicationWifiKey.IsCustomKeyBuild
                ? UiLanguage.Text("DevelopmentCustomWarning")
                : UiLanguage.Text("DevelopmentPublicWarning");
    }

    private void UpdateOpenedBackupSummary()
    {
        if (_openedDocument is null)
        {
            OpenedBackupText.Text = UiLanguage.Text("NotSelected");
            OpenedBackupSummaryText.Text = string.Empty;
            return;
        }

        OpenedBackupSummaryText.Text = UiLanguage.Format(
            "OpenedBackupSummary",
            UiLanguage.FormatDate(_openedDocument.CreatedAt),
            _openedDocument.Profiles.Count);
    }

    private static WifiAdapterInfo LocalizeAdapter(
        WifiAdapterInfo adapter)
    {
        var name = string.Equals(
            adapter.Name,
            "合成 Wi-Fi アダプター",
            StringComparison.Ordinal)
            ? UiLanguage.Text("DemoAdapterName")
            : adapter.Name;
        var description = adapter.Description switch
        {
            "画面確認用・実データなし" =>
                UiLanguage.Text("DemoAdapterDescription"),
            "接続中" => UiLanguage.Text("AdapterConnected"),
            "未接続" => UiLanguage.Text("AdapterDisconnected"),
            "準備中" => UiLanguage.Text("AdapterNotReady"),
            "利用可能" => UiLanguage.Text("AdapterAvailable"),
            _ => adapter.Description,
        };
        return new WifiAdapterInfo(
            adapter.Id,
            name,
            description,
            adapter.IsConnected);
    }

#if UI_TEST_CAPTURE_WIDE || UI_TEST_CAPTURE_NARROW
    internal void CaptureToPng(string outputPath)
    {
        UpdateLayout();
        var width = Math.Max(
            1,
            (int)Math.Ceiling(ActualWidth));
        var height = Math.Max(
            1,
            (int)Math.Ceiling(ActualHeight));
        var warmup = new RenderTargetBitmap(
            width,
            height,
            96,
            96,
            PixelFormats.Pbgra32);
        warmup.Render(this);
        Dispatcher.Invoke(
            System.Windows.Threading.DispatcherPriority.Render,
            new Action(() => { }));
        var bitmap = new RenderTargetBitmap(
            width,
            height,
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(this);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new FileStream(
            outputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);
        encoder.Save(stream);
    }
#endif

#if UI_TEST_CAPTURE_WIDE || UI_TEST_CAPTURE_NARROW
    internal async Task PrepareRestoreCaptureAsync()
    {
        if (RestoreAdapterCombo.SelectedItem is not
            WifiAdapterInfo adapter)
        {
            throw new InvalidOperationException(
                UiLanguage.Text("CaptureNoAdapter"));
        }

        var profileNames = _service
            .GetProfiles(adapter.Id)
            .Select(profile => profile.Name)
            .ToArray();
        var documents = new List<WifiProfileDocument>();
        try
        {
            foreach (var name in profileNames)
            {
                var xml = _profileStore.ExportProfile(
                    adapter.Id,
                    name);
                try
                {
                    documents.Add(
                        new WifiProfileDocument(name, xml));
                }
                finally
                {
                    for (var index = 0;
                         index < xml.Length;
                         index++)
                    {
                        xml[index] = 0;
                    }
                }
            }

            _openedDocument?.Dispose();
            _openedDocument = new WifiCapsuleDocument(
                new DateTimeOffset(
                    2026,
                    7,
                    28,
                    12,
                    0,
                    0,
                    TimeSpan.FromHours(9)),
                documents);
            documents = new List<WifiProfileDocument>();
            ReplaceRows(
                _restoreRows,
                _openedDocument.Profiles.Select(
                    profile => new SelectableProfileRow(
                        profile.Name,
                        UiLanguage.Text("Restorable"))));
            if (_restoreRows.Count > 0)
            {
                _restoreRows[0].IsSelected = true;
            }

            if (_restoreRows.Count > 2)
            {
                _restoreRows[2].IsSelected = true;
            }

            OpenedBackupText.Text =
                "Y-TEC-WiFi-Capsule_SAMPLE.ywcwifi";
            UpdateOpenedBackupSummary();
            MainTabs.SelectedIndex = 1;
            await UpdateRestoreStatesAsync();
            SetStatus(
                UiLanguage.Text("SyntheticOpenedStatus"));
        }
        finally
        {
            foreach (var document in documents)
            {
                document.Dispose();
            }
        }
    }
#endif
}
