#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
source "$ROOT_DIR/scripts/load-local-env.sh"
DB_NAME="${PNMC_LOCAL_DB_NAME:-PNMC_LOCAL}"
DB_PASSWORD="${PNMC_LOCAL_SA_PASSWORD:-}"
DB_PORT="${PNMC_LOCAL_SQL_PORT:-14344}"
SQL_CONTAINER="${PNMC_LOCAL_SQL_CONTAINER:-simus-desarrollo-sqlserver}"
SQL_TOOLS_IMAGE="${PNMC_LOCAL_SQL_TOOLS_IMAGE:-mcr.microsoft.com/mssql-tools}"

if [[ -z "$DB_PASSWORD" ]]; then
  echo "[pnmc-db] Falta PNMC_LOCAL_SA_PASSWORD. Definela en el entorno antes de sembrar la base." >&2
  exit 1
fi

# ---------------------------------------------------------------------------
# CINTURON: un arbol enlazado no siembra la base compartida.
#
# QUE PASO EL 24 DE AGOSTO DE 2026. Este guion se ejecuto desde un worktree de git
# detenido en un commit anterior, y aplico SUS guiones de esquema a LA base compartida:
# devolvio las tres tablas del modelo de entidades aliadas y sus tres roles, retirados dos
# dias antes, y se llevo por delante el tipo de entidad `agrupacion`. El arbol principal
# estaba sano y todas las pruebas en verde: lo que habia regresado era la base.
#
# LA CAUSA NO ES UN DESCUIDO, ES LA FORMA DEL MONTAJE. `git worktree` aisla el CODIGO y no
# la BASE: el contenedor es uno solo y compartido, asi que todo el aislamiento que da git
# desaparece en el momento en que el arbol aislado escribe en un recurso comun. Un guion no
# sabe desde que arbol lo invocan.
#
# LA DEFENSA YA EXISTIA EN EL PROYECTO, pero solo para las pruebas: `ArnesSqlServer` declara
# `BaseProhibida = "PNMC_LOCAL"` y solo admite bases con prefijo `PNMC_PRUEBAS_`, de modo que
# el carril de SQL Server no puede tocar la base del dueno ni por accidente. Esto es la misma
# idea aplicada al otro lado.
#
# NO BLOQUEA EL TRABAJO EN WORKTREES: solo la combinacion peligrosa, que es un arbol enlazado
# escribiendo en la base compartida. Para sembrar una base propia desde un worktree:
#   PNMC_LOCAL_DB_NAME=PNMC_MAPA ./scripts/seed-local-db.sh
# ---------------------------------------------------------------------------
if git -C "$ROOT_DIR" rev-parse --git-dir >/dev/null 2>&1; then
  GIT_DIR_PROPIO="$(git -C "$ROOT_DIR" rev-parse --absolute-git-dir)"
  GIT_DIR_COMUN="$(git -C "$ROOT_DIR" rev-parse --path-format=absolute --git-common-dir)"
  if [ "$GIT_DIR_PROPIO" != "$GIT_DIR_COMUN" ]      && [ "$DB_NAME" = "PNMC_LOCAL" ]      && [ "${PNMC_PERMITIR_SIEMBRA_DESDE_WORKTREE:-0}" != "1" ]; then
    echo "[pnmc-db] ABORTADO: este es un worktree enlazado y la base destino es PNMC_LOCAL." >&2
    echo "[pnmc-db]   arbol : $ROOT_DIR" >&2
    echo "[pnmc-db]   commit: $(git -C "$ROOT_DIR" rev-parse --short HEAD) [$(git -C "$ROOT_DIR" rev-parse --abbrev-ref HEAD)]" >&2
    echo "[pnmc-db]" >&2
    echo "[pnmc-db] Sembrar PNMC_LOCAL desde aqui aplicaria los guiones de ESTE arbol a la base" >&2
    echo "[pnmc-db] que comparten todas las sesiones. Si el arbol esta atrasado, revive esquema" >&2
    echo "[pnmc-db] ya retirado sin que ninguna prueba lo vea: paso el 24 de agosto de 2026." >&2
    echo "[pnmc-db]" >&2
    echo "[pnmc-db] Opciones:" >&2
    echo "[pnmc-db]   1. Sembrar una base propia:  PNMC_LOCAL_DB_NAME=PNMC_MI_RAMA $0" >&2
    echo "[pnmc-db]   2. Sembrar desde el arbol principal, con su HEAD al dia." >&2
    echo "[pnmc-db]   3. Si de verdad es lo que quieres:  PNMC_PERMITIR_SIEMBRA_DESDE_WORKTREE=1 $0" >&2
    exit 1
  fi
