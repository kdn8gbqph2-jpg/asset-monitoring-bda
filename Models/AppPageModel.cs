using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace asset_monitoring.Models
{
    public abstract class AppPageModel : PageModel
    {
        public string? Username =>
            HttpContext.Session.GetString("UserName");

        public string? UserType =>
            HttpContext.Session.GetString("UserType");

        public bool IsLoggedIn =>
            !string.IsNullOrEmpty(Username);

        protected IActionResult LogoutAndRedirect()
        {
            HttpContext.Session.Clear();
            return RedirectToPage("/Index");
        }

        protected void RequireLogin()
        {
            if (!IsLoggedIn)
            {
                Response.Redirect("/Index");
            }
        }
    }
}
