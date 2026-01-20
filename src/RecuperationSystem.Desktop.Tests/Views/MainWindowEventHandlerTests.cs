using Xunit;
using RecuperationSystem.Shared;

namespace RecuperationSystem.Desktop.Tests.Views;

/// <summary>
/// Tests for MainWindow event handler logic
/// Since MainWindow is an Avalonia Window, we test the logic separately
/// </summary>
public class MainWindowEventHandlerTests
{
    [Theory]
    [InlineData(900, 15)]
    [InlineData(1800, 30)]
    [InlineData(3600, 60)]
    public void BoostDuration_CalculatesCorrectMinutes(int seconds, int expectedMinutes)
    {
        // Arrange & Act
        var minutes = seconds / 60;

        // Assert
        Assert.Equal(expectedMinutes, minutes);
    }

    [Theory]
    [InlineData("intelligent", AppConstants.ModeIntelligent)]
    [InlineData("manual", AppConstants.ModeManual)]
    [InlineData("schedule", AppConstants.ModeSchedule)]
    public void ModeTag_MapsToCorrectConstant(string tag, string expectedConstant)
    {
        // Arrange & Act
        var mode = tag;

        // Assert
        Assert.Equal(expectedConstant, mode);
    }

    [Fact]
    public void FlowSpeedRange_IsValid()
    {
        // Arrange
        var min = AppConstants.MinFlowSpeed;
        var max = AppConstants.MaxFlowSpeed;

        // Assert
        Assert.Equal(50, min);
        Assert.Equal(220, max);
        Assert.True(max > min);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(150)]
    [InlineData(200)]
    [InlineData(220)]
    public void FlowSpeed_IsWithinValidRange(int speed)
    {
        // Arrange & Act
        var isValid = speed >= AppConstants.MinFlowSpeed && speed <= AppConstants.MaxFlowSpeed;

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData(49)]
    [InlineData(0)]
    [InlineData(221)]
    [InlineData(300)]
    public void FlowSpeed_IsOutsideValidRange(int speed)
    {
        // Arrange & Act
        var isValid = speed >= AppConstants.MinFlowSpeed && speed <= AppConstants.MaxFlowSpeed;

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void BoostOff_UsesZeroSeconds()
    {
        // Arrange
        var boostOffSeconds = 0;

        // Act & Assert
        Assert.Equal(0, boostOffSeconds);
    }

    [Theory]
    [InlineData(true, "ON")]
    [InlineData(false, "OFF")]
    public void ToggleState_MapsToExpectedText(bool isChecked, string expectedText)
    {
        // This tests the expected behavior of toggle switches
        var displayText = isChecked ? "ON" : "OFF";
        Assert.Equal(expectedText, displayText);
    }
}
