/*
  SIMUS · El banco de archivos, y lo que a la Agenda le faltaba para dar de alta un evento

  DE DONDE SALE ESTO. De comparar nuestro panel de alta con el del desarrollo de septiembre, que
  es el armazon de referencia. Alli un evento se crea con nivel de cobertura, hora de fin,
  organizador, orden de visualizacion, vinculo a un Festival y una IMAGEN SUBIDA DE VERDAD; aqui
  la imagen era una ruta que alguien tecleaba y la mitad de esos campos no existian.

  LA PIEZA QUE FALTABA ERA EL BANCO. `dbo.Archivos` existe desde el principio —con su texto
  alternativo, su pie y su credito— y NINGUNA ruta del API escribia en ella: los Festivales solo
  vinculaban identificadores que nadie podia crear. Sin banco no hay subida, y sin subida el campo
  de imagen de cualquier panel es una ruta a mano.

  LOS BYTES VIVEN EN LA BASE, como ya hace `MediosWeb` con sus `varbinary`. No se inventa un
  segundo almacenamiento: el proyecto ya decidio este, ya lo sirve con ETag y ya tiene el
  reconocedor de formato que valida PNG, JPEG y WebP por su firma.

  EL TOPE VIVE EN LOS DOS SITIOS a proposito, igual que en septiembre: 2 MiB para imagen y 20 MiB
  para PDF. La mitad en el codigo rechaza con un mensaje util; la mitad en la base impide que una
  importacion que escriba directo meta un fichero de cien megas.

  NIVELCOBERTURA LLEGA CON SU CHECK, que es la regla que septiembre tiene y nosotros no: nacional
  sin codigos, departamental con departamento, municipal con los dos. Sin ella se podia guardar un
  municipio sin departamento, que no significa nada.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

/* ─────────────────────────── El contenido del archivo ─────────────────────────── */
IF COL_LENGTH('dbo.Archivos', 'Contenido') IS NULL
    ALTER TABLE dbo.Archivos ADD Contenido varbinary(max) NULL;
GO
IF COL_LENGTH('dbo.Archivos', 'Huella') IS NULL
    ALTER TABLE dbo.Archivos ADD Huella char(64) NULL;
GO
IF COL_LENGTH('dbo.Archivos', 'Ancho') IS NULL
    ALTER TABLE dbo.Archivos ADD Ancho int NULL;
GO
IF COL_LENGTH('dbo.Archivos', 'Alto') IS NULL
    ALTER TABLE dbo.Archivos ADD Alto int NULL;
GO

/*
  EL TOPE POR TIPO, la mitad que vive en la base. El mismo numero que `MediosWebContrato.MaxBytes`
  para imagen; el de documento es diez veces mayor y para un solo tipo, porque un PDF no se sirve
  en linea ni se carga entero para reconocerlo.
*/
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Archivos_PesoPorTipo')
    ALTER TABLE dbo.Archivos ADD CONSTRAINT CK_Archivos_PesoPorTipo CHECK (
        PesoBytes <= (CASE WHEN TipoMime = N'application/pdf' THEN 20971520 ELSE 2097152 END)
    );
GO

/* Buscar por huella es como se evita guardar dos veces la misma imagen. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Archivos_Huella' AND object_id = OBJECT_ID(N'dbo.Archivos'))
    CREATE INDEX IX_Archivos_Huella ON dbo.Archivos (Huella);
GO

/* ─────────────────────────── Lo que un evento necesita ─────────────────────────── */
IF COL_LENGTH('dbo.EventosAgenda', 'DescripcionLarga') IS NULL
    ALTER TABLE dbo.EventosAgenda ADD DescripcionLarga nvarchar(max) NULL;
GO
IF COL_LENGTH('dbo.EventosAgenda', 'HoraFin') IS NULL
    ALTER TABLE dbo.EventosAgenda ADD HoraFin time(0) NULL;
GO
IF COL_LENGTH('dbo.EventosAgenda', 'OrdenVisualizacion') IS NULL
    ALTER TABLE dbo.EventosAgenda ADD OrdenVisualizacion int NULL;
GO
IF COL_LENGTH('dbo.EventosAgenda', 'FestivalId') IS NULL
    ALTER TABLE dbo.EventosAgenda ADD FestivalId int NULL;
GO
IF COL_LENGTH('dbo.EventosAgenda', 'NivelCobertura') IS NULL
    ALTER TABLE dbo.EventosAgenda ADD NivelCobertura nvarchar(20) NULL;
