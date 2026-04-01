using FluentAssertions;
using GRID.Models;
using GRID.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace GRID.Tests.Models;

public class InviteUsageTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private TestDbContext Db => _factory.Context;

    public void Dispose() => _factory.Dispose();

    private Invite SeedInvite()
    {
        var invite = new Invite { Code = "SEEDCODE12345678" };
        Db.Invites.Add(invite);
        Db.SaveChanges();
        return invite;
    }

    // ── Default values ────────────────────────────────────────────────────────

    [Fact]
    public void UsedAt_DefaultsToApproximatelyUtcNow()
    {
        var before = DateTime.UtcNow;
        var usage = new InviteUsage();
        var after = DateTime.UtcNow;

        usage.UsedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Id_DefaultsToZero()
    {
        new InviteUsage().Id.Should().Be(0);
    }

    // ── Database: CRUD ────────────────────────────────────────────────────────

    [Fact]
    public void Db_CanSaveAndRetrieve()
    {
        var invite = SeedInvite();
        var usage = new InviteUsage { InviteId = invite.Id, UserId = "user-abc" };

        Db.InviteUsages.Add(usage);
        Db.SaveChanges();

        var retrieved = Db.InviteUsages.Find(usage.Id);
        retrieved.Should().NotBeNull();
        retrieved!.UserId.Should().Be("user-abc");
        retrieved.InviteId.Should().Be(invite.Id);
        retrieved.UsedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Db_NavigationProperty_LoadsParentInvite()
    {
        var invite = SeedInvite();
        Db.InviteUsages.Add(new InviteUsage { InviteId = invite.Id, UserId = "user-nav" });
        Db.SaveChanges();

        var usage = Db.InviteUsages
            .Include(u => u.Invite)
            .First(u => u.UserId == "user-nav");

        usage.Invite.Should().NotBeNull();
        usage.Invite.Code.Should().Be("SEEDCODE12345678");
    }

    // ── Database: multiple usages per invite ──────────────────────────────────

    [Fact]
    public void Db_MultipleUsages_CanBeSavedForSameInvite()
    {
        var invite = SeedInvite();
        invite.IsSingleUse = false;
        invite.MaxUses = 10;
        Db.SaveChanges();

        Db.InviteUsages.Add(new InviteUsage { InviteId = invite.Id, UserId = "user-1" });
        Db.InviteUsages.Add(new InviteUsage { InviteId = invite.Id, UserId = "user-2" });
        Db.InviteUsages.Add(new InviteUsage { InviteId = invite.Id, UserId = "user-3" });
        Db.SaveChanges();

        Db.InviteUsages.Count(u => u.InviteId == invite.Id).Should().Be(3);
    }

    // ── Database: cascade delete ──────────────────────────────────────────────

    [Fact]
    public void Db_DeletingInvite_DeletesUsages()
    {
        var invite = SeedInvite();
        Db.InviteUsages.Add(new InviteUsage { InviteId = invite.Id, UserId = "orphan-user" });
        Db.SaveChanges();

        Db.Invites.Remove(invite);
        Db.SaveChanges();

        Db.InviteUsages.Any(u => u.UserId == "orphan-user").Should().BeFalse();
    }
}
