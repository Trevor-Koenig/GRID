using GRID.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace GRID.Services
{
    public class ContactRequestReminderService(IServiceScopeFactory scopeFactory, ILogger<ContactRequestReminderService> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = TimeUntilNextSunday7am();
                logger.LogInformation("Contact request reminder scheduled in {Hours:F1} hours.", delay.TotalHours);

                await Task.Delay(delay, stoppingToken);

                if (stoppingToken.IsCancellationRequested) break;

                await SendReminderAsync(stoppingToken);
            }
        }

        private static TimeSpan TimeUntilNextSunday7am()
        {
            var now = DateTime.Now;
            var daysUntilSunday = ((int)DayOfWeek.Sunday - (int)now.DayOfWeek + 7) % 7;
            var nextSunday = now.Date.AddDays(daysUntilSunday == 0 ? 7 : daysUntilSunday).AddHours(7);
            return nextSunday - now;
        }

        private async Task SendReminderAsync(CancellationToken stoppingToken)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

            try
            {
                var unread = await db.ContactRequests
                    .Where(r => !r.IsResponded)
                    .OrderByDescending(r => r.SubmittedAt)
                    .ToListAsync(stoppingToken);

                if (!unread.Any())
                {
                    logger.LogInformation("No unread contact requests. Skipping reminder email.");
                    return;
                }

                var admins = await userManager.GetUsersInRoleAsync("Admin");

                var rows = string.Concat(unread.Select(r =>
                {
                    var preview = r.Message.Length > 100 ? r.Message[..100] + "…" : r.Message;
                    return $"""
                        <tr>
                            <td style="padding:8px 12px;border-bottom:1px solid #eee;">{System.Net.WebUtility.HtmlEncode(r.Name)}</td>
                            <td style="padding:8px 12px;border-bottom:1px solid #eee;">{System.Net.WebUtility.HtmlEncode(r.Email)}</td>
                            <td style="padding:8px 12px;border-bottom:1px solid #eee;">{System.Net.WebUtility.HtmlEncode(r.Subject)}</td>
                            <td style="padding:8px 12px;border-bottom:1px solid #eee;color:#555;">{System.Net.WebUtility.HtmlEncode(preview)}</td>
                        </tr>
                        """;
                }));

                var html = $"""
                    <div style="font-family:sans-serif;max-width:700px;margin:0 auto;">
                        <h2 style="color:#333;">GRID — Unread Contact Requests</h2>
                        <p>You have <strong>{unread.Count}</strong> unread contact request{(unread.Count == 1 ? "" : "s")} awaiting a response.</p>
                        <table style="width:100%;border-collapse:collapse;font-size:14px;">
                            <thead>
                                <tr style="background:#f5f5f5;text-align:left;">
                                    <th style="padding:8px 12px;">Name</th>
                                    <th style="padding:8px 12px;">Email</th>
                                    <th style="padding:8px 12px;">Subject</th>
                                    <th style="padding:8px 12px;">Message preview</th>
                                </tr>
                            </thead>
                            <tbody>
                                {rows}
                            </tbody>
                        </table>
                        <p style="margin-top:24px;font-size:12px;color:#999;">
                            This is an automated weekly reminder from GRID sent every Sunday at 7 AM.
                        </p>
                    </div>
                    """;

                foreach (var admin in admins)
                {
                    if (string.IsNullOrEmpty(admin.Email)) continue;
                    await emailSender.SendEmailAsync(admin.Email, $"GRID — {unread.Count} unread contact request{(unread.Count == 1 ? "" : "s")}", html);
                    logger.LogInformation("Reminder sent to {Email}.", admin.Email);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send contact request reminder.");
            }
        }
    }
}
