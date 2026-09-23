using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RecuperationSystem.Core.ViewModels;

public sealed partial class LoginViewModel : ObservableObject
{
    public LoginViewModel()
    {
        Username = string.Empty;
        Password = string.Empty;
        RememberMe = true;
    }

    [ObservableProperty]
    public partial string Username { get; set; }

    [ObservableProperty]
    public partial string Password { get; set; }

    [ObservableProperty]
    public partial bool RememberMe { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public event EventHandler? SubmitRequested;
    public event EventHandler? CancelRequested;

    [RelayCommand]
    private void Submit() => SubmitRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Cancel() => CancelRequested?.Invoke(this, EventArgs.Empty);
}
