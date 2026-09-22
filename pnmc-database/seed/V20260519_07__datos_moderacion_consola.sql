-- =========================================================================
-- SCRIPT DE DATOS DE PRUEBA EXHAUSTIVOS PARA MODERACIÓN Y CONSOLA DE PNMC
-- =========================================================================

-- -------------------------------------------------------------------------
-- AQUI NO VA UN `USE`. Y ESTUVO, hasta.
--
-- QUE HACIA. Este era el unico de los siete ficheros de `seed/` que abria con
-- `USE [la base]; GO`. Los otros seis heredan la base de quien los ejecuta,
-- que es `sqlcmd ... -d "$DB_NAME"` en `scripts/seed-local-db.sh`.
--
-- POR QUE IMPORTA. `USE` gana al `-d`. Con esta linea dentro, sembrar CUALQUIER
-- base -una de ensayo, una de un worktree, la que pide PNMC_LOCAL_DB_NAME-
-- escribia igualmente en la base, y lo primero que hace este fichero es
-- `DELETE FROM ... WHERE IdEntidad >= 100` sobre cuatro tablas. Borraba en la
-- base compartida los datos de la base que nadie habia pedido tocar. Ocurrio:
-- dos ensayos contra `PNMC_SIMUS_ENSAYO` acabaron dentro de la base.
--
-- Es exactamente el fallo que `seed-local-db.sh` documenta en su cinturon de
-- worktrees -aislar el codigo no aisla la base- por otra puerta: alli el guion
-- se equivocaba de arbol, aqui el fichero se equivocaba de base. El cinturon no
-- podia verlo porque mira el nombre que se PIDE, no el que acaba mandando.
--
-- La regla, vigilada por `SiembraSinBaseCableadaTests`: ningun fichero de
-- `schema/` ni de `seed/` nombra su base. La base la elige quien ejecuta.
-- -------------------------------------------------------------------------

-- -------------------------------------------------------------------------
-- 0. LOS USUARIOS QUE ESTE FICHERO NECESITA, RESUELTOS POR CORREO
--
-- ANTES ESTABAN CABLEADOS COMO NUMEROS: 2, 3, 4, 5 y 7. Y no existian. Una base
-- construida solo con `scripts/seed-local-db.sh` tiene UN usuario -el
-- `sistema@pnmc.local` de V20260519_03-; `admin@`, `gestor@` y `externo@` los
-- crea el arranque del API, que la siembra no ejecuta. Comprobado
-- sobre dos bases recien construidas: 1 usuario, y CERO de las 16 entidades de
-- este fichero.
--
-- COMO FALLABA, que es la parte que importa: `INSERT ... VALUES (a),(b),(c)` es
-- UNA sentencia. Una sola fila con un IdUsuario inexistente se lleva las tres.
-- Por eso el destrozo no era proporcional a la causa: faltaban 3 usuarios y se
-- perdian 8 de las 16 entidades, y detras los 16 vinculos y las 15 lineas de
-- historial ENTEROS, porque referencian entidades que ya no estaban. La consola
-- de moderacion se quedaba a medias y nada lo decia: sqlcmd imprime el Msg 547,
-- devuelve 0 filas afectadas y sigue con el fichero siguiente.
--
-- LA LECCION: un identificador subrogado no es un identificador. Lo elige la
-- base segun el orden de insercion, y de hecho no coincide entre bases -en
-- la base el rol `webmaster` es el 6 y en una base recien sembrada es el 3-.
-- Aqui todo se resuelve por su clave natural: el rol por nombre y el usuario por
-- correo. Y se crean si faltan, para que el fichero baste por si solo.
-- -------------------------------------------------------------------------
DECLARE @IdRolWebmaster int = (SELECT IdRol FROM dbo.Roles WHERE NombreRol = N'webmaster');
DECLARE @IdRolGestor    int = (SELECT IdRol FROM dbo.Roles WHERE NombreRol = N'gestor_interno');
DECLARE @IdRolExterno   int = (SELECT IdRol FROM dbo.Roles WHERE NombreRol = N'externo');

