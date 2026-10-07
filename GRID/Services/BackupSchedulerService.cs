using GRID.Data;

namespace GRID.Services
{
    /// <summary>
    /// Takes an automatic backup every Backup:IntervalHours (default 24, 0 disables) and keeps
    /// the newest Backup:KeepAutomatic (default 14) of them. Manual, uploaded, imported and
    /// pre-restore backups are never pruned. Nothing is backed up until an account exists.
    /// </summary>
    public class BackupSchedulerService(
        BackupService backupService,
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<BackupSchedulerService> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var intervalHours = config.GetValue("Backup:IntervalHours", 24.0);
            var keep = config.GetValue("Backup:KeepAutomatic", 14);
            if (intervalHours <= 0)
            {
                logger.LogInformation("Automatic backups are disabled (Backup:IntervalHours = {Hours}).", intervalHours);
                return;
            }
            var interval = TimeSpan.FromHours(intervalHours);

            // Let startup migrations finish before the first dump.
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                TimeSpan wait;
                try
                {
                    wait = await RunIfDueAsync(interval, keep, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // An exception escaping ExecuteAsync would stop the whole app.
                    logger.LogError(ex, "Automatic backup failed.");
                    await LogFailureAsync(ex.Message);
                    wait = TimeSpan.FromHours(1);
                }
                // Capped so long intervals stay within Task.Delay's limit; the schedule is re-checked.
                await Task.Delay(wait < MaxWait ? wait : MaxWait, stoppingToken);
            }
        }

        private static readonly TimeSpan MaxWait = TimeSpan.FromDays(1);

        /// <summary>Takes an automatic backup if one is due and returns how long to wait before checking again.</summary>
        private async Task<TimeSpan> RunIfDueAsync(TimeSpan interval, int keep, CancellationToken ct)
        {
            // A fresh install has nothing to keep yet, and its empty backups would be listed
            // next to the real one on the setup restore page.
            using (var scope = scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                if (await SetupService.IsFreshInstallAsync(db)) return TimeSpan.FromMinutes(10);
            }

            // Schedule from the newest automatic backup so restarts don't add extra ones.
            var last = backupService.ListBackups().FirstOrDefault(b => b.Kind == BackupKind.Auto)?.CreatedUtc;
            var due = last.HasValue ? last.Value + interval - DateTime.UtcNow : TimeSpan.Zero;
            if (due > TimeSpan.Zero) return due;

            var result = await backupService.CreateBackupAsync(BackupKind.Auto, ct);
            if (!result.Success)
            {
                logger.LogError("Automatic backup failed: {Error}", result.Error);
                await LogFailureAsync(result.Error);
                // Retry well before the next regular run, without hammering a broken setup.
                return TimeSpan.FromHours(1);
            }

            var pruned = backupService.PruneAutomaticBackups(keep);
            if (pruned > 0)
                logger.LogInformation("Pruned {Count} old automatic backup(s).", pruned);
            return interval;
        }

        // Surface failures in the admin audit log — nobody watches container logs.
        private async Task LogFailureAsync(string? error)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var audit = scope.ServiceProvider.GetRequiredService<AuditService>();
                await audit.LogAsync("BackupFailed", entityType: "Backup", details: $"Automatic backup failed: {error}");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not record the backup failure in the audit log.");
            }
        }
    }
}
