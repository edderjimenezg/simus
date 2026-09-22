/*
  SIMUS · Catálogo Editorial — persistencia del contrato cerrado en 05A

  QUE MODELA. La ficha bibliográfica es RELACIONAL, no una fila con cadenas agregadas: agentes,
  créditos, identificadores, accesos, fuentes y programas viven en sus propias tablas. Es la
  decisión de una revisión anterior y la que distingue este catálogo de la tabla plana `CatalogoEditorial`
  que se retiró en `V20260904_03`.

  LAS DOS DECISIONES QUE NO SE MEZCLAN, y por eso son dos columnas y no una:
    - `EstadoCatalogacion` describe la CALIDAD de la ficha (pendiente_revision, en_revision,
      validada, observada).
    - `EstadoPublicacion` describe su VISIBILIDAD (borrador, en_revision, publicado, retirado).
  Una ficha validada puede quedarse sin publicar, y publicar no valida nada por sí mismo.

  LOS DERECHOS SON UNA DECISION PROPIA, con dos autorizaciones separadas: publicar la ficha y
  publicar el archivo. Una ubicación física autorizada puede convivir con un archivo restringido,
  porque cada acceso lleva su propia decisión de derechos.

  NO SIEMBRA NADA. Ni publicaciones, ni agentes, ni vocabularios descriptivos, ni el programa
  «Proyecto Editorial». La fuente institucional y las licencias estan por confirmar: el sistema se puede
  construir con estas puertas, pero no puede publicar hasta que esas decisiones se registren.

  NINGUNA FORANEA LLEVA CASCADA, y no es descuido ni rigidez: es la regla del esquema, que
  `EstructuraSimusSqlServerTests` comprueba. Una cascada borra filas en silencio y deja el mismo
  `DELETE` comportandose de dos maneras segun la tabla. Retirar una publicacion es una operacion
  del dominio —tiene estado `retirado`— y no un borrado; si alguna vez hubiera que borrarla de
  verdad, sus creditos, identificadores, accesos, fuentes y programas se retiran explicitamente
  antes, a la vista de quien lo hace.

  LOS VOCABULARIOS DESCRIPTIVOS NO SE FIJAN AQUI. Tipos de publicación, prácticas, funciones,
  ámbitos y roles editoriales se administrarán como datos versionados. Las únicas listas cerradas
  en CHECK son las estructurales del contrato, que son comprobables sin fuente institucional.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

/* ─────────────────────────── Agentes (control de autoridades) ─────────────────────────── */
IF OBJECT_ID(N'dbo.AgentesEditoriales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AgentesEditoriales (
        IdAgenteEditorial bigint IDENTITY(1,1) NOT NULL,
        Codigo nvarchar(60) NOT NULL,
        Tipo nvarchar(20) NOT NULL,
        NombrePreferido nvarchar(300) NOT NULL,
        FormaNormalizada nvarchar(300) NULL,
        Seudonimo nvarchar(300) NULL,
        Acronimo nvarchar(60) NULL,
        /* Una variante detectada o un posible duplicado NO se funden solos: lo decide una persona. */
        RequiereRevision bit NOT NULL CONSTRAINT DF_AgentesEditoriales_Revision DEFAULT (0),
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_AgentesEditoriales_Creacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion datetime2(0) NOT NULL CONSTRAINT DF_AgentesEditoriales_Actualizacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_AgentesEditoriales PRIMARY KEY (IdAgenteEditorial),
        CONSTRAINT UQ_AgentesEditoriales_Codigo UNIQUE (Codigo),
        CONSTRAINT CK_AgentesEditoriales_Tipo CHECK (Tipo IN (N'persona', N'entidad'))
    );
    CREATE INDEX IX_AgentesEditoriales_Nombre ON dbo.AgentesEditoriales (NombrePreferido);
END;
GO

