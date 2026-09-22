/*
    PNMC · Ecosistema · Ningun registro sin quien responda por el

    QUE HACE
    --------
    Lleva `OrganizacionResponsableId` a los cuatro catalogos del ecosistema que no lo tenian
    —escuelas de musica, mercados musicales, redes y documentacion, luteria— y lo vuelve
    obligatorio. Con esto, los SEIS procesos responden a la misma pregunta: quien responde por
    este registro.

    Festivales lo cerro en B2 y Escenarios nacio con el en B8. Estos cuatro eran los que
    quedaban: sus unicas foraneas eran a `Divipola` y a `EstadosContenido`.

    LO QUE ESTE BLOQUE **NO** HACE, Y POR QUE
    -----------------------------------------
    El plan pedia ademas versionado y propuestas en los cuatro. **Se decide que no, todavia no.**

    El versionado existe para proteger LO PUBLICADO de las ediciones en curso de un tercero: la
    version es lo que el publico ve y la cabecera es el borrador, de modo que editar no cambia la
    ficha hasta que alguien revise. Eso tiene sentido porque Festivales tiene circuito externo: una
    organizacion propone y una persona funcionaria aprueba.

    Estos cuatro catalogos **no tienen circuito externo**. Se escriben desde la consola
    institucional, y solo por `webmaster` o `gestor_interno` (`admin-config.ts`). No hay tercero de
    quien proteger la ficha publicada, porque quien edita es exactamente quien aprueba. Construirles
    ocho tablas —cuatro de versiones y cuatro de propuestas— seria estructura sin nadie que la use,
    y multiplicaria por cinco el coste de cambiar de opinion sobre un modelo que todavia se esta
    estrenando en Festivales.

    QUE LO DISPARARIA: el dia en que uno de estos cuatro abra alta o edicion a una organizacion
    externa. Ese es el momento, y no antes.

    LA CONVERGENCIA EN DOS PASADAS
    -------------------------------
    Igual que en B2 y por lo mismo: `seed-local-db.sh` aplica schema/ ANTES que seed/. Sobre una
    base que ya tiene filas, en la primera pasada la columna nace vacia y el `ALTER ... NOT NULL`
    se salta a si mismo; las semillas rellenan; y en la segunda pasada aplica. Sobre una base nueva
    las tablas estan vacias y aplica a la primera. Las dos pasadas salen 0 en cualquier caso.

    ESTE FICHERO TIENE QUE ESTAR EN LA LISTA `SCHEMAS` DE scripts/seed-local-db.sh.
*/
SET QUOTED_IDENTIFIER ON;
GO

-- ---------------------------------------------------------------------------------------
-- Escuelas de musica
-- ---------------------------------------------------------------------------------------

IF COL_LENGTH(N'dbo.EscuelasMusica', N'OrganizacionResponsableId') IS NULL
BEGIN
    ALTER TABLE dbo.EscuelasMusica ADD OrganizacionResponsableId int NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE parent_object_id = OBJECT_ID(N'dbo.EscuelasMusica', N'U')
                 AND name = N'FK_EscuelasMusica_Organizacion')
BEGIN
    ALTER TABLE dbo.EscuelasMusica
        ADD CONSTRAINT FK_EscuelasMusica_Organizacion
            FOREIGN KEY (OrganizacionResponsableId) REFERENCES dbo.Entidades (IdEntidad);
END;
GO

-- La bandeja de una organizacion: sus registros por estado. Mismo indice y mismo motivo que
-- IX_Festivales_OrganizacionPrincipalId_EstadoRegistro.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_EscuelasMusica_OrganizacionResponsableId_EstadoRegistro'
                 AND object_id = OBJECT_ID(N'dbo.EscuelasMusica', N'U'))
BEGIN
    CREATE INDEX IX_EscuelasMusica_OrganizacionResponsableId_EstadoRegistro
        ON dbo.EscuelasMusica (OrganizacionResponsableId, EstadoRegistro);
