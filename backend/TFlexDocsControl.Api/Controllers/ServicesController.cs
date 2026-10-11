using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TFlexDocsControl.Api.Contracts;
using TFlexDocsControl.Api.Data.Entities;
using TFlexDocsControl.Api.Services;

namespace TFlexDocsControl.Api.Controllers;

[ApiController]
[Route("api/services")]
[Authorize]
public sealed class ServicesController : ControllerBase
{
    private readonly IServiceManager _manager;
    private readonly IServiceRegistry _registry;
    private readonly IAuditService _audit;

    public ServicesController(IServiceManager manager, IServiceRegistry registry, IAuditService audit)
    {
        _manager = manager;
        _registry = registry;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IReadOnlyList<ServiceStatusDto>> GetAll(CancellationToken ct)
    {
        var services = await _registry.GetAllAsync(ct);
        var tasks = services.Select(s => _manager.GetStatusAsync(s, ct));
        return await Task.WhenAll(tasks);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ServiceStatusDto>> Get(string id, CancellationToken ct)
    {
        var service = await _registry.FindAsync(id, ct);
        if (service is null)
        {
            return NotFound();
        }
        return await _manager.GetStatusAsync(service, ct);
    }

    [HttpPost("{id}/start")]
    [Authorize(Roles = Roles.CanControl)]
    public Task<ActionResult<ServiceActionResultDto>> Start(string id, CancellationToken ct) =>
        Execute(id, ServiceAction.Start, ct);

    [HttpPost("{id}/stop")]
    [Authorize(Roles = Roles.CanControl)]
    public Task<ActionResult<ServiceActionResultDto>> Stop(string id, CancellationToken ct) =>
        Execute(id, ServiceAction.Stop, ct);

    [HttpPost("{id}/restart")]
    [Authorize(Roles = Roles.CanControl)]
    public Task<ActionResult<ServiceActionResultDto>> Restart(string id, CancellationToken ct) =>
        Execute(id, ServiceAction.Restart, ct);

    [HttpGet("{id}/logs")]
    public async Task<ActionResult<LogsResponseDto>> Logs(string id, [FromQuery] LogsQuery query, CancellationToken ct)
    {
        var service = await _registry.FindAsync(id, ct);
        if (service is null)
        {
            return NotFound();
        }

        try
        {
            return await _manager.GetLogsAsync(service, query, ct);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(title: "Не удалось прочитать журнал", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private async Task<ActionResult<ServiceActionResultDto>> Execute(string id, ServiceAction action, CancellationToken ct)
    {
        var service = await _registry.FindAsync(id, ct);
        if (service is null)
        {
            return NotFound();
        }

        // Действие не отменяется при обрыве соединения клиента, чтобы не прерывать systemctl на полпути.
        var result = await _manager.ExecuteAsync(service, action, CancellationToken.None);
        await _audit.WriteAsync(
            "service-" + action.ToString().ToLowerInvariant(),
            service.Unit,
            result.Success,
            result.Message,
            CancellationToken.None);

        return result.Success ? result : UnprocessableEntity(result);
    }
}
