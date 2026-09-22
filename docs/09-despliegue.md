# 9. Despliegue

## 9.1 Requisitos

- .NET SDK 10.
- Node.js 24 y npm.
- Docker Desktop con `docker compose`.
- Git. En Windows, Git Bash y PowerShell.

La base local usa `mcr.microsoft.com/azure-sql-edge:latest`. Su puerto solo se publica
en la interfaz local.

## 9.2 Preparar el entorno

```bash
cp .env.example .env
npm ci --prefix pnmc-web
```

Abra `.env` y reemplace el marcador de `PNMC_LOCAL_SA_PASSWORD` por una contraseña local
robusta. Este archivo está ignorado por Git. No copie allí credenciales de Azure.

| Variable | Valor predeterminado |
|---|---|
| `PNMC_LOCAL_SQL_PORT` | `14344` |
| `PNMC_LOCAL_DB_NAME` | `PNMC_LOCAL` |
| `PNMC_LOCAL_API_PORT` | `8180` |
| `PNMC_LOCAL_WEB_PORT` | `4300` |

## 9.3 Iniciar y comprobar

```bash
./scripts/dev-up.sh
./scripts/dev-check.sh
```

El arranque levanta SQL Server, aplica los guiones pendientes de `pnmc-database/schema/`,
incorpora DIVIPOLA MGN 2025 si falta e inicia API y frontend. Los registros temporales
quedan en `tmp/dev/` y no se versionan.

```bash
./scripts/dev-down.sh     # detiene sin borrar el volumen de datos
./scripts/dev-refresh.sh  # detiene y vuelve a iniciar
```

En Windows, `dev-up.sh` delega el inicio a `scripts/dev-start-windows.ps1`. No existen
lanzadores `.command` vigentes.

## 9.4 Operación por componentes

```bash
./scripts/local-db-up.sh
./scripts/schema-local.sh migrar
./scripts/api-local.sh

cd pnmc-web
npm start
```

`schema-local.sh estado` lista los cambios pendientes. `base-limpia-local.sh` elimina y
reconstruye una base local; úselo solo cuando la pérdida esté prevista.

## 9.5 Validar

```bash
./scripts/verificar-estructura.sh
bash scripts/tests/run-script-tests.sh
(cd pnmc-web && npm run lint && npm run trinquete && npm test && npm run build)
(cd pnmc-api && dotnet build PNMC.Api.sln --configuration Release)
```

La suite completa de persistencia necesita SQL local y `PNMC_PRUEBAS_SQLSERVER=1`.

## 9.6 Azure

La infraestructura versionada está en `infra/`. Antes de un despliegue real se confirman
la suscripción, el grupo de recursos, el nombre del sitio y la cadena de conexión. Esos
datos no se infieren ni se guardan en el repositorio.

