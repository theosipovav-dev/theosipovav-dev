using System.Security.Claims;
using TFlexDocsControl.Api.Data;
using TFlexDocsControl.Api.Data.Entities;

namespace TFlexDocsControl.Api.Services;

public interface IAuditService
{
    Task WriteAsync(string action, string? target, bool success, string? details, CancellationToken ct, string? username = null);
}

public sealed class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<AuditService> _logger;

    public AuditService(AppDbContext db, IHttpContextAccessor http, ILogger<AuditService> logger)
    {
        _db = db;
        _http = http;
        _logger = logger;
    }

    public async Task WriteAsync(string action, string? target, bool success, string? details, CancellationToken ct, string? username = null)
    {
        var context = _http.HttpContext;
        var entry = new AuditEntry
        {
            Timestamp = DateTime.UtcNow,
            Username = username ?? context?.User.FindFirstValue(ClaimTypes.Name) ?? "system",
            Action = action,
            Target = target,
            Success = success,
            Details = details is { Length: > 2000 } ? details[..2000] : details,
            RemoteIp = context?.Connection.RemoteIpAddress?.ToString(),
        };

        _logger.LogInformation(
            "Audit: {User} {Action} {Target} success={Success} {Details}",
            entry.Username, action, target, success, entry.Details);

        _db.AuditEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
    }
}
