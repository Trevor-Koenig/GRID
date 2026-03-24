namespace GRID.Services
{
    using GRID.Data;
    using GRID.Models;
    using Microsoft.EntityFrameworkCore;

    public class InviteService(ApplicationDbContext db)
    {
        public async Task<Invite> CreateInviteAsync(string role, bool isSingleUse = true, int maxUses = 1, string? email = null, TimeSpan? validFor = null)
        {
            var invite = new Invite
            {
                Code = GenerateRandomCode(16),
                Role = role,
                IsSingleUse = isSingleUse,
                MaxUses = isSingleUse ? 1 : maxUses,
                Email = email,
                ExpiresAt = validFor.HasValue ? DateTime.UtcNow.Add(validFor.Value) : null
            };
            db.Invites.Add(invite);
            await db.SaveChangesAsync();
            return invite;
        }

        private string GenerateRandomCode(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Range(0, length).Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray());
        }

        public async Task<(int IsValid, Invite? Invite)> ValidateInviteAsync(string code)
        {
            var invite = await db.Invites
                .Include(i => i.Usages)
                .FirstOrDefaultAsync(i => i.Code == code && i.IsActive);

            if (invite == null)
                return (1, null);

            if (invite.ExpiresAt.HasValue && invite.ExpiresAt < DateTime.UtcNow)
                return (2, null);

            if (invite.IsSingleUse && invite.CurrentUses >= 1)
                return (3, null);

            if (!invite.IsSingleUse && invite.CurrentUses >= invite.MaxUses)
                return (4, null);

            return (0, invite);
        }

        public async Task<(bool Success, string? Role)> ConsumeInviteAsync(
                                                            string code,
                                                            string userId)
        {

            var invite = await db.Invites
                .Include(i => i.Usages)
                .FirstOrDefaultAsync(i => i.Code == code && i.IsActive);

            if (invite == null)
                return (false, null);

            // Expiration check
            if (invite.ExpiresAt.HasValue && invite.ExpiresAt < DateTime.UtcNow)
                return (false, null);

            // Usage check (only if limited)
            if (invite.MaxUses.HasValue &&
                invite.CurrentUses >= invite.MaxUses.Value)
            {
                return (false, null);
            }

            invite.CurrentUses++;

            invite.Usages.Add(new InviteUsage
            {
                UserId = userId
            });

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Handle the concurrency conflict
                // You can inspect which entities failed:
                foreach (var entry in ex.Entries)
                {
                    if (entry.Entity is Invite inv)
                    {
                        // Option 1: Reload current values from DB
                        await entry.ReloadAsync();
                    }
                }
            }

            return (true, invite.Role);
        }
    }
}