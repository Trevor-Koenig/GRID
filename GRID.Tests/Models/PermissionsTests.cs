using FluentAssertions;
using GRID.Models;

namespace GRID.Tests.Models;

public class PermissionsTests
{
    // ── Constant values ───────────────────────────────────────────────────────

    [Fact]
    public void AdminAccess_HasExpectedValue()   => Permissions.AdminAccess.Should().Be("admin.access");
    [Fact]
    public void AdminUsers_HasExpectedValue()    => Permissions.AdminUsers.Should().Be("admin.users");
    [Fact]
    public void AdminRoles_HasExpectedValue()    => Permissions.AdminRoles.Should().Be("admin.roles");
    [Fact]
    public void AdminInvites_HasExpectedValue()  => Permissions.AdminInvites.Should().Be("admin.invites");
    [Fact]
    public void AdminContacts_HasExpectedValue() => Permissions.AdminContacts.Should().Be("admin.contacts");
    [Fact]
    public void AdminServices_HasExpectedValue() => Permissions.AdminServices.Should().Be("admin.services");
    [Fact]
    public void AdminAuditLog_HasExpectedValue() => Permissions.AdminAuditLog.Should().Be("admin.auditlog");
    [Fact]
    public void ServicesUse_HasExpectedValue()   => Permissions.ServicesUse.Should().Be("services.use");
    [Fact]
    public void DocsView_HasExpectedValue()      => Permissions.DocsView.Should().Be("docs.view");
    [Fact]
    public void AdminDocs_HasExpectedValue()     => Permissions.AdminDocs.Should().Be("admin.docs");

    // ── All array ─────────────────────────────────────────────────────────────

    [Fact]
    public void All_ContainsExactlyTenPermissions()
    {
        Permissions.All.Should().HaveCount(10);
    }

    [Fact]
    public void All_HasNoDuplicates()
    {
        Permissions.All.Distinct().Should().HaveSameCount(Permissions.All);
    }

    [Theory]
    [InlineData(Permissions.AdminAccess)]
    [InlineData(Permissions.AdminUsers)]
    [InlineData(Permissions.AdminRoles)]
    [InlineData(Permissions.AdminInvites)]
    [InlineData(Permissions.AdminContacts)]
    [InlineData(Permissions.AdminServices)]
    [InlineData(Permissions.AdminAuditLog)]
    [InlineData(Permissions.ServicesUse)]
    [InlineData(Permissions.DocsView)]
    [InlineData(Permissions.AdminDocs)]
    public void All_ContainsPermission(string permission)
    {
        Permissions.All.Should().Contain(permission);
    }

    // ── Labels dictionary ─────────────────────────────────────────────────────

    [Fact]
    public void Labels_HasEntryForEveryPermissionInAll()
    {
        foreach (var permission in Permissions.All)
        {
            Permissions.Labels.Should().ContainKey(permission,
                because: $"Labels is missing an entry for \"{permission}\"");
        }
    }

    [Fact]
    public void Labels_HasNoExtraEntries()
    {
        foreach (var key in Permissions.Labels.Keys)
        {
            Permissions.All.Should().Contain(key,
                because: $"Labels has an entry \"{key}\" that is not in All");
        }
    }

    [Fact]
    public void Labels_NoValuesAreNullOrEmpty()
    {
        foreach (var (key, label) in Permissions.Labels)
        {
            label.Should().NotBeNullOrWhiteSpace(
                because: $"Label for \"{key}\" should not be blank");
        }
    }

    [Fact]
    public void Labels_CountMatchesAllCount()
    {
        Permissions.Labels.Should().HaveCount(Permissions.All.Length);
    }
}
