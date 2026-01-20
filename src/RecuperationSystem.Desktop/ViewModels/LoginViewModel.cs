using System;
using System.Reactive;
using ReactiveUI;

namespace RecuperationSystem.Desktop.ViewModels;

public sealed class LoginViewModel : ReactiveObject
{
    private string _username = string.Empty;
    private string _password = string.Empty;
    private bool _rememberMe = true;
    private string? _errorMessage;

    public string Username
    {
        get => _username;
        set => this.RaiseAndSetIfChanged(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => this.RaiseAndSetIfChanged(ref _password, value);
    }

    public bool RememberMe
    {
        get => _rememberMe;
        set => this.RaiseAndSetIfChanged(ref _rememberMe, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public ReactiveCommand<Unit, Unit> SubmitCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public event EventHandler? SubmitRequested;
    public event EventHandler? CancelRequested;

    public LoginViewModel()
    {
        SubmitCommand = ReactiveCommand.Create(() => SubmitRequested?.Invoke(this, EventArgs.Empty));
        CancelCommand = ReactiveCommand.Create(() => CancelRequested?.Invoke(this, EventArgs.Empty));
    }
}
