#!/usr/bin/env bash
set -euo pipefail

# Actualización explícita del catálogo territorial. Nunca se ejecuta al cargar un formulario:
# el navegador lee exclusivamente la copia local conciliada contra el MGN 2025 del DANE.
ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"

"$ROOT_DIR/scripts/local-db-up.sh"
"$ROOT_DIR/scripts/schema-local.sh" migrar
"$ROOT_DIR/scripts/schema-local.sh" divipola-mgn-2025
