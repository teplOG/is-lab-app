#!/usr/bin/env bash
set -euo pipefail

STACK_DIR=/home/deployer/deploy/is-stack
BACKUP_DIR=/opt/backups/mssql
NETWORK=is-stack_is-net
KEEP=5

source "$STACK_DIR/.env"

NAME="IsLabDb_$(date +%Y%m%d_%H%M%S).bak"

docker run --rm --network "$NETWORK" mcr.microsoft.com/mssql-tools \
  /opt/mssql-tools/bin/sqlcmd -S mssql -U sa -P "$SA_PASSWORD" \
  -Q "BACKUP DATABASE IsLabDb TO DISK = N'/var/opt/mssql/backup/$NAME' WITH INIT, COMPRESSION;"

ls -1t "$BACKUP_DIR"/IsLabDb_[0-9]*.bak 2>/dev/null | tail -n +$((KEEP + 1)) | while read -r old; do
    echo "удаляю устаревшую копию: $(basename "$old")"
    rm -f "$old"
done

echo "хранится копий: $(ls -1 "$BACKUP_DIR"/IsLabDb_[0-9]*.bak 2>/dev/null | wc -l) (политика: последние $KEEP)"