IF @IdRolWebmaster IS NULL OR @IdRolGestor IS NULL OR @IdRolExterno IS NULL
BEGIN
    -- Callarse aqui reproduce el defecto original en su version peor: seguir con
    -- variables NULL y perder las filas de una en una sin decir por que.
    RAISERROR(N'Faltan roles base (webmaster / gestor_interno / externo). Aplica seed/V20260519_03 antes que este fichero.', 16, 1);
END;

-- EL ROL VA EN dbo.UsuariosRoles, NO EN UNA COLUMNA.
-- `Usuarios.IdRol` ya no existe cuando este fichero corre: la retira la seccion 11 de
-- schema/V20260824_01, y `schema/` va entero antes que `seed/`. Escribirla aqui fallaria con
-- Msg 207 y se llevaria por delante las tres secciones que vienen detras.
--
-- Solo INSERT: si el arranque del API ya los creo, este fichero NO les toca el
-- hash ni el rol. La siembra completa lo que falte; no pisa credenciales.
MERGE dbo.Usuarios AS destino
USING (VALUES
    (N'Webmaster PNMC',           N'admin@pnmc.local',             N'interno'),
    (N'Gestor Interno PNMC',      N'gestor@pnmc.local',            N'interno'),
    (N'Colaborador Externo',      N'externo@pnmc.local',           N'externo'),
    (N'Participante de Prueba 2', N'participante.dos@pnmc.local',  N'externo'),
    (N'Participante de Prueba 3', N'participante.tres@pnmc.local', N'externo')
) AS origen (NombreCompleto, CorreoElectronico, CanalAcceso)
ON destino.CorreoElectronico = origen.CorreoElectronico
WHEN NOT MATCHED THEN
    INSERT (NombreCompleto, CorreoElectronico, HashContrasena, CanalAcceso, TipoPerfil, Activo)
    VALUES (origen.NombreCompleto, origen.CorreoElectronico, N'pendiente_configurar_hash_seguro',
            origen.CanalAcceso,
            CASE WHEN origen.CanalAcceso = N'externo' THEN N'organizacion' ELSE NULL END, 1);

-- Y SUS ROLES, por correo y por nombre de rol: ningun identificador subrogado escrito a mano.
-- Solo INSERT, por la misma razon que arriba: no retirar en silencio un rol concedido despues.
MERGE dbo.UsuariosRoles AS destino
USING (
    SELECT u.IdUsuario, reparto.IdRol
    FROM (VALUES
        (N'admin@pnmc.local',             @IdRolWebmaster),
        (N'gestor@pnmc.local',            @IdRolGestor),
        (N'externo@pnmc.local',           @IdRolExterno),
        (N'participante.dos@pnmc.local',  @IdRolExterno),
        (N'participante.tres@pnmc.local', @IdRolExterno)
    ) AS reparto (CorreoElectronico, IdRol)
    JOIN dbo.Usuarios AS u ON u.CorreoElectronico = reparto.CorreoElectronico
) AS origen
ON destino.IdUsuario = origen.IdUsuario AND destino.IdRol = origen.IdRol
WHEN NOT MATCHED THEN
    INSERT (IdUsuario, IdRol) VALUES (origen.IdUsuario, origen.IdRol);

DECLARE @Webmaster int = (SELECT IdUsuario FROM dbo.Usuarios WHERE CorreoElectronico = N'admin@pnmc.local');
DECLARE @Gestor    int = (SELECT IdUsuario FROM dbo.Usuarios WHERE CorreoElectronico = N'gestor@pnmc.local');
DECLARE @Externo1  int = (SELECT IdUsuario FROM dbo.Usuarios WHERE CorreoElectronico = N'externo@pnmc.local');
DECLARE @Externo2  int = (SELECT IdUsuario FROM dbo.Usuarios WHERE CorreoElectronico = N'participante.dos@pnmc.local');
DECLARE @Externo3  int = (SELECT IdUsuario FROM dbo.Usuarios WHERE CorreoElectronico = N'participante.tres@pnmc.local');

