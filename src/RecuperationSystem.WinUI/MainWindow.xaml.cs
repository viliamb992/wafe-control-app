using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using RecuperationSystem.Core.Localization;
using RecuperationSystem.Core.ViewModels;
using RecuperationSystem.Core.ViewModels.Schedule;
using RecuperationSystem.WinUI.Helpers;
using Windows.Graphics;

namespace RecuperationSystem.WinUI;

public sealed partial class MainWindow : Window
{
    private static readonly string IconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app-icon.ico");

    private readonly ILocalizationService _localization;
    private bool _isExiting;

    public MainWindow(AppViewModel viewModel, ScheduleViewModel schedule, SettingsViewModel settings, ILocalizationService localization)
    {
        ViewModel = viewModel;
        Schedule = schedule;
        Settings = settings;
        _localization = localization;
        ShowWindowCommand = new RelayCommand(ShowFromTray);
        ExitCommand = new RelayCommand(Exit);

        InitializeComponent();

        LoginView.ViewModel = viewModel;
        DashboardView.ViewModel = viewModel;
        DashboardView.Schedule = schedule;
        ScheduleView.ViewModel = schedule;
        ScheduleView.Main = viewModel;
        SettingsView.ViewModel = settings;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(IconPath);
        ConfigureSize();

        TrayIcon.Icon = new System.Drawing.Icon(IconPath);
        AppWindow.Closing += OnClosing;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        _localization.LanguageChanged += OnLanguageChanged;
    }

    public AppViewModel ViewModel { get; }

    public ScheduleViewModel Schedule { get; }

    public SettingsViewModel Settings { get; }

    public ICommand ShowWindowCommand { get; }

    public ICommand ExitCommand { get; }

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
        WindowExtensions.Show(this, true);
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
            presenter.Restore();
        Activate();
    }

    private void ConfigureSize()
    {
        var scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        int Scaled(int value) => (int)(value * scale);

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = Scaled(480);
            presenter.PreferredMinimumHeight = Scaled(560);
        }

        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var size = new SizeInt32(Math.Min(Scaled(1040), workArea.Width), Math.Min(Scaled(820), workArea.Height));
        AppWindow.MoveAndResize(new RectInt32(
            workArea.X + (workArea.Width - size.Width) / 2,
            workArea.Y + (workArea.Height - size.Height) / 2,
            size.Width,
            size.Height));
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
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
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
    /// Wafe Recuperation
    /// Running · Mode: Schedule
    /// Flow: 120 m³/h · CO₂: 650 ppm
    /// </code>
    /// </summary>
    private void UpdateTrayToolTip()
    {
        var text = "Wafe Recuperation";
        if (ViewModel.IsAuthenticated)
        {
            var state = Xaml.SystemState(ViewModel.SystemControl.IsSystemRunning);
            var mode = string.Format(Strings.TrayMode, Xaml.ModeName(ViewModel.SystemControl.CurrentAuthority));
            var readings = ViewModel.IsSystemOnline
                ? string.Format(Strings.TrayReadings, Xaml.Flow(ViewModel.CurrentFlow), Xaml.Co2(ViewModel.Co2Level))
                : Strings.AppStatusUnitOffline;
            text = $"{text}\n{state} · {mode}\n{readings}";
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
