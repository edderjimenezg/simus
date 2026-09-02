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

BASE_PRUEBAS="simus_nucleo_pruebas"
CADENA_PRUEBAS="Server=localhost,${SIMUS_PUERTO_BASE_DATOS};Database=${BASE_PRUEBAS};User Id=sa;Password=${SIMUS_BASE_DATOS_CONTRASENA_SA};TrustServerCertificate=True;Encrypt=False"

echo "Preparando esquema de la base de pruebas ($BASE_PRUEBAS)…"
preparado=0
for intento in {1..40}; do
  if dotnet run --project src/Simus.Preparar.BaseDatos/Simus.Preparar.BaseDatos.csproj -- \
    --conexion "$CADENA_PRUEBAS" >"$RUTA_EJECUCION/preparacion-pruebas.log" 2>&1; then
    preparado=1
    break
  fi
  sleep 2
done
if [[ "$preparado" != "1" ]]; then
  echo "La base de pruebas no terminó de iniciar. Revisa $RUTA_EJECUCION/preparacion-pruebas.log" >&2
  tail -n 20 "$RUTA_EJECUCION/preparacion-pruebas.log" >&2 || true
  exit 1
fi

echo "Ejecutando pruebas de integración contra $BASE_PRUEBAS…"
ConnectionStrings__Simus="$CADENA_PRUEBAS" dotnet test tests/Simus.Api.Tests/Simus.Api.Tests.csproj