-- NO HAY `GO` HASTA EL FINAL DE LA SECCION 3, A PROPOSITO: `GO` no es T-SQL sino
-- un separador de sqlcmd, y cada lote empieza sin las variables del anterior.
-- Las tres secciones que usan estos identificadores tienen que ir en el mismo.

-- Limpiar registros de prueba anteriores para evitar duplicados en ejecuciones repetidas
DELETE FROM dbo.EntidadesHistorialRevision WHERE IdEntidad >= 100;
DELETE FROM dbo.UsuariosEntidades WHERE IdEntidad >= 100;
DELETE FROM dbo.EntidadesRegistrosFuente WHERE IdEntidad >= 100;
-- `EsInstitucional = 0` es un cinturon, no adorno: la organizacion institucional es la que
-- responde por todo festival que nadie ha reclamado, asi que borrarla muere con Msg 547 y
-- aborta la siembra entera. Se protege por la MARCA y no por el identificador, para que
-- siga protegida si alguna base la tiene dentro de este rango.
DELETE FROM dbo.Entidades WHERE IdEntidad >= 100 AND EsInstitucional = 0;

-- Habilitar inserción explícita de ID en Entidades para consistencia en pruebas
SET IDENTITY_INSERT dbo.Entidades ON;

-- -------------------------------------------------------------------------
-- 1. POBLAR TABLA: dbo.Entidades (Perfiles Administrativos)
-- -------------------------------------------------------------------------
-- Insertando entidades en todos los estados y tipos disponibles

-- ESTADO: borrador (Borradores en edición)
INSERT INTO dbo.Entidades (IdEntidad, TipoEntidad, Nombre, NombreLegal, Descripcion, CorreoContacto, TelefonoContacto, SitioWeb, NivelCobertura, CodigoDepartamento, CodigoMunicipio, EstadoRegistro, Activo, IdUsuarioCreador, IdUsuarioResponsable, FechaCreacion)
VALUES 
(100, N'colectivo', N'Colectivo de Canto del Litoral Pacífico', N'Colectivo Canto Litoral S.A.S.', N'Grupo musical comunitario enfocado en cantos tradicionales.', N'canto.litoral@pnmc.local', N'3101112233', N'http://cantolitoral.local', N'municipal', '05', '05001', N'borrador', 1, @Externo1, @Externo1, SYSUTCDATETIME()),
(101, N'organizacion', N'Asociación Musical Sinfónica Opus', N'Corporación Opus Colombia', N'Asociación cultural para la promoción de música de cámara.', N'opus@pnmc.local', N'3124445566', N'http://sinfonicaopus.local', N'municipal', '05', '05001', N'borrador', 1, @Externo2, @Externo2, SYSUTCDATETIME()),
(102, N'lutier', N'Lutería del Sur y Cuerdas Frotadas', N'Taller Lutería Sur', N'Construcción y reparación de violines, violas y violonchelos.', N'luteria.sur@pnmc.local', N'3157778899', N'http://luteriasur.local', N'municipal', '05', '05001', N'borrador', 1, @Externo3, @Externo3, SYSUTCDATETIME());

