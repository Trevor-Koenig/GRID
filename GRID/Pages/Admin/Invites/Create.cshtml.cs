using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using GRID.Data;
using GRID.Models;

namespace GRID.Pages.Admin.Invites
{
    public class CreateModel(GRID.Data.ApplicationDbContext context) : PageModel
    {
        public IActionResult OnGet()
        {
            return Page();
        }

        [BindProperty]
        public Invite Invite { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            context.Invites.Add(Invite);
            await context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
