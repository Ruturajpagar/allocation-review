using System.Net;
using System.Net.Http.Json;
using AllocationReview.Api.Auth;
using AllocationReview.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AllocationReview.Api.Tests.Api;

/// <summary>"Unauthenticated review API requests receive 401 and authenticated Viewer requests receive 403."</summary>
public sealed class AuthorizationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    public static TheoryData<string> ReviewEndpoints => new() { "/api/runs", "/api/runs?page=1&pageSize=5", "/api/runs/R-102", "/api/runs/R-999" };

    [Theory]
    [MemberData(nameof(ReviewEndpoints))]
    public async Task Review_endpoints_return_401_without_a_session(string url)
    {
        var client = factory.CreateBrowserClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.ReadAsync<ProblemDetails>();
        Assert.Equal(401, problem.Status);
        Assert.True(problem.Extensions.ContainsKey("correlationId"));
    }

    [Theory]
    [MemberData(nameof(ReviewEndpoints))]
    public async Task Review_endpoints_return_403_to_a_viewer(string url)
    {
        var client = await factory.SignInAsViewerAsync();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_admin_can_use_both_review_endpoints()
    {
        var client = await factory.SignInAsAdminAsync();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/runs")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/runs/R-102")).StatusCode);
    }

    [Fact]
    public async Task Sign_in_returns_the_user_and_whether_they_can_review_runs()
    {
        var client = factory.CreateBrowserClient();

        var admin = await (await client.PostAsJsonAsync("/api/auth/login", new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword }))
            .ReadAsync<CurrentUserResponse>();
        Assert.Equal(ApiFactory.AdminEmail, admin.Email);
        Assert.Equal([Roles.Admin], admin.Roles);
        Assert.True(admin.CanReviewRuns);

        var viewerClient = await factory.SignInAsViewerAsync();
        var viewer = await (await viewerClient.GetAsync("/api/auth/me")).ReadAsync<CurrentUserResponse>();
        Assert.Equal([Roles.Viewer], viewer.Roles);
        Assert.False(viewer.CanReviewRuns);
    }

    [Fact]
    public async Task The_session_cookie_is_http_only_and_same_site_strict()
    {
        var client = factory.CreateBrowserClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword });

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(AuthSetup.CookieName, StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_wrong_password_is_rejected_with_401_and_no_session()
    {
        var client = factory.CreateBrowserClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = ApiFactory.AdminEmail, password = "not-the-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/runs")).StatusCode);
    }

    [Fact]
    public async Task An_unknown_account_gets_the_same_answer_as_a_wrong_password()
    {
        var client = factory.CreateBrowserClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "nobody@test.local", password = "whatever-123" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Invalid email or password", (await response.ReadAsync<ProblemDetails>()).Title);
    }

    [Fact]
    public async Task A_malformed_sign_in_request_returns_400()
    {
        var client = factory.CreateBrowserClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "not-an-email", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Signing_out_ends_the_session()
    {
        var client = await factory.SignInAsAdminAsync();

        var logout = await client.PostAsync("/api/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/runs")).StatusCode);
    }
}
