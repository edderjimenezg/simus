/*
    SIMUS · Referencias históricas de Festivales

    Sustituye la tabla genérica EntidadesRegistrosFuente en el perfil limpio.
    La única procedencia histórica vigente respalda coincidencias entre una
    organización y un Festival; no representa relaciones de procesos futuros.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;

IF DB_NAME() <> N'PNMC_SIMUS_LIMPIO'
BEGIN
    PRINT N'Perfil vigente conservado: esta sustitución solo corresponde a PNMC_SIMUS_LIMPIO.';
    RETURN;
END;
GO

IF OBJECT_ID(N'dbo.FestivalesReferenciasHistoricas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FestivalesReferenciasHistoricas
    (
        IdReferenciaHistoricaFestival int IDENTITY(1,1) NOT NULL,
        IdOrganizacion int NOT NULL,
        IdFestival int NOT NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_FestivalesReferenciasHistoricas_Creacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_FestivalesReferenciasHistoricas PRIMARY KEY (IdReferenciaHistoricaFestival),
        CONSTRAINT FK_FestivalesReferenciasHistoricas_Organizacion FOREIGN KEY (IdOrganizacion) REFERENCES dbo.Entidades (IdEntidad),
        CONSTRAINT FK_FestivalesReferenciasHistoricas_Festival FOREIGN KEY (IdFestival) REFERENCES dbo.Festivales (IdFestival),
        CONSTRAINT UQ_FestivalesReferenciasHistoricas_Organizacion_Festival UNIQUE (IdOrganizacion, IdFestival)
    );
END;
GO

IF OBJECT_ID(N'dbo.EntidadesRegistrosFuente', N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.FestivalesReferenciasHistoricas (IdOrganizacion, IdFestival, FechaCreacion)
    SELECT fuente.IdEntidad, fuente.IdRegistroFuente, fuente.FechaCreacion
    FROM dbo.EntidadesRegistrosFuente fuente
    INNER JOIN dbo.Festivales festival ON festival.IdFestival = fuente.IdRegistroFuente
    WHERE fuente.TablaFuente = N'Festivales'
      AND NOT EXISTS (
          SELECT 1
          FROM dbo.FestivalesReferenciasHistoricas destino
          WHERE destino.IdOrganizacion = fuente.IdEntidad
            AND destino.IdFestival = fuente.IdRegistroFuente
      );

    DROP TABLE dbo.EntidadesRegistrosFuente;
END;
GO

IF OBJECT_ID(N'dbo.EntidadesRelaciones', N'U') IS NOT NULL
    DROP TABLE dbo.EntidadesRelaciones;

IF OBJECT_ID(N'dbo.EntidadesHistorialRevision', N'U') IS NOT NULL
    DROP TABLE dbo.EntidadesHistorialRevision;
GO
