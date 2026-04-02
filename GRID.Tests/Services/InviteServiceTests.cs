using FluentAssertions;
using GRID.Models;
using GRID.Services;
using GRID.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace GRID.Tests.Services;

public class InviteServiceTests : IDisposable
{
    private readonly TestApplicationDbContextFactory _factory = new();
    private TestApplicationDbContext Db => _factory.Context;

    public void Dispose() => _factory.Dispose();

    private InviteService Service => new(Db);

    // ── Code generation ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateInviteAsync_GeneratesA16CharCode()
    {
        var invite = await Service.CreateInviteAsync("User", isSingleUse: true);

        invite.Code.Should().HaveLength(16);
    }

    [Fact]
    public async Task CreateInviteAsync_CodeContainsOnlyUpperAlphanumericChars()
    {
        var invite = await Service.CreateInviteAsync("User", isSingleUse: true);

        invite.Code.Should().MatchRegex("^[A-Z0-9]+$");
    }

    [Fact]
    public async Task CreateInviteAsync_TwoCalls_ProduceDifferentCodes()
    {
        var a = await Service.CreateInviteAsync("User", isSingleUse: true);
        var b = await Service.CreateInviteAsync("User", isSingleUse: true);

        a.Code.Should().NotBe(b.Code);
    }

    // ── CreateInviteAsync (TimeSpan overload) ─────────────────────────────────

    [Fact]
    public async Task CreateInviteAsync_TimeSpanOverload_SingleUse_SetsMaxUsesToOne()
    {
        var invite = await Service.CreateInviteAsync("User", isSingleUse: true, maxUses: 5);

        invite.IsSingleUse.Should().BeTrue();
        invite.MaxUses.Should().Be(1);
    }

    [Fact]
    public async Task CreateInviteAsync_TimeSpanOverload_NotSingleUse_UsesMaxUses()
    {
        var invite = await Service.CreateInviteAsync("User", isSingleUse: false, maxUses: 10);

        invite.IsSingleUse.Should().BeFalse();
        invite.MaxUses.Should().Be(10);
    }

