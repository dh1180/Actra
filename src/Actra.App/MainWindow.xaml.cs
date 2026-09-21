using System.Windows;
using System.Windows.Input;
using Actra.Core.Models;
using Actra.Core.Routing;
using Actra.Core.Services;
using Actra.Interop;

namespace Actra;

public partial class MainWindow : Window, IDisposable
{
    private readonly CompositeIntentRouter _router = new();
    private readonly CommandExecutor _executor = new();
    private GlobalHotkey? _hotkey;
    private CancellationTokenSource? _commandCts;
    private bool _isExecuting;

    public MainWindow()
    {
        InitializeComponent();
    }

    public void ActivateLauncher()
    {
        if (!IsVisible)
            Show();

        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        QueryBox.Focus();
        Keyboard.Focus(QueryBox);
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            _hotkey = new GlobalHotkey(this, ToggleLauncher);

            if (!_hotkey.IsRegistered)
            {
                ShowResult(
                    new ExecutionResult(
                        false,
                        "Ctrl + Space 단축키를 등록하지 못했습니다.",
                        "다른 프로그램이 같은 단축키를 사용 중일 수 있습니다."));
            }
        }
        catch (Exception ex)
        {
            ShowResult(new ExecutionResult(false, "글로벌 단축키 오류", ex.Message));
        }
    }

    private void ToggleLauncher()
    {
        if (IsVisible && IsActive)
        {
            Hide();
            return;
        }

        ActivateLauncher();
    }

    private async void QueryBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _isExecuting)
            return;

        e.Handled = true;
        await ExecuteQueryAsync();
    }

    private void QueryBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_isExecuting)
            ResultPanel.Visibility = Visibility.Collapsed;
    }

    private async Task ExecuteQueryAsync()
    {
        var query = QueryBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
            return;

        _commandCts?.Cancel();
        _commandCts?.Dispose();
        _commandCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));

        _isExecuting = true;
        QueryBox.IsEnabled = false;

        ResultPanel.Visibility = Visibility.Visible;
        ResultTitle.Text = "처리 중…";
        ResultDetail.Text = "Fast Path와 Local Router를 확인하고 있습니다.";

        try
        {
            var intent = await _router.RouteAsync(query, _commandCts.Token);
            var result = await _executor.ExecuteAsync(intent, _commandCts.Token);
            ShowResult(result);
        }
        catch (OperationCanceledException)
        {
            ShowResult(new ExecutionResult(false, "요청 시간이 초과되었습니다."));
        }
        catch (Exception ex)
        {
            ShowResult(new ExecutionResult(false, "명령 처리 중 오류가 발생했습니다.", ex.Message));
        }
        finally
        {
            _isExecuting = false;
            QueryBox.IsEnabled = true;
            QueryBox.Focus();
        }
    }

    private void ShowResult(ExecutionResult result)
    {
        ResultPanel.Visibility = Visibility.Visible;
        ResultTitle.Text = result.Success ? $"✓ {result.Title}" : $"! {result.Title}";
        ResultDetail.Text = result.Detail;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && !_isExecuting)
        {
            Hide();
            e.Handled = true;
        }
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        if (!_isExecuting)
            Hide();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    public void Dispose()
    {
        _commandCts?.Cancel();
        _commandCts?.Dispose();
        _hotkey?.Dispose();
        _router.Dispose();
    }
}
