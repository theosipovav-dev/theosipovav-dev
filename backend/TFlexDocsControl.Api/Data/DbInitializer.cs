using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TFlexDocsControl.Api.Data.Entities;
using TFlexDocsControl.Api.Options;

namespace TFlexDocsControl.Api.Data;

public static class DbInitializer
{
    /// <summary>
    /// Применяет миграции и создаёт первого администратора, если пользователей нет.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");
        var bootstrap = scope.ServiceProvider.GetRequiredService<IOptions<BootstrapOptions>>().Value;
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        await db.Database.MigrateAsync(ct);

        if (await db.Users.AnyAsync(ct))
        {
            return;
        }

        var password = bootstrap.AdminPassword;
        var generated = string.IsNullOrWhiteSpace(password);
        if (generated)
        {
            password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(12));
        }

        var admin = new User
        {
            Username = bootstrap.AdminUsername,
            DisplayName = "Администратор",
            Role = Roles.Admin,
            IsActive = true,
        };
        admin.PasswordHash = hasher.HashPassword(admin, password!);
        db.Users.Add(admin);
        await db.SaveChangesAsync(ct);

        if (generated)
        {
            logger.LogWarning(
                "Создан администратор '{User}' со сгенерированным паролем: {Password}. Смените пароль после первого входа.",
                admin.Username, password);
        }
        else
        {
            logger.LogWarning("Создан администратор '{User}'. Смените пароль после первого входа.", admin.Username);
        }
    }
}
