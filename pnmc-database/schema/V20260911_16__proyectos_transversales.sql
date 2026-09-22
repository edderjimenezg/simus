/* PNMC · Proyectos transversales: Celebra la Música y los que vengan */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

/*
  QUE PROBLEMA RESUELVE.

  El Programa tiene iniciativas que atraviesan varios modulos -Celebra la Musica es la primera- y
  que necesitan reunir su propio contenido: su calendario, sus noticias. Hoy la unica forma de
  hacerlo seria etiquetar a mano y confiar en que nadie escriba la etiqueta distinto, o duplicar
  los eventos en un sitio aparte, que es como se acaba con dos calendarios que discrepan.

  QUE NO ES. No es una categoria tematica. Una categoria dice DE QUE TRATA un contenido
  -«Convocatorias», «Bandas»-; un proyecto transversal dice A QUE INICIATIVA PERTENECE. Un evento
  puede ser de la categoria «Encuentros» Y pertenecer a Celebra la Musica: son dos preguntas
  distintas y mezclarlas obligaria a inventar categorias como «Encuentros de Celebra».

  A QUE SE ENLAZA, Y A QUE NO.

  Se enlazan EVENTOS DE AGENDA y NOTICIAS. NO se enlazan Festivales: lo decidio la direccion el 11
  Un Festival es un proceso del ecosistema con su propia vida y su propia
  organizacion responsable; lo que puede pertenecer a una iniciativa del Programa es el contenido
  que se publica sobre el, no el proceso entero.

  Por eso hay dos tablas puente y no tres, y por eso no se crea una generica por dominio: dos
  tablas concretas con foranea real valen mas que una tabla polimorfica que ninguna clave puede
  proteger. La procedencia usa el par (Dominio, RegistroId) porque tiene que servir a diez modulos
  y crecer sin migraciones; esto son dos, y los dos se conocen.

  LAS FORANEAS NO CASCADEAN, por la regla del proyecto.
*/

IF OBJECT_ID(N'dbo.ProyectosTransversales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProyectosTransversales (
        IdProyectoTransversal int IDENTITY(1,1) NOT NULL,

        /* La direccion estable del proyecto. El portal la usa para filtrar su contenido. */
        Codigo nvarchar(80) NOT NULL,
        Nombre nvarchar(200) NOT NULL,
        Descripcion nvarchar(1000) NULL,

        /*
          UN PROYECTO QUE TERMINA NO SE BORRA, SE DESACTIVA. Borrarlo dejaria sin explicacion el
          contenido que se publico bajo el; desactivado deja de ofrecerse en los formularios y
          sigue explicando lo que ya existe.
        */
        Activo bit NOT NULL CONSTRAINT DF_ProyectosTransversales_Activo DEFAULT (1),

        OrdenVisualizacion int NOT NULL CONSTRAINT DF_ProyectosTransversales_Orden DEFAULT (1),
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_ProyectosTransversales_Creacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion datetime2(0) NULL,

        CONSTRAINT PK_ProyectosTransversales PRIMARY KEY CLUSTERED (IdProyectoTransversal),
        CONSTRAINT UQ_ProyectosTransversales_Codigo UNIQUE (Codigo),
        CONSTRAINT UQ_ProyectosTransversales_Nombre UNIQUE (Nombre),
        CONSTRAINT CK_ProyectosTransversales_Orden CHECK (OrdenVisualizacion > 0)
    );
END
GO

IF OBJECT_ID(N'dbo.EventosAgendaProyectosTransversales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EventosAgendaProyectosTransversales (
        IdEventoAgendaProyectoTransversal bigint IDENTITY(1,1) NOT NULL,
        EventoAgendaId bigint NOT NULL,
        ProyectoTransversalId int NOT NULL,
        CONSTRAINT PK_EventosAgendaProyectosTransversales PRIMARY KEY CLUSTERED (IdEventoAgendaProyectoTransversal),
        CONSTRAINT FK_EventosAgendaProyTrans_Evento FOREIGN KEY (EventoAgendaId) REFERENCES dbo.EventosAgenda (IdEventoAgenda),
        CONSTRAINT FK_EventosAgendaProyTrans_Proyecto FOREIGN KEY (ProyectoTransversalId) REFERENCES dbo.ProyectosTransversales (IdProyectoTransversal),
        CONSTRAINT UQ_EventosAgendaProyectosTransversales UNIQUE (EventoAgendaId, ProyectoTransversalId)
    );
    CREATE INDEX IX_EventosAgendaProyTrans_Proyecto ON dbo.EventosAgendaProyectosTransversales (ProyectoTransversalId);
END
GO

IF OBJECT_ID(N'dbo.NoticiasProyectosTransversales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NoticiasProyectosTransversales (
        IdNoticiaProyectoTransversal bigint IDENTITY(1,1) NOT NULL,
        NoticiaId bigint NOT NULL,
        ProyectoTransversalId int NOT NULL,
        CONSTRAINT PK_NoticiasProyectosTransversales PRIMARY KEY CLUSTERED (IdNoticiaProyectoTransversal),
        CONSTRAINT FK_NoticiasProyTrans_Noticia FOREIGN KEY (NoticiaId) REFERENCES dbo.Noticias (IdNoticia),
        CONSTRAINT FK_NoticiasProyTrans_Proyecto FOREIGN KEY (ProyectoTransversalId) REFERENCES dbo.ProyectosTransversales (IdProyectoTransversal),
        CONSTRAINT UQ_NoticiasProyectosTransversales UNIQUE (NoticiaId, ProyectoTransversalId)
    );
    CREATE INDEX IX_NoticiasProyTrans_Proyecto ON dbo.NoticiasProyectosTransversales (ProyectoTransversalId);
END
GO

/*
  CELEBRA LA MUSICA ENTRA SEMBRADA porque es el proyecto que motivo esta tabla y el unico que la
  direccion nombro. Los demas se crean desde la consola: sembrar una lista entera de proyectos que
  nadie ha aprobado convertiria en regla institucional unos valores inventados aqui.
*/
IF NOT EXISTS (SELECT 1 FROM dbo.ProyectosTransversales WHERE Codigo = N'celebra-la-musica')
BEGIN
    INSERT INTO dbo.ProyectosTransversales (Codigo, Nombre, Descripcion, Activo, OrdenVisualizacion)
    VALUES (
        N'celebra-la-musica',
        N'Celebra la Música',
        N'Iniciativa transversal del Plan Nacional de Música para la Convivencia. El contenido que se enlaza aquí puede reunirse en su propio calendario y en su propia sala de prensa.',
        1,
        1);
END
GO
