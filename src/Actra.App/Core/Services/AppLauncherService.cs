using System.Diagnostics;

namespace Actra.Core.Services;

public sealed class AppLauncherService
{
    private static readonly Dictionary<string, string> KnownApps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["calc"] = "calc.exe",
        ["calculator"] = "calc.exe",
        ["계산기"] = "calc.exe",
        ["notepad"] = "notepad.exe",
        ["메모장"] = "notepad.exe",
        ["explorer"] = "explorer.exe",
        ["탐색기"] = "explorer.exe",
        ["powershell"] = "powershell.exe",
        ["파워셸"] = "powershell.exe",
        ["terminal"] = "wt.exe",
        ["터미널"] = "wt.exe",
        ["cmd"] = "cmd.exe",
        ["taskmgr"] = "taskmgr.exe",
        ["vscode"] = "code",
        ["vs code"] = "code",
        ["chrome"] = "chrome.exe",
        ["크롬"] = "chrome.exe",
        ["edge"] = "msedge.exe",
        ["엣지"] = "msedge.exe",
        ["settings"] = "ms-settings:",
        ["bluetooth-settings"] = "ms-settings:bluetooth",
        ["wifi-settings"] = "ms-settings:network-wifi"
    };

    public Task<(bool Success, string DisplayName, string Error)> LaunchAsync(
        string app,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(app))
            return Task.FromResult((false, app, "실행할 앱 이름이 비어 있습니다."));

        var target = KnownApps.TryGetValue(app.Trim(), out var known)
            ? known
            : app.Trim();

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            });

            return Task.FromResult((true, app.Trim(), string.Empty));
        }
        catch (Exception ex)
        {
            return Task.FromResult((
                false,
                app.Trim(),
                $"앱을 실행하지 못했습니다: {ex.Message}"));
        }
    }
}
