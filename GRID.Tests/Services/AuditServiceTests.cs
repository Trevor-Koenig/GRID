using FluentAssertions;
using GRID.Services;
using GRID.Tests.Helpers;

namespace GRID.Tests.Services;

public class AuditServiceTests : IDisposable
{
    private readonly TestApplicationDbContextFactory _factory = new();
    private TestApplicationDbContext Db => _factory.Context;

    public void Dispose() => _factory.Dispose();

    // ── Persistence ───────────────────────────────────────────────────────────

    [Fact]
    public async Task LogAsync_SavesOneAuditLogRecord()
    {
        var service = new AuditService(Db);

        await service.LogAsync("TestAction");

        Db.AuditLogs.Should().HaveCount(1);
    }

    [Fact]
    public async Task LogAsync_SetsActionField()
    {
        var service = new AuditService(Db);

        await service.LogAsync("UserLoggedIn");

        Db.AuditLogs.Single().Action.Should().Be("UserLoggedIn");
    }

    [Fact]
    public async Task LogAsync_SetsTimestampCloseToUtcNow()
    {
        var before = DateTime.UtcNow;
        var service = new AuditService(Db);

        await service.LogAsync("SomeAction");

        var after = DateTime.UtcNow;
        Db.AuditLogs.Single().Timestamp
            .Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task LogAsync_WithAllFields_PersistsEachField()
    {
        var service = new AuditService(Db);

        await service.LogAsync(
            action: "EditUser",
            actorId: "actor-123",
            actorEmail: "admin@example.com",
            entityType: "User",
            entityId: "user-456",
            details: "Changed role to Admin");

        var log = Db.AuditLogs.Single();
        log.Action.Should().Be("EditUser");
        log.ActorId.Should().Be("actor-123");
        log.ActorEmail.Should().Be("admin@example.com");
        log.EntityType.Should().Be("User");
        log.EntityId.Should().Be("user-456");
        log.Details.Should().Be("Changed role to Admin");
    }

    [Fact]
    public async Task LogAsync_WithNoOptionalFields_NullableFieldsAreNull()
    {
        var service = new AuditService(Db);

        await service.LogAsync("MinimalAction");

        var log = Db.AuditLogs.Single();
        log.ActorId.Should().BeNull();
        log.ActorEmail.Should().BeNull();
        log.EntityType.Should().BeNull();
        log.EntityId.Should().BeNull();
        log.Details.Should().BeNull();
    }

    [Fact]
    public async Task LogAsync_MultipleCalls_SavesAllRecords()
    {
        var service = new AuditService(Db);

        await service.LogAsync("ActionOne");
        await service.LogAsync("ActionTwo");
        await service.LogAsync("ActionThree");

        Db.AuditLogs.Should().HaveCount(3);
    }
}
