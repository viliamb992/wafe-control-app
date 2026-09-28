using System.Net.Http.Headers;
using System.Text.Json;
using WafeControl.Core.Diagnostics;

namespace WafeControl.Core.Services;

/// <summary>
/// A published release: its version, its GitHub page and the file to install (the APK on Android), if attached.
/// </summary>
public sealed record AppRelease(ReleaseVersion Version, Uri Page, Uri? Download);

/// <summary>
/// Where the app's releases are published.
/// </summary>
public interface IReleaseFeed
{
    /// <summary>
    /// The newest release whose tag starts with <paramref name="tagPrefix"/> (e.g. "android-v"); null when there
    /// is none. Throws when the releases can't be read (no network, GitHub unavailable).
    /// </summary>
    Task<AppRelease?> GetLatestAsync(string tagPrefix, bool includePrereleases, CancellationToken cancellationToken = default);
}

/// <summary>
/// Releases from the project's GitHub repository, read without signing in (60 requests an hour per address,
/// plenty for a check every few hours).
/// </summary>
public sealed class GitHubReleaseFeed(IHttpClientFactory httpClients) : IReleaseFeed
{
    public const string HttpClientName = "github";

    /// <summary>
    /// The API address of <see cref="ProblemReport.RepositoryUrl"/>'s releases, newest first.
    /// </summary>
    private static readonly Uri ReleasesUri = new(
        ProblemReport.RepositoryUrl.Replace("https://github.com/", "https://api.github.com/repos/", StringComparison.Ordinal) + "/releases?per_page=30");

    /// <summary>
    /// Sets up the HTTP client GitHub expects: a User-Agent and its JSON media type.
    /// </summary>
    public static void Configure(HttpClient client)
    {
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("WafeControl", AppVersion.Current));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.Timeout = TimeSpan.FromSeconds(20);
    }

    public async Task<AppRelease?> GetLatestAsync(string tagPrefix, bool includePrereleases, CancellationToken cancellationToken = default)
    {
        using var client = httpClients.CreateClient(HttpClientName);
        using var response = await client.GetAsync(ReleasesUri, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        return Latest(json.RootElement, tagPrefix, includePrereleases);
    }

    /// <summary>
    /// Picks the newest release from GitHub's list; drafts and other apps' tags are skipped.
    /// </summary>
    internal static AppRelease? Latest(JsonElement releases, string tagPrefix, bool includePrereleases)
    {
        AppRelease? latest = null;
        foreach (var release in releases.EnumerateArray())
        {
            var tag = String(release, "tag_name");
            if (tag is null || !tag.StartsWith(tagPrefix, StringComparison.Ordinal) || Bool(release, "draft"))
                continue;

            var version = ReleaseVersion.TryParse(tag[tagPrefix.Length..]);
            if (version is null || (!includePrereleases && (version.IsPrerelease || Bool(release, "prerelease"))))
                continue;

            if (latest is not null && version.CompareTo(latest.Version) <= 0)
                continue;

            if (!Uri.TryCreate(String(release, "html_url"), UriKind.Absolute, out var page))
                continue;

            latest = new AppRelease(version, page, InstallFile(release));
        }

        return latest;
    }

    private static Uri? InstallFile(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var asset in assets.EnumerateArray())
        {
            if (String(asset, "name")?.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) == true
                && Uri.TryCreate(String(asset, "browser_download_url"), UriKind.Absolute, out var uri))
                return uri;
        }

        return null;
    }

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool Bool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
