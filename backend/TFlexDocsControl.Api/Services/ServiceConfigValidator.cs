using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TFlexDocsControl.Api.Options;

namespace TFlexDocsControl.Api.Services;

/// <summary>
/// Проверка списка служб при старте: корректные имена unit-ов и уникальные идентификаторы.
/// </summary>
public sealed partial class ServiceConfigValidator : IValidateOptions<ServiceControlOptions>
{
    public ValidateOptionsResult Validate(string? name, ServiceControlOptions options)
    {
        var errors = new List<string>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var s in options.Services)
        {
            if (!IdRegex().IsMatch(s.Id))
            {
                errors.Add($"Некорректный Id службы '{s.Id}': допустимы латиница, цифры, '-' и '_'.");
            }
            else if (!ids.Add(s.Id))
            {
                errors.Add($"Повторяющийся Id службы '{s.Id}'.");
            }

            if (!UnitRegex().IsMatch(s.Unit))
            {
                errors.Add($"Некорректное имя unit '{s.Unit}' у службы '{s.Id}'.");
            }
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    [GeneratedRegex(@"^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex IdRegex();

    // Имя unit: без пробелов, не начинается с '-', с суффиксом типа.
    [GeneratedRegex(@"^(?!-)[A-Za-z0-9_.@:\\-]{1,200}\.(service|target|socket|timer)$")]
    private static partial Regex UnitRegex();
}
