/*
  Comprueba que una base quedó en el estado objetivo de la línea SIMUS del Ministerio.
  Termina con error en la primera comprobación que falle; si todas pasan, lo dice.
*/
SET NOCOUNT ON;

DECLARE @Fallas TABLE (Comprobacion nvarchar(400));

-- Estructura: las tablas del modelo.
INSERT INTO @Fallas
SELECT N'Falta la tabla dbo.' + t.Nombre
FROM (VALUES
    (N'EstadosContenido'), (N'TiposDocumento'), (N'Divipola'), (N'CatalogosReferenciaFuente'), (N'Usuarios'),
    (N'Archivos'), (N'Entidades'), (N'EntidadesResponsable'), (N'UsuariosEntidades'), (N'ProcedenciasDeRegistro'),
    (N'CorrespondenciasHeredadas'), (N'TerritoriosSonoros'), (N'FichasConceptualesTerritoriosSonoros'),
    (N'PracticasMusicales'), (N'FichasConceptualesPracticasMusicales'), (N'RegionesOcad'), (N'DepartamentosRegionOcad'),
    (N'ZonasUrbanoRural'), (N'TitulacionesColectivas'), (N'NaturalezasEntidad'), (N'TipologiasFestival'),
    (N'ExpresionesArtisticas'), (N'FuentesFinanciacion'), (N'ModalidadesParticipacion'), (N'TiposIngreso'),
    (N'TiposOrganizador'), (N'Festivales'), (N'VersionesFestival'), (N'EdicionesFestival'),
    (N'FestivalesReferenciasHistoricas'), (N'TerritoriosSonorosDeRegistro'), (N'PracticasMusicalesDeRegistro'),
    (N'ExpresionesArtisticasDeRegistro'), (N'ModalidadesParticipacionDeRegistro'), (N'TiposIngresoDeRegistro'),
    (N'LocalizacionesDeRegistro'), (N'EntidadesAliadasDeRegistro'), (N'ArchivosDeRegistro'), (N'RevisionesDeRegistro'),
    (N'RevisionesDeRegistroObservaciones'), (N'EnviosDeRevision'), (N'PropuestasDeCambio'), (N'PropuestasDeCambioCampos'),
    (N'RegistrosRevisionHistorial'), (N'TrasladosHeredadosFestival')
) AS t (Nombre)
WHERE OBJECT_ID(N'dbo.' + t.Nombre, N'U') IS NULL;

INSERT INTO @Fallas
SELECT N'Restricción deshabilitada o sin validar: ' + name FROM sys.foreign_keys
WHERE (is_disabled = 1 OR is_not_trusted = 1) AND OBJECT_SCHEMA_NAME(parent_object_id) = N'dbo'
  AND OBJECT_NAME(parent_object_id) NOT LIKE N'ART[_]MUS%'
UNION ALL
SELECT N'Restricción deshabilitada o sin validar: ' + name FROM sys.check_constraints
WHERE (is_disabled = 1 OR is_not_trusted = 1) AND OBJECT_NAME(parent_object_id) NOT LIKE N'ART[_]MUS%';

IF OBJECT_ID(N'dbo.TrasladarFestivalesHeredados', N'P') IS NULL
    INSERT INTO @Fallas VALUES (N'Falta el procedimiento dbo.TrasladarFestivalesHeredados');

