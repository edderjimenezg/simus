/* Demostración reproducible: se ejecuta después de schema/V20260901_01. */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
DECLARE @Autor int = (SELECT TOP 1 IdUsuario FROM dbo.Usuarios WHERE CorreoElectronico = N'externo@pnmc.local');
IF @Autor IS NULL THROW 52901, N'Falta la cuenta externa de arranque para la siembra de reclamaciones.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Entidades WHERE EsInstitucional=1)
INSERT dbo.Entidades (TipoEntidad,Nombre,CorreoContacto,NivelCobertura,EstadoRegistro,Activo,EsInstitucional,IdUsuarioCreador,IdUsuarioResponsable,FechaCreacion)
VALUES (N'organizacion',N'Administración institucional PNMC',N'institucional.demo@pnmc.local',N'nacional',N'publicado',1,1,@Autor,@Autor,SYSUTCDATETIME());
DECLARE @Institucional int = (SELECT TOP 1 IdEntidad FROM dbo.Entidades WHERE EsInstitucional=1);

IF NOT EXISTS (SELECT 1 FROM dbo.Entidades WHERE Nombre=N'Corporación Cultural de Neiva')
INSERT dbo.Entidades (TipoEntidad,Nombre,CorreoContacto,NivelCobertura,CodigoDepartamento,CodigoMunicipio,EstadoRegistro,Activo,IdUsuarioCreador,IdUsuarioResponsable,FechaCreacion)
VALUES (N'organizacion',N'Corporación Cultural de Neiva',N'neiva.demo@pnmc.local',N'municipal',N'41',N'41001',N'borrador',1,@Autor,@Autor,SYSUTCDATETIME());

DECLARE @Neiva int=(SELECT IdEntidad FROM dbo.Entidades WHERE Nombre=N'Corporación Cultural de Neiva');
IF NOT EXISTS (SELECT 1 FROM dbo.UsuariosEntidades WHERE IdUsuario=@Autor AND IdEntidad=@Neiva)
INSERT dbo.UsuariosEntidades (IdUsuario,IdEntidad,RolEntidad,Activo,FechaCreacion) VALUES (@Autor,@Neiva,N'administrador',1,SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.Festivales WHERE NombreFestival=N'Festival Sonidos del Huila')
INSERT dbo.Festivales (NombreFestival,Descripcion,Organizador,NivelCobertura,CodigoDepartamento,CodigoMunicipio,EstadoRegistro,OrganizacionPrincipalId,FechaCreacion)
VALUES(N'Festival Sonidos del Huila',N'Registro histórico de demostración.',N'Corporacion Cultural de Neiva',N'municipal',N'41',N'41001',N'publicado',@Institucional,SYSUTCDATETIME());

/* Datos sintéticos de demostración: no corresponden a festivales u organizaciones reales. */
IF NOT EXISTS (SELECT 1 FROM dbo.Festivales WHERE NombreFestival=N'Encuentro Voces del Río')
INSERT dbo.Festivales (NombreFestival,Descripcion,Organizador,CorreoFestival,TelefonoFestival,InstagramFestival,FacebookFestival,SitioWebFestival,OtroEnlaceFestival,NivelCobertura,CodigoDepartamento,CodigoMunicipio,Periodicidad,EstadoRegistro,OrganizacionPrincipalId,FechaCreacion)
VALUES(N'Encuentro Voces del Río',N'Demostración: encuentro de músicas tradicionales y contemporáneas alrededor del río.',N'Administración institucional PNMC',N'vocesrio.demo@pnmc.local',N'3000000001',N'instagram.com/vocesdelrio.demo',N'facebook.com/vocesdelrio.demo',N'https://demo.pnmc.local/voces-del-rio',NULL,N'municipal',N'08',N'08001',N'anual',N'publicado',@Institucional,SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM dbo.Festivales WHERE NombreFestival=N'Muestra Andina de Cuerdas')
INSERT dbo.Festivales (NombreFestival,Descripcion,Organizador,CorreoFestival,TelefonoFestival,InstagramFestival,FacebookFestival,SitioWebFestival,OtroEnlaceFestival,NivelCobertura,CodigoDepartamento,CodigoMunicipio,Periodicidad,EstadoRegistro,OrganizacionPrincipalId,FechaCreacion)
VALUES(N'Muestra Andina de Cuerdas',N'Demostración: programación de cuerdas andinas, formación y circulación local.',N'Administración institucional PNMC',N'cuerdasandinas.demo@pnmc.local',N'3000000002',N'instagram.com/cuerdasandinas.demo',N'facebook.com/cuerdasandinas.demo',N'https://demo.pnmc.local/cuerdas-andinas',NULL,N'municipal',N'17',N'17001',N'anual',N'publicado',@Institucional,SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM dbo.Festivales WHERE NombreFestival=N'Festival Brisas del Caribe')
INSERT dbo.Festivales (NombreFestival,Descripcion,Organizador,CorreoFestival,TelefonoFestival,InstagramFestival,FacebookFestival,SitioWebFestival,OtroEnlaceFestival,NivelCobertura,CodigoDepartamento,CodigoMunicipio,Periodicidad,EstadoRegistro,OrganizacionPrincipalId,FechaCreacion)
VALUES(N'Festival Brisas del Caribe',N'Demostración: festival de músicas de banda, tambores y procesos comunitarios.',N'Administración institucional PNMC',N'brisascaribe.demo@pnmc.local',N'3000000003',N'instagram.com/brisascaribe.demo',N'facebook.com/brisascaribe.demo',N'https://demo.pnmc.local/brisas-caribe',NULL,N'municipal',N'13',N'13001',N'anual',N'publicado',@Institucional,SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM dbo.Festivales WHERE NombreFestival=N'Circuito Sonoro del Pacífico')
INSERT dbo.Festivales (NombreFestival,Descripcion,Organizador,CorreoFestival,TelefonoFestival,InstagramFestival,FacebookFestival,SitioWebFestival,OtroEnlaceFestival,NivelCobertura,CodigoDepartamento,CodigoMunicipio,Periodicidad,EstadoRegistro,OrganizacionPrincipalId,FechaCreacion)
VALUES(N'Circuito Sonoro del Pacífico',N'Demostración: circulación territorial de marimba, cantos y agrupaciones del Pacífico.',N'Administración institucional PNMC',N'circuitopacifico.demo@pnmc.local',N'3000000004',N'instagram.com/circuitopacifico.demo',N'facebook.com/circuitopacifico.demo',N'https://demo.pnmc.local/circuito-pacifico',NULL,N'municipal',N'76',N'76001',N'anual',N'publicado',@Institucional,SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM dbo.Festivales WHERE NombreFestival=N'Jornadas Llaneras de la Sabana')
INSERT dbo.Festivales (NombreFestival,Descripcion,Organizador,CorreoFestival,TelefonoFestival,InstagramFestival,FacebookFestival,SitioWebFestival,OtroEnlaceFestival,NivelCobertura,CodigoDepartamento,CodigoMunicipio,Periodicidad,EstadoRegistro,OrganizacionPrincipalId,FechaCreacion)
VALUES(N'Jornadas Llaneras de la Sabana',N'Demostración: encuentro de músicas llaneras, baile y transmisión de saberes.',N'Administración institucional PNMC',N'jornadasllaneras.demo@pnmc.local',N'3000000005',N'instagram.com/jornadasllaneras.demo',N'facebook.com/jornadasllaneras.demo',N'https://demo.pnmc.local/jornadas-llaneras',NULL,N'municipal',N'50',N'50001',N'anual',N'publicado',@Institucional,SYSUTCDATETIME());
