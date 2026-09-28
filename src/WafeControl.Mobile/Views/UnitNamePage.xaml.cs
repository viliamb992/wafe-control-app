using WafeControl.Core.Threading;
using WafeControl.Core.ViewModels;

namespace WafeControl.Mobile.Views;

/// <summary>
/// Renames the unit, like the Windows dialog: stays open until the portal accepted the name.
/// </summary>
public partial class UnitNamePage : ContentPage
{
    private readonly AppViewModel _app;
    private readonly UnitNameEditorViewModel _editor;

    public UnitNamePage(AppViewModel app, UnitNameEditorViewModel editor)
    {
        _app = app;
        _editor = editor;
        InitializeComponent();
        BindingContext = editor;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Ready to type over the whole name.
        NameEntry.Focus();
        NameEntry.CursorPosition = 0;
        NameEntry.SelectionLength = _editor.Name.Length;
    }

    private void OnSaveClicked(object? sender, EventArgs e) => SafeAsync.Run(SaveAsync);

    private async Task SaveAsync()
    {
        if (!_editor.CanSave || Busy.IsVisible)
            return;

        SaveButton.IsVisible = false;
        Busy.IsVisible = Busy.IsRunning = true;
        try
        {
            if (await _app.SaveUnitNameAsync(_editor))
                await Navigation.PopModalAsync();
        }
        finally
        {
            SaveButton.IsVisible = true;
            Busy.IsVisible = Busy.IsRunning = false;
        }
    }

    private void OnCancelClicked(object? sender, EventArgs e) => SafeAsync.Run(() => Navigation.PopModalAsync());
}
