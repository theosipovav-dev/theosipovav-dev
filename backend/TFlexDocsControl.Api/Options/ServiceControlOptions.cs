namespace TFlexDocsControl.Api.Options;

/// <summary>
/// Настройки управления службами (секция "ServiceControl" в appsettings.json).
/// </summary>
public sealed class ServiceControlOptions
{
    public const string SectionName = "ServiceControl";

    /// <summary>Путь к systemctl.</summary>
    public string SystemctlPath { get; set; } = "/usr/bin/systemctl";

    /// <summary>Путь к journalctl.</summary>
    public string JournalctlPath { get; set; } = "/usr/bin/journalctl";

    /// <summary>Путь к sudo.</summary>
    public string SudoPath { get; set; } = "/usr/bin/sudo";

    /// <summary>Выполнять start/stop/restart через "sudo -n" (нужно правило в sudoers).</summary>
    public bool UseSudoForControl { get; set; } = true;

    /// <summary>Читать журнал через "sudo -n" (иначе пользователь должен входить в группу systemd-journal).</summary>
    public bool UseSudoForJournal { get; set; }

    /// <summary>Таймаут выполнения команд, секунд.</summary>
    public int CommandTimeoutSeconds { get; set; } = 60;

    /// <summary>Максимальное число строк журнала за один запрос.</summary>
    public int MaxLogLines { get; set; } = 2000;

    /// <summary>Отслеживаемые службы.</summary>
    public List<MonitoredService> Services { get; set; } = new();
}

public sealed class MonitoredService
{
    /// <summary>Идентификатор службы в API (латиница, цифры, '-', '_').</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Имя systemd unit, например "tflexdocs.service".</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>Отображаемое имя.</summary>
    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Разрешены ли start/stop/restart для этой службы (иначе только просмотр).</summary>
    public bool AllowControl { get; set; } = true;
}
