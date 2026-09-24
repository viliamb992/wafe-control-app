using WafeControl.Core.ViewModels;

namespace WafeControl.Mobile.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(AppViewModel app)
    {
        InitializeComponent();
        BindingContext = app;
    }

    private void OnUsernameCompleted(object? sender, EventArgs e) => PasswordEntry.Focus();

    private void OnRememberTapped(object? sender, TappedEventArgs e) => RememberBox.IsChecked = !RememberBox.IsChecked;

    private async void OnSettingsClicked(object? sender, EventArgs e) => await ((AppShell)Shell.Current).GoToSettingsAsync();
}
