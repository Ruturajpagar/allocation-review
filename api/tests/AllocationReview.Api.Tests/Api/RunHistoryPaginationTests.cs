using System.Net;
using AllocationReview.Api.Data;
using AllocationReview.Api.Domain;
using AllocationReview.Api.Runs;
using AllocationReview.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace AllocationReview.Api.Tests.Api;

/// <summary>
/// History pagination: newest first, stable order (ties broken by run ID), bounded page size,
/// and 400 for invalid paging input. Uses its own database with extra runs, several sharing a date.
/// </summary>
public sealed class RunHistoryPaginationTests : IClassFixture<RunHistoryPaginationTests.PaginationFactory>
{
    private readonly PaginationFactory _factory;

    public RunHistoryPaginationTests(PaginationFactory factory) => _factory = factory;

    [Fact]
    public async Task The_seeded_runs_come_newest_first_with_their_total_allocated_units()
    {
        var client = await _factory.SignInAsAdminAsync();

        var page = await (await client.GetAsync("/api/runs?page=1&pageSize=100")).ReadAsync<PagedResult<RunSummary>>();

        var r102 = page.Items.Single(r => r.RunId == "R-102");
        var r101 = page.Items.Single(r => r.RunId == "R-101");
        Assert.Equal(new RunSummary("R-102", new DateOnly(2026, 9, 14), 5), r102);
        Assert.Equal(new RunSummary("R-101", new DateOnly(2026, 9, 13), 1), r101);
        Assert.True(page.Items.ToList().IndexOf(r102) < page.Items.ToList().IndexOf(r101));
    }

    [Fact]
    public async Task Walking_every_page_returns_each_run_exactly_once_in_a_stable_order()
    {
        var client = await _factory.SignInAsAdminAsync();
        var seen = new List<RunSummary>();

        PagedResult<RunSummary> page;
        var number = 1;
        do
        {
            page = await (await client.GetAsync($"/api/runs?page={number}&pageSize=4")).ReadAsync<PagedResult<RunSummary>>();
            Assert.Equal(PaginationFactory.TotalRuns, page.TotalCount);
            Assert.True(page.Items.Count <= 4);
            seen.AddRange(page.Items);
            number++;
        }
        while (number <= page.TotalPages);

        Assert.Equal(PaginationFactory.TotalRuns, seen.Count);
        Assert.Equal(seen.Count, seen.Select(r => r.RunId).Distinct().Count());

        var expected = seen
            .OrderByDescending(r => r.RunDate)
            .ThenByDescending(r => r.RunId, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(expected, seen);
    }

    [Fact]
    public async Task The_same_page_returns_the_same_runs_every_time()
    {
        var client = await _factory.SignInAsAdminAsync();

        var first = await (await client.GetAsync("/api/runs?page=2&pageSize=3")).ReadAsync<PagedResult<RunSummary>>();
        var second = await (await client.GetAsync("/api/runs?page=2&pageSize=3")).ReadAsync<PagedResult<RunSummary>>();

        Assert.Equal(first.Items, second.Items);
    }

    [Fact]
    public async Task A_page_after_the_last_one_is_empty_not_an_error()
    {
        var client = await _factory.SignInAsAdminAsync();

        var page = await (await client.GetAsync("/api/runs?page=50&pageSize=10")).ReadAsync<PagedResult<RunSummary>>();

        Assert.Empty(page.Items);
        Assert.Equal(PaginationFactory.TotalRuns, page.TotalCount);
    }

    [Fact]
    public async Task Defaults_apply_when_no_paging_is_given()
    {
        var client = await _factory.SignInAsAdminAsync();

        var page = await (await client.GetAsync("/api/runs")).ReadAsync<PagedResult<RunSummary>>();

        Assert.Equal(1, page.Page);
        Assert.Equal(RunHistoryQuery.DefaultPageSize, page.PageSize);
        Assert.Equal(RunHistoryQuery.DefaultPageSize, page.Items.Count);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("pageSize=abc")]
    [InlineData("page=1.5")]
    [InlineData("page=99999999999")]
    public async Task Invalid_pagination_input_returns_400(string query)
    {
        var client = await _factory.SignInAsAdminAsync();

        var response = await client.GetAsync($"/api/runs?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadAsync<ValidationProblemDetails>();
        Assert.NotEmpty(problem.Errors);
    }

    public sealed class PaginationFactory : IAsyncLifetime
    {
        public const int ExtraRuns = 25;
        public const int TotalRuns = ExtraRuns + 2;

        private readonly ApiFactory _inner = new();

        public Task<HttpClient> SignInAsAdminAsync() => _inner.SignInAsAdminAsync();

        public async Task InitializeAsync()
        {
            // Start the app (which seeds R-101 and R-102), then add more runs.
            _ = _inner.Server;
            await using var scope = _inner.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            for (var i = 1; i <= ExtraRuns; i++)
            {
                // Five runs per date, so the run ID must break ties for a stable order.
                var date = new DateOnly(2026, 8, 1).AddDays(i / 5);
                db.AllocationRuns.Add(new AllocationRun
                {
                    Id = $"P-{i:D3}",
                    RunDate = date,
                    Gaps =
                    [
                        new AllocationGap
                        {
                            RunId = $"P-{i:D3}", GapCode = "G-01", Store = "Store P", Brand = "Brand P", ProductType = "Frames",
                            OriginalShortfall = 3, Allocations = [new BatchAllocation { BatchId = "B-1", Quantity = i % 4 }],
                        },
                    ],
                });
            }

            await db.SaveChangesAsync();
        }

        public async Task DisposeAsync() => await _inner.DisposeAsync();
    }
}
