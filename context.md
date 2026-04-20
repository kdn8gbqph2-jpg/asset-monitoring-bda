# Chat Context — Full History

Running summary of all Claude-assisted work on `asset-monitoring` (PMS). Read top-to-bottom for a narrative of decisions and fixes; see `deploy/INFRA.md` for the current-state infrastructure reference.

---

## 1. VPS deployment (bring-up)

Bootstrapped production from an empty VPS:

- Provisioned MySQL DB `bda_pump_prod` on the VPS using `deploy/setup_prod_db.sql` — creates all 8 tables, seeds `app_config_tbl`, and creates the default admin row (username `admin`, plain-text password `Admin@123` — the login flow auto-upgrades it to BCrypt on first use).
- Published the .NET 8 app to `/var/www/asset-monitoring`, ran under systemd as `www-data` on `http://localhost:5010`.
- Put Nginx in front as a reverse proxy.
- Hosting provider's external firewall only allows ports **80 / 443 / 2222** — nothing else works (one failed experiment tried port 8080 and timed out).
- Decision: user wanted **IP-only initially** ("I'll purchase domain later"), so first iteration served via `http://69.62.80.7`.

---

## 2. Cookie / session bug — Admin page wouldn't open after login

**Symptom:** Admin password accepted, then Admin page bounced back to login.

**Root cause:** `CookieSecurePolicy.Always` in `Program.cs` forced the `Secure` flag on the session cookie, but the app was served over **HTTP** (not HTTPS yet). Browser refused to send the cookie back → session looked empty on the next request.

**Fix:** changed to `CookieSecurePolicy.SameAsRequest`. Works on HTTP and, once Nginx+SSL is in place, `ForwardedHeaders` makes the request look like HTTPS and the `Secure` flag gets set automatically.

---

## 3. Remote MySQL access from the developer machine

User wanted to query production DB from MySQL Workbench on their Windows box. No direct port exposure.

**Solution:** SSH-tunneled connection in MySQL Workbench — MySQL on `127.0.0.1:3306` *through* SSH `69.62.80.7:2222` using the VPS SSH key. No MySQL port needs to be opened publicly.

---

## 4. GitHub Actions deploy pipeline (iterated)

`.github/workflows/deploy.yml` went through several rounds of refinement:

1. First version triggered on `push`. User said: **"No I do not want it automatically. I want to trigger manual build."** → switched to `workflow_dispatch` only.
2. User asked: "can anyone trigger this workflow on this public repo?" → confirmed only repo collaborators with write access can run `workflow_dispatch`.
3. User asked: **"make build and deploy separate steps in workflow"** → split into `build` job (checkout/publish/tar/upload-artifact) and `deploy` job (`needs: build`, download-artifact, scp, ssh deploy script).
4. Critical bug: publish step overwrote `appsettings.Production.json` on the VPS, wiping the real DB connection string. Fix: backup to `/tmp` before extraction, restore afterwards. The repo copy keeps placeholder `CHANGE_ME` values on purpose — real credentials live only on the server.
5. Cleanup step failed with "Operation not permitted" because sudo created the backup but non-sudo tried to delete it → fixed with `sudo rm -f`.

GitHub secrets used: `VPS_HOST`, `VPS_PORT`, `VPS_USER`, `VPS_SSH_KEY`.

User asked whether the app needs restart after deploy — yes, the script calls `systemctl stop`/`start asset-monitoring` before/after extraction.

---

## 5. VPS security audit & hardening

User asked for a full audit. Applied:

- **SSH root login disabled** (`PermitRootLogin no` in `/etc/ssh/sshd_config.d/99-custom.conf`). User asked if this affects superuser permissions — clarified that `sudo` still works, we only blocked direct `ssh root@…` login.
- **Port 8080 removed from UFW** (leftover from earlier experiments).
- **33 system updates applied** with confirmation that no running services would be affected; reboot performed.
- **`server_tokens off;`** in `nginx.conf` so Nginx doesn't advertise its version.
- **Fail2ban** configured in `/etc/fail2ban/jail.local`: `sshd` jail plus three nginx jails (`nginx-http-auth`, `nginx-botsearch`, `nginx-bad-request`), `banaction = ufw`.
- **Direct IP access blocked** after the subdomain went live — the default Nginx vhost returns `444` (close connection, no response), so only `https://pms.bdabharatpur.org` reaches the app.

Still pending (user aware): `PasswordAuthentication no` on SSH, lock unused `ubuntu` user (has NOPASSWD sudo), `chmod 640` on `appsettings.Production.json`, remove duplicate `dcsdms` Nginx config, add swap, MySQL daily backups, log rotation for app logs.

---

## 6. Timezone bug — "Last Updated" showed wrong time

**Symptom:** dashboard column showed times several hours off.

**Root cause:** server is UTC; views used `.ToLocalTime()` which resolves to the server's local time (UTC) — not IST.

