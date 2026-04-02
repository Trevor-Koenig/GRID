using FluentAssertions;
using GRID.Areas.Identity.Pages.Account;
using GRID.Tests.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GRID.Tests.Pages;

/// <summary>
/// Unit tests for LoginModel.OnPostAsync — focused on the unconfirmed-email
/// branch, the audit log entries it produces, and the contrast cases
/// (wrong password, email not found, success).
/// </summary>
public class LoginModelTests : IDisposable
{
    private readonly TestApplicationDbContextFactory _factory = new();
    private TestApplicationDbContext Db => _factory.Context;

    public void Dispose() => _factory.Dispose();

    // ── Stubs ─────────────────────────────────────────────────────────────────

    /// <summary>Minimal IUserStore — all writes succeed, all reads return null.</summary>
    private sealed class StubUserStore : IUserStore<IdentityUser>
    {
        public Task<IdentityResult> CreateAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
        public void Dispose() { }
        public Task<IdentityUser?> FindByIdAsync(string userId, CancellationToken ct) => Task.FromResult<IdentityUser?>(null);
        public Task<IdentityUser?> FindByNameAsync(string normalizedUserName, CancellationToken ct) => Task.FromResult<IdentityUser?>(null);
        public Task<string?> GetNormalizedUserNameAsync(IdentityUser user, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<string> GetUserIdAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(user.Id);
        public Task<string?> GetUserNameAsync(IdentityUser user, CancellationToken ct) => Task.FromResult<string?>(user.UserName);
        public Task SetNormalizedUserNameAsync(IdentityUser user, string? normalizedName, CancellationToken ct) => Task.CompletedTask;
        public Task SetUserNameAsync(IdentityUser user, string? userName, CancellationToken ct) => Task.CompletedTask;
        public Task<IdentityResult> UpdateAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
    }

    /// <summary>
    /// UserManager that returns a fixed user from FindByEmailAsync and a
    /// configurable result from IsEmailConfirmedAsync.
    /// </summary>
    private sealed class StubUserManager : UserManager<IdentityUser>
    {
        private readonly IdentityUser? _user;
        private readonly bool _emailConfirmed;

        public StubUserManager(IdentityUser? user, bool emailConfirmed)
            : base(
                new StubUserStore(),
                Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
                new PasswordHasher<IdentityUser>(),
                [],
                [],
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                null!,
                NullLogger<UserManager<IdentityUser>>.Instance)
        {
            _user = user;
            _emailConfirmed = emailConfirmed;
        }

        public override Task<IdentityUser?> FindByEmailAsync(string email)
            => Task.FromResult(_user);

        public override Task<bool> IsEmailConfirmedAsync(IdentityUser user)
            => Task.FromResult(_emailConfirmed);
    }

    /// <summary>
    /// SignInManager that returns a fixed SignInResult from PasswordSignInAsync
    /// and an empty list from GetExternalAuthenticationSchemesAsync.
    /// </summary>
    private sealed class StubSignInManager : SignInManager<IdentityUser>
    {
        private readonly IdentitySignInResult _result;

        public StubSignInManager(UserManager<IdentityUser> userManager, IdentitySignInResult result)
            : base(
                userManager,
                new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
                new UserClaimsPrincipalFactory<IdentityUser>(
                    userManager, Microsoft.Extensions.Options.Options.Create(new IdentityOptions())),
                Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
                NullLogger<SignInManager<IdentityUser>>.Instance,
                new AuthenticationSchemeProvider(Microsoft.Extensions.Options.Options.Create(new AuthenticationOptions())),
                new DefaultUserConfirmation<IdentityUser>())
        {
            _result = result;
        }

        public override Task<IdentitySignInResult> PasswordSignInAsync(
            string userName, string password, bool isPersistent, bool lockoutOnFailure)
            => Task.FromResult(_result);

        public override Task<IEnumerable<AuthenticationScheme>> GetExternalAuthenticationSchemesAsync()
            => Task.FromResult(Enumerable.Empty<AuthenticationScheme>());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private LoginModel BuildModel(IdentityUser? user, bool emailConfirmed, IdentitySignInResult signInResult)
    {
        var userManager = new StubUserManager(user, emailConfirmed);
        var signInManager = new StubSignInManager(userManager, signInResult);

        var model = new LoginModel(signInManager, userManager, Db, NullLogger<LoginModel>.Instance)
        {
            Input = new LoginModel.InputModel
            {
                Email = user?.Email ?? "missing@example.com",
                Password = "P@ssw0rd!"
            },
            PageContext = new PageContext(new ActionContext(
                new DefaultHttpContext(),
                new RouteData(),
                new PageActionDescriptor()))
        };

        return model;
    }

    // ── Email not confirmed ───────────────────────────────────────────────────

