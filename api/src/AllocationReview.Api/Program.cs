using System.Text.Json;
using System.Text.Json.Serialization;
using AllocationReview.Api.Auth;
using AllocationReview.Api.Data;
using AllocationReview.Api.Infrastructure;
using AllocationReview.Api.Runs;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---- Logging: readable single lines locally, structured JSON with scopes everywhere else ----
builder.Logging.ClearProviders();
// Keep the W3C trace ID in scopes (for tracing tools); drop the noisier span and parent IDs.
builder.Logging.Configure(o => o.ActivityTrackingOptions = ActivityTrackingOptions.TraceId);
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddSimpleConsole(o =>
    {
        o.IncludeScopes = true;
        o.SingleLine = true;
        o.TimestampFormat = "HH:mm:ss ";
    });
}
else
{
    builder.Logging.AddJsonConsole(o =>
    {
        o.IncludeScopes = true;
        o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        o.UseUtcTimestamp = true;
    });
}

// ---- Configuration-bound options ----
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<DemoUsersOptions>(builder.Configuration.GetSection(DemoUsersOptions.SectionName));

// ---- Data ----
// The integration tests register their own provider (SQLite in memory), so skip PostgreSQL there.
if (!builder.Environment.IsEnvironment("Testing"))
{
    var connectionString = builder.Configuration.GetConnectionString("Default");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "ConnectionStrings:Default is not set. Supply it with the ConnectionStrings__Default environment variable " +
            "or appsettings.Development.json (see README).");
    }

    builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));
}

builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddScoped<IRunReviewService, RunReviewService>();

// ---- Web ----
builder.Services.AddAppAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddAppProblemDetails();
builder.Services
    .AddControllers()
    // Enums travel as camelCase strings ("partiallyFilled"), matching the property names.
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

// `dotnet run -- seed` (or `AllocationReview.Api.exe seed`): create or upgrade the schema, seed, and exit.
if (args.Contains("seed", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.MigrateAsync();
    await initializer.SeedAsync();
    app.Logger.LogInformation("Database migrated and seeded");
    return;
}

await InitializeDatabaseAsync(app);

// ---- HTTP pipeline ----
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages(); // gives empty 401/403/404 responses a ProblemDetails body

if (AllocationReview.Api.Auth.SessionOptions.RequiresHttps(app.Configuration, app.Environment))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// The production build of the Angular app is served from wwwroot (see scripts/build-prod.*).
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

// Unknown /api routes are a real 404; every other path goes to the Angular router.
app.MapFallback("/api/{**path}", () => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found"))
    .AllowAnonymous();
app.MapFallbackToFile("index.html").AllowAnonymous();

await app.RunAsync();

static async Task InitializeDatabaseAsync(WebApplication app)
{
    var options = app.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
    if (!options.MigrateOnStartup && !options.SeedOnStartup)
    {
        return;
    }

    await using var scope = app.Services.CreateAsyncScope();
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    if (options.MigrateOnStartup)
    {
        await initializer.MigrateAsync();
    }

    if (options.SeedOnStartup)
    {
        await initializer.SeedAsync();
    }
}

/// <summary>Exposed so the integration tests can host the app with WebApplicationFactory.</summary>
public partial class Program;
