using GRID.Data;
using GRID.Models;
using Microsoft.EntityFrameworkCore;

namespace GRID.Tests.Helpers;

/// <summary>
/// SQLite-compatible subclass of ApplicationDbContext for service-level tests.
/// Overrides the RowVersion configuration so SQLite can round-trip Invite rows
/// (SQLite has no native row-version support).
/// </summary>
public class TestApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : ApplicationDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Undo the IsRowVersion() / HasColumnType("bytea") / ValueGeneratedOnAddOrUpdate()
        // set by the base class so that SQLite uses the value set in the constructor.
        builder.Entity<Invite>()
            .Property(i => i.RowVersion)
            .IsConcurrencyToken()
            .ValueGeneratedNever();
    }
}
