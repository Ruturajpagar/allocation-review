using AllocationReview.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AllocationReview.Api.Runs;

/// <summary>
/// The two review endpoints. Both are Admin only: no session gives 401, a Viewer gets 403.
/// </summary>
[ApiController]
[Route("api/runs")]
[Authorize(Policy = Policies.CanReviewRuns)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class RunsController(IRunReviewService runs, ILogger<RunsController> logger) : ControllerBase
{
    /// <summary>Saved runs, newest first, paginated on the server.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<RunSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<RunSummary>>> GetHistory([FromQuery] RunHistoryQuery query, CancellationToken cancellationToken)
    {
        var result = await runs.GetHistoryAsync(query.Page, query.PageSize, cancellationToken);
        logger.LogInformation(
            "Run history page {Page} (size {PageSize}) returned {ItemCount} of {TotalCount} runs",
            query.Page, query.PageSize, result.Items.Count, result.TotalCount);
        return result;
    }

    /// <summary>Review of one run: totals, counts by status and gap rows (optionally exceptions only).</summary>
    [HttpGet("{runId}")]
    [ProducesResponseType<RunReview>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RunReview>> GetReview(string runId, [FromQuery] GapFilter filter = GapFilter.All, CancellationToken cancellationToken = default)
    {
        if (runId.Length > 32)
        {
            ModelState.AddModelError(nameof(runId), "runId must be at most 32 characters.");
        }

        // Enum binding also accepts numbers such as "7"; only the named filters are valid.
        if (!Enum.IsDefined(filter))
        {
            ModelState.AddModelError(nameof(filter), "filter must be 'all' or 'exceptions'.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // Every log line for this request carries the run ID.
        using var scope = logger.BeginScope("RunId:{RunId}", runId);

        var review = await runs.GetReviewAsync(runId, filter, cancellationToken);
        if (review is null)
        {
            logger.LogInformation("Run review requested for an unknown run");
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Run not found",
                detail: $"No saved allocation run has the ID '{runId}'.");
        }

        logger.LogInformation(
            "Run review returned {VisibleGaps} of {GapCount} gaps (filter {Filter})",
            review.Gaps.Count, review.GapCount, filter);
        return review;
    }
}
