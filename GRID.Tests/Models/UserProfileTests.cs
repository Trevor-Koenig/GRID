using FluentAssertions;
using GRID.Models;
using GRID.Tests.Helpers;

namespace GRID.Tests.Models;

public class UserProfileTests
{
    private static UserProfile ValidProfile(string userId = "user-123") => new()
    {
        UserId    = userId,
        CreatedAt = DateTime.UtcNow,
    };

    // ── Default values ────────────────────────────────────────────────────────

    [Fact]
    public void IsDeactivated_DefaultsToFalse()
    {
        new UserProfile().IsDeactivated.Should().BeFalse();
    }

    [Fact]
    public void DeactivatedAt_DefaultsToNull()
    {
        new UserProfile().DeactivatedAt.Should().BeNull();
    }

    [Fact]
    public void Theme_DefaultsToNull()
    {
        new UserProfile().Theme.Should().BeNull();
    }

    [Fact]
    public void CreatedAt_DefaultsToApproximatelyUtcNow()
    {
        var before = DateTime.UtcNow;
        var profile = new UserProfile();
        var after = DateTime.UtcNow;

        profile.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    // ── Validation: max-length ────────────────────────────────────────────────

    [Fact]
    public void Validation_Theme_ExceedsMaxLength_ProducesError()
    {
        var profile = ValidProfile();
        profile.Theme = new string('a', 11);

        ModelValidator.ErrorsFor(profile, nameof(UserProfile.Theme)).Should().NotBeEmpty();
    }

    [Fact]
    public void Validation_Theme_AtMaxLength_IsValid()
    {
        var profile = ValidProfile();
        profile.Theme = new string('a', 10);

        ModelValidator.ErrorsFor(profile, nameof(UserProfile.Theme)).Should().BeEmpty();
    }

    [Fact]
    public void Validation_Theme_Null_IsValid()
    {
        var profile = ValidProfile();
        profile.Theme = null;

        ModelValidator.ErrorsFor(profile, nameof(UserProfile.Theme)).Should().BeEmpty();
    }

    [Fact]
    public void Validation_ValidModel_ProducesNoErrors()
    {
        ModelValidator.IsValid(ValidProfile()).Should().BeTrue();
    }

    // ── Database: CRUD ────────────────────────────────────────────────────────

    [Fact]
    public void Db_CanSaveAndRetrieve()
    {
        using var factory = new TestDbContextFactory();
        var db = factory.Context;

        var profile = ValidProfile("user-save-test");
        profile.Theme = "dark";
        db.UserProfiles.Add(profile);
        db.SaveChanges();

        var retrieved = db.UserProfiles.Find("user-save-test");
        retrieved.Should().NotBeNull();
        retrieved!.UserId.Should().Be("user-save-test");
        retrieved.Theme.Should().Be("dark");
        retrieved.IsDeactivated.Should().BeFalse();
    }

    [Fact]
    public void Db_CanDeactivateProfile()
    {
        using var factory = new TestDbContextFactory();
        var db = factory.Context;

        var profile = ValidProfile("user-deactivate");
        db.UserProfiles.Add(profile);
        db.SaveChanges();

        profile.IsDeactivated = true;
        profile.DeactivatedAt = DateTime.UtcNow;
        db.SaveChanges();

        var updated = db.UserProfiles.Find("user-deactivate");
        updated!.IsDeactivated.Should().BeTrue();
        updated.DeactivatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Db_CanUpdateTheme()
    {
        using var factory = new TestDbContextFactory();
        var db = factory.Context;

        var profile = ValidProfile("user-theme");
        db.UserProfiles.Add(profile);
        db.SaveChanges();

        profile.Theme = "light";
        db.SaveChanges();

        db.UserProfiles.Find("user-theme")!.Theme.Should().Be("light");
    }

    [Fact]
    public void UserId_IsThePrimaryKey()
    {
        using var factory = new TestDbContextFactory();
        var db = factory.Context;

        var profile = ValidProfile("pk-test-user");
        db.UserProfiles.Add(profile);
        db.SaveChanges();

        // Find by PK (UserId string, not int Id)
        var found = db.UserProfiles.Find("pk-test-user");
        found.Should().NotBeNull();
    }
}
