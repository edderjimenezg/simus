/*
  SIMUS · Agenda — el modelo de eventos que la sección declaraba no tener

  DE DONDE VIENE. Hasta hoy `/agenda` era una pantalla honesta y vacía: «los filtros territoriales,
  la vista de calendario y los detalles se habilitarán cuando exista el modelo de eventos, el flujo
  de revisión y el contrato público correspondiente». Ninguno existía. Este guion pone el primero.

  LA MODALIDAD DECIDE QUE DATOS SON OBLIGATORIOS, y la base lo impone. Un evento presencial sin
  lugar no se puede anunciar —nadie sabría a dónde ir— y uno virtual sin enlace tampoco. Si esa
  regla viviera solo en el código, la primera importación que escriba directo la saltaría, y el
  síntoma sería una tarjeta en el portal invitando a un sitio que no dice cuál es.

  UN EVENTO PASADO NO DESAPARECE. A diferencia de una noticia futura —que se publica y espera su
  fecha—, aquí no hay corte por fecha en la visibilidad: lo que ya ocurrió sigue siendo información
  pública y el portal lo presenta como finalizado en vez de esconderlo. Quien busca «qué hubo el
  año pasado en el Cauca» tiene tanto derecho a encontrarlo como quien busca qué viene.

  LA FECHA DE FIN ES OPCIONAL Y SIGNIFICA «el mismo día». Obligarla convertiría cada evento de una
  tarde en dos campos idénticos, y el día que alguien se equivocara tendríamos eventos de duración
  negativa; de ahí la CHECK que exige que el fin no preceda al inicio.

  EL TERRITORIO SE GUARDA POR CODIGO DIVIPOLA, no por nombre escrito. `Divipola` es el catálogo
  territorial canónico del proyecto —lo consumen el alta sin sesión, los formularios y el geovisor—
  y escribir «Bogota» a mano hace imposible agrupar por departamento.

  NINGUNA FORANEA LLEVA CASCADA, que es la regla del esquema y `EstructuraSimusSqlServerTests` la
  comprueba. Retirar un evento es cambiarlo de estado, no borrarlo.

  NO SIEMBRA NADA. Una agenda que nace con eventos de demostración acaba enseñándolos.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.EventosAgenda', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EventosAgenda (
        IdEventoAgenda bigint IDENTITY(1,1) NOT NULL,
        /* La dirección pública. Se fija al crear y no cambia. */
        Slug nvarchar(180) NOT NULL,
        Titulo nvarchar(300) NOT NULL,
        Descripcion nvarchar(1000) NOT NULL,
        FechaInicio date NOT NULL,
        /* NULL significa «termina el mismo día». */
        FechaFin date NULL,
        HoraInicio time(0) NULL,
        Modalidad nvarchar(20) NOT NULL CONSTRAINT DF_EventosAgenda_Modalidad DEFAULT (N'presencial'),
        Lugar nvarchar(300) NULL,
        CodigoDepartamento char(2) NULL,
        CodigoMunicipio char(5) NULL,
        Url nvarchar(500) NULL,
        ImagenRuta nvarchar(500) NULL,
        ImagenAlternativa nvarchar(300) NULL,
        Estado nvarchar(20) NOT NULL CONSTRAINT DF_EventosAgenda_Estado DEFAULT (N'borrador'),
        Version int NOT NULL CONSTRAINT DF_EventosAgenda_Version DEFAULT (1),
        FechaCreacion datetime2(3) NOT NULL CONSTRAINT DF_EventosAgenda_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion datetime2(3) NOT NULL CONSTRAINT DF_EventosAgenda_FechaActualizacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_EventosAgenda PRIMARY KEY CLUSTERED (IdEventoAgenda),
        CONSTRAINT UQ_EventosAgenda_Slug UNIQUE (Slug),
        CONSTRAINT CK_EventosAgenda_Estado CHECK (Estado IN (N'borrador', N'en_revision', N'publicado', N'archivado')),
        CONSTRAINT CK_EventosAgenda_Modalidad CHECK (Modalidad IN (N'presencial', N'virtual', N'mixta')),
        CONSTRAINT CK_EventosAgenda_Titulo CHECK (LEN(LTRIM(RTRIM(Titulo))) > 0),
        CONSTRAINT CK_EventosAgenda_Descripcion CHECK (LEN(LTRIM(RTRIM(Descripcion))) > 0),
        CONSTRAINT CK_EventosAgenda_Version CHECK (Version >= 1),
        /* Un evento no puede terminar antes de empezar. */
        CONSTRAINT CK_EventosAgenda_Fechas CHECK (FechaFin IS NULL OR FechaFin >= FechaInicio),
        /*
          LA MODALIDAD DECIDE QUE ES OBLIGATORIO, y solo al publicar no bastaría: un borrador puede
          estar incompleto, pero la fila publicada tiene que poder anunciarse. Se exige aqui sobre
          cualquier estado porque la alternativa —comprobarlo solo en el endpoint— deja la puerta
          abierta a cualquier escritura directa.
            · presencial: hace falta lugar
            · virtual:    hace falta enlace
            · mixta:      hacen falta los dos
        */
        /*
          EL TERRITORIO APUNTA AL CATALOGO, no se escribe suelto. Es la regla del esquema que
          comprueba `EstructuraSimusSqlServerTests`: toda tabla que guarde el par
          departamento/municipio tiene foránea a `Divipola`, porque un par inventado hace
          imposible agrupar y nadie lo nota hasta que un informe sale corto.

          LAS DOS COLUMNAS SON NULAS A PROPOSITO —un evento virtual no ocurre en ningún
          municipio— y SQL Server da la restricción por satisfecha cuando alguna lo es, de modo
          que un evento con solo departamento tampoco se bloquea.
        */
        CONSTRAINT FK_EventosAgenda_Divipola FOREIGN KEY (CodigoDepartamento, CodigoMunicipio)
            REFERENCES dbo.Divipola (CodigoDepartamento, CodigoMunicipio),
        CONSTRAINT CK_EventosAgenda_DatosDeModalidad CHECK (
            (Modalidad = N'presencial' AND (Estado <> N'publicado' OR LEN(LTRIM(RTRIM(ISNULL(Lugar, N'')))) > 0))
         OR (Modalidad = N'virtual'    AND (Estado <> N'publicado' OR LEN(LTRIM(RTRIM(ISNULL(Url, N'')))) > 0))
         OR (Modalidad = N'mixta'      AND (Estado <> N'publicado' OR (LEN(LTRIM(RTRIM(ISNULL(Lugar, N'')))) > 0 AND LEN(LTRIM(RTRIM(ISNULL(Url, N'')))) > 0)))
        )
    );

    CREATE INDEX IX_EventosAgenda_Estado_Fecha ON dbo.EventosAgenda (Estado, FechaInicio);
    CREATE INDEX IX_EventosAgenda_Departamento ON dbo.EventosAgenda (CodigoDepartamento);
END
GO

/* ─────────────────────────── Etiquetas, una fila por término ─────────────────────────── */
IF OBJECT_ID(N'dbo.EtiquetasEventoAgenda', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EtiquetasEventoAgenda (
        IdEtiquetaEventoAgenda bigint IDENTITY(1,1) NOT NULL,
        EventoAgendaId bigint NOT NULL,
        Termino nvarchar(120) NOT NULL,
        CONSTRAINT PK_EtiquetasEventoAgenda PRIMARY KEY CLUSTERED (IdEtiquetaEventoAgenda),
        CONSTRAINT FK_EtiquetasEventoAgenda_Evento FOREIGN KEY (EventoAgendaId) REFERENCES dbo.EventosAgenda (IdEventoAgenda),
        CONSTRAINT UQ_EtiquetasEventoAgenda_Termino UNIQUE (EventoAgendaId, Termino),
        CONSTRAINT CK_EtiquetasEventoAgenda_Termino CHECK (LEN(LTRIM(RTRIM(Termino))) > 0)
    );

    CREATE INDEX IX_EtiquetasEventoAgenda_Termino ON dbo.EtiquetasEventoAgenda (Termino);
END
GO
