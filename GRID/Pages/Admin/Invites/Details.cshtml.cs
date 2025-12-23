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
    public class DetailsModel(GRID.Data.ApplicationDbContext context) : PageModel
    {
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
    }
}