fi

echo "[pnmc-db] Detectando sqlcmd..."
SQLCMD=""
SQLCMD_MODE=""
SQL_NETWORK="$(docker inspect "$SQL_CONTAINER" --format '{{range $network, $_ := .NetworkSettings.Networks}}{{$network}}{{end}}')"
# LA PRUEBA LLEVA `-f 65001` PORQUE LA USA `run_sql_file`, y esa diferencia costo que ningun
# fichero de semilla se aplicara nunca en un equipo con go-sqlcmd.
#
# QUE PASABA. La deteccion probaba con `-Q "SELECT 1"` a secas, que go-sqlcmd acepta, asi que
# elegia la via del equipo. Al sembrar de verdad, `run_sql_file` anade `-f 65001` —la bandera de
# pagina de codigos del sqlcmd clasico, que go-sqlcmd no conoce— y el primer fichero moria con
# «'f': Unknown Option». Con `set -e`, la siembra ENTERA aborta en su primer fichero, y como el
# guion ya habia dicho «Aplicando archivos de semilla...» parecia que iba bien.
#
# MEDIDO EL 13 DE SEPTIEMBRE DE 2026: los ocho catalogos del formulario de Festival estaban a cero
# filas en `PNMC_LOCAL` —tipologia, fuentes de financiacion, tipo de organizador, expresiones
# artisticas, modalidades, tipos de ingreso, naturaleza de entidad y regiones OCAD— aunque su
# fichero de semilla existia desde el 28 de agosto. El formulario de version ofrecia desplegables
# sin una sola opcion.
#
# LA LECCION, que vale para cualquier deteccion: se prueba con las mismas banderas con las que se
# va a trabajar. Una sonda mas permisiva que el uso real elige una via que despues no sirve.
if command -v sqlcmd >/dev/null 2>&1 \
  && sqlcmd -S "127.0.0.1,$DB_PORT" -U sa -P "$DB_PASSWORD" -C -f 65001 -Q "SELECT 1" >/dev/null 2>&1; then
  SQLCMD="$(command -v sqlcmd)"
  SQLCMD_MODE="host"
elif docker exec "$SQL_CONTAINER" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$DB_PASSWORD" -C -Q "SELECT 1" >/dev/null 2>&1; then
  SQLCMD="/opt/mssql-tools18/bin/sqlcmd"
  SQLCMD_MODE="container"
elif docker exec "$SQL_CONTAINER" /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$DB_PASSWORD" -Q "SELECT 1" >/dev/null 2>&1; then
  SQLCMD="/opt/mssql-tools/bin/sqlcmd"
  SQLCMD_MODE="container"
elif docker run --rm --platform linux/amd64 --network "$SQL_NETWORK" "$SQL_TOOLS_IMAGE" \
  /opt/mssql-tools/bin/sqlcmd -S sqlserver -U sa -P "$DB_PASSWORD" -C -l 5 -Q "SELECT 1" >/dev/null 2>&1; then
  SQLCMD_MODE="tools-container"
  SQLCMD="/opt/mssql-tools/bin/sqlcmd"
fi

if [[ -z "$SQLCMD" ]]; then
  echo "[pnmc-db] Error: No se pudo conectar a SQL Server o no se encontró sqlcmd."
  exit 1
fi

echo "[pnmc-db] sqlcmd detectado en: $SQLCMD"

