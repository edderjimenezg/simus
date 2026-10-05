#!/usr/bin/env bash
set -euo pipefail

# Valida la línea de migraciones Flyway de la base SIMUS del Ministerio en un SQL Server 2022
# desechable (Docker):
#
#   A. Instalación completa: base vacía + tablas propias de SIMUS → V1…Vn.
#   B. Actualización: base en el estado inicial (la exportación recibida, si se indica) → baseline
#      en V1 → V2…Vn, y traslado de prueba de todos los festivales heredados.
#
# Ambos escenarios deben pasar verificar_estado_objetivo.sql y producir la misma huella de
# esquema. Con --comparar-con-desarrollo, además, las tablas comunes deben tener la misma huella
# que la cadena DbUp de pnmc-database/schema.
#
# Uso: scripts/validar-simus-ministerio.sh [--estado-inicial <exportacion.sql>] [--comparar-con-desarrollo]

RAIZ="$(cd "$(dirname "$0")/.." && pwd)"
LINEA="$RAIZ/pnmc-database/simus-ministerio"
CONTENEDOR="simus-ministerio-validacion"
PUERTO="${SIMUS_VALIDACION_PUERTO:-14399}"
IMAGEN_SQL="mcr.microsoft.com/mssql/server:2022-latest"
IMAGEN_FLYWAY="flyway/flyway:latest"
TRABAJO="$(mktemp -d)"
trap 'rm -rf "$TRABAJO"' EXIT

estado_inicial=""
comparar_desarrollo=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --estado-inicial) estado_inicial="$2"; shift 2 ;;
    --comparar-con-desarrollo) comparar_desarrollo=1; shift ;;
    *) echo "Argumento desconocido: $1" >&2; exit 2 ;;
  esac
done

for herramienta in docker sqlcmd; do
  command -v "$herramienta" >/dev/null || { echo "Falta $herramienta." >&2; exit 2; }
done

if ! docker ps --format '{{.Names}}' | grep -qx "$CONTENEDOR"; then
  docker rm -f "$CONTENEDOR" >/dev/null 2>&1 || true
  CLAVE="Val1d-$(openssl rand -hex 12)"
  docker run -d --name "$CONTENEDOR" --platform linux/amd64 -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD="$CLAVE" \
    -p "127.0.0.1:$PUERTO:1433" "$IMAGEN_SQL" >/dev/null
else
  CLAVE="$(docker exec "$CONTENEDOR" printenv MSSQL_SA_PASSWORD)"
fi

sql() { sqlcmd -S "127.0.0.1,$PUERTO" -U sa -P "$CLAVE" -C -b "$@"; }

printf '== Esperando a SQL Server ==\n'
for _ in $(seq 1 60); do sql -Q "SELECT 1" >/dev/null 2>&1 && break; sleep 3; done
sql -Q "SELECT 1" >/dev/null

flyway() {
  local base="$1"; shift
  docker run --rm -v "$LINEA:/flyway/proyecto" -w /flyway/proyecto \
    -e FLYWAY_URL="jdbc:sqlserver://host.docker.internal:$PUERTO;databaseName=$base;encrypt=true;trustServerCertificate=true" \
    -e FLYWAY_USER=sa -e FLYWAY_PASSWORD="$CLAVE" "$IMAGEN_FLYWAY" -configFiles=flyway.toml "$@" 2>&1 \
    | grep -E 'baselined|Migrating|Successfully applied|ERROR|WARNING' || true
}

base_nueva() {
  sql -Q "IF DB_ID(N'$1') IS NOT NULL BEGIN ALTER DATABASE [$1] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$1]; END; CREATE DATABASE [$1] COLLATE SQL_Latin1_General_CP1_CI_AS;" >/dev/null
}

huella() { sql -d "$1" -h -1 -W -y 0 -i "$LINEA/verificacion/huella_del_esquema.sql" > "$2"; }

printf '\n== Escenario A: instalación completa ==\n'
base_nueva SIMUS_ESCENARIO_A
sql -d SIMUS_ESCENARIO_A -i "$LINEA/verificacion/prerrequisitos_de_prueba.sql" >/dev/null
flyway SIMUS_ESCENARIO_A -baselineVersion=0 -baselineDescription="simus sin modulo de festivales" baseline migrate
sql -d SIMUS_ESCENARIO_A -i "$LINEA/verificacion/verificar_estado_objetivo.sql"
huella SIMUS_ESCENARIO_A "$TRABAJO/huella_a.txt"