-- ESTADO: en_revision (Esperando aprobación del Gestor Interno o Webmaster)
INSERT INTO dbo.Entidades (IdEntidad, TipoEntidad, Nombre, NombreLegal, Descripcion, CorreoContacto, TelefonoContacto, SitioWeb, NivelCobertura, CodigoDepartamento, CodigoMunicipio, EstadoRegistro, Activo, IdUsuarioCreador, IdUsuarioResponsable, FechaCreacion, FechaRevision)
VALUES 
(103, N'organizacion', N'Fundación Filarmónica Metropolitana', N'Fundación Filarmónica Met.', N'Escuela filarmónica con más de 200 jóvenes activos en orquestas.', N'filarmonica.metro@pnmc.local', N'3202223344', N'http://filamero.local', N'municipal', '05', '05001', N'en_revision', 1, @Externo1, @Gestor, SYSUTCDATETIME(), SYSUTCDATETIME()),
(104, N'festival', N'Festival de Tambores Ancestrales de Barú', N'Festival Tambores Barú Ltda.', N'Evento anual de rescate de ritmos afrocolombianos de percusión.', N'tambores.baru@pnmc.local', N'3113334455', N'http://tamboresbaru.local', N'municipal', '05', '05001', N'en_revision', 1, @Externo2, @Gestor, SYSUTCDATETIME(), SYSUTCDATETIME()),
(105, N'escuela_musica', N'Escuela de Música Campestre Silvestre', N'Silvestre Escuela S.A.S.', N'Formatos musicales rurales y tradicionales para niños.', N'escuela.silvestre@pnmc.local', N'3184445566', N'http://silvestre.local', N'municipal', '05', '05001', N'en_revision', 1, @Externo3, @Gestor, SYSUTCDATETIME(), SYSUTCDATETIME());

-- ESTADO: ajustes_solicitados (Devuelto con observaciones para corregir)
INSERT INTO dbo.Entidades (IdEntidad, TipoEntidad, Nombre, NombreLegal, Descripcion, CorreoContacto, TelefonoContacto, SitioWeb, NivelCobertura, CodigoDepartamento, CodigoMunicipio, EstadoRegistro, Activo, IdUsuarioCreador, IdUsuarioResponsable, FechaCreacion, FechaActualizacion)
VALUES 
(106, N'colectivo', N'Colectivo de Cuerdas Sol del Desierto', N'Cuerdas Desierto', N'Ensamble de cuerdas típicas y andinas colombianas.', N'sol.desierto@pnmc.local', N'3215556677', N'http://soldesierto.local', N'municipal', '05', '05001', N'ajustes_solicitados', 1, @Externo2, @Externo2, SYSUTCDATETIME(), SYSUTCDATETIME()),
(107, N'mercado_musical', N'Mercado de Música Electrónica de Montaña', N'Mercado ElectroMontaña', N'Plataforma comercial para compositores y productores de música electrónica.', N'electro.montana@pnmc.local', N'3176667788', N'http://electromontana.local', N'municipal', '05', '05001', N'ajustes_solicitados', 1, @Externo1, @Externo1, SYSUTCDATETIME(), SYSUTCDATETIME());

-- ESTADO: aprobado (Listas para publicación final)
INSERT INTO dbo.Entidades (IdEntidad, TipoEntidad, Nombre, NombreLegal, Descripcion, CorreoContacto, TelefonoContacto, SitioWeb, NivelCobertura, CodigoDepartamento, CodigoMunicipio, EstadoRegistro, Activo, IdUsuarioCreador, IdUsuarioResponsable, FechaCreacion, FechaAprobacion)
VALUES 
(108, N'organizacion', N'Asociación Coral de Voces Unidas', N'Coral Voces Unidas', N'Coro polifónico profesional y formativo.', N'coral@pnmc.local', N'3169990011', N'http://vocesunidas.local', N'municipal', '05', '05001', N'aprobado', 1, @Gestor, @Gestor, SYSUTCDATETIME(), SYSUTCDATETIME()),
(109, N'lutier', N'Taller de Lutería Fina y Restauración', N'Lutería Fina S.A.S.', N'Expertos en la restauración de instrumentos de madera acústicos.', N'luteria.fina@pnmc.local', N'3008889900', N'http://luteriafina.local', N'municipal', '05', '05001', N'aprobado', 1, @Externo1, @Gestor, SYSUTCDATETIME(), SYSUTCDATETIME());

