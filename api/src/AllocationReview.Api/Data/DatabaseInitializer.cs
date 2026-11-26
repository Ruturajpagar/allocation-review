using AllocationReview.Api.Auth;
using AllocationReview.Api.Data.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AllocationReview.Api.Data;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Apply EF Core migrations when the app starts (on in Development, off in Production).</summary>
    public bool MigrateOnStartup { get; set; }

    /// <summary>Seed the synthetic runs and the demo users when the app starts.</summary>
    public bool SeedOnStartup { get; set; }
}

/// <summary>The two local demo users. Passwords come from configuration, never from code.</summary>
public sealed class DemoUsersOptions
{
    public const string SectionName = "DemoUsers";

    public string AdminEmail { get; set; } = "admin@demo.local";

    public string? AdminPassword { get; set; }

    public string ViewerEmail { get; set; } = "viewer@demo.local";

    public string? ViewerPassword { get; set; }
}

/// <summary>Creates the schema and seeds runs, roles and demo users. Safe to run more than once.</summary>
public sealed class DatabaseInitializer(
    AppDbContext db,
    UserManager<IdentityUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<DemoUsersOptions> demoUsers,
    ILogger<DatabaseInitializer> logger)
{
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        // Migrations are written for PostgreSQL, the app's database.
        if (db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            logger.LogInformation("Applying database migrations");
            await db.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            // Test databases (SQLite in memory) have no PostgreSQL migrations; build the schema from the model.
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }
    }

    public async Task SeedAsync(string? seedFilePath = null, CancellationToken cancellationToken = default)
    {
        await SeedRolesAndUsersAsync();
        await SeedRunsAsync(seedFilePath ?? AllocationSeedFile.DefaultPath, cancellationToken);
    }

    private async Task SeedRunsAsync(string seedFilePath, CancellationToken cancellationToken)
    {
        var runs = await AllocationSeedFile.LoadAsync(seedFilePath, cancellationToken);
        var existing = await db.AllocationRuns.Select(r => r.Id).ToListAsync(cancellationToken);
        var missing = runs.Where(r => !existing.Contains(r.Id)).ToList();

        if (missing.Count == 0)
        {
            logger.LogInformation("Allocation runs already seeded ({RunCount} runs)", existing.Count);
            return;
        }

        db.AllocationRuns.AddRange(missing);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {RunCount} allocation runs", missing.Count);
    }

    private async Task SeedRolesAndUsersAsync()
    {
        foreach (var role in new[] { Roles.Admin, Roles.Viewer })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var options = demoUsers.Value;
        await EnsureUserAsync(options.AdminEmail, options.AdminPassword, Roles.Admin);
        await EnsureUserAsync(options.ViewerEmail, options.ViewerPassword, Roles.Viewer);
    }

    private async Task EnsureUserAsync(string email, string? password, string role)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("No password configured for the demo {Role} user; skipping it", role);
            return;
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var created = await userManager.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create the demo {role} user: {string.Join("; ", created.Errors.Select(e => e.Description))}");
            }

            logger.LogInformation("Created the demo {Role} user", role);
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }
}
