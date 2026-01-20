using System;
using System.Threading;
using System.Threading.Tasks;

namespace RecuperationSystem.Desktop.Services;

public interface IAuthenticationService
{
    event EventHandler<bool>? AuthenticationChanged;
    
    bool IsAuthenticated { get; }
    string Username { get; }
    string Password { get; }
    
    Task<bool> AuthenticateAsync(CancellationToken cancellationToken = default);
    Task<bool> TryAutoLoginAsync(CancellationToken cancellationToken = default);

    Task<bool> LoginAsync(string username, string password, bool rememberMe, CancellationToken cancellationToken = default);
    Task ClearRememberedLoginAsync(CancellationToken cancellationToken = default);
}
