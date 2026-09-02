#!/usr/bin/env bash
set -Eeuo pipefail

RAIZ_SCRIPT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
"$RAIZ_SCRIPT/dev-down.sh"
"$RAIZ_SCRIPT/dev-up.sh"
