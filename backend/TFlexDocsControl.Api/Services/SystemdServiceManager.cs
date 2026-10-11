using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TFlexDocsControl.Api.Contracts;
using TFlexDocsControl.Api.Options;

namespace TFlexDocsControl.Api.Services;

public interface IServiceManager
{
    Task<ServiceStatusDto> GetStatusAsync(MonitoredService service, CancellationToken ct);
    Task<ServiceActionResultDto> ExecuteAsync(MonitoredService service, ServiceAction action, CancellationToken ct);
    Task<LogsResponseDto> GetLogsAsync(MonitoredService service, LogsQuery query, CancellationToken ct);
}

/// <summary>
/// Управление службами через systemctl и чтение журналов через journalctl.
/// Работает только со службами из конфигурации.
/// </summary>
public sealed partial class SystemdServiceManager : IServiceManager
{
    private const string ShowProperties =
        "LoadState,ActiveState,SubState,UnitFileState,MainPID,ActiveEnterTimestamp,MemoryCurrent";

    private readonly IProcessRunner _runner;
    private readonly IOptionsMonitor<ServiceControlOptions> _options;
    private readonly ILogger<SystemdServiceManager> _logger;

    public SystemdServiceManager(
        IProcessRunner runner,
        IOptionsMonitor<ServiceControlOptions> options,
        ILogger<SystemdServiceManager> logger)
    {
        _runner = runner;
        _options = options;
        _logger = logger;
    }

    private ServiceControlOptions Options => _options.CurrentValue;
    private TimeSpan Timeout => TimeSpan.FromSeconds(Math.Max(5, Options.CommandTimeoutSeconds));

