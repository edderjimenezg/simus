# Decisiones del núcleo SIMUS

**Versión:** v04  
**Estado:** APR  
**Fecha:** 2026_09_02  
**Deriva de:** ORI_Decisiones_nucleo_v03_APR_2026_09_02  
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
| D-007 | Persona, organización y vínculo de administración son conceptos y datos independientes. | Modelo de identidad y organizaciones. |
| D-008 | La experiencia inicial de alta será única: crea persona externa, organización y vínculo de administración inicial en una transacción. | Registro externo. |
| D-009 | Una persona puede administrar varias organizaciones y una organización puede tener varias personas administradoras. El primer corte solo expondrá el caso inicial de una persona y una organización. | Modelo de vínculos y evolución funcional. |
| D-010 | No se distinguirá una categoría de “persona responsable”. Toda persona con capacidad de gestión será administradora de la organización. | Roles externos. |
| D-011 | El correo de acceso identifica a la persona; el correo de contacto identifica a la organización. No son el mismo dato ni dependen uno del otro. | Datos y validaciones. |
| D-012 | Cualquier persona puede iniciar públicamente el registro desde el portal. La creación inicial no requiere aprobación institucional. | Acceso público. |

## Regla para el primer Webmaster

El correo y la contraseña iniciales no se guardarán en código, semillas, archivos de ejemplo ni historial Git. Se entregarán al despliegue mediante variables seguras y se consumirán una sola vez cuando exista el modelo de identidad. Después de ese aprovisionamiento, el mecanismo quedará inactivo.

## Pendiente técnico explícito

La verificación de correo, el restablecimiento de contraseña y las invitaciones a otras personas administradoras no se implementarán de forma simulada. Requieren un servicio real de envío de correo, enlaces de un solo uso, vencimiento, revocación y trazabilidad. Permanecen registrados como pendientes antes de exponer esas experiencias al público.

## Alcance siguiente: identidad

El siguiente corte puede introducir únicamente las estructuras y recorridos necesarios para acceso e identidad: cuenta, rol, organización, vínculo administrador y creación única inicial. El portal expondrá este recorrido a cualquier persona. No incorporará Festivales, procesos, coincidencias ni datos demostrativos.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v04 | 2026_09_02 | APR | Define el registro público sin aprobación previa. |
