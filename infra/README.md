# Infraestructura de SIMUS en Azure

Esta carpeta contiene la definición Bicep y los scripts para un App Service Linux con
Azure SQL. Frontend y API se publican en un solo origen para conservar el flujo de sesión.

## Archivos

- `main.bicep`: recursos y configuración del ambiente.
- `parametros.ejemplo.json`: plantilla sin credenciales válidas.
- `desplegar.ps1`: empaquetado, comprobación opcional del esquema y publicación.
- `sembrar-datos.ps1`: carga explícita de semillas aprobadas.

## Preparación

```powershell
Copy-Item infra/parametros.ejemplo.json infra/parametros.json
az login
```

`infra/parametros.json` está ignorado por Git. Antes de seguir se confirman la suscripción,
el grupo de recursos, la región, el nombre del sitio, el usuario SQL, y las reglas de red.
La clave SQL se trata como secreto y no se reutiliza desde desarrollo local.

## Aprovisionamiento

La orden base es:

```bash
az deployment group create \
  --resource-group <grupo-confirmado> \
  --template-file infra/main.bicep \
  --parameters @infra/parametros.json
```

No se incluyen aquí nombres, costos o estado actual del ambiente porque cambian y deben
verificarse en la suscripción antes de operar.

## Esquema y publicación

El esquema se aplica con `PNMC.Migrador`; la aplicación no ejecuta DDL al iniciar. El
script de despliegue se usa desde PowerShell:

```powershell
./infra/desplegar.ps1
./infra/desplegar.ps1 -Migrar
```

Este script no reemplaza las puertas de calidad. `-SoloAplicacion` omite la comprobación
de esquema y se reserva para un cambio que se haya confirmado como ajeno a datos.

El pipeline requiere una conexión de servicio de Azure y un grupo de variables con la
cadena SQL secreta. Sus nombres deben coincidir con `azure-pipelines.yml` o ajustarse allí
antes de activarlo.

