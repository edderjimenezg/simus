/*
    PNMC - Los objetos que hasta hoy solo creaba el arranque del API.

    EL PROBLEMA. Trece tablas y cinco columnas de
    la base no las declara ningun guion de schema/: las crea DatabaseBootstrapper.cs
    al arrancar el API, con DDL embebido en C#, y SOLO cuando Database:EnsureSupportTables
    esta en true. appsettings.Production.json lo pone en false —con razon: un API de
    produccion no debe tener permisos de DDL—, con lo que sobre una base construida con
    estos guiones y ese perfil:

      - el primer login institucional muere con «Invalid column name 'Telefono'»;
      - el CMS de textos arranca sin ContenidoWeb, ContenidoWebHistorial ni EquipoWeb;
      - el circuito de versiones y propuestas de festival no tiene ni una tabla;
      - la verificacion de correo de las personas externas no tiene donde escribir.

    Y nada lo delataba: con ContinueOnStartupFailure en true el arranque lo reducia a un
    warning, y las sondas de salud respondian Healthy. Las pruebas tampoco lo veian: la
    suite corre sobre SQLite —fabricada desde el propio modelo de EF— y el arnes de SQL
    Server arranca el API antes de la primera prueba, con lo que el bootstrapper reparaba
    la divergencia justo a tiempo. El arnes escondia exactamente el defecto que habia que
    medir. Desde hoy lo mide ParidadEsquemaSinArranqueTests.

    LO QUE HACE ESTE GUION. Mueve ese DDL a la via gobernada. Es literalmente el mismo
    T-SQL que DatabaseBootstrapper.cs ejecutaba —extraido del archivo, no reescrito—, para
    que una base hecha solo con guiones sea identica a la base. Cada sentencia conserva
    su guarda de idempotencia (OBJECT_ID, COL_LENGTH, sys.indexes, sys.foreign_keys), asi
    que sobre la base, donde ya existe todo, no hace nada.

    UNA TRAMPA QUE LAS GUARDAS SORTEAN. COL_LENGTH devuelve NULL tambien cuando la TABLA no
    existe, no solo cuando falta la columna: un ALTER TABLE ADD guardado solo por COL_LENGTH
    se ejecuta y revienta en cuanto la tabla deja de crearse. Por eso cada remiendo de
    columna va despues del CREATE de su tabla, o sobre tablas que V20260519_02 y
    V20260521_01 ya garantizan.

    QUE HACER CON EL BOOTSTRAPPER. Nada todavia: sigue corriendo en local, donde
    EnsureSupportTables=true, y es idempotente. El siguiente paso —decision de A04— es que
    el API deje de ejecutar DDL y un migrador con historial (DbUp o Flyway; el DDL canonico
    ya es T-SQL) aplique schema/ en cada entorno. Este guion es lo que hace posible ese paso.
*/

/*
    QUOTED_IDENTIFIER NO ES ADORNO AQUI. Este guion crea tres indices UNIQUE **filtrados**
    (con WHERE), y SQL Server los rechaza —Msg 1934— si la sesion no tiene QUOTED_IDENTIFIER
    en ON. Las conexiones de .NET lo traen en ON por omision, asi que el bootstrapper y el
    arnes de pruebas nunca lo notaron; `scripts/seed-local-db.sh` aplicaba los guiones tuberia
    adentro con `sqlcmd ... -b` y SIN `-I`, donde esta OFF, de modo que la siembra local
    abortaba en este fichero. El mismo tropiezo lo tuvo antes V20260823_01, que ya lo declara.

    ESE GUION PASA `-I` y este SET sigue aqui a proposito. Hicieron
    falta las dos correcciones porque el problema tiene dos caras: crear un indice filtrado
    (esta, la del guion) y ESCRIBIR en una tabla que ya lo tiene (la del que ejecuta, que
    ningun SET de aqui puede arreglar porque quien falla es la semilla de detras). Este SET
    cubre ademas a quien aplique el fichero suelto a mano, sin pasar por el guion.

    La opcion se declara AQUI y no en quien invoca, a proposito: un guion de esquema no debe
    depender de las opciones de sesion de quien lo lance. Este fichero no lleva GO, asi que
    con declararlo una vez al principio queda cubierto entero.
*/
SET QUOTED_IDENTIFIER ON;

