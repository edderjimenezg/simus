#!/usr/bin/env bash
set -euo pipefail

# Comprobacion de salud del entorno local.
#
# La version anterior terminaba cada sonda en `|| true` e imprimia el codigo HTTP sin
# mirarlo. Con el API caida imprimia "000" tres veces y salia 0, asi que nada que la
# invocase (una tarea, un arranque encadenado, el propio desarrollador) podia enterarse
# del fallo. Ahora cada sonda se evalua y el codigo de salida resume el resultado.
#
# Ademas se consulta el API por las dos pilas de bucle local. El frontend llama a
# "localhost", que en Windows resuelve primero a ::1; un API enlazada solo a 127.0.0.1
# escucha, responde a `curl 127.0.0.1` y aun asi devuelve HTTP_0 en el navegador.
# Sondear solo IPv4 dejaba pasar exactamente ese fallo.
#
# Variables: PNMC_LOCAL_WEB_PORT, PNMC_LOCAL_API_PORT, PNMC_SKIP_IPV6_CHECK=1.

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/scripts/load-local-env.sh"

WEB_PORT="${PNMC_LOCAL_WEB_PORT:-4300}"
API_PORT="${PNMC_LOCAL_API_PORT:-8180}"
TIMEOUT="${PNMC_CHECK_TIMEOUT:-5}"

FAILURES=""

add_failure() {
  FAILURES="$FAILURES
  - $1"
}

# Imprime "etiqueta: codigo" y apunta un fallo si el codigo no es 2xx.
probe() {
  local label="$1"
  local url="$2"
  local code

  # `|| echo 000` concatenaba: curl ya imprime "000" al fallar y ademas sale != 0,
  # con lo que la etiqueta acababa siendo "000000". Se separan salida y codigo.
  code="$(curl -sS -o /dev/null -w '%{http_code}' --max-time "$TIMEOUT" "$url" 2>/dev/null)" || true
  if [[ -z "$code" ]]; then
    code="000"
  fi
  printf '%-28s %s\n' "$label:" "$code"

  case "$code" in
    2??) return 0 ;;
    000) add_failure "$label ($url): sin respuesta, no hay nadie escuchando o se agoto el plazo" ;;
    *)   add_failure "$label ($url): responde $code, se esperaba 2xx" ;;
  esac
  return 1
}

IPV4_LIVE_OK=0
IPV6_LIVE_OK=0
IPV6_PROBED=0

echo "== PNMC health check =="
probe "frontend"          "http://127.0.0.1:$WEB_PORT" || true
if probe "api live (IPv4)" "http://127.0.0.1:$API_PORT/health/live"; then IPV4_LIVE_OK=1; fi
probe "api ready (IPv4)"  "http://127.0.0.1:$API_PORT/health/ready" || true

if [[ "${PNMC_SKIP_IPV6_CHECK:-0}" != "1" ]]; then
  IPV6_PROBED=1
  if probe "api live (IPv6)" "http://[::1]:$API_PORT/health/live"; then IPV6_LIVE_OK=1; fi
fi

if [[ -n "$FAILURES" ]]; then
  echo
  echo "[pnmc] COMPROBACION FALLIDA:"
  printf '%s\n' "$FAILURES" | grep -v '^[[:space:]]*$' || true
  # La pista solo aplica cuando IPv4 responde y IPv6 no: ese es el sintoma de un
  # enlace a una sola pila. Si esta todo caido el problema es otro.
  if [[ "$IPV6_PROBED" == "1" && "$IPV4_LIVE_OK" == "1" && "$IPV6_LIVE_OK" == "0" ]]; then
    echo
    echo "[pnmc] Pista: si IPv4 responde y IPv6 no, el API esta enlazada a una sola pila."
    echo "[pnmc]   scripts/api-local.sh debe pasar --urls \"http://127.0.0.1:$API_PORT;http://[::1]:$API_PORT\"."
    echo "[pnmc]   Si esta maquina no tiene IPv6, exporta PNMC_SKIP_IPV6_CHECK=1."
  fi
  exit 1
fi

echo
echo "[pnmc] Todo en verde."
