using System.Text.Json;
using AllocationReview.Api.Domain;

namespace AllocationReview.Api.Data.Seed;

/// <summary>
/// Reads the synthetic rows from the brief (one row per batch allocation, the same shape as the
/// "Example saved rows" table) and turns them into runs, gaps and batch allocations.
/// </summary>
public static class AllocationSeedFile
{
    public const string DefaultFileName = "allocation-runs.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "Data", "Seed", DefaultFileName);

    public static async Task<IReadOnlyList<AllocationRun>> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var file = await JsonSerializer.DeserializeAsync<SeedFile>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Seed file '{path}' is empty.");

        return Build(file);
    }

    internal static IReadOnlyList<AllocationRun> Build(SeedFile file)
    {
        var runs = file.Runs.ToDictionary(
            r => r.RunId,
            r => new AllocationRun { Id = r.RunId, RunDate = DateOnly.Parse(r.RunDate, System.Globalization.CultureInfo.InvariantCulture) });

        // Group rows by run and gap ID: the shortfall belongs to the gap and is counted once.
        foreach (var gapRows in file.Rows.GroupBy(r => (r.RunId, r.GapId)))
        {
            if (!runs.TryGetValue(gapRows.Key.RunId, out var run))
            {
                throw new InvalidOperationException($"Seed row refers to unknown run '{gapRows.Key.RunId}'.");
            }

            var first = gapRows.First();
            if (gapRows.Any(r => r.Shortfall != first.Shortfall || r.Store != first.Store || r.Brand != first.Brand || r.ProductType != first.ProductType))
            {
                throw new InvalidOperationException($"Seed rows for {gapRows.Key.RunId}/{gapRows.Key.GapId} disagree about the gap details.");
            }

            var gap = new AllocationGap
            {
                RunId = run.Id,
                GapCode = first.GapId,
                Store = first.Store,
                Brand = first.Brand,
                ProductType = first.ProductType,
                OriginalShortfall = first.Shortfall,
            };

            foreach (var row in gapRows.Where(r => !string.IsNullOrWhiteSpace(r.BatchId)))
            {
                gap.Allocations.Add(new BatchAllocation { BatchId = row.BatchId!, Quantity = row.Units });
            }

            run.Gaps.Add(gap);
        }

        return [.. runs.Values];
    }

    internal sealed record SeedFile(List<SeedRun> Runs, List<SeedRow> Rows);

    internal sealed record SeedRun(string RunId, string RunDate);

    internal sealed record SeedRow(
        string RunId,
        string GapId,
        string Store,
        string Brand,
        string ProductType,
        decimal Shortfall,
        string? BatchId,
        int Units);
}
