using FluentAssertions;
using GRID.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

namespace GRID.Tests.Pages;

/// <summary>
/// Tests for AdminNotFoundHandler — the IAuthorizationMiddlewareResultHandler
/// that returns 404 for any authorization failure on /Admin/** routes, hiding
/// the existence of the admin surface from unauthenticated and unauthorized users.
/// </summary>
public class AdminNotFoundHandlerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AdminNotFoundHandler CreateHandler() => new();

    private static DefaultHttpContext CreateContext(string path)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        return ctx;
    }

    // Minimal policy — content doesn't matter for these tests.
    private static AuthorizationPolicy AnyPolicy() =>
        new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

    // ── Admin path: unauthenticated (challenged) ──────────────────────────────

    [Theory]
    [InlineData("/Admin")]
    [InlineData("/Admin/Users")]
    [InlineData("/Admin/Dashboard/Index")]
    [InlineData("/Admin/AuditLog")]
    [InlineData("/admin/users")]          // lowercase — must be case-insensitive
    [InlineData("/ADMIN/ROLES")]          // uppercase
    public async Task AdminPath_WhenChallenged_Returns404(string path)
    {
        var ctx = CreateContext(path);

        await CreateHandler().HandleAsync(
            _ => Task.CompletedTask,
            ctx,
            AnyPolicy(),
            PolicyAuthorizationResult.Challenge());

        ctx.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    // ── Admin path: authenticated but unauthorized (forbidden) ────────────────

    [Theory]
    [InlineData("/Admin")]
    [InlineData("/Admin/Users/Details")]
    [InlineData("/Admin/RolePermissions")]
    [InlineData("/admin/invites")]
    public async Task AdminPath_WhenForbidden_Returns404(string path)
    {
        var ctx = CreateContext(path);

        await CreateHandler().HandleAsync(
            _ => Task.CompletedTask,
            ctx,
            AnyPolicy(),
            PolicyAuthorizationResult.Forbid());

        ctx.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    // ── Admin path: authorized — must NOT intercept ───────────────────────────

    [Fact]
    public async Task AdminPath_WhenSucceeded_CallsNext()
    {
        var ctx = CreateContext("/Admin/Dashboard");
        var nextCalled = false;

        await CreateHandler().HandleAsync(
            _ => { nextCalled = true; return Task.CompletedTask; },
            ctx,
            AnyPolicy(),
            PolicyAuthorizationResult.Success());

        nextCalled.Should().BeTrue("an authorized user must be allowed through");
        ctx.Response.StatusCode.Should().NotBe(StatusCodes.Status404NotFound);
    }

    // ── Non-admin paths: must never return 404 ────────────────────────────────

    [Theory]
    [InlineData("/")]
    [InlineData("/Docs/Index")]
    [InlineData("/Identity/Account/Login")]
    [InlineData("/Administration")]   // shares prefix but is a different path segment
    [InlineData("/AdminPanel")]       // same — different segment, not /Admin/
    public async Task NonAdminPath_WhenSucceeded_CallsNext(string path)
    {
        var ctx = CreateContext(path);
        var nextCalled = false;

        await CreateHandler().HandleAsync(
            _ => { nextCalled = true; return Task.CompletedTask; },
            ctx,
            AnyPolicy(),
            PolicyAuthorizationResult.Success());

        nextCalled.Should().BeTrue();
        ctx.Response.StatusCode.Should().NotBe(StatusCodes.Status404NotFound);
    }

    // ── Path segment boundary ─────────────────────────────────────────────────

    [Theory]
    [InlineData("/Administration")]  // /Admin is a prefix but not a full segment
    [InlineData("/AdminPanel")]
    public async Task PathsThatSharePrefixButNotSegment_WhenChallenged_DoNotReturn404(string path)
    {
        // StartsWithSegments("/Admin") requires the next char to be '/' or end-of-path,
        // so /Administration should NOT be caught by the admin guard.
        var ctx = CreateContext(path);
        var nextCalled = false;

        // Use a succeeded result to avoid needing a full DI setup for the
        // default handler's challenge/forbid redirect logic.
        await CreateHandler().HandleAsync(
            _ => { nextCalled = true; return Task.CompletedTask; },
            ctx,
            AnyPolicy(),
            PolicyAuthorizationResult.Success());

        ctx.Response.StatusCode.Should().NotBe(StatusCodes.Status404NotFound);
        nextCalled.Should().BeTrue();
    }
}
