using System.Text.RegularExpressions;

namespace WafeControl.Shared.Diagnostics;

/// <summary>
/// Keeps personal data out of logs and crash reports: users attach logs to public issues.
/// </summary>
public static partial class LogRedaction
{
    /// <summary>
    /// "viliam.birmon@gmail.com" → "v***@g***.com"; anything that isn't an email keeps only its first character.
    /// </summary>
    public static string Email(string? email)
    {
        if (string.IsNullOrEmpty(email))
            return string.Empty;

        var at = email.IndexOf('@');
        if (at <= 0)
            return email[0] + "***";

        var domain = email[(at + 1)..];
        var dot = domain.LastIndexOf('.');
        var host = dot > 0 ? domain[..dot] : domain;
        var tld = dot > 0 ? domain[dot..] : string.Empty;
        return $"{email[0]}***@{(host.Length > 0 ? host[0] : '*')}***{tld}";
    }

    /// <summary>
    /// Replaces every email address in free text (log messages, exception messages).
    /// </summary>
    public static string? Scrub(string? text) =>
        string.IsNullOrEmpty(text) ? text : EmailPattern().Replace(text, m => Email(m.Value));

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailPattern();
}
