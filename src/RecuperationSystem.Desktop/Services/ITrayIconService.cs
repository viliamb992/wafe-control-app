using System;

namespace RecuperationSystem.Desktop.Services;

public interface ITrayIconService : IDisposable
{
    void Initialize();
    void ShowWindow();
    void HideWindow();
    void ExitApplication();
}
