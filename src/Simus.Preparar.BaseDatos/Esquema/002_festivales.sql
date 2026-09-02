IF SCHEMA_ID(N'festivales') IS NULL EXEC(N'CREATE SCHEMA festivales');

CREATE TABLE festivales.Festivales (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    IdOrganizacionAdministradora uniqueidentifier NULL,
    EstadoIdentidad nvarchar(30) NOT NULL,
    FechaCreacion datetime2(0) NOT NULL,
    FechaActualizacion datetime2(0) NOT NULL,
    CONSTRAINT FK_Festivales_OrganizacionAdministradora FOREIGN KEY(IdOrganizacionAdministradora) REFERENCES organizaciones.Organizaciones(Id),
    CONSTRAINT CK_Festivales_EstadoIdentidad CHECK (EstadoIdentidad IN (N'activa',N'archivada',N'retirada'))
);

CREATE TABLE festivales.Perfiles (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    IdFestival uniqueidentifier NOT NULL,
    NumeroVersion int NOT NULL,
    EstadoEditorial nvarchar(30) NOT NULL,
    Nombre nvarchar(240) NOT NULL,
    Descripcion nvarchar(max) NULL,
    CodigoDepartamento nvarchar(10) NOT NULL,
    CodigoMunicipio nvarchar(10) NOT NULL,
    IdPersonaCreadora uniqueidentifier NOT NULL,
    FechaCreacion datetime2(0) NOT NULL,
    FechaActualizacion datetime2(0) NOT NULL,
    CONSTRAINT UQ_Perfiles_FestivalVersion UNIQUE(IdFestival,NumeroVersion),
    CONSTRAINT FK_Perfiles_Festival FOREIGN KEY(IdFestival) REFERENCES festivales.Festivales(Id),
    CONSTRAINT FK_Perfiles_Departamento FOREIGN KEY(CodigoDepartamento) REFERENCES territorio.Departamentos(Codigo),
    CONSTRAINT FK_Perfiles_Municipio FOREIGN KEY(CodigoMunicipio,CodigoDepartamento) REFERENCES territorio.Municipios(Codigo,CodigoDepartamento),
    CONSTRAINT FK_Perfiles_PersonaCreadora FOREIGN KEY(IdPersonaCreadora) REFERENCES identidad.Personas(Id),
    CONSTRAINT CK_Perfiles_NumeroVersion CHECK (NumeroVersion > 0),
    CONSTRAINT CK_Perfiles_EstadoEditorial CHECK (EstadoEditorial IN (N'borrador',N'en_revision',N'publicado',N'requiere_ajustes',N'retirado'))
);

CREATE INDEX IX_Festivales_Organizacion ON festivales.Festivales(IdOrganizacionAdministradora,EstadoIdentidad);
CREATE INDEX IX_Perfiles_FestivalEstado ON festivales.Perfiles(IdFestival,EstadoEditorial,NumeroVersion DESC);

INSERT INTO nucleo.VersionesEsquema (Version) VALUES (N'002_festivales');