    public async Task<ServiceStatusDto> GetStatusAsync(MonitoredService service, CancellationToken ct)
    {
        try
        {
            var result = await _runner.RunAsync(
                Options.SystemctlPath,
                new[] { "show", service.Unit, "--no-pager", "--property=" + ShowProperties },
                Timeout,
                ct);

            if (result.ExitCode != 0)
            {
                return Unknown(service, Trim(result.StdErr) ?? $"systemctl завершился с кодом {result.ExitCode}");
            }

            var props = ParseProperties(result.StdOut);
            return new ServiceStatusDto(
                service.Id,
                service.Unit,
                DisplayNameOf(service),
                service.Description,
                service.Group,
                service.Discovered,
                service.AllowControl,
                props.GetValueOrDefault("LoadState", "unknown"),
                props.GetValueOrDefault("ActiveState", "unknown"),
                props.GetValueOrDefault("SubState", "unknown"),
                NullIfEmpty(props.GetValueOrDefault("UnitFileState")),
                int.TryParse(props.GetValueOrDefault("MainPID"), out var pid) && pid > 0 ? pid : null,
                ParseSystemdTimestamp(props.GetValueOrDefault("ActiveEnterTimestamp")),
                long.TryParse(props.GetValueOrDefault("MemoryCurrent"), out var mem) ? mem : null,
                null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Не удалось получить статус {Unit}", service.Unit);
            return Unknown(service, ex.Message);
        }
    }

    public async Task<ServiceActionResultDto> ExecuteAsync(MonitoredService service, ServiceAction action, CancellationToken ct)
    {
        if (!service.AllowControl)
        {
            return new ServiceActionResultDto(false, "Управление этой службой запрещено конфигурацией", null);
        }

        var verb = action switch
        {
            ServiceAction.Start => "start",
            ServiceAction.Stop => "stop",
            ServiceAction.Restart => "restart",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

        var (file, args) = WithSudo(Options.UseSudoForControl, Options.SystemctlPath, new[] { verb, service.Unit, "--no-pager" });
        var result = await _runner.RunAsync(file, args, Timeout, ct);
        var status = await GetStatusAsync(service, ct);

        if (result.TimedOut)
        {
            return new ServiceActionResultDto(false, $"Команда {verb} не завершилась за {Timeout.TotalSeconds:0} с", status);
        }

        if (result.ExitCode != 0)
        {
            var message = Trim(result.StdErr) ?? Trim(result.StdOut) ?? $"Код завершения {result.ExitCode}";
            _logger.LogWarning("systemctl {Verb} {Unit} завершился с ошибкой: {Message}", verb, service.Unit, message);
            return new ServiceActionResultDto(false, message, status);
        }

        return new ServiceActionResultDto(true, $"Команда {verb} выполнена", status);
    }

    public async Task<LogsResponseDto> GetLogsAsync(MonitoredService service, LogsQuery query, CancellationToken ct)
    {
        var args = new List<string> { "-u", service.Unit, "--no-pager", "-o", "json", "--utc" };

        if (!string.IsNullOrEmpty(query.AfterCursor))
        {
            args.Add("--after-cursor=" + query.AfterCursor);
        }
        else
        {
            var lines = Math.Clamp(query.Lines ?? 200, 1, Options.MaxLogLines);
            args.Add("-n");
            args.Add(lines.ToString(CultureInfo.InvariantCulture));
        }

        if (query.Since is { } since)
        {
            args.Add("--since=" + FormatJournalTime(since));
        }
        if (query.Until is { } until)
        {
            args.Add("--until=" + FormatJournalTime(until));
        }
        if (query.Priority is { } priority)
        {
            args.Add("-p");
            args.Add(Math.Clamp(priority, 0, 7).ToString(CultureInfo.InvariantCulture));
        }
        if (!string.IsNullOrWhiteSpace(query.Grep))
        {
            // Значение передаётся одним аргументом, без shell.
            args.Add("--grep=" + query.Grep);
            args.Add("--case-sensitive=false");
        }

        var (file, fullArgs) = WithSudo(Options.UseSudoForJournal, Options.JournalctlPath, args);
        var result = await _runner.RunAsync(file, fullArgs, Timeout, ct);

        // journalctl возвращает 1, если по --grep ничего не найдено.
        if (result.ExitCode != 0 && string.IsNullOrWhiteSpace(result.StdOut) && !string.IsNullOrWhiteSpace(result.StdErr))
        {
            throw new InvalidOperationException(Trim(result.StdErr));
        }

        var entries = new List<LogEntryDto>();
        foreach (var line in result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var entry = ParseJournalLine(line);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        var max = Options.MaxLogLines;
        if (entries.Count > max)
        {
            entries = entries.GetRange(entries.Count - max, max);
        }

        var lastCursor = entries.Count > 0 ? entries[^1].Cursor : query.AfterCursor;
        return new LogsResponseDto(entries, lastCursor);
    }

    private (string File, IEnumerable<string> Args) WithSudo(bool useSudo, string command, IEnumerable<string> args)
    {
        if (!useSudo)
        {
            return (command, args);
        }
        // -n: не запрашивать пароль, сразу ошибка, если нет правила в sudoers.
        return (Options.SudoPath, new[] { "-n", command }.Concat(args));
    }

    private static LogEntryDto? ParseJournalLine(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            var cursor = GetString(root, "__CURSOR") ?? string.Empty;
            var timestamp = DateTime.UnixEpoch;
            if (long.TryParse(GetString(root, "__REALTIME_TIMESTAMP"), out var micros))
            {
                timestamp = DateTime.UnixEpoch.AddTicks(micros * 10);
            }

            return new LogEntryDto(
                timestamp,
                int.TryParse(GetString(root, "PRIORITY"), out var p) ? p : null,
                GetString(root, "SYSLOG_IDENTIFIER") ?? GetString(root, "_COMM"),
                int.TryParse(GetString(root, "_PID"), out var pid) ? pid : null,
                GetMessage(root),
                cursor);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>
    /// MESSAGE может быть строкой, массивом байтов (если не UTF-8 / содержит управляющие символы) или null.
    /// </summary>
    private static string GetMessage(JsonElement root)
    {
        if (!root.TryGetProperty("MESSAGE", out var value))
        {
            return string.Empty;
        }
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                return value.GetString() ?? string.Empty;
            case JsonValueKind.Array:
                var bytes = value.EnumerateArray()
                    .Where(b => b.ValueKind == JsonValueKind.Number)
                    .Select(b => (byte)b.GetInt32())
                    .ToArray();
                return System.Text.Encoding.UTF8.GetString(bytes);
            default:
                return string.Empty;
        }
    }

    private static Dictionary<string, string> ParseProperties(string output)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = line.IndexOf('=');
            if (idx > 0)
            {
                dict[line[..idx]] = line[(idx + 1)..].Trim();
            }
        }
        return dict;
    }

    /// <summary>Разбор формата "Sat 2026-10-11 07:48:04 UTC" (при LC_ALL=C).</summary>
    private static DateTime? ParseSystemdTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "n/a")
        {
            return null;
        }
        var match = SystemdTimestampRegex().Match(value);
        if (!match.Success)
        {
            return null;
        }
        if (!DateTime.TryParseExact(
                match.Groups["dt"].Value,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var local))
        {
            return null;
        }

        var zone = match.Groups["tz"].Value;
        if (zone is "UTC" or "GMT")
        {
            return DateTime.SpecifyKind(local, DateTimeKind.Utc);
        }
        // Часовой пояс сервера совпадает с часовым поясом процесса бэкенда.
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZoneInfo.Local);
    }

    private static string FormatJournalTime(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value;
        return utc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC";
    }

    private static ServiceStatusDto Unknown(MonitoredService s, string? error) =>
        new(s.Id, s.Unit, DisplayNameOf(s), s.Description, s.Group, s.Discovered, s.AllowControl, "unknown", "unknown", "unknown", null, null, null, null, error);

    private static string DisplayNameOf(MonitoredService s) =>
        string.IsNullOrWhiteSpace(s.DisplayName) ? s.Unit : s.DisplayName;

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    [GeneratedRegex(@"^\w{3}\s+(?<dt>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\s*(?<tz>\S+)?$")]
    private static partial Regex SystemdTimestampRegex();
}
