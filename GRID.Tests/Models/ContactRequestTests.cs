using FluentAssertions;
using GRID.Models;
using GRID.Tests.Helpers;

namespace GRID.Tests.Models;

public class ContactRequestTests
{
    private static ContactRequest ValidRequest() => new()
    {
        Name    = "Trevor",
        Email   = "test@example.com",
        Subject = "Hello",
        Message = "This is a test message.",
    };

    // ── Default values ────────────────────────────────────────────────────────

    [Fact]
    public void SubmittedAt_DefaultsToApproximatelyUtcNow()
    {
        var before = DateTime.UtcNow;
        var req = new ContactRequest { Name = "x", Email = "x@x.com", Subject = "x", Message = "x" };
        var after = DateTime.UtcNow;

        req.SubmittedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void IsResponded_DefaultsToFalse()
    {
        new ContactRequest().IsResponded.Should().BeFalse();
    }

    [Fact]
    public void RespondedAt_DefaultsToNull()
    {
        new ContactRequest().RespondedAt.Should().BeNull();
    }

    // ── Validation: required fields ───────────────────────────────────────────

    [Theory]
    [InlineData(nameof(ContactRequest.Name))]
    [InlineData(nameof(ContactRequest.Email))]
    [InlineData(nameof(ContactRequest.Subject))]
    [InlineData(nameof(ContactRequest.Message))]
    public void Validation_RequiredField_FailsWhenNull(string fieldName)
    {
        var req = ValidRequest();

        // Set field to null via reflection
        typeof(ContactRequest).GetProperty(fieldName)!.SetValue(req, null);

        ModelValidator.ErrorsFor(req, fieldName).Should().NotBeEmpty();
    }

    // ── Validation: email format ──────────────────────────────────────────────

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    [InlineData("@nodomain")]
    [InlineData("no-at-sign")]
    public void Validation_Email_InvalidFormat_ProducesError(string badEmail)
    {
        var req = ValidRequest();
        req.Email = badEmail;

        ModelValidator.ErrorsFor(req, nameof(ContactRequest.Email)).Should().NotBeEmpty();
    }

    [Fact]
    public void Validation_Email_ValidFormat_ProducesNoError()
    {
        var req = ValidRequest();

        ModelValidator.ErrorsFor(req, nameof(ContactRequest.Email)).Should().BeEmpty();
    }

    // ── Validation: max-length ────────────────────────────────────────────────

    [Fact]
    public void Validation_Name_ExceedsMaxLength_ProducesError()
    {
        var req = ValidRequest();
        req.Name = new string('a', 101);

        ModelValidator.ErrorsFor(req, nameof(ContactRequest.Name)).Should().NotBeEmpty();
    }

    [Fact]
    public void Validation_Name_AtMaxLength_IsValid()
    {
        var req = ValidRequest();
        req.Name = new string('a', 100);

        ModelValidator.ErrorsFor(req, nameof(ContactRequest.Name)).Should().BeEmpty();
    }

    [Fact]
    public void Validation_Email_ExceedsMaxLength_ProducesError()
    {
        var req = ValidRequest();
        req.Email = new string('a', 245) + "@test.com"; // 254 chars, over 256? No - 254 is valid, test 257
        req.Email = new string('a', 248) + "@test.com"; // 257 chars

        ModelValidator.ErrorsFor(req, nameof(ContactRequest.Email)).Should().NotBeEmpty();
    }

    [Fact]
    public void Validation_Subject_ExceedsMaxLength_ProducesError()
    {
        var req = ValidRequest();
        req.Subject = new string('a', 201);

        ModelValidator.ErrorsFor(req, nameof(ContactRequest.Subject)).Should().NotBeEmpty();
    }

    [Fact]
    public void Validation_Subject_AtMaxLength_IsValid()
    {
        var req = ValidRequest();
        req.Subject = new string('a', 200);

        ModelValidator.ErrorsFor(req, nameof(ContactRequest.Subject)).Should().BeEmpty();
    }

    // ── Validation: fully valid model ─────────────────────────────────────────

    [Fact]
    public void Validation_ValidModel_ProducesNoErrors()
    {
        ModelValidator.IsValid(ValidRequest()).Should().BeTrue();
    }

    // ── Database round-trip ───────────────────────────────────────────────────

    [Fact]
    public void Db_CanSaveAndRetrieve()
    {
        using var factory = new TestDbContextFactory();
        var db = factory.Context;

        var req = ValidRequest();
        db.ContactRequests.Add(req);
        db.SaveChanges();

        var retrieved = db.ContactRequests.Find(req.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Trevor");
        retrieved.Email.Should().Be("test@example.com");
        retrieved.IsResponded.Should().BeFalse();
    }

    [Fact]
    public void Db_CanMarkResponded()
    {
        using var factory = new TestDbContextFactory();
        var db = factory.Context;

        var req = ValidRequest();
        db.ContactRequests.Add(req);
        db.SaveChanges();

        req.IsResponded  = true;
        req.RespondedAt  = DateTime.UtcNow;
        db.SaveChanges();

        var updated = db.ContactRequests.Find(req.Id);
        updated!.IsResponded.Should().BeTrue();
        updated.RespondedAt.Should().NotBeNull();
    }
}
