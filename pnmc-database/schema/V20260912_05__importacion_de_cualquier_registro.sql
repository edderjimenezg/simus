SET QUOTED_IDENTIFIER ON;
GO

/*
    La importación deja de ser «la de Festivales».

    QUE ESTABA MAL. Las tablas de importación nacieron con el dominio
    como columna —`LotesImportacion.Dominio`— pero con las filas atadas a Festivales por dos
    foráneas: `IdFestivalCoincidente` e `IdFestivalCreado`. Es decir, el almacén decía ser genérico
    y no lo era: un segundo dominio no tenía dónde anotar con qué registro coincidía ni cuál creó.

    POR QUE SE RENOMBRA EN VEZ DE AÑADIR UNA COLUMNA AL LADO. Añadir `IdRegistroCoincidente` y
    dejar la vieja habría dejado el MISMO concepto en dos sitios, con la interpretación dependiendo
    del dominio. La dirección lo decidió: «lo que técnicamente sea
    mejor, estable y que no quede como remiendos».

    NO SE PIERDE NINGUN DATO. `sp_rename` conserva la columna y su contenido; lo que se retira son
    las dos foráneas, que no pueden apuntar a dos tablas a la vez. La integridad de esa referencia
    pasa a ser responsabilidad del dominio, que es quien sabe a qué tabla apunta.

    SE ENSANCHAN A bigint porque los identificadores no son todos `int`: Festivales y Entidades usan
    `int`, pero Noticias, Agenda y Catálogo Editorial usan `bigint`. Con `int` el almacén habría
    vuelto a ser genérico solo de nombre.
*/

-- ---------- 1. Las foráneas a Festivales se retiran ------------------------------------------
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FilasImportacion_FestivalCoincidente')
    ALTER TABLE dbo.FilasImportacion DROP CONSTRAINT FK_FilasImportacion_FestivalCoincidente;
GO

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FilasImportacion_FestivalCreado')
    ALTER TABLE dbo.FilasImportacion DROP CONSTRAINT FK_FilasImportacion_FestivalCreado;
GO

-- ---------- 2. Las columnas pasan a nombrar el registro, no el Festival ----------------------
IF COL_LENGTH(N'dbo.FilasImportacion', N'IdFestivalCoincidente') IS NOT NULL
    AND COL_LENGTH(N'dbo.FilasImportacion', N'IdRegistroCoincidente') IS NULL
    EXEC sp_rename N'dbo.FilasImportacion.IdFestivalCoincidente', N'IdRegistroCoincidente', 'COLUMN';
GO

IF COL_LENGTH(N'dbo.FilasImportacion', N'IdFestivalCreado') IS NOT NULL
    AND COL_LENGTH(N'dbo.FilasImportacion', N'IdRegistroCreado') IS NULL
    EXEC sp_rename N'dbo.FilasImportacion.IdFestivalCreado', N'IdRegistroCreado', 'COLUMN';
GO

ALTER TABLE dbo.FilasImportacion ALTER COLUMN IdRegistroCoincidente bigint NULL;
GO

ALTER TABLE dbo.FilasImportacion ALTER COLUMN IdRegistroCreado bigint NULL;
GO

-- ---------- 3. El resultado de una fila deja de nombrar el estado de un dominio --------------
--
-- `crear_borrador` daba por hecho que todo destino nace en borrador. Una organización importada
-- nace «pendiente de confirmación» y una publicación editorial «pendiente de revisión»: el estado
-- lo declara cada dominio, y la fila solo dice qué se decidió hacer con ella.
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_FilasImportacion_Resultado')
    ALTER TABLE dbo.FilasImportacion DROP CONSTRAINT CK_FilasImportacion_Resultado;
GO

UPDATE dbo.FilasImportacion SET Resultado = N'crear' WHERE Resultado = N'crear_borrador';
GO

ALTER TABLE dbo.FilasImportacion ADD CONSTRAINT CK_FilasImportacion_Resultado
    CHECK (Resultado IN (N'crear', N'rechazar', N'coincidencia_existente'));
GO

-- ---------- 4. Los dominios que hoy tienen circuito -------------------------------------------
--
-- SE LISTAN Y NO SE ABRE LA COLUMNA: es el mismo criterio que `CK_Entidades_Tipo`. Un dominio sin
-- implementación no puede escribir lotes por error, y añadir uno obliga a pasar por aquí, que es
-- justo el sitio donde conviene acordarse de que hay que declararlo también en el API.
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_LotesImportacion_Dominio')
    ALTER TABLE dbo.LotesImportacion DROP CONSTRAINT CK_LotesImportacion_Dominio;
GO

ALTER TABLE dbo.LotesImportacion ADD CONSTRAINT CK_LotesImportacion_Dominio
    CHECK (Dominio IN (N'festivales', N'organizaciones', N'noticias'));
GO

-- ---------- 5. El estado `expirado` faltaba en la base ----------------------------------------
--
-- El depurador de retención lo escribe desde el 11 de septiembre y la CHECK no lo admitía: contra
-- SQLite las pruebas pasaban —no hay CHECK— y contra SQL Server la depuración habría reventado.
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_LotesImportacion_Estado')
    ALTER TABLE dbo.LotesImportacion DROP CONSTRAINT CK_LotesImportacion_Estado;
GO

ALTER TABLE dbo.LotesImportacion ADD CONSTRAINT CK_LotesImportacion_Estado
    CHECK (Estado IN (N'previsualizado', N'aplicando', N'aplicado', N'expirado', N'conflicto'));
GO
