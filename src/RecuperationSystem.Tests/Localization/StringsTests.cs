using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using RecuperationSystem.Core.Localization;

namespace RecuperationSystem.Tests.Localization;

/// <summary>
/// Every language must translate every string, with the same placeholders, so no screen falls back to Czech.
/// </summary>
public partial class StringsTests
{
    public static TheoryData<string> Translations => ["sk", "en"];

    private static Dictionary<string, string> Read(CultureInfo culture) =>
        Strings.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)!
            .Cast<DictionaryEntry>()
            .ToDictionary(e => (string)e.Key, e => (string)e.Value!);

    // Strings.resx lives in the main assembly (NeutralLanguage = cs).
    private static Dictionary<string, string> Czech => Read(CultureInfo.InvariantCulture);

    [Theory]
    [MemberData(nameof(Translations))]
    public void Translation_HasEveryKey(string language)
    {
        var translation = Read(CultureInfo.GetCultureInfo(language));

        Assert.Empty(Czech.Keys.Except(translation.Keys));
        Assert.Empty(translation.Keys.Except(Czech.Keys));
        Assert.All(translation, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Value), entry.Key));
    }

    [Theory]
    [MemberData(nameof(Translations))]
    public void Translation_KeepsPlaceholders(string language)
    {
        var translation = Read(CultureInfo.GetCultureInfo(language));

        Assert.All(Czech, entry => Assert.Equal(Placeholders(entry.Value), Placeholders(translation[entry.Key])));
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("sk")]
    [InlineData("en")]
    public void DayNames_HaveSevenEntries(string language)
    {
        var culture = CultureInfo.GetCultureInfo(language);

        Assert.Equal(7, Strings.ResourceManager.GetString(nameof(Strings.ScheduleDayNames), culture)!.Split(',').Length);
        Assert.Equal(7, Strings.ResourceManager.GetString(nameof(Strings.ScheduleShortDayNames), culture)!.Split(',').Length);
    }

    private static string[] Placeholders(string value) =>
        PlaceholderPattern().Matches(value).Select(m => m.Value).Order().ToArray();

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex PlaceholderPattern();
}
