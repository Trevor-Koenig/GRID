using GRID.Authorization;
using GRID.Data;
using GRID.Models;
using GRID.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

/***********************************
 *
 * DEFAULT/PRE-BUILT SERVICES
 *
 **********************************/
builder.Configuration.AddEnvironmentVariables();
var connectionString = builder.Configuration["ConnectionStrings:DefaultConnection"] ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

var dpBuilder = builder.Services.AddDataProtection()
    .SetApplicationName("GRID");

if (!builder.Environment.IsDevelopment())
{
    var keyPath = builder.Configuration["DataProtection:KeyPath"] ?? "/app/keys";
    dpBuilder.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
}
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = !builder.Environment.IsDevelopment();
    options.User.RequireUniqueEmail = true;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
    options.Conventions.AuthorizeFolder("/Admin/Users", "ManageUsers");
    options.Conventions.AuthorizeFolder("/Admin/Roles", "ManageRoles");
    options.Conventions.AuthorizeFolder("/Admin/RolePermissions", "ManageRoles");
    options.Conventions.AuthorizeFolder("/Admin/Invites", "ManageInvites");
    options.Conventions.AuthorizeFolder("/Admin/ContactRequests", "ManageContacts");
    options.Conventions.AuthorizeFolder("/Admin/Services", "ManageServices");
    options.Conventions.AuthorizeFolder("/Admin/AuditLog", "ViewAuditLog");
    options.Conventions.AuthorizeFolder("/Admin/Docs", "ManageDocs");
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.AddRequirements(new PermissionRequirement(Permissions.AdminAccess)))
    .AddPolicy("UserOnly", policy => policy.AddRequirements(new PermissionRequirement(Permissions.ServicesUse)))
    .AddPolicy("ManageUsers", policy => policy.AddRequirements(new PermissionRequirement(Permissions.AdminUsers)))
    .AddPolicy("ManageRoles", policy => policy.AddRequirements(new PermissionRequirement(Permissions.AdminRoles)))
    .AddPolicy("ManageInvites", policy => policy.AddRequirements(new PermissionRequirement(Permissions.AdminInvites)))
    .AddPolicy("ManageContacts", policy => policy.AddRequirements(new PermissionRequirement(Permissions.AdminContacts)))
    .AddPolicy("ManageServices", policy => policy.AddRequirements(new PermissionRequirement(Permissions.AdminServices)))
    .AddPolicy("ViewAuditLog", policy => policy.AddRequirements(new PermissionRequirement(Permissions.AdminAuditLog)))
    .AddPolicy("ManageDocs", policy => policy.AddRequirements(new PermissionRequirement(Permissions.AdminDocs)))
    .AddPolicy("CanViewPrivateDocs", policy => policy.AddRequirements(new PermissionRequirement(Permissions.DocsView)));

/***********************************
 * 
 * CUSTOM SERVICES
 * 
 **********************************/
// email service (through Mailgun)
if (!builder.Environment.IsDevelopment())
{
    var apiKey = builder.Configuration["Mailgun:ApiKey"];
    if (string.IsNullOrWhiteSpace(apiKey))
        throw new InvalidOperationException("Mailgun API key not configured! Check Docker environment variables.");
}
builder.Services.AddHttpClient();
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddTransient<IEmailSender, DevEmailSender>();
    builder.Services.AddTransient<IExtendedEmailSender, DevEmailSender>();
}
else
{
    builder.Services.AddTransient<IEmailSender, MailgunApiEmailSender>();
    builder.Services.AddTransient<IExtendedEmailSender, MailgunApiEmailSender>();
}

// invite code service
builder.Services.AddScoped<InviteService>();

// audit service
builder.Services.AddScoped<AuditService>();

// permission service
builder.Services.AddSingleton<PermissionService>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();

// service status checker (singleton background service)
builder.Services.AddSingleton<ServiceStatusService>();
builder.Services.AddSingleton<IServiceStatusService>(p => p.GetRequiredService<ServiceStatusService>());
builder.Services.AddHostedService(p => p.GetRequiredService<ServiceStatusService>());

// weekly contact request reminder
builder.Services.AddHostedService<ContactRequestReminderService>();


/***********************************
 * 
 * RATE LIMITING
 * 
 ***********************************/

builder.Services.AddRateLimiter(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        // Disable all rate limiting in development so it never gets in the way.
        options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(
            _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("dev"));

        foreach (var policy in new[] { "InviteLimiter", "LoginLimiter", "ContactLimiter", "ApiLimiter" })
            options.AddPolicy(policy, _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("dev"));

        return;
    }

    options.RejectionStatusCode = 429;
    options.OnRejected = async (ctx, token) =>
    {
        ctx.HttpContext.Response.StatusCode = 429;
        await ctx.HttpContext.Response.WriteAsync("Too many requests. Please try again later.", token);
    };

    // Global safety net: 300 req/min per IP
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Invite redemption: 60/min per IP
    options.AddFixedWindowLimiter("InviteLimiter", opt =>
    {
        opt.PermitLimit = 60;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;
    });

    // Login: 10 attempts per 5 minutes per IP
    options.AddPolicy("LoginLimiter", ctx =>
        System.Threading.RateLimiting.RateLimitPartition.GetSlidingWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new System.Threading.RateLimiting.SlidingWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                SegmentsPerWindow = 5,
                QueueLimit = 0
            }));

    // Contact form: 5 submissions per 10 minutes per IP
    options.AddPolicy("ContactLimiter", ctx =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0
            }));

    // API endpoints: 60 req/min per IP
    options.AddPolicy("ApiLimiter", ctx =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});


/***********************************
 *
 * FORWARDED HEADERS (reverse proxy / Docker)
 *
 **********************************/
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Clear both lists so any upstream proxy (Docker bridge, reverse proxy) is trusted
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

