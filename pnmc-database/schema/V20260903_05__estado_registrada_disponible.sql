/*
    SIMUS · Estado inicial de organizaciones

    La FK de Entidades exige que el estado de nacimiento exista también en una base creada solo
    con migraciones (sin la siembra de demostración). No se reemplazan ni se eliminan estados.
*/
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.EstadosContenido WHERE CodigoEstado = N'registrada')
BEGIN
    INSERT INTO dbo.EstadosContenido (CodigoEstado, NombreEstado, DescripcionEstado)
    VALUES (N'registrada', N'Registrada', N'Organización creada y pendiente de completar o revisar.');
END;
GO
