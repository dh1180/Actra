using Actra.Core.Models;

namespace Actra.Core.Routing;

public sealed class CompositeIntentRouter : IIntentRouter, IDisposable
{
    private readonly FastPathRouter _fastPath = new();
    private readonly OllamaIntentRouter _ollama = new();

    public async Task<CommandIntent> RouteAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var fast = await _fastPath.RouteAsync(query, cancellationToken);
        if (!string.Equals(fast.Intent, "system.unknown", StringComparison.Ordinal))
            return fast;

        return await _ollama.RouteAsync(query, cancellationToken);
    }

    public void Dispose() => _ollama.Dispose();
}
