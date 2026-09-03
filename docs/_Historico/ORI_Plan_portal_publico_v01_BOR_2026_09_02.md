# Plan de construcción — portal público SIMUS

**Versión:** v01
**Estado:** BOR
**Fecha:** 2026_09_02
**Deriva de:** decisión de abrir una ruta paralela al núcleo funcional, para construir la arquitectura pública de navegación desde ahora
**Fuentes:** decisión funcional confirmada para el núcleo SIMUS; alcance ya aprobado en `ORI_Carta_nucleo_Simus_v01` y en los planes de corte 00–02
**Destinatario:** equipo funcional y técnico de SIMUS

## Propósito

SIMUS no es solo un sistema al que una organización entra a gestionar información. Es también
una web pública que debe poder recorrerse de extremo a extremo. Esta ruta corre en paralelo al
**núcleo funcional y de gestión** (organizaciones, Festivales, ediciones, panel externo, panel
administrativo, revisión, publicación) y construye la **arquitectura definitiva** del portal
público — no una maqueta desechable, no una simulación de funciones que no existen.

## Cinco estados posibles de cada sección

Para no repetir la misma tabla de 7 columnas en cada sección, cada componente del portal se
clasifica en uno de estos cinco estados. Un componente puede combinar más de uno con el tiempo
(por ejemplo: hoy es "contenido fijo", en el corte siguiente pasa a "CMS").

| Estado | Significado | Regla |
|---|---|---|
| **Definitivo** | Estructura, navegación, componentes y textos ya son los finales. | Se construye completo desde ahora. No se retrabaja después salvo cambio de alcance. |
| **CMS** | El contenido lo administra una persona editora, no un desarrollador. | Requiere el modelo de contenido en `legal`/`contenido` (mismo patrón que `legal.Documentos` versionado), no texto fijo en el componente. |
| **Dinámico** | Consume datos reales del núcleo (Festivales, organizaciones, territorio). | No se construye hasta que el endpoint de lectura pública exista y esté probado. Mientras tanto, la sección permanece deshabilitada — no se simula con datos de ejemplo dentro del componente definitivo. |
| **Pendiente de desarrollo** | Depende de un módulo que el núcleo todavía no tiene (mapa, agenda, editorial). | Se reserva la ruta y el lugar en el menú, con un estado explícito de "aún no disponible" — igual que ya hace la referencia con sus procesos deshabilitados, pero sin dejar el enlace roto o ausente. |
| **Inactivo temporal** | Existe en la navegación pero la función real está apagada a propósito. | Debe decir explícitamente qué falta, con el mismo estándar de `ORI_Estandar_errores_y_trazabilidad` — nunca un enlace muerto ni un botón que no explica su ausencia. |

## Regla de datos provisionales

Ningún dato de ejemplo vive dentro de un componente definitivo ni en su base de datos de
desarrollo compartida. Cuando una sección "Dinámica" necesita mostrarse antes de tener su fuente
real, se resuelve con **contenido CMS explícitamente marcado como editorial** (una nota de
prensa real, un texto institucional real), nunca con datos de negocio inventados (Festivales,
organizaciones o ediciones ficticias) — esa línea ya la trazó `ORI_Carta_nucleo`para el núcleo y
aplica igual aquí.

## Clasificación por sección

