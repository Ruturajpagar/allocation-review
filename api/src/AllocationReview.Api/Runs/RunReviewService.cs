using AllocationReview.Api.Data;
using AllocationReview.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AllocationReview.Api.Runs;

public interface IRunReviewService
{
    Task<PagedResult<RunSummary>> GetHistoryAsync(int page, int pageSize, CancellationToken cancellationToken);

    /// <returns>The review, or null when the run does not exist.</returns>
    Task<RunReview?> GetReviewAsync(string runId, GapFilter filter, CancellationToken cancellationToken);
}

/// <summary>
/// Read-only queries for saved runs. Aggregation (sums per run and per gap) happens in the
/// database; the status rules are applied in <see cref="GapCalculator"/> so they live in one place.
/// </summary>
public sealed class RunReviewService(AppDbContext db) : IRunReviewService
{
    public async Task<PagedResult<RunSummary>> GetHistoryAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var runs = db.AllocationRuns.AsNoTracking();

        var totalCount = await runs.CountAsync(cancellationToken);

        // Newest first; the run ID breaks ties so pages never overlap or skip rows.
        var items = await runs
            .OrderByDescending(r => r.RunDate)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RunSummary(
                r.Id,
                r.RunDate,
                r.Gaps.SelectMany(g => g.Allocations).Sum(a => (int?)a.Quantity) ?? 0))
            .ToListAsync(cancellationToken);

        return new PagedResult<RunSummary>(items, page, pageSize, totalCount);
    }

    public async Task<RunReview?> GetReviewAsync(string runId, GapFilter filter, CancellationToken cancellationToken)
    {
        var run = await db.AllocationRuns
            .AsNoTracking()
            .Where(r => r.Id == runId)
            .Select(r => new { r.Id, r.RunDate })
            .SingleOrDefaultAsync(cancellationToken);

        if (run is null)
        {
            return null;
        }

        // One row per gap, with its batch rows already summed by the database.
        var gaps = await db.AllocationGaps
            .AsNoTracking()
            .Where(g => g.RunId == runId)
            .OrderBy(g => g.GapCode)
            .Select(g => new
            {
                g.GapCode,
                g.Store,
                g.Brand,
                g.ProductType,
                g.OriginalShortfall,
                UnitsAllocated = g.Allocations.Sum(a => (int?)a.Quantity) ?? 0,
            })
            .ToListAsync(cancellationToken);

        var reviewed = gaps
            .Select(g =>
            {
                var outcome = GapCalculator.Evaluate(g.OriginalShortfall, g.UnitsAllocated);
                return new GapReviewRow(
                    g.GapCode, g.Store, g.Brand, g.ProductType,
                    outcome.OriginalShortfall, outcome.UnitsAllocated, outcome.UnitsRemaining, outcome.Status);
            })
            .ToList();

        var counts = new GapStatusCounts(
            reviewed.Count(g => g.Status == GapStatus.Filled),
            reviewed.Count(g => g.Status == GapStatus.PartiallyFilled),
            reviewed.Count(g => g.Status == GapStatus.Unfilled));

        var visible = filter == GapFilter.Exceptions
            ? reviewed.Where(g => GapCalculator.IsException(g.Status)).ToList()
            : reviewed;

        return new RunReview(
            run.Id,
            run.RunDate,
            reviewed.Sum(g => g.UnitsAllocated),
            reviewed.Count,
            counts,
            filter,
            visible);
    }
}
