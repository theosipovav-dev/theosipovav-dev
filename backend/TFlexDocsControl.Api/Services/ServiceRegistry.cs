using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TFlexDocsControl.Api.Options;

namespace TFlexDocsControl.Api.Services;

public interface IServiceRegistry
{
    /// <summary>Все отслеживаемые службы: перечисленные явно и найденные по шаблонам.</summary>
    Task<IReadOnlyList<MonitoredService>> GetAllAsync(CancellationToken ct);

    Task<MonitoredService?> FindAsync(string id, CancellationToken ct);
}

/// <summary>
/// Список служб из конфигурации плюс экземпляры, найденные через systemctl по шаблонам
/// (например, несколько экземпляров сервера T-FLEX DOCs "tflexdocs@*.service").
/// Результат поиска кэшируется на DiscoveryCacheSeconds.
/// </summary>
public sealed partial class ServiceRegistry : IServiceRegistry
{
    public const string DefaultDiscoveredGroup = "Найденные службы";

    private readonly IProcessRunner _runner;
    private readonly IOptionsMonitor<ServiceControlOptions> _options;
    private readonly ILogger<ServiceRegistry> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private IReadOnlyList<MonitoredService> _discovered = Array.Empty<MonitoredService>();
    private DateTime _discoveredAt = DateTime.MinValue;

    public ServiceRegistry(IProcessRunner runner, IOptionsMonitor<ServiceControlOptions> options, ILogger<ServiceRegistry> logger)
    {
        _runner = runner;
        _options = options;
        _logger = logger;
        // При изменении конфигурации сразу пересобираем список.
        _options.OnChange(_ => _discoveredAt = DateTime.MinValue);
    }

    private ServiceControlOptions Options => _options.CurrentValue;

    public async Task<IReadOnlyList<MonitoredService>> GetAllAsync(CancellationToken ct)
    {
        var explicitServices = Options.Services;
        var discovered = await GetDiscoveredAsync(ct);
        if (discovered.Count == 0)
        {
            return explicitServices;
        }

        var ids = new HashSet<string>(explicitServices.Select(s => s.Id), StringComparer.OrdinalIgnoreCase);
        var units = new HashSet<string>(explicitServices.Select(s => s.Unit), StringComparer.Ordinal);
        var result = new List<MonitoredService>(explicitServices);
        foreach (var s in discovered)
        {
            // Явно описанная служба имеет приоритет над найденной.
            if (units.Contains(s.Unit))
            {
                continue;
            }

            var service = s;
            if (!ids.Add(service.Id))
            {
                // Id совпал с другой службой: добавляем числовой суффикс.
                var n = 2;
                while (!ids.Add($"{s.Id}-{n}"))
                {
                    n++;
                }
                service = Clone(s, $"{s.Id}-{n}");
            }
            result.Add(service);
        }
        return result;
    }

    public async Task<MonitoredService?> FindAsync(string id, CancellationToken ct)
    {
        var all = await GetAllAsync(ct);
        return all.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlyList<MonitoredService>> GetDiscoveredAsync(CancellationToken ct)
    {
        var rules = Options.Discovery;
        if (rules.Count == 0)
        {
            return Array.Empty<MonitoredService>();
        }

        var ttl = TimeSpan.FromSeconds(Math.Max(0, Options.DiscoveryCacheSeconds));
        if (DateTime.UtcNow - _discoveredAt < ttl)
        {
            return _discovered;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (DateTime.UtcNow - _discoveredAt < ttl)
            {
                return _discovered;
            }

            var list = new List<MonitoredService>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in rules)
            {
                foreach (var unit in await FindUnitsAsync(rule.Pattern, ct))
                {
                    if (seen.Add(unit))
                    {
                        list.Add(CreateService(rule, unit));
                    }
                }
            }

            _discovered = list
                .OrderBy(s => s.Group, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(s => s.Unit, StringComparer.Ordinal)
                .ToList();
            _discoveredAt = DateTime.UtcNow;
            return _discovered;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Если поиск не удался, продолжаем с прежним списком.
            _logger.LogError(ex, "Не удалось найти службы по шаблонам");
            return _discovered;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Ищет unit-ы по шаблону среди загруженных (в т.ч. остановленных) и установленных.
    /// Экземпляры шаблонных unit-ов попадают в list-units, если запущены, включены или упоминались с момента загрузки.
    /// </summary>
    private async Task<IReadOnlyCollection<string>> FindUnitsAsync(string pattern, CancellationToken ct)
    {
        var timeout = TimeSpan.FromSeconds(Math.Max(5, Options.CommandTimeoutSeconds));
        var commands = new[]
        {
            new[] { "list-units", "--all", "--type=service", "--plain", "--no-legend", "--no-pager", pattern },
            new[] { "list-unit-files", "--type=service", "--plain", "--no-legend", "--no-pager", pattern },
        };

        var units = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var args in commands)
        {
            var result = await _runner.RunAsync(Options.SystemctlPath, args, timeout, ct);
            if (result.ExitCode != 0)
            {
                _logger.LogWarning("systemctl {Args} завершился с кодом {Code}: {Error}", string.Join(' ', args), result.ExitCode, result.StdErr.Trim());
                continue;
            }

            foreach (var line in result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var unit = line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                // Сам шаблон ("name@.service") службой не является.
                if (unit is not null && UnitRegex().IsMatch(unit) && !unit.EndsWith("@.service", StringComparison.Ordinal))
                {
                    units.Add(unit);
                }
            }
        }
        return units;
    }

    private static MonitoredService CreateService(ServiceDiscoveryRule rule, string unit)
    {
        var baseName = unit[..^".service".Length];
        var at = baseName.IndexOf('@');
        var instance = at >= 0 ? baseName[(at + 1)..] : baseName;

        string Format(string? template) =>
            (template ?? string.Empty).Replace("{instance}", instance).Replace("{unit}", unit);

        var group = Format(rule.Group);
        return new MonitoredService
        {
            Id = MakeId(baseName),
            Unit = unit,
            DisplayName = string.IsNullOrWhiteSpace(rule.DisplayName) ? unit : Format(rule.DisplayName),
            Description = string.IsNullOrWhiteSpace(rule.Description) ? null : Format(rule.Description),
            Group = string.IsNullOrWhiteSpace(group) ? DefaultDiscoveredGroup : group,
            AllowControl = rule.AllowControl,
            Discovered = true,
        };
    }

    private static MonitoredService Clone(MonitoredService s, string id) => new()
    {
        Id = id,
        Unit = s.Unit,
        DisplayName = s.DisplayName,
        Description = s.Description,
        Group = s.Group,
        AllowControl = s.AllowControl,
        Discovered = s.Discovered,
    };

    private static string MakeId(string baseName)
    {
        var id = InvalidIdChars().Replace(baseName, "-").Trim('-');
        return id.Length > 64 ? id[..64] : id;
    }

    [GeneratedRegex(@"[^A-Za-z0-9_-]+")]
    private static partial Regex InvalidIdChars();

    [GeneratedRegex(@"^(?!-)[A-Za-z0-9_.@:\\-]{1,200}\.service$")]
    private static partial Regex UnitRegex();
}
