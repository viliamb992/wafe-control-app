using WafeControl.Core.Diagnostics;
using WafeControl.Core.Localization;
using WafeControl.Core.ViewModels;

namespace WafeControl.Mobile.Helpers;

/// <summary>
/// "Report a problem" and "Share logs" on mobile: a GitHub issue with the diagnostics filled in, or the logs and
/// diagnostics through the share sheet (email, messaging, Drive) for people without a GitHub account.
/// </summary>
public static class ProblemReporting
{
    /// <summary>
    /// Asks how to report, then opens the issue or the share sheet.
    /// </summary>
    public static async Task ReportAsync(Page page, AppViewModel app, ILocalizationService localization)
    {
        var report = ProblemReport.Create(DeviceDescription.Current, localization.Current.Code, app.IsDemo,
            app.Unit?.Unit?.Model, CrashHandler.LastCrash);

        var choice = await page.DisplayActionSheetAsync(Strings.ReportProblem, Strings.ButtonCancel, null,
            Strings.ReportOpenGitHub, Strings.ReportShare);

        if (choice == Strings.ReportOpenGitHub)
            await Launcher.Default.TryOpenAsync(report.IssueUrl);
        else if (choice == Strings.ReportShare)
            await ShareAsync(Strings.ReportProblem, report.Text);
    }

    /// <summary>
    /// The logs (zipped) and, when given, the diagnostics as a text file, through the share sheet.
    /// </summary>
    public static async Task ShareAsync(string title, string? diagnostics = null)
    {
        var folder = Path.Combine(FileSystem.CacheDirectory, "share");
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);

        var files = new List<ShareFile> { new(AppLogging.ZipLogs(MauiProgram.LogDirectory, folder)) };
        if (diagnostics is not null)
        {
            var path = Path.Combine(folder, "diagnostics.txt");
            await File.WriteAllTextAsync(path, diagnostics);
            files.Add(new ShareFile(path));
        }

        await Share.Default.RequestAsync(new ShareMultipleFilesRequest(title, files));
    }
}