/* ─────────────────────────── Fuentes verificables ─────────────────────────── */
IF OBJECT_ID(N'dbo.FuentesEditoriales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FuentesEditoriales (
        IdFuenteEditorial bigint IDENTITY(1,1) NOT NULL,
        Nombre nvarchar(300) NOT NULL,
        Referencia nvarchar(600) NULL,
        /* NULL admitido a proposito: una fuente puede ser un documento institucional sin URL
           publica. Su `Referencia` es entonces lo que permite localizarla. */
        Url nvarchar(1000) NULL,
        FechaFuente datetime2(0) NULL,
        FechaConsulta datetime2(0) NOT NULL,
        VerificadaPor nvarchar(200) NOT NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_FuentesEditoriales_Creacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_FuentesEditoriales PRIMARY KEY (IdFuenteEditorial)
    );
END;
GO

/* ─────────────────────────── Programas ─────────────────────────── */
IF OBJECT_ID(N'dbo.ProgramasEditoriales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProgramasEditoriales (
        IdProgramaEditorial bigint IDENTITY(1,1) NOT NULL,
        Codigo nvarchar(60) NOT NULL,
        Nombre nvarchar(200) NOT NULL,
        Activo bit NOT NULL CONSTRAINT DF_ProgramasEditoriales_Activo DEFAULT (1),
        CONSTRAINT PK_ProgramasEditoriales PRIMARY KEY (IdProgramaEditorial),
        CONSTRAINT UQ_ProgramasEditoriales_Codigo UNIQUE (Codigo)
    );
END;
GO

/* ─────────────────────────── Publicaciones ─────────────────────────── */
IF OBJECT_ID(N'dbo.PublicacionesEditoriales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PublicacionesEditoriales (
        IdPublicacionEditorial bigint IDENTITY(1,1) NOT NULL,
        /* EL CODIGO ES LA IDENTIDAD ESTABLE. El titulo no sirve de clave: cambia, se repite entre
           volumenes de una serie y llega con erratas desde cualquier fuente. */
        Codigo nvarchar(60) NOT NULL,
        Titulo nvarchar(500) NOT NULL,
        Subtitulo nvarchar(500) NULL,
        DesignacionVolumen nvarchar(120) NULL,
        SerieOColeccion nvarchar(300) NULL,
        Resumen nvarchar(max) NULL,
        /* EDTF admite lo que ISO 8601 no: un rango «1990/1992» o una fecha incierta «2015?».
           Los dos anios se guardan aparte para poder ordenar y filtrar sin interpretar el texto. */
        FechaEdtf nvarchar(60) NULL,
        AnioInicio int NULL,
        AnioFin int NULL,
        Idioma nvarchar(20) NULL,
        EstadoCatalogacion nvarchar(30) NOT NULL CONSTRAINT DF_PublicacionesEditoriales_Catalogacion DEFAULT (N'pendiente_revision'),
        EstadoPublicacion nvarchar(30) NOT NULL CONSTRAINT DF_PublicacionesEditoriales_Publicacion DEFAULT (N'borrador'),
        /* Control de concurrencia: guardar cita la version leida y el servidor rechaza si cambio. */
        Version int NOT NULL CONSTRAINT DF_PublicacionesEditoriales_Version DEFAULT (1),
        DerechosEstado nvarchar(30) NOT NULL CONSTRAINT DF_PublicacionesEditoriales_DerechosEstado DEFAULT (N'pendiente'),
        DerechosPermitePublicarFicha bit NOT NULL CONSTRAINT DF_PublicacionesEditoriales_PermiteFicha DEFAULT (0),
        DerechosPermitePublicarArchivo bit NOT NULL CONSTRAINT DF_PublicacionesEditoriales_PermiteArchivo DEFAULT (0),
        DerechosLicenciaONota nvarchar(600) NULL,
        DerechosFuenteId bigint NULL,
        DerechosFechaVerificacion datetime2(0) NULL,
        DerechosVerificadoPor nvarchar(200) NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_PublicacionesEditoriales_Creacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion datetime2(0) NOT NULL CONSTRAINT DF_PublicacionesEditoriales_Actualizacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_PublicacionesEditoriales PRIMARY KEY (IdPublicacionEditorial),
        CONSTRAINT UQ_PublicacionesEditoriales_Codigo UNIQUE (Codigo),
        CONSTRAINT CK_PublicacionesEditoriales_Catalogacion CHECK (EstadoCatalogacion IN
            (N'pendiente_revision', N'en_revision', N'validada', N'observada')),
        CONSTRAINT CK_PublicacionesEditoriales_Publicacion CHECK (EstadoPublicacion IN
            (N'borrador', N'en_revision', N'publicado', N'retirado')),
        CONSTRAINT CK_PublicacionesEditoriales_DerechosEstado CHECK (DerechosEstado IN
            (N'pendiente', N'verificado', N'restringido')),
        CONSTRAINT CK_PublicacionesEditoriales_Anios CHECK (AnioFin IS NULL OR AnioInicio IS NULL OR AnioFin >= AnioInicio),
        CONSTRAINT FK_PublicacionesEditoriales_DerechosFuente FOREIGN KEY (DerechosFuenteId)
            REFERENCES dbo.FuentesEditoriales(IdFuenteEditorial)
    );
    CREATE INDEX IX_PublicacionesEditoriales_Publicacion ON dbo.PublicacionesEditoriales (EstadoPublicacion, EstadoCatalogacion);
    CREATE INDEX IX_PublicacionesEditoriales_Titulo ON dbo.PublicacionesEditoriales (Titulo);
