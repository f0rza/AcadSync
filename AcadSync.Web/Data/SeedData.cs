using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AcadSync.Web.Data
{
    public static class SeedData
    {
        public static async Task EnsureAdminAsync(IServiceProvider services, IConfiguration configuration)
        {
            var loggerFactory = services.GetService<ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger("SeedData");

            try
            {
                var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
                var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

                var adminUser = configuration["AcadSync:AdminUser"];
                var adminPassword = configuration["AcadSync:AdminPassword"];

                if (string.IsNullOrWhiteSpace(adminUser) || string.IsNullOrWhiteSpace(adminPassword))
                {
                    logger?.LogWarning("Admin credentials not configured (AcadSync:AdminUser / AdminPassword). Skipping seeding.");
                    return;
                }

                // Ensure role exists
                const string adminRole = "Admin";
                if (!await roleManager.RoleExistsAsync(adminRole))
                {
                    var roleResult = await roleManager.CreateAsync(new IdentityRole(adminRole));
                    if (!roleResult.Succeeded)
                    {
                        logger?.LogWarning("Failed to create role {Role}: {Errors}", adminRole, string.Join(",", roleResult.Errors));
                    }
                }

                // Create or update admin user
                var user = await userManager.FindByNameAsync(adminUser);
                if (user == null)
                {
                    user = new IdentityUser
                    {
                        UserName = adminUser,
                        Email = adminUser,
                        EmailConfirmed = true
                    };

                    var createResult = await userManager.CreateAsync(user, adminPassword);
                    if (!createResult.Succeeded)
                    {
                        logger?.LogError("Failed to create admin user {User}: {Errors}", adminUser, string.Join(",", createResult.Errors));
                        return;
                    }

                    logger?.LogInformation("Created admin user {User}", adminUser);
                }
                else
                {
                    logger?.LogInformation("Admin user {User} already exists", adminUser);
                }

                // Ensure user is in role
                if (!await userManager.IsInRoleAsync(user, adminRole))
                {
                    var addRoleResult = await userManager.AddToRoleAsync(user, adminRole);
                    if (!addRoleResult.Succeeded)
                    {
                        logger?.LogWarning("Failed to add user {User} to role {Role}: {Errors}", adminUser, adminRole, string.Join(",", addRoleResult.Errors));
                    }
                    else
                    {
                        logger?.LogInformation("Added user {User} to role {Role}", adminUser, adminRole);
                    }
                }
            }
            catch (Exception ex)
            {
                // Do not throw from seeding - log and continue
                var l = services.GetService<ILoggerFactory>()?.CreateLogger("SeedData");
                l?.LogError(ex, "Unexpected error while seeding admin user");
            }
        }
    }
}
