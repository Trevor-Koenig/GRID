using FluentAssertions;
using GRID.Models;
using GRID.Tests.Helpers;

namespace GRID.Tests.Models;

public class AuditLogTests
{
    // ── Default values ────────────────────────────────────────────────────────

    [Fact]
    public void Timestamp_DefaultsToApproximatelyUtcNow()
    {
        var before = DateTime.UtcNow;
        var log = new AuditLog();
        var after = DateTime.UtcNow;

        log.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void AllNullableFields_DefaultToNull()
    {
        var log = new AuditLog();

        log.ActorId.Should().BeNull();
        log.ActorEmail.Should().BeNull();
        log.EntityType.Should().BeNull();
        log.EntityId.Should().BeNull();
        log.Details.Should().BeNull();
        log.DurationSeconds.Should().BeNull();
        log.IpAddress.Should().BeNull();
        log.HttpStatus.Should().BeNull();
    }

    [Fact]
    public void Id_DefaultsToZero()
    {
        new AuditLog().Id.Should().Be(0);
    }

    // ── Property assignment ───────────────────────────────────────────────────

    [Fact]
    public void CanSet_AllProperties()
    {
        var log = new AuditLog
        {
            Action     = "UserDeleted",
            ActorId    = "actor-123",
            ActorEmail = "admin@example.com",
            EntityType = "User",
            EntityId   = "user-456",
            Details    = "{ \"reason\": \"spam\" }",
            DurationSeconds = 2,
            IpAddress  = "192.168.1.1",
            HttpStatus = 200,
            Timestamp  = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        log.Action.Should().Be("UserDeleted");
        log.ActorId.Should().Be("actor-123");
        log.ActorEmail.Should().Be("admin@example.com");
        log.EntityType.Should().Be("User");
        log.EntityId.Should().Be("user-456");
        log.DurationSeconds.Should().Be(2);
        log.IpAddress.Should().Be("192.168.1.1");
        log.HttpStatus.Should().Be(200);
    }

    // ── Database round-trip ───────────────────────────────────────────────────

    [Fact]
    public void Db_CanSaveAndRetrieve()
    {
        using var factory = new TestDbContextFactory();
        var db = factory.Context;

        var log = new AuditLog
        {
            Action    = "TestAction",
            ActorId   = "user-1",
            Timestamp = DateTime.UtcNow,
        };
        db.AuditLogs.Add(log);
        db.SaveChanges();

        var retrieved = db.AuditLogs.Find(log.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Action.Should().Be("TestAction");
        retrieved.ActorId.Should().Be("user-1");
    }
}
