#!/usr/bin/env bash
# Pruebas de regresion del entorno local (carril entorno-scripts).
#
# No necesitan bats ni ninguna dependencia externa: bash, curl y node (que ya hace falta
# para el frontend). No compilan nada y NO tocan el contenedor simus-desarrollo-sqlserver:
# todo lo que arrancan son listeners de usar y tirar en puertos altos (398xx) y un
# proyecto docker-compose ficticio con otro nombre.
#
# Uso:  bash scripts/tests/run-script-tests.sh
# Sale 0 si todo pasa, 1 si algo falla.

set -uo pipefail

TESTS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_DIR="$(cd "$TESTS_DIR/.." && pwd)"
ROOT_DIR="$(cd "$SCRIPTS_DIR/.." && pwd)"

DEV_DOWN="$SCRIPTS_DIR/dev-down.sh"
DEV_CHECK="$SCRIPTS_DIR/dev-check.sh"
API_LOCAL="$SCRIPTS_DIR/api-local.sh"
DEV_UP="$SCRIPTS_DIR/dev-up.sh"
WINDOWS_STARTER="$SCRIPTS_DIR/dev-start-windows.ps1"
COMPOSE="$ROOT_DIR/docker-compose.local.yml"

PASS=0
FAIL=0
SKIP=0
FAILED_NAMES=""

ok() {
  PASS=$((PASS + 1))
  printf '  OK    %s\n' "$1"
}

bad() {
  FAIL=$((FAIL + 1))
  FAILED_NAMES="$FAILED_NAMES
  - $1"
  printf '  FALLA %s\n' "$1"
  if [[ -n "${2:-}" ]]; then
    printf '        %s\n' "$2"
  fi
}

skip() {
  SKIP=$((SKIP + 1))
  printf '  OMITE %s (%s)\n' "$1" "${2:-}"
}

assert_contains() {
  if printf '%s' "$2" | grep -qF -- "$3"; then
    ok "$1"
  else
    bad "$1" "no aparece: $3"
  fi
}

assert_not_contains() {
  if printf '%s' "$2" | grep -qF -- "$3"; then
    bad "$1" "aparece y no deberia: $3"
  else
    ok "$1"
  fi
}

assert_eq() {
  if [[ "$2" == "$3" ]]; then
    ok "$1"
  else
    bad "$1" "esperaba [$3], obtuve [$2]"
  fi
}

yes_no() {
  if [[ "$1" -ne 0 ]]; then echo si; else echo no; fi
}

# ---- listeners de usar y tirar ---------------------------------------------

NODE_OK=0
if command -v node >/dev/null 2>&1; then NODE_OK=1; fi

NODE_SRV_DUAL="const h=require(\"http\").createServer(function(q,s){s.writeHead(200);s.end(\"ok\")}); h.listen(PORT,function(){}); setTimeout(function(){process.exit(0)},40000);"
NODE_SRV_V4="const h=require(\"http\").createServer(function(q,s){s.writeHead(200);s.end(\"ok\")}); h.listen(PORT,\"127.0.0.1\",function(){}); setTimeout(function(){process.exit(0)},40000);"

start_listener() {
  local port="$1"
  local mode="${2:-dual}"
  local src
  if [[ "$mode" == "dual" ]]; then
    src="${NODE_SRV_DUAL//PORT/$port}"
  else
    src="${NODE_SRV_V4//PORT/$port}"
  fi

  node -e "$src" >/dev/null 2>&1 &

  local i
  for i in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15; do
    if curl -sS -o /dev/null --max-time 1 "http://127.0.0.1:$port" 2>/dev/null; then
      return 0
    fi
    sleep 0.5
  done
  return 1
}

probe_pids() {
  # Reutiliza la deteccion del propio dev-down.sh, que es lo que se quiere probar.
  ( source "$DEV_DOWN" >/dev/null 2>&1; pids_on_port "$1" )
}

stop_listener() {
  local p
  for p in $(probe_pids "$1"); do
    case "$(uname -s)" in
      MINGW*|MSYS*|CYGWIN*) MSYS_NO_PATHCONV=1 taskkill /PID "$p" /T /F >/dev/null 2>&1 || true ;;
      *) kill -9 "$p" >/dev/null 2>&1 || true ;;
    esac
  done
}

# ---- dev-down.sh: deteccion ------------------------------------------------

echo "== dev-down.sh: deteccion de procesos vivos =="

if [[ "$NODE_OK" == "0" ]]; then
  skip "deteccion de listener vivo" "node no disponible"
  skip "matar y comprobar" "node no disponible"
