/*
    Capa minima de notificaciones transaccionales.
    WhatsApp y correo real quedan como proveedores futuros; esta tabla conserva
    eventos internos, destinatarios, estado y trazabilidad de lectura/envio.
*/

IF OBJECT_ID(N'dbo.Notificaciones', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Notificaciones (
        IdNotificacion bigint IDENTITY(1,1) NOT NULL,
        UsuarioDestinatarioId int NULL,
        CorreoDestinatario nvarchar(320) NULL,
        TipoEvento nvarchar(100) NOT NULL,
        Canal nvarchar(40) NOT NULL CONSTRAINT DF_Notificaciones_Canal DEFAULT (N'internal'),
        Titulo nvarchar(240) NOT NULL,
        Cuerpo nvarchar(2000) NOT NULL,
        Estado nvarchar(40) NOT NULL CONSTRAINT DF_Notificaciones_Estado DEFAULT (N'pendiente'),
        ModuloId nvarchar(80) NULL,
        RegistroId nvarchar(120) NULL,
        MetadataJson nvarchar(max) NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_Notificaciones_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaEnvio datetime2(0) NULL,
        FechaLectura datetime2(0) NULL,
        Intentos int NOT NULL CONSTRAINT DF_Notificaciones_Intentos DEFAULT (0),
        Error nvarchar(1200) NULL,
        CONSTRAINT PK_Notificaciones PRIMARY KEY (IdNotificacion),
        CONSTRAINT FK_Notificaciones_Usuarios FOREIGN KEY (UsuarioDestinatarioId) REFERENCES dbo.Usuarios (IdUsuario),
        CONSTRAINT CK_Notificaciones_Canal CHECK (Canal IN (N'internal', N'email', N'whatsapp')),
        CONSTRAINT CK_Notificaciones_Estado CHECK (Estado IN (N'pendiente', N'enviada', N'leida', N'fallida', N'cancelada'))
    );

    CREATE INDEX IX_Notificaciones_Destinatario ON dbo.Notificaciones (UsuarioDestinatarioId, FechaCreacion DESC);
    CREATE INDEX IX_Notificaciones_Correo ON dbo.Notificaciones (CorreoDestinatario, FechaCreacion DESC);
    CREATE INDEX IX_Notificaciones_Evento ON dbo.Notificaciones (TipoEvento, Estado, FechaCreacion DESC);
END;