-- Datos maestros.
IF (SELECT COUNT(*) FROM @Fallas) = 0
BEGIN
    INSERT INTO @Fallas
    SELECT CONCAT(N'dbo.', c.Tabla, N' tiene ', c.Filas, N' filas y se esperaban ', c.Esperadas)
    FROM (VALUES
        (N'EstadosContenido', (SELECT COUNT(*) FROM dbo.EstadosContenido), 12),
        (N'TiposDocumento', (SELECT COUNT(*) FROM dbo.TiposDocumento), 10),
        (N'Divipola', (SELECT COUNT(*) FROM dbo.Divipola), 1122),
        (N'Divipola (departamentos)', (SELECT COUNT(DISTINCT CodigoDepartamento) FROM dbo.Divipola), 33),
        (N'TerritoriosSonoros', (SELECT COUNT(*) FROM dbo.TerritoriosSonoros), 14),
        (N'FichasConceptualesTerritoriosSonoros', (SELECT COUNT(*) FROM dbo.FichasConceptualesTerritoriosSonoros), 14),
        (N'FichasConceptualesTerritoriosSonoros (con definición)', (SELECT COUNT(*) FROM dbo.FichasConceptualesTerritoriosSonoros WHERE DefinicionBreve IS NOT NULL), 10),
        (N'PracticasMusicales', (SELECT COUNT(*) FROM dbo.PracticasMusicales), 16),
        (N'FichasConceptualesPracticasMusicales', (SELECT COUNT(*) FROM dbo.FichasConceptualesPracticasMusicales), 16),
        (N'RegionesOcad', (SELECT COUNT(*) FROM dbo.RegionesOcad), 6),
        (N'DepartamentosRegionOcad', (SELECT COUNT(*) FROM dbo.DepartamentosRegionOcad), 33),
        (N'ZonasUrbanoRural', (SELECT COUNT(*) FROM dbo.ZonasUrbanoRural), 2),
        (N'TitulacionesColectivas', (SELECT COUNT(*) FROM dbo.TitulacionesColectivas), 3),
        (N'NaturalezasEntidad', (SELECT COUNT(*) FROM dbo.NaturalezasEntidad), 3),
        (N'TipologiasFestival', (SELECT COUNT(*) FROM dbo.TipologiasFestival), 10),
        (N'ExpresionesArtisticas', (SELECT COUNT(*) FROM dbo.ExpresionesArtisticas), 11),
        (N'FuentesFinanciacion', (SELECT COUNT(*) FROM dbo.FuentesFinanciacion), 9),
        (N'ModalidadesParticipacion', (SELECT COUNT(*) FROM dbo.ModalidadesParticipacion), 5),
        (N'TiposIngreso', (SELECT COUNT(*) FROM dbo.TiposIngreso), 4),
        (N'TiposOrganizador', (SELECT COUNT(*) FROM dbo.TiposOrganizador), 8),
        (N'Entidades (institucional)', (SELECT COUNT(*) FROM dbo.Entidades WHERE EsInstitucional = 1 AND EstadoRegistro = N'activa'), 1)
    ) AS c (Tabla, Filas, Esperadas)
    WHERE c.Filas <> c.Esperadas;

    -- Todo departamento de DIVIPOLA tiene región OCAD, y ninguna región apunta a un departamento inexistente.
    INSERT INTO @Fallas
    SELECT N'Departamento sin región OCAD: ' + d.CodigoDepartamento
    FROM (SELECT DISTINCT CodigoDepartamento FROM dbo.Divipola) AS d
    WHERE NOT EXISTS (SELECT 1 FROM dbo.DepartamentosRegionOcad AS r WHERE r.CodigoDepartamento = d.CodigoDepartamento)
    UNION ALL
    SELECT N'Región OCAD asignada a un departamento que no está en DIVIPOLA: ' + r.CodigoDepartamento
    FROM dbo.DepartamentosRegionOcad AS r
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Divipola AS d WHERE d.CodigoDepartamento = r.CodigoDepartamento);

    -- Cada catálogo heredado presente tiene correspondencia completa (salvo «N/A» de territorios).
    DECLARE @Heredados TABLE (Tabla sysname, Esperadas int);
    INSERT INTO @Heredados VALUES
        (N'ART_MUS_FESTIVALES_ESTADO', 6), (N'ART_MUS_TERRITORIOS_SONOROS', 14), (N'ART_MUS_FESTIVALES_REGION_OCAD', 6),
        (N'ART_MUS_FESTIVALES_ZONA', 2), (N'ART_MUS_FESTIVALES_ZONA_TITULACION_COLECTIVA', 3),
        (N'ART_MUS_FESTIVALES_NATURALEZA_ENTIDAD', 3), (N'ART_MUS_FESTIVALES_TIPOLOGIA', 10),
        (N'ART_MUS_FESTIVALES_EXPRESIONES_ARTISTICAS', 11), (N'ART_MUS_FESTIVALES_FUENTE_FINANCIACION', 9),
        (N'ART_MUS_FESTIVALES_MODALIDADES_PARTICIPACION', 5), (N'ART_MUS_TIPOINGRESO', 4),
        (N'ART_MUS_FESTIVALES_TIPO_ORGANIZADOR', 7);
    INSERT INTO @Fallas
    SELECT CONCAT(N'Correspondencia incompleta de ', h.Tabla, N': ', COUNT(c.IdCorrespondencia), N' de ', h.Esperadas)
    FROM @Heredados AS h
    LEFT JOIN dbo.CorrespondenciasHeredadas AS c ON c.TablaHeredada = h.Tabla
    WHERE OBJECT_ID(N'dbo.' + h.Tabla, N'U') IS NOT NULL
    GROUP BY h.Tabla, h.Esperadas
    HAVING COUNT(c.IdCorrespondencia) <> h.Esperadas;
