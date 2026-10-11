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

    /// <summary>Отслеживаемые службы, перечисленные явно.</summary>
    public List<MonitoredService> Services { get; set; } = new();

    /// <summary>
    /// Автоматический поиск служб по шаблону имени unit-а, например "tflexdocs@*.service".
    /// Удобно, когда на сервере запущено несколько экземпляров T-FLEX DOCs.
    /// </summary>
    public List<ServiceDiscoveryRule> Discovery { get; set; } = new();

    /// <summary>Как часто обновлять список найденных по шаблону служб, секунд.</summary>
    public int DiscoveryCacheSeconds { get; set; } = 30;
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

    /// <summary>
    /// Группа для отображения, например имя экземпляра сервера приложений ("Основной", "Тестовый").
    /// Службы одной группы показываются вместе.
    /// </summary>
    public string? Group { get; set; }

    /// <summary>Разрешены ли start/stop/restart для этой службы (иначе только просмотр).</summary>
    public bool AllowControl { get; set; } = true;

    /// <summary>Служба найдена по шаблону, а не перечислена явно.</summary>
    public bool Discovered { get; set; }
}

public sealed class ServiceDiscoveryRule
{
    /// <summary>Шаблон имени unit-а в синтаксисе systemctl: "tflexdocs@*.service", "tflexdocs-*.service".</summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>
    /// Шаблон отображаемого имени. {instance} - часть имени после '@' (или имя unit-а без ".service"),
    /// {unit} - полное имя unit-а.
    /// </summary>
    public string DisplayName { get; set; } = "{unit}";

    public string? Description { get; set; }

    /// <summary>Шаблон группы, поддерживает {instance} и {unit}. Пусто - группа "Найденные службы".</summary>
    public string? Group { get; set; }

    public bool AllowControl { get; set; } = true;
}
