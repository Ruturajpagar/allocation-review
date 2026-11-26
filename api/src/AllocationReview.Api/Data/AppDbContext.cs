using AllocationReview.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AllocationReview.Api.Data;

/// <summary>
/// One database for allocation data and ASP.NET Core Identity (users and roles).
/// The model sticks to provider-neutral configuration so the same model runs on
/// PostgreSQL (the app) and SQLite (the automated tests).
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<AllocationRun> AllocationRuns => Set<AllocationRun>();

    public DbSet<AllocationGap> AllocationGaps => Set<AllocationGap>();

    public DbSet<BatchAllocation> BatchAllocations => Set<BatchAllocation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AllocationRun>(run =>
        {
            run.ToTable("allocation_runs");
            run.HasKey(r => r.Id);
            run.Property(r => r.Id).HasColumnName("id").HasMaxLength(32);
            run.Property(r => r.RunDate).HasColumnName("run_date");

            // Supports the history page: newest first, with the ID as a stable tie-breaker.
            run.HasIndex(r => new { r.RunDate, r.Id })
                .HasDatabaseName("ix_allocation_runs_run_date_id")
                .IsDescending(true, true);

            run.HasMany(r => r.Gaps)
                .WithOne(g => g.Run)
                .HasForeignKey(g => g.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AllocationGap>(gap =>
        {
            gap.ToTable("allocation_gaps", t =>
                t.HasCheckConstraint("ck_allocation_gaps_shortfall_non_negative", "original_shortfall >= 0"));
            gap.HasKey(g => g.Id);
            gap.Property(g => g.Id).HasColumnName("id");
            gap.Property(g => g.RunId).HasColumnName("run_id").HasMaxLength(32);
            gap.Property(g => g.GapCode).HasColumnName("gap_code").HasMaxLength(32);
            gap.Property(g => g.Store).HasColumnName("store").HasMaxLength(100);
            gap.Property(g => g.Brand).HasColumnName("brand").HasMaxLength(100);
            gap.Property(g => g.ProductType).HasColumnName("product_type").HasMaxLength(100);
            gap.Property(g => g.OriginalShortfall).HasColumnName("original_shortfall").HasPrecision(12, 2);

            // A gap ID is unique within its run, not globally.
            gap.HasIndex(g => new { g.RunId, g.GapCode })
                .IsUnique()
                .HasDatabaseName("ux_allocation_gaps_run_id_gap_code");

            gap.HasMany(g => g.Allocations)
                .WithOne(a => a.Gap)
                .HasForeignKey(a => a.GapId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BatchAllocation>(allocation =>
        {
            allocation.ToTable("batch_allocations", t =>
                t.HasCheckConstraint("ck_batch_allocations_quantity_non_negative", "quantity >= 0"));
            allocation.HasKey(a => a.Id);
            allocation.Property(a => a.Id).HasColumnName("id");
            allocation.Property(a => a.GapId).HasColumnName("gap_id");
            allocation.Property(a => a.BatchId).HasColumnName("batch_id").HasMaxLength(32);
            allocation.Property(a => a.Quantity).HasColumnName("quantity");

            // The same batch is not allocated twice to the same gap.
            allocation.HasIndex(a => new { a.GapId, a.BatchId })
                .IsUnique()
                .HasDatabaseName("ux_batch_allocations_gap_id_batch_id");
        });
    }
}
