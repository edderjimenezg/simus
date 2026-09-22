/*
  SIMUS · Mercados Musicales: el proceso y sus ediciones

  QUE ES, Y POR QUE ES UN MODULO Y NO UNA FICHA. Un mercado musical es un proceso del Ecosistema al
  mismo nivel que un Festival, no una sección suya. quedó fijado: «no se debe plantear como una sección secundaria de Festivales ni como una
  simple ficha de información». Por eso tiene su propia tabla, su propio ciclo de vida y su propio
  circuito de revisión, y reutiliza —sin duplicar— el territorio, la organización responsable y los
  vocabularios que ya existen.

  LA HERENCIA SE REUTILIZA, NO SE COPIA. Prácticas musicales y territorios sonoros son las MISMAS
  tablas de catálogo que usan los festivales: un mercado congrega prácticas y ocurre en territorios
  igual que ellos, y dos catálogos con los mismos valores divergen en cuanto nadie los mira. El
  territorio son las mismas dos claves DIVIPOLA con el mismo CHECK de coherencia entre nivel y
  territorio, no una segunda lógica territorial.

  LOS TRES DEFECTOS DE `dbo.mercadosmusicales` QUE ESTA TABLA NO REPITE. La estructura heredada de la
  entrega de septiembre se leyó entera, columna a columna, y trae tres decisiones que aquí se
  corrigen a propósito:

  1. GUARDABA EL FESTIVAL ASOCIADO DOS VECES —`idfestivalasociado` con clave ajena y
     `nombrefestivalasociado` como texto suelto—, sin nada que obligara a que coincidieran. El
     resultado era un directorio público que enseñaba nombres de festivales que el sistema no sabía
     localizar. Aquí hay UNA columna, `FestivalId`, con clave ajena real: el nombre se lee del
     festival relacionado. «No debemos crear festivales duplicados ni permitir escribir manualmente
     el nombre de un festival para simular la relación.»
  2. METIA LA EDICION DENTRO DEL MERCADO, en cuatro columnas —`tieneedicionvigenteanoactual`,
     `estadoedicionanoactual` y las dos fechas—. Con ellas la edición de 2025 se pierde en cuanto se
     escribe la de 2026, porque no hay dónde guardarla. Aquí las ediciones son una tabla.
  3. DEJABA `alcance`, `modalidad` y `periodicidad` COMO TEXTO LIBRE, sin lista ni CHECK. La regla
     del proyecto es la contraria: todo campo con vocabulario controlado se captura con una lista.
     Alcance y modalidad pasan a ser dos catálogos; la periodicidad reutiliza la de Festivales.

  Y LO QUE NO SE COPIA PORQUE NO HACE FALTA: `numeroediciones` no existe. El número de ediciones se
  CUENTA a partir de la tabla de ediciones; lo derivable no se pregunta.

  LAS EDICIONES DE UN MERCADO NO PASAN POR REVISION INSTITUCIONAL. Decidido por la dirección del
  proyecto, y es la única diferencia real de ciclo de vida respecto de
  Festivales: el control de calidad está en la puerta de entrada del mercado, no en cada
  realización. Por eso aquí NO hay tablas de revisión de ediciones, y la edición sí conserva su
  estado operativo, su visibilidad y su auditoría.

  SIN CASCADAS. El proyecto no las admite y una prueba lo comprueba: borrar una organización no
  puede llevarse por delante sus mercados en silencio.

  ES ADITIVA. No toca ninguna tabla existente, no borra nada y no cambia ninguna columna.
*/

-- ── Los dos vocabularios propios del mercado ────────────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'AlcancesMercado' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.AlcancesMercado (
        IdAlcanceMercado int IDENTITY(1,1) NOT NULL,
        NombreAlcance nvarchar(120) NOT NULL,
        Slug nvarchar(120) NOT NULL,
        OrdenVisualizacion int NOT NULL CONSTRAINT DF_AlcancesMercado_Orden DEFAULT (0),
        CONSTRAINT PK_AlcancesMercado PRIMARY KEY (IdAlcanceMercado),
        CONSTRAINT UQ_AlcancesMercado_Slug UNIQUE (Slug)
    );
END;

