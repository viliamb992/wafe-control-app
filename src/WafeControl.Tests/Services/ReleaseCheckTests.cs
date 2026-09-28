using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using WafeControl.Core.Services;
using WafeControl.Core.ViewModels;
using WafeControl.Tests.Localization;

namespace WafeControl.Tests.Services;

public class ReleaseVersionTests
{
    [Theory]
    [InlineData("1.3.0", "1.2.0")]
    [InlineData("1.10.0", "1.9.9")]
    [InlineData("2.0.0", "1.99.99")]
    [InlineData("1.3.0", "1.3.0-beta.2")]
    [InlineData("1.3.0-beta.10", "1.3.0-beta.2")]
    [InlineData("1.3.0-rc.1", "1.3.0-beta.9")]
    [InlineData("1.3.0-beta", "1.3.0-1")]
    [InlineData("1.3.0-beta.1.1", "1.3.0-beta.1")]
    public void Newer_SortsAfterOlder(string newer, string older)
    {
        var a = ReleaseVersion.TryParse(newer)!;
        var b = ReleaseVersion.TryParse(older)!;

        Assert.True(a.CompareTo(b) > 0);
        Assert.True(b.CompareTo(a) < 0);
    }

    [Theory]
    [InlineData("1.3.0+abc123", "1.3.0")]
    [InlineData("1.3.0-beta.1", "1.3.0-beta.1")]
    public void Parse_KeepsVersionDropsBuild(string text, string expected) =>
        Assert.Equal(expected, ReleaseVersion.TryParse(text)!.ToString());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.3")]
    [InlineData("v1.3.0")]
    [InlineData("1.3.0-")]
    [InlineData("1.x.0")]
    public void Parse_RejectsOtherText(string? text) => Assert.Null(ReleaseVersion.TryParse(text));
}

public class GitHubReleaseFeedTests
{
    private const string Releases = """
        [
          { "tag_name": "v1.4.0", "draft": false, "prerelease": false, "html_url": "https://github.com/o/r/releases/tag/v1.4.0", "assets": [] },
          { "tag_name": "android-v1.4.0-beta.1", "draft": false, "prerelease": true, "html_url": "https://github.com/o/r/releases/tag/android-v1.4.0-beta.1", "assets": [] },
          { "tag_name": "android-v1.5.0", "draft": true, "prerelease": false, "html_url": "https://github.com/o/r/releases/tag/android-v1.5.0", "assets": [] },
          { "tag_name": "android-v1.3.0", "draft": false, "prerelease": false, "html_url": "https://github.com/o/r/releases/tag/android-v1.3.0",
            "assets": [
              { "name": "symbols.zip", "browser_download_url": "https://github.com/o/r/releases/download/android-v1.3.0/symbols.zip" },
              { "name": "WafeControl-1.3.0-android.apk", "browser_download_url": "https://github.com/o/r/releases/download/android-v1.3.0/WafeControl-1.3.0-android.apk" }
            ] },
          { "tag_name": "android-v1.2.0", "draft": false, "prerelease": false, "html_url": "https://github.com/o/r/releases/tag/android-v1.2.0", "assets": [] }
        ]
        """;

    private static AppRelease? Latest(bool includePrereleases)
    {
        using var json = JsonDocument.Parse(Releases);
        return GitHubReleaseFeed.Latest(json.RootElement, ReleaseCheckViewModel.AndroidTagPrefix, includePrereleases);
    }

    [Fact]
    public void Latest_SkipsOtherAppsDraftsAndBetas_AndFindsTheApk()
    {
        var release = Latest(includePrereleases: false);

        Assert.Equal("1.3.0", release?.Version.ToString());
        Assert.Equal("https://github.com/o/r/releases/download/android-v1.3.0/WafeControl-1.3.0-android.apk", release?.Download?.ToString());
        Assert.Equal("https://github.com/o/r/releases/tag/android-v1.3.0", release?.Page.ToString());
    }

    [Fact]
    public void Latest_WithBetas_TakesTheNewerBeta()
    {
        var release = Latest(includePrereleases: true);

        Assert.Equal("1.4.0-beta.1", release?.Version.ToString());
        Assert.Null(release?.Download);
    }

    [Fact]
    public void Latest_NoMatchingRelease_IsNull()
    {
        using var json = JsonDocument.Parse("[]");

        Assert.Null(GitHubReleaseFeed.Latest(json.RootElement, "android-v", includePrereleases: true));
    }
}

public class ReleaseCheckViewModelTests
{
    private static readonly AppRelease Release130 = new(
        new ReleaseVersion(1, 3, 0), new Uri("https://example.com/page"), new Uri("https://example.com/app.apk"));

    private readonly IReleaseFeed _feed = Substitute.For<IReleaseFeed>();
    private readonly InMemorySettingsStore _settings = new();
    private readonly FakeTimeProvider _time = new();

