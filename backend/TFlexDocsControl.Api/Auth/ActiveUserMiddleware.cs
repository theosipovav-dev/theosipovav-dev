using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TFlexDocsControl.Api.Data;

namespace TFlexDocsControl.Api.Auth;

/// <summary>
/// Отклоняет запросы с действующим токеном, если пользователь удалён, заблокирован или сменил роль.
/// </summary>
public sealed class ActiveUserMiddleware
{
    private readonly RequestDelegate _next;

    public ActiveUserMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        var principal = context.User;
        if (principal.Identity?.IsAuthenticated == true)
        {
            var idValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = principal.FindFirstValue(ClaimTypes.Role);
            var ok = int.TryParse(idValue, out var id)
                && await db.Users.AsNoTracking().AnyAsync(
                    u => u.Id == id && u.IsActive && u.Role == role,
                    context.RequestAborted);

            if (!ok)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }

        await _next(context);
    }
}
