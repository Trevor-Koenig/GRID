using FluentAssertions;
using GRID.Models;
using GRID.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace GRID.Tests.Models;

public class DocArticleTests
{
    private static DocArticle ValidArticle(string slug = "getting-started") => new()
    {
        Title    = "Getting Started",
        Slug     = slug,
        Category = "guides",
    };

    // ── Default values ────────────────────────────────────────────────────────

    [Fact]
    public void Content_DefaultsToEmptyString()
    {
        new DocArticle().Content.Should().Be(string.Empty);
    }

    [Fact]
    public void IsPublished_DefaultsToFalse()
    {
        new DocArticle().IsPublished.Should().BeFalse();
    }

    [Fact]
    public void IsPublic_DefaultsToTrue()
    {
        new DocArticle().IsPublic.Should().BeTrue();
    }

    [Fact]
    public void DisplayOrder_DefaultsToZero()
    {
        new DocArticle().DisplayOrder.Should().Be(0);
    }

    [Fact]
    public void ServiceToken_DefaultsToNull()
    {
        new DocArticle().ServiceToken.Should().BeNull();
    }

    // ── Validation: required fields ───────────────────────────────────────────

    [Theory]
    [InlineData(nameof(DocArticle.Title))]
    [InlineData(nameof(DocArticle.Slug))]
    [InlineData(nameof(DocArticle.Category))]
    public void Validation_RequiredField_FailsWhenNull(string fieldName)
    {
        var article = ValidArticle();
        typeof(DocArticle).GetProperty(fieldName)!.SetValue(article, null);

        ModelValidator.ErrorsFor(article, fieldName).Should().NotBeEmpty();
    }

    // ── Validation: max-length ────────────────────────────────────────────────

    [Fact]
    public void Validation_Title_ExceedsMaxLength_ProducesError()
    {
        var article = ValidArticle();
        article.Title = new string('a', 201);

        ModelValidator.ErrorsFor(article, nameof(DocArticle.Title)).Should().NotBeEmpty();
    }

    [Fact]
    public void Validation_Slug_ExceedsMaxLength_ProducesError()
    {
        var article = ValidArticle();
        article.Slug = new string('a', 101);

        ModelValidator.ErrorsFor(article, nameof(DocArticle.Slug)).Should().NotBeEmpty();
    }

    [Fact]
    public void Validation_Category_ExceedsMaxLength_ProducesError()
    {
        var article = ValidArticle();
        article.Category = new string('a', 101);

        ModelValidator.ErrorsFor(article, nameof(DocArticle.Category)).Should().NotBeEmpty();
    }

    [Fact]
    public void Validation_ServiceToken_ExceedsMaxLength_ProducesError()
    {
        var article = ValidArticle();
        article.ServiceToken = new string('a', 21);

        ModelValidator.ErrorsFor(article, nameof(DocArticle.ServiceToken)).Should().NotBeEmpty();
    }

    [Fact]
    public void Validation_ServiceToken_AtMaxLength_IsValid()
    {
        var article = ValidArticle();
        article.ServiceToken = new string('a', 20);

        ModelValidator.ErrorsFor(article, nameof(DocArticle.ServiceToken)).Should().BeEmpty();
    }

    [Fact]
    public void Validation_ValidModel_ProducesNoErrors()
    {
        ModelValidator.IsValid(ValidArticle()).Should().BeTrue();
    }

    // ── Database: round-trip ──────────────────────────────────────────────────

    [Fact]
    public void Db_CanSaveAndRetrieve()
    {
        using var factory = new TestDbContextFactory();
        var db = factory.Context;

        var article = ValidArticle();
        article.Content = "# Hello World";
        db.DocArticles.Add(article);
        db.SaveChanges();

        var retrieved = db.DocArticles.Find(article.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Title.Should().Be("Getting Started");
        retrieved.Content.Should().Be("# Hello World");
        retrieved.IsPublished.Should().BeFalse();
    }

    // ── Null-content guard (mirrors OnPostCreateAsync / OnPostSaveAsync fix) ────

    [Fact]
    public void Db_NullContentCoalesced_SavesAsEmptyString()
    {
        // Simulates form submission where textarea is empty (null from model binding).
        // The page handlers apply `content ?? ""` before saving to prevent the
        // NOT NULL constraint violation on the Content column.
        using var factory = new TestDbContextFactory();
        var db = factory.Context;

        var article = ValidArticle("null-content-test");
        article.Content = null! ?? ""; // mirrors the fix
        db.DocArticles.Add(article);
        db.SaveChanges();

        var retrieved = db.DocArticles.Find(article.Id);
        retrieved!.Content.Should().Be("");
    }

    [Fact]
    public void Db_RawNullContent_ThrowsException()
    {
        // Without the fix, a null Content reaches the DB and violates NOT NULL.
        using var factory = new TestDbContextFactory();
        var db = factory.Context;

        var article = ValidArticle("raw-null-content");
        article.Content = null!;
        db.DocArticles.Add(article);
        var act = () => db.SaveChanges();

        act.Should().Throw<Exception>(); // SQLite raises a constraint exception
    }

    // ── Database: unique constraint on (Category, Slug) ───────────────────────

    [Fact]
    public void Db_DuplicateCategorySlug_ThrowsException()
    {
        using var factory = new TestDbContextFactory();
        var db = factory.Context;

        db.DocArticles.Add(ValidArticle("install"));
        db.SaveChanges();

        db.DocArticles.Add(ValidArticle("install")); // same category + slug
        var act = () => db.SaveChanges();

        act.Should().Throw<DbUpdateException>();
    }

    [Fact]
    public void Db_SameSlug_DifferentCategory_IsAllowed()
    {
        using var factory = new TestDbContextFactory();
        var db = factory.Context;

        db.DocArticles.Add(new DocArticle { Title = "A", Slug = "intro", Category = "guides" });
        db.DocArticles.Add(new DocArticle { Title = "B", Slug = "intro", Category = "reference" });
        db.SaveChanges(); // should not throw
    }
}
