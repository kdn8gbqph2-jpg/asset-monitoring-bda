// BDA PMS service worker.
// The app is online-only: we cache versioned static assets (cache-first) for speed,
// serve navigations/data network-first, and handle Web Push notifications.
// Bump CACHE_VERSION to force an update of cached assets.
const CACHE_VERSION = 'v1';
const CACHE = `bda-pms-${CACHE_VERSION}`;

const SHELL = [
  '/css/site.css',
  '/js/site.js',
  '/lib/bootstrap/dist/css/bootstrap.min.css',
  '/icons/icon-192.png',
  '/images/bda-logo.png'
];

self.addEventListener('install', (event) => {
  event.waitUntil(caches.open(CACHE).then((c) => c.addAll(SHELL)).catch(() => {}));
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(
        keys.filter((k) => k.startsWith('bda-pms-') && k !== CACHE).map((k) => caches.delete(k))
      ))
      .then(() => self.clients.claim())
  );
});

function isStaticAsset(pathname) {
  return /^\/(css|js|lib|icons|images)\//i.test(pathname) ||
         /\.(css|js|png|jpe?g|svg|ico|woff2?)$/i.test(pathname);
}

self.addEventListener('fetch', (event) => {
  const req = event.request;
  if (req.method !== 'GET') return;                  // never intercept status updates / form posts
  const url = new URL(req.url);
  if (url.origin !== self.location.origin) return;   // let CDN / OSM / wa.me pass straight through

  if (isStaticAsset(url.pathname)) {
    // Cache-first for static assets, refreshing the cache in the background.
    event.respondWith(
      caches.match(req).then((hit) => hit || fetch(req).then((res) => {
        const copy = res.clone();
        caches.open(CACHE).then((c) => c.put(req, copy)).catch(() => {});
        return res;
      }))
    );
    return;
  }

  // Network-first for navigations and ?handler= data (online-only app).
  event.respondWith(
    fetch(req).catch(() => caches.match(req).then((hit) => {
      if (hit) return hit;
      if (req.mode === 'navigate') {
        return new Response(
          '<!doctype html><meta charset="utf-8">' +
          '<meta name="viewport" content="width=device-width,initial-scale=1">' +
          '<div style="font-family:system-ui,sans-serif;padding:2rem;text-align:center">' +
          '<h3>You are offline</h3><p>Reconnect to use BDA PMS.</p></div>',
          { headers: { 'Content-Type': 'text/html; charset=utf-8' } }
        );
      }
      return Response.error();
    }))
  );
});

// ── Web Push ────────────────────────────────────────────────────────────────
self.addEventListener('push', (event) => {
  let data = {};
  try { data = event.data ? event.data.json() : {}; }
  catch (e) { data = { body: event.data ? event.data.text() : '' }; }

  const title = data.title || 'BDA PMS';
  const options = {
    body: data.body || '',
    icon: '/icons/icon-192.png',
    badge: '/icons/icon-192.png',
    tag: data.tag || 'bda-pms',
    renotify: true,
    data: { url: data.url || '/Dashboard' },
    requireInteraction: !!data.requireInteraction
  };
  event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  const target = (event.notification.data && event.notification.data.url) || '/Dashboard';
  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((list) => {
      for (const c of list) {
        if (c.url.includes(target) && 'focus' in c) return c.focus();
      }
      if (self.clients.openWindow) return self.clients.openWindow(target);
    })
  );
});
