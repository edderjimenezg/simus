# Pendientes técnicos del núcleo SIMUS

**Versión:** v04  
**Estado:** BOR  
**Fecha:** 2026_09_02  
**Deriva de:** ORI_Pendientes_tecnicos_nucleo_v03_BOR_2026_09_02 y ORI_Decisiones_nucleo_v07_APR_2026_09_02  
**Fuentes:** decisiones funcionales confirmadas para el núcleo SIMUS  
**Destinatario:** equipo funcional y técnico de SIMUS

## Pendientes confirmados

| ID | Pendiente | Estado actual | Condición para implementarlo |
|---|---|---|---|
| PT-001 | Activar verificación de correo en el registro externo. | El paso estará previsto en la interfaz y no bloqueará el alta. No existe proveedor de correo definido. | Definir proveedor, remitente institucional, dominio autorizado, plantillas, límites y monitoreo. Implementar código de seis dígitos y enlace seguro de un solo uso. |
| PT-002 | Restablecimiento de contraseña. | Fuera del primer recorrido de alta. | Reutilizar el servicio de correo verificado con enlaces de un solo uso, vencimiento y auditoría. |
| PT-003 | Invitación de nuevas personas administradoras. | Modelo previsto, interfaz fuera del primer corte. | Resolver correo transaccional y definir aceptación, rechazo, vencimiento y revocación. |
| PT-004 | Gestión visible de múltiples organizaciones por una misma persona. | Modelo previsto, interfaz fuera del primer corte. | Terminar y probar identidad, organización y permisos de administración iniciales. |
| PT-005 | Catálogo de tipos de identificación de personas. | Debe existir antes de exponer el registro. | Definir y aprobar los tipos permitidos, sus etiquetas y reglas de longitud por tipo. |

## Regla de seguimiento

Un pendiente no se considera resuelto por añadir una pantalla, botón o dato ficticio. Debe existir recorrido completo: interfaz, API, reglas de seguridad, persistencia, notificación real cuando aplique y pruebas.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v04 | 2026_09_02 | BOR | Incorpora la doble alternativa futura de verificación y el catálogo de identificación pendiente. |
