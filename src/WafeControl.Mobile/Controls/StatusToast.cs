using System.ComponentModel;
using Microsoft.Maui.Controls.Shapes;
using WafeControl.Core.Localization;
using WafeControl.Core.ViewModels;
using WafeControl.Mobile.Helpers;

namespace WafeControl.Mobile.Controls;

/// <summary>
/// Shows <see cref="AppViewModel.StatusMessage"/> when a command reports progress or its result (DESIGN.md, section 7),
/// replacing the Windows footer. Background status (polling, connection) isn't shown; refresh errors only after
/// the user asked for a refresh (<see cref="ShowRefreshErrors"/>).
/// </summary>
public sealed class StatusToast : ContentView
{
    private static readonly TimeSpan ShowFor = TimeSpan.FromSeconds(3);

    // In-progress messages ("Activating boost…") stay until the result arrives, at most this long.
    private static readonly TimeSpan ShowPendingFor = TimeSpan.FromSeconds(40);

    private readonly Label _text;
    private AppViewModel? _app;
    private CancellationTokenSource? _hide;
    private DateTime _showRefreshErrorsUntil;

    public StatusToast()
    {
        _text = new Label { Style = Theme.Style("Body") };
        var border = new Border
        {
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(16, 12),
            Content = _text,
            Shadow = new Shadow { Brush = Colors.Black, Opacity = 0.14f, Offset = new Point(0, 8), Radius = 16 },
        }.SetColor(VisualElement.BackgroundColorProperty, "CardBackground").SetColor(Border.StrokeProperty, "CardStroke");

        Content = border;
        IsVisible = false;
        InputTransparent = true;
        VerticalOptions = LayoutOptions.End;
        Margin = new Thickness(16);
        SemanticProperties.SetHeadingLevel(this, SemanticHeadingLevel.None);
    }

    /// <summary>
    /// Starts showing the app's status messages; call when the page appears.
    /// </summary>
    public void Attach(AppViewModel app)
    {
        Detach();
        _app = app;
        _app.PropertyChanged += OnAppPropertyChanged;
    }

    public void Detach()
    {
        if (_app is not null)
            _app.PropertyChanged -= OnAppPropertyChanged;
        _app = null;
    }

    /// <summary>
    /// The user asked for a refresh, so a failure is news to them.
    /// </summary>
    public void ShowRefreshErrors() => _showRefreshErrorsUntil = DateTime.UtcNow.AddSeconds(15);

    private void OnAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppViewModel.StatusMessage) && _app?.StatusMessage is { Length: > 0 } message && IsNews(message))
            Dispatcher.Dispatch(() => Show(message));
    }

    private bool IsNews(string message)
    {
        if (message == Strings.AppStatusUnitOnline || message == Strings.AppStatusUnitOffline
            || message == Strings.AppStatusConnected || message == Strings.AppStatusConnecting
            || message == Strings.AppStatusDisconnected || message == Strings.AppStatusNotConnected
            || message == Strings.AppStatusSignInPrompt || message == Strings.AppStatusSigningIn
            || message == Strings.AppStatusConnectionFailed)
            return false;

        var refreshErrorPrefix = Strings.AppStatusRefreshError.Split("{0}")[0];
        return !message.StartsWith(refreshErrorPrefix, StringComparison.Ordinal) || DateTime.UtcNow < _showRefreshErrorsUntil;
    }

    private async void Show(string message)
    {
        _hide?.Cancel();
        var hide = _hide = new CancellationTokenSource();

        _text.Text = message;
        SemanticScreenReader.Announce(message);
        if (!IsVisible)
        {
            Opacity = 0;
            TranslationY = 16;
            IsVisible = true;
            await Task.WhenAll(this.FadeToAsync(1, 150, Easing.CubicOut), this.TranslateToAsync(0, 0, 250, Easing.CubicOut));
        }

        try
        {
            await Task.Delay(message.EndsWith('…') ? ShowPendingFor : ShowFor, hide.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        await this.FadeToAsync(0, 250, Easing.CubicOut);
        if (!hide.IsCancellationRequested)
            IsVisible = false;
    }
}
