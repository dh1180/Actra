using Actra.Core.Models;

namespace Actra.Core.Routing;

public interface IIntentRouter
{
    Task<CommandIntent> RouteAsync(string query, CancellationToken cancellationToken = default);
}
