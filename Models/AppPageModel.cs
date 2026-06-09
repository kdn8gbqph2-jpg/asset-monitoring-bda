using asset_monitoring.Data;
using asset_monitoring.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace asset_monitoring.Models
{
    public abstract class AppPageModel : PageModel
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public string? Username =>
            HttpContext.Session.GetString("UserName");

        public string? UserType =>
            HttpContext.Session.GetString("UserType");

        public int? UserId =>
           HttpContext.Session.GetInt32("UserId");

        public string? Mobile =>
            HttpContext.Session.GetString("Mobile");

        public bool IsLoggedIn =>
            !string.IsNullOrEmpty(Username);

        protected IActionResult LogoutAndRedirect()
        {
            HttpContext.Session.Clear();
            return RedirectToPage("/Index");
        }

        /// <summary>
        /// Parse a comma-separated pump-id list (from the running-log download UI),
        /// keeping only positive integers. Used by the running-log export handlers.
        /// </summary>
        protected static List<int> ParsePumpIds(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return new List<int>();
            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                      .Select(s => int.TryParse(s, out var v) ? v : -1)
                      .Where(v => v > 0)
                      .Distinct()
                      .ToList();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  SHARED HANDLER — Change password (self-service, post-login)
        //  Available on any page that inherits AppPageModel (Operator, JE, Admin)
        //  POST ?handler=ChangePassword
        // ═════════════════════════════════════════════════════════════════════

        public async Task<JsonResult> OnPostChangePasswordAsync([FromBody] ChangePasswordInput input)
        {
            if (!IsLoggedIn || UserId is null or <= 0)
            {
                return new JsonResult(new { success = false, message = "Session expired. Please log in again." })
                { StatusCode = 401 };
            }

            if (input == null
                || string.IsNullOrWhiteSpace(input.CurrentPassword)
                || string.IsNullOrWhiteSpace(input.NewPassword)
                || string.IsNullOrWhiteSpace(input.ConfirmPassword))
            {
                return new JsonResult(new { success = false, message = "All fields are required." });
            }

            if (input.NewPassword != input.ConfirmPassword)
            {
                return new JsonResult(new { success = false, message = "New password and confirmation do not match." });
            }

            if (input.NewPassword.Length < 8)
            {
                return new JsonResult(new { success = false, message = "New password must be at least 8 characters." });
            }

            if (input.CurrentPassword == input.NewPassword)
            {
                return new JsonResult(new { success = false, message = "New password must be different from the current password." });
            }

            try
            {
                var db = HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                var user = await db.BdaUserMasters.FirstOrDefaultAsync(u => u.UserId == UserId!.Value);

                if (user == null || !user.IsActive)
                {
                    Logger.Warn("ChangePassword: userId={0} not found or inactive", UserId);
                    return new JsonResult(new { success = false, message = "User not found." })
                    { StatusCode = 404 };
                }

                // Verify current password — BCrypt hash or legacy plain-text
                var stored = user.Password ?? string.Empty;
                bool currentValid = stored.StartsWith("$2")
                    ? BCrypt.Net.BCrypt.Verify(input.CurrentPassword, stored)
                    : stored == input.CurrentPassword;

                if (!currentValid)
                {
                    Logger.Warn("ChangePassword: wrong current password for userId={0}", UserId);
                    return new JsonResult(new { success = false, message = "Current password is incorrect." });
                }

                // Hash and save new password
                user.Password = BCrypt.Net.BCrypt.HashPassword(input.NewPassword, workFactor: 12);
                user.RowUpdationDateTime = DateTime.UtcNow;
                await db.SaveChangesAsync();

                // Refresh in-memory user cache so next login uses the new hash
                var userCache = HttpContext.RequestServices.GetRequiredService<UserCacheService>();
                userCache.Reload();

                Logger.Info("ChangePassword: password updated for userId={0}, username={1}", UserId, Username);
                return new JsonResult(new { success = true, message = "Password changed successfully." });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "ChangePassword failed for userId={0}", UserId);
                return new JsonResult(new { success = false, message = "Failed to change password. Please try again." })
                { StatusCode = 500 };
            }
        }

        public class ChangePasswordInput
        {
            public string CurrentPassword { get; set; } = "";
            public string NewPassword { get; set; } = "";
            public string ConfirmPassword { get; set; } = "";
        }
    }
}
