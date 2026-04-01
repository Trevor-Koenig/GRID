using GRID.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GRID.Tests.Helpers;

/// <summary>
/// SQLite-compatible test database context. Mirrors ApplicationDbContext but
/// omits PostgreSQL-specific column types and check constraint syntax.
/// </summary>
public class TestDbContext(DbContextOptions<TestDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Invite> Invites => Set<Invite>();
    public DbSet<InviteUsage> InviteUsages => Set<InviteUsage>();
    public DbSet<ContactRequest> ContactRequests => Set<ContactRequest>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<LoginHistory> LoginHistories => Set<LoginHistory>();
    public DbSet<ServiceLink> ServiceLinks => Set<ServiceLink>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<DocArticle> DocArticles => Set<DocArticle>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // [Timestamp] on Invite.RowVersion sets ValueGeneratedOnAddOrUpdate,
        // which causes SQLite to insert NULL (no native rowversion support).
        // Override to ValueGeneratedNever so EF uses the constructor value.
        builder.Entity<Invite>()
            .Property(i => i.RowVersion)
            .IsConcurrencyToken()
            .ValueGeneratedNever();

        builder.Entity<RolePermission>()
            .HasIndex(rp => new { rp.RoleName, rp.Permission })
            .IsUnique();

        builder.Entity<DocArticle>()
            .HasIndex(d => new { d.Category, d.Slug })
            .IsUnique();
    }
}
