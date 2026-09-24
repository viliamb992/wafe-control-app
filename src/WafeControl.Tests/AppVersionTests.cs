using WafeControl.Core;

namespace WafeControl.Tests;

public class AppVersionTests
{
    [Theory]
    [InlineData("1.2.0", "1.2.0")]
    [InlineData("1.2.0+b6531f7a0c", "1.2.0")]
    [InlineData("1.3.0-beta.1+b6531f7a0c", "1.3.0-beta.1")]
    [InlineData(null, "0.0.0")]
    [InlineData("", "0.0.0")]
    public void FromInformational_DropsCommitSuffix(string? informational, string expected)
        => Assert.Equal(expected, AppVersion.FromInformational(informational));

    [Fact]
    public void Current_HasNoCommitSuffix()
        => Assert.DoesNotContain('+', AppVersion.Current);
}
