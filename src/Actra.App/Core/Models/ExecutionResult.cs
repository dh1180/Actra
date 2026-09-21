namespace Actra.Core.Models;

public sealed record ExecutionResult(bool Success, string Title, string Detail = "");
