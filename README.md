# SIMUS — Sistema de Información de la Música

Sistema de información del sector musical colombiano. Reúne el **portal público**, el **espacio de
gestión de las organizaciones** y la **consola de gestión administrativa** en una aplicación Angular,
sobre un API en .NET y una base de datos SQL Server.

## Puesta en marcha

Requisitos: Git, .NET SDK 10, Node.js 24, npm y Docker Desktop.

```bash
cp .env.example .env
# Definir PNMC_LOCAL_SA_PASSWORD en .env
npm ci --prefix pnmc-web
./scripts/dev-up.sh
```

| Servicio | Dirección predeterminada |
|---|---|
| Portal y consola | `http://127.0.0.1:4300` |
| API y documentación de rutas | `http://localhost:8180/swagger` |
| Estado del API | `http://localhost:8180/health/ready` |
| SQL Server | `127.0.0.1,14344` |

Para comprobar el entorno y detenerlo: `./scripts/dev-check.sh` y `./scripts/dev-down.sh`.

## Estructura

| Ruta | Contenido |
|---|---|
| `pnmc-web/` | aplicación Angular: portal público, espacios de gestión y sus pruebas |
| `pnmc-api/` | API, dominio, contratos, persistencia, migrador y pruebas |
| `pnmc-database/` | esquema, semillas y validaciones SQL |
| `scripts/` | operación del entorno local |
| `infra/` | infraestructura y despliegue |
| `docs/` | documentación del sistema |

## Documentación

| | |
|---|---|
| 1 | [Descripción general](docs/01-descripcion-general.md) |
| 2 | [Arquitectura](docs/02-arquitectura.md) |
| 3 | [Estructura del proyecto](docs/03-estructura-del-proyecto.md) |
| 4 | [Instalación y configuración](docs/04-instalacion-y-configuracion.md) |
| 5 | [Base de datos](docs/05-base-de-datos.md) |
| 6 | [Módulos y funcionalidades](docs/06-modulos-y-funcionalidades.md) |
| 7 | [Roles y permisos](docs/07-roles-y-permisos.md) |
| 8 | [API e integraciones](docs/08-api-e-integraciones.md) |
| 9 | [Despliegue](docs/09-despliegue.md) |
| 10 | [Pruebas](docs/10-pruebas.md) |
| 11 | [Operación y mantenimiento](docs/11-operacion-y-mantenimiento.md) |

El historial de versiones está en [`CHANGELOG.md`](CHANGELOG.md).
