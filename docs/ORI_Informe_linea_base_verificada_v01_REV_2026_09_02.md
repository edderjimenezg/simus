# Informe — línea base verificada del núcleo SIMUS

**Versión:** v01
**Estado:** REV
**Fecha:** 2026_09_02
**Deriva de:** ORI_Informe_corte_01_03_identidad_organizacion_festival_v01_REV_2026_09_02 y ORI_Plan_portal_publico_v02_BOR_2026_09_02
**Fuentes:** ejecución local controlada del repositorio, código vigente y resultados de compilación y pruebas del 2026_09_02
**Destinatario:** equipo funcional y técnico de SIMUS

## Propósito

Fijar una línea base reproducible antes de intervenir funcionalidades. Este informe no declara un flujo terminado solo porque compile: diferencia lo comprobado durante esta ejecución de lo que sigue pendiente de validación manual o funcional.

## Identificación del desarrollo vigente

La fuente de verdad es este repositorio local, `Repositorio - SIMUS Nucleo`, rama `main`, commit `e89cc2383f4bd7effd7b91b4b396a38346f2366c` (`feat: construir arquitectura de navegacion del portal publico`). El árbol estaba limpio al iniciar las verificaciones.

No tiene remoto Git configurado. `simus/` corresponde al desarrollo previo y `fuentes/` a entregas y referencias; no se intervinieron ni se usaron como ejecutable de esta línea base.

## Verificaciones ejecutadas

| Verificación | Resultado | Evidencia |
|---|---|---|
| Compilación .NET | Correcta, 0 advertencias y 0 errores | `dotnet build tests/Simus.Api.Tests/Simus.Api.Tests.csproj --no-restore` |
| Pruebas API sin base local | 6 correctas, 5 omitidas por diseño | `dotnet test tests/Simus.Api.Tests/Simus.Api.Tests.csproj --no-build` |
| Build Angular | Correcto | `npm run build` |
| Pruebas Angular | 1 correcta | `npm test -- --watch=false` |
| Integración con SQL Server | 11 correctas, 0 omitidas, 0 fallidas | `./scripts/dev-test-integracion.sh` |
| Salud local | API y base disponibles | `GET /api/salud` respondió `200` |

La integración cubrió registro de persona y organización, ingreso, actualización de organización, creación/listado de Festival y aislamiento entre organizaciones.

## Incidencia corregida durante la verificación

El script `scripts/dev-test-integracion.sh` fallaba antes de ejecutar las pruebas. En el mensaje de consola, la interpolación `$BASE_PRUEBAS…` interpretaba el carácter de elipsis como parte del nombre de variable bajo el entorno local y producía `unbound variable`.

Se corrigió a `${BASE_PRUEBAS}…`. Tras el ajuste, la misma batería de integración pasó 11/11. El cambio no modifica producto, datos de negocio ni contratos; hace ejecutable el procedimiento de verificación ya existente.

## Límites de esta ejecución

- La inspección visual automatizada en el navegador integrado no pudo acceder a `localhost`, por aislamiento de ese navegador. El portal y la API sí respondieron mediante `curl` desde el entorno local.
- No se realizó una prueba manual en navegador de escritorio, tableta y móvil. Por tanto, responsive, foco, navegación visible, consola de navegador y solicitudes de red visibles siguen pendientes de una sesión que pueda acceder al entorno local.
- La única prueba Angular actual verifica creación del shell; no cubre formularios, rutas, errores ni panel.
- La base de integración conserva la base local `simus_nucleo_pruebas`; sus pruebas usan datos aleatorios y no datos de negocio.

## Estado resultante

La línea base técnica es ejecutable en backend, frontend e integración. Los flujos de identidad, organización y Festival borrador tienen evidencia automatizada actual, pero permanecen **parciales** según el criterio integral del proyecto hasta completar la verificación manual, responsive y de experiencia visible.

## Riesgos y siguiente bloque

1. El repositorio vigente carece de remoto verificable: riesgo alto de respaldo y trazabilidad compartida.
2. Siguen pendientes las fronteras de despliegue y seguridad: CORS por entorno, políticas de roles, CSRF, cabeceras de seguridad y configuración de producción.
3. El siguiente bloque recomendado es **fronteras de seguridad y despliegue**, después de definir el destino institucional del remoto.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v01 | 2026_09_02 | REV | Registra la primera línea base ejecutada, el resultado de pruebas y la corrección del script de integración. |
