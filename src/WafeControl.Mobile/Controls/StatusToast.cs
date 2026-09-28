using System.ComponentModel;
using Microsoft.Maui.Controls.Shapes;
using WafeControl.Core.Localization;
using WafeControl.Core.Threading;
using WafeControl.Core.ViewModels;
using WafeControl.Mobile.Helpers;

namespace WafeControl.Mobile.Controls;

/// <summary>
/// Shows <see cref="AppViewModel.Feedback"/>, the result of a command or a refresh the user asked for (DESIGN.md,
/// section 7), replacing the Windows footer. Retry and "Report a problem" appear as a button when they can help.
/// </summary>
public sealed class StatusToast : ContentView
{
    private static readonly TimeSpan ShowFor = TimeSpan.FromSeconds(3);

    // With a button, long enough to reach it.
    private static readonly TimeSpan ShowWithActionFor = TimeSpan.FromSeconds(8);

    // In-progress messages ("Activating boost…") stay until the result arrives, at most this long.
    private static readonly TimeSpan ShowPendingFor = TimeSpan.FromSeconds(40);

    private readonly Label _text;
    private readonly Button _action;
    private AppViewModel? _app;
    private Feedback? _shown;
    private CancellationTokenSource? _hide;

    public StatusToast()
    {
        _text = new Label { Style = Theme.Style("Body"), VerticalOptions = LayoutOptions.Center };
        _action = new Button { Style = Theme.Style("AccentTextButton"), Padding = new Thickness(8, 0), IsVisible = false };
        _action.Clicked += (_, _) => SafeAsync.Run(OnActionAsync);

        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 8,
        };
        grid.Add(_text, 0);
        grid.Add(_action, 1);

        var border = new Border
        {
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(16, 12),
            Content = grid,
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
    /// Starts showing the app's feedback; call when the page appears.
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

    private void OnAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AppViewModel.Feedback))
            return;

        // A result goes on its own time; a "sending…" message that's withdrawn (no answer from the unit) goes now.
        if (_app?.Feedback is { } feedback)
            Dispatcher.Dispatch(() => SafeAsync.Run(() => ShowAsync(feedback)));
        else if (_shown?.Kind == FeedbackKind.Progress)
            Dispatcher.Dispatch(() => SafeAsync.Run(HideAsync));
    }

    private async Task OnActionAsync()
    {
        if (_shown?.Retry is { } retry)
        {
            await retry.ExecuteAsync(null);
        }
        else if (_shown?.OffersReport == true && _app is not null
                 && Handler?.MauiContext?.Services.GetService<ILocalizationService>() is { } localization
                 && ParentPage() is { } page)
        {
            await ProblemReporting.ReportAsync(page, _app, localization);
        }
    }

    private Page? ParentPage()
    {
        Element? element = this;
        while (element is not null and not Page)
            element = element.Parent;
        return element as Page;
    }

    private async Task ShowAsync(Feedback feedback)
    {
        _hide?.Cancel();
        var hide = _hide = new CancellationTokenSource();
        _shown = feedback;

        _text.Text = feedback.Text;
        _action.Text = feedback.Retry is not null ? Strings.ButtonRetry : feedback.OffersReport ? Strings.ReportProblem : null;
        _action.IsVisible = feedback.HasAction;
        InputTransparent = !feedback.HasAction;
        SemanticScreenReader.Announce(feedback.Text);

        if (!IsVisible)
        {
            Opacity = 0;
            TranslationY = 16;
            IsVisible = true;
            await Task.WhenAll(this.FadeToAsync(1, 150, Easing.CubicOut), this.TranslateToAsync(0, 0, 250, Easing.CubicOut));
        }

        var showFor = feedback.Kind == FeedbackKind.Progress ? ShowPendingFor : feedback.HasAction ? ShowWithActionFor : ShowFor;
        try
        {
            await Task.Delay(showFor, hide.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        await FadeOutAsync(hide);
    }

    private Task HideAsync()
    {
        _hide?.Cancel();
        var hide = _hide = new CancellationTokenSource();
        _shown = null;
        return IsVisible ? FadeOutAsync(hide) : Task.CompletedTask;
    }

    // Unless something new was shown meanwhile.
    private async Task FadeOutAsync(CancellationTokenSource hide)
    {
        await this.FadeToAsync(0, 250, Easing.CubicOut);
        if (!hide.IsCancellationRequested)
        {
            IsVisible = false;
            InputTransparent = true;
        }
    }
}
