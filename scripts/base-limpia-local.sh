#!/usr/bin/env bash
set -euo pipefail

# Construye una base aislada para el modelo híbrido vigente. Nunca reutiliza
# PNMC_LOCAL ni borra bases, contenedores o volúmenes.
ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
source "$ROOT_DIR/scripts/load-local-env.sh"

DB_NAME="PNMC_SIMUS_LIMPIO"
DB_PASSWORD="${PNMC_LOCAL_SA_PASSWORD:-}"
DB_PORT="${PNMC_LOCAL_SQL_PORT:-14344}"

if [[ -z "$DB_PASSWORD" ]]; then
  echo "[simus] Falta PNMC_LOCAL_SA_PASSWORD en el entorno local."
  exit 1
fi

# La asignación ocurre después de cargar .env para que el perfil limpio no
# dependa de que ese archivo tenga —o deje de tener— un nombre de base.
export PNMC_LOCAL_DB_NAME="$DB_NAME"

echo "[simus] Preparando la base aislada $DB_NAME..."
"$ROOT_DIR/scripts/local-db-up.sh"

CONNECTION="Server=127.0.0.1,$DB_PORT;Initial Catalog=$DB_NAME;Persist Security Info=False;User ID=sa;Password=$DB_PASSWORD;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=True;Connection Timeout=30;"

echo "[simus] Aplicando el esquema vigente..."
dotnet run --project "$ROOT_DIR/pnmc-api/src/PNMC.Migrador/PNMC.Migrador.csproj" -- \
  migrar --conexion "$CONNECTION"

echo "[simus] Importando DIVIPOLA únicamente si está vacía..."
dotnet run --project "$ROOT_DIR/pnmc-api/src/PNMC.Migrador/PNMC.Migrador.csproj" -- \
  divipola-mgn-2025-si-falta --conexion "$CONNECTION"

echo "[simus] Base limpia preparada: $DB_NAME"
echo "[simus] No se cargaron registros de prueba ni se modificó PNMC_LOCAL."
