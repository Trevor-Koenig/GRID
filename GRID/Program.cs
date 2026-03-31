using GRID.Authorization;
using GRID.Data;
using GRID.Models;
using GRID.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
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

var keyPath = builder.Configuration["DataProtection:KeyPath"] ?? "/app/keys";
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
    .SetApplicationName("GRID");
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
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.AddRequirements(new PermissionRequirement(Permissions.AdminAccess)))
    .AddPolicy("UserOnly", policy => policy.AddRequirements(new PermissionRequirement(Permissions.ServicesUse)))
    .AddPolicy("ManageUsers", policy => policy.AddRequirements(new PermissionRequirement(Permissions.AdminUsers)))
    .AddPolicy("ManageRoles", policy => policy.AddRequirements(new PermissionRequirement(Permissions.AdminRoles)))
    .AddPolicy("ManageInvites", policy => policy.AddRequirements(new PermissionRequirement(Permissions.AdminInvites)))
    .AddPolicy("ManageContacts", policy => policy.AddRequirements(new PermissionRequirement(Permissions.AdminContacts)))
    .AddPolicy("ManageServices", policy => policy.AddRequirements(new PermissionRequirement(Permissions.AdminServices)))
    .AddPolicy("ViewAuditLog", policy => policy.AddRequirements(new PermissionRequirement(Permissions.AdminAuditLog)));

/***********************************
 * 
 * CUSTOM SERVICES
 * 
 **********************************/
// email service (through Mailgun)
var apiKey = builder.Configuration["Mailgun:ApiKey"];
if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("Mailgun API key not configured! Check Docker environment variables.");
}
builder.Services.AddHttpClient();
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddTransient<IEmailSender, DevEmailSender>();
}
else
{
    builder.Services.AddTransient<IEmailSender, MailgunApiEmailSender>();
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
    options.AddFixedWindowLimiter("InviteLimiter", opt =>
    {
        opt.PermitLimit = 10;             // max 10 requests
        opt.Window = TimeSpan.FromMinutes(1);  // per minute
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;               // invoke small queue so that they can not have too many concurrent attempts
    });
});


/***********************************
 * 
 * BUILD APP
 * 
 **********************************/
var app = builder.Build();

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

app.UseAuthorization();

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
}).RequireAuthorization();

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
            // User gets service access
            defaultPerms.Add(new RolePermission { RoleName = "User", Permission = Permissions.ServicesUse });
            context.RolePermissions.AddRange(defaultPerms);
            await context.SaveChangesAsync();
        }

        // Seed default service links if none exist
        if (!context.ServiceLinks.Any())
        {
            context.ServiceLinks.AddRange(
                new ServiceLink { Name = "Jellyfin", Token = "j8kx2m", Url = "https://flix.trevorsystems.com", IconClass = "bi bi-film", Description = "Media server", RequiresAuth = true, IsActive = true, ShowInNav = true, ShowOnHomePage = true, DisplayOrder = 1 },
                new ServiceLink { Name = "Immich", Token = "p4nr9v", Url = "https://photos.trevorsystems.com", IconClass = "bi bi-images", Description = "Photo library", RequiresAuth = true, IsActive = true, ShowInNav = true, ShowOnHomePage = true, DisplayOrder = 2 },
                new ServiceLink { Name = "Mealie", Token = "f2qw5t", Url = "https://food.trevorsystems.com", IconClass = "bi bi-egg-fried", Description = "Recipe manager", RequiresAuth = true, IsActive = true, ShowInNav = true, ShowOnHomePage = true, DisplayOrder = 3 },
                new ServiceLink { Name = "AMP", Token = "xn4a8p", Url = "https://amp.shulker.tech", IconClass = "bi bi-music-note-beamed", Description = "Music streaming", RequiresAuth = true, IsActive = true, ShowInNav = true, ShowOnHomePage = true, DisplayOrder = 4 }
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