    private ReleaseCheckViewModel Create(string currentVersion = "1.2.0")
    {
        var sut = new ReleaseCheckViewModel(_feed, _settings, NullLogger<ReleaseCheckViewModel>.Instance, _time, currentVersion);
        sut.Enable(ReleaseCheckViewModel.AndroidTagPrefix);
        return sut;
    }

    private void Latest(AppRelease? release) =>
        _feed.GetLatestAsync("android-v", Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(release);

    [Fact]
    public async Task NotEnabled_DoesNotCheck()
    {
        var sut = new ReleaseCheckViewModel(_feed, _settings, NullLogger<ReleaseCheckViewModel>.Instance, _time, "1.2.0");

        await sut.CheckAsync();

        Assert.False(sut.IsEnabled);
        await _feed.DidNotReceiveWithAnyArgs().GetLatestAsync(default!, default, default);
    }

    [Fact]
    public async Task NewerRelease_ShowsBannerAndOpensTheApk()
    {
        Latest(Release130);
        var sut = Create();
        Uri? opened = null;
        sut.OpenRequested += (_, uri) => opened = uri;

        await sut.CheckAsync();
        sut.DownloadCommand.Execute(null);

        Assert.Equal("Version 1.3.0 is available", sut.BannerTitle);
        Assert.Equal("Version 1.3.0 is available", sut.StatusText);
        Assert.NotNull(sut.BannerMessage);
        Assert.Equal(Release130.Download, opened);
    }

    [Fact]
    public async Task NoApk_OpensTheReleasePage()
    {
        Latest(Release130 with { Download = null });
        var sut = Create();
        Uri? opened = null;
        sut.OpenRequested += (_, uri) => opened = uri;

        await sut.CheckAsync();
        sut.DownloadCommand.Execute(null);

        Assert.Equal(Release130.Page, opened);
    }

    [Theory]
    [InlineData("1.3.0")]
    [InlineData("1.4.0")]
    public async Task SameOrOlderRelease_IsUpToDate(string current)
    {
        Latest(Release130);
        var sut = Create(current);

        await sut.CheckAsync();

        Assert.False(sut.IsAvailable);
        Assert.Null(sut.BannerTitle);
        Assert.StartsWith("Up to date", sut.StatusText);
    }

    [Fact]
    public async Task BuildWithoutReleaseVersion_IsOlderThanAnyRelease()
    {
        Latest(Release130);
        var sut = Create("dev");

        await sut.CheckAsync();

        Assert.True(sut.IsAvailable);
    }

    [Theory]
    [InlineData(false, "1.2.0", false)]
    [InlineData(true, "1.2.0", true)]
    [InlineData(false, "1.3.0-beta.1", true)]
    public async Task Betas_WhenChosenOrAlreadyOnABeta(bool betaUpdates, string current, bool expected)
    {
        _settings.Settings = new UserSettings { BetaUpdates = betaUpdates };
        var sut = Create(current);

        await sut.CheckAsync();

        await _feed.Received(1).GetLatestAsync("android-v", expected, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Dismiss_HidesBannerUntilANewerVersion()
    {
        Latest(Release130);
        var sut = Create();
        await sut.CheckAsync();

        sut.DismissCommand.Execute(null);

        Assert.Null(sut.BannerTitle);
        Assert.Equal("Version 1.3.0 is available", sut.StatusText);
        Assert.Equal("1.3.0", _settings.Settings.DismissedUpdateVersion);

        // The next start remembers it; a newer version shows again.
        var next = Create();
        await next.CheckAsync();
        Assert.Null(next.BannerTitle);

        Latest(Release130 with { Version = new ReleaseVersion(1, 3, 1) });
        await next.CheckAsync();
        Assert.Equal("Version 1.3.1 is available", next.BannerTitle);
    }

    [Fact]
    public async Task Failure_SaysSoAndKeepsWhatWasFound()
    {
        Latest(Release130);
        var sut = Create();
        await sut.CheckAsync();
        _feed.GetLatestAsync(default!, default, default).ThrowsAsyncForAnyArgs(new HttpRequestException("offline"));

        await sut.CheckAsync();

        Assert.Equal("Couldn't check for updates.", sut.StatusText);
        Assert.Equal("Version 1.3.0 is available", sut.BannerTitle);
    }

    [Fact]
    public async Task CheckIfDue_AtMostEveryTwelveHours()
    {
        Latest(null);
        var sut = Create();

        await sut.CheckIfDueAsync();
        _time.Advance(ReleaseCheckViewModel.CheckInterval - TimeSpan.FromMinutes(1));
        await sut.CheckIfDueAsync();
        _time.Advance(TimeSpan.FromMinutes(1));
        await sut.CheckIfDueAsync();

        await _feed.ReceivedWithAnyArgs(2).GetLatestAsync(default!, default, default);
    }
}
