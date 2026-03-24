using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using GRID.Data;
using GRID.Models;

namespace GRID.Pages.Admin.Invites
{
    public class DeleteModel(GRID.Data.ApplicationDbContext context) : PageModel
    {
        [BindProperty]
        public Invite Invite { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invite = await context.Invites.FirstOrDefaultAsync(m => m.Id == id);

            if (invite is not null)
            {
                Invite = invite;

                return Page();
            }

            return NotFound();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invite = await context.Invites.FindAsync(id);
            if (invite != null)
            {
                Invite = invite;
                context.Invites.Remove(Invite);
                await context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
