using asset_monitoring.Data;
using asset_monitoring.Models;
using asset_monitoring.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace asset_monitoring.Pages
{
    /// <summary>
    /// JSON-only endpoints for Web Push:
    ///   GET  /Push?handler=VapidKey     → { publicKey }
    ///   POST /Push?handler=Subscribe    → store the browser PushSubscription for the logged-in user
    ///   POST /Push?handler=Unsubscribe  → remove a subscription by endpoint
    /// </summary>
    public class PushModel : AppPageModel
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly ApplicationDbContext _db;
        private readonly PushNotificationService _push;

        public PushModel(ApplicationDbContext db, PushNotificationService push)
        {
            _db = db;
            _push = push;
        }

        // No page UI.
        public IActionResult OnGet() => NotFound();

        public JsonResult OnGetVapidKey()
            => new JsonResult(new { publicKey = _push.PublicKey });

        public async Task<JsonResult> OnPostSubscribeAsync([FromBody] BrowserSubscription? sub)
        {
            if (!IsLoggedIn || UserId is null)
                return new JsonResult(new { success = false, message = "Not logged in" }) { StatusCode = 401 };

            if (sub == null || string.IsNullOrWhiteSpace(sub.Endpoint) || sub.Keys == null
                || string.IsNullOrWhiteSpace(sub.Keys.P256dh) || string.IsNullOrWhiteSpace(sub.Keys.Auth))
                return new JsonResult(new { success = false, message = "Invalid subscription" });

            try
            {
                var now = DateTime.UtcNow;
                var existing = await _db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == sub.Endpoint);
                if (existing == null)
                {
                    _db.PushSubscriptions.Add(new PushSubscription
                    {
                        UserId               = UserId,
                        Mobile               = Mobile,
                        UserType             = UserType,
                        Endpoint             = sub.Endpoint,
                        P256dh               = sub.Keys.P256dh,
                        Auth                 = sub.Keys.Auth,
                        RowInsertionDateTime = now,
                        RowUpdationDateTime  = now
                    });
                }
                else
                {
                    // Re-point an existing endpoint to the current user/device keys.
                    existing.UserId              = UserId;
                    existing.Mobile              = Mobile;
                    existing.UserType            = UserType;
                    existing.P256dh              = sub.Keys.P256dh;
                    existing.Auth                = sub.Keys.Auth;
                    existing.RowUpdationDateTime = now;
                }

                await _db.SaveChangesAsync();
                Logger.Info("Push subscribe stored for userId={0}", UserId);
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Push subscribe failed for userId={0}", UserId);
                return new JsonResult(new { success = false }) { StatusCode = 500 };
            }
        }

        public async Task<JsonResult> OnPostUnsubscribeAsync([FromBody] BrowserSubscription? sub)
        {
            if (sub == null || string.IsNullOrWhiteSpace(sub.Endpoint))
                return new JsonResult(new { success = true });

            var rows = await _db.PushSubscriptions.Where(s => s.Endpoint == sub.Endpoint).ToListAsync();
            if (rows.Count > 0)
            {
                _db.PushSubscriptions.RemoveRange(rows);
                await _db.SaveChangesAsync();
            }
            return new JsonResult(new { success = true });
        }

        public class BrowserSubscription
        {
            public string Endpoint { get; set; } = "";
            public SubKeys? Keys { get; set; }

            public class SubKeys
            {
                public string P256dh { get; set; } = "";
                public string Auth { get; set; } = "";
            }
        }
    }
}
