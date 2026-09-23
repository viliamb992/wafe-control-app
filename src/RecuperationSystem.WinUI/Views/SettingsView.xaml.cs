using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RecuperationSystem.Core.ViewModels;

namespace RecuperationSystem.WinUI.Views;

public sealed partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Set by the window before the view loads.
    /// </summary>
    public SettingsViewModel ViewModel { get; set; } = null!;

    /// <summary>
    /// Re-reads the text after the app language changed.
    /// </summary>
    public void RefreshText() => Bindings.Update();

    private void OnThemeChecked(object sender, RoutedEventArgs e) =>
        ViewModel.ThemeIndex = ThemeChoices.Children.IndexOf((UIElement)sender);
}
