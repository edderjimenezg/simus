#!/usr/bin/env bash
set -Eeuo pipefail

RAIZ_SCRIPT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$RAIZ_SCRIPT/entorno-comun.sh"
cargar_configuracion_local
preparar_directorio_ejecucion

if ! docker info >/dev/null 2>&1; then
  echo "Docker Desktop no está disponible. Ábrelo y vuelve a ejecutar este script." >&2
  exit 1
fi

cd "$RAIZ_SIMUS"
echo "Iniciando SQL Server…"
docker compose up -d sqlserver

echo "Preparando esquema y verificando DIVIPOLA…"
preparado=0
for intento in {1..40}; do
  if dotnet run --project src/Simus.Preparar.BaseDatos/Simus.Preparar.BaseDatos.csproj -- \
    --conexion "$ConnectionStrings__Simus" --asegurar-divipola >"$RUTA_EJECUCION/preparacion.log" 2>&1; then
    preparado=1
    break
  fi
  sleep 2
done
if [[ "$preparado" != "1" ]]; then
  echo "La base no terminó de iniciar. Revisa $RUTA_EJECUCION/preparacion.log" >&2
  tail -n 20 "$RUTA_EJECUCION/preparacion.log" >&2 || true
  exit 1
fi

if [[ -f "$RUTA_EJECUCION/api.pid" ]] && kill -0 "$(<"$RUTA_EJECUCION/api.pid")" 2>/dev/null; then
  echo "La API ya fue iniciada por este entorno." >&2
  exit 1
fi
if puerto_en_uso 5050; then
  echo "El puerto 5050 ya está ocupado. Detén la API existente o ejecuta scripts/dev-down.sh." >&2
  exit 1
fi

echo "Iniciando API…"
nohup env ASPNETCORE_ENVIRONMENT=Development ConnectionStrings__Simus="$ConnectionStrings__Simus" \
  dotnet run --project src/Simus.Api/Simus.Api.csproj >"$RUTA_EJECUCION/api.log" 2>&1 &
echo $! >"$RUTA_EJECUCION/api.pid"

if [[ ! -d src/simus-web/node_modules ]]; then
  echo "Faltan dependencias del frontend. Ejecuta: npm --prefix src/simus-web ci" >&2
  "$RAIZ_SCRIPT/dev-down.sh"
  exit 1
fi
if [[ -f "$RUTA_EJECUCION/web.pid" ]] && kill -0 "$(<"$RUTA_EJECUCION/web.pid")" 2>/dev/null; then
  echo "El frontend ya fue iniciado por este entorno." >&2
  exit 1
fi
if puerto_en_uso 4200; then
  echo "El puerto 4200 ya está ocupado. Detén el frontend existente o ejecuta scripts/dev-down.sh." >&2
  "$RAIZ_SCRIPT/dev-down.sh"
  exit 1
fi

echo "Iniciando frontend…"
nohup npm --prefix src/simus-web run start:local >"$RUTA_EJECUCION/web.log" 2>&1 &
echo $! >"$RUTA_EJECUCION/web.pid"

if ! esperar_http "http://localhost:5050/api/salud" "La API"; then
  "$RAIZ_SCRIPT/dev-down.sh"
  exit 1
fi
if ! esperar_http "http://localhost:4200/" "El frontend"; then
  "$RAIZ_SCRIPT/dev-down.sh"
  exit 1
fi

echo "Entorno iniciado."
echo "Portal: http://localhost:4200"
echo "API:    http://localhost:5050/api/salud"
echo "Registros y errores: $RUTA_EJECUCION"
