using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WafeControl.Core.Demo;
using WafeControl.Core.Diagnostics;
using WafeControl.Core.Localization;
using WafeControl.Core.Services;
using WafeControl.Core.Threading;
using WafeControl.Core.ViewModels;
using WafeControl.Core.ViewModels.Schedule;
using WafeControl.WinUI.Helpers;
using Serilog;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;

namespace WafeControl.WinUI;

public sealed partial class MainWindow : Window
{
    private static readonly string IconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app-icon.ico");

    private readonly ILocalizationService _localization;
    private readonly ISettingsStore _settingsStore;
    private readonly Queue<Func<Task>> _notices = new();
    private bool _isExiting;
    private bool _isShowingNotices;
    private bool _crashNoticeQueued;
    private bool _consentAsked;

    /// <summary>
    /// The window's last bounds while neither maximized nor minimized, saved on close.
    /// </summary>
    private RectInt32 _restoredBounds;

    /// <summary>
    /// The window was maximized when it last closed; applied the first time it is shown.
    /// </summary>
    private bool _maximizeOnShow;

    public MainWindow(
        AppViewModel viewModel,
        ScheduleViewModel schedule,
        SettingsViewModel settings,
        UpdateViewModel updates,
        DemoWafeApi demo,
        ILocalizationService localization,
        ISettingsStore settingsStore)
    {
        ViewModel = viewModel;
        Schedule = schedule;
        Settings = settings;
        Updates = updates;
        _localization = localization;
        _settingsStore = settingsStore;
        ShowWindowCommand = new RelayCommand(ShowFromTray);
        ExitCommand = new RelayCommand(Exit);

        InitializeComponent();

        LoginView.ViewModel = viewModel;
        DashboardView.ViewModel = viewModel;
        DashboardView.Schedule = schedule;
        ScheduleView.ViewModel = schedule;
        ScheduleView.Main = viewModel;
        SettingsView.ViewModel = settings;
        SettingsView.Main = viewModel;
        SettingsView.Updates = updates;
        SettingsView.Demo = demo;
        SettingsView.ReportProblemRequested += (_, _) => ShowReportProblem();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(IconPath);
        ConfigureSize();
        ApplyTheme();

        TrayIcon.Icon = new System.Drawing.Icon(IconPath);
        AppWindow.Closing += OnClosing;
        AppWindow.Changed += OnAppWindowChanged;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        Settings.PropertyChanged += OnSettingsPropertyChanged;
        Updates.PropertyChanged += OnUpdatesPropertyChanged;
        Updates.RestartRequested += OnRestartRequested;
        _localization.LanguageChanged += OnLanguageChanged;
        Root.Loaded += (_, _) => ShowNotices();
    }

    public AppViewModel ViewModel { get; }

    public ScheduleViewModel Schedule { get; }

    public SettingsViewModel Settings { get; }

    public UpdateViewModel Updates { get; }

    public ICommand ShowWindowCommand { get; }

    public ICommand ExitCommand { get; }

    /// <summary>
    /// Launches with the window shown, maximized if it was when it last closed.
    /// Call instead of <see cref="Window.Activate"/>.
    /// </summary>
    public void ShowAtLaunch()
    {
        ApplyPendingMaximize();
        Activate();
    }

    /// <summary>
    /// Launches with only the tray icon: the window is never shown until <see cref="ShowFromTray"/>.
    /// Call instead of <see cref="Window.Activate"/>.
    /// </summary>
    public void StartInTray()
    {
        // The window's bindings normally start when it loads, which a hidden window never does;
        // the tray menu needs them now.
        Bindings.Update();
        TrayIcon.ForceCreate(enablesEfficiencyMode: true);
    }

    /// <summary>
    /// Restores the window from the tray (or from minimized) and brings it to the front.
    /// </summary>
    public void ShowFromTray()
    {
        ApplyPendingMaximize();
        WindowExtensions.Show(this, true);
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
            presenter.Restore();
        Activate();
        ShowNotices();
    }

    // ── Notices: dialogs shown one at a time, once the window is visible ──

    /// <summary>
    /// "Closed unexpectedly last time" for a crash of the previous session. Shown once.
    /// </summary>
    public void ShowStartupNotices(CrashRecord? crash)
    {
        if (crash is null || _crashNoticeQueued)
            return;

        _crashNoticeQueued = true;
        EnqueueNotice(() => ShowCrashDialogAsync(crash));
    }

    /// <summary>
    /// "Updated to 1.3.0." in the footer after an update.
    /// </summary>
    public void ShowUpdatedNotice()
    {
        if (Updates.TakeUpdatedVersion() is { } version)
            ViewModel.Feedback = Feedback.Success(string.Format(Strings.UpdatedTo, version));
    }

