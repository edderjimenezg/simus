/*
  Traslado gobernado de festivales ART_MUS_* al modelo nuevo.

  Esta migración no traslada nada: los registros actuales de ART_MUS_FESTIVALES son de prueba y no
  se incorporan. Deja preparados una lista de control y un procedimiento. Para trasladar un
  festival real se inscribe su identificador heredado en TrasladosHeredadosFestival y se ejecuta
  dbo.TrasladarFestivalesHeredados. Cada festival se traslada en su propia transacción, una sola
  vez, y el resultado queda en la lista.

  Qué hace con cada festival:
    - Festival → Festivales, con la entidad institucional como responsable y procedencia
      «historico». El nivel de cobertura se deduce de las localizaciones de sus versiones.
    - Cada versión → EdicionesFestival, con sus territorios, expresiones, modalidades, tipos de
      ingreso, localizaciones válidas en DIVIPOLA, materiales y entidades aliadas.
    - Lo que el modelo nuevo ya no guarda en la edición (organizador en texto, tipo de
      organizador, observación de rechazo, usuario creador) queda en el historial de revisión,
      en MetadataJson, para no perderlo.
    - Los identificadores heredados y los nuevos quedan en CorrespondenciasHeredadas.
  Los archivos físicos no se mueven: se conserva su URL.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE TABLE dbo.TrasladosHeredadosFestival (
    IdFestivalHeredado int NOT NULL,
    FechaSolicitud datetime2(0) NOT NULL CONSTRAINT DF_TrasladosHeredadosFestival_FechaSolicitud DEFAULT (SYSUTCDATETIME()),
    FechaTraslado datetime2(0) NULL,
    IdFestival int NULL,
    Resultado nvarchar(2000) NULL,
    CONSTRAINT PK_TrasladosHeredadosFestival PRIMARY KEY (IdFestivalHeredado),
    CONSTRAINT FK_TrasladosHeredadosFestival_Festival FOREIGN KEY (IdFestival) REFERENCES dbo.Festivales (IdFestival)
);
GO

CREATE FUNCTION dbo.TextoHeredado (@Texto nvarchar(max))
RETURNS nvarchar(max)
WITH SCHEMABINDING
AS
BEGIN
    -- Las exportaciones de SIMUS guardan algunos acentos como entidades HTML.
    DECLARE @Limpio nvarchar(max) = LTRIM(RTRIM(@Texto));
    SET @Limpio = REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(@Limpio,
        N'&aacute;', N'á'), N'&eacute;', N'é'), N'&iacute;', N'í'), N'&oacute;', N'ó'), N'&uacute;', N'ú'), N'&ntilde;', N'ñ');
    SET @Limpio = REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(@Limpio,
        N'&Aacute;', N'Á'), N'&Eacute;', N'É'), N'&Iacute;', N'Í'), N'&Oacute;', N'Ó'), N'&Uacute;', N'Ú'), N'&Ntilde;', N'Ñ');
    SET @Limpio = REPLACE(REPLACE(REPLACE(REPLACE(@Limpio, N'&uuml;', N'ü'), N'&quot;', N'"'), N'&#39;', N''''), N'&amp;', N'&');
    RETURN NULLIF(@Limpio, N'');
END
GO

CREATE PROCEDURE dbo.TrasladarFestivalesHeredados
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF OBJECT_ID(N'dbo.ART_MUS_FESTIVALES', N'U') IS NULL
    BEGIN
        RAISERROR(N'No existen las tablas ART_MUS_*: no hay nada que trasladar.', 16, 1);
        RETURN;
    END

    DECLARE @Institucional int = (SELECT IdEntidad FROM dbo.Entidades WHERE EsInstitucional = 1);
    IF @Institucional IS NULL
    BEGIN
        RAISERROR(N'Falta la entidad institucional.', 16, 1);
        RETURN;
    END

    DECLARE @Hoy date = CAST(SYSUTCDATETIME() AS date);
    DECLARE @IdHeredado int;

    DECLARE pendientes CURSOR LOCAL FAST_FORWARD FOR
        SELECT IdFestivalHeredado FROM dbo.TrasladosHeredadosFestival
        WHERE FechaTraslado IS NULL ORDER BY IdFestivalHeredado;
    OPEN pendientes;
    FETCH NEXT FROM pendientes INTO @IdHeredado;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        BEGIN TRY
            IF NOT EXISTS (SELECT 1 FROM dbo.ART_MUS_FESTIVALES WHERE id = @IdHeredado)
                THROW 50001, N'El festival heredado no existe.', 1;
            IF EXISTS (SELECT 1 FROM dbo.CorrespondenciasHeredadas
                       WHERE TablaHeredada = N'ART_MUS_FESTIVALES' AND IdHeredado = CAST(@IdHeredado AS nvarchar(64)))
                THROW 50002, N'El festival heredado ya fue trasladado.', 1;

            BEGIN TRANSACTION;

            DECLARE @Versiones TABLE (IdVersion int PRIMARY KEY);
            DELETE FROM @Versiones;
            INSERT INTO @Versiones SELECT ID FROM dbo.ART_MUS_FESTIVALES_VERSION WHERE ID_FESTIVAL = @IdHeredado;

            DECLARE @Lugares TABLE (IdVersion int, CodigoDepartamento char(2), CodigoMunicipio char(5), IdZona int, IdTitulacion int);
            DELETE FROM @Lugares;
            INSERT INTO @Lugares
            SELECT l.ID_VERSION, d.CodigoDepartamento, d.CodigoMunicipio, l.ID_ZONA, l.ID_TITULACION_COLECTIVA
            FROM dbo.ART_MUS_LOCALIZACIONXVERSION AS l
            JOIN @Versiones AS v ON v.IdVersion = l.ID_VERSION
            JOIN dbo.Divipola AS d ON d.CodigoMunicipio = LTRIM(RTRIM(l.ZON_ID));

            DECLARE @Municipios int = (SELECT COUNT(DISTINCT CodigoMunicipio) FROM @Lugares);
            DECLARE @Departamentos int = (SELECT COUNT(DISTINCT CodigoDepartamento) FROM @Lugares);
            DECLARE @Nivel nvarchar(40) = CASE WHEN @Municipios = 1 THEN N'municipal'
                                               WHEN @Departamentos = 1 THEN N'departamental'
                                               ELSE N'nacional' END;

            DECLARE @UltimaVersion int = (SELECT MAX(IdVersion) FROM @Versiones);
            DECLARE @IdFestival int;

            INSERT INTO dbo.Festivales
                (NombreFestival, NumeroVersiones, FechaUltimaVersion, Descripcion,
                 CorreoFestival, InstagramFestival, FacebookFestival, SitioWebFestival, OtroEnlaceFestival,
                 TelefonoFestival, ObservacionesContacto, NivelCobertura, CodigoDepartamento, CodigoMunicipio,
                 EstadoRegistro, FechaCreacion, OrganizacionPrincipalId)
            SELECT LEFT(COALESCE(dbo.TextoHeredado(f.NOMBRE_FESTIVAL), N'Festival heredado ' + CAST(f.id AS nvarchar(20))), 220),
                   CASE WHEN f.VERSIONES_REALIZADAS >= 0 THEN f.VERSIONES_REALIZADAS END,
                   CAST(f.FECHA_ULTIMA_VERSION AS date),
                   dbo.TextoHeredado(f.DESCRIPCION_FESTIVAL),
                   LEFT(COALESCE(dbo.TextoHeredado(f.CORREO_CONTACTO), dbo.TextoHeredado(v.CORREO_CONTACTO)), 180),
                   COALESCE(dbo.TextoHeredado(f.INSTAGRAM), dbo.TextoHeredado(v.INSTAGRAM)),
                   COALESCE(dbo.TextoHeredado(f.FACEBOOK), dbo.TextoHeredado(v.FACEBOOK)),
                   COALESCE(dbo.TextoHeredado(f.PAGINA_WEB), dbo.TextoHeredado(v.PAGINA_WEB)),
                   COALESCE(dbo.TextoHeredado(f.OTRO_ENLACE), dbo.TextoHeredado(v.OTRO_ENLACE)),
                   LEFT(COALESCE(dbo.TextoHeredado(f.CELULAR), dbo.TextoHeredado(v.TELEFONO_CELULAR)), 80),
                   COALESCE(dbo.TextoHeredado(f.OBSERVACIONES_CONTACTO), dbo.TextoHeredado(v.OBSERVACIONES_CONTACTO)),
                   @Nivel,
                   CASE WHEN @Nivel IN (N'municipal', N'departamental') THEN (SELECT MIN(CodigoDepartamento) FROM @Lugares) END,
                   CASE WHEN @Nivel = N'municipal' THEN (SELECT MIN(CodigoMunicipio) FROM @Lugares) END,
                   COALESCE(estado.IdDestino, N'borrador'),
                   COALESCE(f.FECHA_ENVIO, SYSUTCDATETIME()),
                   @Institucional
            FROM dbo.ART_MUS_FESTIVALES AS f
            LEFT JOIN dbo.ART_MUS_FESTIVALES_VERSION AS v ON v.ID = @UltimaVersion
            LEFT JOIN dbo.CorrespondenciasHeredadas AS estado
                ON estado.TablaHeredada = N'ART_MUS_FESTIVALES_ESTADO' AND estado.IdHeredado = CAST(f.ID_ESTADO AS nvarchar(64))
            WHERE f.id = @IdHeredado;

            SET @IdFestival = SCOPE_IDENTITY();
            DECLARE @EstadoFestival nvarchar(80) = (SELECT EstadoRegistro FROM dbo.Festivales WHERE IdFestival = @IdFestival);

            INSERT INTO dbo.ProcedenciasDeRegistro (ModuloId, RegistroId, ContextoOrigen, OrganizacionProcedenciaId)
            VALUES (N'festivales', CAST(@IdFestival AS nvarchar(120)), N'historico', @Institucional);

            INSERT INTO dbo.CorrespondenciasHeredadas (TablaHeredada, IdHeredado, TablaDestino, IdDestino)
            VALUES (N'ART_MUS_FESTIVALES', CAST(@IdHeredado AS nvarchar(64)), N'Festivales', CAST(@IdFestival AS nvarchar(120)));

            INSERT INTO dbo.RegistrosRevisionHistorial (ModuloId, RegistroId, EstadoAnterior, EstadoNuevo, Accion, Comentario, MetadataJson, OrganizacionId)
            SELECT N'festivales', CAST(@IdFestival AS nvarchar(120)), NULL, @EstadoFestival, N'traslado_historico',
                   N'Registro trasladado desde ART_MUS_FESTIVALES.',
                   (SELECT f.id AS idHeredado, f.creado_por AS creadoPorHeredado, f.ID_ESTADO AS estadoHeredado
                    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
                   @Institucional
            FROM dbo.ART_MUS_FESTIVALES AS f WHERE f.id = @IdHeredado;

            DECLARE @Ediciones TABLE (IdVersion int PRIMARY KEY, IdEdicion int NOT NULL);
            DELETE FROM @Ediciones;

            MERGE dbo.EdicionesFestival AS destino
            USING (
                SELECT v.ID,
                       fechas.Inicio,
                       CASE WHEN fechas.Fin >= fechas.Inicio OR fechas.Inicio IS NULL THEN fechas.Fin END AS Fin,
                       CASE WHEN v.VERSION_FESTIVAL > 0 THEN v.VERSION_FESTIVAL END AS Numero,
                       LEFT(dbo.TextoHeredado(v.NOMBRE_VERSION), 240) AS Nombre,
                       dbo.TextoHeredado(v.DESCRIPCION_VERSION) AS Descripcion,
                       LEFT(dbo.TextoHeredado(v.DIRECTOR), 240) AS Director,
                       CAST(tipologia.IdDestino AS int) AS Tipologia,
                       dbo.TextoHeredado(v.OTRA_TIPOLOGIA) AS OtraTipologia,
                       CAST(primaria.IdDestino AS int) AS Primaria,
                       dbo.TextoHeredado(v.OTRA_FUENTE_FINANCIACION_PRIMARIA) AS OtraPrimaria,
                       CASE WHEN secundaria.IdDestino <> primaria.IdDestino OR primaria.IdDestino IS NULL
                            THEN CAST(secundaria.IdDestino AS int) END AS Secundaria,
                       dbo.TextoHeredado(v.OTRA_FUENTE_FINANCIACION_SECUNDARIA) AS OtraSecundaria,
                       v.USO_ESTAMPILLA_PROCULTURA AS Estampilla,
                       dbo.TextoHeredado(v.PRACTICAS_MUSICALES_CONGREGA) AS Practicas,
                       dbo.TextoHeredado(v.OTRA_MODALIDAD_PARTICIPACION) AS OtraModalidad,
                       dbo.TextoHeredado(v.OTRA_EXPRESION_ARTISTICA) AS OtraExpresion,
                       COALESCE(estado.IdDestino, @EstadoFestival) AS EstadoRegistro
                FROM dbo.ART_MUS_FESTIVALES_VERSION AS v
                JOIN @Versiones AS lista ON lista.IdVersion = v.ID
                CROSS APPLY (SELECT TRY_CONVERT(date, LTRIM(RTRIM(v.FECHA_INICIO)), 23) AS Inicio,
                                    TRY_CONVERT(date, LTRIM(RTRIM(v.FECHA_FIN)), 23) AS Fin) AS fechas
                LEFT JOIN dbo.CorrespondenciasHeredadas AS tipologia
                    ON tipologia.TablaHeredada = N'ART_MUS_FESTIVALES_TIPOLOGIA' AND tipologia.IdHeredado = CAST(v.ID_TIPOLOGIA AS nvarchar(64))
                LEFT JOIN dbo.CorrespondenciasHeredadas AS primaria
                    ON primaria.TablaHeredada = N'ART_MUS_FESTIVALES_FUENTE_FINANCIACION' AND primaria.IdHeredado = CAST(v.ID_FUENTE_FINANCIACION AS nvarchar(64))
                LEFT JOIN dbo.CorrespondenciasHeredadas AS secundaria
                    ON secundaria.TablaHeredada = N'ART_MUS_FESTIVALES_FUENTE_FINANCIACION' AND secundaria.IdHeredado = CAST(v.ID_FUENTE_FINANCIACION_SECUNDARIA AS nvarchar(64))
                LEFT JOIN dbo.CorrespondenciasHeredadas AS estado
                    ON estado.TablaHeredada = N'ART_MUS_FESTIVALES_ESTADO' AND estado.IdHeredado = CAST(v.ID_ESTADO AS nvarchar(64))
            ) AS origen
            ON 1 = 0
            WHEN NOT MATCHED THEN
                INSERT (FestivalId, Anio, NumeroEdicion, Nombre, Descripcion, FechaInicio, FechaFin, Estado, Director,
                        EstadoRegistro, EstadoVisibilidad, TipologiaFestivalId, OtraTipologia,
                        FuenteFinanciacionPrimariaId, OtraFuenteFinanciacionPrimaria,
                        FuenteFinanciacionSecundariaId, OtraFuenteFinanciacionSecundaria,
                        UsaEstampillaProcultura, PracticasMusicalesQueCongrega, OtraModalidadParticipacion, OtraExpresionArtistica)
                VALUES (@IdFestival, YEAR(origen.Inicio), origen.Numero,
                        CASE WHEN origen.Nombre IS NULL AND origen.Numero IS NULL AND origen.Inicio IS NULL AND origen.Fin IS NULL
                             THEN N'Edición heredada ' + CAST(origen.ID AS nvarchar(20)) ELSE origen.Nombre END,
                        origen.Descripcion, origen.Inicio, origen.Fin,
                        CASE WHEN origen.Fin < @Hoy THEN N'realizada'
                             WHEN origen.Inicio > @Hoy THEN N'programada'
                             ELSE N'en_preparacion' END,
                        origen.Director, origen.EstadoRegistro,
                        CASE origen.EstadoRegistro WHEN N'publicado' THEN N'publicada' WHEN N'archivado' THEN N'archivada' ELSE N'borrador' END,
                        origen.Tipologia, LEFT(origen.OtraTipologia, 120),
                        origen.Primaria, LEFT(origen.OtraPrimaria, 120),
                        origen.Secundaria, LEFT(origen.OtraSecundaria, 120),
                        origen.Estampilla, origen.Practicas, LEFT(origen.OtraModalidad, 120), LEFT(origen.OtraExpresion, 120))
            OUTPUT origen.ID, inserted.IdEdicionFestival INTO @Ediciones (IdVersion, IdEdicion);

            INSERT INTO dbo.ProcedenciasDeRegistro (ModuloId, RegistroId, ContextoOrigen, OrganizacionProcedenciaId)
            SELECT N'ediciones-festival', CAST(IdEdicion AS nvarchar(120)), N'historico', @Institucional FROM @Ediciones;

            INSERT INTO dbo.CorrespondenciasHeredadas (TablaHeredada, IdHeredado, TablaDestino, IdDestino)
            SELECT N'ART_MUS_FESTIVALES_VERSION', CAST(IdVersion AS nvarchar(64)), N'EdicionesFestival', CAST(IdEdicion AS nvarchar(120))
            FROM @Ediciones;

            INSERT INTO dbo.RegistrosRevisionHistorial (ModuloId, RegistroId, EstadoAnterior, EstadoNuevo, Accion, Comentario, MetadataJson, OrganizacionId)
            SELECT N'ediciones-festival', CAST(e.IdEdicion AS nvarchar(120)), NULL, ed.EstadoRegistro, N'traslado_historico',
                   LEFT(dbo.TextoHeredado(v.OBSERVACIONES_RECHAZO), 1200),
                   (SELECT v.ID AS idHeredado,
                           dbo.TextoHeredado(v.NOMBRE_ORGANIZACION) AS organizacion,
                           v.PERTENECE_ORG_COLETIVA AS perteneceAColectivo,
                           tipoOrg.IdDestino AS tipoOrganizadorId,
                           dbo.TextoHeredado(v.OTRO_TIPO_ORGANIZADOR) AS otroTipoOrganizador
                    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
                   @Institucional
            FROM @Ediciones AS e
            JOIN dbo.ART_MUS_FESTIVALES_VERSION AS v ON v.ID = e.IdVersion
            JOIN dbo.EdicionesFestival AS ed ON ed.IdEdicionFestival = e.IdEdicion
            LEFT JOIN dbo.CorrespondenciasHeredadas AS tipoOrg
                ON tipoOrg.TablaHeredada = N'ART_MUS_FESTIVALES_TIPO_ORGANIZADOR' AND tipoOrg.IdHeredado = CAST(v.ID_TIPO_ORGANIZADOR AS nvarchar(64));

            INSERT INTO dbo.TerritoriosSonorosDeRegistro (ModuloId, RegistroId, IdTerritorioSonoro)
            SELECT DISTINCT N'ediciones-festival', CAST(e.IdEdicion AS nvarchar(120)), CAST(c.IdDestino AS int)
            FROM dbo.ART_MUS_TERRITORIOS_SONOROSXVERSION AS x
            JOIN @Ediciones AS e ON e.IdVersion = x.ID_VERSION
            JOIN dbo.CorrespondenciasHeredadas AS c
                ON c.TablaHeredada = N'ART_MUS_TERRITORIOS_SONOROS' AND c.IdHeredado = CAST(x.ID_TERRITORIOS_SONOROS AS nvarchar(64));

            INSERT INTO dbo.ExpresionesArtisticasDeRegistro (ModuloId, RegistroId, IdExpresionArtistica)
            SELECT DISTINCT N'ediciones-festival', CAST(e.IdEdicion AS nvarchar(120)), CAST(c.IdDestino AS int)
            FROM dbo.ART_MUS_FESTIVALES_EXPRESIONXVERSION AS x
            JOIN @Ediciones AS e ON e.IdVersion = x.ID_VERSION
            JOIN dbo.CorrespondenciasHeredadas AS c
                ON c.TablaHeredada = N'ART_MUS_FESTIVALES_EXPRESIONES_ARTISTICAS' AND c.IdHeredado = CAST(x.ID_EXPRESION_ARTISTICA AS nvarchar(64));

            INSERT INTO dbo.ModalidadesParticipacionDeRegistro (ModuloId, RegistroId, IdModalidadParticipacion)
            SELECT DISTINCT N'ediciones-festival', CAST(e.IdEdicion AS nvarchar(120)), CAST(c.IdDestino AS int)
            FROM dbo.ART_MUS_FESTIVALES_MODALIDADES_PARTICIPACIONXVERSION AS x
            JOIN @Ediciones AS e ON e.IdVersion = x.ID_VERSION
            JOIN dbo.CorrespondenciasHeredadas AS c
                ON c.TablaHeredada = N'ART_MUS_FESTIVALES_MODALIDADES_PARTICIPACION' AND c.IdHeredado = CAST(x.ID_MODALIDAD AS nvarchar(64));

            INSERT INTO dbo.TiposIngresoDeRegistro (ModuloId, RegistroId, IdTipoIngreso)
            SELECT DISTINCT N'ediciones-festival', CAST(e.IdEdicion AS nvarchar(120)), CAST(c.IdDestino AS int)
            FROM dbo.ART_MUS_TIPOINGRESOXVERSION AS x
            JOIN @Ediciones AS e ON e.IdVersion = x.IDVERSION
            JOIN dbo.CorrespondenciasHeredadas AS c
                ON c.TablaHeredada = N'ART_MUS_TIPOINGRESO' AND c.IdHeredado = CAST(x.ID_TIPOINGRESO AS nvarchar(64));

            -- Solo los códigos que existen en DIVIPOLA; los inválidos quedan fuera y se informan.
            INSERT INTO dbo.LocalizacionesDeRegistro (ModuloId, RegistroId, CodigoDepartamento, CodigoMunicipio, ZonaUrbanoRuralId, TitulacionColectivaId)
            SELECT N'ediciones-festival', CAST(e.IdEdicion AS nvarchar(120)), l.CodigoDepartamento, l.CodigoMunicipio,
                   CAST(zona.IdDestino AS int), MIN(CAST(titulacion.IdDestino AS int))
            FROM @Lugares AS l
            JOIN @Ediciones AS e ON e.IdVersion = l.IdVersion
            LEFT JOIN dbo.CorrespondenciasHeredadas AS zona
                ON zona.TablaHeredada = N'ART_MUS_FESTIVALES_ZONA' AND zona.IdHeredado = CAST(l.IdZona AS nvarchar(64))
            LEFT JOIN dbo.CorrespondenciasHeredadas AS titulacion
                ON titulacion.TablaHeredada = N'ART_MUS_FESTIVALES_ZONA_TITULACION_COLECTIVA' AND titulacion.IdHeredado = CAST(l.IdTitulacion AS nvarchar(64))
            GROUP BY e.IdEdicion, l.CodigoDepartamento, l.CodigoMunicipio, zona.IdDestino;

            DECLARE @LugaresDescartados int = (
                SELECT COUNT(*) FROM dbo.ART_MUS_LOCALIZACIONXVERSION AS l
                JOIN @Versiones AS v ON v.IdVersion = l.ID_VERSION
                WHERE NOT EXISTS (SELECT 1 FROM dbo.Divipola AS d WHERE d.CodigoMunicipio = LTRIM(RTRIM(l.ZON_ID))));

            -- El afiche es la imagen principal de la edición; programa y logo son material.
            INSERT INTO dbo.ArchivosDeRegistro (ModuloId, RegistroId, Url, RolArchivo, DescripcionArchivo, OrdenVisualizacion)
            SELECT N'ediciones-festival', CAST(a.IdEdicion AS nvarchar(120)), a.Url, a.Rol, a.Descripcion,
                   ROW_NUMBER() OVER (PARTITION BY a.IdEdicion, a.Rol ORDER BY a.ID)
            FROM (
                SELECT m.ID, e.IdEdicion, LEFT(dbo.TextoHeredado(m.URL_ARCHIVO), 1000) AS Url,
                       dbo.TextoHeredado(m.DESCRIPCION_ARCHIVO) AS Descripcion,
                       CASE WHEN LOWER(LTRIM(RTRIM(m.DESCRIPCION_ARCHIVO))) = N'afiche'
                             AND m.ID = MIN(CASE WHEN LOWER(LTRIM(RTRIM(m.DESCRIPCION_ARCHIVO))) = N'afiche' THEN m.ID END)
                                        OVER (PARTITION BY e.IdEdicion)
                            THEN N'portada' ELSE N'material' END AS Rol
                FROM dbo.ART_MUS_MATERIALMULTIMEDIA AS m
                JOIN @Ediciones AS e ON e.IdVersion = m.ID_VERSION
                WHERE dbo.TextoHeredado(m.URL_ARCHIVO) IS NOT NULL
            ) AS a;

            INSERT INTO dbo.EntidadesAliadasDeRegistro (ModuloId, RegistroId, NombreEntidadAliada, CorreoEntidadAliada, NaturalezaEntidadId)
            SELECT N'ediciones-festival', CAST(e.IdEdicion AS nvarchar(120)),
                   LEFT(dbo.TextoHeredado(a.NOMBRE_ENTIDAD_ALIADA), 300), LEFT(dbo.TextoHeredado(a.CORREO_ENTIDAD), 180),
                   CAST(c.IdDestino AS int)
            FROM dbo.ART_MUS_FESTIVALES_ENTIDADES_ALIADAS AS a
            JOIN @Ediciones AS e ON e.IdVersion = a.ID_FESTIVAL
            LEFT JOIN dbo.CorrespondenciasHeredadas AS c
                ON c.TablaHeredada = N'ART_MUS_FESTIVALES_NATURALEZA_ENTIDAD' AND c.IdHeredado = CAST(a.ID_NATURALEZA AS nvarchar(64))
            WHERE dbo.TextoHeredado(a.NOMBRE_ENTIDAD_ALIADA) IS NOT NULL;

            UPDATE dbo.TrasladosHeredadosFestival
            SET FechaTraslado = SYSUTCDATETIME(), IdFestival = @IdFestival,
                Resultado = CONCAT(N'Trasladado con ', (SELECT COUNT(*) FROM @Ediciones), N' edición(es)',
                                   CASE WHEN @LugaresDescartados > 0
                                        THEN CONCAT(N'; ', @LugaresDescartados, N' localización(es) con código DIVIPOLA inválido descartada(s)') END,
                                   N'.')
            WHERE IdFestivalHeredado = @IdHeredado;

            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
            UPDATE dbo.TrasladosHeredadosFestival
            SET Resultado = LEFT(CONCAT(N'Error ', ERROR_NUMBER(), N': ', ERROR_MESSAGE()), 2000)
            WHERE IdFestivalHeredado = @IdHeredado;
        END CATCH

        FETCH NEXT FROM pendientes INTO @IdHeredado;
    END

    CLOSE pendientes;
    DEALLOCATE pendientes;
END
GO
