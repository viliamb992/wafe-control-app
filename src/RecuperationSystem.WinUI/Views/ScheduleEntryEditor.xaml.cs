using Microsoft.UI.Xaml.Controls;
using RecuperationSystem.Core.ViewModels.Schedule;

namespace RecuperationSystem.WinUI.Views;

/// <summary>
/// Content of the add/edit schedule action dialog.
/// </summary>
public sealed partial class ScheduleEntryEditor : UserControl
{
    public ScheduleEntryEditor(ScheduleEntryEditorViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public ScheduleEntryEditorViewModel ViewModel { get; }
}
