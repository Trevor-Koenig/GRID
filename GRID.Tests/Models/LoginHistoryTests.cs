using FluentAssertions;
using GRID.Models;
using GRID.Tests.Helpers;

namespace GRID.Tests.Models;

public class LoginHistoryTests
{
    private static LoginHistory ValidEntry() => new()
    {
        UserId    = "user-123",
        UserEmail = "user@example.com",
        Succeeded = true,
    };

    // ── Default values ────────────────────────────────────────────────────────

    [Fact]
    public void Timestamp_DefaultsToApproximatelyUtcNow()
    {
        var before = DateTime.UtcNow;
        var entry = new LoginHistory();
        var after = DateTime.UtcNow;

        entry.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Succeeded_DefaultsToFalse()
    {
        new LoginHistory().Succeeded.Should().BeFalse();
    }

    [Fact]
    public void IpAddress_DefaultsToNull()
    {
        new LoginHistory().IpAddress.Should().BeNull();
    }

    [Fact]
    public void Id_DefaultsToZero()
    {
        new LoginHistory().Id.Should().Be(0);
    }

    // ── Property assignment ───────────────────────────────────────────────────

    [Fact]
    public void CanSet_AllProperties()
    {
        var entry = new LoginHistory
        {
            UserId    = "user-456",
            UserEmail = "admin@example.com",
            Succeeded = false,
            IpAddress = "10.0.0.1",
            Timestamp = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc),
        };

        entry.UserId.Should().Be("user-456");
        entry.UserEmail.Should().Be("admin@example.com");
        entry.Succeeded.Should().BeFalse();
        entry.IpAddress.Should().Be("10.0.0.1");
    }

    // ── Database: round-trip ──────────────────────────────────────────────────

    [Fact]
    public void Db_CanSaveSuccessfulLogin()
    {
        using var factory = new TestDbContextFactory();
        var db = factory.Context;

        var entry = ValidEntry();
        entry.IpAddress = "192.168.0.1";
        db.LoginHistories.Add(entry);
        db.SaveChanges();

        var retrieved = db.LoginHistories.Find(entry.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Succeeded.Should().BeTrue();
        retrieved.IpAddress.Should().Be("192.168.0.1");
        retrieved.UserEmail.Should().Be("user@example.com");
    }

    [Fact]
    public void Db_CanSaveFailedLogin()
    {
        using var factory = new TestDbContextFactory();
        var db = factory.Context;

        var entry = ValidEntry();
        entry.Succeeded = false;
        db.LoginHistories.Add(entry);
        db.SaveChanges();

        var retrieved = db.LoginHistories.Find(entry.Id);
        retrieved!.Succeeded.Should().BeFalse();
    }

    [Fact]
    public void Db_MultipleEntries_ForSameUser_AreAllowed()
    {
        using var factory = new TestDbContextFactory();
        var db = factory.Context;

        db.LoginHistories.Add(new LoginHistory { UserId = "u1", UserEmail = "u@test.com", Succeeded = true });
        db.LoginHistories.Add(new LoginHistory { UserId = "u1", UserEmail = "u@test.com", Succeeded = false });
        db.SaveChanges();

        db.LoginHistories.Count(h => h.UserId == "u1").Should().Be(2);
    }
}