IF COL_LENGTH(N'[Usuarios]', N'CanalAcceso') IS NULL
BEGIN
    ALTER TABLE [Usuarios]
    ADD [CanalAcceso] nvarchar(40) NOT NULL
        CONSTRAINT [DF_Usuarios_CanalAcceso] DEFAULT (N'interno') WITH VALUES;
END;

IF COL_LENGTH(N'[Usuarios]', N'TipoPerfil') IS NULL
BEGIN
    ALTER TABLE [Usuarios]
    ADD [TipoPerfil] nvarchar(80) NULL;
END;

IF COL_LENGTH(N'[Usuarios]', N'Telefono') IS NULL
BEGIN
    ALTER TABLE [Usuarios]
    ADD [Telefono] nvarchar(80) NULL;
END;

IF OBJECT_ID(N'[UsuariosCodigosVerificacion]', N'U') IS NULL
BEGIN
    CREATE TABLE [UsuariosCodigosVerificacion] (
        [IdUsuarioCodigoVerificacion] int IDENTITY(1,1) NOT NULL,
        [IdUsuario] int NOT NULL,
        [Proposito] nvarchar(80) NOT NULL,
        [Codigo] nvarchar(120) NOT NULL,
        [FechaExpiracion] datetime2(0) NOT NULL,
        [FechaConsumo] datetime2(0) NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_UsuariosCodigosVerificacion_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_UsuariosCodigosVerificacion] PRIMARY KEY ([IdUsuarioCodigoVerificacion]),
        CONSTRAINT [FK_UsuariosCodigosVerificacion_Usuarios] FOREIGN KEY ([IdUsuario]) REFERENCES [Usuarios] ([IdUsuario])
    );

    CREATE INDEX [IX_UsuariosCodigosVerificacion_Usuario]
        ON [UsuariosCodigosVerificacion] ([IdUsuario], [Proposito], [FechaExpiracion] DESC);
END;

IF OBJECT_ID(N'[ContenidoWeb]', N'U') IS NULL
BEGIN
    CREATE TABLE [ContenidoWeb] (
        [IdContenidoWeb] int IDENTITY(1,1) NOT NULL,
        [Clave] nvarchar(160) NOT NULL,
        [GrupoId] nvarchar(120) NOT NULL,
        [GrupoEtiqueta] nvarchar(160) NOT NULL,
        [Seccion] nvarchar(120) NOT NULL,
        [Etiqueta] nvarchar(240) NOT NULL,
        [LimiteCaracteres] int NOT NULL CONSTRAINT [DF_ContenidoWeb_LimiteCaracteres] DEFAULT (300),
        [Borrador] nvarchar(max) NOT NULL CONSTRAINT [DF_ContenidoWeb_Borrador] DEFAULT (N''),
        [Publicado] nvarchar(max) NULL,
        [Version] int NOT NULL CONSTRAINT [DF_ContenidoWeb_Version] DEFAULT (1),
        [ActualizadoPor] nvarchar(160) NOT NULL CONSTRAINT [DF_ContenidoWeb_ActualizadoPor] DEFAULT (N'Sistema'),
        [FechaActualizacion] datetime2(0) NOT NULL CONSTRAINT [DF_ContenidoWeb_FechaActualizacion] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_ContenidoWeb] PRIMARY KEY ([IdContenidoWeb]),
        CONSTRAINT [UQ_ContenidoWeb_Clave] UNIQUE ([Clave])
    );

    CREATE INDEX [IX_ContenidoWeb_GrupoId] ON [ContenidoWeb] ([GrupoId]);
END;

IF COL_LENGTH(N'[ContenidoWeb]', N'FechaRetiro') IS NULL
BEGIN
    ALTER TABLE [ContenidoWeb] ADD [FechaRetiro] datetime2(0) NULL;
END;

