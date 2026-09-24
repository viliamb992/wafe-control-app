using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WafeControl.Core.ViewModels;

namespace WafeControl.WinUI.Views;

/// <summary>
/// Content of the rename unit dialog.
/// </summary>
public sealed partial class UnitNameEditor : UserControl
{
    public UnitNameEditor(UnitNameEditorViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        // Ready to type over the whole name.
        Loaded += (_, _) =>
        {
            NameBox.Focus(FocusState.Programmatic);
            NameBox.SelectAll();
        };
    }

    public UnitNameEditorViewModel ViewModel { get; }
}