# ---------------------------------------------------------------------------
# EL `-I` NO ES DECORACION: es la mitad del problema que este guion no veia.
#
# `-I` enciende QUOTED_IDENTIFIER en la sesion de sqlcmd. SQL Server exige esa
# opcion en ON para DOS cosas distintas, y la siembra tropezaba con las dos:
#
#   1. CREAR un indice filtrado (`CREATE INDEX ... WHERE`) -> Msg 1934.
#      Esto ya se conocia: `V20260823_02` lo sufrio el 24 ago 2026 y de ahi
#      salio `GuionesIndependientesDeLaSesionTests`, que obliga a cada guion de
#      `schema/` con indice filtrado a declarar `SET QUOTED_IDENTIFIER ON;`.
#
#   2. ESCRIBIR en una tabla QUE YA TIENE uno -> Msg 1934 tambien.
#      Esto no se conocia, y es la parte que aquella prueba no puede cubrir: el
#      fichero que falla NO es el que crea el indice, sino cualquier INSERT o
#      DELETE posterior sobre esa tabla. Los ficheros de `seed/` no crean
#      indices, asi que la prueba de texto ni los mira; y no pueden declarar la
#      opcion uno a uno sin que esa lista haya que mantenerla para siempre.
#
# Se descubrio el 24 ago 2026 al anadir un indice unico filtrado a `Usuarios`:
# el guion que lo creaba pasaba -declaraba su SET-, y reventaba la siembra
# ENTERA detras, en los ficheros que insertan usuarios. Medido en una base de
# control: sin el indice, 0 errores; con el indice y sin `-I`, Msg 1934; con el
# indice y con `-I`, 0 errores otra vez.
#
# La opcion la trae QUIEN EJECUTA. Ponerla aqui, una vez, cubre las dos causas
# y todos los ficheros. Vigilado por `SiembraSinBaseCableadaTests`.
# ---------------------------------------------------------------------------
# ---------------------------------------------------------------------------
# LOS FICHEROS ENTRAN POR `-i`, NO POR UNA TUBERIA. Y con `-f 65001`.
#
# Los .sql de pnmc-database estan en UTF-8 y son correctos. El `sqlcmd` de Windows
# -/c/Program Files/Microsoft SQL Server/Client SDK/ODBC/170/...-, que es el que esta rama
# elige PRIMERO cuando existe, decodifica lo que le llega POR STDIN con la pagina de codigos
# de la consola (CP850 en un Windows en espanol), no con UTF-8. Cada vocal acentuada son dos
# bytes en UTF-8 y CP850 los convierte en dos caracteres: `Í` (C3 8D) acaba guardada como
# `├` + `ì`.
#
# MEDIDO EL 26 DE AGOSTO DE 2026 en una base de laboratorio, insertando el mismo texto por
# las tres vias y mirando el punto de codigo guardado (no la consola, que tambien miente):
#
#   cat fichero | sqlcmd            -> U+251C  ROTO
#   cat fichero | sqlcmd -f 65001   -> U+251C  ROTO   <- `-f` NO alcanza a stdin
#   sqlcmd -f 65001 -i fichero      -> U+00CD  bien
#
# Esa segunda linea es la que importa: anadir `-f 65001` y dejar la tuberia NO arregla nada.
# Hay que dejar de usar la tuberia. Como el `GO` final se anadia justo ahi, ahora se escribe
# un fichero temporal con el original mas el `GO`, y se pasa ese.
#
# EN LAS DOS RAMAS DE CONTENEDOR EL SQLCMD ES EL DE LINUX, que si trata stdin como UTF-8
# -medido: U+00CD por las dos vias- y que NO IMPLEMENTA `-f`: es una opcion de la version de
# Windows, donde sirve para la pagina de codigos de la consola. Se les ponia igualmente «para que
# la invocacion fuera la misma», y eso las rompia: `Sqlcmd: 'f65001': Unknown Option`, en el primer
# fichero y con `set -e`, es decir la siembra entera. Se retira de las dos.
#
# ASI QUE LA BANDERA VA DONDE HACE FALTA Y SOLO AHI. Uniformar la invocacion es deseable hasta que
# uniformarla obliga a pasarle a una herramienta una opcion que no tiene. Para que la
# codificacion sea una decision escrita y no el valor por omision de la maquina de cada uno.
#
# Vigilado por `verificar_tildes`, al final de este guion: si algo vuelve a romperlo, la
# siembra termina en rojo en vez de dejar la base en silencio con las tildes partidas.
# ---------------------------------------------------------------------------
run_sql_file() {
  local file_path="$1"
  local con_go
  con_go="$(mktemp -t pnmc-seed-XXXXXX.sql)"
  { cat "$file_path"; echo ""; echo "GO"; } > "$con_go"

  local ruta_cliente="$con_go"
  if command -v cygpath >/dev/null 2>&1; then
    ruta_cliente="$(cygpath -w "$con_go")"
  fi

  if [[ "$SQLCMD_MODE" == "host" ]]; then
    "$SQLCMD" -S "127.0.0.1,$DB_PORT" -U sa -P "$DB_PASSWORD" -d "$DB_NAME" -C -I -b -f 65001 -i "$ruta_cliente"
  elif [[ "$SQLCMD_MODE" == "tools-container" ]]; then
    # POR `-i` Y NO POR TUBERIA, que es lo que el encabezado de este guion pide desde el principio
    # y lo que las dos ramas de contenedor NO hacian. Por stdin, sqlcmd corta las lineas largas y
    # el fichero llega partido: `V20260519_01` moria con «Incorrect syntax near '*'» en la linea 39
    # y ocho errores mas, todos en mitad de sentencias que el fichero tiene bien escritas. El
    # fichero temporal se monta en el contenedor y se le pasa por su ruta.
    docker run --rm --platform linux/amd64 --network "$SQL_NETWORK" \
      -v "$con_go:/tmp/semilla.sql:ro" "$SQL_TOOLS_IMAGE" \
      /opt/mssql-tools/bin/sqlcmd -S sqlserver -U sa -P "$DB_PASSWORD" -d "$DB_NAME" -C -I -b -i /tmp/semilla.sql
  else
    # El contenedor de la base no puede montar nada nuestro: se copia dentro y se borra al salir.
    docker cp "$con_go" "$SQL_CONTAINER:/tmp/semilla.sql" >/dev/null
    docker exec "$SQL_CONTAINER" "$SQLCMD" \
      -S localhost -U sa -P "$DB_PASSWORD" -d "$DB_NAME" -C -I -b -i /tmp/semilla.sql
    local salida=$?
    docker exec "$SQL_CONTAINER" rm -f /tmp/semilla.sql >/dev/null 2>&1 || true
    return $salida
  fi
  local codigo=$?

  rm -f "$con_go"
  return $codigo
}

