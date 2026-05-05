#!/usr/bin/env bash
# AttendTrack restore script (Gap 14)
set -euo pipefail

BACKUP_FILE="${1:-}"
DB_NAME="${POSTGRES_DB:-attendtrack}"
DB_USER="${POSTGRES_USER:-attendtrack}"

if [ -z "$BACKUP_FILE" ]; then
    echo "Usage: $0 /backups/daily/attendtrack_YYYYMMDD_HHMMSS.sql.gz"
    exit 1
fi

if [ ! -f "$BACKUP_FILE" ]; then
    echo "ERROR: Backup file not found: $BACKUP_FILE"
    exit 1
fi

echo "WARNING: This will DESTROY and recreate the database '$DB_NAME'."
read -r -p "Type YES to confirm: " CONFIRM

if [ "$CONFIRM" != "YES" ]; then
    echo "Aborted."
    exit 0
fi

echo "Restoring from $BACKUP_FILE..."
dropdb -U "$DB_USER" --if-exists "$DB_NAME"
createdb -U "$DB_USER" "$DB_NAME"
gunzip -c "$BACKUP_FILE" | psql -U "$DB_USER" "$DB_NAME"
echo "Restore complete."
