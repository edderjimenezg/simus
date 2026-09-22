# 3. Estructura del proyecto

## Decisión estructural

Las imágenes de referencia describen responsabilidades útiles, pero corresponden a un
backend Node y un frontend React. SIMUS usa .NET y Angular; su estructura debe expresar
esas plataformas sin crear capas equivalentes con nombres ajenos.

```text
SIMUS - Desarrollo/
├── pnmc-web/          Angular: portal, consola, pruebas y herramientas
├── pnmc-api/          .NET: HTTP, dominio, contratos, infraestructura y migrador
├── pnmc-database/     SQL: esquema, semillas y validaciones
├── scripts/           entorno local reproducible
├── infra/             despliegue Azure
├── docs/              documentación vigente y entregables
└── Archivos basura/   material retirado y aislado
```

No se agrupan frontend, backend y base bajo una carpeta adicional: ya son unidades con
manifiestos, comandos y ciclos de verificación propios. Moverlas añadiría profundidad y
rompería referencias sin mejorar el aislamiento.

## Backend

`pnmc-api/PNMC.Api.sln` contiene:

| Proyecto | Responsabilidad |
|---|---|
| `PNMC.Api` | composición HTTP, endpoints, seguridad y observabilidad |
| `PNMC.Contracts` | solicitudes y respuestas públicas |
| `PNMC.Domain` | entidades y conceptos de dominio |
| `PNMC.Infrastructure` | EF Core, acceso a datos e integraciones |
| `PNMC.Migrador` | aplicación de esquema y carga territorial controlada |
| `PNMC.Api.Tests` | pruebas unitarias e integración |

Los endpoints minimal API cumplen la función que tendría una carpeta `controllers` en
otro marco. EF Core y las entidades reemplazan `models`; no existe un router separado
porque el mapeo de rutas pertenece a los propios endpoints.

## Frontend

Dentro de `pnmc-web/src/app/`:

- `core/`: servicios globales, HTTP, guardas, configuración y CMS.
- `features/`: capacidades organizadas por dominio y páginas enrutables.
- `shared/`: componentes, directivas y utilidades reutilizables sin dominio propio.

Existe un único dominio `features/ecosistema/`. El duplicado heredado

Las pruebas de navegador viven en `e2e/`; las auditorías amplias, en `e2e-audit/`; las
herramientas mantenidas, en `tools/`; los límites cuantificados de deuda, en `trinquete/`.

## Datos

- `pnmc-database/schema/`: historial inmutable de cambios estructurales.
- `pnmc-database/seed/`: cargas explícitas separadas del esquema.
- `pnmc-database/scripts/`: consultas de auditoría y validación.
- `pnmc-database/local/`: notas sobre el modelo local.

DbUp, mediante `PNMC.Migrador`, es el único ejecutor autorizado del esquema. La carpeta
retirada `migrations/` no formaba parte de ese flujo.

## Dependencias permitidas

```text
pnmc-web ──HTTP──> pnmc-api ──EF Core──> SQL Server
scripts ──orquesta──> pnmc-web + pnmc-api + pnmc-database
infra ──empaqueta──> artefactos verificados
```

El frontend no consulta SQL. La base no depende del frontend. Ninguna parte activa
depende de `Archivos basura/`.