IF OBJECT_ID(N'[EquipoWeb]', N'U') IS NULL
BEGIN
    CREATE TABLE [EquipoWeb] (
        -- Sin IDENTITY y con CHECK: la tabla guarda una sola nomina, y
        -- que sea asi no depende de que el codigo se acuerde.
        [IdEquipoWeb] int NOT NULL,
        [Borrador] nvarchar(max) NOT NULL CONSTRAINT [DF_EquipoWeb_Borrador] DEFAULT (N'[]'),
        [Publicado] nvarchar(max) NULL,
        [Version] int NOT NULL CONSTRAINT [DF_EquipoWeb_Version] DEFAULT (1),
        [ActualizadoPor] nvarchar(160) NOT NULL CONSTRAINT [DF_EquipoWeb_ActualizadoPor] DEFAULT (N'Sistema'),
        [FechaActualizacion] datetime2(0) NOT NULL CONSTRAINT [DF_EquipoWeb_FechaActualizacion] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_EquipoWeb] PRIMARY KEY ([IdEquipoWeb]),
        CONSTRAINT [CK_EquipoWeb_Unica] CHECK ([IdEquipoWeb] = 1) -- WebTeamRow.SingletonId: la fila unica de la nomina
    );
END;

IF OBJECT_ID(N'[ContenidoWebHistorial]', N'U') IS NULL
BEGIN
    CREATE TABLE [ContenidoWebHistorial] (
        [IdContenidoWebHistorial] bigint IDENTITY(1,1) NOT NULL,
        [Clave] nvarchar(160) NOT NULL,
        [Accion] nvarchar(40) NOT NULL,
        -- Nulo cuando la accion no reescribe el texto: retirar quita
        -- del sitio, no toca el borrador.
        [Valor] nvarchar(max) NULL,
        [Usuario] nvarchar(160) NOT NULL,
        [Fecha] datetime2(0) NOT NULL CONSTRAINT [DF_ContenidoWebHistorial_Fecha] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_ContenidoWebHistorial] PRIMARY KEY ([IdContenidoWebHistorial])
    );

    -- La consulta del panel es «las N ultimas de esta clave».
    CREATE INDEX [IX_ContenidoWebHistorial_Clave_Fecha]
        ON [ContenidoWebHistorial] ([Clave], [Fecha] DESC);
END;

IF OBJECT_ID(N'[CatalogoEditorial]', N'U') IS NULL
BEGIN
    CREATE TABLE [CatalogoEditorial] (
        [IdRecursoEditorial] int IDENTITY(1,1) NOT NULL,
        [CodigoRecurso] nvarchar(64) NOT NULL,
        [Titulo] nvarchar(500) NOT NULL,
        -- Anio y Paginas siguen siendo texto acotado, NO enteros.
        -- El tipo correcto es numerico, pero la conversion esta
        -- bloqueada fuera de este carril: ver la nota BLOQUEADO
        -- al final de este metodo.
        [Anio] nvarchar(20) NULL,
        [SeccionPrincipal] nvarchar(200) NULL,
        [RutaSeccion] nvarchar(500) NULL,
        [TipoPublicacion] nvarchar(120) NULL,
        [PracticaMusical] nvarchar(160) NULL,
        [Categoria] nvarchar(160) NULL,
        [Subcategoria] nvarchar(160) NULL,
        [Autor] nvarchar(500) NULL,
        [AutorCorporativo] nvarchar(500) NULL,
        [CreditosAdicionales] nvarchar(max) NULL,
        -- ISBN-13 y ISMN con guiones ocupan 17 caracteres.
        [ISBN] nvarchar(20) NULL,
        [ISMN] nvarchar(20) NULL,
        [TamanoFormato] nvarchar(120) NULL,
        [Paginas] nvarchar(20) NULL,
        -- Duracion: formato sin decidir (ver nota DISCUTIBLE abajo).
        [Duracion] nvarchar(40) NULL,
        [AmbitoRegional] nvarchar(160) NULL,
        [UbicacionPublicacion] nvarchar(300) NULL,
        [Url] nvarchar(1000) NULL,
        [PalabrasClave] nvarchar(1000) NULL,
        [Resumen] nvarchar(max) NULL,
        [CamposAdicionales] nvarchar(max) NULL,
        [DiapositivaOrigen] nvarchar(50) NULL,
        [ArchivoMiniatura] nvarchar(500) NULL,
        [TextoPortada] nvarchar(max) NULL,
        [TextoFuenteCompleto] nvarchar(max) NULL,
        [OrdenFuente] int NOT NULL CONSTRAINT [DF_CatalogoEditorial_OrdenFuente] DEFAULT (0),
        [Activo] bit NOT NULL CONSTRAINT [DF_CatalogoEditorial_Activo] DEFAULT (1),
        [FechaImportacion] datetime2(0) NOT NULL CONSTRAINT [DF_CatalogoEditorial_FechaImportacion] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_CatalogoEditorial] PRIMARY KEY ([IdRecursoEditorial]),
        CONSTRAINT [UQ_CatalogoEditorial_CodigoRecurso] UNIQUE ([CodigoRecurso])
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CatalogoEditorial_OrdenFuente' AND object_id = OBJECT_ID(N'[CatalogoEditorial]'))
BEGIN
    CREATE INDEX [IX_CatalogoEditorial_OrdenFuente] ON [CatalogoEditorial] ([OrdenFuente]);
