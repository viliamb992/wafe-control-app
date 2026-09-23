using System.Text.Json;
using RecuperationSystem.Shared.Serialization;

namespace RecuperationSystem.Tests.Serialization;

/// <summary>
/// Deserialization against responses captured from the real Wafe API (see Fixtures/).
/// </summary>
public class ApiFixtureTests
{
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void MainStatus_UnitNotReportingSensors_Deserializes()
    {
        var status = JsonSerializer.Deserialize(Fixture("main-not-reporting.json"), WafeJsonContext.Default.SystemStatus);

        Assert.NotNull(status);
        Assert.Equal(16195, status.Gen);
        Assert.Equal([null, null, null, null], status.Temperatures!);
        Assert.Null(status.Humidity);
        Assert.Null(status.Co2);
        Assert.Equal(50, status.FlowMin);
        Assert.Equal(220, status.FlowMax);
        Assert.Equal(200, status.FlowRequested);
        Assert.Equal("schedule", status.Authority);
        Assert.Equal(["intelligent", "manual", "schedule"], status.AuthorityAvailable!);
        Assert.Equal(["boost", "silent", "holiday"], status.Capabilities!);
        Assert.Equal(900, status.BoostDuration);
        Assert.False(status.StopActive);
        Assert.Equal(100, status.Filters?.Fresh?.Health);
        Assert.Equal("good", status.Filters?.Waste?.Status);
    }
}