run_sql_query() {
  local query="$1"
  if [[ "$SQLCMD_MODE" == "host" ]]; then
    "$SQLCMD" -S "127.0.0.1,$DB_PORT" -U sa -P "$DB_PASSWORD" -d "$DB_NAME" -C -I -b -Q "$query"
  elif [[ "$SQLCMD_MODE" == "tools-container" ]]; then
    docker run --rm -i --platform linux/amd64 --network "$SQL_NETWORK" "$SQL_TOOLS_IMAGE" \
      /opt/mssql-tools/bin/sqlcmd -S sqlserver -U sa -P "$DB_PASSWORD" -d "$DB_NAME" -C -I -b -Q "$query"
  else
    docker exec -i "$SQL_CONTAINER" "$SQLCMD" \
      -S localhost -U sa -P "$DB_PASSWORD" -d "$DB_NAME" -C -I -b \
      -Q "$query"
  fi
}

# DbUp es la única autoridad del esquema. Descubre todos los guiones, los ordena y registra
# cada aplicación en dbo.SchemaVersions; no existe una segunda lista manual que pueda quedar
# atrasada ni se vuelven a ejecutar migraciones históricas.
MIGRATOR_CONNECTION="Server=127.0.0.1,$DB_PORT;Database=$DB_NAME;User Id=sa;Password=$DB_PASSWORD;TrustServerCertificate=True;Encrypt=False"
echo "[pnmc-db] Aplicando migraciones pendientes con DbUp..."
dotnet run --project "$ROOT_DIR/pnmc-api/src/PNMC.Migrador/PNMC.Migrador.csproj" -- \
  migrar --conexion "$MIGRATOR_CONNECTION"

