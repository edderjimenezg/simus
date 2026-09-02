# Guía de arranque — corte 00 Base técnica

**Versión:** v01  
**Estado:** REV  
**Fecha:** 2026_09_02  
**Deriva de:** ORI_Plan_corte_00_base_tecnica_v01_APR_2026_09_02  
**Fuentes:** estructura creada en Repositorio - SIMUS Nucleo  
**Destinatario:** equipo técnico SIMUS

## Preparación local

1. Copiar `.env.example` como `.env` y definir una contraseña local segura para SQL Server.
2. Iniciar la base vacía:

```bash
docker compose up -d sqlserver
```

3. Iniciar la API con la conexión declarada únicamente para esa sesión:

```bash
ConnectionStrings__Simus='Server=localhost,14335;Database=master;User Id=sa;Password=<clave-local>;TrustServerCertificate=True;Encrypt=False' \
dotnet run --project src/Simus.Api
```

4. En otra terminal, iniciar el frontend:

```bash
npm --prefix src/simus-web run start:local
```

La página inicial debe informar el estado de API y base de datos. La API se expone localmente en el puerto `5050`; Angular se comunica mediante el proxy local y nunca contiene una cadena de conexión.

## Verificaciones

```bash
dotnet build src/Simus.Api/Simus.Api.csproj
npm --prefix src/simus-web run build
npm --prefix src/simus-web test -- --watch=false
```

## Límites

La conexión se realiza a `master` solo para comprobar infraestructura. El corte 00 no crea tablas, usuarios, secretos, datos de negocio ni una base de aplicación definitiva.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v01 | 2026_09_02 | REV | Documenta el arranque y las verificaciones del núcleo técnico vacío. |
