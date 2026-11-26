namespace AllocationReview.Api.Domain;

public enum GapStatus
{
    Filled,
    PartiallyFilled,
    Unfilled,
}

/// <summary>The reviewed numbers for one gap.</summary>
public readonly record struct GapOutcome(int OriginalShortfall, int UnitsAllocated, int UnitsRemaining, GapStatus Status);

/// <summary>
/// The business rules for reviewing a gap, kept in one pure class so they are easy to test
/// and the same rules are used everywhere.
/// </summary>
public static class GapCalculator
{
    /// <summary>
    /// Reviews one gap.
    /// </summary>
    /// <param name="originalShortfall">The shortfall as saved. It is counted once and rounded up to a whole unit.</param>
    /// <param name="batchQuantities">The quantities of every batch row allocated to the gap (may be empty).</param>
    public static GapOutcome Evaluate(decimal originalShortfall, IEnumerable<int> batchQuantities)
    {
        ArgumentNullException.ThrowIfNull(batchQuantities);
        return Evaluate(originalShortfall, batchQuantities.Sum());
    }

    /// <summary>
    /// Reviews one gap when the allocated units were already summed (for example by the database).
    /// </summary>
    public static GapOutcome Evaluate(decimal originalShortfall, int unitsAllocated)
    {
        if (originalShortfall < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(originalShortfall), "A shortfall cannot be negative.");
        }

        if (unitsAllocated < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitsAllocated), "Allocated units cannot be negative.");
        }

        var shortfall = (int)decimal.Ceiling(originalShortfall);
        var remaining = Math.Max(0, shortfall - unitsAllocated);

        var status = remaining == 0
            ? GapStatus.Filled
            : unitsAllocated == 0
                ? GapStatus.Unfilled
                : GapStatus.PartiallyFilled;

        return new GapOutcome(shortfall, unitsAllocated, remaining, status);
    }

    /// <summary>Exceptions are the gaps a merchandiser still has to act on.</summary>
    public static bool IsException(GapStatus status) =>
        status is GapStatus.PartiallyFilled or GapStatus.Unfilled;
}
