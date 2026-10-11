using System.ComponentModel.DataAnnotations;

namespace TFlexDocsControl.Api.Contracts;

public sealed class LoginRequest
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public sealed record LoginResponse(string Token, DateTime ExpiresAt, UserDto User);

public sealed class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed record UserDto(
    int Id,
    string Username,
    string? DisplayName,
    string Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

public sealed class CreateUserRequest
{
    [Required, RegularExpression(@"^[A-Za-z0-9._-]{3,64}$", ErrorMessage = "Логин: 3-64 символа, латиница, цифры, '.', '_', '-'")]
    public string Username { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? DisplayName { get; set; }

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public sealed class UpdateUserRequest
{
    [MaxLength(128)]
    public string? DisplayName { get; set; }

    [Required]
    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public sealed class ResetPasswordRequest
{
    [Required, MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed record AuditEntryDto(
    long Id,
    DateTime Timestamp,
    string Username,
    string Action,
    string? Target,
    bool Success,
    string? Details,
    string? RemoteIp);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
