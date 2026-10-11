namespace TFlexDocsControl.Api.Contracts;

public sealed record ServiceStatusDto(
    string Id,
    string Unit,
    string DisplayName,
    string? Description,
    string? Group,
    bool Discovered,
    bool AllowControl,
    string LoadState,
    string ActiveState,
    string SubState,
    string? UnitFileState,
    int? MainPid,
    DateTime? ActiveSince,
    long? MemoryBytes,
    string? Error);

public sealed record ServiceActionResultDto(bool Success, string Message, ServiceStatusDto? Status);

public sealed record LogEntryDto(
    DateTime Timestamp,
    int? Priority,
    string? Identifier,
    int? Pid,
    string Message,
    string Cursor);

public sealed record LogsResponseDto(IReadOnlyList<LogEntryDto> Entries, string? LastCursor);

public sealed class LogsQuery
{
    /// <summary>Количество последних строк.</summary>
    public int? Lines { get; set; }

    /// <summary>Начало периода (UTC/ISO 8601).</summary>
    public DateTime? Since { get; set; }

    /// <summary>Конец периода (UTC/ISO 8601).</summary>
    public DateTime? Until { get; set; }

    /// <summary>Максимальный приоритет syslog: 0 (emerg) ... 7 (debug).</summary>
    public int? Priority { get; set; }

    /// <summary>Фильтр по тексту (journalctl --grep).</summary>
    public string? Grep { get; set; }

    /// <summary>Вернуть только записи после этого курсора (для "живого" режима).</summary>
    public string? AfterCursor { get; set; }
}

public enum ServiceAction
{
    Start,
    Stop,
    Restart,
}
