using asset_monitoring.Data;
using asset_monitoring.Models;
using asset_monitoring.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace asset_monitoring.Pages
{
    public class AdminModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserCacheService _userCache;

        [BindProperty]
        public EditUserInputModel EditUserModel { get; set; } = new();
        public AdminModel(ApplicationDbContext context, UserCacheService userCache)
        {
            _context = context;
            _userCache = userCache;
        }

        public List<BdaUserMaster> Users { get; set; } = new();
        public bool IsAdmin { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Example: get userType from session or claims
            var userType = HttpContext.Session.GetString("UserType");
            IsAdmin = userType == "ADMIN";

            if (!IsAdmin)
                return Page();

            Users = await _context.BdaUserMasters
                .AsNoTracking()
                .Where(u => u.IsActive)
                .ToListAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var userType = HttpContext.Session.GetString("UserType");
            IsAdmin = userType == "ADMIN";
            if (!IsAdmin)
                return Forbid();

            var user = await _context.BdaUserMasters.FindAsync(id);
            if (user != null)
            {
                    user.IsActive = false; // Set is_active to 0 (false)
                    _context.BdaUserMasters.Update(user);
                await _context.SaveChangesAsync();
            }
            return RedirectToPage();
        }

        // Add OnPostAddAsync and OnPostEditAsync as needed for add/update logic

        public async Task<IActionResult> OnPostLogoutAsync()
        {
            HttpContext.Session.Clear();
            return RedirectToPage("/Dashboard");
        }

        public async Task<IActionResult> OnPostEditAsync()
        {
            var userType = HttpContext.Session.GetString("UserType");
            IsAdmin = userType == "ADMIN";
            if (!IsAdmin)
                return Forbid();

            if (!ModelState.IsValid)
            {
                await OnGetAsync();
                return Page();
            }

            var user = await _context.BdaUserMasters.FindAsync(EditUserModel.UserId);
            if (user != null)
            {
                user.Name = EditUserModel.Name;
                if (Enum.TryParse<BdaUserType>(EditUserModel.UserType, out var ut))
                    user.UserType = ut;
                user.MobileNumber = EditUserModel.MobileNumber;
                user.Password = EditUserModel.Password;
                var istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                user.RowUpdationDateTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);

                _context.BdaUserMasters.Update(user);
                await _context.SaveChangesAsync();
            }
            return RedirectToPage();
        }


    }
    public class EditUserInputModel
    {
        public int UserId { get; set; }
        public string Name { get; set; } = "";
        public string UserType { get; set; } = "";
        public string MobileNumber { get; set; } = "";
        public string Password { get; set; } = "";
    }
}
