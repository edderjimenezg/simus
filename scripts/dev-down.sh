#!/usr/bin/env bash
set -Eeuo pipefail

RAIZ_SCRIPT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$RAIZ_SCRIPT/entorno-comun.sh"
preparar_directorio_ejecucion

detener_proceso_registrado web
detener_proceso_registrado api

if docker info >/dev/null 2>&1; then
  echo "Deteniendo SQL Server del núcleo…"
  (cd "$RAIZ_SIMUS" && docker compose down)
else
  echo "Docker Desktop no está disponible; no hay contenedor que detener desde este script." >&2
fi

echo "Entorno local detenido. Se conservaron el volumen SQL y los datos existentes."
