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
    btn.innerHTML = '<i class="bi bi-download"></i><span class="ms-1">Install app</span>';
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

  // iOS Safari never fires beforeinstallprompt — surface a manual "how to install" hint
  // so the option is visible there too (it's hidden once running as an installed PWA).
  (function iosInstallHint() {
    var isIos = /iphone|ipad|ipod/i.test(navigator.userAgent || '');
    var isStandalone = window.navigator.standalone === true ||
                       window.matchMedia('(display-mode: standalone)').matches;
    if (!isIos || isStandalone) return;
    var btn = document.getElementById('bdaInstallBtn');
    if (!btn) return;
    btn.style.display = '';
    btn.innerHTML = '<i class="bi bi-box-arrow-up"></i><span class="ms-1">Install app</span>';
    btn.onclick = function () {
      alert('To install: tap the Share button (the square with an up-arrow) at the bottom of Safari, ' +
            'then choose “Add to Home Screen”. Open the app from the new icon and log in.');
    };
  })();

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

  // Console helper for testing: send a push to your own devices.
  window.bdaTestPush = function () {
    return fetch('/Push?handler=SendTest', {
      method: 'POST',
      headers: { 'RequestVerificationToken': requestToken() }
    }).then(function (r) { return r.json(); }).then(function (d) { console.log('bdaTestPush:', d); return d; });
  };

  // ── Push (enabled by default; the only manual control is "Disable") ──────
  function alertsOptedOut() {
    try { return localStorage.getItem('bda-alerts-disabled') === '1'; } catch (e) { return false; }
  }
  function setOptOut(v) {
    try {
      if (v) localStorage.setItem('bda-alerts-disabled', '1');
      else localStorage.removeItem('bda-alerts-disabled');
    } catch (e) {}
  }

  function initPush(reg) {
    if (!('PushManager' in window) || !('Notification' in window) || !isLoggedIn()) return;

    // Enabled by default: anyone who has already granted permission is (re)subscribed
    // silently on every load unless they explicitly disabled alerts. A first-time opt-in
    // still needs one tap (auto-prompting on load risks a permanent browser block), so we
    // surface a prominent "Enable alerts" button for that case.
    if (!alertsOptedOut() && Notification.permission === 'granted') {
      subscribe(reg);
    }
    reflectAlertsButton(reg);
    window.bdaEnablePush = function () { subscribe(swReg || reg); };
  }

  function setAlertsBtn(btn, cls, icon, label, onclick) {
    btn.style.display = '';
    btn.className = 'btn btn-sm ms-2 ' + cls;
    btn.innerHTML = '<i class="bi ' + icon + '"></i><span class="ms-1">' + label + '</span>';
    btn.onclick = onclick;
  }

  // Navbar bell reflects state: active → "Disable alerts"; otherwise → prominent "Enable alerts".
  function reflectAlertsButton(reg) {
    var btn = document.getElementById('bdaAlertsBtn');
    if (!btn) return;
    if (Notification.permission === 'granted' && !alertsOptedOut()) {
      // Light/contrasting styles so the button is legible on the blue navbar.
      setAlertsBtn(btn, 'btn-outline-light', 'bi-bell-slash', 'Disable alerts', function () { disableAlerts(reg); });
    } else if (Notification.permission !== 'denied') {
      setAlertsBtn(btn, 'btn-light', 'bi-bell', 'Enable alerts', function () { subscribe(reg); });
    } else {
      btn.style.display = 'none';
    }
  }

  function subscribe(reg) {
    if (!reg) reg = swReg;
    if (!reg) return;
    setOptOut(false);
    Notification.requestPermission().then(function (perm) {
      if (perm !== 'granted') { reflectAlertsButton(reg); return; }
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
        })
        .then(function () { reflectAlertsButton(reg); });
    }).catch(function (e) { console.warn('push subscribe failed', e); });
  }

  function disableAlerts(reg) {
    if (!reg) reg = swReg;
    setOptOut(true);
    if (!reg) { reflectAlertsButton(reg); return; }
    reg.pushManager.getSubscription().then(function (sub) {
      if (!sub) return;
      var endpoint = sub.endpoint;
      return sub.unsubscribe().then(function () {
        return fetch('/Push?handler=Unsubscribe', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': requestToken() },
          body: JSON.stringify({ endpoint: endpoint })
        });
      });
    }).catch(function (e) { console.warn('push unsubscribe failed', e); })
      .then(function () { reflectAlertsButton(reg); });
  }
})();
