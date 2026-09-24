using System.Globalization;
using WafeControl.Core.Localization;
using WafeControl.Shared;
using WafeControl.Shared.Models;

namespace WafeControl.Tests.Localization;

public class DisplayFormatTests
{
    [Fact]
    public void Temperature_UsesTheCulturesDecimalSeparator()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("cs-CZ");
            Assert.Equal("21,5 °C", DisplayFormat.Temperature(21.46));

            CultureInfo.CurrentCulture = TestCulture.English;
            Assert.Equal("21.5 °C", DisplayFormat.Temperature(21.46));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void MissingValues_ShowADash()
    {
        Assert.Equal(ModeNames.NoValue, DisplayFormat.Temperature(null));
        Assert.Equal(ModeNames.NoValue, DisplayFormat.Humidity(null));
        Assert.Equal(ModeNames.NoValue, DisplayFormat.Co2(0));
        Assert.Equal(ModeNames.NoValue, DisplayFormat.Percent(null));
    }

    [Theory]
    [InlineData(0, Co2Rating.NoReading)]
    [InlineData(650, Co2Rating.Good)]
    [InlineData(799, Co2Rating.Good)]
    [InlineData(800, Co2Rating.Fair)]
    [InlineData(1199, Co2Rating.Fair)]
    [InlineData(1200, Co2Rating.Poor)]
    public void RateCo2(int ppm, Co2Rating expected) => Assert.Equal(expected, DisplayFormat.RateCo2(ppm));

    [Fact]
    public void Co2Quality_FollowsTheRating()
    {
        Assert.Equal("Good air quality", DisplayFormat.Co2Quality(650));
        Assert.Equal("Poor: ventilate now", DisplayFormat.Co2Quality(1500));
        Assert.Equal("No reading", DisplayFormat.Co2Quality(0));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(19, true)]
    [InlineData(20, false)]
    [InlineData(80, false)]
    public void IsFilterLow(int? health, bool expected) => Assert.Equal(expected, DisplayFormat.IsFilterLow(health));

    [Fact]
    public void SystemSummary_LeavesOutTheFlowWithoutSensorData()
    {
        Assert.Equal("Manual mode · 140 m³/h", DisplayFormat.SystemSummary(AppConstants.ModeManual, 140, hasSensorData: true));
        Assert.Equal("Manual mode", DisplayFormat.SystemSummary(AppConstants.ModeManual, 140, hasSensorData: false));
    }

    [Fact]
    public void ShowsNextStart_OnlyWhileRunningOnTheSchedule()
    {
        var next = new DateTime(2026, 9, 25, 6, 0, 0);

        Assert.True(DisplayFormat.ShowsNextStart(next, AppConstants.ModeSchedule, isRunning: true));
        Assert.False(DisplayFormat.ShowsNextStart(next, AppConstants.ModeManual, isRunning: true));
        Assert.False(DisplayFormat.ShowsNextStart(next, AppConstants.ModeSchedule, isRunning: false));
        Assert.False(DisplayFormat.ShowsNextStart(null, AppConstants.ModeSchedule, isRunning: true));
    }

    [Fact]
    public void LastUpdate_IsEmptyUntilKnown() => Assert.Equal(string.Empty, DisplayFormat.LastUpdate(null));

    [Fact]
    public void LastUpdate_Today_ShowsTheTime()
    {
        var now = DateTimeOffset.Now;
        Assert.Equal($"Last update: {now.LocalDateTime.ToString("HH:mm:ss", CultureInfo.CurrentCulture)}", DisplayFormat.LastUpdate(now));
    }

    [Theory]
    [InlineData(0, "Off")]
    [InlineData(725, "12:05")]
    [InlineData(3600, "60:00")]
    public void Countdown(int seconds, string expected) => Assert.Equal(expected, DisplayFormat.Countdown(seconds));

    [Fact]
    public void Service_LinksOnlyForValidAddresses()
    {
        var info = new SystemInfo { Contacts = new SystemContacts { Service = new ContactInfo { Mail = "servis@wafe.eu", Web = "https://wafe.eu/" } } };

        Assert.Equal(new Uri("mailto:servis@wafe.eu"), DisplayFormat.ServiceMailUri(info));
        Assert.Equal("wafe.eu", DisplayFormat.ServiceWebText(info));

        info.Contacts.Service.Web = "javascript:alert(1)";
        Assert.Null(DisplayFormat.ServiceWebUri(info));
        Assert.Equal(string.Empty, DisplayFormat.ServiceWebText(info));
        Assert.Null(DisplayFormat.ServiceMailUri(null));
    }
}
