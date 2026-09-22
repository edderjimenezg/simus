#!/usr/bin/env bash
set -euo pipefail

# ---------------------------------------------------------------------------
# Siembra de demostración del Mapa Ecosistémico.
#
# POR QUE ES UN GUION APARTE Y NO UNA LINEA EN `seed-local-db.sh`. Aquella lista está declarada,
# con esas palabras, como «semillas de referencia y soporte local; no contiene datos demostrativos
# ni módulos retirados del perfil vigente». Es una separación deliberada: una base recién levantada
# queda con los catálogos, los roles y DIVIPOLA, y sin un solo registro inventado. Meter aquí
# ciento y pico de Festivales de mentira borraría esa distinción para siempre, y el día que alguien
# dude de si un registro es real tendría que leerse el guion para saberlo.
#
# Se pide a mano, se sabe lo que se pide, y se puede deshacer: el guion SQL retira lo que él mismo
# sembró antes de volver a sembrar, identificándolo por su procedencia declarada.
#
# NO PUEDE TOCAR PRODUCCION: `pnmc-database/seed/` no lo aplica `migrar`, sólo los guiones locales.
# ---------------------------------------------------------------------------

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
source "$ROOT_DIR/scripts/load-local-env.sh"
DB_NAME="${PNMC_LOCAL_DB_NAME:-PNMC_LOCAL}"
DB_PASSWORD="${PNMC_LOCAL_SA_PASSWORD:-}"
DB_PORT="${PNMC_LOCAL_SQL_PORT:-14344}"
SQL_CONTAINER="${PNMC_LOCAL_SQL_CONTAINER:-simus-desarrollo-sqlserver}"
SQL_TOOLS_IMAGE="${PNMC_LOCAL_SQL_TOOLS_IMAGE:-mcr.microsoft.com/mssql-tools}"
SEMILLA="$ROOT_DIR/pnmc-database/seed/V20260912_01__demostracion_mapa_ecosistemico.sql"

if [[ -z "$DB_PASSWORD" ]]; then
  echo "[pnmc-demo] Falta PNMC_LOCAL_SA_PASSWORD." >&2
  exit 1
fi

# EL MISMO CINTURON QUE `seed-local-db.sh`, y por el mismo motivo: `git worktree` aísla el código y
# no la base. Un árbol atrasado escribiendo en la base compartida revivió esquema retirado el 24 de
# agosto de 2026 sin que ninguna prueba lo viera.
if git -C "$ROOT_DIR" rev-parse --git-dir >/dev/null 2>&1; then
  GIT_DIR_PROPIO="$(git -C "$ROOT_DIR" rev-parse --absolute-git-dir)"
  GIT_DIR_COMUN="$(git -C "$ROOT_DIR" rev-parse --path-format=absolute --git-common-dir)"
  if [ "$GIT_DIR_PROPIO" != "$GIT_DIR_COMUN" ] \
    && [ "$DB_NAME" = "PNMC_LOCAL" ] \
    && [ "${PNMC_PERMITIR_SIEMBRA_DESDE_WORKTREE:-0}" != "1" ]; then
    echo "[pnmc-demo] ABORTADO: arbol enlazado sobre la base compartida PNMC_LOCAL." >&2
    echo "[pnmc-demo]   Siembra una base propia:  PNMC_LOCAL_DB_NAME=PNMC_MI_RAMA $0" >&2
    exit 1
  fi
fi

SQL_NETWORK="$(docker inspect "$SQL_CONTAINER" --format '{{range $network, $_ := .NetworkSettings.Networks}}{{$network}}{{end}}' 2>/dev/null || true)"

ejecutar() {
  # `-I` enciende QUOTED_IDENTIFIER y `-b` hace que un error devuelva codigo distinto de cero; sin
  # el segundo, una siembra rota terminaba en verde.
  #
  # SIN `-f 65001`: el `sqlcmd` de Homebrew es go-sqlcmd, que lee UTF-8 de serie y RECHAZA esa
  # opcion —«'f': Unknown Option»—. `seed-local-db.sh` la pasa siempre y por eso no funciona en esta
  # maquina por el carril del anfitrion; aqui se omite y las tildes llegan igual.
  if command -v sqlcmd >/dev/null 2>&1 \
    && sqlcmd -S "127.0.0.1,$DB_PORT" -U sa -P "$DB_PASSWORD" -C -Q "SELECT 1" >/dev/null 2>&1; then
    sqlcmd -S "127.0.0.1,$DB_PORT" -U sa -P "$DB_PASSWORD" -d "$DB_NAME" -C -I -b -i "$1"
  elif docker exec "$SQL_CONTAINER" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$DB_PASSWORD" -C -Q "SELECT 1" >/dev/null 2>&1; then
    docker exec -i "$SQL_CONTAINER" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$DB_PASSWORD" -d "$DB_NAME" -C -I -b < "$1"
  else
    # La imagen `mssql-tools` (no la 18) no admite `-f`; sí `-C` e `-I`, que son los que importan.
    docker run --rm -i --platform linux/amd64 --network host "$SQL_TOOLS_IMAGE" \
      /opt/mssql-tools/bin/sqlcmd -S "127.0.0.1,$DB_PORT" -U sa -P "$DB_PASSWORD" -d "$DB_NAME" -C -I -b < "$1"
  fi
}

echo "[pnmc-demo] Sembrando la demostración del mapa en $DB_NAME..."
CON_GO="$(mktemp -t pnmc-demo-XXXXXX.sql)"
{ cat "$SEMILLA"; echo ""; echo "GO"; } > "$CON_GO"
trap 'rm -f "$CON_GO"' EXIT
ejecutar "$CON_GO"
echo "[pnmc-demo] Listo. Para retirarla, vuelve a correr el guion: retira lo suyo antes de sembrar."