else
  PORT_A=39841
  if start_listener "$PORT_A" v4; then
    FOUND="$(probe_pids "$PORT_A")"
    if printf '%s' "$FOUND" | grep -qE '^[0-9]+$'; then
      ok "encuentra el PID que escucha en $PORT_A"
    else
      bad "encuentra el PID que escucha en $PORT_A" "devolvio [$FOUND]"
    fi

    # PNMC-053: la sonda antigua solo consultaba lsof. Donde lsof no existe devolvia
    # vacio y el script anunciaba "ya estaba apagado" sobre un proceso vivo.
    if command -v lsof >/dev/null 2>&1; then
      skip "la sonda antigua (solo lsof) era ciega aqui" "esta maquina si tiene lsof"
    else
      LSOF_ONLY="$(lsof -tiTCP:"$PORT_A" -sTCP:LISTEN 2>/dev/null || true)"
      assert_eq "la sonda antigua (solo lsof) no veia este proceso" "$LSOF_ONLY" ""
    fi

    OUT="$( ( source "$DEV_DOWN" >/dev/null 2>&1
              stop_service_on_port "listener de prueba" "$PORT_A"
              printf 'PENDIENTES=[%s]\n' "$FAILURES" ) 2>&1 )"
    assert_contains "confirma el puerto libre despues de matar" "$OUT" "comprobado"
    assert_contains "no deja ningun pendiente" "$OUT" "PENDIENTES=[]"
    assert_eq "el puerto $PORT_A queda realmente libre" "$(probe_pids "$PORT_A")" ""
    stop_listener "$PORT_A"
  else
    skip "deteccion de listener vivo" "no arranco el listener de prueba"
    skip "matar y comprobar" "no arranco el listener de prueba"
  fi

  PORT_B=39842
  if start_listener "$PORT_B" v4; then
    UNK="$( ( PATH="/usr/bin:/bin"; source "$DEV_DOWN" >/dev/null 2>&1; pids_on_port "$PORT_B" ) )"
    if [[ "$UNK" == *UNKNOWN* ]]; then
      ok "sin lsof/ss/netstat/powershell responde UNKNOWN, no vacio"
    elif printf '%s' "$UNK" | grep -qE '^[0-9]+$'; then
      skip "rama UNKNOWN" "el PATH reducido aun conserva alguna herramienta"
    else
      bad "sin lsof/ss/netstat/powershell responde UNKNOWN, no vacio" "devolvio [$UNK]"
    fi
    stop_listener "$PORT_B"
  else
    skip "rama UNKNOWN" "no arranco el listener de prueba"
  fi
fi

# ---- dev-down.sh: codigo de salida -----------------------------------------

echo
echo "== dev-down.sh: codigo de salida =="

FAKE_ROOT="$(mktemp -d 2>/dev/null || echo "${TMPDIR:-/tmp}/pnmc-devdown-test.$$")"
mkdir -p "$FAKE_ROOT/scripts"
cp "$DEV_DOWN" "$FAKE_ROOT/scripts/dev-down.sh"
cp "$SCRIPTS_DIR/load-local-env.sh" "$FAKE_ROOT/scripts/load-local-env.sh"
printf '%s\n' \
  'name: pnmc-devdown-selftest' \
  'services:' \
  '  nada:' \
  '    image: alpine:3' \
  '    command: ["true"]' \
  > "$FAKE_ROOT/docker-compose.local.yml"

OUT_RED="$( ( PATH="/usr/bin:/bin"; bash "$FAKE_ROOT/scripts/dev-down.sh" ) 2>&1 )"
RC_RED=$?
assert_eq "sale != 0 cuando no puede comprobar los puertos" "$(yes_no "$RC_RED")" "si"
assert_contains "dice que no pudo comprobar" "$OUT_RED" "NO SE PUDO COMPROBAR"
assert_not_contains "no afirma 'Proyecto local apagado' sin haberlo visto" "$OUT_RED" "Proyecto local apagado"

if command -v docker >/dev/null 2>&1 && docker info >/dev/null 2>&1; then
  OUT_GREEN="$(PNMC_LOCAL_WEB_PORT=39843 PNMC_LOCAL_API_PORT=39844 bash "$FAKE_ROOT/scripts/dev-down.sh" 2>&1)"
  RC_GREEN=$?
  assert_eq "sale 0 cuando todo esta comprobadamente abajo" "$RC_GREEN" "0"
  assert_contains "solo entonces declara el apagado" "$OUT_GREEN" "Proyecto local apagado"
  docker compose -f "$FAKE_ROOT/docker-compose.local.yml" down >/dev/null 2>&1 || true
else
  skip "caso verde de dev-down" "docker no disponible"
  skip "mensaje de apagado confirmado" "docker no disponible"
fi
rm -rf "$FAKE_ROOT" 2>/dev/null || true

# ---- dev-check.sh ----------------------------------------------------------

echo
echo "== dev-check.sh: codigo de salida =="

