using System.Net.Http.Json;
using AllocationReview.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AllocationReview.Api.Tests.Infrastructure;

/// <summary>
/// Hosts the real API in memory with the "Testing" environment. The database is SQLite in memory
/// (one open connection per factory), built from the same EF Core model and seeded from the same
/// seed file as the app, so the tests need no PostgreSQL, Docker or network access.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "Admin#Test2026";
    public const string ViewerEmail = "viewer@test.local";
    public const string ViewerPassword = "Viewer#Test2026";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ApiFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:MigrateOnStartup"] = "true",
            ["Database:SeedOnStartup"] = "true",
            ["DemoUsers:AdminEmail"] = AdminEmail,
            ["DemoUsers:AdminPassword"] = AdminPassword,
            ["DemoUsers:ViewerEmail"] = ViewerEmail,
            ["DemoUsers:ViewerPassword"] = ViewerPassword,
            ["Logging:LogLevel:Default"] = "Warning",
        }));

        builder.ConfigureTestServices(services =>
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection)));
    }

    /// <summary>A client that keeps cookies, like a browser.</summary>
    public HttpClient CreateBrowserClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });

    public async Task<HttpClient> SignInAsync(string email, string password)
    {
        var client = CreateBrowserClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        return client;
    }

    public Task<HttpClient> SignInAsAdminAsync() => SignInAsync(AdminEmail, AdminPassword);

    public Task<HttpClient> SignInAsViewerAsync() => SignInAsync(ViewerEmail, ViewerPassword);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
