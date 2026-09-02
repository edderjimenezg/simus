# Plan de construcción — panel de organización

**Versión:** v01  
**Estado:** APR  
**Fecha:** 2026_09_02  
**Deriva de:** ORI_Decisiones_nucleo_v09_APR_2026_09_02 y revisión funcional del panel de referencia  
**Fuentes:** decisiones confirmadas para el núcleo SIMUS; revisión visual y funcional del repositorio de referencia, sin reutilizar su código  
**Destinatario:** equipo funcional y técnico de SIMUS

## Propósito

Construir el panel externo nuevo a partir de los datos y permisos reales del núcleo. Una persona puede administrar varias organizaciones y cada organización puede tener varias personas administradoras, sin distinguir una persona “responsable” especial.

## Alcance del primer bloque

| Entrega | Estado esperado |
|---|---|
| Sesión y contexto | Consulta la persona autenticada y sus organizaciones administradas. |
| Resumen | Muestra la organización activa y estados reales, incluidos los vacíos. |
| Perfil de organización | Permite actualizar nombre, identificador opcional y territorio, verificando administración en backend. |
| Administradores | Lista las personas administradoras activas de la organización. |
| Cuenta y seguridad | Expone los datos personales básicos y el cierre de sesión; las acciones aún no implementadas se identifican como pendientes. |

## Límites explícitos

- No se crean Festivales, solicitudes, notificaciones ni indicadores simulados.
- No se implementa invitación, retiro ni modificación de otros administradores en este bloque. Requieren reglas institucionales y trazabilidad propias.
- La confirmación efectiva de correo continúa pendiente del proveedor de envío; el registro actual conserva la preparación no bloqueante ya aprobada.
- La actualización de datos de identidad personal se tratará en un bloque posterior, porque puede requerir validación documental.

## Rutas nuevas

| Ruta | Uso |
|---|---|
| `/mi-panel` | Resumen de la organización activa. |
| `/mi-panel/organizacion` | Perfil de la organización. |
| `/mi-panel/administradores` | Administradores de la organización. |
| `/mi-panel/cuenta-y-seguridad` | Cuenta personal y seguridad. |

## Criterios de aceptación

1. Toda consulta y edición valida la sesión y la administración de la organización en el backend.
2. Cambiar un identificador de URL no permite ver ni modificar otra organización.
3. Los estados de carga, vacío, error y validación se muestran de forma contextual.
4. El panel se adapta a móvil y escritorio sin posicionamientos manuales superpuestos.
5. No existe dependencia del panel, rutas, tablas ni APIs heredadas.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v01 | 2026_09_02 | APR | Define el primer bloque funcional del panel externo limpio. |
