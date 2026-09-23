using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using RecuperationSystem.Core.Localization;
using RecuperationSystem.Core.Services;
using RecuperationSystem.Core.ViewModels;
using RecuperationSystem.Core.ViewModels.Schedule;
using RecuperationSystem.WinUI.Helpers;
using Serilog;
using Windows.Graphics;

namespace RecuperationSystem.WinUI;

public sealed partial class MainWindow : Window
{
    private static readonly string IconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app-icon.ico");

    private readonly ILocalizationService _localization;
    private readonly ISettingsStore _settingsStore;
    private bool _isExiting;

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
        ILocalizationService localization,
        ISettingsStore settingsStore)
    {
        ViewModel = viewModel;
        Schedule = schedule;
        Settings = settings;
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
        _localization.LanguageChanged += OnLanguageChanged;
    }

    public AppViewModel ViewModel { get; }

    public ScheduleViewModel Schedule { get; }

    public SettingsViewModel Settings { get; }

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
    }

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

    private void PrepareExit()
    {
        _isExiting = true;
        SavePlacement();
        AppWindow.Changed -= OnAppWindowChanged;
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        Settings.PropertyChanged -= OnSettingsPropertyChanged;
        _localization.LanguageChanged -= OnLanguageChanged;
        TrayIcon.Dispose();
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
        if (e.PropertyName == nameof(AppViewModel.IsAuthenticated) && !ViewModel.IsAuthenticated)
            Schedule.Close();

        // IsSystemOnline is raised on every status update, which also carries the running state and mode.
        if (e.PropertyName is nameof(AppViewModel.IsSystemOnline) or nameof(AppViewModel.IsAuthenticated)
            or nameof(AppViewModel.CurrentFlow) or nameof(AppViewModel.Co2Level))
        {
            UpdateTrayToolTip();
        }
    }

    /// <summary>
    /// Hovering the tray icon shows the unit at a glance:
    /// <code>
    /// Wafe Recuperation · byt 1.001
    /// Running · Mode: Schedule
    /// Flow: 120 m³/h · CO₂: 650 ppm
    /// </code>
    /// The last line says "offline" when the unit is, and is left out while it's online without sensor data.
    /// </summary>
    private void UpdateTrayToolTip()
    {
        var text = "Wafe Recuperation";
        if (ViewModel.IsAuthenticated)
        {
            if (ViewModel.UnitName.Length > 0)
                text = $"{text} · {ViewModel.UnitName}";

            var state = Xaml.SystemState(ViewModel.SystemControl.IsSystemRunning);
            var mode = string.Format(Strings.TrayMode, Xaml.ModeName(ViewModel.SystemControl.CurrentAuthority));
            text = $"{text}\n{state} · {mode}";

            if (ViewModel.HasSensorData)
                text += "\n" + string.Format(Strings.TrayReadings, Xaml.Flow(ViewModel.CurrentFlow), Xaml.Co2(ViewModel.Co2Level));
            else if (!ViewModel.IsSystemOnline)
                text += "\n" + Strings.AppStatusUnitOffline;
        }

        if (TrayIcon.ToolTipText != text)
            TrayIcon.ToolTipText = text;
    }

    private void OnOpenLogFolderClick(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(App.LogDirectory);
        Process.Start(new ProcessStartInfo(App.LogDirectory) { UseShellExecute = true });
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);
}
