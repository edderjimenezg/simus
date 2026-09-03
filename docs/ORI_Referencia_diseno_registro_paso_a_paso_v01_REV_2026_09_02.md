# Referencia de diseño — registro externo paso a paso

**Versión:** v01  
**Estado:** REV  
**Fecha:** 2026_09_02  
**Deriva de:** decisión funcional de convertir el registro externo de una sola pantalla con scroll a un recorrido por pasos  
**Fuentes:** revisión funcional y visual del flujo de registro en `fuentes/Entregas - Divergente/Repositorio - Agosto 31/pnmc-web` (y su copia idéntica en `... - Reconstruccion`), sin reutilizar su código  
**Destinatario:** equipo funcional y técnico de SIMUS

## Qué se observó en la referencia

- 2 pasos: "La organización" y "Acceso y responsabilidad". Un recorrido anterior de hasta 7 pantallas se colapsó a estos 2 porque las intermedias no aportaban.
- Indicador de pasos numerado, clicable, sin restricción para saltar entre pasos.
- Validación agregada: no bloquea el avance mientras se escribe; solo al enviar valida todo el formulario, salta al paso donde vive el primer campo con error y le da foco ahí.
- Un solo objeto de estado en memoria; una sola llamada a la API al confirmar el último paso.
- Layout de dos columnas a pantalla completa (marca fija + columna de formulario con su propio scroll).

## Qué se descarta

- El corte exacto en 2 pasos de la referencia: el núcleo ya tenía su propia segmentación en 3 bloques (`Datos personales`, `Organización y territorio`, `Consentimientos`), y forzar el corte de la referencia habría mezclado un formulario de identidad con datos de organización sin necesidad.
- El layout de dos columnas a pantalla completa: es diseño visual de la referencia, no lógica de interacción. El núcleo conserva su propia superficie de formulario ya vigente.
- Cualquier fragmento de código, nombre de clase o markup de la referencia. La revisión fue solo funcional/visual.

## Qué se decide para el núcleo

- 3 pasos, uno por cada bloque que el núcleo ya tenía como `<fieldset>`: `Datos personales` → `Organización y territorio` → `Consentimientos`.
- Indicador numerado y clicable arriba del formulario (círculos con número o marca de check), con el mismo sistema de color ya vigente (morado para el paso activo, verde para los completados).
- Validación agregada solo al enviar en el paso final: si hay errores, el paso salta automáticamente al primero que los contiene y enfoca el campo, igual que hacía el registro en una sola pantalla — no se pierde ese comportamiento, se adapta a que ahora el campo puede estar en un paso no visible.
- Una sola solicitud a `/api/registro` al confirmar el paso 3, sin llamadas intermedias por paso.
- Botones "Atrás" (desde el paso 2) y "Siguiente" (pasos 1–2); en el paso 3 el botón principal es el envío final, sin pantalla de resumen adicional.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v01 | 2026_09_02 | REV | Documenta la revisión de referencia y la adaptación decidida para el registro paso a paso del núcleo. |