    private void EnqueueNotice(Func<Task> notice)
    {
        _notices.Enqueue(notice);
        ShowNotices();
    }

    // Dialogs need the window's content on screen; a window hidden in the tray shows them when it opens.
    private void ShowNotices()
    {
        if (_isShowingNotices || _notices.Count == 0 || Root.XamlRoot is null || !AppWindow.IsVisible)
            return;

        SafeAsync.Run(async () =>
        {
            _isShowingNotices = true;
            try
            {
                while (_notices.TryDequeue(out var notice))
                    await notice();
            }
            finally
            {
                _isShowingNotices = false;
            }
        });
    }

    private async Task ShowCrashDialogAsync(CrashRecord crash)
    {
        var details = new StackPanel { Spacing = 8 };
        details.Children.Add(new TextBlock { Text = Strings.CrashDialogMessage, TextWrapping = TextWrapping.Wrap });
        if (crash.ReportId is not null)
            details.Children.Add(new TextBlock { Text = Strings.CrashDialogReportSent, TextWrapping = TextWrapping.Wrap });
        details.Children.Add(new TextBlock
        {
            Text = $"{crash.ExceptionType} · {crash.Source}",
            Style = (Style)Application.Current.Resources["SecondaryTextStyle"],
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        });

        var dialog = CreateDialog(Strings.CrashDialogTitle, details);
        dialog.PrimaryButtonText = Strings.ReportProblem;
        dialog.CloseButtonText = Strings.ButtonClose;
        dialog.DefaultButton = ContentDialogButton.Primary;

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            await ShowReportProblemDialogAsync();
    }

    private async Task AskCrashReportConsentAsync()
    {
        if (!Settings.NeedsCrashReportConsent)
            return;

        var dialog = CreateDialog(Strings.CrashConsentTitle, new TextBlock { Text = Strings.CrashConsentMessage, TextWrapping = TextWrapping.Wrap });
        dialog.PrimaryButtonText = Strings.CrashConsentSend;
        dialog.CloseButtonText = Strings.CrashConsentDontSend;
        dialog.DefaultButton = ContentDialogButton.Primary;

        Settings.AnswerCrashReportConsent(await dialog.ShowAsync() == ContentDialogResult.Primary);
    }

    // ── Report a problem ─────────────────────────────────────────────────

    private void ShowReportProblem() => EnqueueNotice(ShowReportProblemDialogAsync);

