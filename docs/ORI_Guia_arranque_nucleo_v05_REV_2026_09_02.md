# Guía de arranque — núcleo SIMUS

**Versión:** v05  
**Estado:** REV  
**Fecha:** 2026_09_02  
**Deriva de:** ORI_Guia_arranque_nucleo_v04_REV_2026_09_02  
**Fuentes:** estructura creada en Repositorio - SIMUS Nucleo y DIVIPOLA MGN 2025 de DANE  
**Destinatario:** equipo técnico SIMUS

## Preparación única

1. Abrir Docker Desktop.
2. Copiar `.env.example` como `.env` y definir una contraseña local segura en `SIMUS_BASE_DATOS_CONTRASENA_SA`.
3. Instalar dependencias del frontend una sola vez:

```bash
npm --prefix src/simus-web ci
```

## Scripts de entorno

Desde la raíz del repositorio:

```bash
cd "/Users/edderjimenez/Proyecto SIMUS/Reestructuración SIMUS/Repositorio - SIMUS Nucleo"
```

| Acción | Comando | Efecto |
|---|---|---|
| Levantar | `./scripts/dev-up.sh` | Inicia SQL Server, aplica el esquema pendiente, conserva DIVIPOLA ya incorporada y arranca API y frontend. |
| Detener | `./scripts/dev-down.sh` | Detiene API, frontend y SQL Server. Conserva la base y el volumen local. |
| Refrescar | `./scripts/dev-refresh.sh` | Ejecuta detener y levantar. No borra datos ni reemplaza DIVIPOLA. |
| Pruebas de integración | `./scripts/dev-test-integracion.sh` | Prepara una base separada `simus_nucleo_pruebas` en el mismo SQL Server local, siembra únicamente territorio de prueba (códigos `ZZ`/`ZZZ`, no DIVIPOLA real) y ejecuta `dotnet test` contra datos reales. Limpia los datos operativos de esa base en cada ejecución; nunca toca `simus_nucleo`. |

El portal queda disponible en `http://localhost:4200` y la verificación de API en `http://localhost:5050/api/salud`.

Los procesos arrancados por los scripts, sus identificadores y sus registros se guardan localmente en `.ejecucion-local/`, que no debe versionarse.

## Regla de datos

DIVIPOLA es el único dato de referencia incorporado por este arranque. No se crean organizaciones, Festivales, personas externas ni datos demostrativos. El refresco no borra registros existentes; para una base completamente nueva se requiere una decisión explícita y una operación separada.

## Verificaciones

```bash
dotnet build src/Simus.Api/Simus.Api.csproj
dotnet test tests/Simus.Api.Tests/Simus.Api.Tests.csproj
npm --prefix src/simus-web run build
npm --prefix src/simus-web test
```

Sin `ConnectionStrings__Simus` configurada, las pruebas que requieren base de datos real se omiten explícitamente (`Omitido`, no `Superado`). Para ejecutarlas con datos reales usa `./scripts/dev-test-integracion.sh`.

## Límites actuales

El formulario de registro puede finalizar cuando existan documentos vigentes y DIVIPOLA. La verificación efectiva de correo requiere un proveedor institucional y permanece preparada, no simulada.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v05 | 2026_09_02 | REV | Actualiza la ruta del repositorio: se trasladó a `Proyecto SIMUS/Reestructuración SIMUS/Repositorio - SIMUS Nucleo`, separado de `simus/` (desarrollo previo) y de `fuentes/` (referencia). |
| v04 | 2026_09_02 | REV | Trasladada a `_Historico/`. |
