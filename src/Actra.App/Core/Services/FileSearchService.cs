using System.IO;

namespace Actra.Core.Services;

public sealed record FileSearchItem(string Name, string FullPath, DateTime LastWriteTime);

public sealed class FileSearchService
{
    private const int MaxResults = 8;

    public Task<IReadOnlyList<FileSearchItem>> SearchAsync(
        string? query,
        string? extension,
        string? location,
        CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<FileSearchItem>>(() =>
        {
            var root = ResolveRoot(location);

            if (!Directory.Exists(root))
                return Array.Empty<FileSearchItem>();

            var normalizedExtension = NormalizeExtension(extension);
            var normalizedQuery = query?.Trim();

            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false,
                AttributesToSkip = FileAttributes.System | FileAttributes.ReparsePoint
            };

            var results = new List<FileSearchItem>(MaxResults * 3);

            try
            {
                foreach (var path in Directory.EnumerateFiles(root, "*", options))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (normalizedExtension is not null &&
                        !string.Equals(Path.GetExtension(path), normalizedExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var name = Path.GetFileName(path);

                    if (!string.IsNullOrWhiteSpace(normalizedQuery) &&
                        !name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        var info = new FileInfo(path);
                        results.Add(new FileSearchItem(info.Name, info.FullName, info.LastWriteTime));

                        if (results.Count >= MaxResults * 3)
                            break;
                    }
                    catch
                    {
                        // A file can disappear or become unavailable while enumerating.
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // A root can become unavailable while searching. Return what we found.
            }

            return results
                .OrderByDescending(x => x.LastWriteTime)
                .Take(MaxResults)
                .ToArray();
        }, cancellationToken);
    }

    private static string ResolveRoot(string? location)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return location?.ToLowerInvariant() switch
        {
            "downloads" => Path.Combine(home, "Downloads"),
            "desktop" => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            _ => home
        };
    }

    private static string? NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return null;

        var trimmed = extension.Trim();
        return trimmed.StartsWith('.') ? trimmed : $".{trimmed}";
    }
}
