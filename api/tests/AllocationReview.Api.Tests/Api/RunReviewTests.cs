using System.Net;
using AllocationReview.Api.Domain;
using AllocationReview.Api.Runs;
using AllocationReview.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AllocationReview.Api.Tests.Api;

/// <summary>The brief's "Expected review for the latest run" and its acceptance checks, end to end through the API and database.</summary>
public sealed class RunReviewTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task R102_review_matches_the_expected_results_from_the_brief()
    {
        var client = await factory.SignInAsAdminAsync();

        var review = await (await client.GetAsync("/api/runs/R-102")).ReadAsync<RunReview>();

        Assert.Equal("R-102", review.RunId);
        Assert.Equal(new DateOnly(2026, 9, 14), review.RunDate);
        Assert.Equal(5, review.TotalAllocatedUnits);
        Assert.Equal(3, review.GapCount);
        Assert.Equal(new GapStatusCounts(Filled: 1, PartiallyFilled: 1, Unfilled: 1), review.StatusCounts);
        Assert.Equal(GapFilter.All, review.Filter);

        Assert.Collection(
            review.Gaps,
            g => Assert.Equal(new GapReviewRow("G-01", "Store A", "Brand X", "Frames", 5, 3, 2, GapStatus.PartiallyFilled), g),
            g => Assert.Equal(new GapReviewRow("G-02", "Store B", "Brand X", "Frames", 2, 2, 0, GapStatus.Filled), g),
            g => Assert.Equal(new GapReviewRow("G-03", "Store C", "Brand Y", "Sunglasses", 4, 0, 4, GapStatus.Unfilled), g));
    }

    [Fact]
    public async Task The_exceptions_filter_shows_G01_and_G03_only_and_keeps_whole_run_totals()
    {
        var client = await factory.SignInAsAdminAsync();

        var review = await (await client.GetAsync("/api/runs/R-102?filter=exceptions")).ReadAsync<RunReview>();

        Assert.Equal(["G-01", "G-03"], review.Gaps.Select(g => g.GapId));
        Assert.Equal(GapFilter.Exceptions, review.Filter);
        // Totals and counts still describe the whole run, not just the filtered rows.
        Assert.Equal(5, review.TotalAllocatedUnits);
        Assert.Equal(3, review.GapCount);
        Assert.Equal(1, review.StatusCounts.Filled);
    }

    [Fact]
    public async Task G01_in_R101_is_a_separate_gap_from_G01_in_R102()
    {
        var client = await factory.SignInAsAdminAsync();

        var review = await (await client.GetAsync("/api/runs/R-101")).ReadAsync<RunReview>();

        var gap = Assert.Single(review.Gaps);
        Assert.Equal(new GapReviewRow("G-01", "Store D", "Brand Z", "Frames", 1, 1, 0, GapStatus.Filled), gap);
        Assert.Equal(1, review.TotalAllocatedUnits);
    }

    [Fact]
    public async Task An_unknown_run_returns_404_to_an_admin()
    {
        var client = await factory.SignInAsAdminAsync();

        var response = await client.GetAsync("/api/runs/R-999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.ReadAsync<ProblemDetails>();
        Assert.Equal("Run not found", problem.Title);
        Assert.True(problem.Extensions.ContainsKey("correlationId"));
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("7")]
    public async Task An_unknown_filter_returns_400(string filter)
    {
        var client = await factory.SignInAsAdminAsync();

        var response = await client.GetAsync($"/api/runs/R-102?filter={filter}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_correlation_id_sent_by_the_caller_is_echoed_back()
    {
        var client = await factory.SignInAsAdminAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/runs/R-102");
        request.Headers.Add("X-Correlation-ID", "test-correlation-42");

        var response = await client.SendAsync(request);

        Assert.Equal("test-correlation-42", Assert.Single(response.Headers.GetValues("X-Correlation-ID")));
    }

    [Fact]
    public async Task An_unsafe_correlation_id_is_replaced_with_a_new_one()
    {
        var client = await factory.SignInAsAdminAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/runs");
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", "<script>alert(1)</script>");

        var response = await client.SendAsync(request);

        var id = Assert.Single(response.Headers.GetValues("X-Correlation-ID"));
        Assert.Matches("^[0-9a-f]{32}$", id);
    }
}