| Sección | Estado hoy | Depende de | Reutiliza |
|---|---|---|---|
| Home | Definitivo + CMS (bloques destacados) | Ediciones/Agenda para "próximamente" (Dinámico, deshabilitado hasta entonces) | Encabezado y pie del portal, tarjeta de contenido |
| Ecosistema musical | Definitivo (estructura) + CMS (descripciones de capa) | Nada para publicarse; los contadores reales son Dinámicos y llegan después | Tarjeta de contenido, listado con estado vacío |
| Mapa ecosistémico | Pendiente de desarrollo | Módulo de georreferenciación y datos de Festivales/agentes publicados (no existen aún) | — |
| Festivales (capa pública) | Estructura Definitiva, listado Dinámico | Endpoint público de Festivales **publicados** (hoy solo existe el privado de borradores) | Tarjeta de Festival, filtro por territorio (comparte lógica con el selector de departamento/municipio ya construido) |
| Agenda | Pendiente de desarrollo | Modelo de ediciones anuales (corte 03+), aún no existe | Tarjeta de evento (compartida con Festivales si se diseña bien desde el inicio) |
| Noticias | Definitivo (estructura) + CMS (contenido) | Nada bloqueante | Tarjeta de contenido, paginación |
| Editorial y contenidos | Definitivo (estructura) + CMS | Nada bloqueante para artículos; enlaces a Festivales/agenda son Dinámicos | Tarjeta de contenido, encabezado de artículo |
| Información institucional PNMC/SIMUS | Definitivo + CMS | Nada bloqueante — puede usar las referencias ya versionadas en `legal.Documentos` | Página informativa base |
| Páginas informativas y de contexto | Definitivo + CMS | Nada bloqueante | Página informativa base |
| Ayuda, tutoriales y contacto | Definitivo (estructura) + CMS (contenido) | El formulario de contacto real requiere el servicio de correo institucional (mismo bloqueo que PT-001); mientras tanto el canal de contacto se publica como dato informativo, no como formulario que promete un envío que no ocurre | Página informativa base |
| Accesos a inicio de sesión y registro | Definitivo — ya existe y funciona | Nada; es el punto de unión con el núcleo | — |

## Orden de construcción propuesto

No se construyen las 11 secciones a la vez. El orden respeta la misma lógica del núcleo: primero
lo que no depende de nada, después lo que solo depende de CMS, al final lo que depende de
desarrollo pendiente.

1. **Arquitectura de navegación global**: menú, pie de página, jerarquía de rutas, transición
   entre portal público y acceso/panel privado (el "Accesos a inicio de sesión y registro" ya
   construido se integra aquí como el primer punto de unión real).
2. **Componentes reutilizables base**: encabezado, pie, tarjeta de contenido, página informativa
   genérica, estado vacío, estado "aún no disponible". Se construyen una sola vez y los usan
   todas las secciones siguientes.
3. **Secciones 100% Definitivo + CMS, sin dependencias**: Información institucional,
   páginas informativas, Ayuda/contacto (sin formulario funcional todavía), Noticias, Editorial.
4. **Home y Ecosistema musical**: dependen de que existan 2–3 secciones reales para enlazar
   (evita un home que apunta a páginas vacías).
5. **Festivales (capa pública)**: requiere primero el endpoint público de lectura (Festivales
   publicados) — trabajo del núcleo, no del portal, y bloquea esta sección hasta entonces.
6. **Mapa ecosistémico y Agenda**: quedan con ruta reservada y estado "Pendiente de desarrollo"
   desde el paso 1; se activan cuando sus módulos de datos existan.

## Criterio de aceptación

1. Ninguna sección "Definitivo" o "CMS" depende de datos de ejemplo dentro del código.
2. Toda sección "Pendiente de desarrollo" o "Inactivo temporal" es visible en la navegación y
   explica por qué no está disponible — nunca un enlace roto ni ausente sin explicación.
3. El tránsito entre portal público y `/mi-panel` (ya construido) no se retrabaja: se integra tal
   cual.
4. Ningún componente reutilizable se duplica entre secciones; se construye una vez en el paso 2.
5. Antes de construir cada sección se aplica la regla de referencia ya vigente
   (`[[feedback-frontend-referencia-primero]]`): revisar la referencia por función/estética,
   nunca copiar, y verificar responsive en varias resoluciones antes de dar por terminada.

## Pendiente de decisión

- Confirmar si "Editorial y contenidos" y "Noticias" son la misma sección con dos vistas o dos
  módulos de contenido distintos (impacta el modelo de datos del CMS).
- Definir el modelo de contenido CMS (tabla versionada al estilo `legal.Documentos`, o motor de
  contenido separado) antes de construir la primera sección CMS.
- Confirmar el orden relativo entre "Festivales (capa pública)" y "Mapa ecosistémico": si el mapa
  depende de Festivales publicados, su desarrollo no puede adelantarse a esa sección.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v01 | 2026_09_02 | BOR | Propone la ruta de trabajo paralela para el portal público, clasificación por sección y orden de construcción. |
