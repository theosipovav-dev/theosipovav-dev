using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TFlexDocsControl.Api.Auth;
using TFlexDocsControl.Api.Contracts;
using TFlexDocsControl.Api.Data;
using TFlexDocsControl.Api.Data.Entities;
using TFlexDocsControl.Api.Services;

namespace TFlexDocsControl.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    private readonly ITokenService _tokens;
    private readonly IAuditService _audit;

    public AuthController(AppDbContext db, IPasswordHasher<User> hasher, ITokenService tokens, IAuditService audit)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _audit = audit;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == request.Username, ct);
        var verified = user is not null
            && user.IsActive
            && _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) != PasswordVerificationResult.Failed;

        if (!verified)
        {
            await _audit.WriteAsync("login", null, false, "Неверный логин или пароль", ct, request.Username);
            return Unauthorized(new ProblemDetails { Title = "Неверный логин или пароль" });
        }

        user!.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("login", null, true, null, ct, user.Username);

        var (token, expires) = _tokens.CreateToken(user);
        return new LoginResponse(token, expires, UsersController.ToDto(user));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        var user = await CurrentUserAsync(ct);
        return user is null ? Unauthorized() : UsersController.ToDto(user);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await CurrentUserAsync(ct);
        if (user is null)
        {
            return Unauthorized();
        }

        if (_hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            return BadRequest(new ProblemDetails { Title = "Текущий пароль указан неверно" });
        }

        user.PasswordHash = _hasher.HashPassword(user, request.NewPassword);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("change-password", user.Username, true, null, ct);
        return NoContent();
    }

    private async Task<User?> CurrentUserAsync(CancellationToken ct)
    {
        var id = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var value) ? value : 0;
        return await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
    }
}