END;
GO

/* ─────────────────────────── Fuentes de cada publicación ─────────────────────────── */
IF OBJECT_ID(N'dbo.PublicacionesEditorialesFuentes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PublicacionesEditorialesFuentes (
        PublicacionEditorialId bigint NOT NULL,
        FuenteEditorialId bigint NOT NULL,
        CONSTRAINT PK_PublicacionesEditorialesFuentes PRIMARY KEY (PublicacionEditorialId, FuenteEditorialId),
        CONSTRAINT FK_PublicacionesEditorialesFuentes_Publicacion FOREIGN KEY (PublicacionEditorialId)
            REFERENCES dbo.PublicacionesEditoriales(IdPublicacionEditorial),
        /* SIN CASCADA hacia la fuente: borrar una fuente no puede llevarse por delante la
           evidencia de las fichas que la citan. Se impide el borrado y se decide a mano. */
        CONSTRAINT FK_PublicacionesEditorialesFuentes_Fuente FOREIGN KEY (FuenteEditorialId)
            REFERENCES dbo.FuentesEditoriales(IdFuenteEditorial)
    );
    CREATE INDEX IX_PublicacionesEditorialesFuentes_Fuente ON dbo.PublicacionesEditorialesFuentes (FuenteEditorialId);
END;
GO

/* ─────────────────────────── Créditos ─────────────────────────── */
IF OBJECT_ID(N'dbo.CreditosEditoriales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CreditosEditoriales (
        IdCreditoEditorial bigint IDENTITY(1,1) NOT NULL,
        PublicacionEditorialId bigint NOT NULL,
        AgenteEditorialId bigint NOT NULL,
        /* El codigo es MARC Relators (aut, cmp, arr, edt, pbl...). La ETIQUETA se guarda aparte y
           no se deriva del codigo: el catalogo presenta «Arreglista» o «Arreglos», y esa decision
           es editorial, no una tabla de traduccion escondida en el codigo. */
        RolCodigo nvarchar(20) NOT NULL,
        RolEtiqueta nvarchar(120) NOT NULL,
        Principal bit NOT NULL CONSTRAINT DF_CreditosEditoriales_Principal DEFAULT (0),
        Orden int NOT NULL CONSTRAINT DF_CreditosEditoriales_Orden DEFAULT (0),
        CONSTRAINT PK_CreditosEditoriales PRIMARY KEY (IdCreditoEditorial),
        CONSTRAINT FK_CreditosEditoriales_Publicacion FOREIGN KEY (PublicacionEditorialId)
            REFERENCES dbo.PublicacionesEditoriales(IdPublicacionEditorial),
        CONSTRAINT FK_CreditosEditoriales_Agente FOREIGN KEY (AgenteEditorialId)
            REFERENCES dbo.AgentesEditoriales(IdAgenteEditorial),
        CONSTRAINT UQ_CreditosEditoriales_PorRol UNIQUE (PublicacionEditorialId, AgenteEditorialId, RolCodigo)
    );
    CREATE INDEX IX_CreditosEditoriales_Agente ON dbo.CreditosEditoriales (AgenteEditorialId);
