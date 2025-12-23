using GRID.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GRID.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
    {
        public DbSet<Invite> Invites => Set<Invite>();
        public DbSet<InviteUsage> InviteUsages => Set<InviteUsage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // Required for some reason?

            // invite-related constraints are here
            modelBuilder.Entity<Invite>()
                .Property(i => i.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<Invite>(entity =>
            {
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint(
                        "CK_Invites_Uses",
                        "\"MaxUses\" IS NULL OR \"CurrentUses\" <= \"MaxUses\""
                    );
                });
            });

            modelBuilder.Entity<Invite>(builder =>
            {
                builder.Property(e => e.RowVersion)
                    .IsRowVersion()
                    .HasColumnType("bytea")
                    .ValueGeneratedOnAddOrUpdate();
            });
        }
    }
}
