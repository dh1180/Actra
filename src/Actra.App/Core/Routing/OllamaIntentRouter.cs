using System.Net.Http;
using System.Text;
using System.Text.Json;
using Actra.Core.Models;

namespace Actra.Core.Routing;

public sealed class OllamaIntentRouter : IIntentRouter, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    public OllamaIntentRouter()
    {
        _model = Environment.GetEnvironmentVariable("ACTRA_OLLAMA_MODEL") ?? "qwen2.5:0.5b";
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://127.0.0.1:11434"),
            Timeout = TimeSpan.FromSeconds(4)
        };
    }

    public async Task<CommandIntent> RouteAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var body = JsonSerializer.Serialize(new
            {
                model = _model,
                stream = false,
                format = "json",
                options = new { temperature = 0 },
                prompt = BuildPrompt(query)
            });

            using var response = await _httpClient.PostAsync(
                "/api/generate",
                new StringContent(body, Encoding.UTF8, "application/json"),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return CommandIntent.Unknown;

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            using var envelope = JsonDocument.Parse(payload);

            if (!envelope.RootElement.TryGetProperty("response", out var responseNode))
                return CommandIntent.Unknown;

            return ParseIntent(responseNode.GetString());
        }
        catch
        {
            return CommandIntent.Unknown;
        }
    }

    private static CommandIntent ParseIntent(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return CommandIntent.Unknown;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var intent = root.TryGetProperty("intent", out var intentNode)
                ? intentNode.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(intent))
                return CommandIntent.Unknown;

            var confidence = root.TryGetProperty("confidence", out var confidenceNode)
                && confidenceNode.TryGetDouble(out var parsedConfidence)
                    ? parsedConfidence
                    : 0.7;

            var slots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (root.TryGetProperty("slots", out var slotsNode)
                && slotsNode.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in slotsNode.EnumerateObject())
                {
                    var value = property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString()
                        : property.Value.ToString();

                    if (!string.IsNullOrWhiteSpace(value))
                        slots[property.Name] = value!;
                }
            }

            return new CommandIntent(intent!, confidence, slots);
        }
        catch
        {
            return CommandIntent.Unknown;
        }
    }

    private static string BuildPrompt(string query) => $$"""
You are Actra's local intent router for Windows.
Return exactly one JSON object and nothing else.

Allowed intents:
- app.launch: launch an installed Windows application or Settings page
- file.search: search local files
- system.unknown: use when the request is unsupported or ambiguous

Schema:
{
  "intent": "app.launch | file.search | system.unknown",
  "confidence": 0.0,
  "slots": {}
}

For app.launch, slots may contain:
- app: short app name such as notepad, calculator, vscode, chrome, explorer, settings

For file.search, slots may contain:
- query: filename keyword without conversational filler
- extension: extension beginning with a dot, for example .pdf
- location: one of home, downloads, desktop, documents

Never invent capabilities outside the allowed intents.
Never output shell commands.
Do not execute anything yourself.

User query:
{{JsonSerializer.Serialize(query)}}
""";

    public void Dispose() => _httpClient.Dispose();
}