GO

/*
  LAS FILAS QUE YA EXISTEN RECIBEN SU NIVEL DEDUCIDO DE SUS CODIGOS, no un valor por omision:
  poner 'nacional' a un evento que tiene municipio lo dejaria incumpliendo la CHECK que viene
  justo despues, y la migracion fallaria en la linea siguiente sin decir por que.
*/
UPDATE dbo.EventosAgenda
SET NivelCobertura = CASE
        WHEN CodigoMunicipio IS NOT NULL THEN N'municipal'
        WHEN CodigoDepartamento IS NOT NULL THEN N'departamental'
        ELSE N'nacional'
    END
WHERE NivelCobertura IS NULL;
GO

ALTER TABLE dbo.EventosAgenda ALTER COLUMN NivelCobertura nvarchar(20) NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_EventosAgenda_NivelCobertura')
    ALTER TABLE dbo.EventosAgenda ADD CONSTRAINT CK_EventosAgenda_NivelCobertura CHECK (
        NivelCobertura IN (N'nacional', N'departamental', N'municipal')
        AND (
            (NivelCobertura = N'nacional'      AND CodigoDepartamento IS NULL     AND CodigoMunicipio IS NULL)
         OR (NivelCobertura = N'departamental' AND CodigoDepartamento IS NOT NULL AND CodigoMunicipio IS NULL)
         OR (NivelCobertura = N'municipal'     AND CodigoDepartamento IS NOT NULL AND CodigoMunicipio IS NOT NULL)
        )
    );
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_EventosAgenda_Horas')
    ALTER TABLE dbo.EventosAgenda ADD CONSTRAINT CK_EventosAgenda_Horas CHECK (
        HoraFin IS NULL OR HoraInicio IS NULL OR HoraFin >= HoraInicio
    );
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_EventosAgenda_Orden')
    ALTER TABLE dbo.EventosAgenda ADD CONSTRAINT CK_EventosAgenda_Orden CHECK (
        OrdenVisualizacion IS NULL OR OrdenVisualizacion > 0
    );
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_EventosAgenda_Festivales')
    ALTER TABLE dbo.EventosAgenda ADD CONSTRAINT FK_EventosAgenda_Festivales
        FOREIGN KEY (FestivalId) REFERENCES dbo.Festivales (IdFestival);
GO

/* ─────────────────────────── El vinculo evento ↔ archivo ─────────────────────────── */
/*
  UNA TABLA PUENTE Y NO UNA COLUMNA, que es como lo tiene septiembre. `RolArchivo` distingue la
  imagen principal de las demas, y el orden las ordena: un evento puede llegar a tener varias y
  una columna `ImagenId` obligaria a rehacer esto el dia que ocurra.
*/
IF OBJECT_ID(N'dbo.EventosAgendaArchivos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EventosAgendaArchivos (
        IdEventoAgendaArchivo bigint IDENTITY(1,1) NOT NULL,
        EventoAgendaId bigint NOT NULL,
        ArchivoId int NOT NULL,
        RolArchivo nvarchar(80) NOT NULL CONSTRAINT DF_EventosAgendaArchivos_Rol DEFAULT (N'imagen_principal'),
        OrdenVisualizacion int NOT NULL CONSTRAINT DF_EventosAgendaArchivos_Orden DEFAULT (1),
        CONSTRAINT PK_EventosAgendaArchivos PRIMARY KEY CLUSTERED (IdEventoAgendaArchivo),
        CONSTRAINT FK_EventosAgendaArchivos_Evento FOREIGN KEY (EventoAgendaId) REFERENCES dbo.EventosAgenda (IdEventoAgenda),
        CONSTRAINT FK_EventosAgendaArchivos_Archivo FOREIGN KEY (ArchivoId) REFERENCES dbo.Archivos (IdArchivo),
        CONSTRAINT UQ_EventosAgendaArchivos UNIQUE (EventoAgendaId, ArchivoId, RolArchivo),
        CONSTRAINT CK_EventosAgendaArchivos_Orden CHECK (OrdenVisualizacion > 0)
    );

    CREATE INDEX IX_EventosAgendaArchivos_Evento ON dbo.EventosAgendaArchivos (EventoAgendaId);
    CREATE INDEX IX_EventosAgendaArchivos_Archivo ON dbo.EventosAgendaArchivos (ArchivoId);
END
GO