END;

IF OBJECT_ID(N'[CatalogoEditorial]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM [CatalogoEditorial])
BEGIN
    ALTER TABLE [CatalogoEditorial] ALTER COLUMN [Anio] nvarchar(20) NULL;
    ALTER TABLE [CatalogoEditorial] ALTER COLUMN [SeccionPrincipal] nvarchar(200) NULL;
    ALTER TABLE [CatalogoEditorial] ALTER COLUMN [RutaSeccion] nvarchar(500) NULL;
    ALTER TABLE [CatalogoEditorial] ALTER COLUMN [TipoPublicacion] nvarchar(120) NULL;
    ALTER TABLE [CatalogoEditorial] ALTER COLUMN [PracticaMusical] nvarchar(160) NULL;
    ALTER TABLE [CatalogoEditorial] ALTER COLUMN [Categoria] nvarchar(160) NULL;
    ALTER TABLE [CatalogoEditorial] ALTER COLUMN [Subcategoria] nvarchar(160) NULL;
    ALTER TABLE [CatalogoEditorial] ALTER COLUMN [Autor] nvarchar(500) NULL;
    ALTER TABLE [CatalogoEditorial] ALTER COLUMN [AutorCorporativo] nvarchar(500) NULL;
    ALTER TABLE [CatalogoEditorial] ALTER COLUMN [ISBN] nvarchar(20) NULL;
    ALTER TABLE [CatalogoEditorial] ALTER COLUMN [ISMN] nvarchar(20) NULL;
    ALTER TABLE [CatalogoEditorial] ALTER COLUMN [TamanoFormato] nvarchar(120) NULL;
    ALTER TABLE [CatalogoEditorial] ALTER COLUMN [Paginas] nvarchar(20) NULL;
    ALTER TABLE [CatalogoEditorial] ALTER COLUMN [Duracion] nvarchar(40) NULL;
    ALTER TABLE [CatalogoEditorial] ALTER COLUMN [AmbitoRegional] nvarchar(160) NULL;
    ALTER TABLE [CatalogoEditorial] ALTER COLUMN [UbicacionPublicacion] nvarchar(300) NULL;
    ALTER TABLE [CatalogoEditorial] ALTER COLUMN [Url] nvarchar(1000) NULL;
    -- PalabrasClave no estaba en la lista del bloque anterior. Si
    -- se omite aqui, una tabla creada de cero queda en
    -- nvarchar(1000) y una ya existente se queda en nvarchar(max):
    -- la misma divergencia entre rutas que corrige esta tanda.
    ALTER TABLE [CatalogoEditorial] ALTER COLUMN [PalabrasClave] nvarchar(1000) NULL;
END;

IF COL_LENGTH(N'[Entidades]', N'NumeroIdentificacion') IS NULL
BEGIN
    ALTER TABLE [Entidades] ADD [NumeroIdentificacion] nvarchar(60) NULL;
END;

-- Unico filtrado sobre el numero de identificacion. SQL dinamico porque la columna puede
-- haberse agregado en este mismo lote y el enlace estatico fallaria al compilar (Msg 207).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_Entidades_NumeroIdentificacion' AND object_id = OBJECT_ID(N'[Entidades]'))
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UQ_Entidades_NumeroIdentificacion]
        ON [Entidades] ([NumeroIdentificacion])
        WHERE [NumeroIdentificacion] IS NOT NULL;');
END;

IF COL_LENGTH(N'[Festivales]', N'OrganizacionPrincipalId') IS NULL
BEGIN
    ALTER TABLE [Festivales] ADD [OrganizacionPrincipalId] int NULL;
END;

IF COL_LENGTH(N'[Festivales]', N'Periodicidad') IS NULL
BEGIN
    ALTER TABLE [Festivales] ADD [Periodicidad] nvarchar(80) NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Festivales_OrganizacionPrincipal')
