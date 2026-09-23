using RecuperationSystem.Core.ViewModels;

namespace RecuperationSystem.Tests.ViewModels;

public class UnitNameEditorViewModelTests
{
    [Theory]
    [InlineData("Chata", true)]
    [InlineData("  Chata  ", true)]
    [InlineData("1234567890123456789012345678", true)]    // 28 characters
    [InlineData("12345678901234567890123456789", false)]  // 29 characters
    [InlineData("   ", false)]
    [InlineData("", false)]
    [InlineData("byt 1.001", false)]                        // unchanged
    [InlineData(" byt 1.001 ", false)]                      // unchanged after trimming
    public void CanSave(string name, bool expected)
    {
        var editor = new UnitNameEditorViewModel("byt 1.001") { Name = name };

        Assert.Equal(expected, editor.CanSave);
    }

    [Fact]
    public void Name_Change_RaisesCanSaveChanged()
    {
        var editor = new UnitNameEditorViewModel("byt 1.001");
        var raised = new List<string?>();
        editor.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        editor.Name = "Chata";

        Assert.Contains(nameof(UnitNameEditorViewModel.CanSave), raised);
    }
}
