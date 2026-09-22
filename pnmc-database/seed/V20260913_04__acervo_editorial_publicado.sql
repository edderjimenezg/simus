/*
  SIMUS · Catálogo Editorial — el acervo sale al portal, con su fuente y su atribución

  publicaciones que te compartí son reales, podemos hacer que se puedan bajar y atribuírselas al
  webmaster; luego podremos cambiar la atribución». Hasta ese momento el acervo estaba cargado y el
  catálogo público mostraba CERO, porque las 171 entraron como la fuente las declara —«Pendiente
  revisión»— y sin derechos verificados. Publicarlas no es algo que el sistema haga solo: es esta
  decisión, y por eso está escrita aquí con fecha y con quien la tomó.

  LAS CUATRO CONDICIONES DE LA PUERTA PUBLICA, Y ESTE GUION LAS CUMPLE UNA A UNA. `EsVisiblePublicamente`
  exige ficha validada, estado publicado, al menos una fuente y derechos que permitan publicar la
  ficha. No se toca la puerta: se le da a cada ficha lo que le falta.

  SE PUBLICA LA FICHA, NO EL ARCHIVO. `DerechosPermitePublicarArchivo` se queda en 0 en las 171 y en
  sus accesos. Enseñar la ficha bibliográfica de una obra que el propio Ministerio editó e imprimió
  —título, autoría, ISBN, dónde se consulta— es lo que hace cualquier catálogo de biblioteca.
  Repartir el fichero es otra autorización y nadie la ha dado.

  NO SE INVENTA UNA LICENCIA. `DerechosLicenciaONota` se queda como está —vacía—: la fuente no
  declara ninguna y escribir «Todos los derechos reservados» por omisión sería inventar una
  afirmación jurídica que nadie hizo. Es la misma decisión que tomó el desarrollo de septiembre.

  LA ATRIBUCION SE RESUELVE, NO SE CODIFICA. El usuario ejecutor se busca por ROL —el webmaster
  activo más antiguo— y la entidad de procedencia por su bandera `EsInstitucional`. Escribir aquí un
  identificador fijo ataría el acervo a la cuenta de una base concreta, y el dueño ya avisó de que la
  atribución va a cambiar: cambiarla tiene que ser mover una fila, no editar una migración aplicada.

  EL CONTEXTO ES `importacion` Y NO `administrativo`. Es lo que de verdad pasó y lo que ese valor
  significa en `ProcedenciaDeRegistros`: la ejecuta un funcionario pero los datos vienen de una
  fuente de fuera. Llamarlo administrativo diría que alguien los escribió en la consola.

  ES UNA SEMILLA Y NO UNA MIGRACION, Y ESO IMPORTA. Nació como migración y funcionaba solo por
  casualidad: el acervo ya estaba cargado cuando se aplicó. Las migraciones corren ANTES que las
  semillas —`scripts/seed-local-db.sh` ejecuta `migrar` y después la lista de `seed/`—, así que en
  una instalación nueva esto se ejecutaba sobre una base sin publicaciones y no hacía nada: el
  catálogo público quedaba en CERO. Se descubrió al volver a sembrar el acervo para comprobar otra
  cosa, y el portal se quedó vacío. Aquí, detrás de la semilla que carga las 171 y de la que
  normaliza sus accesos, se aplica sobre los datos que tiene que tocar.

  IDEMPOTENTE: se puede volver a pasar. La fuente se crea si no está, el enlace y la procedencia
  solo donde faltan, y la actualización de estados solo alcanza a las fichas del acervo que sigan
  pendientes. Una ficha que alguien haya retirado a mano después NO se vuelve a publicar.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

/* El rol vive en `UsuariosRoles`, no en una columna de `Usuarios`: un usuario puede tener varios. */
DECLARE @webmaster int = (
    SELECT TOP 1 u.IdUsuario
    FROM dbo.Usuarios u
    JOIN dbo.UsuariosRoles ur ON ur.IdUsuario = u.IdUsuario
    JOIN dbo.Roles r ON r.IdRol = ur.IdRol
    WHERE r.NombreRol = N'webmaster' AND u.Activo = 1
    ORDER BY u.IdUsuario);

DECLARE @nombreWebmaster nvarchar(200) = (
    SELECT TOP 1 u.NombreCompleto FROM dbo.Usuarios u WHERE u.IdUsuario = @webmaster);

DECLARE @institucional int = (
    SELECT TOP 1 e.IdEntidad FROM dbo.Entidades e WHERE e.EsInstitucional = 1 AND e.Activo = 1);

