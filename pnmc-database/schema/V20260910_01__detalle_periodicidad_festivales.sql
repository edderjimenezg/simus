/*
    Completa el contrato de periodicidad que ya consume el API. Estas columnas
    existían en el DDL retirado del arranque, pero no en la cadena canónica de
    migraciones; una base nueva creada solo con DbUp quedaba incompleta.
*/

IF COL_LENGTH(N'dbo.Festivales', N'PeriodicidadDetalle') IS NULL
    ALTER TABLE dbo.Festivales ADD PeriodicidadDetalle nvarchar(600) NULL;
GO

IF COL_LENGTH(N'dbo.VersionesFestival', N'PeriodicidadDetalle') IS NULL
    ALTER TABLE dbo.VersionesFestival ADD PeriodicidadDetalle nvarchar(600) NULL;
GO

IF COL_LENGTH(N'dbo.PropuestasCambioFestival', N'PeriodicidadDetalle') IS NULL
    ALTER TABLE dbo.PropuestasCambioFestival ADD PeriodicidadDetalle nvarchar(600) NULL;
GO
