namespace TFlexDocsControl.Api.Data.Entities;

public sealed class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.Viewer;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}

public static class Roles
{
    /// <summary>Полный доступ, включая управление пользователями.</summary>
    public const string Admin = "Admin";

    /// <summary>Просмотр и управление службами.</summary>
    public const string Operator = "Operator";

    /// <summary>Только просмотр статусов и логов.</summary>
    public const string Viewer = "Viewer";

    public static readonly string[] All = { Admin, Operator, Viewer };

    public const string CanControl = Admin + "," + Operator;
}