BEGIN
    ALTER TABLE [Festivales] ADD CONSTRAINT [FK_Festivales_OrganizacionPrincipal]
        FOREIGN KEY ([OrganizacionPrincipalId]) REFERENCES [Entidades] ([IdEntidad]);
END;

-- Indice de la bandeja de una organizacion (sus Festivales por estado). SQL dinamico por la
-- misma razon: OrganizacionPrincipalId puede nacer en este lote.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Festivales_OrganizacionPrincipalId_EstadoRegistro' AND object_id = OBJECT_ID(N'[Festivales]'))
BEGIN
    EXEC(N'CREATE INDEX [IX_Festivales_OrganizacionPrincipalId_EstadoRegistro]
        ON [Festivales] ([OrganizacionPrincipalId], [EstadoRegistro]);');
END;

IF OBJECT_ID(N'[FestivalesPracticasMusicales]', N'U') IS NULL
BEGIN
    CREATE TABLE [FestivalesPracticasMusicales] (
        [IdFestivalPracticaMusical] int IDENTITY(1,1) NOT NULL,
        [FestivalId] int NOT NULL,
        [PracticaMusicalId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_FestivalesPracticasMusicales_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_FestivalesPracticasMusicales] PRIMARY KEY ([IdFestivalPracticaMusical]),
        CONSTRAINT [FK_FestivalesPracticasMusicales_Festival] FOREIGN KEY ([FestivalId]) REFERENCES [Festivales] ([IdFestival]),
        CONSTRAINT [FK_FestivalesPracticasMusicales_Practica] FOREIGN KEY ([PracticaMusicalId]) REFERENCES [PracticasMusicales] ([IdPracticaMusical]),
        CONSTRAINT [UQ_FestivalesPracticasMusicales_Festival_Practica] UNIQUE ([FestivalId], [PracticaMusicalId])
    );
END;

IF OBJECT_ID(N'[FestivalesTerritoriosSonoros]', N'U') IS NULL
BEGIN
    CREATE TABLE [FestivalesTerritoriosSonoros] (
        [IdFestivalTerritorioSonoro] int IDENTITY(1,1) NOT NULL,
        [FestivalId] int NOT NULL,
        [TerritorioSonoroId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_FestivalesTerritoriosSonoros_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_FestivalesTerritoriosSonoros] PRIMARY KEY ([IdFestivalTerritorioSonoro]),
        CONSTRAINT [FK_FestivalesTerritoriosSonoros_Festival] FOREIGN KEY ([FestivalId]) REFERENCES [Festivales] ([IdFestival]),
        CONSTRAINT [FK_FestivalesTerritoriosSonoros_TerritorioSonoro] FOREIGN KEY ([TerritorioSonoroId]) REFERENCES [TerritoriosSonoros] ([IdTerritorioSonoro]),
        CONSTRAINT [UQ_FestivalesTerritoriosSonoros_Festival_TerritorioSonoro] UNIQUE ([FestivalId], [TerritorioSonoroId])
    );
END;

IF OBJECT_ID(N'[VersionesFestival]', N'U') IS NULL
BEGIN
    CREATE TABLE [VersionesFestival] (
        [IdVersionFestival] int IDENTITY(1,1) NOT NULL,
        [FestivalOrigenId] int NOT NULL,
        [NumeroVersion] int NOT NULL,
        [EsVigente] bit NOT NULL,
        [Nombre] nvarchar(240) NOT NULL,
        [Descripcion] nvarchar(1200) NULL,
        [NivelCobertura] nvarchar(40) NOT NULL,
        [CodigoDepartamento] nvarchar(20) NULL,
        [CodigoMunicipio] nvarchar(20) NULL,
        [Periodicidad] nvarchar(80) NULL,
        [CorreoContacto] nvarchar(180) NULL,
        [FechaPublicacion] datetime2(0) NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_VersionesFestival_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_VersionesFestival] PRIMARY KEY ([IdVersionFestival]),
        CONSTRAINT [FK_VersionesFestival_Festival] FOREIGN KEY ([FestivalOrigenId]) REFERENCES [Festivales] ([IdFestival]),
        CONSTRAINT [UQ_VersionesFestival_Festival_Numero] UNIQUE ([FestivalOrigenId], [NumeroVersion])
    );