# Lista de semillas de referencia y soporte local. No contiene datos demostrativos ni módulos
# retirados del perfil vigente.
SEEDS=(
  "seed/V20260519_01__maestras_estaticas_seed.sql"
  "seed/V20260519_02__divipola_seed.sql"
  "seed/V20260519_03__administracion_control_seed.sql"
  # Copia los roles institucionales de soporte. No crea organizaciones externas,
  # Festivales ni contenido de demostración.
  "seed/V20260824_01__usuarios_roles_seed.sql"
  # Los nueve catalogos del Festival, que V20260824_02 creo VACIOS a proposito, y las regiones
  # OCAD. Va DESPUES de `divipola_seed`: el reparto de departamentos a region comprueba contra
  # dbo.Divipola antes de insertar, y con la tabla vacia no entraria ninguna de las 32 filas.
  "seed/V20260828_01__catalogos_festival_seed.sql"
  "seed/V20260908_03__descripciones_territorios_sonoros_fuente_institucional.sql"
  # El acervo del Proyecto Editorial: 171 publicaciones, 403 agentes, 856 creditos, 98
  # identificadores y 191 puntos de acceso. ES DATO INSTITUCIONAL REAL, no demostrativo, y por
  # eso esta en esta lista y no fuera: es el catalogo del Plan. Va DESPUES de que DbUp haya
  # pasado `V20260913_04`, que crea el vocabulario de tipologia RDA contra el que enlaza, y que
  # ademas ensancha `TipoPublicacion`, `Paginas` y `Duracion`: sin ese ensanchamiento la carga
  # falla por truncamiento y, como cada fichero corre en una transaccion, no entra ni una obra.
  "seed/V20260913_02__catalogo_editorial_acervo.sql"
  # Normaliza lo que inserta el acervo: las direcciones que la fuente escribió dentro del texto
  # libre pasan a ser accesos. Va DESPUES, y por eso es semilla y no migracion.
  "seed/V20260913_03__accesos_editoriales_normalizados.sql"
  # La decision de publicar el acervo. Va DESPUES de cargarlo y de normalizar sus accesos: era una
  # migracion, y como las migraciones corren antes que las semillas, en una base nueva no encontraba
  # ninguna publicacion que publicar.
  "seed/V20260913_04__acervo_editorial_publicado.sql"
)

echo "[pnmc-db] Aplicando archivos de semilla..."
for seed in "${SEEDS[@]}"; do
  echo "  -> Aplicando $seed..."
  run_sql_file "$ROOT_DIR/pnmc-database/$seed"
done


# ---------------------------------------------------------------------------
# LA PUERTA: que las tildes hayan sobrevivido de verdad.
#
# `run_sql_file` pasa los ficheros como hay que pasarlos, pero eso es una AFIRMACION sobre
# el guion. Esto es una MEDICION sobre la base: cuenta los caracteres de dibujo de cajas, que
# es lo que deja un texto UTF-8 leido con una pagina de codigos de consola. En prosa en
# espanol no aparece ninguno; si aparecen, la siembra dejo el dato roto.
#
# Se miran columnas vigentes con texto territorial y editorial.
#
# TERMINA EN ROJO, no en un aviso. Una base sembrada con las tildes partidas se ve bien en
# los recuentos, pasa todas las pruebas de la API y solo se nota mirando la pantalla: es
# exactamente el tipo de fallo que hay que hacer ruidoso.
# ---------------------------------------------------------------------------
verificar_tildes() {
  echo "[pnmc-db] Verificando que las tildes sobrevivieron..."

  local consulta
  consulta="DECLARE @caja NVARCHAR(60) = N'%[' + NCHAR(0x251C) + NCHAR(0x2502) + NCHAR(0x2551) + NCHAR(0x2592) + NCHAR(0x252C) + N']%';
SELECT CONVERT(VARCHAR(20), (
  (SELECT COUNT(*) FROM dbo.Divipola WHERE NombreMunicipio LIKE @caja OR NombreDepartamento LIKE @caja)
+ (SELECT COUNT(*) FROM dbo.Entidades WHERE Nombre LIKE @caja OR ISNULL(Descripcion, N'') LIKE @caja)
+ (SELECT COUNT(*) FROM dbo.Festivales WHERE NombreFestival LIKE @caja)));"

  local rotas
  rotas="$(run_sql_query "$consulta" | tr -d '[:space:]' | grep -oE '[0-9]+' | head -1)"

  if [[ -z "$rotas" ]]; then
    echo "[pnmc-db] Error: no se pudo comprobar la codificación. No se declara la siembra correcta." >&2
    exit 1
  fi

  if [[ "$rotas" != "0" ]]; then
    echo "[pnmc-db] Error: $rotas filas quedaron con las tildes partidas." >&2
    echo "[pnmc-db] El .sql está bien; lo que falla es cómo se le entrega a sqlcmd." >&2
    echo "[pnmc-db] Comprobar que run_sql_file use  -f 65001 -i <fichero>  y no una tubería." >&2
    exit 1
  fi

  echo "[pnmc-db] Codificación correcta: 0 filas con caracteres de caja."
}

verificar_tildes

echo "[pnmc-db] Base de datos local inicializada y sembrada con éxito."