INSERT INTO dbo.AlcancesMercado (NombreAlcance, Slug, OrdenVisualizacion)
SELECT v.NombreAlcance, v.Slug, v.OrdenVisualizacion
FROM (VALUES
    (N'Local', N'local', 1),
    (N'Regional', N'regional', 2),
    (N'Nacional', N'nacional', 3),
    (N'Internacional', N'internacional', 4)
) AS v(NombreAlcance, Slug, OrdenVisualizacion)
WHERE NOT EXISTS (SELECT 1 FROM dbo.AlcancesMercado a WHERE a.Slug = v.Slug);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'ModalidadesMercado' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.ModalidadesMercado (
        IdModalidadMercado int IDENTITY(1,1) NOT NULL,
        NombreModalidad nvarchar(120) NOT NULL,
        Slug nvarchar(120) NOT NULL,
        OrdenVisualizacion int NOT NULL CONSTRAINT DF_ModalidadesMercado_Orden DEFAULT (0),
        CONSTRAINT PK_ModalidadesMercado PRIMARY KEY (IdModalidadMercado),
        CONSTRAINT UQ_ModalidadesMercado_Slug UNIQUE (Slug)
    );
END;

INSERT INTO dbo.ModalidadesMercado (NombreModalidad, Slug, OrdenVisualizacion)
SELECT v.NombreModalidad, v.Slug, v.OrdenVisualizacion
FROM (VALUES
    (N'Presencial', N'presencial', 1),
    (N'Virtual', N'virtual', 2),
    (N'Mixta', N'mixta', 3)
) AS v(NombreModalidad, Slug, OrdenVisualizacion)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ModalidadesMercado m WHERE m.Slug = v.Slug);

