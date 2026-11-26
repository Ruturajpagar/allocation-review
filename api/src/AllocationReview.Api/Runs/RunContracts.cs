using System.ComponentModel.DataAnnotations;
using AllocationReview.Api.Domain;

namespace AllocationReview.Api.Runs;

/// <summary>Query string for the run history. Out-of-range values are rejected with 400.</summary>
public sealed class RunHistoryQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    [Range(1, 100_000, ErrorMessage = "page must be between 1 and 100000.")]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize, ErrorMessage = "pageSize must be between 1 and 100.")]
    public int PageSize { get; init; } = DefaultPageSize;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record RunSummary(string RunId, DateOnly RunDate, int TotalAllocatedUnits);

public enum GapFilter
{
    /// <summary>Every gap in the run.</summary>
    All,

    /// <summary>Only partially filled and unfilled gaps.</summary>
    Exceptions,
}

public sealed record GapStatusCounts(int Filled, int PartiallyFilled, int Unfilled);

public sealed record GapReviewRow(
    string GapId,
    string Store,
    string Brand,
    string ProductType,
    int OriginalShortfall,
    int UnitsAllocated,
    int UnitsRemaining,
    GapStatus Status);

/// <summary>
/// The review of one run. Totals and counts always describe the whole run;
/// <see cref="Gaps"/> respects the requested filter.
/// </summary>
public sealed record RunReview(
    string RunId,
    DateOnly RunDate,
    int TotalAllocatedUnits,
    int GapCount,
    GapStatusCounts StatusCounts,
    GapFilter Filter,
    IReadOnlyList<GapReviewRow> Gaps);
