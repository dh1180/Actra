using System.Diagnostics;
using Actra.Core.Models;

namespace Actra.Core.Services;

public sealed class CommandExecutor
{
    private readonly AppLauncherService _appLauncher = new();
    private readonly FileSearchService _fileSearch = new();

    public async Task<ExecutionResult> ExecuteAsync(
        CommandIntent command,
        CancellationToken cancellationToken = default)
    {
        return command.Intent switch
        {
            "app.launch" => await ExecuteAppLaunchAsync(command, cancellationToken),
            "file.search" => await ExecuteFileSearchAsync(command, cancellationToken),
            _ => new ExecutionResult(
                false,
                "아직 지원하지 않는 명령입니다.",
                "앱 실행 또는 파일 검색을 입력해 보세요.")
        };
    }

    private async Task<ExecutionResult> ExecuteAppLaunchAsync(
        CommandIntent command,
        CancellationToken cancellationToken)
    {
        if (!command.Slots.TryGetValue("app", out var app) || string.IsNullOrWhiteSpace(app))
            return new ExecutionResult(false, "앱 이름을 찾지 못했습니다.");

        var result = await _appLauncher.LaunchAsync(app, cancellationToken);

        return result.Success
            ? new ExecutionResult(
                true,
                $"{result.DisplayName} 실행",
                command.FromFastPath ? "Fast Path로 처리했습니다." : "Local Router로 처리했습니다.")
            : new ExecutionResult(false, "실행 실패", result.Error);
    }

    private async Task<ExecutionResult> ExecuteFileSearchAsync(
        CommandIntent command,
        CancellationToken cancellationToken)
    {
        command.Slots.TryGetValue("query", out var query);
        command.Slots.TryGetValue("extension", out var extension);
        command.Slots.TryGetValue("location", out var location);
        command.Slots.TryGetValue("action", out var action);
        command.Slots.TryGetValue("sort", out var sort);

        var results = await _fileSearch.SearchAsync(query, extension, location, cancellationToken);

        // "최근/최신 파일" 요청에서 설명용 단어가 파일명과 안 맞으면
        // 확장자 + 위치 기준으로 다시 찾아 가장 최근 파일을 사용한다.
        if (results.Count == 0 &&
            string.Equals(sort, "recent", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(query))
        {
            results = await _fileSearch.SearchAsync(null, extension, location, cancellationToken);
        }

        if (results.Count == 0)
        {
            return new ExecutionResult(
                false,
                "파일을 찾지 못했습니다.",
                "검색어 또는 위치를 바꿔 다시 시도해 보세요.");
        }

        if (string.Equals(action, "open", StringComparison.OrdinalIgnoreCase))
        {
            var target = results[0];

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = target.FullPath,
                    UseShellExecute = true
                });

                return new ExecutionResult(
                    true,
                    $"{target.Name} 열기",
                    $"가장 최근 조건에 맞는 파일을 열었습니다.\n{target.FullPath}");
            }
            catch (Exception ex)
            {
                return new ExecutionResult(
                    false,
                    "파일을 열지 못했습니다.",
                    ex.Message);
            }
        }

        var lines = results.Select((item, index) =>
            $"{index + 1}. {item.Name}\n   {item.FullPath}");

        var source = command.FromFastPath ? "Fast Path" : "Local Router";

        return new ExecutionResult(
            true,
            $"{results.Count}개 파일 발견 · {source}",
            string.Join("\n\n", lines));
    }
}
