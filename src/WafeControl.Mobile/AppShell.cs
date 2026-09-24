using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using WafeControl.Core.Localization;
using WafeControl.Core.ViewModels;
using WafeControl.Mobile.Helpers;
using WafeControl.Mobile.Views;

namespace WafeControl.Mobile;

/// <summary>
/// Navigation: the sign-in page, then tabs for Overview, Schedule and Settings.
/// Follows <see cref="AppViewModel.IsAuthenticated"/>: signing in opens the tabs, signing out returns to sign-in.
/// </summary>
public sealed class AppShell : Shell
{
    private const string LoginRoute = "login";
    private const string MainRoute = "main";
    private const string SettingsRoute = "settings";

    /// <summary>
    /// Settings opened from the sign-in page (language and appearance before signing in).
    /// </summary>
    public const string LoginSettingsRoute = "preferences";

    private readonly AppViewModel _app;
    private readonly TabBar _tabs;

    public AppShell(IServiceProvider services, AppViewModel app)
    {
        _app = app;
        FlyoutBehavior = FlyoutBehavior.Disabled;

        Items.Add(new ShellContent
        {
            Route = LoginRoute,
            ContentTemplate = new DataTemplate(services.GetRequiredService<LoginPage>),
        });

        _tabs = new TabBar
        {
            Route = MainRoute,
            Items =
            {
                Tab(services.GetRequiredService<DashboardPage>, "dashboard", Strings.NavDashboard, FluentIcons.Home),
                Tab(services.GetRequiredService<SchedulePage>, "schedule", Strings.NavSchedule, FluentIcons.CalendarLtr),
                Tab(services.GetRequiredService<SettingsPage>, SettingsRoute, Strings.SettingsTitle, FluentIcons.Settings),
            },
        };
        Items.Add(_tabs);

        _app.PropertyChanged += OnAppPropertyChanged;
        Loaded += (_, _) => UpdateRoute();
        Unloaded += (_, _) => _app.PropertyChanged -= OnAppPropertyChanged;
    }

    /// <summary>
    /// Opens Settings: the tab once signed in, otherwise on top of the sign-in page.
    /// </summary>
    public Task GoToSettingsAsync() =>
        GoToAsync(_app.IsAuthenticated ? $"//{MainRoute}/{SettingsRoute}" : $"//{LoginRoute}/{LoginSettingsRoute}");

    private static ShellContent Tab(Func<object> page, string route, string title, string glyph) => new()
    {
        Route = route,
        Title = title,
        Icon = new FontImageSource { FontFamily = FluentIcons.FontFamily, Glyph = glyph, Size = 24 },
        ContentTemplate = new DataTemplate(page),
    };

    private void OnAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppViewModel.IsAuthenticated))
            Dispatcher.Dispatch(UpdateRoute);
    }

    private async void UpdateRoute()
    {
        var onTabs = CurrentItem == _tabs;
        if (_app.IsAuthenticated && !onTabs)
        {
            await GoToAsync($"//{MainRoute}/dashboard");
        }
        else if (!_app.IsAuthenticated && onTabs)
        {
            while (Navigation.ModalStack.Count > 0)
                await Navigation.PopModalAsync(animated: false);
            await GoToAsync($"//{LoginRoute}");
        }
    }
}
