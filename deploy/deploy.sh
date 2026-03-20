#!/usr/bin/env bash
# =============================================================================
# deploy.sh — Build and deploy BDA Asset Monitoring to the Linux VPS
# Run from your Windows machine via WSL, or from any Linux/Mac with .NET SDK
#
# Usage:
#   bash deploy.sh [VPS_IP_OR_HOSTNAME] [SSH_USER]
#
# Example:
#   bash deploy.sh 123.45.67.89 root
# =============================================================================
set -euo pipefail

SERVER="${1:-your-server-ip}"
SSH_USER="${2:-root}"
APP_DIR="/var/www/asset-monitoring"
PUBLISH_DIR="./publish"

echo "==> Publishing for linux-x64..."
dotnet publish asset-monitoring.csproj \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained false \
    --output "${PUBLISH_DIR}" \
    /p:EnvironmentName=Production

# Remove dev-only files from the publish output
rm -f "${PUBLISH_DIR}/appsettings.Development.json"
rm -f "${PUBLISH_DIR}/nlog-internal.log"

echo "==> Stopping service on server..."
ssh "${SSH_USER}@${SERVER}" "systemctl stop asset-monitoring || true"

echo "==> Syncing files to ${SERVER}:${APP_DIR} ..."
rsync -avz --delete \
    --exclude "logs/" \
    "${PUBLISH_DIR}/" \
    "${SSH_USER}@${SERVER}:${APP_DIR}/"

echo "==> Setting permissions..."
ssh "${SSH_USER}@${SERVER}" "
    chown -R bda:bda ${APP_DIR}
    chmod +x ${APP_DIR}/asset-monitoring
    mkdir -p ${APP_DIR}/logs
    chown bda:bda ${APP_DIR}/logs
"

echo "==> Starting service..."
ssh "${SSH_USER}@${SERVER}" "systemctl start asset-monitoring && systemctl status asset-monitoring --no-pager"

echo ""
echo "======================================================================"
echo " Deploy complete! App is running at http://${SERVER}:5000"
echo " (Nginx should be proxying https://your-domain.com → localhost:5000)"
echo "======================================================================"

# Clean up local publish folder
rm -rf "${PUBLISH_DIR}"
