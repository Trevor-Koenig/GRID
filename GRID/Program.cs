using GRID.Data;
using GRID.Services;
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
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
    options.User.RequireUniqueEmail = true;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"));

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

// Apply migrations at startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        // Log any errors during migration
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}

app.Run();