printf '\n== Escenario B: actualización desde el estado inicial ==\n'
base_nueva SIMUS_ESCENARIO_B
sql -d SIMUS_ESCENARIO_B -i "$LINEA/verificacion/prerrequisitos_de_prueba.sql" >/dev/null
if [[ -n "$estado_inicial" ]]; then
  # La exportación de SSMS viene en UTF-16 y fija la base con USE: se convierte y se retira.
  if file "$estado_inicial" | grep -q 'UTF-16'; then
    iconv -f UTF-16 -t UTF-8 "$estado_inicial" > "$TRABAJO/inicial.sql"
  else
    cp "$estado_inicial" "$TRABAJO/inicial.sql"
  fi
  sed -i.bak -e 's/\r$//' -e '/^USE \[/d' "$TRABAJO/inicial.sql"
  sql -d SIMUS_ESCENARIO_B -I -i "$TRABAJO/inicial.sql" >/dev/null
else
  sql -d SIMUS_ESCENARIO_B -I -i "$LINEA/migraciones/V1__estado_inicial_simus.sql" >/dev/null
fi
flyway SIMUS_ESCENARIO_B baseline migrate
sql -d SIMUS_ESCENARIO_B -i "$LINEA/verificacion/verificar_estado_objetivo.sql"
huella SIMUS_ESCENARIO_B "$TRABAJO/huella_b.txt"

printf '\n== Comparación de estructura A / B ==\n'
if diff "$TRABAJO/huella_a.txt" "$TRABAJO/huella_b.txt" > "$TRABAJO/diferencias_ab.txt"; then
  printf 'Idénticas: %s líneas de huella.\n' "$(wc -l < "$TRABAJO/huella_a.txt" | tr -d ' ')"
else
  cat "$TRABAJO/diferencias_ab.txt"; echo "FALLA: los escenarios no producen la misma estructura." >&2; exit 1
fi

printf '\n== Traslado de prueba de los festivales heredados (escenario B) ==\n'
sql -d SIMUS_ESCENARIO_B -W -s'|' -Q "SET NOCOUNT ON;
  INSERT INTO dbo.TrasladosHeredadosFestival (IdFestivalHeredado) SELECT id FROM dbo.ART_MUS_FESTIVALES;
  EXEC dbo.TrasladarFestivalesHeredados;
  SELECT COUNT(*) AS Solicitados, SUM(IIF(FechaTraslado IS NOT NULL, 1, 0)) AS Trasladados,
         SUM(IIF(FechaTraslado IS NULL, 1, 0)) AS ConError FROM dbo.TrasladosHeredadosFestival;
  SELECT IdFestivalHeredado, Resultado FROM dbo.TrasladosHeredadosFestival
  WHERE FechaTraslado IS NULL OR Resultado LIKE N'%descartada%';"
sql -d SIMUS_ESCENARIO_B -h -1 -W -Q "SET NOCOUNT ON;
  IF EXISTS (SELECT 1 FROM dbo.TrasladosHeredadosFestival WHERE FechaTraslado IS NULL)
    RAISERROR(N'Hay festivales que no se pudieron trasladar.', 16, 1);"
sql -d SIMUS_ESCENARIO_B -i "$LINEA/verificacion/verificar_estado_objetivo.sql"

if [[ "$comparar_desarrollo" -eq 1 ]]; then
  printf '\n== Comparación con la cadena DbUp del desarrollo ==\n'
  command -v dotnet >/dev/null || { echo "Falta dotnet." >&2; exit 2; }
  base_nueva PNMC_ALINEACION
  PNMC_MIGRADOR_CONEXION="Server=127.0.0.1,$PUERTO;Database=PNMC_ALINEACION;User Id=sa;Password=$CLAVE;TrustServerCertificate=True" \
    dotnet run --project "$RAIZ/pnmc-api/src/PNMC.Migrador" -- migrar | tail -1
  huella PNMC_ALINEACION "$TRABAJO/huella_desarrollo.txt"
  cut -d'|' -f1 "$TRABAJO/huella_a.txt" | sed 's/ *$//' | sort -u \
    | grep -vxE 'CorrespondenciasHeredadas|TrasladosHeredadosFestival' | sed '/^$/d' > "$TRABAJO/tablas.txt"
  filtrar() { awk -F' \\| ' 'NR==FNR{t[$1]=1;next} ($1 in t)' "$TRABAJO/tablas.txt" "$1"; }
  filtrar "$TRABAJO/huella_a.txt" > "$TRABAJO/comun_ministerio.txt"
  filtrar "$TRABAJO/huella_desarrollo.txt" > "$TRABAJO/comun_desarrollo.txt"
  if diff "$TRABAJO/comun_desarrollo.txt" "$TRABAJO/comun_ministerio.txt" > "$TRABAJO/diferencias_dev.txt"; then
    printf 'Idénticas en las %s tablas comunes.\n' "$(wc -l < "$TRABAJO/tablas.txt" | tr -d ' ')"
  else
    cat "$TRABAJO/diferencias_dev.txt"; echo "FALLA: el desarrollo y la línea del Ministerio difieren." >&2; exit 1
  fi
fi

printf '\nValidación completa.\n'
