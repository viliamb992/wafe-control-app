using NSubstitute;
using WafeControl.Core.Localization;
using WafeControl.Core.Services;
using WafeControl.Core.ViewModels;
using WafeControl.Tests.Localization;

namespace WafeControl.Tests.ViewModels;

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
    public void Theme_DefaultsToSystemWithoutSaving()
    {
        Assert.Equal(AppTheme.System, _sut.Theme);
        Assert.Equal(0, _sut.ThemeIndex);
        Assert.Equal(0, _store.SaveCount);
    }

    [Fact]
    public void Theme_LoadsAndSavesKeepingOtherSettings()
    {
        _store.Settings = new UserSettings { Language = "sk", Theme = AppTheme.Light };
        var sut = new SettingsViewModel(_localization, _store, _startup);
        Assert.Equal(AppTheme.Light, sut.Theme);

        sut.Theme = AppTheme.Dark;

        Assert.Equal(new UserSettings { Language = "sk", Theme = AppTheme.Dark }, _store.Settings);
        Assert.Equal(1, _store.SaveCount);
    }

    [Fact]
    public void ThemeIndex_FollowsListOrderAndIgnoresNoSelection()
    {
        var changed = new List<string?>();
        _sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _sut.ThemeIndex = 2;
        _sut.ThemeIndex = -1;

        Assert.Equal(AppTheme.Dark, _sut.Theme);
        Assert.Equal(2, _sut.ThemeIndex);
        Assert.Contains(nameof(SettingsViewModel.ThemeIndex), changed);
    }

    [Fact]
    public void MainWindowPlacement_IsKeptWhenAnotherSettingChanges()
    {
        var placement = new WindowPlacement(10, 20, 1040, 820, IsMaximized: true);
        _store.Settings = new UserSettings { MainWindow = placement };
        var sut = new SettingsViewModel(_localization, _store, _startup);

        sut.StartInTray = true;

        Assert.Equal(placement, _store.Settings.MainWindow);
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
