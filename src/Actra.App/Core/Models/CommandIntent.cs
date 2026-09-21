namespace Actra.Core.Models;

public sealed record CommandIntent(
    string Intent,
    double Confidence,
    IReadOnlyDictionary<string, string> Slots,
    bool FromFastPath = false)
{
    public static CommandIntent Unknown { get; } =
        new("system.unknown", 0, new Dictionary<string, string>());
}
