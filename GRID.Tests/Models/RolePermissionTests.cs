using FluentAssertions;
using GRID.Models;
using GRID.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace GRID.Tests.Models;

public class RolePermissionTests
{
    private static RolePermission ValidRolePermission() => new()
    {
        RoleName   = "Admin",
        Permission = Permissions.AdminAccess,
    };

    // ── Default values ────────────────────────────────────────────────────────

    [Fact]
    public void RoleName_DefaultsToEmptyString()
    {
        new RolePermission().RoleName.Should().Be(string.Empty);
    }

    [Fact]
    public void Permission_DefaultsToEmptyString()
    {
        new RolePermission().Permission.Should().Be(string.Empty);
    }

    // ── Validation: required fields ───────────────────────────────────────────

    [Fact]
    public void Validation_RoleName_Empty_ProducesError()
    {
        var rp = ValidRolePermission();
        rp.RoleName = string.Empty;

        ModelValidator.ErrorsFor(rp, nameof(RolePermission.RoleName)).Should().NotBeEmpty();
    }

    [Fact]
    public void Validation_Permission_Empty_ProducesError()
    {
        var rp = ValidRolePermission();
        rp.Permission = string.Empty;

        ModelValidator.ErrorsFor(rp, nameof(RolePermission.Permission)).Should().NotBeEmpty();
    }

    // ── Validation: max-length ────────────────────────────────────────────────

    [Fact]
    public void Validation_RoleName_ExceedsMaxLength_ProducesError()
    {
        var rp = ValidRolePermission();
        rp.RoleName = new string('a', 257);

        ModelValidator.ErrorsFor(rp, nameof(RolePermission.RoleName)).Should().NotBeEmpty();
    }

    [Fact]
    public void Validation_RoleName_AtMaxLength_IsValid()
    {
        var rp = ValidRolePermission();
        rp.RoleName = new string('a', 256);

        ModelValidator.ErrorsFor(rp, nameof(RolePermission.RoleName)).Should().BeEmpty();
    }

    [Fact]
    public void Validation_Permission_ExceedsMaxLength_ProducesError()
    {
        var rp = ValidRolePermission();
        rp.Permission = new string('a', 101);

        ModelValidator.ErrorsFor(rp, nameof(RolePermission.Permission)).Should().NotBeEmpty();
    }

    [Fact]
    public void Validation_Permission_AtMaxLength_IsValid()
    {
        var rp = ValidRolePermission();
        rp.Permission = new string('a', 100);

        ModelValidator.ErrorsFor(rp, nameof(RolePermission.Permission)).Should().BeEmpty();
    }

    [Fact]
    public void Validation_ValidModel_ProducesNoErrors()
    {
        ModelValidator.IsValid(ValidRolePermission()).Should().BeTrue();
    }

    // ── Database: CRUD ────────────────────────────────────────────────────────

    [Fact]
    public void Db_CanSaveAndRetrieve()
    {
        using var factory = new TestDbContextFactory();
        var db = factory.Context;

        var rp = ValidRolePermission();
        db.RolePermissions.Add(rp);
        db.SaveChanges();

        var retrieved = db.RolePermissions.Find(rp.Id);
        retrieved.Should().NotBeNull();
        retrieved!.RoleName.Should().Be("Admin");
        retrieved.Permission.Should().Be(Permissions.AdminAccess);
    }

    // ── Database: unique constraint ───────────────────────────────────────────

    [Fact]
    public void Db_DuplicateRolePermission_ThrowsException()
    {
        using var factory = new TestDbContextFactory();
        var db = factory.Context;

        db.RolePermissions.Add(ValidRolePermission());
        db.SaveChanges();

        db.RolePermissions.Add(ValidRolePermission()); // exact duplicate
        var act = () => db.SaveChanges();

        act.Should().Throw<DbUpdateException>();
    }

    [Fact]
    public void Db_SameRole_DifferentPermission_IsAllowed()
    {
        using var factory = new TestDbContextFactory();
        var db = factory.Context;

        db.RolePermissions.Add(new RolePermission { RoleName = "Admin", Permission = Permissions.AdminAccess });
        db.RolePermissions.Add(new RolePermission { RoleName = "Admin", Permission = Permissions.AdminUsers });
        db.SaveChanges(); // should not throw
    }

    [Fact]
    public void Db_SamePermission_DifferentRole_IsAllowed()
    {
        using var factory = new TestDbContextFactory();
        var db = factory.Context;

        db.RolePermissions.Add(new RolePermission { RoleName = "Admin", Permission = Permissions.AdminAccess });
        db.RolePermissions.Add(new RolePermission { RoleName = "SuperAdmin", Permission = Permissions.AdminAccess });
        db.SaveChanges(); // should not throw
    }
}
