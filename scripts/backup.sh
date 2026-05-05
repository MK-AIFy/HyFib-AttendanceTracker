#!/usr/bin/env bash
# AttendTrack daily backup script (Gap 9)
set -euo pipefail

BACKUP_DIR="/backups"
DAILY_DIR="$BACKUP_DIR/daily"
WEEKLY_DIR="$BACKUP_DIR/weekly"
FACE_DIR="/app/face-captures"
DB_NAME="${POSTGRES_DB:-attendtrack}"
DB_USER="${POSTGRES_USER:-attendtrack}"

DATE=$(date +%Y%m%d_%H%M%S)
DOW=$(date +%u)   # 1=Monday ... 7=Sunday

mkdir -p "$DAILY_DIR" "$WEEKLY_DIR"

echo "[$DATE] Starting backup..."

# PostgreSQL dump
pg_dump -U "$DB_USER" "$DB_NAME" | gzip > "$DAILY_DIR/attendtrack_${DATE}.sql.gz"

# Face captures tarball
tar -czf "$DAILY_DIR/face-captures_${DATE}.tar.gz" -C "$FACE_DIR" . 2>/dev/null || true

# Weekly copy on Sunday
if [ "$DOW" -eq 7 ]; then
    cp "$DAILY_DIR/attendtrack_${DATE}.sql.gz" "$WEEKLY_DIR/"
fi

# Rotation: keep 30 daily, 12 weekly
find "$DAILY_DIR" -name "*.gz" -mtime +30 -delete
find "$WEEKLY_DIR" -name "*.gz" -mtime +84 -delete

echo "[$DATE] Backup complete."
