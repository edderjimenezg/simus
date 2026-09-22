#!/usr/bin/env bash
set -euo pipefail

# Este script declara "proyecto apagado". Solo puede decirlo cuando lo ha comprobado.
#
# Antes usaba unicamente `lsof`. Donde `lsof` no existe (Git Bash en Windows, imagenes
# minimas de Linux) el comando fallaba en silencio, `2>/dev/null || true` se tragaba el
# error, la lista de PID salia vacia y el script anunciaba "ya estaba apagado" mientras
# el proceso seguia escuchando. Acto seguido paraba la base. El desarrollador se quedaba
# con el peor estado posible para depurar: API y frontend vivos contra una base muerta,
# devolviendo errores de conexion que parecen un bug de la aplicacion.
#
# Reglas de esta version:
#   1. Ausencia de herramienta NO es ausencia de proceso. Si no se puede mirar, falla.
#   2. Despues de matar se vuelve a mirar. Si sigue vivo, se fuerza; si aun sigue, falla.
#   3. "Proyecto local apagado" solo se imprime con las tres piezas confirmadas abajo.
#   4. El codigo de salida es != 0 si algo quedo vivo o sin verificar.

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/scripts/load-local-env.sh"
COMPOSE_FILE="$ROOT_DIR/docker-compose.local.yml"

WEB_PORT="${PNMC_LOCAL_WEB_PORT:-4300}"
API_PORT="${PNMC_LOCAL_API_PORT:-8180}"

# Lista de pendientes como cadena, no como array: en bash 3.2 (el de macOS) expandir
# ${#ARR[@]} sobre un array vacio con `set -u` aborta el script.
FAILURES=""

add_failure() {
  FAILURES="$FAILURES
  - $1"
}

is_windows_shell() {
  case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*) return 0 ;;
    *) return 1 ;;
  esac
}

# Imprime, uno por linea, los PID que escuchan en el puerto indicado.
# Imprime el testigo UNKNOWN si ninguna herramienta disponible pudo responder.
# Devuelve siempre 0: la respuesta viaja por stdout, nunca por el codigo de salida.
listeners_on_port() {
  local port="$1"
  local out=""

  if command -v lsof >/dev/null 2>&1; then
    out="$(lsof -tiTCP:"$port" -sTCP:LISTEN 2>/dev/null || true)"
    printf '%s\n' "$out" | grep -E '^[0-9]+$' | sort -u || true
    return 0
  fi

  if command -v ss >/dev/null 2>&1; then
    out="$(ss -lntpH "sport = :$port" 2>/dev/null || true)"
    printf '%s\n' "$out" | grep -oE 'pid=[0-9]+' | cut -d= -f2 | sort -u || true
    return 0
  fi

  if is_windows_shell && command -v netstat >/dev/null 2>&1; then
    # netstat de Windows: Proto | Direccion local | Direccion remota | Estado | PID
    # El puerto debe casar como sufijo de la direccion LOCAL, para no confundir
    # 0.0.0.0:18080 con el puerto 8080.
    out="$(netstat -ano -p TCP 2>/dev/null || true)"
    printf '%s\n' "$out" \
      | awk -v p=":$port" 'NF == 5 && $4 == "LISTENING" && index($2, p) == length($2) - length(p) + 1 { print $5 }' \
      | grep -E '^[0-9]+$' | sort -u || true
    return 0
  fi

  if command -v powershell.exe >/dev/null 2>&1; then
    out="$(powershell.exe -NoProfile -Command "try { (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction Stop).OwningProcess } catch { }" 2>/dev/null || true)"
    printf '%s\n' "$out" | tr -d '\r' | grep -E '^[0-9]+$' | sort -u || true
    return 0
  fi

  echo "UNKNOWN"
  return 0
}

pids_on_port() {
  listeners_on_port "$1" | tr '\n' ' ' | sed 's/^ *//; s/ *$//'
}

kill_pid() {
  local pid="$1"
  local mode="$2"

  if is_windows_shell; then
    if [[ "$mode" == "force" ]]; then
      MSYS_NO_PATHCONV=1 taskkill /PID "$pid" /T /F >/dev/null 2>&1 || true
    else
      MSYS_NO_PATHCONV=1 taskkill /PID "$pid" /T >/dev/null 2>&1 || true
    fi
  else
    if [[ "$mode" == "force" ]]; then
      kill -9 "$pid" >/dev/null 2>&1 || true
    else
      kill "$pid" >/dev/null 2>&1 || true
    fi
  fi
}

