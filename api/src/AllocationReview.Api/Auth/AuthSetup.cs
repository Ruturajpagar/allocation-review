using AllocationReview.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace AllocationReview.Api.Auth;

public sealed class SessionOptions
{
    public const string SectionName = "Session";

    /// <summary>How long an idle session lasts. Each request inside the window extends it.</summary>
    public int IdleTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Send the session cookie over HTTPS only and redirect HTTP to HTTPS. On by default in Production;
    /// switch off only to try a production build on plain http://localhost.
    /// </summary>
    public bool? RequireHttps { get; set; }

    public static bool RequiresHttps(IConfiguration configuration, IHostEnvironment environment) =>
        configuration.GetSection(SectionName).Get<SessionOptions>()?.RequireHttps ?? environment.IsProduction();
}

public static class AuthSetup
{
    public const string CookieName = "allocation_review_session";

    public static IServiceCollection AddAppAuthentication(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var session = configuration.GetSection(SessionOptions.SectionName).Get<SessionOptions>() ?? new SessionOptions();

        services
            .AddIdentityCore<IdentityUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 10;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager();

        services
            .AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = CookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            // HTTPS-only outside local development and tests.
            options.Cookie.SecurePolicy = SessionOptions.RequiresHttps(configuration, environment)
                ? CookieSecurePolicy.Always
                : CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(session.IdleTimeoutMinutes);
            options.SlidingExpiration = true;

            // An API answers with status codes; it never redirects to an HTML login page.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.CanReviewRuns, policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Admin))
            // Anything not marked otherwise needs a signed-in user.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }
}
