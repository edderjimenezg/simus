-- =============================================================================================
-- El estado de un Festival, dicho una sola vez y guardado con guarda
-- =============================================================================================
--
-- QUE CIERRA. El circuito Festival tiene tres tablas con estado —`Festivales`,
-- `VersionesFestival` y `PropuestasCambioFestival`— y solo las dos primeras tenian algo que
-- impidiera escribir cualquier cosa. Comprobado en la base.
--
-- 1. LA PROPUESTA NO TENIA NI CHECK NI FORANEA. `PropuestasCambioFestival.Estado` admitia
--    cualquier cadena. El codigo si la controla —`ResolverDecision` traduce la accion del
--    funcionario a uno de cuatro estados y rechaza lo demas— pero la base no sabia nada: un
--    camino nuevo escrito por descuido, o una correccion a mano, podia dejar ahi un valor que
--    ningun lector reconoce. Y hay un lector que decide algo importante:
--    `LecturaFestivalesPublicados.VersionesRepudiadasAsync` retira del portal las versiones cuya
--    propuesta no acabo en `Publicada`. Un estado mal escrito ahi no rompe nada visiblemente:
--    retira una version que deberia verse, o deja una que no.
--
--    LOS CINCO SON EN FEMENINO Y NO SON LOS DEL FESTIVAL. El sujeto es la propuesta —«Publicada»,
--    «Rechazada»— y ese vocabulario es legitimo y deliberado; lo que no habia era quien lo
--    sujetara.
--
-- 2. LAS VERSIONES NACIAN SIN ESTADO. De los cinco caminos que crean una fila en
--    `VersionesFestival`, CUATRO no le ponian `EstadoRegistro`: la normalizacion de historicos,
--    las dos fotografias que se toman para basar una propuesta, y —la que mas pesa— la version
--    NUEVA que nace al publicar una propuesta aprobada. La unica version que existia en la base
--    tenia el estado nulo por ese camino.
--
--    QUE CONSECUENCIA TENIA. No es que saliera mal en el portal: la lectura publica no mira el
--    estado de la version, mira la propuesta que la produjo. Lo que decide `EstadoRegistro` es si
--    la ORGANIZACION puede editar esa version —`EstadosEditables` admite `borrador` y
--    `ajustes_solicitados`—, asi que una version sin estado no era editable y tampoco decia por
--    que. Un registro en ningun estado declarado no se puede explicar a quien pregunta.
--
--    El codigo ya las crea con `publicado` desde esta versión; aqui se repone lo que quedo.
--
-- NO ES DESTRUCTIVO. Solo añade una CHECK y rellena estados que estaban a nulo.

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ---------------------------------------------------------------------------------------------
-- 1. El estado de una propuesta, cerrado a los cinco que el circuito usa
-- ---------------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PropuestasCambioFestival_Estado')
BEGIN
    /*
      SE NORMALIZA ANTES DE CERRAR. La columna no tenia guarda, asi que puede haber grafias
      distintas de lo mismo. Se corrigen las que se sabe traducir y solo despues se pone la CHECK:
      ponerla primero fallaria y dejaria la tabla sin guarda por una fila con una mayuscula.
    */
    UPDATE dbo.PropuestasCambioFestival SET Estado = N'Borrador'           WHERE LOWER(Estado) IN (N'borrador');
    UPDATE dbo.PropuestasCambioFestival SET Estado = N'EnRevision'         WHERE LOWER(Estado) IN (N'enrevision', N'en_revision');
    UPDATE dbo.PropuestasCambioFestival SET Estado = N'AjustesSolicitados' WHERE LOWER(Estado) IN (N'ajustessolicitados', N'ajustes_solicitados');
    UPDATE dbo.PropuestasCambioFestival SET Estado = N'Publicada'          WHERE LOWER(Estado) IN (N'publicada', N'publicado');
    UPDATE dbo.PropuestasCambioFestival SET Estado = N'Rechazada'          WHERE LOWER(Estado) IN (N'rechazada', N'rechazado');

    /*
      LA GUARDA NO SE PONE A CIEGAS. Si queda alguna fila con un estado que no se supo traducir, el
      guion se detiene con el valor a la vista en vez de fallar con un mensaje de restriccion que
      no dice cual era.
    */
    IF EXISTS (SELECT 1 FROM dbo.PropuestasCambioFestival
               WHERE Estado NOT IN (N'Borrador', N'EnRevision', N'AjustesSolicitados', N'Publicada', N'Rechazada'))
    BEGIN
        DECLARE @raros nvarchar(400) = (
            SELECT STRING_AGG(CONVERT(nvarchar(80), Estado), N', ')
            FROM (SELECT DISTINCT Estado FROM dbo.PropuestasCambioFestival
                  WHERE Estado NOT IN (N'Borrador', N'EnRevision', N'AjustesSolicitados', N'Publicada', N'Rechazada')) AS x);
        RAISERROR(N'Hay propuestas con estados desconocidos y no se pone la guarda: %s', 16, 1, @raros);
        RETURN;
    END

    ALTER TABLE dbo.PropuestasCambioFestival
        ADD CONSTRAINT CK_PropuestasCambioFestival_Estado
        CHECK (Estado IN (N'Borrador', N'EnRevision', N'AjustesSolicitados', N'Publicada', N'Rechazada'));
END
GO

-- ---------------------------------------------------------------------------------------------
-- 2. Ninguna version se queda sin estado
-- ---------------------------------------------------------------------------------------------
--
-- PUBLICADA Y NO BORRADOR, para las que son vigentes de un Festival publicado: es exactamente lo
-- que el publico esta leyendo de ese Festival. Llamarlas borrador diria que hay algo sin terminar
-- donde no lo hay, y ademas las abriria a edicion directa, saltandose el circuito de propuestas
-- que existe justo para que lo publicado no se toque sin revision.
UPDATE v
SET v.EstadoRegistro = N'publicado'
FROM dbo.VersionesFestival AS v
INNER JOIN dbo.Festivales AS f ON f.IdFestival = v.FestivalOrigenId
WHERE v.EstadoRegistro IS NULL
  AND v.EsVigente = 1
  AND f.EstadoRegistro = N'publicado';
GO

-- LAS QUE NO SON VIGENTES Y SIGUEN SIN ESTADO SON HISTORIA: versiones que en su dia se publicaron
-- y fueron sustituidas por una posterior. `archivado` es lo que significa eso en este catalogo, y
-- es ademas un estado no editable, que es lo que corresponde a algo que ya paso.
UPDATE dbo.VersionesFestival
SET EstadoRegistro = N'archivado'
WHERE EstadoRegistro IS NULL AND EsVigente = 0;
GO

-- Y LO QUE QUEDE —vigente de un Festival que no esta publicado— es un borrador de verdad: alguien
-- lo esta preparando y todavia no ha salido.
UPDATE dbo.VersionesFestival
SET EstadoRegistro = N'borrador'
WHERE EstadoRegistro IS NULL;
GO
