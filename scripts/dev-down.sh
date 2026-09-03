#!/usr/bin/env bash
set -Eeuo pipefail

RAIZ_SCRIPT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$RAIZ_SCRIPT/entorno-comun.sh"
preparar_directorio_ejecucion

purga_datos=0
if [[ "${1:-}" == "--purge-data" ]]; then
  purga_datos=1
elif [[ $# -gt 0 ]]; then
  echo "Uso: scripts/dev-down.sh [--purge-data]" >&2
  exit 2
fi

detener_proceso_registrado web
detener_proceso_registrado api

if docker info >/dev/null 2>&1; then
  echo "Deteniendo SQL Server del núcleo…"
  if [[ "$purga_datos" == "1" ]]; then
    echo "Eliminando también el volumen SQL exclusivo del núcleo…"
    (cd "$RAIZ_SIMUS" && docker compose down --volumes --remove-orphans)
  else
    (cd "$RAIZ_SIMUS" && docker compose down --remove-orphans)
  fi
else
  echo "Docker Desktop no está disponible; no hay contenedor que detener desde este script." >&2
fi

if [[ "$purga_datos" == "1" ]]; then
  echo "Entorno local desmontado y volumen SQL eliminado."
else
  echo "Entorno local detenido. Se conservaron el volumen SQL y los datos existentes."
fi
