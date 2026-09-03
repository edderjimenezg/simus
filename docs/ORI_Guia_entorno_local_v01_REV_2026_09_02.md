# Guía — operación del entorno local del núcleo SIMUS

**Versión:** v01
**Estado:** REV
**Fecha:** 2026_09_02
**Deriva de:** ORI_Guia_arranque_nucleo_v05_REV_2026_09_02 y ORI_Informe_linea_base_verificada_v01_REV_2026_09_02
**Fuentes:** scripts operativos vigentes y ejecución local controlada
**Destinatario:** equipo técnico de SIMUS

## Propósito

Definir tres comandos ejecutables para operar el único entorno local del núcleo sin intervenir los repositorios heredados ni otros proyectos de la máquina.

## Comandos

| Comando | Resultado | Datos SQL |
|---|---|---|
| `./scripts/dev-up.sh` | Inicia SQL Server, prepara esquema/DIVIPOLA, API y frontend; espera ambos servicios antes de declarar éxito. | Conserva los existentes. |
| `./scripts/dev-down.sh` | Detiene API, frontend, contenedor y red del núcleo. | Conserva el volumen SQL. |
| `./scripts/dev-refresh.sh` | Detiene todo, elimina el volumen SQL exclusivo del núcleo y monta el entorno desde cero. | Elimina por completo los datos locales del núcleo. |

Para purgar manualmente datos sin volver a montar, usar `./scripts/dev-down.sh --purge-data`.

## Límites de seguridad

Los scripts solo gestionan procesos registrados en `.ejecucion-local` y recursos Docker definidos por este repositorio. Si los puertos 4200 o 5050 pertenecen a otro proceso, `dev-up.sh` se detiene y no lo mata: eliminar procesos ajenos sería una acción destructiva fuera del alcance del Núcleo.

`dev-refresh.sh` es destructivo respecto de la base local: borra el volumen `simus_sql_data` del proyecto Docker del Núcleo. No usarlo si hay información local que deba conservarse.

## Verificación operativa

El montaje termina solo tras confirmar `GET /api/salud` y la respuesta del frontend. La batería de integración se ejecuta aparte con `./scripts/dev-test-integracion.sh` y usa `simus_nucleo_pruebas`.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v01 | 2026_09_02 | REV | Formaliza los comandos de montar, desmontar y refrescar limpio. |
