namespace AllocationReview.Api.Domain;

/// <summary>
/// A saved allocation run: one proposed dispatch from the central warehouse to stores.
/// The run ID is the business identifier used by merchandisers (for example "R-102").
/// </summary>
public class AllocationRun
{
    public required string Id { get; set; }

    public DateOnly RunDate { get; set; }

    public List<AllocationGap> Gaps { get; set; } = [];
}

/// <summary>
/// A replenishment gap inside one run: a store / brand / product type that was short of stock.
/// The gap code (for example "G-01") is unique within its run only, so "G-01" in R-101 and
/// "G-01" in R-102 are different gaps.
/// </summary>
public class AllocationGap
{
    public int Id { get; set; }

    public required string RunId { get; set; }

    public AllocationRun? Run { get; set; }

    public required string GapCode { get; set; }

    public required string Store { get; set; }

    public required string Brand { get; set; }

    public required string ProductType { get; set; }

    /// <summary>Shortfall as recorded by the run. It can be fractional; reviews round it up.</summary>
    public decimal OriginalShortfall { get; set; }

    /// <summary>Zero or more warehouse batches allocated to this gap.</summary>
    public List<BatchAllocation> Allocations { get; set; } = [];
}

/// <summary>One warehouse batch row allocated to a gap.</summary>
public class BatchAllocation
{
    public int Id { get; set; }

    public int GapId { get; set; }

    public AllocationGap? Gap { get; set; }

    public required string BatchId { get; set; }

    public int Quantity { get; set; }
}
