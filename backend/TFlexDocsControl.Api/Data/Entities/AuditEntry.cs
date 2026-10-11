namespace TFlexDocsControl.Api.Data.Entities;

/// <summary>Журнал действий пользователей.</summary>
public sealed class AuditEntry
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Username { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Target { get; set; }
    public bool Success { get; set; }
    public string? Details { get; set; }
    public string? RemoteIp { get; set; }
}
