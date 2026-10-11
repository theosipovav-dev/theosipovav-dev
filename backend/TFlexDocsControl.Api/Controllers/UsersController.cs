using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TFlexDocsControl.Api.Contracts;
using TFlexDocsControl.Api.Data;
using TFlexDocsControl.Api.Data.Entities;
using TFlexDocsControl.Api.Services;

namespace TFlexDocsControl.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = Roles.Admin)]
public sealed class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    private readonly IAuditService _audit;

    public UsersController(AppDbContext db, IPasswordHasher<User> hasher, IAuditService audit)
    {
        _db = db;
        _hasher = hasher;
        _audit = audit;
    }

    internal static UserDto ToDto(User u) =>
        new(u.Id, u.Username, u.DisplayName, u.Role, u.IsActive, u.CreatedAt, u.LastLoginAt);

    [HttpGet]
    public async Task<IReadOnlyList<UserDto>> GetAll(CancellationToken ct)
    {
        var users = await _db.Users.AsNoTracking().OrderBy(u => u.Username).ToListAsync(ct);
        return users.Select(ToDto).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct)
    {
        if (!Roles.All.Contains(request.Role))
        {
            return BadRequest(new ProblemDetails { Title = "Неизвестная роль" });
        }
        if (await _db.Users.AnyAsync(u => u.Username == request.Username, ct))
        {
            return Conflict(new ProblemDetails { Title = "Пользователь с таким логином уже существует" });
        }

        var user = new User
        {
            Username = request.Username,
            DisplayName = request.DisplayName,
            Role = request.Role,
            IsActive = request.IsActive,
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("user-create", user.Username, true, $"role={user.Role}", ct);

        return CreatedAtAction(nameof(GetAll), ToDto(user));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UserDto>> Update(int id, UpdateUserRequest request, CancellationToken ct)
    {
        if (!Roles.All.Contains(request.Role))
        {
            return BadRequest(new ProblemDetails { Title = "Неизвестная роль" });
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return NotFound();
        }

        var losesAdmin = user.Role == Roles.Admin && user.IsActive && (request.Role != Roles.Admin || !request.IsActive);
        if (losesAdmin && IsCurrentUser(user))
        {
            return BadRequest(new ProblemDetails { Title = "Нельзя снять с себя роль администратора или заблокировать себя" });
        }
        if (losesAdmin && !await HasOtherActiveAdminAsync(user.Id, ct))
        {
            return BadRequest(new ProblemDetails { Title = "Должен остаться хотя бы один активный администратор" });
        }

        user.DisplayName = request.DisplayName;
        user.Role = request.Role;
        user.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("user-update", user.Username, true, $"role={user.Role}, active={user.IsActive}", ct);

        return ToDto(user);
    }

    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return NotFound();
        }

        user.PasswordHash = _hasher.HashPassword(user, request.NewPassword);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("user-reset-password", user.Username, true, null, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return NotFound();
        }
        if (IsCurrentUser(user))
        {
            return BadRequest(new ProblemDetails { Title = "Нельзя удалить самого себя" });
        }
        if (user.Role == Roles.Admin && user.IsActive && !await HasOtherActiveAdminAsync(user.Id, ct))
        {
            return BadRequest(new ProblemDetails { Title = "Должен остаться хотя бы один активный администратор" });
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("user-delete", user.Username, true, null, ct);
        return NoContent();
    }

    private bool IsCurrentUser(User user) =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) == user.Id.ToString();

    private Task<bool> HasOtherActiveAdminAsync(int exceptId, CancellationToken ct) =>
        _db.Users.AnyAsync(u => u.Id != exceptId && u.Role == Roles.Admin && u.IsActive, ct);
}
