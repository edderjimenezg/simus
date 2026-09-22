#!/usr/bin/env bash
set -euo pipefail

# Reinicio limpio del proyecto PNMC local. No elimina contenedores, volúmenes ni datos.
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

"$ROOT_DIR/scripts/dev-down.sh"
"$ROOT_DIR/scripts/dev-up.sh"
