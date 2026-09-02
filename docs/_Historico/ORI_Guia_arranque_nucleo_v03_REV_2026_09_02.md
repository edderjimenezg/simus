# Guía de arranque — núcleo SIMUS

**Versión:** v03  
**Estado:** REV  
**Fecha:** 2026_09_02  
**Deriva de:** ORI_Guia_arranque_nucleo_v02_REV_2026_09_02  
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
cd "/Users/edderjimenez/Proyecto SIMUS/Repositorio - SIMUS Nucleo"
```

| Acción | Comando | Efecto |
|---|---|---|
| Levantar | `./scripts/dev-up.sh` | Inicia SQL Server, aplica el esquema pendiente, conserva DIVIPOLA ya incorporada y arranca API y frontend. |
| Detener | `./scripts/dev-down.sh` | Detiene API, frontend y SQL Server. Conserva la base y el volumen local. |
| Refrescar | `./scripts/dev-refresh.sh` | Ejecuta detener y levantar. No borra datos ni reemplaza DIVIPOLA. |

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

## Límites actuales

El formulario de registro puede finalizar cuando existan documentos vigentes y DIVIPOLA. La verificación efectiva de correo requiere un proveedor institucional y permanece preparada, no simulada.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v03 | 2026_09_02 | REV | Incorpora scripts seguros para levantar, detener y refrescar el entorno local. |
| v02 | 2026_09_02 | REV | Trasladada a `_Historico/`. |