-- ESTADO: publicado (Visibles al público en general)
INSERT INTO dbo.Entidades (IdEntidad, TipoEntidad, Nombre, NombreLegal, Descripcion, CorreoContacto, TelefonoContacto, SitioWeb, NivelCobertura, CodigoDepartamento, CodigoMunicipio, EstadoRegistro, Activo, IdUsuarioCreador, IdUsuarioResponsable, FechaCreacion, FechaPublicacion)
VALUES 
(110, N'organizacion', N'Corporación Artística Batuta Local', N'Batuta Local Regional', N'Sede regional autorizada con cobertura de más de 300 estudiantes.', N'batutalocal@pnmc.local', N'3012223344', N'http://batutalocal.local', N'municipal', '05', '05001', N'publicado', 1, @Webmaster, @Webmaster, SYSUTCDATETIME(), SYSUTCDATETIME()),
(111, N'festival', N'Festival Internacional de Jazz Medellín', N'Fundación Jazz Medellin', N'El evento de Jazz más representativo de la región con invitados de 10 países.', N'jazz@pnmc.local', N'3023334455', N'http://jazzmedellin.local', N'municipal', '05', '05001', N'publicado', 1, @Webmaster, @Webmaster, SYSUTCDATETIME(), SYSUTCDATETIME()),
(112, N'mercado_musical', N'Mercado Musical del Pacífico y de la Montaña', N'Mercado Pacífico M.', N'Punto de encuentro de compradores internacionales y bandas emergentes.', N'mercado@pnmc.local', N'3044445566', N'http://mercadopacifico.local', N'municipal', '05', '05001', N'publicado', 1, @Gestor, @Gestor, SYSUTCDATETIME(), SYSUTCDATETIME());

-- ESTADO: rechazado (No cumplen criterios)
INSERT INTO dbo.Entidades (IdEntidad, TipoEntidad, Nombre, NombreLegal, Descripcion, CorreoContacto, TelefonoContacto, SitioWeb, NivelCobertura, CodigoDepartamento, CodigoMunicipio, EstadoRegistro, Activo, IdUsuarioCreador, IdUsuarioResponsable, FechaCreacion)
VALUES 
(113, N'organizacion', N'Asociación Musical del Viento Incompleta', N'Asociación Vientos Inc.', N'Propuesta de banda que no cuenta con NIT ni sede física registrada.', N'vientos@pnmc.local', N'3055556677', N'http://vientos.local', N'municipal', '05', '05001', N'rechazado', 1, @Externo2, @Gestor, SYSUTCDATETIME()),
(114, N'festival', N'Festival Fantasma Sin Planificación', N'Festival Fantasma Inc.', N'Evento propuesto sin cronograma ni patrocinadores definidos.', N'fantasma@pnmc.local', N'3066667788', N'http://fantasma.local', N'municipal', '05', '05001', N'rechazado', 1, @Externo3, @Gestor, SYSUTCDATETIME());

-- ESTADO: archivado (Retirados del flujo activo)
INSERT INTO dbo.Entidades (IdEntidad, TipoEntidad, Nombre, NombreLegal, Descripcion, CorreoContacto, TelefonoContacto, SitioWeb, NivelCobertura, CodigoDepartamento, CodigoMunicipio, EstadoRegistro, Activo, IdUsuarioCreador, IdUsuarioResponsable, FechaCreacion)
VALUES 
(115, N'colectivo', N'Antiguo Colectivo de Vientos y Cañas', N'Antiguo Colectivo Vientos', N'Agrupación disuelta en el año 2024.', N'antiguo@pnmc.local', N'3077778899', N'http://antiguo.local', N'municipal', '05', '05001', N'archivado', 1, @Externo1, @Externo1, SYSUTCDATETIME());

SET IDENTITY_INSERT dbo.Entidades OFF;

