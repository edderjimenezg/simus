# Estándar de errores y trazabilidad

**Versión:** v01  
**Estado:** APR  
**Fecha:** 2026_09_02  
**Deriva de:** ORI_Decisiones_nucleo_v09_APR_2026_09_02  
**Fuentes:** decisión funcional confirmada para el núcleo SIMUS  
**Destinatario:** equipo funcional y técnico de SIMUS

## Regla general

Toda operación debe diferenciar lo que la persona puede corregir de aquello que corresponde al sistema. Ningún mensaje técnico, excepción, contraseña, token o dato sensible se muestra en la interfaz ni se registra en texto abierto.

## Contrato de error

| Elemento | Uso |
|---|---|
| `codigo` | Identificador estable y legible para soporte, por ejemplo `registro_no_disponible`. |
| `mensaje` | Explicación breve en español para la persona. |
| `campos` | Errores por campo, cuando la persona puede corregirlos. |
| `trazaId` | Referencia técnica para investigar un fallo sin pedir capturas ni datos privados. |

## Comportamiento de interfaz

- Cada campo inválido se marca y muestra su mensaje inmediato.
- Al enviar con errores, el foco llega al primer campo pendiente.
- Los valores escritos se conservan.
- Los mensajes generales se reservan para conexión, permisos, sesión, disponibilidad o fallos inesperados.
- La interfaz puede mostrar el código de referencia de un error global para soporte, pero no detalles técnicos.

## Estados HTTP

| Estado | Uso |
|---|---|
| 400 | Solicitud inválida. |
| 401 | Sesión inexistente o vencida. |
| 403 | La persona no tiene permiso. |
| 404 | Recurso inexistente. |
| 409 | Dato duplicado o conflicto de estado. |
| 422 | Validación de campos. |
| 429 | Demasiados intentos. |
| 500 | Error no previsto. |
| 503 | Servicio o dependencia no disponible. |

## Registro técnico

Cada petición registra método, ruta, estado, duración y `trazaId`. Los errores no previstos se registran con el detalle necesario para diagnóstico en el servidor. Se excluyen explícitamente contraseñas, cookies, tokens, números de identificación y contenido de formularios.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v01 | 2026_09_02 | APR | Define mensajes, códigos, trazabilidad y manejo de errores transversal. |
