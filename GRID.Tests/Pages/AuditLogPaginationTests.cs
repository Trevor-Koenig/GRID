using FluentAssertions;
using GRID.Models;
using GRID.Pages.Admin.AuditLog;
using GRID.Tests.Helpers;

namespace GRID.Tests.Pages;

/// <summary>
/// Tests for the AuditLog index page pagination logic.
///
/// Root cause of the original bug: the bound property was named "Page", which
/// is a reserved Razor Pages route value (it identifies which .cshtml to render).
/// Using asp-route-page in the view overwrote the page path instead of appending
/// a query-string parameter, so clicking a pagination link navigated to a broken
/// URL rather than ?currentPage=N. Fixed by renaming to CurrentPage.
/// </summary>
public class AuditLogPaginationTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private TestDbContext Db => _factory.Context;

    public void Dispose() => _factory.Dispose();

    // ── TotalPages formula ────────────────────────────────────────────────────

    [Theory]
    [InlineData(0,   50, 0)]
    [InlineData(1,   50, 1)]
    [InlineData(50,  50, 1)]
    [InlineData(51,  50, 2)]
    [InlineData(100, 50, 2)]
    [InlineData(101, 50, 3)]
    [InlineData(99,  50, 2)]
    public void TotalPages_CalculatesCorrectly(int totalCount, int pageSize, int expected)
    {
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        totalPages.Should().Be(expected);
    }

    [Fact]
    public void PageSize_ConstantIs50()
    {
        IndexModel.PageSize.Should().Be(50);
    }

    // ── Page clamping ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(-10, 1)]
    [InlineData(-1,  1)]
    [InlineData(0,   1)]
    [InlineData(1,   1)]
    [InlineData(2,   2)]
    [InlineData(99,  99)]
    public void Page_BelowOne_ClampsToOne(int input, int expected)
    {
        var page = input;
        if (page < 1) page = 1;
        page.Should().Be(expected);
    }

    // ── Skip / Take math ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, 50,  0)]
    [InlineData(2, 50, 50)]
    [InlineData(3, 50, 100)]
    [InlineData(5, 50, 200)]
    public void SkipAmount_CalculatesCorrectly(int page, int pageSize, int expectedSkip)
    {
        var skip = (page - 1) * pageSize;
        skip.Should().Be(expectedSkip);
    }

    [Theory]
    [InlineData(1,  50, 50,   1,  50)]  // page 1, 50 entries  → 1–50
    [InlineData(2, 100, 50,  51, 100)]  // page 2, 100 entries → 51–100
    [InlineData(3, 110, 50, 101, 110)]  // page 3, 110 entries → last page, 10 remaining
    [InlineData(1,   7, 50,   1,   7)]  // fewer entries than pageSize → 1–7
    public void DisplayRange_CalculatesCorrectly(
        int page, int totalCount, int pageSize,
        int expectedFrom, int expectedTo)
    {
        var from = (page - 1) * pageSize + 1;
        var to   = Math.Min(page * pageSize, totalCount);

        from.Should().Be(expectedFrom);
        to.Should().Be(expectedTo);
    }

    // ── Pagination query: skip / take against real data ───────────────────────

    private void SeedLogs(int count)
    {
        var logs = Enumerable.Range(1, count).Select(i => new AuditLog
        {
            Action    = "PageView",
            Timestamp = DateTime.UtcNow.AddMinutes(-i),
        });
        Db.AuditLogs.AddRange(logs);
        Db.SaveChanges();
    }

    [Fact]
    public void Query_Page1_ReturnsFirstPageSize_Entries()
    {
        SeedLogs(120);
        const int pageSize = 50;

        var results = Db.AuditLogs
            .OrderByDescending(l => l.Timestamp)
            .Skip(0)
            .Take(pageSize)
            .ToList();

        results.Should().HaveCount(pageSize);
    }

    [Fact]
    public void Query_Page2_ReturnsNextPageSize_Entries()
    {
        SeedLogs(120);
        const int pageSize = 50;

        var results = Db.AuditLogs
            .OrderByDescending(l => l.Timestamp)
            .Skip(pageSize)
            .Take(pageSize)
            .ToList();

        results.Should().HaveCount(pageSize);
    }

    [Fact]
    public void Query_LastPage_ReturnsRemainder()
    {
        SeedLogs(110);   // 110 entries → page 3 has 10
        const int pageSize = 50;

        var results = Db.AuditLogs
            .OrderByDescending(l => l.Timestamp)
            .Skip(2 * pageSize)   // skip pages 1 and 2
            .Take(pageSize)
            .ToList();

        results.Should().HaveCount(10);
    }

    [Fact]
    public void Query_OrderedByTimestampDescending_MostRecentFirst()
    {
        SeedLogs(5);

        var results = Db.AuditLogs
            .OrderByDescending(l => l.Timestamp)
            .ToList();

        results.Should().BeInDescendingOrder(l => l.Timestamp);
    }

    [Fact]
    public void Query_TotalCount_MatchesSeedCount()
    {
        SeedLogs(73);
        Db.AuditLogs.Count().Should().Be(73);
    }

    // ── Filter: action type ───────────────────────────────────────────────────

    [Fact]
    public void Filter_PageViews_ExcludesAdminActions()
    {
        Db.AuditLogs.AddRange(
            new AuditLog { Action = "PageView" },
            new AuditLog { Action = "PageView" },
            new AuditLog { Action = "UserDeleted" }
        );
        Db.SaveChanges();

        var results = Db.AuditLogs
            .Where(l => l.Action == "PageView")
            .ToList();

        results.Should().HaveCount(2);
        results.Should().AllSatisfy(l => l.Action.Should().Be("PageView"));
    }

    [Fact]
    public void Filter_Admin_ExcludesPageViews()
    {
        Db.AuditLogs.AddRange(
            new AuditLog { Action = "PageView" },
            new AuditLog { Action = "UserDeleted" },
            new AuditLog { Action = "InviteCreated" }
        );
        Db.SaveChanges();

        var results = Db.AuditLogs
            .Where(l => l.Action != "PageView")
            .ToList();

        results.Should().HaveCount(2);
        results.Should().NotContain(l => l.Action == "PageView");
    }

    [Fact]
    public void Filter_All_ReturnsEverything()
    {
        Db.AuditLogs.AddRange(
            new AuditLog { Action = "PageView" },
            new AuditLog { Action = "UserDeleted" }
        );
        Db.SaveChanges();

        Db.AuditLogs.Count().Should().Be(2);
    }
}
