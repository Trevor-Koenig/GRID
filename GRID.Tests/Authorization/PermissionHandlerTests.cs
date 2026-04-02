using FluentAssertions;
using GRID.Authorization;
using GRID.Models;
using GRID.Services;
using GRID.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace GRID.Tests.Authorization;

public class PermissionHandlerTests : IDisposable
{
    private readonly TestApplicationDbContextFactory _factory = new();
    private TestApplicationDbContext Db => _factory.Context;

    public void Dispose() => _factory.Dispose();

    // Reuse the same FakeScopeFactory pattern as PermissionServiceTests
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
                serviceType == typeof(GRID.Data.ApplicationDbContext) ? db : null;
        }
    }

    private PermissionHandler BuildHandler() =>
        new(new PermissionService(new FakeScopeFactory(Db)));

    private static AuthorizationHandlerContext BuildContext(
        PermissionRequirement requirement,
        params string[] roles)
    {
        var claims = roles.Select(r => new Claim(ClaimTypes.Role, r));
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        return new AuthorizationHandlerContext([requirement], principal, null);
    }

    private void Seed(string role, string permission)
    {
        Db.RolePermissions.Add(new RolePermission { RoleName = role, Permission = permission });
        Db.SaveChanges();
    }

    // ── HandleRequirementAsync ────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_WhenUserRoleHasPermission_Succeeds()
    {
        Seed("Admin", "manage-users");
        var requirement = new PermissionRequirement("manage-users");
        var context = BuildContext(requirement, "Admin");

        await ((IAuthorizationHandler)BuildHandler()).HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenUserRoleDoesNotHavePermission_DoesNotSucceed()
    {
        Seed("Admin", "manage-users");
        var requirement = new PermissionRequirement("manage-users");
        var context = BuildContext(requirement, "Moderator");

        await ((IAuthorizationHandler)BuildHandler()).HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_WithNoRoleClaims_DoesNotSucceed()
    {
        Seed("Admin", "manage-users");
        var requirement = new PermissionRequirement("manage-users");
        var context = BuildContext(requirement);  // no roles

        await ((IAuthorizationHandler)BuildHandler()).HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_WhenOneOfMultipleRolesHasPermission_Succeeds()
    {
        Seed("Admin", "manage-users");
        var requirement = new PermissionRequirement("manage-users");
        var context = BuildContext(requirement, "Moderator", "Admin");

        await ((IAuthorizationHandler)BuildHandler()).HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenNeitherRoleHasPermission_DoesNotSucceed()
    {
        Seed("Admin", "manage-users");
        var requirement = new PermissionRequirement("manage-users");
        var context = BuildContext(requirement, "Moderator", "Viewer");

        await ((IAuthorizationHandler)BuildHandler()).HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_WithNoPermissionsInDb_DoesNotSucceed()
    {
        var requirement = new PermissionRequirement("manage-users");
        var context = BuildContext(requirement, "Admin");

        await ((IAuthorizationHandler)BuildHandler()).HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }
}
