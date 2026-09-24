using System.Globalization;
using Microsoft.Extensions.Logging;
using WafeControl.Core.Services;

namespace WafeControl.Core.Localization;

/// <summary>
/// A language the app is translated to. <paramref name="NativeName"/> is written in that language,
/// so every option in the picker stays readable whatever language is active.
/// </summary>
public sealed record AppLanguage(string Code, string NativeName, string CultureName);

public interface ILocalizationService
{
    IReadOnlyList<AppLanguage> Languages { get; }

    AppLanguage Current { get; }

    /// <summary>
    /// Raised after <see cref="SetLanguage"/> switched the language; views refresh their text.
    /// </summary>
    event EventHandler? LanguageChanged;

    /// <summary>
    /// Switches the app to <paramref name="code"/> and remembers it for the next launch.
    /// </summary>
    void SetLanguage(string code);
}

/// <summary>
/// Applies the app language to <see cref="Strings"/> and to the default culture used for formatting,
/// and stores the choice in <see cref="ISettingsStore"/>. Czech is the primary language.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    public static AppLanguage Czech { get; } = new("cs", "Čeština", "cs-CZ");
    public static AppLanguage Slovak { get; } = new("sk", "Slovenčina", "sk-SK");
    public static AppLanguage English { get; } = new("en", "English", "en-US");

    private readonly ISettingsStore _settings;
    private readonly ILogger<LocalizationService> _logger;

    public LocalizationService(ISettingsStore settings, ILogger<LocalizationService> logger)
    {
        _settings = settings;
        _logger = logger;
        Current = Czech;
    }

    public IReadOnlyList<AppLanguage> Languages { get; } = [Czech, Slovak, English];

    public AppLanguage Current { get; private set; }

    public event EventHandler? LanguageChanged;

    /// <summary>
    /// Applies the saved language, or Czech when none is saved. Call once at startup, before creating any UI.
    /// </summary>
    public void Initialize()
    {
        var saved = _settings.Load().Language;
        var language = Find(saved);
        if (saved is not null && language is null)
            _logger.LogWarning("Unknown saved language {Language}, using {Default}", saved, Czech.Code);

        Apply(language ?? Czech);
    }

    public void SetLanguage(string code)
    {
        var language = Find(code) ?? throw new ArgumentException($"Unsupported language '{code}'.", nameof(code));
        if (language == Current)
            return;

        Apply(language);
        _settings.Save(_settings.Load() with { Language = language.Code });
        _logger.LogInformation("Language changed to {Language}", language.Code);

        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private AppLanguage? Find(string? code) =>
        Languages.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));

    private void Apply(AppLanguage language)
    {
        var culture = CultureInfo.GetCultureInfo(language.CultureName);

        // Strings reads its explicit culture, so text follows the language on every thread.
        // The defaults cover number formatting (21,5 °C) on threads that never set their own culture.
        Strings.Culture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        Current = language;
    }
}
