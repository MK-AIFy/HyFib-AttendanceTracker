#!/usr/bin/env bash
# AttendTrack health check script (Gap 10)
# Run via cron every 2 minutes: */2 * * * * /scripts/healthcheck.sh
set -euo pipefail

HEALTH_URL="${HEALTH_URL:-https://localhost/health}"
ALERT_EMAIL="${ALERT_EMAIL:-admin@company.local}"
HIKVISION_IP="${HIKVISION_IP:-192.168.1.50}"
STATE_FILE="${STATE_FILE:-/tmp/attendtrack_hik_offline_since}"

# ── 1. AttendTrack app health check ─────────────────────────────────────────
HTTP_STATUS=$(curl -sk -o /dev/null -w "%{http_code}" "$HEALTH_URL" || echo "000")

if [ "$HTTP_STATUS" != "200" ]; then
    MSG="ALERT: AttendTrack health check failed (HTTP $HTTP_STATUS) at $(date)"
    echo "$MSG" | mail -s "AttendTrack DOWN" "$ALERT_EMAIL" 2>/dev/null || echo "$MSG"
fi

# ── 2. Hikvision device reachability check (with 5-min offline threshold) ───
PING_RESULT=$(curl -sk -o /dev/null -w "%{http_code}" \
    --max-time 5 "http://$HIKVISION_IP/ISAPI/System/deviceInfo" || echo "000")

if [ "$PING_RESULT" == "200" ]; then
    # Device is online — clear offline state file if it existed
    rm -f "$STATE_FILE"
else
    NOW=$(date +%s)
    if [ ! -f "$STATE_FILE" ]; then
        # First failure — record the timestamp
        echo "$NOW" > "$STATE_FILE"
    else
        # Failure is ongoing — check how long
        OFFLINE_SINCE=$(cat "$STATE_FILE")
        OFFLINE_SECS=$(( NOW - OFFLINE_SINCE ))
        if [ "$OFFLINE_SECS" -ge 300 ]; then
            # 5+ minutes offline — send alert
            OFFLINE_MIN=$(( OFFLINE_SECS / 60 ))
            MSG="ALERT: Hikvision device $HIKVISION_IP has been unreachable for ${OFFLINE_MIN} minute(s)"
            echo "$MSG" | mail -s "Hikvision OFFLINE ${OFFLINE_MIN}m" "$ALERT_EMAIL" 2>/dev/null || echo "$MSG"
        fi
    fi
fi