# 0 si el puerto quedo libre dentro del plazo, 1 si sigue ocupado.
wait_until_free() {
  local port="$1"
  local attempts="$2"
  local i pids

  i=0
  while [[ "$i" -lt "$attempts" ]]; do
    pids="$(pids_on_port "$port")"
    if [[ -z "$pids" ]]; then
      return 0
    fi
    sleep 1
    i=$((i + 1))
  done
  return 1
}

stop_service_on_port() {
  local name="$1"
  local port="$2"
  local pids pid

  pids="$(pids_on_port "$port")"

  if [[ "$pids" == *UNKNOWN* ]]; then
    echo "[pnmc] NO SE PUDO COMPROBAR $name (puerto $port): no hay lsof, ss, netstat ni powershell."
    echo "[pnmc]   No doy por apagado lo que no he podido mirar. Revisa el puerto $port a mano."
    add_failure "$name (puerto $port): sin herramienta para comprobar el puerto"
    return 0
  fi

  if [[ -z "$pids" ]]; then
    echo "[pnmc] $name ya estaba apagado (puerto $port libre, comprobado)."
    return 0
  fi

  echo "[pnmc] Deteniendo $name (PID: $pids)..."
  for pid in $pids; do
    kill_pid "$pid" "graceful"
  done

  if wait_until_free "$port" 10; then
    echo "[pnmc] $name detenido (puerto $port libre, comprobado)."
    return 0
  fi

  pids="$(pids_on_port "$port")"
  echo "[pnmc] $name no respondio al cierre ordenado; forzando (PID: $pids)..."
  for pid in $pids; do
    kill_pid "$pid" "force"
  done

  if wait_until_free "$port" 10; then
    echo "[pnmc] $name detenido a la fuerza (puerto $port libre, comprobado)."
    return 0
  fi

  pids="$(pids_on_port "$port")"
  echo "[pnmc] FALLO: $name sigue escuchando en el puerto $port (PID: $pids)."
  add_failure "$name (puerto $port): sigue vivo tras intentar pararlo, PID $pids"
  return 0
}

stop_database() {
  if ! command -v docker >/dev/null 2>&1 || ! docker info >/dev/null 2>&1; then
    echo "[pnmc] NO SE PUDO COMPROBAR la base: Docker no esta disponible desde este script."
    echo "[pnmc]   Si el contenedor seguia corriendo, sigue corriendo. Abre Docker y repite."
    add_failure "base de datos: Docker no disponible, estado sin verificar"
    return 0
  fi

  echo "[pnmc] Deteniendo base de datos Docker (se conservan los datos)..."
  if ! docker compose -f "$COMPOSE_FILE" stop; then
    echo "[pnmc] FALLO: 'docker compose stop' devolvio error."
    add_failure "base de datos: 'docker compose stop' devolvio error"
    return 0
  fi

  # `ps --status running` no existe en todas las versiones de compose; si el flag no
  # se reconoce la salida seria vacia y daria por parado algo que sigue en marcha.
  # Se pregunta contenedor a contenedor.
  local ids id still
  still=""
  ids="$(docker compose -f "$COMPOSE_FILE" ps -aq 2>/dev/null || true)"
  for id in $ids; do
    if [[ "$(docker inspect -f '{{.State.Running}}' "$id" 2>/dev/null || echo false)" == "true" ]]; then
      still="$still $id"
    fi
  done

  if [[ -n "${still// /}" ]]; then
    echo "[pnmc] FALLO: siguen corriendo contenedores del proyecto tras el stop:$still"
    add_failure "base de datos: contenedores todavia en marcha tras el stop"
    return 0
  fi

  echo "[pnmc] Base de datos detenida (comprobado)."
}

main() {
  echo "[pnmc] Deteniendo servicios locales..."
  stop_service_on_port "frontend Angular" "$WEB_PORT"
  stop_service_on_port "backend API" "$API_PORT"
  stop_database

  if [[ -n "$FAILURES" ]]; then
    echo
    echo "[pnmc] EL PROYECTO NO ESTA APAGADO DEL TODO. Pendiente:"
    printf '%s\n' "$FAILURES" | grep -v '^[[:space:]]*$' || true
    exit 1
  fi

  echo "[pnmc] Proyecto local apagado (frontend, API y base comprobados)."
}

if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
  main "$@"
fi