END;
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'EscuelasMusica'
             AND COLUMN_NAME = N'OrganizacionResponsableId' AND IS_NULLABLE = N'YES')
   AND NOT EXISTS (SELECT 1 FROM dbo.EscuelasMusica WHERE OrganizacionResponsableId IS NULL)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_EscuelasMusica_OrganizacionResponsableId_EstadoRegistro'
                 AND object_id = OBJECT_ID(N'dbo.EscuelasMusica', N'U'))
    BEGIN
        DROP INDEX IX_EscuelasMusica_OrganizacionResponsableId_EstadoRegistro ON dbo.EscuelasMusica;
    END;

    ALTER TABLE dbo.EscuelasMusica ALTER COLUMN OrganizacionResponsableId int NOT NULL;

    EXEC(N'CREATE INDEX IX_EscuelasMusica_OrganizacionResponsableId_EstadoRegistro
        ON dbo.EscuelasMusica (OrganizacionResponsableId, EstadoRegistro);');
END;
GO

-- ---------------------------------------------------------------------------------------
-- Mercados musicales
-- ---------------------------------------------------------------------------------------

IF COL_LENGTH(N'dbo.MercadosMusicales', N'OrganizacionResponsableId') IS NULL
BEGIN
    ALTER TABLE dbo.MercadosMusicales ADD OrganizacionResponsableId int NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE parent_object_id = OBJECT_ID(N'dbo.MercadosMusicales', N'U')
                 AND name = N'FK_MercadosMusicales_Organizacion')
BEGIN
    ALTER TABLE dbo.MercadosMusicales
        ADD CONSTRAINT FK_MercadosMusicales_Organizacion
            FOREIGN KEY (OrganizacionResponsableId) REFERENCES dbo.Entidades (IdEntidad);
END;
GO

-- La bandeja de una organizacion: sus registros por estado. Mismo indice y mismo motivo que
-- IX_Festivales_OrganizacionPrincipalId_EstadoRegistro.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_MercadosMusicales_OrganizacionResponsableId_EstadoRegistro'
                 AND object_id = OBJECT_ID(N'dbo.MercadosMusicales', N'U'))
BEGIN
    CREATE INDEX IX_MercadosMusicales_OrganizacionResponsableId_EstadoRegistro
        ON dbo.MercadosMusicales (OrganizacionResponsableId, EstadoRegistro);
END;
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'MercadosMusicales'
             AND COLUMN_NAME = N'OrganizacionResponsableId' AND IS_NULLABLE = N'YES')
   AND NOT EXISTS (SELECT 1 FROM dbo.MercadosMusicales WHERE OrganizacionResponsableId IS NULL)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_MercadosMusicales_OrganizacionResponsableId_EstadoRegistro'
                 AND object_id = OBJECT_ID(N'dbo.MercadosMusicales', N'U'))
    BEGIN
        DROP INDEX IX_MercadosMusicales_OrganizacionResponsableId_EstadoRegistro ON dbo.MercadosMusicales;
    END;

    ALTER TABLE dbo.MercadosMusicales ALTER COLUMN OrganizacionResponsableId int NOT NULL;

    EXEC(N'CREATE INDEX IX_MercadosMusicales_OrganizacionResponsableId_EstadoRegistro
        ON dbo.MercadosMusicales (OrganizacionResponsableId, EstadoRegistro);');
END;
GO

-- ---------------------------------------------------------------------------------------
-- Redes y documentacion
-- ---------------------------------------------------------------------------------------

IF COL_LENGTH(N'dbo.RedesDocumentacion', N'OrganizacionResponsableId') IS NULL
BEGIN
    ALTER TABLE dbo.RedesDocumentacion ADD OrganizacionResponsableId int NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE parent_object_id = OBJECT_ID(N'dbo.RedesDocumentacion', N'U')
                 AND name = N'FK_RedesDocumentacion_Organizacion')
