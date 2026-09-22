SET QUOTED_IDENTIFIER ON;
GO

/*
    Centro de importaciones gobernadas: primera rama, Festivales.

    No conserva el archivo original. La cabecera guarda su SHA-256 y las filas contienen solo el
    dato normalizado que una persona previsualizó. Aplicar crea borradores; ninguna tabla de
    publicación forma parte de estas relaciones.
*/

IF OBJECT_ID(N'dbo.LotesImportacion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LotesImportacion
    (
        IdLoteImportacion bigint IDENTITY(1,1) NOT NULL,
        Dominio nvarchar(40) NOT NULL,
        NombreArchivo nvarchar(255) NOT NULL,
        Formato nvarchar(10) NOT NULL,
        HuellaArchivo char(64) NOT NULL,
        HuellaPlan char(64) NOT NULL,
        VersionContrato int NOT NULL,
        Estado nvarchar(30) NOT NULL,
        IdUsuario int NOT NULL,
        ClaveIdempotencia nvarchar(100) NULL,
        FechaPrevisualizacion datetime2(3) NOT NULL,
        FechaAplicacion datetime2(3) NULL,
        TotalFilas int NOT NULL,
        FilasImportables int NOT NULL,
        FilasRechazadas int NOT NULL,
        FilasAplicadas int NOT NULL CONSTRAINT DF_LotesImportacion_FilasAplicadas DEFAULT (0),
        FilasExcluidas int NOT NULL CONSTRAINT DF_LotesImportacion_FilasExcluidas DEFAULT (0),
        CONSTRAINT PK_LotesImportacion PRIMARY KEY (IdLoteImportacion),
        CONSTRAINT FK_LotesImportacion_Usuario FOREIGN KEY (IdUsuario) REFERENCES dbo.Usuarios (IdUsuario),
        CONSTRAINT CK_LotesImportacion_Dominio CHECK (Dominio IN (N'festivales')),
        CONSTRAINT CK_LotesImportacion_Formato CHECK (Formato IN (N'csv', N'xlsx')),
        CONSTRAINT CK_LotesImportacion_Estado CHECK (Estado IN (N'previsualizado', N'aplicando', N'aplicado', N'conflicto')),
        CONSTRAINT CK_LotesImportacion_Version CHECK (VersionContrato >= 1),
        CONSTRAINT CK_LotesImportacion_Huellas CHECK (
            LEN(HuellaArchivo) = 64 AND HuellaArchivo NOT LIKE '%[^0-9a-f]%'
            AND LEN(HuellaPlan) = 64 AND HuellaPlan NOT LIKE '%[^0-9a-f]%'),
        CONSTRAINT CK_LotesImportacion_Conteos CHECK (
            TotalFilas >= 0 AND FilasImportables >= 0 AND FilasRechazadas >= 0
            AND FilasAplicadas >= 0 AND FilasExcluidas >= 0
            AND FilasImportables + FilasRechazadas = TotalFilas)
    );
END;
GO

IF OBJECT_ID(N'dbo.FilasImportacion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FilasImportacion
    (
        IdFilaImportacion bigint IDENTITY(1,1) NOT NULL,
        IdLoteImportacion bigint NOT NULL,
        NumeroFila int NOT NULL,
        ContenidoNormalizadoJson nvarchar(max) NOT NULL,
        Resultado nvarchar(40) NOT NULL,
        PuedeImportarse bit NOT NULL,
        IdFestivalCoincidente int NULL,
        IdFestivalCreado int NULL,
        CONSTRAINT PK_FilasImportacion PRIMARY KEY (IdFilaImportacion),
        CONSTRAINT FK_FilasImportacion_Lote FOREIGN KEY (IdLoteImportacion)
            REFERENCES dbo.LotesImportacion (IdLoteImportacion) ON DELETE CASCADE,
        CONSTRAINT FK_FilasImportacion_FestivalCoincidente FOREIGN KEY (IdFestivalCoincidente)
            REFERENCES dbo.Festivales (IdFestival),
        CONSTRAINT FK_FilasImportacion_FestivalCreado FOREIGN KEY (IdFestivalCreado)
            REFERENCES dbo.Festivales (IdFestival),
        CONSTRAINT CK_FilasImportacion_Numero CHECK (NumeroFila >= 2),
        CONSTRAINT CK_FilasImportacion_Resultado CHECK (Resultado IN (N'crear_borrador', N'rechazar', N'coincidencia_existente')),
        CONSTRAINT CK_FilasImportacion_ContenidoJson CHECK (ISJSON(ContenidoNormalizadoJson) = 1)
    );
END;
GO

IF OBJECT_ID(N'dbo.HallazgosImportacion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HallazgosImportacion
    (
        IdHallazgoImportacion bigint IDENTITY(1,1) NOT NULL,
        IdFilaImportacion bigint NOT NULL,
        Severidad nvarchar(20) NOT NULL,
        Codigo nvarchar(60) NOT NULL,
        Campo nvarchar(80) NULL,
        Mensaje nvarchar(600) NOT NULL,
        CONSTRAINT PK_HallazgosImportacion PRIMARY KEY (IdHallazgoImportacion),
        CONSTRAINT FK_HallazgosImportacion_Fila FOREIGN KEY (IdFilaImportacion)
            REFERENCES dbo.FilasImportacion (IdFilaImportacion) ON DELETE CASCADE,
        CONSTRAINT CK_HallazgosImportacion_Severidad CHECK (Severidad IN (N'informacion', N'advertencia', N'error'))
    );
END;
GO

IF OBJECT_ID(N'dbo.DecisionesImportacion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DecisionesImportacion
    (
        IdDecisionImportacion bigint IDENTITY(1,1) NOT NULL,
        IdLoteImportacion bigint NOT NULL,
        IdFilaImportacion bigint NOT NULL,
        IdUsuario int NOT NULL,
        Decision nvarchar(30) NOT NULL,
        Fecha datetime2(3) NOT NULL,
        CONSTRAINT PK_DecisionesImportacion PRIMARY KEY (IdDecisionImportacion),
        CONSTRAINT FK_DecisionesImportacion_Lote FOREIGN KEY (IdLoteImportacion)
            REFERENCES dbo.LotesImportacion (IdLoteImportacion) ON DELETE CASCADE,
        CONSTRAINT FK_DecisionesImportacion_Fila FOREIGN KEY (IdFilaImportacion)
            REFERENCES dbo.FilasImportacion (IdFilaImportacion),
        CONSTRAINT FK_DecisionesImportacion_Usuario FOREIGN KEY (IdUsuario)
            REFERENCES dbo.Usuarios (IdUsuario),
        CONSTRAINT CK_DecisionesImportacion_Decision CHECK (Decision IN (N'importar', N'excluir', N'rechazar_validacion'))
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_LotesImportacion_ClaveIdempotencia' AND object_id = OBJECT_ID(N'dbo.LotesImportacion'))
    CREATE UNIQUE INDEX UQ_LotesImportacion_ClaveIdempotencia
        ON dbo.LotesImportacion (ClaveIdempotencia)
        WHERE ClaveIdempotencia IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LotesImportacion_Dominio_Fecha' AND object_id = OBJECT_ID(N'dbo.LotesImportacion'))
    CREATE INDEX IX_LotesImportacion_Dominio_Fecha
        ON dbo.LotesImportacion (Dominio, FechaPrevisualizacion DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_FilasImportacion_Lote_Numero' AND object_id = OBJECT_ID(N'dbo.FilasImportacion'))
    CREATE UNIQUE INDEX UQ_FilasImportacion_Lote_Numero
        ON dbo.FilasImportacion (IdLoteImportacion, NumeroFila);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_DecisionesImportacion_Fila' AND object_id = OBJECT_ID(N'dbo.DecisionesImportacion'))
    CREATE UNIQUE INDEX UQ_DecisionesImportacion_Fila
        ON dbo.DecisionesImportacion (IdFilaImportacion);
GO
