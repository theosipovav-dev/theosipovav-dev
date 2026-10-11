namespace TFlexDocsControl.Api.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "TFlexDocsControl";
    public string Audience { get; set; } = "TFlexDocsControl";

    /// <summary>Секретный ключ подписи, не короче 32 символов.</summary>
    public string Key { get; set; } = string.Empty;

    public int ExpiresMinutes { get; set; } = 480;
}
