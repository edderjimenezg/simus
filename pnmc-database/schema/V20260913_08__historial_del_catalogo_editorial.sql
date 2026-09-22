/*
  SIMUS · El historial de revisión admite los estados del Catálogo Editorial

  QUE FALTABA. `dbo.RegistrosRevisionHistorial` es el hilo único de revisión del proyecto —módulo,
  registro, estado anterior, estado nuevo, acción, comentario y quién— y el circuito de Festivales
  escribe en él cada decisión. El Catálogo Editorial NO escribía nada: validar una ficha, observarla,
  publicarla o retirarla no dejaba rastro. A la pregunta «quién validó esto y cuándo, y por qué se
  retiró» no había respuesta, aunque la tabla para contestarla ya existía.

  POR QUE HAY QUE TOCAR EL CHECK. `CK_RegistrosRevisionHistorial_EstadoNuevo` admite los siete
  códigos del ciclo de Festivales, y el Catálogo tiene los suyos: la catalogación va por
  `pendiente_revision`, `en_revision`, `validada` y `observada`, y la publicación por `borrador`,
  `en_revision`, `publicado` y `retirado`. De esos, cuatro no estaban.

  SE AMPLIA EN VEZ DE TRADUCIR, y esa es la decisión. Lo cómodo era mapear —«validada» se guarda
  como «aprobado», «observada» como «ajustes_solicitados», «retirado» como «archivado»— y no tocar
  nada. Pero entonces el historial diría una palabra que nadie pulsó: quien lea «aprobado» en la
  traza de una ficha buscará un botón «Aprobar» que no existe, y la traza de un módulo dejaría de
  poder compararse con su propia pantalla. Un historial que traduce es un historial en el que hay
  que confiar dos veces.

  LOS DOS EJES CABEN EN UNA COLUMNA PORQUE NUNCA CAMBIAN A LA VEZ. Cada decisión mueve uno solo, y
  `Accion` dice cuál —`CatalogacionCambiada` o `PublicacionCambiada`—, así que `EstadoNuevo` es
  siempre el valor nuevo del eje que se movió. El otro viaja en `MetadataJson`, para que una fila
  del historial se pueda leer sola.

  Y UNA ANOTACION ES UNA FILA MAS DE ESTE MISMO HILO, con `Accion = 'Anotacion'` y el estado sin
  moverse. No hay una segunda tabla de comentarios: un comentario sobre una ficha y la decisión que
  comenta se leen en el mismo sitio y en el mismo orden, que es como se lee una conversación.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_RegistrosRevisionHistorial_EstadoNuevo')
    ALTER TABLE dbo.RegistrosRevisionHistorial DROP CONSTRAINT CK_RegistrosRevisionHistorial_EstadoNuevo;
GO

ALTER TABLE dbo.RegistrosRevisionHistorial ADD CONSTRAINT CK_RegistrosRevisionHistorial_EstadoNuevo
    CHECK (EstadoNuevo IN (
        /* El ciclo de Festivales y del resto de módulos. */
        N'borrador', N'en_revision', N'ajustes_solicitados', N'aprobado', N'publicado', N'rechazado', N'archivado',
        /* Catálogo Editorial · catalogación. */
        N'pendiente_revision', N'validada', N'observada',
        /* Catálogo Editorial · publicación. */
        N'retirado'
    ));
GO
