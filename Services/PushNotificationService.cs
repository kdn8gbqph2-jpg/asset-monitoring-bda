using asset_monitoring.Data;
using Microsoft.EntityFrameworkCore;
using NLog;
using WebPush;
using LibPushSubscription = WebPush.PushSubscription;
using AppPushSubscription = asset_monitoring.Models.PushSubscription;

namespace asset_monitoring.Services
{
    /// <summary>
    /// Sends Web Push notifications (VAPID) to a user's registered browser
    /// subscriptions and prunes dead endpoints. No-ops cleanly when VAPID keys
    /// are not configured. Uses its own DB scope so it is safe to call
    /// fire-and-forget from a request handler.
    /// </summary>
    public class PushNotificationService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly VapidDetails? _vapid;
        private static readonly WebPushClient Client = new();

        public PushNotificationService(IServiceScopeFactory scopeFactory, IConfiguration config)
        {
            _scopeFactory = scopeFactory;

            var subject    = config["WebPush:Subject"];
            var publicKey  = config["WebPush:PublicKey"];
            var privateKey = config["WebPush:PrivateKey"];

            if (!string.IsNullOrWhiteSpace(subject) &&
                !string.IsNullOrWhiteSpace(publicKey) &&
                !string.IsNullOrWhiteSpace(privateKey))
            {
                _vapid = new VapidDetails(subject, publicKey, privateKey);
            }
            else
            {
                Logger.Warn("PushNotificationService: VAPID keys not configured — push notifications disabled.");
            }
        }

        public bool IsConfigured => _vapid != null;
        public string? PublicKey => _vapid?.PublicKey;

        public Task SendToUserAsync(int userId, string title, string body, string url = "/Dashboard")
            => SendAsync(q => q.Where(s => s.UserId == userId), title, body, url);

        public Task SendToMobileAsync(string mobile, string title, string body, string url = "/Dashboard")
            => string.IsNullOrWhiteSpace(mobile)
                ? Task.CompletedTask
                : SendAsync(q => q.Where(s => s.Mobile == mobile), title, body, url);

        private async Task SendAsync(
            Func<IQueryable<AppPushSubscription>, IQueryable<AppPushSubscription>> filter,
            string title, string body, string url)
        {
            if (_vapid == null) return;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var subs = await filter(db.PushSubscriptions).ToListAsync();
                if (subs.Count == 0) return;

                var payload = System.Text.Json.JsonSerializer.Serialize(new { title, body, url });
                var dead = new List<AppPushSubscription>();

                foreach (var s in subs)
                {
                    try
                    {
                        await Client.SendNotificationAsync(
                            new LibPushSubscription(s.Endpoint, s.P256dh, s.Auth), payload, _vapid);
                    }
                    catch (WebPushException ex)
                    {
                        // 404/410 → the subscription is gone; prune it.
                        if (ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                            ex.StatusCode == System.Net.HttpStatusCode.Gone)
                            dead.Add(s);
                        else
                            Logger.Warn("Push send failed (status={0}) for subId={1}", ex.StatusCode, s.Id);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Push send error for subId={0}", s.Id);
                    }
                }

                if (dead.Count > 0)
                {
                    db.PushSubscriptions.RemoveRange(dead);
                    await db.SaveChangesAsync();
                    Logger.Info("Pruned {0} dead push subscriptions", dead.Count);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "PushNotificationService.SendAsync failed");
            }
        }
    }
}
