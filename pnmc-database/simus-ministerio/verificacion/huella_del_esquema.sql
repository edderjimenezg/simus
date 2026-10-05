/*
  Huella canónica del esquema: una línea por columna, índice, CHECK, DEFAULT y clave foránea de
  cada tabla del modelo, en orden estable. Dos bases con la misma huella tienen la misma
  estructura. Se excluyen las tablas heredadas y las de historial de migraciones.

  Uso: sqlcmd ... -i huella_del_esquema.sql -h -1 -W > huella.txt
*/
SET NOCOUNT ON;

WITH Tablas AS (
    SELECT t.object_id, t.name COLLATE DATABASE_DEFAULT AS Tabla
    FROM sys.tables AS t
    WHERE t.schema_id = SCHEMA_ID(N'dbo') AND t.is_ms_shipped = 0
      AND t.name NOT LIKE N'ART[_]MUS%' AND t.name NOT LIKE N'BAS[_]%'
      AND t.name NOT IN (N'flyway_schema_history', N'SchemaVersions')
)
SELECT Linea FROM (
    SELECT t.Tabla, 1 AS Orden, c.name COLLATE DATABASE_DEFAULT AS Clave,
           CONCAT(t.Tabla, N' | columna | ', c.name, N' ', ty.name,
                  CASE WHEN ty.name IN (N'nvarchar', N'nchar') THEN CONCAT(N'(', IIF(c.max_length = -1, N'max', CAST(c.max_length / 2 AS nvarchar(10))), N')')
                       WHEN ty.name IN (N'varchar', N'char', N'varbinary') THEN CONCAT(N'(', IIF(c.max_length = -1, N'max', CAST(c.max_length AS nvarchar(10))), N')')
                       WHEN ty.name IN (N'decimal', N'numeric') THEN CONCAT(N'(', c.precision, N',', c.scale, N')')
                       WHEN ty.name IN (N'datetime2', N'time') THEN CONCAT(N'(', c.scale, N')') ELSE N'' END,
                  IIF(c.is_identity = 1, N' IDENTITY', N''), IIF(c.is_nullable = 1, N' NULL', N' NOT NULL'),

                  IIF(dc.definition IS NULL, N'', CONCAT(N' DEFAULT ', dc.definition))) COLLATE DATABASE_DEFAULT AS Linea
    FROM Tablas AS t
    JOIN sys.columns AS c ON c.object_id = t.object_id
    JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
    LEFT JOIN sys.default_constraints AS dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    UNION ALL
    SELECT t.Tabla, 2, i.name COLLATE DATABASE_DEFAULT,
           CONCAT(t.Tabla, N' | indice | ', i.name,
                  IIF(i.is_primary_key = 1, N' PK', IIF(i.is_unique_constraint = 1, N' UQ', IIF(i.is_unique = 1, N' UNIQUE', N''))),
                  N' ', i.type_desc, N' (', k.Columnas, N')',
                  IIF(inc.Columnas IS NULL, N'', CONCAT(N' INCLUDE (', inc.Columnas, N')')),
                  IIF(i.has_filter = 1, CONCAT(N' WHERE ', i.filter_definition), N'')) COLLATE DATABASE_DEFAULT
    FROM Tablas AS t
    JOIN sys.indexes AS i ON i.object_id = t.object_id AND i.type > 0
    CROSS APPLY (SELECT STRING_AGG(CONCAT(COL_NAME(ic.object_id, ic.column_id), IIF(ic.is_descending_key = 1, N' DESC', N'')), N',')
                        WITHIN GROUP (ORDER BY ic.key_ordinal) AS Columnas
                 FROM sys.index_columns AS ic
                 WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0) AS k
    OUTER APPLY (SELECT STRING_AGG(COL_NAME(ic.object_id, ic.column_id), N',') WITHIN GROUP (ORDER BY COL_NAME(ic.object_id, ic.column_id)) AS Columnas
                 FROM sys.index_columns AS ic
                 WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 1) AS inc
    UNION ALL
    SELECT t.Tabla, 3, ck.name COLLATE DATABASE_DEFAULT,
           CONCAT(t.Tabla, N' | check | ', ck.name, N' ', ck.definition,
                  IIF(ck.is_not_trusted = 1, N' [NO VALIDADA]', N''), IIF(ck.is_disabled = 1, N' [DESHABILITADA]', N'')) COLLATE DATABASE_DEFAULT
    FROM Tablas AS t
    JOIN sys.check_constraints AS ck ON ck.parent_object_id = t.object_id
    UNION ALL
    SELECT t.Tabla, 4, fk.name COLLATE DATABASE_DEFAULT,
           CONCAT(t.Tabla, N' | fk | ', fk.name, N' (', a.Origen, N') -> ', OBJECT_NAME(fk.referenced_object_id), N' (', a.Destino, N')',
                  IIF(fk.delete_referential_action <> 0, CONCAT(N' ON DELETE ', fk.delete_referential_action_desc), N''),
                  IIF(fk.is_not_trusted = 1, N' [NO VALIDADA]', N''), IIF(fk.is_disabled = 1, N' [DESHABILITADA]', N'')) COLLATE DATABASE_DEFAULT
    FROM Tablas AS t
    JOIN sys.foreign_keys AS fk ON fk.parent_object_id = t.object_id
    CROSS APPLY (SELECT STRING_AGG(COL_NAME(fc.parent_object_id, fc.parent_column_id), N',') WITHIN GROUP (ORDER BY fc.constraint_column_id) AS Origen,
                        STRING_AGG(COL_NAME(fc.referenced_object_id, fc.referenced_column_id), N',') WITHIN GROUP (ORDER BY fc.constraint_column_id) AS Destino
                 FROM sys.foreign_key_columns AS fc WHERE fc.constraint_object_id = fk.object_id) AS a
) AS huella
ORDER BY Tabla, Orden, Clave;