END;

-- Una sola version vigente por Festival. Indice filtrado: el bootstrapper lo creaba y el
-- guion del 23 ago lo omitio en su primera version (lo detecto la documentacion del modulo,
-- no la prueba de paridad, que entonces solo comparaba tablas y columnas).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_VersionesFestival_Festival_Vigente' AND object_id = OBJECT_ID(N'[VersionesFestival]'))
    CREATE UNIQUE INDEX [UX_VersionesFestival_Festival_Vigente]
        ON [VersionesFestival] ([FestivalOrigenId]) WHERE [EsVigente] = 1;

IF OBJECT_ID(N'[VersionesFestivalPracticasMusicales]', N'U') IS NULL
BEGIN
    CREATE TABLE [VersionesFestivalPracticasMusicales] (
        [IdVersionFestivalPracticaMusical] int IDENTITY(1,1) NOT NULL,
        [VersionFestivalId] int NOT NULL,
        [PracticaMusicalId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_VersionesFestivalPracticas_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_VersionesFestivalPracticasMusicales] PRIMARY KEY ([IdVersionFestivalPracticaMusical]),
        CONSTRAINT [FK_VersionesFestivalPracticas_Version] FOREIGN KEY ([VersionFestivalId]) REFERENCES [VersionesFestival] ([IdVersionFestival]),
        CONSTRAINT [FK_VersionesFestivalPracticas_Practica] FOREIGN KEY ([PracticaMusicalId]) REFERENCES [PracticasMusicales] ([IdPracticaMusical]),
        CONSTRAINT [UQ_VersionesFestivalPracticas] UNIQUE ([VersionFestivalId], [PracticaMusicalId])
    );
END;

IF OBJECT_ID(N'[VersionesFestivalTerritoriosSonoros]', N'U') IS NULL
BEGIN
    CREATE TABLE [VersionesFestivalTerritoriosSonoros] (
        [IdVersionFestivalTerritorioSonoro] int IDENTITY(1,1) NOT NULL,
        [VersionFestivalId] int NOT NULL,
        [TerritorioSonoroId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_VersionesFestivalTerritorios_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_VersionesFestivalTerritoriosSonoros] PRIMARY KEY ([IdVersionFestivalTerritorioSonoro]),
        CONSTRAINT [FK_VersionesFestivalTerritorios_Version] FOREIGN KEY ([VersionFestivalId]) REFERENCES [VersionesFestival] ([IdVersionFestival]),
        CONSTRAINT [FK_VersionesFestivalTerritorios_Territorio] FOREIGN KEY ([TerritorioSonoroId]) REFERENCES [TerritoriosSonoros] ([IdTerritorioSonoro]),
        CONSTRAINT [UQ_VersionesFestivalTerritorios] UNIQUE ([VersionFestivalId], [TerritorioSonoroId])
    );
END;

IF OBJECT_ID(N'[PropuestasCambioFestival]', N'U') IS NULL
BEGIN
    CREATE TABLE [PropuestasCambioFestival] (
        [IdPropuestaCambioFestival] int IDENTITY(1,1) NOT NULL,
        [FestivalOrigenId] int NOT NULL,
        [VersionOrigenId] int NOT NULL,
        [VersionNuevaId] int NULL,
        [OrganizacionId] int NOT NULL,
        [PersonaProponenteId] int NOT NULL,
        [Estado] nvarchar(80) NOT NULL,
        [Activa] bit NOT NULL,
        [Nombre] nvarchar(240) NOT NULL,
        [Descripcion] nvarchar(1200) NULL,
        [NivelCobertura] nvarchar(40) NOT NULL,
        [CodigoDepartamento] nvarchar(20) NULL,
        [CodigoMunicipio] nvarchar(20) NULL,
        [Periodicidad] nvarchar(80) NULL,
        [CorreoContacto] nvarchar(180) NULL,
        [FechaPropuesta] datetime2(0) NOT NULL CONSTRAINT [DF_PropuestasCambioFestival_FechaPropuesta] DEFAULT (SYSUTCDATETIME()),
        [FechaEnvioRevision] datetime2(0) NULL,
        [FechaActualizacion] datetime2(0) NOT NULL CONSTRAINT [DF_PropuestasCambioFestival_FechaActualizacion] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_PropuestasCambioFestival] PRIMARY KEY ([IdPropuestaCambioFestival]),
        CONSTRAINT [FK_PropuestasCambioFestival_Festival] FOREIGN KEY ([FestivalOrigenId]) REFERENCES [Festivales] ([IdFestival]),
        CONSTRAINT [FK_PropuestasCambioFestival_Version] FOREIGN KEY ([VersionOrigenId]) REFERENCES [VersionesFestival] ([IdVersionFestival]),
        CONSTRAINT [FK_PropuestasCambioFestival_VersionNueva] FOREIGN KEY ([VersionNuevaId]) REFERENCES [VersionesFestival] ([IdVersionFestival]),
        CONSTRAINT [FK_PropuestasCambioFestival_Organizacion] FOREIGN KEY ([OrganizacionId]) REFERENCES [Entidades] ([IdEntidad]),
        CONSTRAINT [FK_PropuestasCambioFestival_Persona] FOREIGN KEY ([PersonaProponenteId]) REFERENCES [Usuarios] ([IdUsuario])
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PropuestasCambioFestival_Estado' AND object_id = OBJECT_ID(N'[PropuestasCambioFestival]'))
    CREATE INDEX [IX_PropuestasCambioFestival_Estado] ON [PropuestasCambioFestival] ([Estado]);

