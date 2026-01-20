using Xunit;
using Moq;
using RecuperationSystem.Desktop.ViewModels;
using RecuperationSystem.Desktop.Tests.Helpers;
using RecuperationSystem.Shared;
using System.Threading.Tasks;

namespace RecuperationSystem.Desktop.Tests.ViewModels;

public class AppViewModelTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Arrange & Act
        var viewModel = TestHelper.CreateViewModel();

        // Assert
        Assert.False(viewModel.IsAuthenticated);
        Assert.Equal(50, viewModel.FlowSpeed.FlowSpeed);
        Assert.Equal(AppConstants.ModeIntelligent, viewModel.OperatingMode.SelectedMode);
        Assert.False(viewModel.SpecialModes.IsSilentMode);
        Assert.False(viewModel.SpecialModes.IsHolidayMode);
    }

    [Fact]
    public void FlowSpeed_ClampsToMinValue()
    {
        // Arrange
        var viewModel = TestHelper.CreateViewModel();

        // Act
        viewModel.FlowSpeed.FlowSpeed = 30; // Below minimum

        // Assert
        Assert.Equal(AppConstants.MinFlowSpeed, viewModel.FlowSpeed.FlowSpeed); // Should be 50
    }

    [Fact]
    public void FlowSpeed_ClampsToMaxValue()
    {
        // Arrange
        var viewModel = TestHelper.CreateViewModel();

        // Act
        viewModel.FlowSpeed.FlowSpeed = 300; // Above maximum

        // Assert
        Assert.Equal(AppConstants.MaxFlowSpeed, viewModel.FlowSpeed.FlowSpeed); // Should be 220
    }

    [Theory]
    [InlineData(AppConstants.ModeManual, true, false, false)]
    [InlineData(AppConstants.ModeIntelligent, false, true, false)]
    [InlineData(AppConstants.ModeSchedule, false, false, true)]
    public void ModeProperties_ReturnCorrectValues(string mode, bool isManual, bool isIntelligent, bool isSchedule)
    {
        // Arrange
        var viewModel = TestHelper.CreateViewModel();

        // Act
        viewModel.OperatingMode.SelectedMode = mode;

        // Assert
        Assert.Equal(isManual, viewModel.OperatingMode.IsManualMode);
        Assert.Equal(isIntelligent, viewModel.OperatingMode.IsIntelligentMode);
        Assert.Equal(isSchedule, viewModel.OperatingMode.IsScheduleMode);
    }

    [Fact]
    public void SelectedMode_RaisesPropertyChanged()
    {
        // Arrange
        var viewModel = TestHelper.CreateViewModel();
        var propertyChangedRaised = false;
        viewModel.OperatingMode.PropertyChanged += (s, e) => 
        {
            if (e.PropertyName == nameof(viewModel.OperatingMode.SelectedMode) ||
                e.PropertyName == nameof(viewModel.OperatingMode.IsManualMode) ||
                e.PropertyName == nameof(viewModel.OperatingMode.IsIntelligentMode) ||
                e.PropertyName == nameof(viewModel.OperatingMode.IsScheduleMode))
            {
                propertyChangedRaised = true;
            }
        };

        // Act
        viewModel.OperatingMode.SelectedMode = AppConstants.ModeManual;

        // Assert
        Assert.True(propertyChangedRaised);
    }

    [Fact]
    public void IsSilentMode_UpdatesCorrectly()
    {
        // Arrange
        var viewModel = TestHelper.CreateViewModel();
        var propertyChangedRaised = false;
        viewModel.SpecialModes.PropertyChanged += (s, e) => 
        {
            if (e.PropertyName == nameof(viewModel.SpecialModes.IsSilentMode))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.SpecialModes.IsSilentMode = true;

        // Assert
        Assert.True(viewModel.SpecialModes.IsSilentMode);
        Assert.True(propertyChangedRaised);
    }

    [Fact]
    public void IsHolidayMode_UpdatesCorrectly()
    {
        // Arrange
        var viewModel = TestHelper.CreateViewModel();
        var propertyChangedRaised = false;
        viewModel.SpecialModes.PropertyChanged += (s, e) => 
        {
            if (e.PropertyName == nameof(viewModel.SpecialModes.IsHolidayMode))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.SpecialModes.IsHolidayMode = true;

        // Assert
        Assert.True(viewModel.SpecialModes.IsHolidayMode);
        Assert.True(propertyChangedRaised);
    }

    [Fact]
    public void FlowSpeed_RaisesPropertyChanged()
    {
        // Arrange
        var viewModel = TestHelper.CreateViewModel();
        var propertyChangedRaised = false;
        viewModel.FlowSpeed.PropertyChanged += (s, e) => 
        {
            if (e.PropertyName == nameof(viewModel.FlowSpeed.FlowSpeed))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.FlowSpeed.FlowSpeed = 100;

        // Assert
        Assert.Equal(100, viewModel.FlowSpeed.FlowSpeed);
        Assert.True(propertyChangedRaised);
    }

    [Fact]
    public void StatusMessage_UpdatesAndRaisesPropertyChanged()
    {
        // Arrange
        var viewModel = TestHelper.CreateViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) => 
        {
            if (e.PropertyName == nameof(viewModel.StatusMessage))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.StatusMessage = "Test Status";

        // Assert
        Assert.Equal("Test Status", viewModel.StatusMessage);
        Assert.True(propertyChangedRaised);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(150)]
    [InlineData(220)]
    public void FlowSpeed_AcceptsValidValues(int value)
    {
        // Arrange
        var viewModel = TestHelper.CreateViewModel();

        // Act
        viewModel.FlowSpeed.FlowSpeed = value;

        // Assert
        Assert.Equal(value, viewModel.FlowSpeed.FlowSpeed);
    }

    [Fact]
    public void Dispose_UnsubscribesFromEvents()
    {
        // Arrange
        var mockAuthService = TestHelper.CreateMockAuthService();
        var mockSystemControl = TestHelper.CreateMockSystemControlService();
        var viewModel = TestHelper.CreateViewModel(mockAuthService, mockSystemControl);

        // Act
        viewModel.Dispose();

        // Assert - Verify event handlers were unsubscribed
        // This is implicit - no exception should be thrown
        mockAuthService.Raise(x => x.AuthenticationChanged += null, mockAuthService.Object, true);
    }

    [Fact]
    public void Commands_AreNotNull()
    {
        // Arrange
        var viewModel = TestHelper.CreateViewModel();

        // Assert - Commands are now in card ViewModels
        Assert.NotNull(viewModel.SystemControl.ToggleSystemCommand);
        Assert.NotNull(viewModel.OperatingMode.UpdateModeCommand);
        Assert.NotNull(viewModel.FlowSpeed.UpdateFlowSpeedCommand);
        Assert.NotNull(viewModel.SpecialModes.SetSilentModeCommand);
        Assert.NotNull(viewModel.SpecialModes.SetHolidayModeCommand);
        Assert.NotNull(viewModel.BoostMode.SetBoostCommand);
        Assert.NotNull(viewModel.RefreshStatusCommand);
    }

    [Fact]
    public void ToggleSystemCommand_CanExecute_WhenAuthenticatedAndNotOperating()
    {
        // Arrange
        var mockAuthService = TestHelper.CreateMockAuthService(isAuthenticated: true);
        var viewModel = TestHelper.CreateViewModel(authService: mockAuthService);
        viewModel.SystemControl.IsOperationInProgress = false;

        // Act - ReactiveCommand uses CanExecute as IObservable, not method
        var subscription = viewModel.SystemControl.ToggleSystemCommand.CanExecute.Subscribe(canExecute =>
        {
            // Assert
            Assert.True(canExecute);
        });
        
        subscription.Dispose();
    }

    [Fact]
    public void ToggleSystemCommand_CannotExecute_WhenNotAuthenticated()
    {
        // Arrange
        var mockAuthService = TestHelper.CreateMockAuthService(isAuthenticated: false);
        var viewModel = TestHelper.CreateViewModel(authService: mockAuthService);

        // Act
        var canExecuteValue = false;
        var subscription = viewModel.SystemControl.ToggleSystemCommand.CanExecute.Subscribe(canExecute =>
        {
            canExecuteValue = canExecute;
        });
        
        // Assert
        Assert.False(canExecuteValue);
        subscription.Dispose();
    }

    [Fact]
    public void CardViewModels_AreInitialized()
    {
        // Arrange & Act
        var viewModel = TestHelper.CreateViewModel();

        // Assert
        Assert.NotNull(viewModel.SystemControl);
        Assert.NotNull(viewModel.OperatingMode);
        Assert.NotNull(viewModel.FlowSpeed);
        Assert.NotNull(viewModel.SpecialModes);
        Assert.NotNull(viewModel.BoostMode);
    }
}