END

-- Integridad de las relaciones genéricas: todo RegistroId apunta a un registro existente.
INSERT INTO @Fallas
SELECT CONCAT(N'Fila huérfana en ', r.Tabla, N': ', r.ModuloId, N' ', r.RegistroId)
FROM (
    SELECT N'TerritoriosSonorosDeRegistro' AS Tabla, ModuloId, RegistroId FROM dbo.TerritoriosSonorosDeRegistro
    UNION ALL SELECT N'PracticasMusicalesDeRegistro', ModuloId, RegistroId FROM dbo.PracticasMusicalesDeRegistro
    UNION ALL SELECT N'ExpresionesArtisticasDeRegistro', ModuloId, RegistroId FROM dbo.ExpresionesArtisticasDeRegistro
    UNION ALL SELECT N'ModalidadesParticipacionDeRegistro', ModuloId, RegistroId FROM dbo.ModalidadesParticipacionDeRegistro
    UNION ALL SELECT N'TiposIngresoDeRegistro', ModuloId, RegistroId FROM dbo.TiposIngresoDeRegistro
    UNION ALL SELECT N'LocalizacionesDeRegistro', ModuloId, RegistroId FROM dbo.LocalizacionesDeRegistro
    UNION ALL SELECT N'EntidadesAliadasDeRegistro', ModuloId, RegistroId FROM dbo.EntidadesAliadasDeRegistro
    UNION ALL SELECT N'ArchivosDeRegistro', ModuloId, RegistroId FROM dbo.ArchivosDeRegistro
) AS r
WHERE (r.ModuloId = N'festivales' AND NOT EXISTS (SELECT 1 FROM dbo.Festivales WHERE CAST(IdFestival AS nvarchar(120)) = r.RegistroId))
   OR (r.ModuloId = N'versiones-festival' AND NOT EXISTS (SELECT 1 FROM dbo.VersionesFestival WHERE CAST(IdVersionFestival AS nvarchar(120)) = r.RegistroId))
   OR (r.ModuloId = N'ediciones-festival' AND NOT EXISTS (SELECT 1 FROM dbo.EdicionesFestival WHERE CAST(IdEdicionFestival AS nvarchar(120)) = r.RegistroId))
   OR (r.ModuloId = N'ediciones-mercado' AND OBJECT_ID(N'dbo.EdicionesMercado', N'U') IS NULL);

IF EXISTS (SELECT 1 FROM @Fallas)
BEGIN
    SELECT Comprobacion AS Falla FROM @Fallas;
    RAISERROR(N'El estado objetivo NO se cumple.', 16, 1);
END
ELSE
    PRINT N'Estado objetivo verificado: estructura, datos maestros, correspondencias e integridad.';
