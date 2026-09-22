/*
    SIMUS · Un solo perfil de base de datos

    QUE PROBLEMA CIERRA. Desde la pregunta «qué tablas tiene SIMUS»
    tenía dos respuestas. `V20260904_01` retira los cinco procesos heredados —escuelas de música,
    mercados musicales, redes de documentación, lutería y escenarios— junto con la capa genérica
    `RegistrosEcosistema` y las métricas del mapa, pero solo si la base se llama
    `PNMC_SIMUS_LIMPIO`; en cualquier otra imprime un aviso y no hace nada. El resultado: el mismo
    código convivía con dos esquemas distintos según el nombre de la base.

    Este guion aplica el mismo retiro en TODOS los perfiles. Las clases del modelo EF que
    describían esas tablas se retiraron en el mismo corte, así que conservarlas en la base solo
    dejaba estructuras que nadie lee.

    NO SE PIERDE INFORMACION. Igual que `V20260904_03`, primero comprueba y se detiene si alguna
    contiene filas: exportar o conciliar es una decisión de producto, no de una migración.

    VOLVERAN, Y NO ASI. Los cinco procesos entran de nuevo con modelo, permisos y circuito de
    revisión propios, como se hizo con Festivales. Lo que se retira aquí es la capa genérica que
    nunca tuvo circuito, no la intención de tenerlos.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

DECLARE @tabla sysname;
DECLARE @tablas TABLE (Nombre sysname NOT NULL PRIMARY KEY);

INSERT INTO @tablas (Nombre)
VALUES
    (N'RegistrosEcosistemaTerritoriosSonoros'), (N'RegistrosEcosistemaPracticasMusicales'),
    (N'MetricasDepartamentoMapa'), (N'MetricasMunicipioMapa'),
    (N'Escenarios'), (N'EscuelasMusica'), (N'MercadosMusicales'),
    (N'RedesDocumentacion'), (N'Lutieres'),
    (N'RegistrosEcosistema'), (N'TiposRegistroEcosistema');

DECLARE cursor_tablas CURSOR LOCAL FAST_FORWARD FOR
    SELECT Nombre FROM @tablas;

OPEN cursor_tablas;
FETCH NEXT FROM cursor_tablas INTO @tabla;

WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'dbo.' + @tabla, N'U') IS NOT NULL
    BEGIN
        DECLARE @sql nvarchar(max) = N'IF EXISTS (SELECT 1 FROM dbo.' + QUOTENAME(@tabla) + N')
            THROW 51004, ''No se puede retirar dbo.' + @tabla + N': contiene registros. Exporte o concilie sus datos antes de continuar.'', 1;';
        EXEC sys.sp_executesql @sql;
    END;

    FETCH NEXT FROM cursor_tablas INTO @tabla;
END;

CLOSE cursor_tablas;
DEALLOCATE cursor_tablas;
GO

/* El disparador y el procedimiento de la capa genérica, antes que las tablas que tocan. */
IF OBJECT_ID(N'dbo.TR_RegistrosEcosistema_ValidarOrigen', N'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_RegistrosEcosistema_ValidarOrigen;
GO

IF OBJECT_ID(N'dbo.sp_ActualizarMetricasMapa', N'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_ActualizarMetricasMapa;
GO

/* La procedencia histórica puede existir en el futuro, pero no debe conservar un enlace a la
   capa genérica retirada. */
DECLARE @fks nvarchar(max) = N'';

SELECT @fks = @fks + N'ALTER TABLE dbo.EntidadesRegistrosFuente DROP CONSTRAINT [' + fk.name + N'];'
FROM sys.foreign_keys fk
WHERE fk.parent_object_id = OBJECT_ID(N'dbo.EntidadesRegistrosFuente')
  AND fk.referenced_object_id = OBJECT_ID(N'dbo.RegistrosEcosistema');

IF @fks <> N'' EXEC sys.sp_executesql @fks;
GO

/* Primero las dependientes, después las fuentes y al final el tipo genérico. */
IF OBJECT_ID(N'dbo.RegistrosEcosistemaTerritoriosSonoros', N'U') IS NOT NULL DROP TABLE dbo.RegistrosEcosistemaTerritoriosSonoros;
IF OBJECT_ID(N'dbo.RegistrosEcosistemaPracticasMusicales', N'U') IS NOT NULL DROP TABLE dbo.RegistrosEcosistemaPracticasMusicales;
IF OBJECT_ID(N'dbo.MetricasDepartamentoMapa', N'U') IS NOT NULL DROP TABLE dbo.MetricasDepartamentoMapa;
IF OBJECT_ID(N'dbo.MetricasMunicipioMapa', N'U') IS NOT NULL DROP TABLE dbo.MetricasMunicipioMapa;
IF OBJECT_ID(N'dbo.Escenarios', N'U') IS NOT NULL DROP TABLE dbo.Escenarios;
IF OBJECT_ID(N'dbo.EscuelasMusica', N'U') IS NOT NULL DROP TABLE dbo.EscuelasMusica;
IF OBJECT_ID(N'dbo.MercadosMusicales', N'U') IS NOT NULL DROP TABLE dbo.MercadosMusicales;
IF OBJECT_ID(N'dbo.RedesDocumentacion', N'U') IS NOT NULL DROP TABLE dbo.RedesDocumentacion;
IF OBJECT_ID(N'dbo.Lutieres', N'U') IS NOT NULL DROP TABLE dbo.Lutieres;
IF OBJECT_ID(N'dbo.RegistrosEcosistema', N'U') IS NOT NULL DROP TABLE dbo.RegistrosEcosistema;
IF OBJECT_ID(N'dbo.TiposRegistroEcosistema', N'U') IS NOT NULL DROP TABLE dbo.TiposRegistroEcosistema;
GO
