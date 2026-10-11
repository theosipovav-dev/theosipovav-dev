using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TFlexDocsControl.Api.Contracts;
using TFlexDocsControl.Api.Data;
using TFlexDocsControl.Api.Data.Entities;

namespace TFlexDocsControl.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Roles = Roles.Admin)]
public sealed class AuditController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuditController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<PagedResult<AuditEntryDto>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? username = null,
        [FromQuery] string? action = null,
        [FromQuery] string? target = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var query = _db.AuditEntries.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(username))
        {
            query = query.Where(e => e.Username == username);
        }
        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(e => e.Action == action);
        }
        if (!string.IsNullOrWhiteSpace(target))
        {
            query = query.Where(e => e.Target == target);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new AuditEntryDto(e.Id, e.Timestamp, e.Username, e.Action, e.Target, e.Success, e.Details, e.RemoteIp))
            .ToListAsync(ct);

        return new PagedResult<AuditEntryDto>(items, total, page, pageSize);
    }
}