END;
GO

/* ─────────────────────────── Identificadores ─────────────────────────── */
IF OBJECT_ID(N'dbo.IdentificadoresEditoriales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IdentificadoresEditoriales (
        IdIdentificadorEditorial bigint IDENTITY(1,1) NOT NULL,
        PublicacionEditorialId bigint NOT NULL,
        Esquema nvarchar(20) NOT NULL,
        /* SE CONSERVA EL VALOR RECIBIDO, tal cual llego. La validacion del digito de control se
           registra aparte: un ISBN mal transcrito en la fuente es un dato del catalogo, y
           corregirlo en silencio al guardarlo borraria la prueba de que la fuente venia mal. */
        CodigoRecibido nvarchar(60) NOT NULL,
        Cualificador nvarchar(120) NULL,
        Valido bit NULL,
        ObservacionValidacion nvarchar(400) NULL,
        CONSTRAINT PK_IdentificadoresEditoriales PRIMARY KEY (IdIdentificadorEditorial),
        CONSTRAINT FK_IdentificadoresEditoriales_Publicacion FOREIGN KEY (PublicacionEditorialId)
            REFERENCES dbo.PublicacionesEditoriales(IdPublicacionEditorial),
        CONSTRAINT CK_IdentificadoresEditoriales_Esquema CHECK (Esquema IN (N'ISBN', N'ISMN'))
    );
    CREATE INDEX IX_IdentificadoresEditoriales_Codigo ON dbo.IdentificadoresEditoriales (Esquema, CodigoRecibido);
END;
GO

