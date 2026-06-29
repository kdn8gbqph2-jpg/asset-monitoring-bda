// BDA PMS — PWA bootstrap: registers the service worker, offers an install
// affordance, and wires Web Push subscription for logged-in users.
(function () {
  'use strict';
  if (!('serviceWorker' in navigator)) return;

  var swReg = null;

  navigator.serviceWorker.register('/sw.js')
    .then(function (reg) { swReg = reg; initPush(reg); })
    .catch(function (err) { console.warn('SW registration failed', err); });

  // ── Install affordance ──────────────────────────────────────────────────
  var deferredInstall = null;
  window.addEventListener('beforeinstallprompt', function (e) {
    e.preventDefault();
    deferredInstall = e;
    var btn = document.getElementById('bdaInstallBtn');
    if (!btn) return;
    btn.style.display = '';
    btn.onclick = function () {
      btn.style.display = 'none';
      deferredInstall.prompt();
      deferredInstall.userChoice.finally(function () { deferredInstall = null; });
    };
  });
  window.addEventListener('appinstalled', function () {
    var btn = document.getElementById('bdaInstallBtn');
    if (btn) btn.style.display = 'none';
  });

  // ── Helpers ─────────────────────────────────────────────────────────────
  function requestToken() {
    var m = document.querySelector('meta[name="request-token"]');
    return m ? m.content : '';
  }
  function isLoggedIn() {
    return document.body && document.body.dataset.loggedIn === 'true';
  }
  function urlBase64ToUint8Array(base64String) {
    var padding = '='.repeat((4 - base64String.length % 4) % 4);
    var base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
    var raw = atob(base64);
    var out = new Uint8Array(raw.length);
    for (var i = 0; i < raw.length; i++) out[i] = raw.charCodeAt(i);
    return out;
  }

  // ── Push ────────────────────────────────────────────────────────────────
  function initPush(reg) {
    if (!('PushManager' in window) || !('Notification' in window) || !isLoggedIn()) return;

    var alertsBtn = document.getElementById('bdaAlertsBtn');

    if (Notification.permission === 'granted') {
      // Already allowed — (re)subscribe silently so the server has a fresh endpoint.
      subscribe(reg);
    } else if (Notification.permission === 'default' && alertsBtn) {
      // Contextual opt-in: show the "Alerts" button instead of prompting on load.
      alertsBtn.style.display = '';
      alertsBtn.onclick = function () {
        alertsBtn.style.display = 'none';
        subscribe(reg);
      };
    }

    // Expose a programmatic enable hook for other UI to call.
    window.bdaEnablePush = function () { subscribe(swReg || reg); };
  }

  function subscribe(reg) {
    if (!reg) return;
    Notification.requestPermission().then(function (perm) {
      if (perm !== 'granted') return;
      return fetch('/Push?handler=VapidKey')
        .then(function (r) { return r.json(); })
        .then(function (data) {
          if (!data || !data.publicKey) return;
          return reg.pushManager.getSubscription().then(function (existing) {
            return existing || reg.pushManager.subscribe({
              userVisibleOnly: true,
              applicationServerKey: urlBase64ToUint8Array(data.publicKey)
            });
          });
        })
        .then(function (sub) {
          if (!sub) return;
          return fetch('/Push?handler=Subscribe', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': requestToken() },
            body: JSON.stringify(sub)
          });
        });
    }).catch(function (e) { console.warn('push subscribe failed', e); });
  }
})();
