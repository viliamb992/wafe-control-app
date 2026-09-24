using WafeControl.Core.ViewModels;

namespace WafeControl.Tests.ViewModels;

public class UnitNamesTests
{
    [Theory]
    [InlineData("byt 1.001, sn 1234567", "byt 1.001")]
    [InlineData("byt 1.001 sn 1234567", "byt 1.001")]
    [InlineData("byt 1.001, sn 1234567x", "byt 1.001")]
    [InlineData("byt 1.001, sn 1234567 garáž", "byt 1.001 garáž")]
    [InlineData("SN: 1234567, Chata", "Chata")]
    [InlineData("Dům - sn 1234567", "Dům")]
    [InlineData("Snowdon 12", "Snowdon 12")]
    [InlineData("byt 1.001", "byt 1.001")]
    [InlineData("sn 1234567", "")]
    [InlineData("  ", "")]
    [InlineData(null, "")]
    public void WithoutSerialNumber(string? name, string expected) =>
        Assert.Equal(expected, UnitNames.WithoutSerialNumber(name));
}
