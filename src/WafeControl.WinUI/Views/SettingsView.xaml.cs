using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WafeControl.Core.Localization;
using WafeControl.Core.ViewModels;

namespace WafeControl.WinUI.Views;

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
    /// For the unit card; set by the window before the view loads.
    /// </summary>
    public AppViewModel Main { get; set; } = null!;

    /// <summary>
    /// Re-reads the text after the app language changed.
    /// </summary>
    public void RefreshText() => Bindings.Update();

    private void OnThemeChecked(object sender, RoutedEventArgs e) =>
        ViewModel.ThemeIndex = ThemeChoices.Children.IndexOf((UIElement)sender);

    private async void OnRenameUnitClick(object sender, RoutedEventArgs e)
    {
        var editor = Main.EditUnitName();
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            // Dialogs open outside the window's content, so they don't inherit a theme chosen in Settings.
            RequestedTheme = ActualTheme,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            Title = Strings.UnitNameTitle,
            Content = new UnitNameEditor(editor),
            PrimaryButtonText = Strings.ButtonSave,
            CloseButtonText = Strings.ButtonCancel,
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = editor.CanSave,
        };

        editor.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(UnitNameEditorViewModel.CanSave))
                dialog.IsPrimaryButtonEnabled = editor.CanSave;
        };

        // Keep the dialog open until the API accepted the name; errors show inside the dialog.
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            args.Cancel = !await Main.SaveUnitNameAsync(editor);
            deferral.Complete();
        };

        await dialog.ShowAsync();
    }
}
