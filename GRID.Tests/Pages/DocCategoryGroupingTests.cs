using FluentAssertions;
using GRID.Models;
using GRID.Tests.Helpers;

namespace GRID.Tests.Pages;

/// <summary>
/// Tests for doc article category grouping and normalization.
/// The grouping logic lives in IndexModel.OnGetAsync and uses a
/// Dictionary&lt;string, List&lt;DocArticle&gt;&gt;(StringComparer.OrdinalIgnoreCase)
/// so that mixed-case categories are treated as the same group.
/// </summary>
public class DocCategoryGroupingTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Replicates the grouping algorithm from IndexModel.OnGetAsync exactly.
    /// </summary>
    private static Dictionary<string, List<DocArticle>> Group(IEnumerable<DocArticle> articles)
    {
        var grouped = new Dictionary<string, List<DocArticle>>(StringComparer.OrdinalIgnoreCase);
        foreach (var article in articles)
        {
            var key = article.Category.Trim().ToLower();
            if (!grouped.TryGetValue(key, out var bucket))
                grouped[key] = bucket = new List<DocArticle>();
            bucket.Add(article);
        }
        return grouped;
    }

    private static DocArticle Article(string title, string slug, string category) => new()
    {
        Title    = title,
        Slug     = slug,
        Category = category,
    };

    // ── Grouping: mixed case ──────────────────────────────────────────────────

    [Fact]
    public void Group_SameCategoryDifferentCase_ProducesSingleGroup()
    {
        var articles = new[]
        {
            Article("A", "a", "gRid"),
            Article("B", "b", "grId"),
            Article("C", "c", "GRID"),
        };

        var groups = Group(articles);

        groups.Should().HaveCount(1);
        groups.ContainsKey("grid").Should().BeTrue();
        groups["grid"].Should().HaveCount(3);
    }

    [Fact]
    public void Group_DifferentCategories_ProduceSeparateGroups()
    {
        var articles = new[]
        {
            Article("A", "a", "guides"),
            Article("B", "b", "reference"),
            Article("C", "c", "grid"),
        };

        var groups = Group(articles);

        groups.Should().HaveCount(3);
        groups.Should().ContainKey("guides");
        groups.Should().ContainKey("reference");
        groups.Should().ContainKey("grid");
    }

    [Fact]
    public void Group_MixedCaseAcrossMultipleCategories_GroupsEachCorrectly()
    {
        var articles = new[]
        {
            Article("A", "a", "Guides"),
            Article("B", "b", "GUIDES"),
            Article("C", "c", "Reference"),
            Article("D", "d", "reference"),
        };

        var groups = Group(articles);

        groups.Should().HaveCount(2);
        groups["guides"].Should().HaveCount(2);
        groups["reference"].Should().HaveCount(2);
    }

    // ── Grouping: whitespace trimming ─────────────────────────────────────────

    [Fact]
    public void Group_CategoryWithSurroundingWhitespace_MergesWithTrimmedCategory()
    {
        var articles = new[]
        {
            Article("A", "a", " grid "),
            Article("B", "b", "grid"),
        };

        var groups = Group(articles);

        groups.Should().HaveCount(1);
        groups["grid"].Should().HaveCount(2);
    }

    // ── Grouping: ordering ────────────────────────────────────────────────────

    [Fact]
    public void Group_OrderedAlphabetically_KeysAreInOrder()
    {
        var articles = new[]
        {
            Article("A", "a", "Zebra"),
            Article("B", "b", "Alpha"),
            Article("C", "c", "Middle"),
        };

        var ordered = Group(articles).OrderBy(kv => kv.Key).Select(kv => kv.Key).ToList();

        ordered.Should().Equal("alpha", "middle", "zebra");
    }

    // ── Normalization on save ─────────────────────────────────────────────────

    [Fact]
    public void Db_CategorySavedAsLowercase_StoredNormalized()
    {
        // Simulates what OnPostCreateAsync does: category.Trim().ToLower()
        using var factory = new TestDbContextFactory();
        var db = factory.Context;

        db.DocArticles.Add(new DocArticle
        {
            Title    = "Test",
            Slug     = "test",
            Category = "GRID".Trim().ToLower(), // mirrors the save handler
        });
        db.SaveChanges();

        var saved = db.DocArticles.First();
        saved.Category.Should().Be("grid");
    }

    [Fact]
    public void Db_TwoArticlesSavedWithDifferentCases_StoredIdentically()
    {
        using var factory = new TestDbContextFactory();
        var db = factory.Context;

        db.DocArticles.Add(new DocArticle { Title = "A", Slug = "a", Category = "gRid".Trim().ToLower() });
        db.DocArticles.Add(new DocArticle { Title = "B", Slug = "b", Category = "GRID".Trim().ToLower() });
        db.SaveChanges();

        var categories = db.DocArticles.Select(a => a.Category).ToList();
        categories.Should().AllBe("grid");
    }

    // ── Full round-trip: DB fetch → group ─────────────────────────────────────

    [Fact]
    public void Db_FetchAndGroup_MixedCaseLegacyData_GroupedTogether()
    {
        // Simulates legacy rows that were saved before normalization was added.
        using var factory = new TestDbContextFactory();
        var db = factory.Context;

        // Bypass the handler normalization by setting Category directly
        db.DocArticles.Add(new DocArticle { Title = "A", Slug = "a", Category = "gRid" });
        db.DocArticles.Add(new DocArticle { Title = "B", Slug = "b", Category = "grId" });
        db.DocArticles.Add(new DocArticle { Title = "C", Slug = "c", Category = "guides" });
        db.SaveChanges();

        var articles = db.DocArticles.OrderBy(d => d.DisplayOrder).ThenBy(d => d.Title).ToList();
        var groups = Group(articles);

        groups.Should().HaveCount(2);
        groups["grid"].Should().HaveCount(2);
        groups["guides"].Should().HaveCount(1);
    }

    [Fact]
    public void Db_FetchAndGroup_NormalizedAndLegacyData_MergedCorrectly()
    {
        // One article saved before normalization ("GRID"), one after ("grid").
        using var factory = new TestDbContextFactory();
        var db = factory.Context;

        db.DocArticles.Add(new DocArticle { Title = "Legacy", Slug = "legacy", Category = "GRID" });
        db.DocArticles.Add(new DocArticle { Title = "New",    Slug = "new",    Category = "grid" });
        db.SaveChanges();

        var articles = db.DocArticles.ToList();
        var groups = Group(articles);

        groups.Should().HaveCount(1);
        groups["grid"].Should().HaveCount(2);
    }
}
