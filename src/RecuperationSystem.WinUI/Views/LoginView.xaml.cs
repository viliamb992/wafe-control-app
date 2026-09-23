using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using RecuperationSystem.Core.ViewModels;
using Windows.System;

namespace RecuperationSystem.WinUI.Views;

public sealed partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Set by the window before the view loads.
    /// </summary>
    public AppViewModel ViewModel { get; set; } = null!;

    /// <summary>
    /// Re-reads the text after the app language changed.
    /// </summary>
    public void RefreshText() => Bindings.Update();

    private void OnPasswordKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
            return;

        e.Handled = true;
        if (ViewModel.SubmitLoginCommand.CanExecute(null))
            ViewModel.SubmitLoginCommand.Execute(null);
    }
}
