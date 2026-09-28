using Microsoft.Extensions.Logging.Abstractions;
using WafeControl.Core;
using WafeControl.Core.Services;
using WafeControl.Core.ViewModels;
using WafeControl.Tests.Localization;

namespace WafeControl.Tests.ViewModels;

public class UpdateViewModelTests
{
    private readonly FakeUpdateService _updates = new();
    private readonly InMemorySettingsStore _store = new();
    private readonly UpdateViewModel _sut;

    public UpdateViewModelTests() => _sut = new UpdateViewModel(_updates, _store, NullLogger<UpdateViewModel>.Instance);

    [Fact]
    public async Task NewVersion_IsDownloaded_AndTheButtonAsksForARestart()
    {
        _updates.Offer = "1.3.0";

        await _sut.CheckAsync();

        Assert.Equal(1, _updates.Downloads);
        Assert.True(_sut.IsButtonVisible);
        Assert.True(_sut.IsReady);
        Assert.Equal("Restart to update", _sut.ButtonText);
        Assert.Equal("Version 1.3.0 is ready", _sut.Title);
    }

    [Fact]
    public async Task AutomaticDownloadsOff_TheButtonOffersTheUpdate()
    {
        _store.Save(new UserSettings { AutoDownloadUpdates = false });
        _updates.Offer = "1.3.0";

        await _sut.CheckAsync();

        Assert.Equal(0, _updates.Downloads);
        Assert.Equal("Update", _sut.ButtonText);

        await _sut.PrimaryCommand.ExecuteAsync(null);
        Assert.True(_sut.IsReady);
    }

    [Fact]
    public async Task Ready_ThePrimaryActionAsksTheWindowToRestart()
    {
        _updates.Offer = "1.3.0";
        await _sut.CheckAsync();
        var asked = false;
        _sut.RestartRequested += (_, _) => asked = true;

        await _sut.PrimaryCommand.ExecuteAsync(null);

        Assert.True(asked);
    }

    [Fact]
    public async Task UpToDate_NoButton()
    {
        await _sut.CheckAsync();

        Assert.False(_sut.IsButtonVisible);
        Assert.StartsWith("Up to date", _sut.StatusText);
    }

    [Fact]
    public async Task BetaSetting_IsPassedToTheCheck()
    {
        _store.Save(new UserSettings { BetaUpdates = true });

        await _sut.CheckAsync();

        Assert.True(_updates.LastIncludedPrereleases);
    }

    [Fact]
    public void NotInstalled_SaysSoAndNeverChecks()
    {
        _updates.Supported = false;

        _sut.Start();

        Assert.Equal("Updates work in the installed app only.", _sut.StatusText);
        Assert.Equal(0, _updates.Checks);
    }

    [Fact]
    public void UpdatedVersion_IsReportedOnceAfterAnUpdate()
    {
        _store.Save(new UserSettings { LastRunVersion = "0.9.0" });

        Assert.Equal(AppVersion.Current, _sut.TakeUpdatedVersion());
        Assert.Null(_sut.TakeUpdatedVersion());
    }

    [Fact]
    public void FirstRun_IsNotAnUpdate()
    {
        Assert.Null(_sut.TakeUpdatedVersion());
        Assert.Equal(AppVersion.Current, _store.Load().LastRunVersion);
    }

    private sealed class FakeUpdateService : IUpdateService
    {
        public bool Supported { get; set; } = true;
        public string? Offer { get; set; }
        public int Checks { get; private set; }
        public int Downloads { get; private set; }
        public bool LastIncludedPrereleases { get; private set; }

        public bool IsSupported => Supported;
        public UpdateStage Stage { get; private set; }
        public string? AvailableVersion { get; private set; }
        public int DownloadProgress => 0;
        public string? ReleaseNotes => null;
        public event EventHandler? Changed;

        public Task CheckAsync(bool includePrereleases, CancellationToken cancellationToken = default)
        {
            Checks++;
            LastIncludedPrereleases = includePrereleases;
            AvailableVersion = Offer;
            Set(Offer is null ? UpdateStage.None : UpdateStage.Available);
            return Task.CompletedTask;
        }

        public Task DownloadAsync(CancellationToken cancellationToken = default)
        {
            Downloads++;
            Set(UpdateStage.ReadyToRestart);
            return Task.CompletedTask;
        }

        public void RestartToApply(bool startInTray)
        {
        }

        public void ApplyOnExit()
        {
        }

        private void Set(UpdateStage stage)
        {
            Stage = stage;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