    /// <summary>
    /// A GitHub issue with the diagnostics filled in, or the diagnostics to copy, plus the log folder to attach from.
    /// </summary>
    private async Task ShowReportProblemDialogAsync()
    {
        var report = ProblemReport.Create(DeviceDescription.Current, _localization.Current.Code, ViewModel.IsDemo,
            ViewModel.Unit?.Unit?.Model, CrashHandler.LastCrash);

        var copied = new TextBlock
        {
            Text = Strings.ReportCopied,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorSuccessBrush"],
            Visibility = Visibility.Collapsed,
        };
        var logs = new HyperlinkButton { Content = Strings.MenuOpenLogFolder, Padding = new Thickness(0) };
        logs.Click += (_, _) => OpenLogFolder();

        var content = new StackPanel { Spacing = 12, MaxWidth = 460 };
        content.Children.Add(new TextBlock { Text = Strings.ReportDialogMessage, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new TextBox
        {
            Text = report.Text,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            FontSize = 12,
        });
        content.Children.Add(new TextBlock
        {
            Text = Strings.ReportLogsHint,
            Style = (Style)Application.Current.Resources["SecondaryTextStyle"],
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(logs);
        content.Children.Add(copied);

        var dialog = CreateDialog(Strings.ReportProblem, content);
        dialog.PrimaryButtonText = Strings.ReportOpenGitHub;
        dialog.SecondaryButtonText = Strings.ReportCopyDiagnostics;
        dialog.CloseButtonText = Strings.ButtonClose;
        dialog.DefaultButton = ContentDialogButton.Primary;
        dialog.SecondaryButtonClick += (_, args) =>
        {
            // Stays open: the user may still want the issue or the log folder.
            args.Cancel = true;
            var package = new DataPackage();
            package.SetText(report.Text);
            Clipboard.SetContent(package);
            copied.Visibility = Visibility.Visible;
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            Process.Start(new ProcessStartInfo(report.IssueUrl.AbsoluteUri) { UseShellExecute = true });
            OpenLogFolder();
        }
    }

    private ContentDialog CreateDialog(string title, object content) => new()
    {
        XamlRoot = Root.XamlRoot,
        // Dialogs open outside the window's content, so they don't inherit a theme chosen in Settings.
        RequestedTheme = Root.ActualTheme,
        Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
        Title = title,
        Content = content,
    };

    private void OnReportProblemClick(object sender, RoutedEventArgs e) => ShowReportProblem();

    private void OnRetryClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Feedback?.Retry is { } retry)
            SafeAsync.Run(() => retry.ExecuteAsync(null));
    }

    // ── Updates ──────────────────────────────────────────────────────────

    /// <summary>
    /// "Restart to update": save everything, remove the tray icon, then let the updater swap versions and start
    /// the new one where this one was (window or tray).
    /// </summary>
    private void OnRestartRequested(object? sender, EventArgs e)
    {
        var startInTray = !AppWindow.IsVisible;
        PrepareExit(applyUpdate: false);
        Log.Information("Restarting for the update");
        Log.CloseAndFlush();
        Updates.Restart(startInTray);
    }

    private void OnUpdatesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UpdateViewModel.IsReady))
            UpdateTrayToolTip();
    }

    // ── Window placement ─────────────────────────────────────────────────

    /// <summary>
    /// Opens the window where it was when it last closed, or centered on the primary display the first time
    /// (or when that place is no longer on any display).
    /// </summary>
    private void ConfigureSize()
    {
        var scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        int Scaled(int value) => (int)(value * scale);

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = Scaled(480);
            presenter.PreferredMinimumHeight = Scaled(560);
        }

        if (_settingsStore.Load().MainWindow is { } saved
            && DisplayArea.GetFromRect(new RectInt32(saved.X, saved.Y, saved.Width, saved.Height), DisplayAreaFallback.None) is { } display)
        {
            // Fit it inside that display, which may have shrunk or moved since.
            var area = display.WorkArea;
            var width = Math.Min(saved.Width, area.Width);
            var height = Math.Min(saved.Height, area.Height);
            var bounds = new RectInt32(
                Math.Clamp(saved.X, area.X, area.X + area.Width - width),
                Math.Clamp(saved.Y, area.Y, area.Y + area.Height - height),
                width,
                height);

            // Move first: arriving on a display with another DPI rescales the window, and the saved size
            // already is in that display's pixels.
            AppWindow.Move(new PointInt32(bounds.X, bounds.Y));
            AppWindow.MoveAndResize(bounds);
            _maximizeOnShow = saved.IsMaximized;
        }
        else
        {
            var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
            var size = new SizeInt32(Math.Min(Scaled(1040), workArea.Width), Math.Min(Scaled(820), workArea.Height));
            AppWindow.MoveAndResize(new RectInt32(
                workArea.X + (workArea.Width - size.Width) / 2,
                workArea.Y + (workArea.Height - size.Height) / 2,
                size.Width,
                size.Height));
        }

        _restoredBounds = new RectInt32(AppWindow.Position.X, AppWindow.Position.Y, AppWindow.Size.Width, AppWindow.Size.Height);
    }

    private void ApplyPendingMaximize()
    {
        if (!_maximizeOnShow)
            return;

        _maximizeOnShow = false;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
            presenter.Maximize();
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if ((args.DidPositionChange || args.DidSizeChange)
            && sender.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Restored })
        {
            _restoredBounds = new RectInt32(sender.Position.X, sender.Position.Y, sender.Size.Width, sender.Size.Height);
        }
    }

    /// <summary>
    /// Remembers where the window is for the next launch.
    /// </summary>
    private void SavePlacement()
    {
        var isMaximized = _maximizeOnShow
            || AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized };
        var placement = new WindowPlacement(
            _restoredBounds.X, _restoredBounds.Y, _restoredBounds.Width, _restoredBounds.Height, isMaximized);

        try
        {
            var saved = _settingsStore.Load();
            if (saved.MainWindow != placement)
                _settingsStore.Save(saved with { MainWindow = placement });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Could not save the window placement");
        }
    }

    /// <summary>
    /// Light, dark or the system's theme for the window content and its caption buttons.
    /// </summary>
    private void ApplyTheme()
    {
        var (elementTheme, titleBarTheme) = Settings.Theme switch
        {
            AppTheme.Light => (ElementTheme.Light, TitleBarTheme.Light),
            AppTheme.Dark => (ElementTheme.Dark, TitleBarTheme.Dark),
            _ => (ElementTheme.Default, TitleBarTheme.UseDefaultAppMode),
        };

        if (Content is FrameworkElement root)
            root.RequestedTheme = elementTheme;
        AppWindow.TitleBar.PreferredTheme = titleBarTheme;
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.Theme))
            ApplyTheme();
    }

    // ── Closing ──────────────────────────────────────────────────────────

    /// <summary>
    /// Closing the window keeps the app running in the tray (in Efficiency Mode) unless the user turned
    /// that off in Settings; Exit always quits.
    /// </summary>
    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isExiting)
            return;

        if (Settings.MinimizeToTray)
        {
            args.Cancel = true;
            SavePlacement();
            WindowExtensions.Hide(this, true);
        }
        else
        {
            PrepareExit();
        }
    }

    private void Exit()
    {
        PrepareExit();
        Close();
    }

    /// <summary>
    /// Saves the window's place and removes the tray icon. A downloaded update is applied once the app has exited,
    /// unless the caller restarts into it right away.
    /// </summary>
    private void PrepareExit(bool applyUpdate = true)
    {
        _isExiting = true;
        SavePlacement();
        AppWindow.Changed -= OnAppWindowChanged;
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        Settings.PropertyChanged -= OnSettingsPropertyChanged;
        Updates.PropertyChanged -= OnUpdatesPropertyChanged;
        _localization.LanguageChanged -= OnLanguageChanged;
        TrayIcon.Dispose();

        if (applyUpdate)
            Updates.ApplyOnExit();
    }

    private void OnTitleBarBackRequested(Microsoft.UI.Xaml.Controls.TitleBar sender, object args)
    {
        if (Settings.IsOpen)
            Settings.Close();
        else
            Schedule.Close();
    }

    /// <summary>
    /// Re-reads every text in the window in the new language. Deferred, because the change comes from
    /// the language picker, which is still inside its selection change.
    /// </summary>
    private void OnLanguageChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(() =>
    {
        Bindings.Update();
        LoginView.RefreshText();
        DashboardView.RefreshText();
        ScheduleView.RefreshText();
        SettingsView.RefreshText();
        UpdateTrayToolTip();
    });

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppViewModel.IsAuthenticated))
        {
            if (!ViewModel.IsAuthenticated)
            {
                Schedule.Close();
            }
            else if (!ViewModel.IsDemo && !_consentAsked && Settings.NeedsCrashReportConsent)
            {
                // Once, after the first real sign-in.
                _consentAsked = true;
                EnqueueNotice(AskCrashReportConsentAsync);
            }
        }

        // IsSystemOnline is raised on every status update, which also carries the running state and mode.
        if (e.PropertyName is nameof(AppViewModel.IsSystemOnline) or nameof(AppViewModel.IsAuthenticated)
            or nameof(AppViewModel.CurrentFlow) or nameof(AppViewModel.Co2Level) or nameof(AppViewModel.DataStateText))
        {
            UpdateTrayToolTip();
        }
    }

    /// <summary>
    /// Hovering the tray icon shows the unit at a glance:
    /// <code>
    /// WAFE Control · byt 1.001
    /// Running · Mode: Schedule
    /// Flow: 120 m³/h · CO₂: 650 ppm
    /// </code>
    /// The last line says "offline" when the unit is, and is left out while it's online without sensor data.
    /// Old or missing data and a ready update add a line.
    /// </summary>
    private void UpdateTrayToolTip()
    {
        var text = "WAFE Control";
        if (ViewModel.IsAuthenticated)
        {
            var subtitle = Xaml.Subtitle(ViewModel.UnitName, ViewModel.IsDemo);
            if (subtitle.Length > 0)
                text = $"{text} · {subtitle}";

            var state = Xaml.SystemState(ViewModel.SystemControl.IsSystemRunning);
            var mode = string.Format(Strings.TrayMode, Xaml.ModeName(ViewModel.SystemControl.CurrentAuthority));
            text = $"{text}\n{state} · {mode}";

            if (ViewModel.HasSensorData)
                text += "\n" + string.Format(Strings.TrayReadings, DisplayFormat.Flow(ViewModel.CurrentFlow), Xaml.Co2(ViewModel.Co2Level));
            else if (!ViewModel.IsSystemOnline)
                text += "\n" + Strings.AppStatusUnitOffline;

            if (ViewModel.DataState is DataState.Stale or DataState.ServerUnreachable or DataState.NoInternet)
                text += "\n" + ViewModel.DataStateText;
        }

        if (Updates.IsReady)
            text += "\n" + Strings.TrayUpdateReady;

        if (TrayIcon.ToolTipText != text)
            TrayIcon.ToolTipText = text;
    }

    private void OnOpenLogFolderClick(object sender, RoutedEventArgs e) => OpenLogFolder();

    private static void OpenLogFolder()
    {
        Directory.CreateDirectory(App.LogDirectory);
        Process.Start(new ProcessStartInfo(App.LogDirectory) { UseShellExecute = true });
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);
}
