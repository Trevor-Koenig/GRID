using GRID.Data;
using GRID.Models;
using GRID.Services;
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
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"))
    .AddPolicy("UserOnly", policy => policy.RequireRole("User", "Admin"));

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
        context.Database.Migrate();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Admin", "User" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // Seed default service links if none exist
        if (!context.ServiceLinks.Any())
        {
            context.ServiceLinks.AddRange(
                new ServiceLink { Name = "Jellyfin", Token = "j8kx2m", Url = "https://flix.trevorsystems.com", IconClass = "bi bi-film", Description = "Media server", RequiresAuth = true, IsActive = true, ShowInNav = true, ShowOnHomePage = true, DisplayOrder = 1 },
                new ServiceLink { Name = "Immich", Token = "p4nr9v", Url = "https://photos.trevorsystems.com", IconClass = "bi bi-images", Description = "Photo library", RequiresAuth = true, IsActive = true, ShowInNav = true, ShowOnHomePage = true, DisplayOrder = 2 },
                new ServiceLink { Name = "Mealie", Token = "f2qw5t", Url = "https://food.trevorsystems.com", IconClass = "bi bi-egg-fried", Description = "Recipe manager", RequiresAuth = true, IsActive = true, ShowInNav = true, ShowOnHomePage = true, DisplayOrder = 3 }
            );
            await context.SaveChangesAsync();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred during startup.");
    }
}

app.Run();
