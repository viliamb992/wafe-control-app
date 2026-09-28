using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WafeControl.Core.Demo;
using WafeControl.Core.Localization;
using WafeControl.Core.Threading;
using WafeControl.Core.ViewModels;

namespace WafeControl.WinUI.Views;

public sealed partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        Loaded += (_, _) => BuildDemoFaults();
    }

    /// <summary>
    /// Set by the window before the view loads.
    /// </summary>
    public SettingsViewModel ViewModel { get; set; } = null!;

    /// <summary>
    /// For the unit card; set by the window before the view loads.
    /// </summary>
    public AppViewModel Main { get; set; } = null!;

    /// <summary>
    /// For the Updates card; set by the window before the view loads.
    /// </summary>
    public UpdateViewModel Updates { get; set; } = null!;

    /// <summary>
    /// The demo unit, whose faults can be switched on in Debug builds; set by the window before the view loads.
    /// </summary>
    public DemoWafeApi Demo { get; set; } = null!;

    /// <summary>
    /// "Report a problem" in the About card; the window shows the dialog.
    /// </summary>
    public event EventHandler? ReportProblemRequested;

    /// <summary>
    /// Re-reads the text after the app language changed.
    /// </summary>
    public void RefreshText() => Bindings.Update();

    private void OnThemeChecked(object sender, RoutedEventArgs e) =>
        ViewModel.ThemeIndex = ThemeChoices.Children.IndexOf((UIElement)sender);

    private void OnReportProblemClick(object sender, RoutedEventArgs e) => ReportProblemRequested?.Invoke(this, EventArgs.Empty);

    private void OnRenameUnitClick(object sender, RoutedEventArgs e) => SafeAsync.Run(RenameUnitAsync);

    private async Task RenameUnitAsync()
    {
        var editor = Main.EditUnitName();
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            // Dialogs open outside the window's content, so they don't inherit a theme chosen in Settings.
            RequestedTheme = ActualTheme,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            Title = Strings.UnitNameTitle,
            Content = new UnitNameEditor(editor),
            PrimaryButtonText = Strings.ButtonSave,
            CloseButtonText = Strings.ButtonCancel,
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = editor.CanSave,
        };

        editor.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(UnitNameEditorViewModel.CanSave))
                dialog.IsPrimaryButtonEnabled = editor.CanSave;
        };

        // Keep the dialog open until the API accepted the name; errors show inside the dialog.
        dialog.PrimaryButtonClick += (_, args) =>
        {
            var deferral = args.GetDeferral();
            SafeAsync.Run(async () =>
            {
                try
                {
                    args.Cancel = !await Main.SaveUnitNameAsync(editor);
                }
                finally
                {
                    deferral.Complete();
                }
            });
        };

        await dialog.ShowAsync();
    }

    // Debug builds: crashes on purpose, one per path a real crash can take.
    private void OnCrashUiThreadClick(object sender, RoutedEventArgs e) =>
        throw new InvalidOperationException("Test crash on the UI thread (Settings → Debug)");

    private void OnCrashBackgroundClick(object sender, RoutedEventArgs e) =>
        new Thread(() => throw new InvalidOperationException("Test crash on a background thread (Settings → Debug)")).Start();

    // Bypasses every .NET handler, like a WinUI stowed exception; found in the event log at the next start.
    private void OnCrashNativeClick(object sender, RoutedEventArgs e) =>
        Environment.FailFast("Test native crash (Settings → Debug)");

    // Debug builds: one check box per simulated problem.
    private void BuildDemoFaults()
    {
        if (DemoFaultChoices.Children.Count > 0)
            return;

        foreach (var fault in Enum.GetValues<DemoFaults>().Where(f => f != DemoFaults.None))
        {
            var box = new CheckBox { Content = fault.ToString(), IsChecked = Demo.Faults.HasFlag(fault) };
            box.Checked += (_, _) => Demo.Faults |= fault;
            box.Unchecked += (_, _) => Demo.Faults &= ~fault;
            DemoFaultChoices.Children.Add(box);
        }
    }
}
