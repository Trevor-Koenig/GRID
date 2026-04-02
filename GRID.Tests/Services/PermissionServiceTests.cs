using FluentAssertions;
using GRID.Data;
using GRID.Models;
using GRID.Services;
using GRID.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace GRID.Tests.Services;

public class PermissionServiceTests : IDisposable
{
    private readonly TestApplicationDbContextFactory _factory = new();
    private TestApplicationDbContext Db => _factory.Context;

    public void Dispose() => _factory.Dispose();

    // Minimal IServiceScopeFactory stub that provides the test DbContext
    // as ApplicationDbContext (TestApplicationDbContext IS an ApplicationDbContext).
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

    private PermissionService BuildService() => new(new FakeScopeFactory(Db));

    private void Seed(string role, string permission)
    {
        Db.RolePermissions.Add(new RolePermission { RoleName = role, Permission = permission });
        Db.SaveChanges();
    }

    // ── RoleHasPermissionAsync ────────────────────────────────────────────────

    [Fact]
    public async Task RoleHasPermissionAsync_WhenRoleHasPermission_ReturnsTrue()
    {
        Seed("Admin", "manage-users");
        var service = BuildService();

        var result = await service.RoleHasPermissionAsync("Admin", "manage-users");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task RoleHasPermissionAsync_WhenRoleDoesNotHavePermission_ReturnsFalse()
    {
        Seed("Admin", "manage-users");
        var service = BuildService();

        var result = await service.RoleHasPermissionAsync("Admin", "view-audit-log");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RoleHasPermissionAsync_WhenRoleDoesNotExist_ReturnsFalse()
    {
        var service = BuildService();

        var result = await service.RoleHasPermissionAsync("NonExistentRole", "manage-users");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RoleHasPermissionAsync_IsCaseInsensitiveForRoleName()
    {
        Seed("Admin", "manage-users");
        var service = BuildService();

        var result = await service.RoleHasPermissionAsync("admin", "manage-users");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task RoleHasPermissionAsync_IsCaseInsensitiveForPermission()
    {
        Seed("Admin", "manage-users");
        var service = BuildService();

        var result = await service.RoleHasPermissionAsync("Admin", "MANAGE-USERS");

        result.Should().BeTrue();
    }

    // ── UserHasPermissionAsync ────────────────────────────────────────────────

    [Fact]
    public async Task UserHasPermissionAsync_WhenOneRoleHasPermission_ReturnsTrue()
    {
        Seed("Admin", "manage-users");
        Seed("Moderator", "view-reports");
        var service = BuildService();

        var result = await service.UserHasPermissionAsync(["Moderator", "Admin"], "manage-users");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task UserHasPermissionAsync_WhenNoRoleHasPermission_ReturnsFalse()
    {
        Seed("Admin", "manage-users");
        var service = BuildService();

        var result = await service.UserHasPermissionAsync(["Moderator"], "manage-users");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task UserHasPermissionAsync_WithEmptyRoleList_ReturnsFalse()
    {
        Seed("Admin", "manage-users");
        var service = BuildService();

        var result = await service.UserHasPermissionAsync([], "manage-users");

        result.Should().BeFalse();
    }

    // ── GetPermissionsForRoleAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetPermissionsForRoleAsync_ReturnsAllPermissionsForRole()
    {
        Seed("Admin", "manage-users");
        Seed("Admin", "view-audit-log");
        Seed("Admin", "manage-invites");
        var service = BuildService();

        var perms = await service.GetPermissionsForRoleAsync("Admin");

        perms.Should().BeEquivalentTo(["manage-users", "view-audit-log", "manage-invites"]);
    }

    [Fact]
    public async Task GetPermissionsForRoleAsync_DoesNotReturnPermissionsFromOtherRoles()
    {
        Seed("Admin", "manage-users");
        Seed("Moderator", "view-reports");
        var service = BuildService();

        var perms = await service.GetPermissionsForRoleAsync("Admin");

        perms.Should().NotContain("view-reports");
    }

    [Fact]
    public async Task GetPermissionsForRoleAsync_WhenRoleDoesNotExist_ReturnsEmptySet()
    {
        var service = BuildService();

        var perms = await service.GetPermissionsForRoleAsync("NonExistentRole");

        perms.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPermissionsForRoleAsync_ReturnedSetIsCaseInsensitive()
    {
        Seed("Admin", "manage-users");
        var service = BuildService();

        var perms = await service.GetPermissionsForRoleAsync("Admin");

        perms.Contains("MANAGE-USERS").Should().BeTrue();
    }

    // ── InvalidateCache ───────────────────────────────────────────────────────

    [Fact]
    public async Task InvalidateCache_CausesPermissionsAddedAfterCacheLoadToBeVisible()
    {
        // Load cache with no permissions
        var service = BuildService();
        var before = await service.RoleHasPermissionAsync("Admin", "manage-users");
        before.Should().BeFalse();

        // Add a new permission after the cache was populated
        Seed("Admin", "manage-users");

        // Without invalidation, the cache still returns false
        var cached = await service.RoleHasPermissionAsync("Admin", "manage-users");
        cached.Should().BeFalse();

        // After invalidation, the new permission is visible
        service.InvalidateCache();
        var afterInvalidation = await service.RoleHasPermissionAsync("Admin", "manage-users");
        afterInvalidation.Should().BeTrue();
    }
}
