# Plan de construcción — corte 00 Base técnica

**Versión:** v01  
**Estado:** APR  
**Fecha:** 2026_09_02  
**Deriva de:** ORI_Decisiones_nucleo_v02_APR_2026_09_02  
**Fuentes:** decisiones aprobadas para el núcleo SIMUS  
**Destinatario:** equipo funcional y técnico de SIMUS

## Resultado esperado

Una instancia local vacía puede iniciar SQL Server, API y frontend. El frontend muestra el estado real de la API y de su conexión a base de datos. No hay información de negocio persistida.

## Componentes mínimos

| Capa | Componente | Responsabilidad |
|---|---|---|
| Base de datos | SQL Server en Docker | Motor vacío y aislado. |
| API | `Simus.Api` | Ruta de salud y comprobación de conectividad. |
| Contrato | OpenAPI generado desde la API | Describe la única ruta técnica inicial. |
| Frontend | `simus-web` Angular | Consulta y presenta el estado técnico. |
| Configuración | `.env.example` | Declara nombres, puertos y secretos requeridos, sin valores reales. |
| Pruebas | API y frontend | Comprueban respuesta de salud y visualización de estado. |

## Fuera de alcance

- tablas de usuarios, roles, organizaciones o Festivales;
- autenticación y autorización;
- semillas, catálogos y datos de prueba;
- rutas de negocio;
- consola administrativa;
- componentes visuales definitivos.

## Criterio de aceptación

1. El arranque no usa archivos del proyecto anterior.
2. Una base nueva no contiene tablas de negocio.
3. La API informa por separado disponibilidad propia y de base de datos.
4. El frontend conserva estados de carga, disponible y error.
5. Ningún secreto queda versionado.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v01 | 2026_09_02 | APR | Define el alcance técnico mínimo antes de identidad. |
