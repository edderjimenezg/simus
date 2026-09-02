# Pendientes técnicos del núcleo SIMUS

**Versión:** v02  
**Estado:** BOR  
**Fecha:** 2026_09_02  
**Deriva de:** ORI_Pendientes_tecnicos_nucleo_v01_BOR_2026_09_02 y ORI_Decisiones_nucleo_v05_APR_2026_09_02  
**Fuentes:** decisiones funcionales confirmadas para el núcleo SIMUS  
**Destinatario:** equipo funcional y técnico de SIMUS

## Pendientes confirmados

| ID | Pendiente | Estado actual | Condición para implementarlo |
|---|---|---|---|
| PT-001 | Servicio de correo para verificar el registro externo. | Requisito del corte de identidad; sin proveedor definido. | Definir proveedor, remitente institucional, dominio autorizado, plantillas, límites y monitoreo. |
| PT-002 | Restablecimiento de contraseña. | Fuera del primer recorrido de alta. | Reutilizar el servicio de correo verificado con enlaces de un solo uso, vencimiento y auditoría. |
| PT-003 | Invitación de nuevas personas administradoras. | Modelo previsto, interfaz fuera del primer corte. | Resolver correo transaccional y definir aceptación, rechazo, vencimiento y revocación. |
| PT-004 | Gestión visible de múltiples organizaciones por una misma persona. | Modelo previsto, interfaz fuera del primer corte. | Terminar y probar identidad, organización y permisos de administración iniciales. |

## Regla de seguimiento

Un pendiente no se considera resuelto por añadir una pantalla, botón o dato ficticio. Debe existir recorrido completo: interfaz, API, reglas de seguridad, persistencia, notificación real cuando aplique y pruebas.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v02 | 2026_09_02 | BOR | Actualiza correo de verificación como requisito del corte de identidad. |
