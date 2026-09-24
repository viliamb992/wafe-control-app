using System.Globalization;
using System.Runtime.CompilerServices;
using WafeControl.Core.Localization;

namespace WafeControl.Tests;

/// <summary>
/// Tests assert English text whatever the machine's language. Czech is the app's primary language,
/// so without this every message would come out Czech. Tests that switch languages restore English.
/// </summary>
internal static class TestCulture
{
    public static CultureInfo English { get; } = CultureInfo.GetCultureInfo("en-US");

    [ModuleInitializer]
    internal static void UseEnglish() => Strings.Culture = English;
}