-- -------------------------------------------------------------------------
-- 2. POBLAR TABLA: dbo.UsuariosEntidades (Vínculos de Usuarios y Roles de Entidad)
-- -------------------------------------------------------------------------
-- Esto asegura que los perfiles puedan ser vistos y editados por los respectivos creadores
INSERT INTO dbo.UsuariosEntidades (IdUsuario, IdEntidad, RolEntidad, Activo, FechaCreacion)
VALUES 
(@Externo1, 100, N'propietario', 1, SYSUTCDATETIME()),
(@Externo2, 101, N'propietario', 1, SYSUTCDATETIME()),
(@Externo3, 102, N'propietario', 1, SYSUTCDATETIME()),
(@Externo1, 103, N'propietario', 1, SYSUTCDATETIME()),
(@Externo2, 104, N'propietario', 1, SYSUTCDATETIME()),
(@Externo3, 105, N'propietario', 1, SYSUTCDATETIME()),
(@Externo2, 106, N'propietario', 1, SYSUTCDATETIME()),
(@Externo1, 107, N'propietario', 1, SYSUTCDATETIME()),
(@Gestor, 108, N'administrador', 1, SYSUTCDATETIME()),
(@Externo1, 109, N'propietario', 1, SYSUTCDATETIME()),
(@Webmaster, 110, N'administrador', 1, SYSUTCDATETIME()),
(@Webmaster, 111, N'administrador', 1, SYSUTCDATETIME()),
(@Gestor, 112, N'administrador', 1, SYSUTCDATETIME()),
(@Externo2, 113, N'propietario', 1, SYSUTCDATETIME()),
(@Externo3, 114, N'propietario', 1, SYSUTCDATETIME()),
(@Externo1, 115, N'propietario', 1, SYSUTCDATETIME());

-- -------------------------------------------------------------------------
-- 3. POBLAR TABLA: dbo.EntidadesHistorialRevision (Historial de Auditoría de Moderación)
-- -------------------------------------------------------------------------
-- Registra justificaciones ficticias de revisión y estados para hacerlo 100% realista

INSERT INTO dbo.EntidadesHistorialRevision (IdEntidad, IdUsuario, Accion, Comentario, FechaAccion)
VALUES 
-- Historial para en_revision
(103, @Externo1, N'enviar_revision', N'Solicito revisión formal para la Filarmónica Metropolitana. Cumplimos con todos los requisitos.', SYSUTCDATETIME()),
(104, @Externo2, N'enviar_revision', N'Enviado para aprobación pública.', SYSUTCDATETIME()),
(105, @Externo3, N'enviar_revision', N'Enviado por colaborador externo de la comunidad.', SYSUTCDATETIME()),

-- Historial para ajustes_solicitados
(106, @Externo2, N'enviar_revision', N'Solicito revisión.', DATEADD(day, -2, SYSUTCDATETIME())),
(106, @Gestor, N'rechazar', N'Por favor, adjunta la certificación catastral de la sede o corregir la dirección.', DATEADD(day, -1, SYSUTCDATETIME())),

(107, @Externo1, N'enviar_revision', N'Se envía para revisión final.', DATEADD(day, -3, SYSUTCDATETIME())),
(107, @Webmaster, N'rechazar', N'La descripción de las actividades del mercado musical no es lo suficientemente detallada.', DATEADD(day, -1, SYSUTCDATETIME())),

-- Historial para aprobados
(108, @Gestor, N'aprobar', N'Verificado por Gestor. Cumple con todos los estándares y NIT correcto.', SYSUTCDATETIME()),
(109, @Gestor, N'aprobar', N'Lutería fina verificada. Documentación física validada.', SYSUTCDATETIME()),

-- Historial para publicados
(110, @Webmaster, N'publicar', N'Publicado en el mapa y catálogo nacional.', SYSUTCDATETIME()),
(111, @Webmaster, N'publicar', N'Aprobado y publicado para la agenda del mapa local.', SYSUTCDATETIME()),
(112, @Webmaster, N'publicar', N'Visibilidad pública concedida.', SYSUTCDATETIME()),

-- Historial para rechazados
(113, @Gestor, N'rechazar', N'Rechazado permanentemente por falta de sustento legal de la organización.', SYSUTCDATETIME()),
(114, @Gestor, N'rechazar', N'Rechazado por ser un registro de prueba no verídico ni planificado.', SYSUTCDATETIME()),

-- Historial para archivados
(115, @Externo1, N'archivar', N'Se archiva voluntariamente por disolución del grupo.', SYSUTCDATETIME());
GO