/* ─────────────────────────── La fuente del acervo ───────────────────────────
   TODA FICHA PUBLICA CITA DE DONDE SALE, y esa es una de las cuatro condiciones de la puerta. La
   fuente es el catálogo del propio Proyecto Editorial, que es de donde salieron las 171. */
IF NOT EXISTS (SELECT 1 FROM dbo.FuentesEditoriales WHERE Nombre = N'Catálogo del Proyecto Editorial del PNMC')
BEGIN
    INSERT INTO dbo.FuentesEditoriales (Nombre, Referencia, Url, FechaConsulta, VerificadaPor)
    VALUES (N'Catálogo del Proyecto Editorial del PNMC',
            N'Base de datos del Proyecto Editorial, 171 publicaciones con sus agentes, créditos e identificadores.',
            NULL, SYSUTCDATETIME(), ISNULL(@nombreWebmaster, N'Equipo del PNMC'));
END;

DECLARE @fuente bigint = (
    SELECT TOP 1 IdFuenteEditorial FROM dbo.FuentesEditoriales
    WHERE Nombre = N'Catálogo del Proyecto Editorial del PNMC');

/* ─────────────────────────── Cada ficha del acervo cita esa fuente ───────────────────────────
   EL ACERVO SE RECONOCE POR `DiapositivaOrigen`, que solo tienen las 171 que vinieron del catálogo
   original. Las tres fichas de verificación no la tienen y quedan fuera sin nombrarlas. */
INSERT INTO dbo.PublicacionesEditorialesFuentes (PublicacionEditorialId, FuenteEditorialId)
SELECT p.IdPublicacionEditorial, @fuente
FROM dbo.PublicacionesEditoriales p
WHERE p.DiapositivaOrigen IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.PublicacionesEditorialesFuentes f
                  WHERE f.PublicacionEditorialId = p.IdPublicacionEditorial AND f.FuenteEditorialId = @fuente);

/* ─────────────────────────── Validar, dar derechos y publicar ─────────────────────────── */
UPDATE p
SET EstadoCatalogacion = N'validada',
    EstadoPublicacion = N'publicado',
    DerechosEstado = N'verificado',
    DerechosPermitePublicarFicha = 1,
    /* El archivo NO. Son dos autorizaciones distintas. */
    DerechosPermitePublicarArchivo = 0,
    DerechosFuenteId = @fuente,
    DerechosFechaVerificacion = SYSUTCDATETIME(),
    DerechosVerificadoPor = ISNULL(@nombreWebmaster, N'Equipo del PNMC'),
    FechaActualizacion = SYSUTCDATETIME()
FROM dbo.PublicacionesEditoriales p
WHERE p.DiapositivaOrigen IS NOT NULL
  AND p.EstadoCatalogacion = N'pendiente_revision'
  AND p.EstadoPublicacion = N'borrador';

/* Los accesos de esas fichas —dónde se consulta y el enlace— se pueden enseñar; el archivo no. */
UPDATE a
SET DerechosEstado = N'verificado',
    DerechosPermitePublicarFicha = 1,
    DerechosPermitePublicarArchivo = 0,
    DerechosFuenteId = @fuente,
    DerechosFechaVerificacion = SYSUTCDATETIME(),
    DerechosVerificadoPor = ISNULL(@nombreWebmaster, N'Equipo del PNMC')
FROM dbo.AccesosEditoriales a
JOIN dbo.PublicacionesEditoriales p ON p.IdPublicacionEditorial = a.PublicacionEditorialId
WHERE p.DiapositivaOrigen IS NOT NULL
  AND a.Tipo <> N'archivo'
  AND a.DerechosEstado = N'pendiente';

/* ─────────────────────────── La procedencia de cada ficha ───────────────────────────
   NINGUN REGISTRO SE QUEDA SIN PROCEDENCIA. Las tres dimensiones separadas: de dónde vino
   (`importacion`), qué entidad lo incorporó (el PNMC) y qué cuenta lo ejecutó (el webmaster). */
INSERT INTO dbo.ProcedenciaDeRegistros (Dominio, RegistroId, ContextoOrigen, OrganizacionProcedenciaId, UsuarioCreadorId, FechaRegistro)
SELECT N'editorial', CAST(p.IdPublicacionEditorial AS nvarchar(120)), N'importacion', @institucional, @webmaster, SYSUTCDATETIME()
FROM dbo.PublicacionesEditoriales p
WHERE p.DiapositivaOrigen IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.ProcedenciaDeRegistros x
                  WHERE x.Dominio = N'editorial'
                    AND x.RegistroId = CAST(p.IdPublicacionEditorial AS nvarchar(120)));
GO
