# Decisiones del núcleo SIMUS

**Versión:** v07  
**Estado:** APR  
**Fecha:** 2026_09_02  
**Deriva de:** ORI_Decisiones_nucleo_v06_APR_2026_09_02  
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
| D-013 | El recorrido de registro reservará una etapa de confirmación de correo, pero mientras no exista un proveedor de correo real permanecerá deshabilitada y no bloqueará el alta. | Registro externo y evolución de correo. |
| D-014 | Tras completar el registro, el sistema inicia sesión automáticamente y lleva a la persona a su panel de organización. | Experiencia de acceso. |
| D-015 | Las sesiones no son permanentes: deben vencer por inactividad y tener una duración máxima, aun cuando exista actividad. | Seguridad de sesión. |
| D-016 | Cuando exista correo transaccional, la confirmación ofrecerá dos alternativas equivalentes: ingresar un código de seis dígitos o abrir un enlace seguro de un solo uso. | Verificación de correo. |

## Datos mínimos para el alta pública

### Organización

| Dato | Regla inicial |
|---|---|
| Nombre de la organización | Obligatorio. |
| Departamento | Obligatorio; se elige de DIVIPOLA. |
| Municipio | Obligatorio; se habilita según el departamento elegido y proviene de DIVIPOLA. |
| Identificación de la organización | Opcional. No se asume un tipo específico mientras no se defina la taxonomía institucional. |

### Persona administradora inicial

| Dato | Regla inicial |
|---|---|
| Primer nombre | Obligatorio. |
| Segundo nombre | Opcional. |
| Primer apellido | Obligatorio. |
| Segundo apellido | Opcional. |
| Tipo de identificación | Obligatorio; se elige de un catálogo controlado. |
| Número de identificación | Obligatorio. |
| Correo electrónico | Obligatorio; identifica el acceso de la persona. |
| Teléfono | Opcional. |

Después del alta, el panel podrá solicitar completar el perfil. Los campos posteriores no se incluirán ahora hasta definir su propósito, validación y uso.

## Criterio de sesión para diseñar el corte de identidad

La implementación usará una cookie segura no accesible desde JavaScript. Como punto de partida técnico se propondrá vencimiento tras 30 minutos sin actividad, aviso antes de vencer y una duración máxima de 8 horas. Estos valores se documentarán como parámetros configurables y se validarán antes de un despliegue público.

## Regla para el primer Webmaster

El correo y la contraseña iniciales no se guardarán en código, semillas, archivos de ejemplo ni historial Git. Se entregarán al despliegue mediante variables seguras y se consumirán una sola vez cuando exista el modelo de identidad. Después de ese aprovisionamiento, el mecanismo quedará inactivo.

## Regla de transparencia para correo

Mientras no exista correo transaccional, el sistema no generará códigos, enlaces, mensajes de “correo enviado” ni marcará una dirección como verificada. La etapa se mostrará como preparación futura y permitirá continuar el registro. Cuando se conecte un proveedor, se habilitarán el código y el enlace en el mismo punto del recorrido, sin reestructurar cuentas u organizaciones.

## Alcance siguiente: identidad

El siguiente corte introduce únicamente las estructuras y recorridos necesarios para cuenta, rol, organización, vínculo administrador y sesión. La interfaz reservará la etapa futura de correo, sin simular la verificación. No incorporará Festivales, procesos, coincidencias ni datos demostrativos.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v07 | 2026_09_02 | APR | Define datos mínimos de alta y dos alternativas futuras de verificación de correo. |
