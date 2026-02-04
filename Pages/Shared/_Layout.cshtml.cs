using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace asset_monitoring.Pages.Shared
{
    public class _LayoutModel : PageModel
    {
        public string? Username { get; private set; }

        public void OnGet()
        {
            Username = HttpContext.Session.GetString("UserName");
        }
        public IActionResult OnPostLogout()
        {
            HttpContext.Session.Clear();
            return RedirectToPage("/Index");
        }
    }
}