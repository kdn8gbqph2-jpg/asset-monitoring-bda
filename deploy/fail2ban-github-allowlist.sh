#!/usr/bin/env bash
#
# fail2ban-github-allowlist.sh
# -----------------------------
# Whitelists GitHub Actions runner IP ranges in fail2ban so the CI deploy
# (which connects from GitHub's rotating shared IPs) is never banned by the
# sshd/nginx jails. Symptom it fixes: deploy step failing with
#   ssh: connect to host ... port 2222: Connection timed out
# i.e. the runner IP was DROPped by UFW because fail2ban banned it.
#
# What it does:
#   1. Fetches the current GitHub Actions ranges from https://api.github.com/meta
#   2. Keeps the IPv4 CIDRs (the VPS is IPv4-only, so that's all it ever sees)
#   3. Writes them — plus your own admin IPs — to a fail2ban [DEFAULT] ignoreip
#      override in /etc/fail2ban/jail.d/, so ALL jails skip those IPs
#   4. Reloads fail2ban and clears any currently-active bans
#
# GitHub changes these ranges periodically — run this WEEKLY via cron:
#   sudo crontab -e
#   17 4 * * 0  /usr/local/sbin/fail2ban-github-allowlist.sh >/var/log/f2b-gh-allowlist.log 2>&1
#
# Usage:  sudo ./fail2ban-github-allowlist.sh
#
set -euo pipefail

META_URL="https://api.github.com/meta"
OUT="/etc/fail2ban/jail.d/github-actions-ignoreip.local"

# Admin IPs that must NEVER be banned. Loopback is always included; add your
# office/static IP here so a fat-fingered SSH login can't lock you out.
# (This becomes the sshd/nginx jails' ignoreip, so list anything you rely on.)
ADMIN_IPS="127.0.0.1/8 ::1"

# ── Preconditions ────────────────────────────────────────────────────────────
if [ "$(id -u)" -ne 0 ]; then echo "Run with sudo." >&2; exit 1; fi
command -v jq   >/dev/null 2>&1 || { echo "Missing jq.   Install: apt-get install -y jq";   exit 1; }
command -v curl >/dev/null 2>&1 || { echo "Missing curl. Install: apt-get install -y curl"; exit 1; }

# ── Fetch GitHub Actions IPv4 ranges ─────────────────────────────────────────
echo "Fetching GitHub Actions ranges from ${META_URL} ..."
mapfile -t GH_RANGES < <(curl -fsSL "$META_URL" | jq -r '.actions[] | select(contains(":") | not)')
if [ "${#GH_RANGES[@]}" -eq 0 ]; then
  echo "ERROR: fetched zero IPv4 ranges — aborting (leaving current config untouched)." >&2
  exit 1
fi
echo "  got ${#GH_RANGES[@]} IPv4 CIDRs."

# ── Write the fail2ban override ──────────────────────────────────────────────
tmp="$(mktemp)"
{
  echo "# AUTO-GENERATED $(date -u +%FT%TZ) by fail2ban-github-allowlist.sh — do not edit."
  echo "# Whitelists GitHub Actions runner IPv4 ranges so CI deploys aren't banned."
  echo "[DEFAULT]"
  printf 'ignoreip = %s %s\n' "$ADMIN_IPS" "${GH_RANGES[*]}"
} > "$tmp"
mv "$tmp" "$OUT"
chmod 644 "$OUT"
echo "Wrote ${OUT}"

# ── Apply ────────────────────────────────────────────────────────────────────
fail2ban-client reload
# Clear any bans that are active right now (so an already-banned runner IP is
# released immediately). --unban --all is fail2ban >= 0.11; fall back to restart.
fail2ban-client unban --all 2>/dev/null || systemctl restart fail2ban

echo "Done. GitHub Actions IPv4 ranges are now in the fail2ban ignore list and active bans were cleared."
