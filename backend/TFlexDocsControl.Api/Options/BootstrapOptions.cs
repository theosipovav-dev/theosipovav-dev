namespace TFlexDocsControl.Api.Options;

/// <summary>
/// Первичный администратор, создаётся при старте, если в базе нет ни одного пользователя.
/// </summary>
public sealed class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    public string AdminUsername { get; set; } = "admin";

    /// <summary>Если пусто, пароль будет сгенерирован и выведен в лог один раз.</summary>
    public string? AdminPassword { get; set; }
}
