using Xunit;
using Moq;
using RecuperationSystem.Desktop.ViewModels;
using RecuperationSystem.Desktop.Tests.Helpers;
using System.Threading.Tasks;
using System.Threading;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

namespace RecuperationSystem.Desktop.Tests.ViewModels;

public class AppViewModelIntegrationTests
{
    [Fact]
    public void IsAuthenticated_InitiallyFalse()
    {
        // Arrange & Act
        var viewModel = TestHelper.CreateViewModel();

        // Assert
        Assert.False(viewModel.IsAuthenticated);
    }

    [Fact]
    public void IsAuthenticated_ReflectsAuthServiceState()
    {
        // Arrange
        var mockAuthService = TestHelper.CreateMockAuthService(isAuthenticated: true);
        var viewModel = TestHelper.CreateViewModel(authService: mockAuthService);

        // Assert
        Assert.True(viewModel.IsAuthenticated);
    }

    [Fact]
    public void SystemToggleButtonText_ReturnsCorrectTextWhenRunning()
    {
        // Arrange
        var mockSystemControl = TestHelper.CreateMockSystemControlService();
        mockSystemControl.SetupGet(x => x.IsSystemRunning).Returns(true);
        var viewModel = TestHelper.CreateViewModel(systemControl: mockSystemControl);

        // Assert
        Assert.Equal("Stop System", viewModel.SystemControl.SystemToggleButtonText);
    }

    [Fact]
    public void SystemToggleButtonText_ReturnsCorrectTextWhenStopped()
    {
        // Arrange
        var mockSystemControl = TestHelper.CreateMockSystemControlService();
        mockSystemControl.SetupGet(x => x.IsSystemRunning).Returns(false);
        var viewModel = TestHelper.CreateViewModel(systemControl: mockSystemControl);

        // Assert
        Assert.Equal("Start System", viewModel.SystemControl.SystemToggleButtonText);
    }

    [Fact]
    public void BoostRemainingText_ReturnsOffWhenZero()
    {
        // Arrange
        var mockSystemControl = TestHelper.CreateMockSystemControlService();
        var status = TestHelper.CreateMockSystemStatus(boostRemaining: 0);
        mockSystemControl.SetupGet(x => x.CurrentStatus).Returns(status);
        var viewModel = TestHelper.CreateViewModel(systemControl: mockSystemControl);

        // Assert
        Assert.Equal("Off", viewModel.BoostMode.BoostRemainingText);
    }

    [Fact]
    public void BoostRemainingText_FormatsTimeCorrectly()
    {
        // Arrange
        var mockSystemControl = TestHelper.CreateMockSystemControlService();
        var status = TestHelper.CreateMockSystemStatus(boostRemaining: 125); // 2:05
        mockSystemControl.SetupGet(x => x.CurrentStatus).Returns(status);
        var viewModel = TestHelper.CreateViewModel(systemControl: mockSystemControl);

        // Assert
        Assert.Equal("02:05", viewModel.BoostMode.BoostRemainingText);
    }

    [Fact]
    public void IsBoostActive_ReturnsTrueWhenRemainingGreaterThanZero()
    {
        // Arrange
        var mockSystemControl = TestHelper.CreateMockSystemControlService();
        var status = TestHelper.CreateMockSystemStatus(boostRemaining: 300);
        mockSystemControl.SetupGet(x => x.CurrentStatus).Returns(status);
        var viewModel = TestHelper.CreateViewModel(systemControl: mockSystemControl);

        // Assert
        Assert.True(viewModel.BoostMode.IsBoostActive);
    }

    [Fact]
    public void IsBoostActive_ReturnsFalseWhenRemainingIsZero()
    {
        // Arrange
        var mockSystemControl = TestHelper.CreateMockSystemControlService();
        var status = TestHelper.CreateMockSystemStatus(boostRemaining: 0);
        mockSystemControl.SetupGet(x => x.CurrentStatus).Returns(status);
        var viewModel = TestHelper.CreateViewModel(systemControl: mockSystemControl);

        // Assert
        Assert.False(viewModel.BoostMode.IsBoostActive);
    }

    [Fact]
    public async Task ToggleSystemCommand_CallsSystemControl()
    {
        // Arrange
        var mockAuthService = TestHelper.CreateMockAuthService(isAuthenticated: true);
        var mockSystemControl = TestHelper.CreateMockSystemControlService();
        mockSystemControl.Setup(x => x.StartSystemAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        mockSystemControl.SetupGet(x => x.IsSystemRunning).Returns(false);
        
        var viewModel = TestHelper.CreateViewModel(mockAuthService, mockSystemControl);

        try
        {
            // Act - Execute ReactiveCommand and convert to Task
            await viewModel.SystemControl.ToggleSystemCommand.Execute().ToTask();

            // Assert
            mockSystemControl.Verify(x => x.StartSystemAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task SetBoostCommand_WithParameter_CallsSystemControl()
    {
        // Arrange
        var mockAuthService = TestHelper.CreateMockAuthService(isAuthenticated: true);
        var mockSystemControl = TestHelper.CreateMockSystemControlService();
        mockSystemControl.Setup(x => x.SetBoostAsync(900, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        
        var viewModel = TestHelper.CreateViewModel(mockAuthService, mockSystemControl);

        try
        {
            // Act - Execute ReactiveCommand with parameter and convert to Task
            await viewModel.BoostMode.SetBoostCommand.Execute(900).ToTask();

            // Assert
            mockSystemControl.Verify(x => x.SetBoostAsync(900, It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            viewModel.Dispose();
        }
    }

    [Fact]
    public void Commands_RaiseCanExecuteChanged_WhenAuthenticationChanges()
    {
        // Arrange
        var mockAuthService = TestHelper.CreateMockAuthService(isAuthenticated: false);
        var mockSystemControl = TestHelper.CreateMockSystemControlService();
        var viewModel = TestHelper.CreateViewModel(mockAuthService, mockSystemControl);

        var canExecuteChangedRaised = false;
        var subscription = viewModel.SystemControl.ToggleSystemCommand.CanExecute.Subscribe(_ => 
            canExecuteChangedRaised = true);

        // Act
        mockAuthService.SetupGet(x => x.IsAuthenticated).Returns(true);
        mockAuthService.Raise(x => x.AuthenticationChanged += null, mockAuthService.Object, true);

        // Assert
        Assert.True(canExecuteChangedRaised);
        subscription.Dispose();
    }

    [Theory]
    [InlineData(50, "50 m³/h")]
    [InlineData(100, "100 m³/h")]
    [InlineData(220, "220 m³/h")]
    public void FlowSpeedText_DisplaysCorrectFormat(int flowSpeed, string expectedText)
    {
        // Arrange
        var viewModel = TestHelper.CreateViewModel();

        // Act
        viewModel.FlowSpeed.FlowSpeed = flowSpeed;

        // Assert
        Assert.Equal(expectedText, viewModel.FlowSpeed.FlowSpeedText);
    }
}
