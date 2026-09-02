#!/usr/bin/env bash
set -Eeuo pipefail

RAIZ_SIMUS="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUTA_ENTORNO="$RAIZ_SIMUS/.env"
RUTA_EJECUCION="$RAIZ_SIMUS/.ejecucion-local"

preparar_directorio_ejecucion() {
  mkdir -p "$RUTA_EJECUCION"
}

cargar_configuracion_local() {
  if [[ ! -f "$RUTA_ENTORNO" ]]; then
    echo "Falta $RUTA_ENTORNO. Copia .env.example como .env y define la contraseña local." >&2
    exit 1
  fi
  set -a
  # shellcheck disable=SC1090
  source "$RUTA_ENTORNO"
  set +a
  if [[ -z "${SIMUS_BASE_DATOS_CONTRASENA_SA:-}" || "${SIMUS_BASE_DATOS_CONTRASENA_SA}" == "<define-una-contrasena-segura>" ]]; then
    echo "Define SIMUS_BASE_DATOS_CONTRASENA_SA en .env antes de iniciar." >&2
    exit 1
  fi
  export SIMUS_PUERTO_BASE_DATOS="${SIMUS_PUERTO_BASE_DATOS:-14335}"
  export ConnectionStrings__Simus="Server=localhost,${SIMUS_PUERTO_BASE_DATOS};Database=simus_nucleo;User Id=sa;Password=${SIMUS_BASE_DATOS_CONTRASENA_SA};TrustServerCertificate=True;Encrypt=False"
}

detener_proceso_registrado() {
  local nombre="$1"
  local archivo_pid="$RUTA_EJECUCION/$nombre.pid"
  [[ -f "$archivo_pid" ]] || return 0
  local pid
  pid="$(<"$archivo_pid")"
  if kill -0 "$pid" 2>/dev/null; then
    echo "Deteniendo $nombre (PID $pid)…"
    kill "$pid" 2>/dev/null || true
    for _ in {1..20}; do
      kill -0 "$pid" 2>/dev/null || break
      sleep .25
    done
    kill -9 "$pid" 2>/dev/null || true
  fi
  rm -f "$archivo_pid"
}

puerto_en_uso() {
  local puerto="$1"
  lsof -nP -iTCP:"$puerto" -sTCP:LISTEN >/dev/null 2>&1
}
