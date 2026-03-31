using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GRID.Pages.Admin.Docs
{
    public class IndexModel : PageModel
    {
        public IActionResult OnGet() => RedirectToPage("/Docs/Index");
    }
}