-- Una sola propuesta activa por Festival (misma historia que UX_VersionesFestival_Festival_Vigente).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_PropuestasCambioFestival_Activa' AND object_id = OBJECT_ID(N'[PropuestasCambioFestival]'))
    CREATE UNIQUE INDEX [UX_PropuestasCambioFestival_Activa]
        ON [PropuestasCambioFestival] ([FestivalOrigenId]) WHERE [Activa] = 1;

IF OBJECT_ID(N'[PropuestasCambioFestivalPracticasMusicales]', N'U') IS NULL
BEGIN
    CREATE TABLE [PropuestasCambioFestivalPracticasMusicales] (
        [IdPropuestaCambioFestivalPracticaMusical] int IDENTITY(1,1) NOT NULL,
        [PropuestaCambioFestivalId] int NOT NULL,
        [PracticaMusicalId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_PropuestasCambioFestivalPracticas_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_PropuestasCambioFestivalPracticasMusicales] PRIMARY KEY ([IdPropuestaCambioFestivalPracticaMusical]),
        CONSTRAINT [FK_PropuestasCambioFestivalPracticas_Propuesta] FOREIGN KEY ([PropuestaCambioFestivalId]) REFERENCES [PropuestasCambioFestival] ([IdPropuestaCambioFestival]),
        CONSTRAINT [FK_PropuestasCambioFestivalPracticas_Practica] FOREIGN KEY ([PracticaMusicalId]) REFERENCES [PracticasMusicales] ([IdPracticaMusical]),
        CONSTRAINT [UQ_PropuestasCambioFestivalPracticas] UNIQUE ([PropuestaCambioFestivalId], [PracticaMusicalId])
    );
END;

IF OBJECT_ID(N'[PropuestasCambioFestivalTerritoriosSonoros]', N'U') IS NULL
BEGIN
    CREATE TABLE [PropuestasCambioFestivalTerritoriosSonoros] (
        [IdPropuestaCambioFestivalTerritorioSonoro] int IDENTITY(1,1) NOT NULL,
        [PropuestaCambioFestivalId] int NOT NULL,
        [TerritorioSonoroId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_PropuestasCambioFestivalTerritorios_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_PropuestasCambioFestivalTerritoriosSonoros] PRIMARY KEY ([IdPropuestaCambioFestivalTerritorioSonoro]),
        CONSTRAINT [FK_PropuestasCambioFestivalTerritorios_Propuesta] FOREIGN KEY ([PropuestaCambioFestivalId]) REFERENCES [PropuestasCambioFestival] ([IdPropuestaCambioFestival]),
        CONSTRAINT [FK_PropuestasCambioFestivalTerritorios_Territorio] FOREIGN KEY ([TerritorioSonoroId]) REFERENCES [TerritoriosSonoros] ([IdTerritorioSonoro]),
        CONSTRAINT [UQ_PropuestasCambioFestivalTerritorios] UNIQUE ([PropuestaCambioFestivalId], [TerritorioSonoroId])
    );
END;
