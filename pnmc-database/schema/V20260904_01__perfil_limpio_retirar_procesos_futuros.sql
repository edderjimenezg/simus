/*
    SIMUS · Perfil local limpio

    Este guion no altera una base existente. Solo opera sobre la instalación
    local nueva PNMC_SIMUS_LIMPIO, creada por scripts/base-limpia-local.sh.
    Permite conservar la historia de migraciones mientras se termina de
    separar físicamente el modelo heredado de seis procesos.

    El perfil vigente habilita Festivales. Escuelas de música, mercados,
    redes de documentación, lutería y escenarios quedan fuera de la base
    hasta que exista una decisión de producto, su modelo y su circuito propio.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;

IF DB_NAME() <> N'PNMC_SIMUS_LIMPIO'
BEGIN
    PRINT N'Perfil vigente conservado: el retiro físico solo corresponde a PNMC_SIMUS_LIMPIO.';
    RETURN;
END;
GO

/* La tabla de procedencia histórica puede existir en el futuro, pero no debe
   conservar un enlace a la capa genérica retirada. */
DECLARE @sql nvarchar(max) = N'';

SELECT @sql = @sql + N'ALTER TABLE dbo.EntidadesRegistrosFuente DROP CONSTRAINT [' + fk.name + N'];'
FROM sys.foreign_keys fk
WHERE fk.parent_object_id = OBJECT_ID(N'dbo.EntidadesRegistrosFuente')
  AND fk.referenced_object_id = OBJECT_ID(N'dbo.RegistrosEcosistema');

IF @sql <> N'' EXEC sp_executesql @sql;
GO

IF OBJECT_ID(N'dbo.TR_RegistrosEcosistema_ValidarOrigen', N'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_RegistrosEcosistema_ValidarOrigen;
GO

IF OBJECT_ID(N'dbo.sp_ActualizarMetricasMapa', N'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_ActualizarMetricasMapa;
GO

/* Primero las tablas dependientes, después las fuentes y, al final, el tipo
   genérico. No se usa DROP DATABASE ni se toca una base distinta del perfil. */
IF OBJECT_ID(N'dbo.RegistrosEcosistemaTerritoriosSonoros', N'U') IS NOT NULL
    DROP TABLE dbo.RegistrosEcosistemaTerritoriosSonoros;

IF OBJECT_ID(N'dbo.RegistrosEcosistemaPracticasMusicales', N'U') IS NOT NULL
    DROP TABLE dbo.RegistrosEcosistemaPracticasMusicales;

IF OBJECT_ID(N'dbo.MetricasDepartamentoMapa', N'U') IS NOT NULL
    DROP TABLE dbo.MetricasDepartamentoMapa;

IF OBJECT_ID(N'dbo.MetricasMunicipioMapa', N'U') IS NOT NULL
    DROP TABLE dbo.MetricasMunicipioMapa;

IF OBJECT_ID(N'dbo.Escenarios', N'U') IS NOT NULL
    DROP TABLE dbo.Escenarios;

IF OBJECT_ID(N'dbo.EscuelasMusica', N'U') IS NOT NULL
    DROP TABLE dbo.EscuelasMusica;

IF OBJECT_ID(N'dbo.MercadosMusicales', N'U') IS NOT NULL
    DROP TABLE dbo.MercadosMusicales;

IF OBJECT_ID(N'dbo.RedesDocumentacion', N'U') IS NOT NULL
    DROP TABLE dbo.RedesDocumentacion;

IF OBJECT_ID(N'dbo.Lutieres', N'U') IS NOT NULL
    DROP TABLE dbo.Lutieres;

IF OBJECT_ID(N'dbo.RegistrosEcosistema', N'U') IS NOT NULL
    DROP TABLE dbo.RegistrosEcosistema;

IF OBJECT_ID(N'dbo.TiposRegistroEcosistema', N'U') IS NOT NULL
    DROP TABLE dbo.TiposRegistroEcosistema;
GO
