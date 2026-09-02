# Plan de construcción — corte 02 Festival inicial

**Versión:** v01  
**Estado:** APR  
**Fecha:** 2026_09_02  
**Deriva de:** ORI_Plan_panel_organizacion_v01_APR_2026_09_02 y decisiones confirmadas para el núcleo SIMUS  
**Fuentes:** decisiones funcionales de SIMUS sobre administración de Festivales  
**Destinatario:** equipo funcional y técnico de SIMUS

## Propósito

Incorporar el primer proceso del núcleo: un Festival administrado por una organización. El diseño separa desde el comienzo la identidad permanente del Festival, sus perfiles versionados y sus futuras ediciones anuales.

## Modelo inicial

| Concepto | Tabla | Alcance de este corte |
|---|---|---|
| Festival | `festivales.Festivales` | Identidad canónica y organización administradora. |
| Perfil del Festival | `festivales.Perfiles` | Nombre, descripción y territorio guardados como borrador versionado. |
| Edición anual | No se crea aún. | Será una entidad separada y nunca modificará el perfil permanente. |

## Recorrido implementado

`Mi panel → Procesos → Registrar Festival → guardar borrador → continuar desde Mis procesos`.

La creación valida la sesión y que la persona sea administradora activa de la organización. El backend no acepta una organización arbitraria enviada desde el navegador.

## Límites del corte

- No hay publicaciones, revisión institucional, propuestas de modificación, coincidencias históricas, reclamaciones, IA, importaciones ni ediciones anuales.
- No hay datos de prueba ni Festivales presembrados.
- Los estados futuros se reservan en el modelo únicamente cuando expresan el ciclo editorial del perfil; no activan acciones que aún no existen.

## Criterios de aceptación

1. Crear un Festival crea su identidad y su primer perfil borrador en una transacción.
2. Una organización solo lista y continúa sus propios borradores.
3. Nombre, descripción y territorio se validan en backend y se reportan junto al campo.
4. El listado de procesos no se llena con indicadores inventados.
5. El esquema se aplica en orden y desde cero mediante scripts versionados del núcleo.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v01 | 2026_09_02 | APR | Define Festival inicial, perfil versionado y límites del segundo corte. |
