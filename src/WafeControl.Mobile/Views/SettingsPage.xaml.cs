using WafeControl.Core.Localization;
using WafeControl.Core.Threading;
using WafeControl.Core.ViewModels;
using WafeControl.Mobile.Helpers;

namespace WafeControl.Mobile.Views;

/// <summary>
/// Settings: language and appearance (also before sign-in), then the unit, the account and crash reports.
/// Changes apply right away; the language rebuilds the pages (see <see cref="App"/>).
/// </summary>
public partial class SettingsPage : ContentPage
{
    private const string ProjectUrl = "https://github.com/viliamb992/wafe-control-app";

    private readonly SettingsViewModel _settings;
    private readonly AppViewModel _app;
    private readonly ILocalizationService _localization;
    private bool _syncing;

    public SettingsPage(SettingsViewModel settings, AppViewModel app, ILocalizationService localization)
    {
        _settings = settings;
        _app = app;
        _localization = localization;
        InitializeComponent();
        BindingContext = settings;
        UnitCard.BindingContext = app;
        AccountCard.BindingContext = app;

        _syncing = true;
        LanguagePicker.ItemsSource = settings.Languages.Select(l => l.NativeName).ToList();
        LanguagePicker.SelectedIndex = IndexOf(settings.Languages, settings.SelectedLanguage);
        for (var i = 0; i < ThemeChoices.Count; i++)
            ((RadioButton)ThemeChoices[i]).IsChecked = i == settings.ThemeIndex;
        _syncing = false;

#if DEBUG
        // Debug builds: crash on purpose, to test crash handling and reports.
        var crash = new Button { Text = "Crash (test)", Style = Theme.Style("AccentTextButton"), Padding = new Thickness(0), HorizontalOptions = LayoutOptions.Start };
        crash.Clicked += (_, _) => throw new InvalidOperationException("Test crash (Settings, Debug build)");
        AboutActions.Add(crash);
#endif
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        // Pushed on top of the sign-in page rather than shown as a tab.
        BackButton.IsVisible = Navigation.NavigationStack.Count > 1;
    }

    private void OnBackClicked(object? sender, EventArgs e) => SafeAsync.Run(() => Navigation.PopAsync());

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (!_syncing && LanguagePicker.SelectedIndex >= 0)
            _settings.SelectedLanguage = _settings.Languages[LanguagePicker.SelectedIndex];
    }

    private void OnThemeChecked(object? sender, CheckedChangedEventArgs e)
    {
        if (!_syncing && e.Value)
            _settings.ThemeIndex = ThemeChoices.IndexOf((RadioButton)sender!);
    }

    private void OnRenameClicked(object? sender, EventArgs e) => SafeAsync.Run(async () =>
    {
        if (Navigation.ModalStack.Count == 0)
            await Navigation.PushModalAsync(new UnitNamePage(_app, _app.EditUnitName()));
    });

    private void OnServiceMailClicked(object? sender, EventArgs e) => SafeAsync.Run(() => OpenAsync(DisplayFormat.ServiceMailUri(_app.Unit)));

    private void OnServiceWebClicked(object? sender, EventArgs e) => SafeAsync.Run(() => OpenAsync(DisplayFormat.ServiceWebUri(_app.Unit)));

    private void OnProjectClicked(object? sender, EventArgs e) => SafeAsync.Run(() => OpenAsync(new Uri(ProjectUrl)));

    private void OnReportProblemClicked(object? sender, EventArgs e) => SafeAsync.Run(() => ProblemReporting.ReportAsync(this, _app, _localization));

    private void OnShareLogsClicked(object? sender, EventArgs e) => SafeAsync.Run(() => ProblemReporting.ShareAsync(Strings.SettingsShareLogs));

    private static async Task OpenAsync(Uri? uri)
    {
        if (uri is not null)
            await Launcher.Default.TryOpenAsync(uri);
    }

    private static int IndexOf(IReadOnlyList<AppLanguage> languages, AppLanguage language)
    {
        for (var i = 0; i < languages.Count; i++)
        {
            if (languages[i] == language)
                return i;
        }

        return -1;
    }
}
