# Production / Infrastructure — Asset Monitoring PMS

Handoff reference for future Claude sessions. Keep this file updated whenever the VPS, domain, DB, or deploy pipeline changes.

---

## 1. At a glance

| | |
|---|---|
| **App** | ASP.NET Core 8 Razor Pages (`asset-monitoring.csproj`, target `net8.0`) |
| **Repo** | https://github.com/… (branch `develop` is the deploy source) |
| **Production URL** | https://pms.bdabharatpur.org |
| **Services hub** | https://services.bdabharatpur.org (static HTML landing page, listing PMS / DMS / E-Works / E-Accounts) |
| **VPS IP** | `69.62.80.7` (direct `http://69.62.80.7` is blocked — returns `444`) |
| **Parent domain** | `bdabharatpur.org` (other subdomains planned, e.g. `dms.bdabharatpur.org`) |
| **VPS SSH user** | `itadmin` (passwordless sudo). Root login disabled. |
| **SSL** | Let's Encrypt via `certbot --nginx` (auto-renew enabled) |
| **Hosting firewall** | Only ports **80 / 443 / 2222** allowed inbound (provider-level, can't add more) |

---

## 2. Server layout

### Systemd service
- Unit: `/etc/systemd/system/asset-monitoring.service`
- App binds: `http://localhost:5010` (loopback only; Nginx proxies to it)
- Env: `ASPNETCORE_ENVIRONMENT=Production`
- Working dir: `/var/www/asset-monitoring`
- Runs as `www-data`

### App deployment directory
```
/var/www/asset-monitoring/
├── asset-monitoring.dll           # published binary
├── appsettings.Production.json    # contains DB connection string — NOT in git
└── logs/                          # NLog output — not rotated yet
```

### Nginx (`/etc/nginx/sites-available/`)
- `default` — catch-all, returns `444` to block direct-IP access
- `pms-bdabharatpur` — subdomain config, certbot-managed, proxies `/` → `http://localhost:5010`
- `services-bdabharatpur` — static site for `services.bdabharatpur.org`, certbot-managed, root `/var/www/services-bdabharatpur`
- `archives-bda` — DMS, proxies to `localhost:5000` (site) + `localhost:5001` (API)
- `nginx.conf` has `server_tokens off`

### SSH
- Port `2222` (standard `22` blocked at provider firewall)
- Custom config: `/etc/ssh/sshd_config.d/99-custom.conf` → `PermitRootLogin no`
- `PasswordAuthentication yes` still — should be switched to `no` (pending)

### Firewall — UFW
- Allowed: `22/tcp` (unused — provider blocks it), `80`, `443`, `2222`
- Port 8080 was removed (previously left open from earlier experiments)

### Fail2ban (`/etc/fail2ban/jail.local`)
- `sshd` jail
- 3 nginx jails (`nginx-http-auth`, `nginx-botsearch`, `nginx-limit-req`)
- `banaction = ufw` — integrates bans with UFW (a ban DROPs the IP on **all** ports, incl. 2222)
- Unban an IP: `sudo fail2ban-client set sshd unbanip <IP>` (substitute jail name as needed)

#### CI deploy connectivity (investigated 2026-06-09)
Two separate problems were found and fixed:

1. **fail2ban was DEAD for ~4h** — `jail.local` had a **duplicate `[sshd]`
   section** (a manual edit appended a 2nd `[sshd]` with an `ignoreip` line).
   fail2ban refuses any config with a duplicate section, so it crash-failed on
   every start. Fixed: removed the duplicate; the allowlist now lives only in
   `/etc/fail2ban/jail.d/github-actions-ignoreip.local` under `[DEFAULT]`.
   **Gotcha for the future: never add a 2nd `[sshd]` — put overrides in jail.d.**

2. **The deploy timeouts are UPSTREAM, not the VPS.** Packet capture (tcpdump on
   :2222 during a deploy) proved that when a run *fails*, GitHub's SYN never
   reaches the box; when it *arrives* (e.g. Azure IP 20.55.127.228), the VPS
   answers and SSH completes in ~24s. So some GitHub/Azure runner IPs are dropped
   on the path to the Hostinger VPS — intermittent, per-runner-IP, outside our
   control. Re-running the workflow usually lands on a good IP. For reliable
   deploys, move off GitHub-hosted runners' rotating IPs (self-hosted runner on
   the VPS, or Tailscale) — see options discussed in chat.

The fail2ban allowlist (`deploy/fail2ban-github-allowlist.sh`) is still worth
keeping so the VPS never *itself* bans a runner: it fetches GitHub's Actions
IPv4 ranges from `api.github.com/meta`, writes `[DEFAULT] ignoreip` to jail.d,
reloads, and clears bans. Run weekly via cron (ranges change). `ADMIN_IPS` in
the script includes the office IP `27.58.26.217` — keep it there or a cron run
will drop it from `ignoreip`. (Cron not yet installed.)

---

## 3. Database — Production

