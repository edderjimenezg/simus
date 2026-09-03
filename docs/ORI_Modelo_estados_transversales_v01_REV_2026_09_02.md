# Modelo de estados transversales — núcleo SIMUS

**Versión:** v01
**Estado:** REV
**Fecha:** 2026_09_02
**Deriva de:** revisión del modelo Festival y ORI_Plan_portal_publico_v02_BOR_2026_09_02
**Fuentes:** esquema y flujos vigentes del núcleo
**Destinatario:** equipo funcional y técnico de SIMUS

## Regla central

Los estados se gobiernan en un único catálogo `nucleo.Estados` y sus transiciones permitidas en `nucleo.TransicionesEstado`. No se crean tablas de estados por módulo ni se usa `editorial` para registros sectoriales.

## Ejes

| Eje | Propósito | Estados base |
|---|---|---|
| Registro | Existencia administrativa del registro | activo, archivado, retirado |
| Revisión | Validación institucional de una versión | borrador, en revisión, requiere ajustes, aprobado |
| Publicación | Exposición pública de un recurso aprobado | no publicado, publicado, retirado de publicación |
| Editorial | Solo futuro contenido editorial | se define con su primer dominio real |

Festival usa `EstadoRegistro` en su identidad y `EstadoRevision` en cada perfil versionado. La publicación no se adelanta: se incorpora cuando exista el recorrido institucional que la gobierne.

## Transiciones iniciales

- Registro: activo → archivado → activo; activo → retirado.
- Revisión: borrador → en revisión → aprobado o requiere ajustes; requiere ajustes → borrador.
- Publicación: no publicado → publicado → retirado de publicación.

Solo la API puede cambiar estados; la interfaz presenta las acciones permitidas por el estado retornado. Los futuros dominios reutilizan estos ejes o justifican formalmente una extensión.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v01 | 2026_09_02 | REV | Define catálogo único, ejes y transición inicial de Festival. |
