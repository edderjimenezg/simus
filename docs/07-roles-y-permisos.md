# 7. Roles y permisos

## 7.1 Los dos espacios de acceso

SIMUS distingue **quién entra** antes que **qué puede hacer**. Hay dos espacios con puertas
separadas:

- El **espacio de la organización**, al que entra quien representa a una entidad registrada. Ve y
  administra únicamente los procesos de su propia organización.
- El **espacio de gestión administrativa**, al que entra el personal institucional. Ve el conjunto
  del sistema y decide sobre él.

Una cuenta pertenece a uno u otro canal de acceso, y esa separación es estructural: no se resuelve
con un permiso.

## 7.2 Los roles

| Rol | Alcance |
|---|---|
| `externo` | participante sin acceso administrativo; representa a una organización |
| `gestor_interno` | segundo nivel de administración institucional |
| `webmaster` | control total: usuarios, módulos, datos, configuración, revisión, publicación y mantenimiento |

Los roles viven en `Roles` y se asignan en `UsuariosRoles`. Una cuenta puede tener más de uno.

## 7.3 Los permisos

Los permisos son acciones concretas, no áreas. Se declaran en `Permisos` y se asocian a los roles en
`RolesPermisos`. Nombran lo que autorizan:

| Permiso | Autoriza |
|---|---|
| `festivales.decidir` | aprobar o devolver un registro en revisión |
| `festivales.normalizar` | corregir un registro sin pasar por el circuito |
| `propuestas.decidir` | aceptar o rechazar una propuesta de cambio, campo por campo |
| `entidades.administrar` | alta, edición y confirmación de organizaciones |
| `cms.editar`, `cms.publicar`, `cms.importar` | trabajar y publicar contenidos del sitio |
| `equipo_web.editar`, `equipo_web.publicar` | administrar el equipo que se muestra en el portal |
| `gobernanza.resolver_vinculos` | resolver duplicados y reclamaciones de administración |
| `monitor.consultar` | consultar el estado del sistema |

## 7.4 Los módulos se conceden por cuenta

Además del rol, **cada cuenta administrativa tiene una lista de módulos** que ve y gestiona en la
navegación izquierda de la consola. Vive en `ModulosPorCuenta`, con el módulo concedido, la fecha y
la cuenta que lo concedió.

Esta es una decisión de producto deliberada: **los permisos de la consola se definen por cuenta y no
por rol.** Dos personas con el mismo rol pueden tener módulos distintos, porque lo que determina qué
administra alguien es su encargo, no su nivel jerárquico.

Los módulos concedibles son: `ecosistema`, `mercados`, `organizaciones`, `catalogo-editorial`,
`agenda`, `noticias`, `galeria`, `banco-de-archivos`, `gestion-sitio`, `categorias`, `boletin`,
`analisis`, `auditoria`, `usuarios` y `sistema`.

## 7.5 Cómo nace una cuenta administrativa

Una cuenta administrativa **no se autogestiona**: la crea quien ya administra el sistema.

1. Se decide **antes de crearla** qué módulos tendrá.
2. Se entrega con correo electrónico y una contraseña por omisión.
3. En el primer ingreso, la persona debe cambiar la contraseña y completar su perfil.

El indicador `DebeCambiarContrasena` fuerza el primer paso y `PerfilCompletado` el segundo. Mientras
no los cumpla, la cuenta no opera.

## 7.6 Cómo nace una cuenta externa

Una organización se registra por su cuenta desde el espacio externo. **Sin correo electrónico
confirmado no puede registrar procesos**: la confirmación es la puerta.

Mientras no haya proveedor de correo contratado, la confirmación la realiza el equipo del PNMC desde
la consola, dejando registrado el motivo y la cuenta que la ejecutó. No se sustituye la verificación
real por un mecanismo permanente de simulación.

## 7.7 Qué queda registrado

Toda concesión y toda decisión quedan en la bitácora de auditoría con su cuenta ejecutora y su fecha.
Conceder un módulo, confirmar un correo, aprobar un registro o aceptar una propuesta de cambio son
actos atribuibles y consultables después.
