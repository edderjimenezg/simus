# Plan de construcción — portal público SIMUS

**Versión:** v02
**Estado:** BOR
**Fecha:** 2026_09_02
**Deriva de:** ORI_Plan_portal_publico_v01_BOR
**Fuentes:** decisiones confirmadas para el núcleo SIMUS; revisión funcional de Noticias, Editorial, Agenda, Ecosistema musical y Mapa ecosistémico en `fuentes/Entregas - Divergente/Repositorio - Agosto 31/pnmc-web` (y su copia idéntica en `... - Reconstruccion`), sin reutilizar su código
**Destinatario:** equipo funcional y técnico de SIMUS

## Propósito

Continúa `ORI_Plan_portal_publico_v01_BOR`: resuelve sus tres preguntas abiertas y registra el
primer avance de construcción (arquitectura de navegación y componentes reutilizables).

## Preguntas resueltas

### 1. Editorial, Noticias y Agenda son tres elementos separados

Confirmado. La referencia los mantiene como páginas independientes, y su propia estructura de
datos lo justifica: no comparten forma.

- **Noticias**: título, resumen, cuerpo, autor, categoría libre, fecha de publicación, imagen —
  forma de artículo corto, feed cronológico con destacadas.
- **Editorial**: no es un feed de artículos, es un **catálogo bibliográfico/documental** — resumen,
  año, sección temática, tipo de publicación, autor y autor corporativo, identificadores
  bibliográficos, formato, páginas, ubicación física, palabras clave. Se navega como catálogo con
  filtros, no como feed.
- **Agenda**: eventos con fecha/hora de inicio y fin, cobertura territorial, lugar, organizador,
  y un vínculo *opcional* a un Festival (no obligatorio — la Agenda funciona sin Festivales
  vinculados).

Confirma que forzar estas tres secciones en un único modelo de "contenido genérico" habría sido
un error: se construyen como tres modelos de datos distintos, cada uno con su propia tabla.

### 2. Motor de contenido (CMS)

**Se extiende el propio núcleo — SQL Server y `Simus.Api` — en vez de traer un producto CMS
externo (Strapi, Contentful u otro).**

Razones:

- El núcleo ya resuelve exactamente el problema que un CMS resolvería: contenido versionado,
  con estado y trazabilidad, administrado por rol. `legal.Documentos` ya es contenido versionado
  administrable (código, versión, vigencia). `festivales.Perfiles.EstadoEditorial` ya tiene un
  ciclo de vida editorial (`borrador`, `en_revision`, `publicado`, `requiere_ajustes`, `retirado`).
- La referencia confirma el mismo patrón por su cuenta: Noticias, Editorial y Agenda comparten
  ahí un único ciclo de estados de publicación validado contra una tabla maestra. Es la misma idea
  que el núcleo ya construyó para Festivales, con distinto vocabulario.
- Traer un CMS externo obligaría a reconciliar dos sistemas de identidad y permisos (el de SIMUS
  y el del CMS), a correr un servicio adicional en `docker-compose.yml`, y a mantener sincronizados
  los roles de una persona editora en dos lugares. Ninguna de esas complicaciones aporta algo que
  el propio patrón del núcleo no resuelva ya.

**Decisión concreta**: nuevo esquema `contenido` en la misma base de datos, una tabla por tipo de
contenido (Noticias, Editorial, Agenda — nunca una tabla genérica de "contenido"), reutilizando
el vocabulario de estado ya usado en `festivales.Perfiles.EstadoEditorial`
(`borrador`/`en_revision`/`publicado`/`requiere_ajustes`/`retirado`), un nuevo rol
`editor_contenido` en `identidad.Roles`, y endpoints de lectura pública separados de los de
administración — siguiendo el mismo patrón `ObtenerConexionAutorizadaAsync` /
`PuedeAdministrarOrganizacionAsync` ya construido, adaptado a contenido en vez de organizaciones.
El diseño exacto de columnas se define al construir cada tipo, no de antemano.

### 3. El Mapa ecosistémico depende de Festivales públicos

Confirmado: no se desarrolla todavía. Ambas secciones quedan en estado "Pendiente de desarrollo"
en la navegación, con su razón visible.

## Avance de construcción — arquitectura de navegación (Fase 1)

Completado y verificado (escritorio y móvil, sin desborde horizontal):

- `PortalLayoutComponent`: encabezado con marca, navegación (menú horizontal en escritorio, menú
  desplegable con botón de apertura en móvil) y pie de página. Vive fuera de `/ingresar` y
  `/registro`, que conservan su pantalla propia ya construida.
- Componentes reutilizables: `PaginaInformativaComponent` (página informativa base, con
  proyección de contenido) y `SeccionPendienteComponent` (estado "aún no disponible" honesto,
  con el motivo explicado, reutilizado en las seis secciones que todavía no tienen datos reales).
- `NAVEGACION_PORTAL` en `portal-nav.ts` es la única fuente de verdad de qué secciones existen,
  su ruta, y si están disponibles — la navegación y las rutas de Angular se generan desde ahí,
  para que no se puedan desincronizar.
- Páginas completas: Home (estructura definitiva, con texto ya existente en el resto del
  producto, ninguna cifra ni afirmación institucional inventada) e Institucional (responsable y
  encargado del tratamiento de datos, ya aprobados en `ORI_Decision_tratamiento_datos_SIMUS`) y
  Ayuda (registro, ingreso, y aviso honesto de que el contacto directo depende del servicio de
  correo institucional, igual que ya se explica en el registro).
- La raíz (`/`) ahora es la Home pública; antes redirigía a `/ingresar`.

Pendiente de esta fase: construir el esquema `contenido`, el primer tipo (Noticias, por ser el
más simple de los tres) de punta a punta (modelo → autorización → endpoint → interfaz pública →
interfaz de administración → prueba automatizada), y luego Editorial y Agenda con su propio
modelo. Festivales públicos y Mapa ecosistémico siguen bloqueados por el núcleo, no por el portal.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v02 | 2026_09_02 | BOR | Resuelve las tres preguntas abiertas y registra la primera fase de construcción (navegación y componentes reutilizables). |
| v01 | 2026_09_02 | BOR | Trasladada a `_Historico/`. |
