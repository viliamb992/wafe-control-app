using NSubstitute;
using RecuperationSystem.Core.Localization;
using RecuperationSystem.Core.Services;
using RecuperationSystem.Core.ViewModels;
using RecuperationSystem.Tests.Localization;

namespace RecuperationSystem.Tests.ViewModels;

public class SettingsViewModelTests
{
    private readonly ILocalizationService _localization = Substitute.For<ILocalizationService>();
    private readonly InMemorySettingsStore _store = new();
    private readonly IStartupRegistration _startup = Substitute.For<IStartupRegistration>();
    private readonly SettingsViewModel _sut;

    public SettingsViewModelTests()
    {
        _localization.Languages.Returns([LocalizationService.Czech, LocalizationService.Slovak, LocalizationService.English]);
        _localization.Current.Returns(LocalizationService.Czech);
        _sut = new SettingsViewModel(_localization, _store, _startup);
    }

    [Fact]
    public void Initially_SelectsCurrentLanguageWithoutChangingIt()
    {
        Assert.Equal(LocalizationService.Czech, _sut.SelectedLanguage);
        Assert.Equal(3, _sut.Languages.Count);
        _localization.DidNotReceiveWithAnyArgs().SetLanguage(default!);
    }

    [Fact]
    public void SelectingLanguage_SwitchesTheApp()
    {
        _sut.SelectedLanguage = LocalizationService.Slovak;

        _localization.Received(1).SetLanguage("sk");
    }

    [Fact]
    public void ClearedSelection_IsIgnored()
    {
        _sut.SelectedLanguage = null!;

        _localization.DidNotReceiveWithAnyArgs().SetLanguage(default!);
    }

    [Fact]
    public void MinimizeToTray_DefaultsToOnWithoutSaving()
    {
        Assert.True(_sut.MinimizeToTray);
        Assert.Equal(0, _store.SaveCount);
    }

    [Fact]
    public void MinimizeToTray_LoadsSavedValue()
    {
        _store.Settings = new UserSettings { MinimizeToTray = false };

        var sut = new SettingsViewModel(_localization, _store, _startup);

        Assert.False(sut.MinimizeToTray);
        Assert.Equal(0, _store.SaveCount);
    }

    [Fact]
    public void MinimizeToTray_ChangeIsSavedKeepingOtherSettings()
    {
        _store.Settings = new UserSettings { Language = "sk" };

        _sut.MinimizeToTray = false;

        Assert.Equal(new UserSettings { Language = "sk", MinimizeToTray = false }, _store.Settings);
        Assert.Equal(1, _store.SaveCount);
    }

    [Fact]
    public void StartInTray_LoadsAndSaves()
    {
        Assert.False(_sut.StartInTray);

        _sut.StartInTray = true;

        Assert.True(_store.Settings.StartInTray);
        Assert.True(new SettingsViewModel(_localization, _store, _startup).StartInTray);
    }

    [Fact]
    public void RunAtStartup_ReflectsSystemWithoutChangingIt()
    {
        _startup.IsEnabled.Returns(true);

        var sut = new SettingsViewModel(_localization, _store, _startup);

        Assert.True(sut.RunAtStartup);
        _startup.DidNotReceiveWithAnyArgs().TrySetEnabled(default);
    }

    [Fact]
    public void RunAtStartup_Toggle_RegistersWithSystem()
    {
        _startup.TrySetEnabled(true).Returns(true);

        _sut.RunAtStartup = true;

        _startup.Received(1).TrySetEnabled(true);
        Assert.True(_sut.RunAtStartup);
    }

    [Fact]
    public void RunAtStartup_Refused_SwitchesBack()
    {
        _startup.TrySetEnabled(true).Returns(false);

        _sut.RunAtStartup = true;

        Assert.False(_sut.RunAtStartup);
        _startup.Received(1).TrySetEnabled(Arg.Any<bool>());
    }

    [Fact]
    public void OpenAndClose()
    {
        _sut.OpenCommand.Execute(null);
        Assert.True(_sut.IsOpen);

        _sut.Close();
        Assert.False(_sut.IsOpen);
    }
}
