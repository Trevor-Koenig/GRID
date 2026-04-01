using FluentAssertions;
using GRID.Models;
using GRID.Tests.Helpers;

namespace GRID.Tests.Models;

public class ServiceLinkTests
{
    private static ServiceLink ValidLink() => new()
    {
        Name  = "Grafana",
        Token = "grafana",
        Url   = "https://grafana.example.com",
    };

    // ── Default values ────────────────────────────────────────────────────────

    [Fact]
    public void RequiresAuth_DefaultsToTrue()  => new ServiceLink().RequiresAuth.Should().BeTrue();
    [Fact]
    public void IsActive_DefaultsToTrue()      => new ServiceLink().IsActive.Should().BeTrue();
    [Fact]
    public void ShowInNav_DefaultsToTrue()     => new ServiceLink().ShowInNav.Should().BeTrue();
    [Fact]
    public void ShowInHero_DefaultsToTrue()    => new ServiceLink().ShowInHero.Should().BeTrue();
    [Fact]
    public void ShowInServices_DefaultsToTrue()=> new ServiceLink().ShowInServices.Should().BeTrue();
    [Fact]
    public void DisplayOrder_DefaultsToZero()  => new ServiceLink().DisplayOrder.Should().Be(0);
    [Fact]
    public void IconClass_DefaultsToNull()     => new ServiceLink().IconClass.Should().BeNull();
    [Fact]
    public void Description_DefaultsToNull()   => new ServiceLink().Description.Should().BeNull();

    // ── Validation: required fields ───────────────────────────────────────────

    [Theory]
    [InlineData(nameof(ServiceLink.Name))]
    [InlineData(nameof(ServiceLink.Token))]
    [InlineData(nameof(ServiceLink.Url))]
    public void Validation_RequiredField_FailsWhenNull(string fieldName)
    {
        var link = ValidLink();
        typeof(ServiceLink).GetProperty(fieldName)!.SetValue(link, null);

        ModelValidator.ErrorsFor(link, fieldName).Should().NotBeEmpty();
    }

    // ── Validation: max-length ────────────────────────────────────────────────

    [Fact]
    public void Validation_Name_ExceedsMaxLength_ProducesError()
    {
        var link = ValidLink();
        link.Name = new string('a', 101);

        ModelValidator.ErrorsFor(link, nameof(ServiceLink.Name)).Should().NotBeEmpty();
    }

    [Fact]
    public void Validation_Name_AtMaxLength_IsValid()
    {
        var link = ValidLink();
        link.Name = new string('a', 100);

        ModelValidator.ErrorsFor(link, nameof(ServiceLink.Name)).Should().BeEmpty();
    }

    [Fact]
    public void Validation_Token_ExceedsMaxLength_ProducesError()
    {
        var link = ValidLink();
        link.Token = new string('a', 21);

        ModelValidator.ErrorsFor(link, nameof(ServiceLink.Token)).Should().NotBeEmpty();
    }

    [Fact]
    public void Validation_Token_AtMaxLength_IsValid()
    {
        var link = ValidLink();
        link.Token = new string('a', 20);

        ModelValidator.ErrorsFor(link, nameof(ServiceLink.Token)).Should().BeEmpty();
    }

    [Fact]
    public void Validation_IconClass_ExceedsMaxLength_ProducesError()
    {
        var link = ValidLink();
        link.IconClass = new string('a', 101);

        ModelValidator.ErrorsFor(link, nameof(ServiceLink.IconClass)).Should().NotBeEmpty();
    }

    [Fact]
    public void Validation_IconClass_AtMaxLength_IsValid()
    {
        var link = ValidLink();
        link.IconClass = new string('a', 100);

        ModelValidator.ErrorsFor(link, nameof(ServiceLink.IconClass)).Should().BeEmpty();
    }

    [Fact]
    public void Validation_ValidModel_ProducesNoErrors()
    {
        ModelValidator.IsValid(ValidLink()).Should().BeTrue();
    }

    // ── Database: round-trip ──────────────────────────────────────────────────

    [Fact]
    public void Db_CanSaveAndRetrieve()
    {
        using var factory = new TestDbContextFactory();
        var db = factory.Context;

        var link = ValidLink();
        link.IconClass    = "bi bi-graph-up";
        link.Description  = "Monitoring dashboard";
        link.DisplayOrder = 3;
        db.ServiceLinks.Add(link);
        db.SaveChanges();

        var retrieved = db.ServiceLinks.Find(link.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Grafana");
        retrieved.Token.Should().Be("grafana");
        retrieved.IconClass.Should().Be("bi bi-graph-up");
        retrieved.DisplayOrder.Should().Be(3);
        retrieved.RequiresAuth.Should().BeTrue();
    }

    [Fact]
    public void Db_CanToggleVisibilityFlags()
    {
        using var factory = new TestDbContextFactory();
        var db = factory.Context;

        var link = ValidLink();
        link.ShowInNav      = false;
        link.ShowInHero     = false;
        link.ShowInServices = true;
        db.ServiceLinks.Add(link);
        db.SaveChanges();

        var retrieved = db.ServiceLinks.Find(link.Id);
        retrieved!.ShowInNav.Should().BeFalse();
        retrieved.ShowInHero.Should().BeFalse();
        retrieved.ShowInServices.Should().BeTrue();
    }
}
