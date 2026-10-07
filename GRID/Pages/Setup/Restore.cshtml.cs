using GRID.Data;
using GRID.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace GRID.Pages.Setup
{
    /// <summary>
    /// Restore a backup into a fresh install. Only exists while there are no accounts, and
    /// requires the setup code from the container log (see <see cref="SetupService"/>).
    /// </summary>
    [EnableRateLimiting("LoginLimiter")]
    [RequestSizeLimit(BackupService.MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = BackupService.MaxUploadBytes)]
    public class RestoreModel(
        ApplicationDbContext db,
        BackupService backupService,
        SetupService setupService,
        AuditService audit,
        ILogger<RestoreModel> logger) : PageModel
    {
        /// <summary>Backups already in the backup directory, e.g. copied onto the host dataset.</summary>
        public IReadOnlyList<BackupFile> ServerBackups { get; set; } = [];
        public string BackupDirectory => backupService.BackupDirectory;
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await SetupService.IsFreshInstallAsync(db)) return NotFound();
            ServerBackups = backupService.ListBackups();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string? setupCode, string? existingBackup, IFormFile? file)
        {
            if (!await SetupService.IsFreshInstallAsync(db)) return NotFound();
            ServerBackups = backupService.ListBackups();

            if (!setupService.IsValidCode(setupCode))
            {
                logger.LogWarning("Setup restore attempted with a wrong setup code from {Ip}.", HttpContext.Connection.RemoteIpAddress);
                ErrorMessage = "Wrong setup code. It is printed in the GRID container log at startup.";
                return Page();
            }

            string name;
            if (file is { Length: > 0 })
            {
                await using var stream = file.OpenReadStream();
                var upload = await backupService.SaveUploadAsync(stream, HttpContext.RequestAborted);
                if (!upload.Success)
                {
                    ErrorMessage = $"Upload rejected: {upload.Error}";
                    return Page();
                }
                name = upload.FileName!;
            }
            else if (!string.IsNullOrEmpty(existingBackup))
            {
                name = existingBackup;
            }
            else
            {
                ErrorMessage = "Upload a backup file or pick one from the server.";
                return Page();
            }

            // Nothing to protect in an empty install, so no pre-restore backup.
            var result = await backupService.RestoreAsync(name, safetyBackup: false, CancellationToken.None);
            if (!result.Success)
            {
                ErrorMessage = result.Error;
                ServerBackups = backupService.ListBackups();
                return Page();
            }

            await audit.LogAsync("RestoredBackup", entityType: "Backup", entityId: name,
                details: $"Restored during setup from {HttpContext.Connection.RemoteIpAddress}");

            TempData["StatusMessage"] = $"Database restored from {name}. Sign in with an account from that backup.";
            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }
    }
}
