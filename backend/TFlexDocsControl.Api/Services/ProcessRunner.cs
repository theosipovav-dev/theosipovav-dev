using System.Diagnostics;

namespace TFlexDocsControl.Api.Services;

public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr, bool TimedOut);

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(string fileName, IEnumerable<string> arguments, TimeSpan timeout, CancellationToken ct);
}

/// <summary>
/// Запуск внешних процессов. Аргументы передаются списком (без shell),
/// поэтому подстановка команд через ввод пользователя невозможна.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    private readonly ILogger<ProcessRunner> _logger;

    public ProcessRunner(ILogger<ProcessRunner> logger)
    {
        _logger = logger;
    }

    public async Task<ProcessResult> RunAsync(string fileName, IEnumerable<string> arguments, TimeSpan timeout, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }
        // Стабильный вывод утилит systemd вне зависимости от локали сервера.
        psi.Environment["LC_ALL"] = "C.UTF-8";
        psi.Environment["SYSTEMD_PAGER"] = "";
        psi.Environment["SYSTEMD_COLORS"] = "0";

        _logger.LogDebug("Run: {File} {Args}", fileName, string.Join(' ', psi.ArgumentList));

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);
        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            timedOut = !ct.IsCancellationRequested;
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Процесс уже завершился.
            }
            if (!timedOut)
            {
                throw;
            }
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        return new ProcessResult(timedOut ? -1 : process.ExitCode, stdout, stderr, timedOut);
    }
}
