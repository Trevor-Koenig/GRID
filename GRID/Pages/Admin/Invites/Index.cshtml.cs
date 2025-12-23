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
    public class IndexModel(GRID.Data.ApplicationDbContext context) : PageModel
    {
        public IList<Invite> Invite { get;set; } = default!;

        public async Task OnGetAsync()
        {
            Invite = await context.Invites.ToListAsync();
        }
    }
}