| | |
|---|---|
| Engine | MySQL (local to VPS, `localhost:3306`) |
| DB name | `bda_pump_prod` |
| App user | `bdauser` / password `Bda@Pump2026` (local only, not exposed externally) |
| Schema bootstrap | `deploy/setup_prod_db.sql` — all 8 tables + seed `app_config` + admin user |
| Seeded admin | username `admin`, plain-text password `Admin@123` (auto-upgrades to BCrypt on first login) |

### Tables
`bda_pump_master_tbl`, `bda_pump_location_tbl`, `bda_user_master_tbl`, `pump_status_tbl`, `pump_status_log_tbl`, `pump_daily_summary_tbl`, `complaint_log_tbl`, `app_config_tbl`

### Password handling
- Stored as BCrypt hash (`$2a$12$…`) in `bda_user_master_tbl.password`
- Legacy plain-text rows still accepted at login and silently re-hashed (see `VerifyAndUpgradePassword` in `Pages/Dashboard.cshtml.cs`)
- Admin edit + Self-service Change Password (via `AppPageModel.OnPostChangePasswordAsync`) both hash with `workFactor: 12`

### Remote access for dev
No direct port exposure. Use SSH tunnel in MySQL Workbench:
- Hostname: `127.0.0.1`, Port `3306`
- SSH host: `69.62.80.7:2222`, SSH user: your VPS user, SSH key file

### Pending
- No automated backups — need daily `mysqldump` + off-host rotation

---

## 4. Deploy pipeline

`.github/workflows/deploy.yml` — **manual trigger only** (`workflow_dispatch`).

Split into two jobs:

### `build` (ubuntu-latest)
1. Checkout
2. Setup .NET 8
3. `dotnet publish -c Release -o ./publish`
4. `tar -czf deploy.tar.gz -C ./publish .`
5. Upload artifact `app-build` (retention 3 days)

### `deploy` (needs: build)
1. Download artifact
2. `appleboy/scp-action` → copy `deploy.tar.gz` to `/tmp` on VPS
3. `appleboy/ssh-action` → run deploy script:
   - `systemctl stop asset-monitoring`
   - **Backup** `appsettings.Production.json` to `/tmp` (preserves DB creds)
   - Extract tarball into `/var/www/asset-monitoring`
   - **Restore** `appsettings.Production.json` and `sudo rm -f` the backup
   - `chown -R www-data:www-data`
   - `systemctl start asset-monitoring`
   - Verify via `systemctl is-active` — tail `journalctl` if failed

### GitHub Secrets used
`VPS_HOST`, `VPS_PORT`, `VPS_USER`, `VPS_SSH_KEY`

### Why the backup/restore dance?
`appsettings.Production.json` contains the DB connection string and is **not** in git (the repo copy has placeholder values). Without the backup step, every deploy would wipe the production DB credentials.

---

## 5. App-level conventions

- **Timezone**: server is UTC. All DB timestamps are UTC. Views explicitly convert with `TimeZoneInfo.ConvertTimeFromUtc(... "Asia/Kolkata")`. Do **not** use `.ToLocalTime()` — it resolves to UTC on the server and displays wrong times.
  - Fixed locations: `Pages/Shared/_PumpRunningSummary.cshtml`, `Pages/Admin.cshtml`, `Services/ReportExportService.cs` (Excel + PDF)
- **Cookies**: `CookieSecurePolicy.SameAsRequest` — works on both HTTP (dev) and HTTPS (prod via Nginx + ForwardedHeaders middleware).
- **Logging**: NLog → `/var/www/asset-monitoring/logs/` on VPS. Dev writes to `logs/` in project dir.
- **Session keys**: `UserId` (int), `UserName`, `UserType` (`ADMIN` | `OPERATOR` | `JE`), `Mobile`.
- **Base PageModel**: `asset_monitoring.Models.AppPageModel` — exposes `IsLoggedIn`, `Username`, `UserType`, `UserId`, `LogoutAndRedirect()`, and (new) `OnPostChangePasswordAsync`.

---

## 6. Known pending tasks

Carry-overs that haven't been completed yet:

### Security
- [ ] SSH: set `PasswordAuthentication no`
- [ ] Lock unused `ubuntu` user (has NOPASSWD sudo)
- [ ] `chmod 640` on `appsettings.Production.json`
- [x] Remove duplicate `dcsdms` Nginx config (done 2026-04-21 — also cleaned up orphan `archives-bday` and `asset-monitoring` files in `sites-available`)

### Operational
- [ ] MySQL daily backups (cron + `mysqldump` + rotation)
- [ ] Log rotation for `/var/www/asset-monitoring/logs/`
- [ ] Add swap (none configured)

### Code / repo
- [ ] PR #4 (dead code cleanup) — needs merge

---

## 7. Local dev quick-ref

```bash
cd asset-monitoring
ASPNETCORE_ENVIRONMENT=Development dotnet run --launch-profile http
# → http://localhost:5005
```

- Uses `appsettings.Development.json` for the **local** MySQL connection (developer machine)
- `appsettings.Production.json` in the repo has placeholder `CHANGE_ME` credentials by design — the real one lives only on the VPS
