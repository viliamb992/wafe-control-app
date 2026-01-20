using System.Threading;
using System.Threading.Tasks;

namespace RecuperationSystem.Desktop.Services;

public interface ICredentialStore
{
    Task<(string Username, string Password)?> TryGetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(string username, string password, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
