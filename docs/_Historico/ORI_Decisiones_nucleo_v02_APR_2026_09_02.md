# Decisiones del núcleo SIMUS

**Versión:** v02  
**Estado:** APR  
**Fecha:** 2026_09_02  
**Deriva de:** ORI_Registro_decisiones_nucleo_v01_BOR_2026_09_02  
**Fuentes:** decisiones funcionales confirmadas el 2026_09_02  
**Destinatario:** equipo funcional y técnico de SIMUS

## Decisiones aprobadas

| ID | Decisión | Alcance |
|---|---|---|
| D-001 | El núcleo es un repositorio independiente y no incorpora artefactos previos automáticamente. | Todas las capas. |
| D-002 | La base inicia sin registros operativos ni demostrativos. | Base de datos y pruebas. |
| D-003 | Cada capacidad se incorpora verticalmente y con trazabilidad completa. | Producto, API y datos. |
| D-004 | Angular, .NET y SQL Server se conservan como tecnologías, pero su implementación será nueva. | Plataforma. |
| D-005 | Desde identidad podrán iniciar sesión tanto Webmaster como personas externas. | Identidad. |
| D-006 | La primera cuenta Webmaster se aprovisionará una única vez mediante configuración técnica segura. | Identidad y despliegue. |

## Regla para el primer Webmaster

El correo y la contraseña iniciales no se guardarán en código, semillas, archivos de ejemplo ni historial Git. Se entregarán al despliegue mediante variables seguras y se consumirán una sola vez cuando exista el modelo de identidad. Después de ese aprovisionamiento, el mecanismo quedará inactivo.

## Alcance del corte 00

El corte técnico implementará únicamente:

- arranque de SQL Server vacío;
- API .NET con comprobación de disponibilidad de la base;
- aplicación Angular que consulta esa comprobación;
- configuración por variables de entorno;
- prueba de comunicación básica.

No implementará cuentas, formularios, roles persistidos, organizaciones ni Festivales. Estos pertenecen a los cortes siguientes.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v02 | 2026_09_02 | APR | Formaliza tecnologías, accesos iniciales y aprovisionamiento único del Webmaster. |
