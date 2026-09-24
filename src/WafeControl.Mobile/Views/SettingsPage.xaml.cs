using WafeControl.Core.Localization;
using WafeControl.Core.ViewModels;

namespace WafeControl.Mobile.Views;

/// <summary>
/// Settings: language and appearance (also before sign-in), then the unit and the account.
/// Changes apply right away; the language rebuilds the pages (see <see cref="App"/>).
/// </summary>
public partial class SettingsPage : ContentPage
{
    private const string ProjectUrl = "https://github.com/viliamb992/wafe-recuperation-app";

    private readonly SettingsViewModel _settings;
    private readonly AppViewModel _app;
    private bool _syncing;

    public SettingsPage(SettingsViewModel settings, AppViewModel app)
    {
        _settings = settings;
        _app = app;
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
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        // Pushed on top of the sign-in page rather than shown as a tab.
        BackButton.IsVisible = Navigation.NavigationStack.Count > 1;
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopAsync();

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

    private async void OnRenameClicked(object? sender, EventArgs e)
    {
        if (Navigation.ModalStack.Count == 0)
            await Navigation.PushModalAsync(new UnitNamePage(_app, _app.EditUnitName()));
    }

    private async void OnServiceMailClicked(object? sender, EventArgs e) => await OpenAsync(DisplayFormat.ServiceMailUri(_app.Unit));

    private async void OnServiceWebClicked(object? sender, EventArgs e) => await OpenAsync(DisplayFormat.ServiceWebUri(_app.Unit));

    private async void OnProjectClicked(object? sender, EventArgs e) => await OpenAsync(new Uri(ProjectUrl));

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
