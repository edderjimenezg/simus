# API de SIMUS

Backend en .NET 10 con Minimal APIs, Entity Framework Core y SQL Server.

## Proyectos

- `src/PNMC.Api`: composición HTTP, endpoints, seguridad y observabilidad.
- `src/PNMC.Contracts`: contratos públicos.
- `src/PNMC.Domain`: entidades de dominio.
- `src/PNMC.Infrastructure`: persistencia e integraciones.
- `src/PNMC.Migrador`: migración DbUp y actualización territorial controlada.
- `tests/PNMC.Api.Tests`: pruebas unitarias e integración.

`openapi.yaml` es el contrato versionado. Una prueba comprueba que coincida con el
documento generado por la aplicación.

## Ejecución local

La vía recomendada desde la raíz es:

```bash
./scripts/local-db-up.sh
./scripts/schema-local.sh migrar
./scripts/api-local.sh
```

Swagger: `http://localhost:8180/swagger`
Salud: `http://localhost:8180/health/live` y `http://localhost:8180/health/ready`

Para una base remota puede crear `pnmc-api/.env` desde `.env.example`. La cadena completa
`AZURE_SQL_CONNECTION_STRING` tiene prioridad sobre los campos separados. Ningún archivo
con secretos se versiona.

En SQL Server, el API valida la conexión y puede cargar configuración o usuarios locales
si el perfil lo autoriza; no crea ni altera tablas. El esquema pertenece al migrador.

## Verificación

```bash
dotnet restore PNMC.Api.sln
dotnet build PNMC.Api.sln --configuration Release --no-restore
dotnet test PNMC.Api.sln --configuration Release --no-build
```

Para actualizar deliberadamente el contrato:

```bash
ACTUALIZAR_OPENAPI=1 dotnet test --filter ContratoOpenApi
```