-- ── El mercado ──────────────────────────────────────────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Mercados' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.Mercados (
        IdMercado int IDENTITY(1,1) NOT NULL,
        NombreMercado nvarchar(240) NOT NULL,
        Descripcion nvarchar(max) NULL,

        -- Vocabularios: listas, no texto libre.
        AlcanceMercadoId int NULL,
        ModalidadMercadoId int NULL,
        Periodicidad nvarchar(80) NULL,
        PeriodicidadDetalle nvarchar(240) NULL,

        -- Contacto propio del mercado. El de la organización vive en la organización.
        CorreoMercado nvarchar(180) NULL,
        TelefonoMercado nvarchar(80) NULL,
        SitioWebMercado nvarchar(500) NULL,
        InstagramMercado nvarchar(500) NULL,
        FacebookMercado nvarchar(500) NULL,
        OtroEnlaceMercado nvarchar(500) NULL,
        ObservacionesContacto nvarchar(1000) NULL,

        -- Territorio: las mismas dos claves DIVIPOLA y el mismo CHECK que un festival.
        NivelCobertura nvarchar(40) NOT NULL,
        CodigoDepartamento char(2) NULL,
        CodigoMunicipio char(5) NULL,
        LugarEspecifico nvarchar(300) NULL,

        /*
          LA RELACION CON EL FESTIVAL, QUE ES LO UNICO NUEVO DE ESTE MODULO.

          SON DOS COLUMNAS Y NO UNA PORQUE SON DOS PREGUNTAS. `SeRealizaEnElMarcoDeUnFestival` es lo
          que se le pregunta a la persona; `FestivalId` es la respuesta. Sin la primera, un mercado
          independiente y un mercado cuyo festival aún no se ha elegido serían indistinguibles: los
          dos tendrían `FestivalId` nulo.

          EL CHECK LAS ATA. Decir que sí y no elegir festival deja el dato a medias; decir que no y
          dejar un festival guardado es una relación que la ficha no enseña y que nadie sabe que
          existe.

          LA PERTENENCIA A LA MISMA ORGANIZACION NO SE COMPRUEBA AQUI, y es deliberado: es una regla
          entre dos filas de dos tablas distintas, que un CHECK no puede expresar sin una función
          escalar que se ejecutaría en cada escritura. La comprueba el servidor al guardar, y una
          prueba lo fija. El ESTADO del festival no se comprueba en ningún sitio: puede estar en
          borrador, en revisión o publicado, porque la condición es existir y pertenecer.
        */
        SeRealizaEnElMarcoDeUnFestival bit NOT NULL CONSTRAINT DF_Mercados_EnMarcoDeFestival DEFAULT (0),
        FestivalId int NULL,

        -- Procedencia, ciclo de vida y auditoría, como en cualquier registro del Ecosistema.
        OrganizacionPrincipalId int NOT NULL,
        EstadoRegistro nvarchar(80) NOT NULL CONSTRAINT DF_Mercados_EstadoRegistro DEFAULT (N'borrador'),
        Activo bit NOT NULL CONSTRAINT DF_Mercados_Activo DEFAULT (1),
        IdUsuarioCreador int NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_Mercados_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion datetime2(0) NULL,
        FechaEnvioARevision datetime2(0) NULL,
        FechaPublicacion datetime2(0) NULL,
        FechaArchivado datetime2(0) NULL,

        CONSTRAINT PK_Mercados PRIMARY KEY (IdMercado),
        CONSTRAINT FK_Mercados_Alcance FOREIGN KEY (AlcanceMercadoId) REFERENCES dbo.AlcancesMercado(IdAlcanceMercado),
        CONSTRAINT FK_Mercados_Modalidad FOREIGN KEY (ModalidadMercadoId) REFERENCES dbo.ModalidadesMercado(IdModalidadMercado),
        CONSTRAINT FK_Mercados_Festival FOREIGN KEY (FestivalId) REFERENCES dbo.Festivales(IdFestival),
        CONSTRAINT FK_Mercados_Organizacion FOREIGN KEY (OrganizacionPrincipalId) REFERENCES dbo.Entidades(IdEntidad),
        CONSTRAINT CK_Mercados_NivelCobertura CHECK (
            NivelCobertura IN (N'nacional', N'departamental', N'municipal')
            AND (
                (NivelCobertura = N'nacional' AND CodigoDepartamento IS NULL AND CodigoMunicipio IS NULL)
                OR (NivelCobertura = N'departamental' AND CodigoDepartamento IS NOT NULL AND CodigoMunicipio IS NULL)
                OR (NivelCobertura = N'municipal' AND CodigoDepartamento IS NOT NULL AND CodigoMunicipio IS NOT NULL)
            )
        ),
        CONSTRAINT CK_Mercados_EstadoRegistro CHECK (
            EstadoRegistro IN (N'borrador', N'en_revision', N'ajustes_solicitados', N'publicado', N'archivado')
        ),
        CONSTRAINT CK_Mercados_FestivalCoherente CHECK (
            (SeRealizaEnElMarcoDeUnFestival = 1 AND FestivalId IS NOT NULL)
            OR (SeRealizaEnElMarcoDeUnFestival = 0 AND FestivalId IS NULL)
        )
    );

    CREATE INDEX IX_Mercados_Organizacion_Estado ON dbo.Mercados (OrganizacionPrincipalId, EstadoRegistro);
    CREATE INDEX IX_Mercados_Territorio ON dbo.Mercados (CodigoDepartamento, CodigoMunicipio);
    CREATE INDEX IX_Mercados_Festival ON dbo.Mercados (FestivalId);
END;

