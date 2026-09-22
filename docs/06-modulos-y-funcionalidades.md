# 6. Módulos y funcionalidades

Qué hay dentro del sistema, espacio por espacio.

---

## 6.1 Portal público

| Sección | Ruta | Estado |
|---|---|---|
| Inicio | `/` | operativo |
| Sobre el PNMC | `/pnmc` | operativo |
| Ejes | `/ejes`, `/ejes/:componentSlug` | operativo |
| Catálogo Editorial | `/editorial`, `/editorial/:codigo` | operativo |
| Galería | `/galeria` | operativo |
| Noticias | `/noticias`, `/noticias/:slug` | operativo |
| Agenda | `/agenda` | operativo |
| Mapa ecosistémico | `/mapa-ecosistemico`, `/mapa-ecosistemico/sobre-el-mapa` | operativo |
| Ecosistema · Festivales | `/ecosistema/festivales[/:id]` | operativo |
| Ecosistema · Mercados musicales | `/ecosistema/mercados-musicales[/:id]` | operativo |
| Ecosistema · Territorios sonoros | `/ecosistema/territorios-sonoros[/:slug]` | operativo |
| Ecosistema · Prácticas musicales | `/ecosistema/practicas-musicales[/:slug]` | operativo |
| Registro e ingreso | `/registro`, `/ingresar` | operativo |

## 6.2 Espacio de la organización

Bajo `/gestion` y `/ecosistema/mi-panel`: resumen, organización, responsable, archivos, eventos,
solicitudes, seguridad de la cuenta, y los procesos que la organización registra —**Festivales**, con
sus Ediciones, y **Mercados musicales**, con las suyas—. Cada proceso tiene su lista
(`/gestion/procesos/{proceso}`), su alta (`/nuevo`) y su ficha (`/:id`).

Una organización **no puede registrar procesos sin correo confirmado**. Mientras no haya proveedor
de correo, la confirmación la hace el equipo del PNMC desde la consola, con motivo y registro.

## 6.3 Consola de gestión administrativa

**Dieciséis secciones en cinco grupos**, bajo `/administracion`. Salen de
`features/admin/domain/navegacion-administrativa.ts`, que es la única lista:

| Grupo | Secciones |
|---|---|
| Bandeja de trabajo | Resumen operativo · Solicitudes y revisiones |
| Ecosistema musical | Festivales y ediciones · Mercados musicales · Organizaciones y responsables |
| Publicaciones y mediateca | Catálogo Editorial · Agenda y eventos · Noticias y prensa · Categorías y proyectos · Banco de archivos |
| Sitio web y comunicación | Páginas y bloques (CMS) · Boletín informativo |
| Gobierno y control | Análisis y consultas · Auditoría y trazabilidad · Usuarios y roles · Salud del sistema |

**Dos secciones se retiraron y conviene decir por qué.** «Calidad y coincidencias» listaba en tres de
sus cinco pestañas los mismos trámites que la bandeja, leídos de los mismos endpoints: eran dos
pantallas para decidir lo mismo, y sus dos pestañas propias —duplicados y alertas de calidad— entraron
en la bandeja como dos posiciones más de su filtro. «Galería y memoria visual» no tenía detrás ningún
módulo: una entrada de navegación que lleva a una pantalla vacía promete algo que no existe.

Las secciones visibles dependen del **conjunto** de roles de la cuenta, no del rol principal: quien
tenga dos roles ve lo que le permiten los dos.

## 6.4 Los seis procesos del Ecosistema Musical

Escuelas de música · Escenarios · **Festivales** · Mercados musicales · Redes y documentación ·
Lutería.

Hoy **Festivales y Mercados musicales tienen directorio público, ficha y recuento**; los otros cuatro
están declarados y sin registros. El bloque del Inicio los muestra igualmente, con una raya donde iría la cifra: una raya
dice «no hay dato publicado», un cero diría «no hay ninguno».

El Festival es la entidad principal y **la Edición es un subelemento suyo**, nunca un módulo de
navegación equivalente.

## 6.5 Alcance de cada módulo

Funcionan de punta a punta: Festivales y Ediciones, **Mercados musicales y sus ediciones**,
Catálogo Editorial, Agenda, Noticias, Galería, Banco de archivos, CMS de páginas y bloques, Boletín,
Organizaciones, Usuarios y roles, Auditoría y el Mapa ecosistémico.

**Mercados musicales recorre el circuito entero**, igual que Festivales: se registra desde el espacio
externo o desde la consola, se envía a revisión, se devuelve campo por campo, se publica, se propone
un cambio sobre lo publicado y se pide su retiro; tiene ediciones con su propio ciclo, ficha pública,
recuento en el Inicio, presencia en el geovisor y consultas propias en la Consulta Guiada. Lo único
que le falta es la importación asistida, que espera una decisión de producto.

El **envío de correo** está construido por completo y expone un único punto de conexión con el
proveedor; mientras no haya proveedor contratado, las notificaciones se registran en el sistema y no
se simula su entrega.

---


---

## 6.6 El flujo de revisión y publicación

Es el mismo para cualquier proceso del Ecosistema, y es lo que define el producto.

### Los estados

| Estado | Qué significa |
|---|---|
| `borrador` | en elaboración, visible solo para quien lo trabaja |
| `en_revision` | enviado, a la espera de decisión institucional |
| `ajustes_solicitados` | devuelto con observaciones concretas |
| `publicado` | visible en el portal |
| `archivado` | retirado del flujo activo, sin borrarse |

### El recorrido

1. **Registro.** Una organización crea el proceso desde su espacio, o el equipo lo incorpora desde la
   consola. Nace en `borrador` y admite autoguardado: el formulario se puede dejar a medias.
2. **Comprobación de duplicados.** Al registrar, el sistema busca coincidencias con registros
   históricos reclamables y con los ya registrados, para que la misma realidad no entre dos veces.
3. **Envío a revisión.** Cuando el registro está completo, su responsable lo envía y queda
   `en_revision`. A partir de ahí no puede editarlo.
4. **Revisión campo por campo.** El equipo revisa por secciones y deja observaciones **sobre campos
   concretos**, no un comentario general. Puede aprobar o devolver.
5. **Ajustes.** Si se devuelve, el responsable ve exactamente qué campos hay que corregir, los
   atiende uno a uno y vuelve a enviar.
6. **Publicación.** Aprobado, el registro se publica y aparece en el portal. Queda guardada la
   versión publicada de la ficha.

### Sobre lo ya publicado

Un registro publicado **no se edita directamente**. Se distingue entre dos cosas:

- **Corregir el contacto público** —correo, teléfono y enlaces— es directo y no necesita aprobación:
  son datos operativos que deben poder actualizarse el mismo día.
- **Cambiar cualquier otro dato** exige una **propuesta de cambio**, que se registra campo por campo
  con el valor anterior y el propuesto. El equipo acepta o rechaza **cada campo**, y solo lo aceptado
  se aplica.

### Retirar un registro

Un borrador que nunca se publicó se elimina. **Lo que llegó a publicarse en algún momento se
archiva**, no se borra: quedó a la vista del público y su rastro debe poder consultarse. El sistema
lo decide por el historial del registro, no por su estado actual.
