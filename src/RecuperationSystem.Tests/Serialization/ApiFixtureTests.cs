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

    [Fact]
    public void Header_Deserializes()
    {
        var header = JsonSerializer.Deserialize(Fixture("header.json"), WafeJsonContext.Default.HeaderInfo);

        Assert.NotNull(header);
        Assert.Equal(1790184364, header.Gen);
        Assert.Equal(JsonValueKind.Null, header.Message?.ValueKind ?? JsonValueKind.Null);
        Assert.Equal("byt 1.001, sn 1234567", header.Name);
        Assert.Equal("1234567000000", header.SerialNumber);
        Assert.Equal(1000, header.Gid);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1790184364642), header.Time);
        Assert.Equal("W201 E", header.Type);
        Assert.True(header.Online);
        Assert.True(header.Pm);
    }

    [Fact]
    public void Info_Deserializes()
    {
        var info = JsonSerializer.Deserialize(Fixture("info.json"), WafeJsonContext.Default.SystemInfo);

        Assert.NotNull(info);
        Assert.Equal("servis@wafe.cz", info.Contacts?.Service?.Mail);
        Assert.Equal("WAFE s.r.o.", info.Contacts?.Service?.Name);
        Assert.Equal("https://wafe.eu/", info.Contacts?.Service?.Web);
        Assert.Equal("W0201CEdEUBQ210", info.Unit?.Model);
        Assert.Equal("1234567000000", info.Unit?.SerialNumber);
        Assert.Equal("W201 E", info.Unit?.Type);
    }
}