BEGIN
    ALTER TABLE dbo.RedesDocumentacion
        ADD CONSTRAINT FK_RedesDocumentacion_Organizacion
            FOREIGN KEY (OrganizacionResponsableId) REFERENCES dbo.Entidades (IdEntidad);
END;
GO

-- La bandeja de una organizacion: sus registros por estado. Mismo indice y mismo motivo que
-- IX_Festivales_OrganizacionPrincipalId_EstadoRegistro.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_RedesDocumentacion_OrganizacionResponsableId_EstadoRegistro'
                 AND object_id = OBJECT_ID(N'dbo.RedesDocumentacion', N'U'))
BEGIN
    CREATE INDEX IX_RedesDocumentacion_OrganizacionResponsableId_EstadoRegistro
        ON dbo.RedesDocumentacion (OrganizacionResponsableId, EstadoRegistro);
END;
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'RedesDocumentacion'
             AND COLUMN_NAME = N'OrganizacionResponsableId' AND IS_NULLABLE = N'YES')
   AND NOT EXISTS (SELECT 1 FROM dbo.RedesDocumentacion WHERE OrganizacionResponsableId IS NULL)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_RedesDocumentacion_OrganizacionResponsableId_EstadoRegistro'
                 AND object_id = OBJECT_ID(N'dbo.RedesDocumentacion', N'U'))
    BEGIN
        DROP INDEX IX_RedesDocumentacion_OrganizacionResponsableId_EstadoRegistro ON dbo.RedesDocumentacion;
    END;

    ALTER TABLE dbo.RedesDocumentacion ALTER COLUMN OrganizacionResponsableId int NOT NULL;

    EXEC(N'CREATE INDEX IX_RedesDocumentacion_OrganizacionResponsableId_EstadoRegistro
        ON dbo.RedesDocumentacion (OrganizacionResponsableId, EstadoRegistro);');
END;
GO

-- ---------------------------------------------------------------------------------------
-- Luteria
-- ---------------------------------------------------------------------------------------

IF COL_LENGTH(N'dbo.Lutieres', N'OrganizacionResponsableId') IS NULL
BEGIN
    ALTER TABLE dbo.Lutieres ADD OrganizacionResponsableId int NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE parent_object_id = OBJECT_ID(N'dbo.Lutieres', N'U')
                 AND name = N'FK_Lutieres_Organizacion')
BEGIN
    ALTER TABLE dbo.Lutieres
        ADD CONSTRAINT FK_Lutieres_Organizacion
            FOREIGN KEY (OrganizacionResponsableId) REFERENCES dbo.Entidades (IdEntidad);
END;
GO

-- La bandeja de una organizacion: sus registros por estado. Mismo indice y mismo motivo que
-- IX_Festivales_OrganizacionPrincipalId_EstadoRegistro.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_Lutieres_OrganizacionResponsableId_EstadoRegistro'
                 AND object_id = OBJECT_ID(N'dbo.Lutieres', N'U'))
BEGIN
    CREATE INDEX IX_Lutieres_OrganizacionResponsableId_EstadoRegistro
        ON dbo.Lutieres (OrganizacionResponsableId, EstadoRegistro);
END;
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'Lutieres'
             AND COLUMN_NAME = N'OrganizacionResponsableId' AND IS_NULLABLE = N'YES')
   AND NOT EXISTS (SELECT 1 FROM dbo.Lutieres WHERE OrganizacionResponsableId IS NULL)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_Lutieres_OrganizacionResponsableId_EstadoRegistro'
                 AND object_id = OBJECT_ID(N'dbo.Lutieres', N'U'))
    BEGIN
        DROP INDEX IX_Lutieres_OrganizacionResponsableId_EstadoRegistro ON dbo.Lutieres;
    END;

    ALTER TABLE dbo.Lutieres ALTER COLUMN OrganizacionResponsableId int NOT NULL;

    EXEC(N'CREATE INDEX IX_Lutieres_OrganizacionResponsableId_EstadoRegistro
        ON dbo.Lutieres (OrganizacionResponsableId, EstadoRegistro);');
END;
GO
