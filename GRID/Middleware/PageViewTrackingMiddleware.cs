using GRID.Data;
using GRID.Models;
using System.Security.Claims;

namespace GRID.Middleware
{
    public class PageViewTrackingMiddleware(RequestDelegate next)
    {
        private static readonly HashSet<string> _skipExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".css", ".js", ".png", ".jpg", ".jpeg", ".gif", ".ico", ".svg",
            ".woff", ".woff2", ".ttf", ".eot", ".map", ".webp", ".json"
        };

        public async Task InvokeAsync(HttpContext context, IServiceScopeFactory scopeFactory)
        {
            await next(context);

            // Skip logging for status-code re-executions (e.g. /NotFound rendered by
            // UseStatusCodePagesWithReExecute). The original request is either already
            // logged above or is an admin 404 that intentionally produces no log entry.
            if (context.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodeReExecuteFeature>() != null)
                return;

            var path = context.Request.Path.Value ?? "";

            // Skip static assets
            var ext = Path.GetExtension(path);
            if (!string.IsNullOrEmpty(ext) && _skipExtensions.Contains(ext))
                return;

            // Skip API endpoints and health checks
            if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                return;

            // Skip antiforgery / identity infrastructure paths
            if (path.StartsWith("/_", StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userEmail = context.User.Identity?.IsAuthenticated == true
                    ? context.User.Identity.Name
                    : null;
                var rawIp = context.Connection.RemoteIpAddress;
                var ip = rawIp?.IsIPv4MappedToIPv6 == true
                    ? rawIp.MapToIPv4().ToString()
                    : rawIp?.ToString();
                var status = context.Response.StatusCode;

                db.AuditLogs.Add(new AuditLog
                {
                    Action = "PageView",
                    ActorId = userId,
                    ActorEmail = userEmail,
                    EntityType = "Page",
                    EntityId = $"{context.Request.Method} {path}",
                    IpAddress = ip,
                    HttpStatus = status,
                    Timestamp = DateTime.UtcNow
                });

                await db.SaveChangesAsync();
            }
            catch
            {
                // Never let tracking errors break a request
            }
        }
    }
}
