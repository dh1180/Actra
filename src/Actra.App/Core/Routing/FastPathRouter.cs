using System.Text.RegularExpressions;
using Actra.Core.Models;

namespace Actra.Core.Routing;

public sealed class FastPathRouter : IIntentRouter
{
    private static readonly Dictionary<string, string> AppAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["계산기"] = "calc",
        ["calculator"] = "calc",
        ["calc"] = "calc",
        ["메모장"] = "notepad",
        ["notepad"] = "notepad",
        ["파일 탐색기"] = "explorer",
        ["탐색기"] = "explorer",
        ["explorer"] = "explorer",
        ["파워셸"] = "powershell",
        ["powershell"] = "powershell",
        ["터미널"] = "terminal",
        ["terminal"] = "terminal",
        ["명령 프롬프트"] = "cmd",
        ["cmd"] = "cmd",
        ["작업 관리자"] = "taskmgr",
        ["task manager"] = "taskmgr",
        ["vscode"] = "vscode",
        ["vs code"] = "vscode",
        ["visual studio code"] = "vscode",
        ["크롬"] = "chrome",
        ["chrome"] = "chrome",
        ["엣지"] = "edge",
        ["edge"] = "edge"
    };

    public Task<CommandIntent> RouteAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Task.FromResult(CommandIntent.Unknown);

        var normalized = query.Trim().ToLowerInvariant();

        var settings = TrySettings(normalized);
        if (settings is not null)
            return Task.FromResult(settings);

        // 파일 요청을 앱 실행보다 먼저 판별한다.
        var fileSearch = TryFileSearch(normalized);
        if (fileSearch is not null)
            return Task.FromResult(fileSearch);

        var appLaunch = TryAppLaunch(normalized);
        if (appLaunch is not null)
            return Task.FromResult(appLaunch);

        return Task.FromResult(CommandIntent.Unknown);
    }

    private static CommandIntent? TrySettings(string query)
    {
        if (query is "설정" or "설정 열어" or "설정 열어줘" or "settings" or "open settings")
            return Intent("app.launch", ("app", "settings"));

        if (query.Contains("블루투스") && ContainsOpenVerb(query))
            return Intent("app.launch", ("app", "bluetooth-settings"));

        if ((query.Contains("와이파이") || query.Contains("wifi") || query.Contains("wi-fi"))
            && ContainsOpenVerb(query))
            return Intent("app.launch", ("app", "wifi-settings"));

        return null;
    }

    private static CommandIntent? TryAppLaunch(string query)
    {
        if (!ContainsOpenVerb(query))
            return null;

        foreach (var (alias, app) in AppAliases.OrderByDescending(x => x.Key.Length))
        {
            if (query.Contains(alias, StringComparison.OrdinalIgnoreCase))
                return Intent("app.launch", ("app", app));
        }

        var cleaned = Regex.Replace(
            query,
            @"\s*(열어\s*줘?|실행\s*해?\s*줘?|켜\s*줘?|open|launch|run)\s*$",
            "",
            RegexOptions.IgnoreCase).Trim();

        // 파일처럼 보이는 문장은 앱 이름 fallback으로 넘기지 않는다.
        if (LooksLikeFileRequest(query))
            return null;

        if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length <= 80)
            return Intent("app.launch", ("app", cleaned), confidence: 0.86);

        return null;
    }

    private static CommandIntent? TryFileSearch(string query)
    {
        var extension = DetectExtension(query);

        var asksToFind =
            query.Contains("찾아", StringComparison.OrdinalIgnoreCase) ||
            query.Contains("검색", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(query, @"\b(find|search)\b", RegexOptions.IgnoreCase);

        var asksToOpen = ContainsOpenVerb(query);

        if (!asksToFind && !(asksToOpen && LooksLikeFileRequest(query)))
            return null;

        var slots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (ContainsDownloadReference(query))
            slots["location"] = "downloads";
        else if (query.Contains("바탕화면") || query.Contains("desktop"))
            slots["location"] = "desktop";
        else if (query.Contains("문서") || query.Contains("documents"))
            slots["location"] = "documents";
        else
            slots["location"] = "home";

        if (extension is not null)
            slots["extension"] = extension;

        if (asksToOpen)
            slots["action"] = "open";

        if (ContainsRecentReference(query))
            slots["sort"] = "recent";

        var keyword = ExtractFileKeyword(query);
        if (!string.IsNullOrWhiteSpace(keyword))
            slots["query"] = keyword;

        return new CommandIntent("file.search", 0.98, slots, true);
    }

    private static string ExtractFileKeyword(string query)
    {
        var keyword = query;

        string[] noise =
        [
            "찾아줘", "찾아 줘", "찾아", "검색해줘", "검색해 줘", "검색",
            "열어줘", "열어 줘", "열어", "open", "find", "search",
            "파일", "file",
            "다운로드에서", "다운로드 폴더에서", "다운로드 폴더", "다운로드",
            "다운받은", "다운 받은", "다운받았던", "받은",
            "바탕화면에서", "바탕화면", "desktop",
            "문서에서", "문서", "documents",
            "가장 최근에", "가장 최근", "최근에", "최근", "최신",
            "방금", "좀", "제일"
        ];

        foreach (var token in noise.OrderByDescending(x => x.Length))
            keyword = keyword.Replace(token, "", StringComparison.OrdinalIgnoreCase);

        foreach (var extWord in new[] { "pdf", "docx", "word", "pptx", "ppt", "xlsx", "excel", "txt", "hwp", "hwpx", "zip" })
            keyword = Regex.Replace(keyword, $@"\b{Regex.Escape(extWord)}\b", "", RegexOptions.IgnoreCase);

        return Regex.Replace(keyword, @"\s+", " ").Trim();
    }

    private static bool LooksLikeFileRequest(string query) =>
        DetectExtension(query) is not null ||
        query.Contains("파일", StringComparison.OrdinalIgnoreCase) ||
        ContainsDownloadReference(query) ||
        query.Contains("바탕화면", StringComparison.OrdinalIgnoreCase) ||
        query.Contains("문서", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsDownloadReference(string query) =>
        query.Contains("다운로드", StringComparison.OrdinalIgnoreCase) ||
        query.Contains("다운받", StringComparison.OrdinalIgnoreCase) ||
        query.Contains("다운 받", StringComparison.OrdinalIgnoreCase) ||
        query.Contains("download", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsRecentReference(string query) =>
        query.Contains("최근", StringComparison.OrdinalIgnoreCase) ||
        query.Contains("최신", StringComparison.OrdinalIgnoreCase) ||
        query.Contains("방금", StringComparison.OrdinalIgnoreCase) ||
        Regex.IsMatch(query, @"\b(latest|recent|newest)\b", RegexOptions.IgnoreCase);

    private static string? DetectExtension(string query)
    {
        if (Regex.IsMatch(query, @"\bpdf\b", RegexOptions.IgnoreCase)) return ".pdf";
        if (Regex.IsMatch(query, @"\b(docx|word)\b", RegexOptions.IgnoreCase)) return ".docx";
        if (Regex.IsMatch(query, @"\b(pptx|ppt)\b", RegexOptions.IgnoreCase)) return ".pptx";
        if (Regex.IsMatch(query, @"\b(xlsx|excel)\b", RegexOptions.IgnoreCase)) return ".xlsx";
        if (Regex.IsMatch(query, @"\btxt\b", RegexOptions.IgnoreCase)) return ".txt";
        if (Regex.IsMatch(query, @"\bhwpx?\b", RegexOptions.IgnoreCase)) return ".hwp";
        if (Regex.IsMatch(query, @"\bzip\b", RegexOptions.IgnoreCase)) return ".zip";
        return null;
    }

    private static bool ContainsOpenVerb(string query) =>
        query.Contains("열어", StringComparison.OrdinalIgnoreCase) ||
        query.Contains("실행", StringComparison.OrdinalIgnoreCase) ||
        query.Contains("켜", StringComparison.OrdinalIgnoreCase) ||
        Regex.IsMatch(query, @"\b(open|launch|run)\b", RegexOptions.IgnoreCase);

    private static CommandIntent Intent(
        string name,
        (string Key, string Value) slot,
        double confidence = 0.99) =>
        new(
            name,
            confidence,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [slot.Key] = slot.Value
            },
            true);
}
