using AllocationReview.Api.Domain;

namespace AllocationReview.Api.Tests.Domain;

/// <summary>The multi-batch calculation rules from the brief's acceptance checks.</summary>
public sealed class GapCalculatorTests
{
    [Fact]
    public void Sums_every_batch_row_of_a_gap_and_counts_the_shortfall_once()
    {
        // R-102 / G-01: shortfall 5, batches B-401 (2) and B-402 (1).
        var outcome = GapCalculator.Evaluate(5m, [2, 1]);

        Assert.Equal(new GapOutcome(OriginalShortfall: 5, UnitsAllocated: 3, UnitsRemaining: 2, GapStatus.PartiallyFilled), outcome);
    }

    [Fact]
    public void A_gap_with_nothing_remaining_is_filled()
    {
        // R-102 / G-02: shortfall 2, batch B-501 (2).
        var outcome = GapCalculator.Evaluate(2m, [2]);

        Assert.Equal(0, outcome.UnitsRemaining);
        Assert.Equal(GapStatus.Filled, outcome.Status);
    }

    [Fact]
    public void A_gap_with_no_batches_is_unfilled()
    {
        // R-102 / G-03: shortfall 4, no batch.
        var outcome = GapCalculator.Evaluate(4m, []);

        Assert.Equal(new GapOutcome(4, 0, 4, GapStatus.Unfilled), outcome);
    }

    [Theory]
    [InlineData(4.01, 5)]
    [InlineData(4.5, 5)]
    [InlineData(4.99, 5)]
    [InlineData(5.0, 5)]
    [InlineData(0.2, 1)]
    public void The_original_shortfall_is_rounded_up_to_a_whole_unit(decimal shortfall, int expected)
    {
        Assert.Equal(expected, GapCalculator.Evaluate(shortfall, []).OriginalShortfall);
    }

    [Fact]
    public void Rounding_happens_before_remaining_units_are_worked_out()
    {
        // 4.2 rounds up to 5; 4 allocated leaves 1 (not 0.2), so the gap is only partially filled.
        var outcome = GapCalculator.Evaluate(4.2m, [4]);

        Assert.Equal(1, outcome.UnitsRemaining);
        Assert.Equal(GapStatus.PartiallyFilled, outcome.Status);
    }

    [Fact]
    public void Remaining_units_never_go_below_zero_when_a_gap_is_over_allocated()
    {
        var outcome = GapCalculator.Evaluate(2m, [2, 3]);

        Assert.Equal(5, outcome.UnitsAllocated);
        Assert.Equal(0, outcome.UnitsRemaining);
        Assert.Equal(GapStatus.Filled, outcome.Status);
    }

    [Fact]
    public void A_zero_shortfall_with_no_batches_is_filled_because_nothing_remains()
    {
        Assert.Equal(GapStatus.Filled, GapCalculator.Evaluate(0m, []).Status);
    }

    [Fact]
    public void Zero_quantity_batch_rows_do_not_count_as_an_allocation()
    {
        Assert.Equal(GapStatus.Unfilled, GapCalculator.Evaluate(3m, [0, 0]).Status);
    }

    [Theory]
    [InlineData(GapStatus.PartiallyFilled, true)]
    [InlineData(GapStatus.Unfilled, true)]
    [InlineData(GapStatus.Filled, false)]
    public void Exceptions_are_partially_filled_and_unfilled_gaps(GapStatus status, bool isException)
    {
        Assert.Equal(isException, GapCalculator.IsException(status));
    }

    [Fact]
    public void Negative_inputs_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GapCalculator.Evaluate(-1m, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => GapCalculator.Evaluate(1m, -1));
    }
}
