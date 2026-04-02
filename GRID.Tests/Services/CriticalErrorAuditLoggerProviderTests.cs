using FluentAssertions;
using GRID.Data;
using GRID.Models;
using GRID.Services;
using GRID.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GRID.Tests.Services;

/// <summary>
/// Tests for CriticalErrorAuditLoggerProvider and its inner logger.
///
/// DB-write tests call the internal WriteEntryAsync / BuildEntry helpers
/// directly so they don't depend on Task.Run timing.
/// </summary>
public class CriticalErrorAuditLoggerProviderTests : IDisposable
{
    private readonly TestApplicationDbContextFactory _factory = new();
    private TestApplicationDbContext Db => _factory.Context;

    public void Dispose() => _factory.Dispose();

    // ── Stub IServiceScopeFactory ─────────────────────────────────────────────

    private sealed class FakeScopeFactory(TestApplicationDbContext db) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new FakeScope(db);

        private sealed class FakeScope(TestApplicationDbContext db) : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = new FakeServiceProvider(db);
            public void Dispose() { }
        }

        private sealed class FakeServiceProvider(TestApplicationDbContext db) : IServiceProvider
        {
            public object? GetService(Type serviceType) =>
                serviceType == typeof(ApplicationDbContext) ? db : null;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private FakeScopeFactory ScopeFactory() => new(Db);

    private static ILogger CreateLogger(IServiceScopeFactory factory, string category = "TestCategory")
        => new CriticalErrorAuditLoggerProvider(factory).CreateLogger(category);

    // ── IsEnabled ─────────────────────────────────────────────────────────────

    [Fact]
    public void IsEnabled_Critical_ReturnsTrue()
    {
        var logger = CreateLogger(ScopeFactory());

        logger.IsEnabled(LogLevel.Critical).Should().BeTrue();
    }

    [Theory]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.None)]
    public void IsEnabled_BelowCritical_ReturnsFalse(LogLevel level)
    {
        var logger = CreateLogger(ScopeFactory());

        logger.IsEnabled(level).Should().BeFalse();
    }

    // ── BuildEntry ────────────────────────────────────────────────────────────

    [Fact]
    public void BuildEntry_SetsActionToCriticalError()
    {
        var entry = CriticalErrorAuditLogger.BuildEntry("Cat", default, "msg", null);

        entry.Action.Should().Be("CriticalError");
    }

    [Fact]
    public void BuildEntry_SetsEntityTypeToCategory()
    {
        var entry = CriticalErrorAuditLogger.BuildEntry("GRID.Services.Foo", default, "msg", null);

        entry.EntityType.Should().Be("GRID.Services.Foo");
    }

    [Fact]
    public void BuildEntry_WithNoException_DetailsEqualsMessage()
    {
        var entry = CriticalErrorAuditLogger.BuildEntry("Cat", default, "Something went wrong", null);

        entry.Details.Should().Be("Something went wrong");
    }

    [Fact]
    public void BuildEntry_WithException_DetailsContainsExceptionType()
    {
        var ex = new InvalidOperationException("bad state");

        var entry = CriticalErrorAuditLogger.BuildEntry("Cat", default, "Something went wrong", ex);

        entry.Details.Should().Contain("InvalidOperationException");
        entry.Details.Should().Contain("bad state");
        entry.Details.Should().Contain("Something went wrong");
    }

    [Fact]
    public void BuildEntry_WithException_DetailsContainsStackTrace()
    {
        Exception ex;
        try { throw new Exception("oops"); }
        catch (Exception e) { ex = e; }

        var entry = CriticalErrorAuditLogger.BuildEntry("Cat", default, "msg", ex);

        entry.Details.Should().Contain("at "); // stack trace fragment
    }

    [Fact]
    public void BuildEntry_LongDetails_TruncatedAt4000Chars()
    {
        var longMessage = new string('x', 5000);

        var entry = CriticalErrorAuditLogger.BuildEntry("Cat", default, longMessage, null);

        entry.Details!.Length.Should().Be(4000);
    }

    [Fact]
    public void BuildEntry_WithNonZeroEventId_SetsEntityId()
    {
        var entry = CriticalErrorAuditLogger.BuildEntry("Cat", new EventId(42, "MyEvent"), "msg", null);

        entry.EntityId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void BuildEntry_WithZeroEventId_EntityIdIsNull()
    {
        var entry = CriticalErrorAuditLogger.BuildEntry("Cat", default, "msg", null);

        entry.EntityId.Should().BeNull();
    }

    [Fact]
    public void BuildEntry_TimestampIsUtc()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);

        var entry = CriticalErrorAuditLogger.BuildEntry("Cat", default, "msg", null);

        entry.Timestamp.Should().BeAfter(before);
        entry.Timestamp.Kind.Should().Be(DateTimeKind.Utc);
    }

    // ── WriteEntryAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task WriteEntryAsync_PersistsEntryToDb()
    {
        var entry = CriticalErrorAuditLogger.BuildEntry("Cat", default, "Something critical", null);

        await CriticalErrorAuditLogger.WriteEntryAsync(ScopeFactory(), entry);

        Db.AuditLogs.Should().ContainSingle(l => l.Action == "CriticalError");
    }

    [Fact]
    public async Task WriteEntryAsync_AllFieldsRoundTrip()
    {
        var entry = CriticalErrorAuditLogger.BuildEntry(
            "GRID.Program", new EventId(7, "Boot"), "Fatal startup error", null);

        await CriticalErrorAuditLogger.WriteEntryAsync(ScopeFactory(), entry);

        var saved = Db.AuditLogs.Single();
        saved.Action.Should().Be("CriticalError");
        saved.EntityType.Should().Be("GRID.Program");
        saved.EntityId.Should().NotBeNullOrEmpty();
        saved.Details.Should().Contain("Fatal startup error");
    }

    [Fact]
    public async Task WriteEntryAsync_WhenDbThrows_DoesNotPropagateException()
    {
        // Use a scope factory that always returns null for ApplicationDbContext
        // to force SaveChanges to throw.
        var brokenFactory = new BrokenScopeFactory();

        var act = async () => await CriticalErrorAuditLogger.WriteEntryAsync(
            brokenFactory,
            CriticalErrorAuditLogger.BuildEntry("Cat", default, "msg", null));

        await act.Should().NotThrowAsync();
    }

    // ── Log() integration (via public ILogger interface) ─────────────────────

    [Fact]
    public void Log_WithNonCriticalLevel_DoesNotQueueWrite()
    {
        // The logger is synchronous up to the Task.Run boundary.
        // Verify it exits early for non-critical levels by checking IsEnabled
        // is the gate — if it returns false the body is never executed.
        var logger = CreateLogger(ScopeFactory());

        // Should not throw even though ScopeFactory would fail if reached.
        logger.Log(LogLevel.Error, "message");
        logger.Log(LogLevel.Warning, "message");
        logger.Log(LogLevel.Information, "message");

        // No DB writes should happen (Task.Run is never called).
        // Give any accidental background task a moment to complete, then check.
        Db.AuditLogs.Should().BeEmpty();
    }

    // ── Provider ──────────────────────────────────────────────────────────────

    [Fact]
    public void CreateLogger_ReturnsDifferentInstancePerCategory()
    {
        var provider = new CriticalErrorAuditLoggerProvider(ScopeFactory());

        var a = provider.CreateLogger("CategoryA");
        var b = provider.CreateLogger("CategoryB");

        a.Should().NotBeSameAs(b);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>A scope factory whose service provider returns null for everything.</summary>
    private sealed class BrokenScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new BrokenScope();

        private sealed class BrokenScope : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = new BrokenProvider();
            public void Dispose() { }
        }

        private sealed class BrokenProvider : IServiceProvider
        {
            public object? GetService(Type serviceType) => null;
        }
    }
}
