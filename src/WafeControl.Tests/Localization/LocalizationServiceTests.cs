using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using WafeControl.Core.Localization;
using WafeControl.Core.Services;
using WafeControl.Core.ViewModels.Schedule;

namespace WafeControl.Tests.Localization;

/// <summary>
/// These tests change process-wide cultures, so they run alone, after the parallel tests.
/// </summary>
[CollectionDefinition(nameof(LanguageSwitchingCollection), DisableParallelization = true)]
public sealed class LanguageSwitchingCollection;

internal sealed class InMemorySettingsStore : ISettingsStore
{
    public UserSettings Settings { get; set; } = new();

    public int SaveCount { get; private set; }

    public UserSettings Load() => Settings;

    public void Save(UserSettings settings)
    {
        Settings = settings;
        SaveCount++;
    }
}

[Collection(nameof(LanguageSwitchingCollection))]
public sealed class LocalizationServiceTests : IDisposable
{
    private readonly InMemorySettingsStore _store = new();
    private readonly LocalizationService _sut;

    public LocalizationServiceTests()
    {
        _sut = new LocalizationService(_store, NullLogger<LocalizationService>.Instance);
    }

    public void Dispose()
    {
        Strings.Culture = TestCulture.English;
        CultureInfo.DefaultThreadCurrentCulture = null;
        CultureInfo.DefaultThreadCurrentUICulture = null;
    }

    [Fact]
    public void Initialize_NothingSaved_UsesCzech()
    {
        _sut.Initialize();

        Assert.Equal(LocalizationService.Czech, _sut.Current);
        Assert.Equal("Přihlášení", Strings.LoginTitle);
        Assert.Equal("cs-CZ", CultureInfo.DefaultThreadCurrentCulture?.Name);
        Assert.Equal(0, _store.SaveCount);
    }

    [Theory]
    [InlineData("sk", "Prihlásenie", "Pondelok")]
    [InlineData("en", "Sign in", "Monday")]
    [InlineData("cs", "Přihlášení", "Pondělí")]
    public void Initialize_UsesSavedLanguage(string saved, string loginTitle, string monday)
    {
        _store.Settings = new UserSettings { Language = saved };

        _sut.Initialize();

        Assert.Equal(saved, _sut.Current.Code);
        Assert.Equal(loginTitle, Strings.LoginTitle);
        Assert.Equal(monday, ScheduleFormat.DayNames[0]);
    }

    [Fact]
    public void Initialize_UnknownSavedLanguage_FallsBackToCzech()
    {
        _store.Settings = new UserSettings { Language = "de" };

        _sut.Initialize();

        Assert.Equal(LocalizationService.Czech, _sut.Current);
    }

    [Fact]
    public void SetLanguage_AppliesSavesAndNotifies()
    {
        _sut.Initialize();
        var raised = 0;
        _sut.LanguageChanged += (_, _) => raised++;

        _sut.SetLanguage("sk");

        Assert.Equal(LocalizationService.Slovak, _sut.Current);
        Assert.Equal("Prihlásenie", Strings.LoginTitle);
        Assert.Equal("sk-SK", CultureInfo.DefaultThreadCurrentUICulture?.Name);
        Assert.Equal("sk", _store.Settings.Language);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void SetLanguage_Current_DoesNothing()
    {
        _sut.Initialize();
        var raised = 0;
        _sut.LanguageChanged += (_, _) => raised++;

        _sut.SetLanguage("cs");

        Assert.Equal(0, raised);
        Assert.Equal(0, _store.SaveCount);
    }

    [Fact]
    public void SetLanguage_Unsupported_Throws()
    {
        _sut.Initialize();

        Assert.Throws<ArgumentException>(() => _sut.SetLanguage("de"));
        Assert.Equal(LocalizationService.Czech, _sut.Current);
    }
}