    [Fact]
    public async Task CreateInviteAsync_TimeSpanOverload_WithValidFor_SetsExpiresAtInFuture()
    {
        var before = DateTime.UtcNow.AddHours(1);

        var invite = await Service.CreateInviteAsync("User", validFor: TimeSpan.FromHours(1));

        var after = DateTime.UtcNow.AddHours(1);
        invite.ExpiresAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task CreateInviteAsync_TimeSpanOverload_WithNullValidFor_ExpiresAtIsNull()
    {
        var invite = await Service.CreateInviteAsync("User", validFor: null);

        invite.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task CreateInviteAsync_TimeSpanOverload_SetsRoleAndEmail()
    {
        var invite = await Service.CreateInviteAsync("Admin", email: "user@example.com");

        invite.Role.Should().Be("Admin");
        invite.Email.Should().Be("user@example.com");
    }

    [Fact]
    public async Task CreateInviteAsync_TimeSpanOverload_PersistsToDatabase()
    {
        var invite = await Service.CreateInviteAsync("User");

        var saved = await Db.Invites.FindAsync(invite.Id);
        saved.Should().NotBeNull();
        saved!.Code.Should().Be(invite.Code);
    }

    // ── CreateInviteAsync (DateTime overload) ─────────────────────────────────

    [Fact]
    public async Task CreateInviteAsync_DateTimeOverload_SingleUse_SetsMaxUsesToOne()
    {
        var invite = await Service.CreateInviteAsync(
            role: "User", isSingleUse: true, maxUses: 99, email: null, expiresAt: null);

        invite.MaxUses.Should().Be(1);
    }

    [Fact]
    public async Task CreateInviteAsync_DateTimeOverload_WithExpiresAt_ConvertedToUtc()
    {
        var localExpiry = new DateTime(2030, 6, 15, 12, 0, 0, DateTimeKind.Local);

        var invite = await Service.CreateInviteAsync(
            role: null, isSingleUse: false, maxUses: 5, email: null, expiresAt: localExpiry);

        invite.ExpiresAt.Should().Be(localExpiry.ToUniversalTime());
    }

    [Fact]
    public async Task CreateInviteAsync_DateTimeOverload_WithNullExpiresAt_ExpiresAtIsNull()
    {
        var invite = await Service.CreateInviteAsync(
            role: null, isSingleUse: true, maxUses: null, email: null, expiresAt: null);

        invite.ExpiresAt.Should().BeNull();
    }

    // ── EnsureDevInviteAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task EnsureDevInviteAsync_WhenCodeDoesNotExist_CreatesInvite()
    {
        await Service.EnsureDevInviteAsync("DEVCODE123");

        var invite = await Db.Invites.SingleAsync(i => i.Code == "DEVCODE123");
        invite.Should().NotBeNull();
        invite.IsSingleUse.Should().BeTrue();
        invite.MaxUses.Should().Be(1);
    }

    [Fact]
    public async Task EnsureDevInviteAsync_WhenCodeAlreadyExists_DoesNotCreateDuplicate()
    {
        Db.Invites.Add(new Invite { Code = "EXISTINGCODE" });
        await Db.SaveChangesAsync();

        await Service.EnsureDevInviteAsync("EXISTINGCODE");

        Db.Invites.Count(i => i.Code == "EXISTINGCODE").Should().Be(1);
    }

    [Fact]
    public async Task EnsureDevInviteAsync_CalledTwice_DoesNotCreateDuplicate()
    {
        await Service.EnsureDevInviteAsync("DEVCODE456");
        await Service.EnsureDevInviteAsync("DEVCODE456");

        Db.Invites.Count(i => i.Code == "DEVCODE456").Should().Be(1);
    }

    // ── ValidateInviteAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task ValidateInviteAsync_WhenValidAndUnused_ReturnsCode0WithInvite()
    {
        Db.Invites.Add(new Invite { Code = "VALIDCODE1234567" });
        await Db.SaveChangesAsync();

        var (code, invite) = await Service.ValidateInviteAsync("VALIDCODE1234567");

        code.Should().Be(0);
        invite.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateInviteAsync_WhenNotFound_ReturnsCode1()
    {
        var (code, invite) = await Service.ValidateInviteAsync("DOESNOTEXIST1234");

        code.Should().Be(1);
        invite.Should().BeNull();
    }

    [Fact]
    public async Task ValidateInviteAsync_WhenInactive_ReturnsCode1()
    {
        Db.Invites.Add(new Invite { Code = "INACTIVECODE1234", IsActive = false });
        await Db.SaveChangesAsync();

        var (code, invite) = await Service.ValidateInviteAsync("INACTIVECODE1234");

        code.Should().Be(1);
        invite.Should().BeNull();
    }

    [Fact]
    public async Task ValidateInviteAsync_WhenExpired_ReturnsCode2()
    {
        Db.Invites.Add(new Invite
        {
            Code = "EXPIREDCODE12345",
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        });
        await Db.SaveChangesAsync();

        var (code, invite) = await Service.ValidateInviteAsync("EXPIREDCODE12345");

        code.Should().Be(2);
        invite.Should().BeNull();
    }

    [Fact]
    public async Task ValidateInviteAsync_WhenSingleUseAlreadyUsed_ReturnsCode3()
    {
        Db.Invites.Add(new Invite
        {
            Code = "SINGLEUSECODE123",
            IsSingleUse = true,
            MaxUses = 1,
            CurrentUses = 1
        });
        await Db.SaveChangesAsync();

        var (code, invite) = await Service.ValidateInviteAsync("SINGLEUSECODE123");

        code.Should().Be(3);
        invite.Should().BeNull();
    }

    [Fact]
    public async Task ValidateInviteAsync_WhenMaxUsesExceeded_ReturnsCode4()
    {
        Db.Invites.Add(new Invite
        {
            Code = "MAXUSEDCODE12345",
            IsSingleUse = false,
            MaxUses = 3,
            CurrentUses = 3
        });
        await Db.SaveChangesAsync();

        var (code, invite) = await Service.ValidateInviteAsync("MAXUSEDCODE12345");

        code.Should().Be(4);
        invite.Should().BeNull();
    }

    [Fact]
    public async Task ValidateInviteAsync_WhenUsesAreBelowMax_ReturnsCode0()
    {
        Db.Invites.Add(new Invite
        {
            Code = "PARTIALUSECD1234",
            IsSingleUse = false,
            MaxUses = 5,
            CurrentUses = 3
        });
        await Db.SaveChangesAsync();

        var (code, invite) = await Service.ValidateInviteAsync("PARTIALUSECD1234");

        code.Should().Be(0);
        invite.Should().NotBeNull();
    }

    // ── ConsumeInviteAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task ConsumeInviteAsync_WhenValid_ReturnsSuccessTrue()
    {
        Db.Invites.Add(new Invite { Code = "CONSUMECODE12345", Role = "User" });
        await Db.SaveChangesAsync();

        var (success, role) = await Service.ConsumeInviteAsync("CONSUMECODE12345", "user-1");

        success.Should().BeTrue();
        role.Should().Be("User");
    }

    [Fact]
    public async Task ConsumeInviteAsync_WhenValid_IncrementsCurrentUses()
    {
        Db.Invites.Add(new Invite
        {
            Code = "CONSUMECODE23456",
            IsSingleUse = false,
            MaxUses = 5,
            CurrentUses = 2
        });
        await Db.SaveChangesAsync();

        await Service.ConsumeInviteAsync("CONSUMECODE23456", "user-1");

        var invite = await Db.Invites.SingleAsync(i => i.Code == "CONSUMECODE23456");
        invite.CurrentUses.Should().Be(3);
    }

    [Fact]
    public async Task ConsumeInviteAsync_WhenValid_AddsInviteUsageRecord()
    {
        Db.Invites.Add(new Invite { Code = "CONSUMECODE34567" });
        await Db.SaveChangesAsync();

        await Service.ConsumeInviteAsync("CONSUMECODE34567", "user-abc");

        var usage = await Db.InviteUsages
            .Include(u => u.Invite)
            .SingleAsync(u => u.Invite!.Code == "CONSUMECODE34567");

        usage.UserId.Should().Be("user-abc");
    }

    [Fact]
    public async Task ConsumeInviteAsync_WhenNotFound_ReturnsFalse()
    {
        var (success, role) = await Service.ConsumeInviteAsync("DOESNOTEXIST1234", "user-1");

        success.Should().BeFalse();
        role.Should().BeNull();
    }

    [Fact]
    public async Task ConsumeInviteAsync_WhenExpired_ReturnsFalse()
    {
        Db.Invites.Add(new Invite
        {
            Code = "EXPIREDCONSUME12",
            ExpiresAt = DateTime.UtcNow.AddSeconds(-1)
        });
        await Db.SaveChangesAsync();

        var (success, _) = await Service.ConsumeInviteAsync("EXPIREDCONSUME12", "user-1");

        success.Should().BeFalse();
    }

    [Fact]
    public async Task ConsumeInviteAsync_WhenMaxUsesReached_ReturnsFalse()
    {
        Db.Invites.Add(new Invite
        {
            Code = "MAXCONSUMECODE12",
            IsSingleUse = false,
            MaxUses = 2,
            CurrentUses = 2
        });
        await Db.SaveChangesAsync();

        var (success, _) = await Service.ConsumeInviteAsync("MAXCONSUMECODE12", "user-1");

        success.Should().BeFalse();
    }

    [Fact]
    public async Task ConsumeInviteAsync_WhenInviteHasNoRole_ReturnsNullRole()
    {
        Db.Invites.Add(new Invite { Code = "NOROLECODE123456", Role = null });
        await Db.SaveChangesAsync();

        var (success, role) = await Service.ConsumeInviteAsync("NOROLECODE123456", "user-1");

        success.Should().BeTrue();
        role.Should().BeNull();
    }
}