-- -------------------------------------------------------------------------
-- 4. POBLAR TABLA: dbo.Festivales (Módulo de Contenido Específico)
-- -------------------------------------------------------------------------
-- Registros de Festivales de prueba en todos los estados

DELETE FROM dbo.Festivales WHERE IdFestival >= 100;
GO

SET IDENTITY_INSERT dbo.Festivales ON;

-- organizacion de la comunidad lo reclame, responde la institucion; la crea
-- seed/V20260519_03, que corre antes que este fichero.
DECLARE @IdOrganizacionInstitucional int = (
    SELECT TOP (1) IdEntidad FROM dbo.Entidades WHERE EsInstitucional = 1 ORDER BY IdEntidad
);

INSERT INTO dbo.Festivales (IdFestival, NombreFestival, Descripcion, Organizador, CorreoFestival, NivelCobertura, CodigoDepartamento, CodigoMunicipio, Activo, EstadoRegistro, OrganizacionPrincipalId, FechaCreacion)
VALUES 
(100, N'Festival de Cuerdas de Barichara (Borrador)', N'Festival musical en desarrollo.', N'Taller Cuerdas', N'cuerdas@pnmc.local', N'municipal', '05', '05001', 1, N'borrador', @IdOrganizacionInstitucional, SYSUTCDATETIME()),
(101, N'Encuentro Andino en la Neblina (En Revisión)', N'Festival de música andina tradicional.', N'Neblina Colectivo', N'neblina@pnmc.local', N'municipal', '05', '05001', 1, N'en_revision', @IdOrganizacionInstitucional, SYSUTCDATETIME()),
(102, N'Festival del Pasillo y Vientos (Ajustes)', N'Festival musical con ajustes pendientes.', N'Comité Pasillo', N'pasillo@pnmc.local', N'municipal', '05', '05001', 1, N'ajustes_solicitados', @IdOrganizacionInstitucional, SYSUTCDATETIME()),
(103, N'Festival Nacional del Bambuco Metropolitano (Publicado)', N'El festival de bambuco más grande de la región.', N'Corpbambuco', N'bambuco@pnmc.local', N'municipal', '05', '05001', 1, N'publicado', @IdOrganizacionInstitucional, SYSUTCDATETIME()),
(104, N'Festival Fallido de Rock Local (Rechazado)', N'Propuesta de festival sin viabilidad.', N'NoName', N'noname@pnmc.local', N'municipal', '05', '05001', 1, N'rechazado', @IdOrganizacionInstitucional, SYSUTCDATETIME());

SET IDENTITY_INSERT dbo.Festivales OFF;
GO

-- -------------------------------------------------------------------------
-- 5. POBLAR TABLA: dbo.EscuelasMusica (Módulo de Contenido Específico)
-- -------------------------------------------------------------------------
-- Registros de Escuelas de prueba en todos los estados

DELETE FROM dbo.EscuelasMusica WHERE IdEscuelaMusica >= 100;
GO

SET IDENTITY_INSERT dbo.EscuelasMusica ON;

-- lotes y borra las variables, asi que cada lote declara la suya.
DECLARE @IdOrganizacionInstitucional int = (
    SELECT TOP (1) IdEntidad FROM dbo.Entidades WHERE EsInstitucional = 1 ORDER BY IdEntidad
);

