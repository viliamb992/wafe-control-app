using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;
using WafeControl.Core;
using WafeControl.Core.Diagnostics;
using WafeControl.Core.Services;

namespace WafeControl.WinUI.Services;

/// <summary>
/// Updates from GitHub Releases with Velopack: the new version is downloaded and prepared next to the running one,
/// and a restart (or the next exit) switches over in about a second. Only works in an installed app; a build run
/// from the IDE reports <see cref="IsSupported"/> false.
/// To try it locally, set WAFE_UPDATE_SOURCE to a folder with packages from <c>vpk pack</c>.
/// </summary>
public sealed class VelopackUpdateService : IUpdateService
{
    private const string SourceOverrideVariable = "WAFE_UPDATE_SOURCE";

    private readonly ILogger<VelopackUpdateService> _logger;
    private readonly UpdateManager? _installed;
    private UpdateManager? _checked;
    private UpdateInfo? _update;
    private VelopackAsset? _prepared;

    public VelopackUpdateService(ILogger<VelopackUpdateService> logger)
    {
        _logger = logger;
        try
        {
            _installed = CreateManager(includePrereleases: false);
            if (!_installed.IsInstalled)
            {
                _installed = null;
                return;
            }

            // Downloaded in an earlier session and not applied yet.
            _prepared = _installed.UpdatePendingRestart;
            if (_prepared is not null)
            {
                AvailableVersion = _prepared.Version.ToString();
                ReleaseNotes = _prepared.NotesMarkdown;
                Stage = UpdateStage.ReadyToRestart;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Updates are not available");
            _installed = null;
        }
    }

    public bool IsSupported => _installed is not null;

    public UpdateStage Stage { get; private set; }

    public string? AvailableVersion { get; private set; }

    public int DownloadProgress { get; private set; }

    public string? ReleaseNotes { get; private set; }

    public event EventHandler? Changed;

    /// <summary>
    /// "velopack" when installed, "dev" otherwise; for logs and reports.
    /// </summary>
    public string InstallType => IsSupported ? "velopack" : "dev";

    public async Task CheckAsync(bool includePrereleases, CancellationToken cancellationToken = default)
    {
        if (!IsSupported || Stage is UpdateStage.Downloading or UpdateStage.ReadyToRestart)
            return;

        Set(UpdateStage.Checking);
        try
        {
            var manager = CreateManager(includePrereleases);
            var update = await manager.CheckForUpdatesAsync();
            _checked = manager;
            _update = update;
            if (update is null)
            {
                _logger.LogInformation("No update available (current {Version})", AppVersion.Current);
                AvailableVersion = null;
                ReleaseNotes = null;
                Set(UpdateStage.None);
                return;
            }

            AvailableVersion = update.TargetFullRelease.Version.ToString();
            ReleaseNotes = update.TargetFullRelease.NotesMarkdown;
            _logger.LogInformation("Update {Version} available", AvailableVersion);
            Set(UpdateStage.Available);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Offline, GitHub unavailable, rate limited: try again at the next check.
            _logger.LogWarning("Update check failed: {Reason}", ex.Message);
            Set(UpdateStage.Failed);
        }
    }

    public async Task DownloadAsync(CancellationToken cancellationToken = default)
    {
        if (_checked is null || _update is null || Stage != UpdateStage.Available)
            return;

        DownloadProgress = 0;
        Set(UpdateStage.Downloading);
        try
        {
            await _checked.DownloadUpdatesAsync(_update, progress =>
            {
                DownloadProgress = progress;
                Changed?.Invoke(this, EventArgs.Empty);
            }, cancellationToken);

            _prepared = _update.TargetFullRelease;
            _logger.LogInformation("Update {Version} downloaded and ready", AvailableVersion);
            Set(UpdateStage.ReadyToRestart);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("Update download failed: {Reason}", ex.Message);
            Set(UpdateStage.Available);
        }
    }

    public void RestartToApply(bool startInTray)
    {
        if (_prepared is null || (_checked ?? _installed) is not { } manager)
            return;

        _logger.LogInformation("Restarting to apply {Version}", _prepared.Version);
        manager.ApplyUpdatesAndRestart(_prepared, startInTray ? [App.TrayArgument] : []);
    }

    public void ApplyOnExit()
    {
        if (_prepared is null || (_checked ?? _installed) is not { } manager)
            return;

        _logger.LogInformation("Applying {Version} after exit", _prepared.Version);
        manager.WaitExitThenApplyUpdates(_prepared, silent: true, restart: false);
    }

    private static UpdateManager CreateManager(bool includePrereleases) =>
        Environment.GetEnvironmentVariable(SourceOverrideVariable) is { Length: > 0 } folder
            ? new UpdateManager(folder)
            : new UpdateManager(new GithubSource(ProblemReport.RepositoryUrl, accessToken: null, prerelease: includePrereleases));

    private void Set(UpdateStage stage)
    {
        Stage = stage;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
