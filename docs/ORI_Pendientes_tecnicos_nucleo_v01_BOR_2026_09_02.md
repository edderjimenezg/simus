# Pendientes técnicos del núcleo SIMUS

**Versión:** v01  
**Estado:** BOR  
**Fecha:** 2026_09_02  
**Deriva de:** ORI_Decisiones_nucleo_v03_APR_2026_09_02  
**Fuentes:** decisiones funcionales confirmadas para el núcleo SIMUS  
**Destinatario:** equipo funcional y técnico de SIMUS

## Pendientes confirmados

| ID | Pendiente | Motivo para no simularlo | Condición para implementarlo |
|---|---|---|---|
| PT-001 | Verificación de correo de una cuenta externa. | Sin proveedor real de correo el código no llega a la persona y el alta quedaría bloqueada artificialmente. | Definir proveedor, remitente institucional, plantillas, vencimiento y reenvío. |
| PT-002 | Restablecimiento de contraseña. | Requiere entregar un enlace seguro a un correo comprobable. | Resolver PT-001 y definir enlaces de un solo uso, vencimiento y auditoría. |
| PT-003 | Invitación de nuevas personas administradoras. | Debe comprobar la identidad de quien recibe la invitación y permitir aceptar o rechazar el vínculo. | Resolver correo transaccional y definir el recorrido de invitación. |
| PT-004 | Gestión visible de múltiples organizaciones por una misma persona. | El modelo quedará preparado, pero se validará primero el alta inicial completa. | Terminar y probar identidad, organización y permisos de administración iniciales. |

## Regla de seguimiento

Un pendiente no se considera resuelto por añadir una pantalla, botón o dato ficticio. Debe existir recorrido completo: interfaz, API, reglas de seguridad, persistencia, notificación real cuando aplique y pruebas.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v01 | 2026_09_02 | BOR | Registra pendientes técnicos explícitos de identidad y correo. |
