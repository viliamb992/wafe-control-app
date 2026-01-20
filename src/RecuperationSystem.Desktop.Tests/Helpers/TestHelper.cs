using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using RecuperationSystem.Desktop.Configuration;
using RecuperationSystem.Desktop.Services;
using RecuperationSystem.Desktop.ViewModels;
using RecuperationSystem.Shared.Models;
using RecuperationSystem.Shared.Services;
using System.Reactive.Concurrency;
using ReactiveUI;

namespace RecuperationSystem.Desktop.Tests.Helpers;

public static class TestHelper
{
    static TestHelper()
    {
        // Configure ReactiveUI to use immediate scheduler for tests
        // This prevents it from trying to use the UI thread dispatcher
        RxApp.MainThreadScheduler = ImmediateScheduler.Instance;
        RxApp.TaskpoolScheduler = ImmediateScheduler.Instance;
    }

    public static AppViewModel CreateViewModel(
        Mock<IAuthenticationService>? authService = null,
        Mock<ISystemControlService>? systemControl = null,
        IOptions<PollingConfiguration>? pollingConfig = null)
    {
        authService ??= CreateMockAuthService();
        systemControl ??= CreateMockSystemControlService();
        pollingConfig ??= CreateMockPollingConfig();

        var viewModel = new AppViewModel(
            authService.Object,
            systemControl.Object,
            pollingConfig);
            
        // Give time for AutoLoginAsync to complete
        // The mock returns false, so auto-refresh won't start
        Task.Delay(100).Wait();
        
        return viewModel;
    }

    private static IOptions<PollingConfiguration> CreateMockPollingConfig()
    {
        return Options.Create(new PollingConfiguration
        {
            StatusRefreshIntervalMs = 3000,
            StateChangeIntervalMs = 1000,
            StateChangeTimeoutSeconds = 15
        });
    }

    public static Mock<IAuthenticationService> CreateMockAuthService(bool isAuthenticated = false)
    {
        var mock = new Mock<IAuthenticationService>();
        mock.SetupGet(x => x.IsAuthenticated).Returns(isAuthenticated);
        mock.SetupGet(x => x.Username).Returns("testuser");
        mock.SetupGet(x => x.Password).Returns("testpass");

        // Mock async methods to prevent actual execution during tests
        // TryAutoLoginAsync MUST return false to prevent auto-refresh loop from starting
        mock.Setup(x => x.TryAutoLoginAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mock.Setup(x => x.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        return mock;
    }

    public static Mock<ISystemControlService> CreateMockSystemControlService()
    {
        var mock = new Mock<ISystemControlService>();
        mock.SetupGet(x => x.CurrentStatus).Returns((SystemStatus?)null);
        mock.SetupGet(x => x.IsSystemRunning).Returns(false);
        mock.SetupGet(x => x.IsSystemStopped).Returns(true);
        mock.SetupGet(x => x.IsSystemOnline).Returns(false);

        // Mock async methods to prevent actual execution during tests
        mock.Setup(x => x.RefreshStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemStatus?)null);
        mock.Setup(x => x.SetFlowSpeedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mock.Setup(x => x.SetAuthorityModeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mock.Setup(x => x.SetSilentModeAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mock.Setup(x => x.SetHolidayModeAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mock.Setup(x => x.SetBoostAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mock.Setup(x => x.StartSystemAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mock.Setup(x => x.StopSystemAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        return mock;
    }

    public static SystemStatus CreateMockSystemStatus(
        int flowRequested = 50,
        int flowCurrent = 50,
        int boostRemaining = 0,
        bool silentActive = false,
        bool holidayActive = false,
        string authority = "intelligent")
    {
        return new SystemStatus
        {
            FlowRequested = flowRequested,
            FlowCurrent = flowCurrent,
            BoostRemaining = boostRemaining,
            SilentActive = silentActive,
            HolidayActive = holidayActive,
            Authority = authority,
            Temperatures = new List<double?> { 20.0, 21.0, 22.0, 19.0 },
            Co2 = 400
        };
    }
}