**Fix:** replaced with explicit `TimeZoneInfo.ConvertTimeFromUtc(..., "Asia/Kolkata")` in four places:
- `Pages/Shared/_PumpRunningSummary.cshtml` (Last Updated column)
- `Pages/Admin.cshtml` (complaints table)
- `Services/ReportExportService.cs` (both Excel and PDF exporters)

Convention going forward: DB timestamps are always UTC; presentation layer converts to IST explicitly. **Never** use `.ToLocalTime()` in this codebase.

---

## 7. Subdomain + SSL — `pms.bdabharatpur.org`

User had the parent domain `bdabharatpur.org` already. They asked:
- **"why is it named pms?"** — PMS = Pump Management System (short, memorable, SEO-friendly).
- **"what is type A?"** — DNS A record maps hostname → IPv4.
- **"will I be able to add dms.bdabharatpur.org etc. later?"** — yes, each subdomain is an independent A record pointing to the same (or different) IP; Nginx routes by `server_name`.

Sequence:
1. Added Nginx vhost config `pms-bdabharatpur` (proxies to `localhost:5010`).
2. User added the DNS A record in their registrar (confirmed via screenshot).
3. DNS lookup initially resolved to wrong IPs — turned out to be a local resolver suffix issue. Verified with trailing-dot query `pms.bdabharatpur.org.`
4. Ran `certbot --nginx` to issue Let's Encrypt cert for the subdomain; auto-renew is set up.
5. Enabled the `444`-response default vhost to block direct-IP access now that the domain works.

Production URL is now `https://pms.bdabharatpur.org`.

---

## 8. Change-password feature (this session)

User request: **"I want to add change password post login for JE and Operator role."**

Implemented as a shared handler so no page-level wiring is needed:

- `Models/AppPageModel.cs` — added `OnPostChangePasswordAsync` on the base class. Validates session + input (required fields, min 8 chars, new ≠ confirm, new ≠ current). Verifies current password via BCrypt (with legacy plain-text fallback, same pattern as login). Writes a fresh `workFactor: 12` hash, calls `userCache.Reload()`. DI is resolved via `HttpContext.RequestServices` so the base class needs no constructor changes.
- `Pages/Shared/_ChangePasswordModal.cshtml` — new reusable Bootstrap modal with three password fields, eye-toggle visibility, live match-check, and JS that POSTs to `?handler=ChangePassword` with the antiforgery token. `window.__changePwdInit` guards against double-binding.
- `Pages/Shared/_LoginHeader.cshtml` — added "Change Password" button between username and Logout; includes the modal partial.

Because all three role pages (Admin/Operator/JE) already use `_LoginHeader`, the feature appears on all of them without individual edits. Build passed with 0 warnings; smoke-tested locally by starting the dev server on `http://localhost:5005` (server compiled all views including `_ChangePasswordModal.cshtml`). End-to-end UI click-through by the user is still pending.

---

## 9. "How do I decrypt a hash?" detour

User asked how to decrypt BCrypt hashes locally. Explained: BCrypt is one-way, no decryption possible. Offered three practical alternatives (verify via `BCrypt.Verify`, reset from Admin panel, or replace the hash in MySQL Workbench with a pre-computed one).

User then said they want to **switch to plain-text passwords**. I pushed back on grounds of public-domain exposure, password reuse, admin trust, and compliance — and pointed out the existing `VerifyAndUpgradePassword` already gives them most of the convenience they probably want. User said **"drop this"** — no code changes were made; BCrypt stays.

---

## 10. Handoff notes (this session, last step)

User asked for two standing documents so future chats can resume without re-reading the transcript:

- **`context.md`** (this file) — narrative of what's been done and why.
- **`deploy/INFRA.md`** — current-state infrastructure reference (VPS layout, DB, Nginx, SSH, Fail2ban, deploy pipeline, conventions, pending tasks).

Both committed to `develop` along with the change-password feature:
- `34c1f41` — Add self-service change password for logged-in users
- `bfafca8` — Add context.md and deploy/INFRA.md handoff notes

Pushed to `origin/develop`. GitHub flagged "Changes must be made through a pull request" but the push was allowed by a bypass rule on the account — worth deciding later whether to enforce PR-only for `develop`.

Deployment to the VPS still needs a manual click in the GitHub Actions tab (`workflow_dispatch`).

---

## 11. Open threads / not yet done

### Security
- SSH: `PasswordAuthentication no`
- Lock unused `ubuntu` user (has NOPASSWD sudo)
- `chmod 640` on `/var/www/asset-monitoring/appsettings.Production.json`
- Remove duplicate `dcsdms` Nginx config

### Operational
- MySQL daily backup cron
- Log rotation for `/var/www/asset-monitoring/logs/`
- Add swap (VPS has none configured)

### Code / repo
- PR #4 (dead code cleanup) — needs merge
- End-to-end manual click-through of the new Change Password UI

### House-keeping
- Local dev server task `byhad8wtl` (port 5005) may still be running — stop when no longer needed.
