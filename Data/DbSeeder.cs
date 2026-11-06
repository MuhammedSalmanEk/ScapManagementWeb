using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ScrapManagement.Web.Infrastructure;
using ScrapManagement.Web.Models;

namespace ScrapManagement.Web.Data;

public static class DbSeeder
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        // Use migrations once you have created them (dotnet ef migrations add Initial); until then create the schema directly.
        if (db.Database.GetMigrations().Any())
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { AppRoles.MainAdmin, AppRoles.ShopAdmin, AppRoles.Worker })
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        if (!await db.SubscriptionPlans.AnyAsync())
        {
            db.SubscriptionPlans.Add(new SubscriptionPlan
            {
                // Name = "Starter (Yearly)",
                Name = "Starter",
                Description = "Shop admin + 2 workers, purchase billing, stock and daily reports.",
                MonthlyPrice = 100m, YearlyPrice = 999m,
                OfferMonthly = true, OfferYearly = true,
                // YearlyPrice = 999m,
                MaxWorkers = 2,
                AllowReportDownloads = true,
                SalesModuleEnabled = false
            });
            await db.SaveChangesAsync();
        }

        var seed = sp.GetRequiredService<IOptions<SeedOptions>>().Value;
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        if (await userManager.FindByEmailAsync(seed.MainAdminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = seed.MainAdminEmail,
                Email = seed.MainAdminEmail,
                EmailConfirmed = true,
                FullName = seed.MainAdminName,
                IsActive = true
            };
            var result = await userManager.CreateAsync(admin, seed.MainAdminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, AppRoles.MainAdmin);
                logger.LogWarning("Main admin created: {Email}. Change the seeded password immediately.", seed.MainAdminEmail);
            }
            else
            {
                logger.LogError("Could not create main admin: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