/***********************************
 *
 * BUILD APP
 *
 **********************************/
var app = builder.Build();

// Must be first — rewrites RemoteIpAddress from X-Forwarded-For before any other middleware reads it
app.UseForwardedHeaders();

// Security headers on every response
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Frame-Options"] = "DENY";
    headers["X-Content-Type-Options"] = "nosniff";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["X-XSS-Protection"] = "0";
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self' data:; " +
        "connect-src 'self'; " +
        "frame-ancestors 'none'; " +
        "form-action 'self'; " +
        "base-uri 'self';";
    await next();
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthorization();

app.UseMiddleware<GRID.Middleware.AdminTwoFactorMiddleware>();
app.UseMiddleware<GRID.Middleware.PageViewTrackingMiddleware>();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

// Theme preference API
app.MapPost("/api/theme", async (HttpContext ctx, ApplicationDbContext db, string theme) =>
{
    if (theme != "dark" && theme != "light") return Results.BadRequest();
    var userId = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (userId == null) return Results.Unauthorized();
    var profile = await db.UserProfiles.FindAsync(userId);
    if (profile == null) return Results.NotFound();
    profile.Theme = theme;
    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization().RequireRateLimiting("ApiLimiter");

// Page duration tracking API
app.MapPost("/api/page-duration", async (HttpContext ctx, ApplicationDbContext db, Stream body) =>
{
    // Validate Origin header to prevent cross-site beacon abuse
    if (ctx.Request.Headers.TryGetValue("Origin", out var origin))
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri) ||
            !originUri.Host.Equals(ctx.Request.Host.Host, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Forbid();
        }
    }

    using var reader = new System.IO.StreamReader(body);
    var json = await reader.ReadToEndAsync();
    var data = System.Text.Json.JsonSerializer.Deserialize<PageDurationRequest>(json,
        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    if (data == null || data.Duration <= 0 || string.IsNullOrWhiteSpace(data.Path))
        return Results.BadRequest();

    var userId = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    var entityId = "GET " + data.Path;
    var cutoff = DateTime.UtcNow.AddHours(-1);

    var log = await db.AuditLogs
        .Where(l => l.Action == "PageView"
               && l.EntityId == entityId
               && l.DurationSeconds == null
               && l.Timestamp >= cutoff
               && l.ActorId == userId)
        .OrderByDescending(l => l.Timestamp)
        .FirstOrDefaultAsync();

    if (log != null)
    {
        log.DurationSeconds = Math.Min(data.Duration, 86400);
        await db.SaveChangesAsync();
    }

    return Results.Ok();
}).RequireRateLimiting("ApiLimiter");

// Apply migrations and seed roles at startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        // Retry loop so the app waits for Postgres to be ready on first deploy
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                var pending = context.Database.GetPendingMigrations().ToList();
                var applied = context.Database.GetAppliedMigrations().ToList();
                logger.LogInformation("Applied migrations: {Applied}", string.Join(", ", applied));
                logger.LogInformation("Pending migrations: {Pending}", string.Join(", ", pending));

                context.Database.Migrate();
                logger.LogInformation("Migrations complete.");
                break;
            }
            catch (Exception ex) when (attempt < 10)
            {
                logger.LogWarning(ex, "Migration attempt {Attempt} failed, retrying in 3 s...", attempt);
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Admin", "User" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // Seed default role permissions if none exist
        if (!context.RolePermissions.Any())
        {
            var defaultPerms = new List<RolePermission>();
            // Admin gets everything
            foreach (var perm in Permissions.All)
                defaultPerms.Add(new RolePermission { RoleName = "Admin", Permission = perm });
            // User gets service access and public+private doc viewing
            defaultPerms.Add(new RolePermission { RoleName = "User", Permission = Permissions.ServicesUse });
            defaultPerms.Add(new RolePermission { RoleName = "User", Permission = Permissions.DocsView });
            context.RolePermissions.AddRange(defaultPerms);
            await context.SaveChangesAsync();
        }

        // Seed default service links if none exist
        if (!context.ServiceLinks.Any())
        {
            context.ServiceLinks.AddRange(
                new ServiceLink { Name = "Jellyfin", Token = "j8kx2m", Url = "https://flix.trevorsystems.com", IconClass = "bi bi-film", Description = "Media server", RequiresAuth = true, IsActive = true, ShowInNav = true, ShowInHero = true, ShowInServices = true, DisplayOrder = 1 },
                new ServiceLink { Name = "Immich", Token = "p4nr9v", Url = "https://photos.trevorsystems.com", IconClass = "bi bi-images", Description = "Photo library", RequiresAuth = true, IsActive = true, ShowInNav = true, ShowInHero = true, ShowInServices = true, DisplayOrder = 2 },
                new ServiceLink { Name = "Mealie", Token = "f2qw5t", Url = "https://food.trevorsystems.com", IconClass = "bi bi-egg-fried", Description = "Recipe manager", RequiresAuth = true, IsActive = true, ShowInNav = true, ShowInHero = false, ShowInServices = true, DisplayOrder = 3 },
                new ServiceLink { Name = "AMP", Token = "xn4a8p", Url = "https://amp.shulker.tech", IconClass = "bi bi-music-note-beamed", Description = "Music streaming", RequiresAuth = true, IsActive = true, ShowInNav = true, ShowInHero = true, ShowInServices = true, DisplayOrder = 4 }
            );
            await context.SaveChangesAsync();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogCritical(ex, "A startup error occurred. The application may not function correctly.");
        throw; // surface the error so the container exits instead of running broken
    }
}

app.Run();

record PageDurationRequest(string Path, int Duration);