    [Fact]
    public async Task OnPost_EmailNotConfirmed_AuditLogsEmailNotConfirmed()
    {
        var user = new IdentityUser { Id = "u1", Email = "user@example.com" };
        var model = BuildModel(user, emailConfirmed: false, IdentitySignInResult.NotAllowed);

        await model.OnPostAsync("/");

        Db.AuditLogs.Should().ContainSingle(l =>
            l.Action == "FailedLogin" && l.Details == "Email not confirmed");
    }

    [Fact]
    public async Task OnPost_EmailNotConfirmed_DoesNotLogWrongPassword()
    {
        var user = new IdentityUser { Id = "u1", Email = "user@example.com" };
        var model = BuildModel(user, emailConfirmed: false, IdentitySignInResult.NotAllowed);

        await model.OnPostAsync("/");

        Db.AuditLogs.Should().NotContain(l => l.Details == "Wrong password");
    }

    [Fact]
    public async Task OnPost_EmailNotConfirmed_AddsConfirmEmailModelError()
    {
        var user = new IdentityUser { Id = "u1", Email = "user@example.com" };
        var model = BuildModel(user, emailConfirmed: false, IdentitySignInResult.NotAllowed);

        await model.OnPostAsync("/");

        model.ModelState[string.Empty]!.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Contain("confirm your email");
    }

    [Fact]
    public async Task OnPost_EmailNotConfirmed_ReturnsPageResult()
    {
        var user = new IdentityUser { Id = "u1", Email = "user@example.com" };
        var model = BuildModel(user, emailConfirmed: false, IdentitySignInResult.NotAllowed);

        var result = await model.OnPostAsync("/");

        result.Should().BeOfType<PageResult>();
    }

    [Fact]
    public async Task OnPost_EmailNotConfirmed_AuditLogRecordsEmail()
    {
        var user = new IdentityUser { Id = "u1", Email = "user@example.com" };
        var model = BuildModel(user, emailConfirmed: false, IdentitySignInResult.NotAllowed);

        await model.OnPostAsync("/");

        Db.AuditLogs.Should().ContainSingle(l =>
            l.ActorEmail == "user@example.com");
    }

    // ── IsNotAllowed but email IS confirmed (edge case) ───────────────────────

    [Fact]
    public async Task OnPost_NotAllowedButEmailConfirmed_FallsThroughToWrongPassword()
    {
        // If IsNotAllowed fires for a reason other than email confirmation
        // (e.g. a phone-number requirement), the generic "Wrong password" branch
        // should be used — not the email-not-confirmed message.
        var user = new IdentityUser { Id = "u1", Email = "user@example.com" };
        var model = BuildModel(user, emailConfirmed: true, IdentitySignInResult.NotAllowed);

        await model.OnPostAsync("/");

        Db.AuditLogs.Should().ContainSingle(l =>
            l.Action == "FailedLogin" && l.Details == "Wrong password");
    }

    [Fact]
    public async Task OnPost_NotAllowedButEmailConfirmed_DoesNotShowConfirmEmailMessage()
    {
        var user = new IdentityUser { Id = "u1", Email = "user@example.com" };
        var model = BuildModel(user, emailConfirmed: true, IdentitySignInResult.NotAllowed);

        await model.OnPostAsync("/");

        model.ModelState[string.Empty]!.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().NotContain("confirm your email");
    }

    // ── Wrong password (baseline comparison) ─────────────────────────────────

    [Fact]
    public async Task OnPost_WrongPassword_AuditLogsWrongPassword()
    {
        var user = new IdentityUser { Id = "u1", Email = "user@example.com" };
        var model = BuildModel(user, emailConfirmed: true, IdentitySignInResult.Failed);

        await model.OnPostAsync("/");

        Db.AuditLogs.Should().ContainSingle(l =>
            l.Action == "FailedLogin" && l.Details == "Wrong password");
    }

    [Fact]
    public async Task OnPost_WrongPassword_AddsGenericModelError()
    {
        var user = new IdentityUser { Id = "u1", Email = "user@example.com" };
        var model = BuildModel(user, emailConfirmed: true, IdentitySignInResult.Failed);

        await model.OnPostAsync("/");

        model.ModelState[string.Empty]!.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Invalid login attempt.");
    }

    // ── Email not found ───────────────────────────────────────────────────────

    [Fact]
    public async Task OnPost_EmailNotFound_AuditLogsEmailNotFound()
    {
        // user == null → FindByEmailAsync returned null
        var model = BuildModel(user: null, emailConfirmed: false, IdentitySignInResult.Failed);

        await model.OnPostAsync("/");

        Db.AuditLogs.Should().ContainSingle(l =>
            l.Action == "FailedLogin" && l.Details == "Email not found");
    }

    // ── Success ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task OnPost_Success_DoesNotAddFailedLoginAuditEntry()
    {
        var user = new IdentityUser { Id = "u1", Email = "user@example.com" };
        var model = BuildModel(user, emailConfirmed: true, IdentitySignInResult.Success);

        await model.OnPostAsync("/");

        Db.AuditLogs.Should().NotContain(l => l.Action == "FailedLogin");
    }
}
