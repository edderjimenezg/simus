#!/usr/bin/env bash

# Carga exclusivamente la configuracion local ignorada por Git. Los scripts que
# requieren servicios locales la incluyen para que `cp .env.example .env` sea
# suficiente y no haya credenciales versionadas ni valores secretos por defecto.

if [[ -f "$ROOT_DIR/.env" ]]; then
  set -a
  # shellcheck source=/dev/null
  . "$ROOT_DIR/.env"
  set +a
fi
