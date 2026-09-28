using WafeControl.Core.Localization;
using WafeControl.Core.Threading;
using WafeControl.Core.ViewModels;

namespace WafeControl.Mobile.Views;

public partial class LoginPage : ContentPage
{
    private static readonly Uri WafeWebsite = new("https://wafe.eu/");

    public LoginPage(AppViewModel app)
    {
        InitializeComponent();
        BindingContext = app;
    }

    private void OnUsernameCompleted(object? sender, EventArgs e) => PasswordEntry.Focus();

    private void OnRememberTapped(object? sender, TappedEventArgs e) => RememberBox.IsChecked = !RememberBox.IsChecked;

    private void OnShowPasswordClicked(object? sender, EventArgs e)
    {
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
        ShowPasswordButton.Text = PasswordEntry.IsPassword ? Strings.LoginShowPassword : Strings.LoginHidePassword;
    }

    private void OnWafeClicked(object? sender, EventArgs e) => SafeAsync.Run(() => Launcher.Default.TryOpenAsync(WafeWebsite));

    private void OnSettingsClicked(object? sender, EventArgs e) => SafeAsync.Run(() => ((AppShell)Shell.Current).GoToSettingsAsync());
}
