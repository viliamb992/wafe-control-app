using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WafeControl.Core.ViewModels;
using Windows.System;
using Windows.UI.Core;

namespace WafeControl.WinUI.Views;

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

    // A password typed with Caps Lock on is the most common "wrong password".
    private void OnPasswordKeyUp(object sender, KeyRoutedEventArgs e) => UpdateCapsLockHint(((PasswordBox)sender).FocusState != FocusState.Unfocused);

    private void OnPasswordFocusChanged(object sender, RoutedEventArgs e) => UpdateCapsLockHint(((PasswordBox)sender).FocusState != FocusState.Unfocused);

    private void UpdateCapsLockHint(bool hasFocus)
    {
        var capsLock = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.CapitalLock).HasFlag(CoreVirtualKeyStates.Locked);
        CapsLockHint.Visibility = hasFocus && capsLock ? Visibility.Visible : Visibility.Collapsed;
    }
}
