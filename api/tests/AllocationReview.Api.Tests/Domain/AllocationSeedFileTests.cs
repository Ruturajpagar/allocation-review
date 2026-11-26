using AllocationReview.Api.Data.Seed;

namespace AllocationReview.Api.Tests.Domain;

/// <summary>The seed file is grouped by run and gap ID, so a shortfall shared by several batch rows is stored once.</summary>
public sealed class AllocationSeedFileTests
{
    [Fact]
    public async Task The_brief_rows_become_two_runs_four_gaps_and_four_batch_allocations()
    {
        var runs = await AllocationSeedFile.LoadAsync(AllocationSeedFile.DefaultPath);

        var r102 = runs.Single(r => r.Id == "R-102");
        Assert.Equal(new DateOnly(2026, 9, 14), r102.RunDate);
        Assert.Equal(["G-01", "G-02", "G-03"], r102.Gaps.Select(g => g.GapCode));

        var g01 = r102.Gaps.Single(g => g.GapCode == "G-01");
        Assert.Equal(5m, g01.OriginalShortfall);
        Assert.Equal(["B-401", "B-402"], g01.Allocations.Select(a => a.BatchId));

        // "None 0" in the brief means no batch row at all.
        Assert.Empty(r102.Gaps.Single(g => g.GapCode == "G-03").Allocations);

        var r101 = runs.Single(r => r.Id == "R-101");
        Assert.Equal("Store D", Assert.Single(r101.Gaps).Store);
    }

    [Fact]
    public void Rows_that_disagree_about_a_gap_are_rejected()
    {
        var file = new AllocationSeedFile.SeedFile(
            [new("R-1", "2026-01-01")],
            [
                new("R-1", "G-01", "Store A", "Brand X", "Frames", 5, "B-1", 1),
                new("R-1", "G-01", "Store A", "Brand X", "Frames", 6, "B-2", 1),
            ]);

        Assert.Throws<InvalidOperationException>(() => AllocationSeedFile.Build(file));
    }
}
