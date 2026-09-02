# Decisiones del núcleo SIMUS

**Versión:** v10  
**Estado:** APR  
**Fecha:** 2026_09_02  
**Deriva de:** ORI_Decisiones_nucleo_v09_APR_2026_09_02  
**Fuentes:** decisiones funcionales confirmadas el 2026_09_02; resuelve D-004 de ORI_Registro_decisiones_nucleo_v01_BOR_2026_09_02  
**Destinatario:** equipo funcional y técnico de SIMUS

## Decisiones aprobadas

Se mantienen las decisiones D-001 a D-020 de la versión anterior.

| ID | Decisión | Alcance |
|---|---|---|
| D-021 | La primera cuenta administradora del sistema (Webmaster) se aprovisiona mediante dos variables de entorno locales, correo y contraseña, leídas solo al preparar la base. La operación es idempotente: se ejecuta una única vez por base y queda registrada en `nucleo.EventosInicializacion`. Ninguna cuenta ni contraseña de prueba queda en el repositorio. | Identidad y aprovisionamiento técnico. |

## Reglas de interfaz y trazabilidad

- Las casillas empiezan sin seleccionar.
- Cada una enlaza al documento vigente antes de la aceptación.
- No se permite completar el registro sin ambas aceptaciones.
- Una actualización posterior de un documento no modifica ni borra el registro de una aceptación anterior.
- No se redactarán ni mostrarán textos jurídicos de muestra como si fueran vigentes.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v10 | 2026_09_02 | APR | Resuelve D-004 (aprovisionamiento del Webmaster) y retira `ORI_Registro_decisiones_nucleo` como serie activa; esta serie es la única fuente vigente de decisiones. |
