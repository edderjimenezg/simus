#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
source "$ROOT_DIR/scripts/load-local-env.sh"
DB_NAME="${PNMC_LOCAL_DB_NAME:-PNMC_LOCAL}"
DB_PASSWORD="${PNMC_LOCAL_SA_PASSWORD:-}"
DB_PORT="${PNMC_LOCAL_SQL_PORT:-14344}"
API_PORT="${PNMC_LOCAL_API_PORT:-8180}"

if [[ -z "$DB_PASSWORD" ]]; then
  echo "[pnmc] Falta PNMC_LOCAL_SA_PASSWORD. Definela en el entorno antes de iniciar la API."
  exit 1
fi

export ASPNETCORE_ENVIRONMENT=Local
export AZURE_SQL_CONNECTION_STRING="Server=127.0.0.1,$DB_PORT;Initial Catalog=$DB_NAME;Persist Security Info=False;User ID=sa;Password=$DB_PASSWORD;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=True;Connection Timeout=30;"

# Las cuentas técnicas locales permiten recorrer los dos paneles sin crear datos de
# producto. Producción conserva el valor seguro por defecto (`false`) de appsettings.
# Esta siembra solo asegura usuarios y roles; no introduce organizaciones, Festivales,
# Ediciones ni contenidos de ejemplo.
export Database__SeedBootstrapUsers=true

# La instalación limpia es una base de verificación, no una forma alternativa
# de cargar datos de ejemplo. Sus estructuras se crean por migración y queda en
# blanco hasta que cada capacidad tenga una siembra explícitamente aprobada.
if [[ "$DB_NAME" == "PNMC_SIMUS_LIMPIO" ]]; then
  export Database__SeedWebConfiguration=false
fi

cd "$ROOT_DIR/pnmc-api"
# Kestrel no hace dual-stack con una IP literal: enlazar solo IPv4 deja [::1] sin
# escuchar. En Windows "localhost" resuelve primero a ::1, y el frontend recibe HTTP_0.
# Hay que enlazar las dos pilas de bucle local de forma explicita.
# Se usa `run` y no `watch`: este script también se ejecuta sin terminal interactiva desde
# dev-up.sh. `watch` puede quedar detenido por una edición brusca y dejar el puerto vacío.
dotnet run --project src/PNMC.Api/PNMC.Api.csproj --no-launch-profile --urls "http://127.0.0.1:$API_PORT;http://[::1]:$API_PORT"
