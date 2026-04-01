using FluentAssertions;
using GRID.Models;
using GRID.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace GRID.Tests.Models;

public class InviteTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private TestDbContext Db => _factory.Context;

    public void Dispose() => _factory.Dispose();

    private static Invite ValidInvite() => new()
    {
        Code = "TESTCODE12345678",
    };

    // ── Constructor / RowVersion ──────────────────────────────────────────────

    [Fact]
    public void Constructor_InitializesRowVersion()
    {
        new Invite().RowVersion.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_RowVersion_IsEightBytes()
    {
        new Invite().RowVersion.Should().HaveCount(8);
    }

    [Fact]
    public void GenerateRandomRowVersion_DefaultSize_IsEightBytes()
    {
        Invite.GenerateRandomRowVersion().Should().HaveCount(8);
    }

    [Fact]
    public void GenerateRandomRowVersion_CustomSize_ReturnsCorrectLength()
    {
        Invite.GenerateRandomRowVersion(16).Should().HaveCount(16);
        Invite.GenerateRandomRowVersion(4).Should().HaveCount(4);
    }

    [Fact]
    public void GenerateRandomRowVersion_TwoCallsProduceDifferentValues()
    {
        var a = Invite.GenerateRandomRowVersion();
        var b = Invite.GenerateRandomRowVersion();

        // Statistically impossible to collide on 8 random bytes
        a.Should().NotBeEquivalentTo(b);
    }

    // ── Default values ────────────────────────────────────────────────────────

    [Fact]
    public void Defaults_IsSingleUse_IsTrue()
    {
        new Invite().IsSingleUse.Should().BeTrue();
    }

    [Fact]
    public void Defaults_MaxUses_Is1()
    {
        new Invite().MaxUses.Should().Be(1);
    }

    [Fact]
    public void Defaults_CurrentUses_IsZero()
    {
        new Invite().CurrentUses.Should().Be(0);
    }

    [Fact]
    public void Defaults_IsActive_IsTrue()
    {
        new Invite().IsActive.Should().BeTrue();
    }

    [Fact]
    public void Defaults_CreatedAt_IsApproximatelyUtcNow()
    {
        var before = DateTime.UtcNow;
        var invite = new Invite();
        var after = DateTime.UtcNow;

        invite.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Defaults_ExpiresAt_IsNull()
    {
        new Invite().ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void Defaults_Role_IsNull()
    {
        new Invite().Role.Should().BeNull();
    }

    [Fact]
    public void Defaults_Email_IsNull()
    {
        new Invite().Email.Should().BeNull();
    }

    [Fact]
    public void Defaults_Usages_IsEmptyList()
    {
        new Invite().Usages.Should().BeEmpty();
    }

    // ── Database: CRUD ────────────────────────────────────────────────────────

    [Fact]
    public void Db_CanSaveAndRetrieve()
    {
        var invite = ValidInvite();
        invite.Role = "User";
        invite.Email = "invited@example.com";
        Db.Invites.Add(invite);
        Db.SaveChanges();

        var retrieved = Db.Invites.Find(invite.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Code.Should().Be("TESTCODE12345678");
        retrieved.Role.Should().Be("User");
        retrieved.Email.Should().Be("invited@example.com");
        retrieved.IsSingleUse.Should().BeTrue();
        retrieved.MaxUses.Should().Be(1);
        retrieved.CurrentUses.Should().Be(0);
        retrieved.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Db_CanUpdateCurrentUses()
    {
        var invite = ValidInvite();
        invite.IsSingleUse = false;
        invite.MaxUses = 5;
        Db.Invites.Add(invite);
        Db.SaveChanges();

        invite.CurrentUses = 3;
        Db.SaveChanges();

        var updated = Db.Invites.Find(invite.Id);
        updated!.CurrentUses.Should().Be(3);
    }

    [Fact]
    public void Db_CanDeactivate()
    {
        var invite = ValidInvite();
        Db.Invites.Add(invite);
        Db.SaveChanges();

        invite.IsActive = false;
        Db.SaveChanges();

        var updated = Db.Invites.Find(invite.Id);
        updated!.IsActive.Should().BeFalse();
    }

    // ── Database: relationships ───────────────────────────────────────────────

    [Fact]
    public void Db_CanAddUsage()
    {
        var invite = ValidInvite();
        Db.Invites.Add(invite);
        Db.SaveChanges();

        Db.InviteUsages.Add(new InviteUsage
        {
            InviteId = invite.Id,
            UserId   = "user-abc",
        });
        invite.CurrentUses = 1;
        Db.SaveChanges();

        var loaded = Db.Invites
            .Include(i => i.Usages)
            .First(i => i.Id == invite.Id);

        loaded.Usages.Should().HaveCount(1);
        loaded.Usages[0].UserId.Should().Be("user-abc");
    }

    [Fact]
    public void Db_DeleteInvite_CascadesToUsages()
    {
        var invite = ValidInvite();
        Db.Invites.Add(invite);
        Db.SaveChanges();

        Db.InviteUsages.Add(new InviteUsage { InviteId = invite.Id, UserId = "user-1" });
        Db.InviteUsages.Add(new InviteUsage { InviteId = invite.Id, UserId = "user-2" });
        Db.SaveChanges();

        Db.Invites.Remove(invite);
        Db.SaveChanges();

        Db.InviteUsages.Where(u => u.InviteId == invite.Id).Should().BeEmpty();
    }

    // ── Expiry logic ──────────────────────────────────────────────────────────

    [Fact]
    public void ExpiresAt_CanBeSetAndRetrieved()
    {
        var expiry = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var invite = ValidInvite();
        invite.ExpiresAt = expiry;

        Db.Invites.Add(invite);
        Db.SaveChanges();

        var retrieved = Db.Invites.Find(invite.Id);
        retrieved!.ExpiresAt.Should().Be(expiry);
    }
}
