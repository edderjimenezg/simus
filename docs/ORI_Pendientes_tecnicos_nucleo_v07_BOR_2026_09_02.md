# Pendientes técnicos del núcleo SIMUS

**Versión:** v07  
**Estado:** BOR  
**Fecha:** 2026_09_02  
**Deriva de:** ORI_Pendientes_tecnicos_nucleo_v06_BOR_2026_09_02 y ORI_Decision_tratamiento_datos_SIMUS_v01_APR_2026_09_02  
**Fuentes:** decisiones funcionales confirmadas para el núcleo SIMUS  
**Destinatario:** equipo funcional y técnico de SIMUS

## Pendientes confirmados

| ID | Pendiente | Estado actual | Condición para implementarlo |
|---|---|---|---|
| PT-001 | Activar verificación de correo en el registro externo. | El paso estará previsto en la interfaz y no bloqueará el alta. No existe proveedor de correo definido. | Definir proveedor, remitente institucional, dominio autorizado, plantillas, límites y monitoreo. Implementar código de seis dígitos y enlace seguro de un solo uso. |
| PT-002 | Restablecimiento de contraseña. | Fuera del primer recorrido de alta. | Reutilizar el servicio de correo verificado con enlaces de un solo uso, vencimiento y auditoría. |
| PT-003 | Invitación de nuevas personas administradoras. | Modelo previsto, interfaz fuera del primer corte. | Resolver correo transaccional y definir aceptación, rechazo, vencimiento y revocación. |
| PT-004 | Gestión visible de múltiples organizaciones por una misma persona. | Modelo previsto, interfaz fuera del primer corte. | Terminar y probar identidad, organización y permisos de administración iniciales. |
| PT-005 | Reglas de formato y validación por tipo de identificación. | Solo se definió el catálogo; no se inventan longitudes ni patrones. | Confirmar reglas aplicables por cada tipo antes de endurecer validaciones. |
| PT-006 | Definir si menores de edad pueden registrarse. | No habilitado. | Resolver alcance funcional, requisitos de representación y tratamiento de datos. |
| PT-007 | Confirmar aplicabilidad jurídica operativa de las referencias de MinCulturas para el dominio público definitivo de SIMUS. | Responsable y encargado ya definidos; documentos oficiales versionados en el núcleo. | Confirmación de la dependencia jurídica competente y definición de canales de atención de derechos para SIMUS. |

## Regla de seguimiento

Un pendiente no se considera resuelto por añadir una pantalla, botón o dato ficticio. Debe existir recorrido completo: interfaz, API, reglas de seguridad, persistencia, notificación real cuando aplique y pruebas.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v07 | 2026_09_02 | BOR | Sustituye la ausencia de textos por referencias institucionales aprobadas y conserva control jurídico de aplicabilidad. |
| v06 | 2026_09_02 | BOR | Trasladada a `_Historico/`. |
