#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
source "$ROOT_DIR/scripts/load-local-env.sh"

WEB_URL="http://127.0.0.1:${PNMC_LOCAL_WEB_PORT:-4300}"
API_LIVE_URL="http://127.0.0.1:${PNMC_LOCAL_API_PORT:-8180}/health/live"
LOG_DIR="$ROOT_DIR/tmp/dev"

http_ok() {
  curl -sS -o /dev/null --max-time 3 -w '%{http_code}' "$1" 2>/dev/null \
    | grep -q '^2'
}

start_on_macos() {
  local start_api="$1"
  local start_frontend="$2"

  if [[ "$start_api" == "1" ]]; then
    osascript <<APPLESCRIPT
 tell application "Terminal"
   do script "cd '$ROOT_DIR' && ./scripts/api-local.sh"
   activate
 end tell
APPLESCRIPT
  fi

  if [[ "$start_frontend" == "1" ]]; then
    osascript <<APPLESCRIPT
 tell application "Terminal"
   do script "cd '$ROOT_DIR/pnmc-web' && npm start"
   activate
 end tell
APPLESCRIPT
  fi
}

start_on_windows() {
  local start_api="$1"
  local start_frontend="$2"
  local services=""
  local launcher="$ROOT_DIR/scripts/dev-start-windows.ps1"

  if [[ "$start_api" == "1" && "$start_frontend" == "1" ]]; then
    services="both"
  elif [[ "$start_api" == "1" ]]; then
    services="api"
  elif [[ "$start_frontend" == "1" ]]; then
    services="frontend"
  else
    return 0
  fi

  if ! command -v powershell.exe >/dev/null 2>&1; then
    echo "[pnmc] PowerShell no esta disponible para iniciar procesos en Windows."
    return 1
  fi

  local launcher_arg="$launcher"
  local root_arg="$ROOT_DIR"
  if command -v cygpath >/dev/null 2>&1; then
    launcher_arg="$(cygpath -w "$launcher")"
    root_arg="$(cygpath -w "$ROOT_DIR")"
  fi

  powershell.exe -NoProfile -ExecutionPolicy Bypass \
    -File "$launcher_arg" -RootPath "$root_arg" -Services "$services"
}

start_on_posix() {
  local start_api="$1"
  local start_frontend="$2"
  mkdir -p "$LOG_DIR"

  if [[ "$start_api" == "1" ]]; then
    nohup "$ROOT_DIR/scripts/api-local.sh" \
      >"$LOG_DIR/api.stdout.log" 2>"$LOG_DIR/api.stderr.log" </dev/null &
    echo "$!" > "$LOG_DIR/api.pid"
  fi

  if [[ "$start_frontend" == "1" ]]; then
    (
      cd "$ROOT_DIR/pnmc-web"
      nohup npm start \
        >"$LOG_DIR/frontend.stdout.log" 2>"$LOG_DIR/frontend.stderr.log" </dev/null &
      echo "$!" > "$LOG_DIR/frontend.pid"
    )
  fi
}

wait_until_ready() {
  local attempt=0
  echo "[pnmc] Esperando a que frontend, API y base queden listos..."

  while [[ "$attempt" -lt 90 ]]; do
    if PNMC_CHECK_TIMEOUT=2 "$ROOT_DIR/scripts/dev-check.sh" >/dev/null 2>&1; then
      return 0
    fi
    sleep 2
    attempt=$((attempt + 1))
  done

  echo "[pnmc] Los servicios no quedaron listos dentro de 180 segundos."
  "$ROOT_DIR/scripts/dev-check.sh" || true
  if [[ -d "$LOG_DIR" ]]; then
    echo "[pnmc] Revisa los registros en: $LOG_DIR"
  fi
  return 1
}

if ! command -v docker >/dev/null 2>&1; then
  echo "[pnmc] Docker CLI no esta disponible. Instala o abre Docker Desktop."
  exit 1
fi

if ! docker info >/dev/null 2>&1; then
  if [[ "$(uname)" == "Darwin" ]] && command -v open >/dev/null 2>&1; then
    echo "[pnmc] Iniciando Docker Desktop..."
    open -a Docker
    echo "[pnmc] Esperando a que Docker Desktop este listo..."
    for _ in {1..90}; do
      if docker info >/dev/null 2>&1; then
        break
      fi
      sleep 2
    done
  fi
fi

if ! docker info >/dev/null 2>&1; then
  echo "[pnmc] Docker Desktop no esta listo. Abrelo y vuelve a ejecutar el script."
  exit 1
fi

echo "[pnmc] Preparando base de datos local..."
"$ROOT_DIR/scripts/local-db-up.sh"

# Un servicio disponible sobre una base con tablas pero sin catálogo territorial no está listo:
# el alta externa muestra selects vacíos y termina rechazando cualquier sede. El esquema y
# DIVIPOLA se preparan antes de arrancar API/frontend. La fuente oficial se importa solamente
# sobre catálogo vacío: iniciar el entorno no puede sustituir datos locales ya verificados.
"$ROOT_DIR/scripts/schema-local.sh" migrar
"$ROOT_DIR/scripts/schema-local.sh" divipola-mgn-2025-si-falta

START_API=1
START_FRONTEND=1

if http_ok "$API_LIVE_URL"; then
  START_API=0
  echo "[pnmc] Backend API ya estaba activo."
fi

if http_ok "$WEB_URL"; then
  START_FRONTEND=0
  echo "[pnmc] Frontend Angular ya estaba activo."
fi

if [[ "$START_API" == "1" || "$START_FRONTEND" == "1" ]]; then
  case "$(uname -s)" in
    Darwin)
      echo "[pnmc] Iniciando servicios en Terminal de macOS..."
      start_on_macos "$START_API" "$START_FRONTEND"
      ;;
    MINGW*|MSYS*|CYGWIN*)
      echo "[pnmc] Iniciando servicios en segundo plano para Windows..."
      start_on_windows "$START_API" "$START_FRONTEND"
      ;;
    *)
      echo "[pnmc] Iniciando servicios en segundo plano..."
      start_on_posix "$START_API" "$START_FRONTEND"
      ;;
  esac
fi

wait_until_ready

echo "[pnmc] Servicios iniciados y comprobados"
echo "Frontend: $WEB_URL"
echo "API: http://localhost:${PNMC_LOCAL_API_PORT:-8180}/swagger"
