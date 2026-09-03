CREATE TABLE nucleo.Estados (
    Codigo nvarchar(30) NOT NULL PRIMARY KEY,
    Eje nvarchar(30) NOT NULL,
    Nombre nvarchar(120) NOT NULL,
    EsTerminal bit NOT NULL,
    CONSTRAINT CK_Estados_Eje CHECK (Eje IN (N'registro', N'revision', N'publicacion', N'editorial'))
);
CREATE TABLE nucleo.TransicionesEstado (
    EstadoOrigen nvarchar(30) NOT NULL,
    EstadoDestino nvarchar(30) NOT NULL,
    PRIMARY KEY (EstadoOrigen, EstadoDestino),
    CONSTRAINT FK_Transiciones_Origen FOREIGN KEY (EstadoOrigen) REFERENCES nucleo.Estados(Codigo),
    CONSTRAINT FK_Transiciones_Destino FOREIGN KEY (EstadoDestino) REFERENCES nucleo.Estados(Codigo)
);
INSERT INTO nucleo.Estados (Codigo,Eje,Nombre,EsTerminal) VALUES
(N'registro_activo',N'registro',N'Activo',0),(N'registro_archivado',N'registro',N'Archivado',0),(N'registro_retirado',N'registro',N'Retirado',1),
(N'revision_borrador',N'revision',N'Borrador',0),(N'revision_en_revision',N'revision',N'En revisión',0),(N'revision_requiere_ajustes',N'revision',N'Requiere ajustes',0),(N'revision_aprobado',N'revision',N'Aprobado',0),
(N'publicacion_no_publicado',N'publicacion',N'No publicado',0),(N'publicacion_publicado',N'publicacion',N'Publicado',0),(N'publicacion_retirado',N'publicacion',N'Retirado de publicación',1);
INSERT INTO nucleo.TransicionesEstado (EstadoOrigen,EstadoDestino) VALUES
(N'registro_activo',N'registro_archivado'),(N'registro_archivado',N'registro_activo'),(N'registro_activo',N'registro_retirado'),
(N'revision_borrador',N'revision_en_revision'),(N'revision_en_revision',N'revision_requiere_ajustes'),(N'revision_en_revision',N'revision_aprobado'),(N'revision_requiere_ajustes',N'revision_borrador'),
(N'publicacion_no_publicado',N'publicacion_publicado'),(N'publicacion_publicado',N'publicacion_retirado');
ALTER TABLE organizaciones.Organizaciones DROP CONSTRAINT CK_Organizaciones_Estado;
EXEC sp_rename N'organizaciones.Organizaciones.Estado', N'EstadoRegistro', N'COLUMN';
EXEC(N'UPDATE organizaciones.Organizaciones SET EstadoRegistro=CASE EstadoRegistro WHEN N''activa'' THEN N''registro_activo'' WHEN N''archivada'' THEN N''registro_archivado'' ELSE EstadoRegistro END;');
EXEC(N'ALTER TABLE organizaciones.Organizaciones ADD CONSTRAINT FK_Organizaciones_EstadoRegistro FOREIGN KEY (EstadoRegistro) REFERENCES nucleo.Estados(Codigo);');
ALTER TABLE festivales.Festivales DROP CONSTRAINT CK_Festivales_EstadoIdentidad;
EXEC sp_rename N'festivales.Festivales.EstadoIdentidad', N'EstadoRegistro', N'COLUMN';
EXEC(N'UPDATE festivales.Festivales SET EstadoRegistro=CASE EstadoRegistro WHEN N''activa'' THEN N''registro_activo'' WHEN N''archivada'' THEN N''registro_archivado'' WHEN N''retirada'' THEN N''registro_retirado'' ELSE EstadoRegistro END;');
EXEC(N'ALTER TABLE festivales.Festivales ADD CONSTRAINT FK_Festivales_EstadoRegistro FOREIGN KEY (EstadoRegistro) REFERENCES nucleo.Estados(Codigo);');
DROP INDEX IX_Perfiles_FestivalEstado ON festivales.Perfiles;
ALTER TABLE festivales.Perfiles DROP CONSTRAINT CK_Perfiles_EstadoEditorial;
EXEC sp_rename N'festivales.Perfiles.EstadoEditorial', N'EstadoRevision', N'COLUMN';
EXEC(N'UPDATE festivales.Perfiles SET EstadoRevision=CASE EstadoRevision WHEN N''borrador'' THEN N''revision_borrador'' WHEN N''en_revision'' THEN N''revision_en_revision'' WHEN N''requiere_ajustes'' THEN N''revision_requiere_ajustes'' WHEN N''publicado'' THEN N''revision_aprobado'' WHEN N''retirado'' THEN N''revision_requiere_ajustes'' ELSE EstadoRevision END;');
EXEC(N'ALTER TABLE festivales.Perfiles ADD CONSTRAINT FK_Perfiles_EstadoRevision FOREIGN KEY (EstadoRevision) REFERENCES nucleo.Estados(Codigo);');
EXEC(N'CREATE INDEX IX_Perfiles_FestivalEstadoRevision ON festivales.Perfiles(IdFestival,EstadoRevision,NumeroVersion DESC);');
INSERT INTO nucleo.VersionesEsquema (Version) VALUES (N'003_estados_transversales');
