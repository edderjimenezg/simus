/*
    SIMUS · Festivales · Instantáneas de los envíos a revisión

    Cada fila conserva el contenido exacto que recibió la institución. No representa una Edición
    ni una versión del perfil público: permite comparar el segundo, tercer y posteriores envíos
    contra el inmediatamente anterior sin leer valores vivos que ya pudieron cambiar.
*/
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.EnviosRevisionFestival', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EnviosRevisionFestival
    (
        IdEnvioRevisionFestival bigint        IDENTITY(1,1) NOT NULL,
        IdFestival              int           NOT NULL,
        NumeroEnvio             int           NOT NULL,
        IdUsuarioRemitente      int           NOT NULL,
        IdOrganizacion          int           NOT NULL,
        IdRevisionFestivalOrigen bigint        NULL,
        EstadoAnterior          nvarchar(40)  NOT NULL,
        DatosJson               nvarchar(max) NOT NULL,
        FechaEnvio              datetime2(0)  NOT NULL,

        CONSTRAINT PK_EnviosRevisionFestival PRIMARY KEY (IdEnvioRevisionFestival),
        CONSTRAINT UQ_EnviosRevisionFestival_Numero UNIQUE (IdFestival, NumeroEnvio),
        CONSTRAINT CK_EnviosRevisionFestival_Numero CHECK (NumeroEnvio > 0),
        CONSTRAINT CK_EnviosRevisionFestival_Datos CHECK (ISJSON(DatosJson) = 1),
        CONSTRAINT FK_EnviosRevisionFestival_Festival FOREIGN KEY (IdFestival) REFERENCES dbo.Festivales (IdFestival),
        CONSTRAINT FK_EnviosRevisionFestival_Usuario FOREIGN KEY (IdUsuarioRemitente) REFERENCES dbo.Usuarios (IdUsuario),
        CONSTRAINT FK_EnviosRevisionFestival_Organizacion FOREIGN KEY (IdOrganizacion) REFERENCES dbo.Entidades (IdEntidad),
        CONSTRAINT FK_EnviosRevisionFestival_RevisionOrigen FOREIGN KEY (IdRevisionFestivalOrigen) REFERENCES dbo.RevisionesFestival (IdRevisionFestival)
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_EnviosRevisionFestival_Fecha'
      AND object_id = OBJECT_ID(N'dbo.EnviosRevisionFestival', N'U'))
BEGIN
    CREATE INDEX IX_EnviosRevisionFestival_Fecha
        ON dbo.EnviosRevisionFestival (IdFestival, FechaEnvio DESC);
END;
GO