INSERT INTO dbo.EscuelasMusica (IdEscuelaMusica, NombreEscuela, CategoriaEscuela, TipoEscuela, EntidadResponsable, CorreoContacto, NivelCobertura, CodigoDepartamento, CodigoMunicipio, EscuelaActiva, Activo, EstadoRegistro, OrganizacionResponsableId, FechaCreacion)
VALUES 
(100, N'Semillero Instrumental del Valle (Borrador)', N'Básica', N'Pública', N'Municipio', N'semillero@pnmc.local', N'municipal', '05', '05001', 1, 1, N'borrador', @IdOrganizacionInstitucional, SYSUTCDATETIME()),
(101, N'Escuela de Vientos del Norte (En Revisión)', N'Intermedia', N'Privada', N'Fundación Norte', N'vientos.norte@pnmc.local', N'municipal', '05', '05001', 1, 1, N'en_revision', @IdOrganizacionInstitucional, SYSUTCDATETIME()),
(102, N'Centro de Formación de Guitarras (Ajustes)', N'Avanzada', N'Pública', N'Colectivo', N'guitarras@pnmc.local', N'municipal', '05', '05001', 1, 1, N'ajustes_solicitados', @IdOrganizacionInstitucional, SYSUTCDATETIME()),
(103, N'Escuela de Formación Musical Batuta Medellín (Publicada)', N'Básica-Intermedia', N'Pública', N'Ministerio', N'batuta.med@pnmc.local', N'municipal', '05', '05001', 1, 1, N'publicado', @IdOrganizacionInstitucional, SYSUTCDATETIME()),
(104, N'Escuela Comercial de Canto Express (Rechazada)', N'Ninguna', N'Privada', N'Particular', N'express@pnmc.local', N'municipal', '05', '05001', 0, 1, N'rechazado', @IdOrganizacionInstitucional, SYSUTCDATETIME());

SET IDENTITY_INSERT dbo.EscuelasMusica OFF;
GO

-- -------------------------------------------------------------------------
-- 6. POBLAR TABLA: dbo.Lutieres (Módulo de Contenido Específico)
-- -------------------------------------------------------------------------
-- Registros de Lutieres de prueba en todos los estados

DELETE FROM dbo.Lutieres WHERE IdLutier >= 100;
GO

SET IDENTITY_INSERT dbo.Lutieres ON;

-- lotes y borra las variables, asi que cada lote declara la suya.
DECLARE @IdOrganizacionInstitucional int = (
    SELECT TOP (1) IdEntidad FROM dbo.Entidades WHERE EsInstitucional = 1 ORDER BY IdEntidad
);

INSERT INTO dbo.Lutieres (IdLutier, Nombre, TipoLutier, NombreTaller, Especialidad, CorreoContacto, NivelCobertura, CodigoDepartamento, CodigoMunicipio, Activo, EstadoRegistro, OrganizacionResponsableId, FechaCreacion)
VALUES 
(100, N'Carlos Viento Arpa (Borrador)', N'individual', N'Taller Carlos Arpas', N'Arpas llaneras', N'carlos@pnmc.local', N'municipal', '05', '05001', 1, N'borrador', @IdOrganizacionInstitucional, SYSUTCDATETIME()),
(101, N'Taller de Violines La Sonata (En Revisión)', N'taller', N'La Sonata Lutieres', N'Violines y violonchelos', N'sonata@pnmc.local', N'municipal', '05', '05001', 1, N'en_revision', @IdOrganizacionInstitucional, SYSUTCDATETIME()),
(102, N'Lutier de Percusión Menor (Ajustes)', N'individual', N'Taller Percusión', N'Tambores y maracas', N'percusión@pnmc.local', N'municipal', '05', '05001', 1, N'ajustes_solicitados', @IdOrganizacionInstitucional, SYSUTCDATETIME()),
(103, N'Taller de Guitarras Clásicas Ramírez (Publicado)', N'taller', N'Guitarras Ramírez', N'Guitarras clásicas y acústicas', N'ramirez@pnmc.local', N'municipal', '05', '05001', 1, N'publicado', @IdOrganizacionInstitucional, SYSUTCDATETIME()),
(104, N'Fabricante de Guitarras de Juguete (Rechazado)', N'individual', N'Juguetes', N'Guitarras plásticas sin afinación', N'juguete@pnmc.local', N'municipal', '05', '05001', 1, N'rechazado', @IdOrganizacionInstitucional, SYSUTCDATETIME());

SET IDENTITY_INSERT dbo.Lutieres OFF;
GO

PRINT '=====================================================';
PRINT 'DATOS DE PRUEBA EXHAUSTIVOS CARGADOS EXITOSAMENTE';
PRINT '=====================================================';