/* ─────────────────────────── Accesos ─────────────────────────── */
IF OBJECT_ID(N'dbo.AccesosEditoriales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AccesosEditoriales (
        IdAccesoEditorial bigint IDENTITY(1,1) NOT NULL,
        PublicacionEditorialId bigint NOT NULL,
        Tipo nvarchar(20) NOT NULL,
        ArchivoId int NULL,
        Url nvarchar(1000) NULL,
        UbicacionFisica nvarchar(600) NULL,
        Etiqueta nvarchar(200) NULL,
        Nota nvarchar(600) NULL,
        Orden int NOT NULL CONSTRAINT DF_AccesosEditoriales_Orden DEFAULT (0),
        DerechosEstado nvarchar(30) NOT NULL CONSTRAINT DF_AccesosEditoriales_DerechosEstado DEFAULT (N'pendiente'),
        DerechosPermitePublicarFicha bit NOT NULL CONSTRAINT DF_AccesosEditoriales_PermiteFicha DEFAULT (0),
        DerechosPermitePublicarArchivo bit NOT NULL CONSTRAINT DF_AccesosEditoriales_PermiteArchivo DEFAULT (0),
        DerechosLicenciaONota nvarchar(600) NULL,
        DerechosFuenteId bigint NULL,
        DerechosFechaVerificacion datetime2(0) NULL,
        DerechosVerificadoPor nvarchar(200) NULL,
        CONSTRAINT PK_AccesosEditoriales PRIMARY KEY (IdAccesoEditorial),
        CONSTRAINT FK_AccesosEditoriales_Publicacion FOREIGN KEY (PublicacionEditorialId)
            REFERENCES dbo.PublicacionesEditoriales(IdPublicacionEditorial),
        CONSTRAINT FK_AccesosEditoriales_Archivo FOREIGN KEY (ArchivoId)
            REFERENCES dbo.Archivos(IdArchivo),
        CONSTRAINT FK_AccesosEditoriales_DerechosFuente FOREIGN KEY (DerechosFuenteId)
            REFERENCES dbo.FuentesEditoriales(IdFuenteEditorial),
        CONSTRAINT CK_AccesosEditoriales_Tipo CHECK (Tipo IN (N'archivo', N'enlace', N'ubicacion')),
        CONSTRAINT CK_AccesosEditoriales_DerechosEstado CHECK (DerechosEstado IN
            (N'pendiente', N'verificado', N'restringido')),
        /* UN SOLO DESTINO, Y QUE CORRESPONDA CON SU TIPO. La misma regla que `TieneDestinoValido`
           comprueba en el contrato, impuesta tambien aqui: si solo vive en el codigo, la primera
           importacion o el primer guion SQL que escriba directo la salta sin enterarse. */
        CONSTRAINT CK_AccesosEditoriales_UnSoloDestino CHECK (
            (Tipo = N'archivo'   AND ArchivoId IS NOT NULL AND Url IS NULL     AND UbicacionFisica IS NULL)
         OR (Tipo = N'enlace'    AND ArchivoId IS NULL     AND Url IS NOT NULL AND UbicacionFisica IS NULL)
         OR (Tipo = N'ubicacion' AND ArchivoId IS NULL     AND Url IS NULL     AND UbicacionFisica IS NOT NULL)
        )
    );
    CREATE INDEX IX_AccesosEditoriales_Publicacion ON dbo.AccesosEditoriales (PublicacionEditorialId, Orden);
END;
GO

/* ─────────────────────────── Pertenencia a programas ─────────────────────────── */
IF OBJECT_ID(N'dbo.PublicacionesEditorialesProgramas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PublicacionesEditorialesProgramas (
        IdPublicacionEditorialPrograma bigint IDENTITY(1,1) NOT NULL,
        PublicacionEditorialId bigint NOT NULL,
        ProgramaEditorialId bigint NOT NULL,
        /* LA FUENTE ES OBLIGATORIA. La pertenencia a un programa no se infiere del titulo ni del
           anio: se afirma, y quien la afirma deja con que la respalda. */
        FuenteEditorialId bigint NOT NULL,
        VigenteDesde date NULL,
        VigenteHasta date NULL,
        VerificadaPor nvarchar(200) NOT NULL,
        CONSTRAINT PK_PublicacionesEditorialesProgramas PRIMARY KEY (IdPublicacionEditorialPrograma),
        CONSTRAINT FK_PublicacionesEditorialesProgramas_Publicacion FOREIGN KEY (PublicacionEditorialId)
            REFERENCES dbo.PublicacionesEditoriales(IdPublicacionEditorial),
        CONSTRAINT FK_PublicacionesEditorialesProgramas_Programa FOREIGN KEY (ProgramaEditorialId)
            REFERENCES dbo.ProgramasEditoriales(IdProgramaEditorial),
        CONSTRAINT FK_PublicacionesEditorialesProgramas_Fuente FOREIGN KEY (FuenteEditorialId)
            REFERENCES dbo.FuentesEditoriales(IdFuenteEditorial),
        CONSTRAINT UQ_PublicacionesEditorialesProgramas UNIQUE (PublicacionEditorialId, ProgramaEditorialId),
        CONSTRAINT CK_PublicacionesEditorialesProgramas_Vigencia CHECK (
            VigenteHasta IS NULL OR VigenteDesde IS NULL OR VigenteHasta >= VigenteDesde)
    );
    CREATE INDEX IX_PublicacionesEditorialesProgramas_Programa ON dbo.PublicacionesEditorialesProgramas (ProgramaEditorialId);
END;
GO