OUT="$(PNMC_LOCAL_WEB_PORT=39851 PNMC_LOCAL_API_PORT=39852 PNMC_CHECK_TIMEOUT=2 bash "$DEV_CHECK" 2>&1)"
RC=$?
assert_eq "sale != 0 con los servicios caidos" "$(yes_no "$RC")" "si"
assert_not_contains "no concatena codigos tipo 000000" "$OUT" "000000"

if [[ "$NODE_OK" == "1" ]]; then
  PORT_C=39853
  if start_listener "$PORT_C" dual; then
    OUT="$(PNMC_LOCAL_WEB_PORT=$PORT_C PNMC_LOCAL_API_PORT=$PORT_C PNMC_CHECK_TIMEOUT=3 bash "$DEV_CHECK" 2>&1)"
    RC=$?
    assert_eq "sale 0 con el servicio vivo en las dos pilas" "$RC" "0"
    stop_listener "$PORT_C"
  else
    skip "caso verde de dev-check" "no arranco el listener dual"
  fi

  PORT_D=39854
  if start_listener "$PORT_D" v4; then
    OUT="$(PNMC_LOCAL_WEB_PORT=$PORT_D PNMC_LOCAL_API_PORT=$PORT_D PNMC_CHECK_TIMEOUT=3 bash "$DEV_CHECK" 2>&1)"
    RC=$?
    assert_eq "detecta el enlace a una sola pila" "$(yes_no "$RC")" "si"
    assert_contains "explica el sintoma de una sola pila" "$OUT" "una sola pila"
    stop_listener "$PORT_D"
  else
    skip "deteccion de una sola pila" "no arranco el listener IPv4"
  fi
else
  skip "caso verde de dev-check" "node no disponible"
  skip "deteccion de una sola pila" "node no disponible"
fi

# ---- contratos estaticos ---------------------------------------------------

echo
echo "== docker-compose.local.yml y api-local.sh =="

COMPOSE_TXT="$(tr -d '\r' < "$COMPOSE" 2>/dev/null || true)"
# El comando del healthcheck puede ocupar varias lineas (escalar plegado de YAML),
# asi que se toma el bloque entero sin comentarios, no solo las lineas con
# palabras clave: filtrar por "sqlcmd" dejaba fuera la linea con la consulta.
HC_BLOCK="$(printf '%s\n' "$COMPOSE_TXT" | sed -n '/healthcheck:/,/interval:/p' | grep -v '^[[:space:]]*#' || true)"

assert_not_contains "el healthcheck ya no es 'pgrep sqlservr'" "$HC_BLOCK" "pgrep"
assert_contains "el healthcheck comprueba que el motor escucha" "$HC_BLOCK" "/dev/tcp/127.0.0.1/1433"
assert_contains "la comprobacion real queda en el arranque local" "$(tr -d '\r' < "$ROOT_DIR/scripts/local-db-up.sh")" "SELECT 1"

PORT_LINE="$(printf '%s\n' "$COMPOSE_TXT" | grep -E '^[[:space:]]*-[[:space:]]*"[^"]*:1433"' || true)"
assert_contains "SQL Server se publica solo en bucle local" "$PORT_LINE" "127.0.0.1:"

API_TXT="$(tr -d '\r' < "$API_LOCAL" 2>/dev/null || true)"
URLS_LINE="$(printf '%s\n' "$API_TXT" | grep -- '--urls' | grep -v '^[[:space:]]*#' || true)"
assert_contains "api-local.sh enlaza la pila IPv4" "$URLS_LINE" "http://127.0.0.1:\$API_PORT"
assert_contains "api-local.sh enlaza la pila IPv6" "$URLS_LINE" "http://[::1]:\$API_PORT"

DEV_UP_TXT="$(tr -d '\r' < "$DEV_UP" 2>/dev/null || true)"
WINDOWS_STARTER_TXT="$(tr -d '\r' < "$WINDOWS_STARTER" 2>/dev/null || true)"
assert_contains "dev-up detecta Windows" "$DEV_UP_TXT" "MINGW*|MSYS*|CYGWIN*"
assert_contains "dev-up espera el health check completo" "$DEV_UP_TXT" "wait_until_ready"
assert_contains "Windows inicia procesos ocultos" "$WINDOWS_STARTER_TXT" "-WindowStyle Hidden"
assert_contains "Windows configura el entorno Local" "$WINDOWS_STARTER_TXT" "ASPNETCORE_ENVIRONMENT = 'Local'"
assert_contains "Windows configura la conexion SQL local" "$WINDOWS_STARTER_TXT" "AZURE_SQL_CONNECTION_STRING"

# ---- resumen ---------------------------------------------------------------

echo
echo "== resumen =="
printf 'pasan: %s   fallan: %s   omitidas: %s\n' "$PASS" "$FAIL" "$SKIP"
if [[ "$FAIL" -gt 0 ]]; then
  printf 'fallos:%s\n' "$FAILED_NAMES"
  exit 1
fi
exit 0
