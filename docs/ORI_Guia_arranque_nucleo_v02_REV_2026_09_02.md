# Guía de arranque — núcleo SIMUS

**Versión:** v02  
**Estado:** REV  
**Fecha:** 2026_09_02  
**Deriva de:** ORI_Guia_arranque_corte_00_v01_REV_2026_09_02  
**Fuentes:** estructura creada en Repositorio - SIMUS Nucleo y DIVIPOLA MGN 2025 de DANE  
**Destinatario:** equipo técnico SIMUS

## Preparación local

1. Copiar `.env.example` como `.env` y definir una contraseña local segura para SQL Server.
2. Iniciar la base vacía:

```bash
docker compose up -d sqlserver
```

3. Declarar una conexión únicamente en la sesión actual de terminal:

```bash
export ConnectionStrings__Simus='Server=localhost,14335;Database=simus_nucleo;User Id=sa;Password=<clave-local>;TrustServerCertificate=True;Encrypt=False'
```

4. Preparar el esquema e incorporar DIVIPOLA de forma explícita:

```bash
dotnet run --project src/Simus.Preparar.BaseDatos/Simus.Preparar.BaseDatos.csproj -- \
  --conexion "$ConnectionStrings__Simus" \
  --importar-divipola
```

5. Iniciar la API:

```bash
dotnet run --project src/Simus.Api/Simus.Api.csproj
```

6. En otra terminal, iniciar el frontend:

```bash
npm --prefix src/simus-web run start:local
```

La aplicación queda disponible en `http://localhost:4200`. Angular se comunica con la API mediante el proxy local y nunca contiene una cadena de conexión.

## Regla de datos

DIVIPOLA es el único dato de referencia incorporado por este arranque. No se crean organizaciones, festivales, personas externas ni datos demostrativos. El importador se detiene si ya existen territorios para impedir reemplazos automáticos.

## Verificaciones

```bash
dotnet build src/Simus.Api/Simus.Api.csproj
dotnet test tests/Simus.Api.Tests/Simus.Api.Tests.csproj
npm --prefix src/simus-web run build
npm --prefix src/simus-web test
```

## Límites actuales

El formulario de registro solo puede finalizar cuando existan documentos vigentes de términos de uso y tratamiento de datos. La verificación efectiva de correo requiere un proveedor institucional y permanece preparada, no simulada.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v02 | 2026_09_02 | REV | Actualiza arranque, nomenclatura española e importación explícita de DIVIPOLA. |
| v01 | 2026_09_02 | REV | Inicio del corte técnico; trasladada a `_Historico/`. |
