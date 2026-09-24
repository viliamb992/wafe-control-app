using CommunityToolkit.Mvvm.ComponentModel;
using WafeControl.Core.Localization;
using WafeControl.Shared;

namespace WafeControl.Core.ViewModels;

/// <summary>
/// The rename dialog for the unit. Starts from the portal name as stored, serial number included.
/// </summary>
public sealed partial class UnitNameEditorViewModel : ObservableObject
{
    public UnitNameEditorViewModel(string original)
    {
        Original = original;
        Name = original;
    }

    public string Original { get; }

    public int MaxLength => AppConstants.MaxUnitNameLength;

    public string Hint => string.Format(Strings.UnitNameHint, MaxLength);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>
    /// The name that gets saved: without leading and trailing spaces.
    /// </summary>
    public string NewName => (Name ?? string.Empty).Trim();

    /// <summary>
    /// A changed name that isn't empty and fits the limit.
    /// </summary>
    public bool CanSave => NewName.Length is > 0 and <= AppConstants.MaxUnitNameLength && NewName != Original;
}