-- ── Lo que el mercado congrega, con los catálogos que ya existen ────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'MercadosPracticasMusicales' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.MercadosPracticasMusicales (
        IdMercadoPracticaMusical int IDENTITY(1,1) NOT NULL,
        MercadoId int NOT NULL,
        PracticaMusicalId int NOT NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_MercadosPracticas_Fecha DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_MercadosPracticasMusicales PRIMARY KEY (IdMercadoPracticaMusical),
        CONSTRAINT UQ_MercadosPracticas_Par UNIQUE (MercadoId, PracticaMusicalId),
        CONSTRAINT FK_MercadosPracticas_Mercado FOREIGN KEY (MercadoId) REFERENCES dbo.Mercados(IdMercado),
        CONSTRAINT FK_MercadosPracticas_Practica FOREIGN KEY (PracticaMusicalId) REFERENCES dbo.PracticasMusicales(IdPracticaMusical)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'MercadosTerritoriosSonoros' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.MercadosTerritoriosSonoros (
        IdMercadoTerritorioSonoro int IDENTITY(1,1) NOT NULL,
        MercadoId int NOT NULL,
        TerritorioSonoroId int NOT NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_MercadosTerritorios_Fecha DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_MercadosTerritoriosSonoros PRIMARY KEY (IdMercadoTerritorioSonoro),
        CONSTRAINT UQ_MercadosTerritorios_Par UNIQUE (MercadoId, TerritorioSonoroId),
        CONSTRAINT FK_MercadosTerritorios_Mercado FOREIGN KEY (MercadoId) REFERENCES dbo.Mercados(IdMercado),
        CONSTRAINT FK_MercadosTerritorios_Territorio FOREIGN KEY (TerritorioSonoroId) REFERENCES dbo.TerritoriosSonoros(IdTerritorioSonoro)
    );
END;

-- ── Las ediciones ───────────────────────────────────────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'EdicionesMercado' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.EdicionesMercado (
        IdEdicionMercado int IDENTITY(1,1) NOT NULL,
        MercadoId int NOT NULL,
        Anio int NOT NULL,
        NumeroEdicion int NULL,
        Nombre nvarchar(240) NULL,
        Descripcion nvarchar(max) NULL,
        FechaInicio date NULL,
        FechaFin date NULL,

        -- Dónde ocurrió ESA edición, que puede no ser donde ocurre el mercado de ordinario.
        CodigoDepartamento char(2) NULL,
        CodigoMunicipio char(5) NULL,
        LugarEspecifico nvarchar(300) NULL,

        /*
          DOS ESTADOS, Y NO SON LO MISMO. `Estado` es el ciclo real del acontecimiento —se prepara,
          está programado, ya ocurrió, se canceló—; `EstadoVisibilidad` es si se enseña o no. Un
          mercado cancelado que SIGUE publicado es información legítima, y con un solo eje habría
          que elegir entre decir que se canceló y decir que se ve.

          NO HAY `EstadoRegistro` NI CIRCUITO DE REVISION: las ediciones de un mercado no pasan por
          revisión institucional, por decisión. Quien publica una ya
          demostró, al publicar el mercado, que su proceso está validado.
        */
        Estado nvarchar(40) NOT NULL CONSTRAINT DF_EdicionesMercado_Estado DEFAULT (N'en_preparacion'),
        EstadoVisibilidad nvarchar(40) NOT NULL CONSTRAINT DF_EdicionesMercado_Visibilidad DEFAULT (N'borrador'),

        IdUsuarioCreador int NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_EdicionesMercado_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion datetime2(0) NULL,
        FechaPublicacion datetime2(0) NULL,

        CONSTRAINT PK_EdicionesMercado PRIMARY KEY (IdEdicionMercado),
        CONSTRAINT UQ_EdicionesMercado_MercadoAnio UNIQUE (MercadoId, Anio),
        CONSTRAINT FK_EdicionesMercado_Mercado FOREIGN KEY (MercadoId) REFERENCES dbo.Mercados(IdMercado),
        CONSTRAINT CK_EdicionesMercado_Estado CHECK (
            Estado IN (N'en_preparacion', N'programada', N'realizada', N'cancelada')
        ),
        CONSTRAINT CK_EdicionesMercado_Visibilidad CHECK (
            EstadoVisibilidad IN (N'borrador', N'publicado', N'archivado')
        ),
        -- Fin nunca antes que inicio, cuando las dos existen. Es la misma regla que ya vigila las
        -- fechas de la edición heredada, y sin ella el error saltaría como un 500 sin explicación.
        CONSTRAINT CK_EdicionesMercado_Fechas CHECK (
            FechaInicio IS NULL OR FechaFin IS NULL OR FechaFin >= FechaInicio
        ),
        CONSTRAINT CK_EdicionesMercado_Anio CHECK (Anio BETWEEN 1900 AND 2200)
    );

    CREATE INDEX IX_EdicionesMercado_Mercado ON dbo.EdicionesMercado (MercadoId, Anio DESC);
END;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'EdicionesMercadoPracticasMusicales' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.EdicionesMercadoPracticasMusicales (
        IdEdicionMercadoPracticaMusical int IDENTITY(1,1) NOT NULL,
        EdicionMercadoId int NOT NULL,
        PracticaMusicalId int NOT NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_EdicionMercadoPracticas_Fecha DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_EdicionesMercadoPracticasMusicales PRIMARY KEY (IdEdicionMercadoPracticaMusical),
        CONSTRAINT UQ_EdicionMercadoPracticas_Par UNIQUE (EdicionMercadoId, PracticaMusicalId),
        CONSTRAINT FK_EdicionMercadoPracticas_Edicion FOREIGN KEY (EdicionMercadoId) REFERENCES dbo.EdicionesMercado(IdEdicionMercado),
        CONSTRAINT FK_EdicionMercadoPracticas_Practica FOREIGN KEY (PracticaMusicalId) REFERENCES dbo.PracticasMusicales(IdPracticaMusical)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'EdicionesMercadoTerritoriosSonoros' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.EdicionesMercadoTerritoriosSonoros (
        IdEdicionMercadoTerritorioSonoro int IDENTITY(1,1) NOT NULL,
        EdicionMercadoId int NOT NULL,
        TerritorioSonoroId int NOT NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_EdicionMercadoTerritorios_Fecha DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_EdicionesMercadoTerritoriosSonoros PRIMARY KEY (IdEdicionMercadoTerritorioSonoro),
        CONSTRAINT UQ_EdicionMercadoTerritorios_Par UNIQUE (EdicionMercadoId, TerritorioSonoroId),
        CONSTRAINT FK_EdicionMercadoTerritorios_Edicion FOREIGN KEY (EdicionMercadoId) REFERENCES dbo.EdicionesMercado(IdEdicionMercado),
        CONSTRAINT FK_EdicionMercadoTerritorios_Territorio FOREIGN KEY (TerritorioSonoroId) REFERENCES dbo.TerritoriosSonoros(IdTerritorioSonoro)
    );
END;

-- ── El territorio apunta a DIVIPOLA, como todo lo demás ─────────────────────────────────────────

/*
  DIVIPOLA ES LA FUENTE UNICA DEL TERRITORIO, y eso se sostiene con una foránea y no con buena
  voluntad. Una prueba del proyecto lo comprueba al revés de como suele hacerse: en vez de listar
  las tablas que deben tenerla, busca CUALQUIER tabla que guarde el par departamento/municipio y no
  la tenga. Ese aserto no caduca —no fija un número, fija que no sobre ninguna— y fue el que
  descubrió que estas dos faltaban.

  LA FORANEA ES COMPUESTA Y LAS COLUMNAS SON ANULABLES, y las dos cosas conviven: SQL Server no
  comprueba una foránea compuesta cuando alguna de sus columnas es NULL, que es justo lo que hace
  falta aquí —en cobertura departamental el municipio es NULL, y en nacional lo son las dos—.

  SIN CASCADA, como todas las del proyecto.
*/
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Mercados_Divipola')
BEGIN
    ALTER TABLE dbo.Mercados ADD CONSTRAINT FK_Mercados_Divipola
        FOREIGN KEY (CodigoDepartamento, CodigoMunicipio)
        REFERENCES dbo.Divipola (CodigoDepartamento, CodigoMunicipio);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_EdicionesMercado_Divipola')
BEGIN
    ALTER TABLE dbo.EdicionesMercado ADD CONSTRAINT FK_EdicionesMercado_Divipola
        FOREIGN KEY (CodigoDepartamento, CodigoMunicipio)
        REFERENCES dbo.Divipola (CodigoDepartamento, CodigoMunicipio);
END;

-- ── El módulo nuevo, para las cuentas que ya existen ────────────────────────────────────────────

/*
  LAS CUENTAS QUE YA EXISTEN CONSERVAN TODOS LOS MODULOS, y por eso un módulo nuevo hay que
  concedérselo explícitamente: la tabla guarda una fila por par, así que nacer no basta. El dueño
  quedó definido así: «con las cuentas que existen de momento deja activado todos los módulos
  y simplemente se las desactivaremos manualmente después».
*/
INSERT INTO dbo.ModulosPorCuenta (IdUsuario, CodigoModulo, FechaOtorgado)
SELECT u.IdUsuario, N'mercados', SYSUTCDATETIME()
FROM dbo.Usuarios u
WHERE EXISTS (
        SELECT 1 FROM dbo.UsuariosRoles ur
        JOIN dbo.Roles r ON r.IdRol = ur.IdRol
        WHERE ur.IdUsuario = u.IdUsuario AND r.NombreRol IN (N'webmaster', N'gestor_interno')
      )
  AND NOT EXISTS (
        SELECT 1 FROM dbo.ModulosPorCuenta m
        WHERE m.IdUsuario = u.IdUsuario AND m.CodigoModulo = N'mercados'
      );
