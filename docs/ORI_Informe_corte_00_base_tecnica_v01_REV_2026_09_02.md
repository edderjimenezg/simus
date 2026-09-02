# Informe de implementación — corte 00 Base técnica

**Versión:** v01  
**Estado:** REV  
**Fecha:** 2026_09_02  
**Deriva de:** ORI_Plan_corte_00_base_tecnica_v01_APR_2026_09_02  
**Fuentes:** implementación y validaciones locales del núcleo SIMUS  
**Destinatario:** equipo funcional y técnico de SIMUS

## Entregado

| Capa | Entrega | Estado |
|---|---|---|
| Repositorio | Repositorio Git independiente, sin historial ni artefactos del desarrollo anterior. | Completado |
| Base de datos | Definición de SQL Server 2022 en Docker, con volumen propio y sin esquema de negocio. | Completado |
| API | `Simus.Api` .NET con `GET /api/health`. Distingue API no configurada, base no configurada y base no disponible. | Completado |
| Frontend | `simus-web` Angular con una pantalla técnica que consulta el estado real de la API. | Completado |
| Configuración | `.env.example` sin secretos y guía para variables locales no versionadas. | Completado |
| Pruebas | Prueba de integración de API y dos pruebas de frontend. | Completado |

## Verificaciones realizadas

| Verificación | Resultado |
|---|---|
| Compilación de `Simus.Api` | Correcta, sin advertencias ni errores. |
| Prueba de integración de API | Correcta: 1 de 1. Comprueba que, sin conexión configurada, la API responde `503` y no simula disponibilidad. |
| Compilación de Angular | Correcta. |
| Pruebas de Angular | Correctas: 2 de 2. |
| Ejecución manual sin configuración de base | Correcta: `GET /api/health` respondió `503` con `api: unavailable` y `database: not-configured`. |

## Pendiente verificable localmente

No se configuró una contraseña real de SQL Server ni se creó un archivo `.env`; hacerlo habría expuesto una configuración que debe permanecer local. La imagen de SQL Server tampoco está disponible aún en Docker local. Por ello queda pendiente una única comprobación manual positiva: iniciar el contenedor con una contraseña local, configurar la cadena de conexión y confirmar que `GET /api/health` devuelve disponibilidad de API y base.

Esto no bloquea el cierre del código del corte 00: no hay tablas de negocio, semillas, cuentas ni secretos incorporados.

## Riesgos y límites actuales

- Aún no existe identidad, por decisión de alcance.
- La API no tiene rutas de negocio, por decisión de alcance.
- La prueba positiva contra SQL debe realizarse antes de iniciar el corte que persista identidades.

## Siguiente paso propuesto

Definir el comportamiento de identidad en lenguaje funcional y construir el corte 01 de forma vertical: modelo mínimo, API, interfaz de acceso, permisos y pruebas. No se crearán organizaciones ni Festivales hasta revisar ese recorrido completo.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v01 | 2026_09_02 | REV | Reporta la implementación, evidencias y pendiente verificable del corte 00. |
