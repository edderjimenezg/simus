/*
    SIMUS · La organización es un actor, no una ficha publicable

    QUE PROBLEMA CIERRA. En el desarrollo de septiembre `dbo.Entidades` era una ficha publicable
    genérica: un festival, una escuela de música, un lutier, un mercado y un escenario eran filas de
    esa misma tabla, con su grafo de relaciones (`EntidadesRelaciones`), su puente polimórfico a las
    cinco tablas del ecosistema (`EntidadesRegistrosFuente`) y su propio circuito de moderación de
    ocho acciones (`EntidadesHistorialRevision`). Esta base tomó otro camino y ya lo demostró:
    Festival es una entidad principal con tabla, ciclo y revisión propios, y `V20260911_04` retiró
    los cinco procesos genéricos. Lo que quedó de septiembre son cáscaras.

    LO QUE SE RETIRA, Y POR QUE CADA COSA.

    1. Las tres tablas de la capa genérica. Cero filas, cero mapeo EF, cero referencias en
       `pnmc-api/src`. `V20260904_02` ya las retiraba, pero SOLO si la base se llamaba
       `PNMC_SIMUS_LIMPIO`: en cualquier otra imprimía un aviso y DbUp la daba por aplicada. El
       mismo código convivía con dos esquemas según el nombre de la base, que es justo lo que
       `V20260911_04` vino a cerrar para las tablas del ecosistema y dejó abierto para estas.
       Aquí se cierra para todos los perfiles, con la misma guarda: si alguna tiene filas, se
       detiene.

    2. `Latitud` y `Longitud` de `dbo.Entidades`. Son del geovisor de septiembre, que pintaba
       organizaciones en el mapa. Ningún camino del API las escribe —`ExternalOrganizationEndpoints`
       las nombra en su lista de «lo que no se escribe»— y ninguno las lee. La ubicación propia de
       una organización es su sede DIVIPOLA (`CodigoDepartamentoSede`, `CodigoMunicipioSede`) más su
       dirección en texto. El alcance territorial es de cada proceso, no de la entidad: eso ya lo
       decidió `V20260909_02` al retirar `NivelCobertura`.

    3. Los siete tipos de entidad que describen procesos y lugares. `CK_Entidades_Tipo` admitía
       nueve valores: dos actores —`organizacion` y `agrupacion`, los que el propio código declara
       como creables (`TiposDeEntidad.AltaExterna`)— y siete que nombran procesos y lugares:
       `festival`, `escuela_musica`, `mercado_musical`, `espacio`, `lutier`, `colectivo` e
       `individuo`. Los siete son la idea de septiembre de que todo cabe en una tabla, y esta base
       ya decidió lo contrario: Festival tiene tabla, ciclo y revisión propios, y `V20260911_04`
       retiró los otros cuatro procesos diciendo «VOLVERAN, Y NO ASI».

       No es cosmético: `AdministracionDeOrganizacion.PuedeAdministrarAsync` resolvía quién
       administra por vínculo y vigencia, sin mirar el tipo, de modo que una fila con
       `TipoEntidad = 'festival'` era administrable como si fuera una organización. La condición se
       añadió también en el código; esto es la otra mitad.

    LO QUE SE ESTRECHA. `EstadoRegistro` sigue con foránea a `dbo.EstadosContenido` —moverla
    arrastraría las siete tablas de contenido que comparten ese catálogo—, pero una entidad solo
    puede estar en los CUATRO estados que significan algo para un actor: `registrada`, `verificada`,
    `ajustes_solicitados` y `archivado`. Los otros cinco prometían lo que no hay: no existe página
    pública de organización que `publicado` publique, ni bandeja de revisión que `en_revision`
    alimente, ni acto institucional que `aprobado` respalde. Las filas existentes se mueven: lo que
    afirmaba respaldo institucional (`aprobado`, `publicado`) pasa a `verificada`, y lo que describía
    un borrador o un rechazo que nunca ocurrió vuelve a `registrada`.

    NO SE PIERDE INFORMACION. Las tres tablas se comprueban vacías antes de caer, y el movimiento de
    estados queda escrito aquí con su regla, que es más de lo que decía la columna.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

/* ---------- 1. La capa genérica de septiembre ---------------------------------------------- */

DECLARE @tabla sysname;
DECLARE @tablas TABLE (Nombre sysname NOT NULL PRIMARY KEY);

INSERT INTO @tablas (Nombre)
VALUES (N'EntidadesRelaciones'), (N'EntidadesHistorialRevision'), (N'EntidadesRegistrosFuente');

DECLARE cursor_tablas CURSOR LOCAL FAST_FORWARD FOR SELECT Nombre FROM @tablas;
OPEN cursor_tablas;
FETCH NEXT FROM cursor_tablas INTO @tabla;

WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'dbo.' + @tabla, N'U') IS NOT NULL
    BEGIN
        DECLARE @sql nvarchar(max) = N'IF EXISTS (SELECT 1 FROM dbo.' + QUOTENAME(@tabla) + N')
            THROW 51005, ''No se puede retirar dbo.' + @tabla + N': contiene registros. Exporte o concilie sus datos antes de continuar.'', 1;';
        EXEC sys.sp_executesql @sql;
    END;

    FETCH NEXT FROM cursor_tablas INTO @tabla;
END;

CLOSE cursor_tablas;
DEALLOCATE cursor_tablas;
GO

IF OBJECT_ID(N'dbo.EntidadesRelaciones', N'U') IS NOT NULL DROP TABLE dbo.EntidadesRelaciones;
IF OBJECT_ID(N'dbo.EntidadesHistorialRevision', N'U') IS NOT NULL DROP TABLE dbo.EntidadesHistorialRevision;
IF OBJECT_ID(N'dbo.EntidadesRegistrosFuente', N'U') IS NOT NULL DROP TABLE dbo.EntidadesRegistrosFuente;
GO

/* ---------- 2. Las coordenadas del geovisor ------------------------------------------------ */

DECLARE @restriccionesDeCoordenada nvarchar(max) = N'';

SELECT @restriccionesDeCoordenada = @restriccionesDeCoordenada
     + N'ALTER TABLE dbo.Entidades DROP CONSTRAINT [' + dc.name + N'];'
FROM sys.default_constraints dc
JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Entidades')
  AND c.name IN (N'Latitud', N'Longitud');

IF @restriccionesDeCoordenada <> N'' EXEC sys.sp_executesql @restriccionesDeCoordenada;
GO

IF COL_LENGTH(N'dbo.Entidades', N'Latitud') IS NOT NULL ALTER TABLE dbo.Entidades DROP COLUMN Latitud;
IF COL_LENGTH(N'dbo.Entidades', N'Longitud') IS NOT NULL ALTER TABLE dbo.Entidades DROP COLUMN Longitud;
GO

/* ---------- 3. Un solo tipo de entidad ------------------------------------------------------ */

IF EXISTS (SELECT 1 FROM dbo.Entidades WHERE TipoEntidad NOT IN (N'organizacion', N'agrupacion'))
    THROW 51006, N'dbo.Entidades contiene filas de un tipo que describe un proceso o un lugar. Concilie esos registros antes de continuar.', 1;
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Entidades_Tipo')
    ALTER TABLE dbo.Entidades DROP CONSTRAINT CK_Entidades_Tipo;
GO

ALTER TABLE dbo.Entidades ADD CONSTRAINT CK_Entidades_Tipo
    CHECK (TipoEntidad IN (N'organizacion', N'agrupacion'));
GO

/* ---------- 4. El estado de un actor -------------------------------------------------------- */

IF NOT EXISTS (SELECT 1 FROM dbo.EstadosContenido WHERE CodigoEstado = N'verificada')
BEGIN
    INSERT INTO dbo.EstadosContenido (CodigoEstado, NombreEstado, DescripcionEstado)
    VALUES (N'verificada', N'Verificada', N'Organización cuya identificación y responsable comprobó el equipo institucional.');
END;
GO

/*
    El movimiento de las filas existentes. `aprobado` y `publicado` afirmaban un respaldo
    institucional: lo que de verdad quisieron decir es que alguien de la consola dio la ficha por
    buena, y eso ahora se llama `verificada`. `borrador`, `en_revision` y `rechazado` describían un
    circuito que para una organización nunca existió: vuelven al estado con el que nace.
*/
UPDATE dbo.Entidades SET EstadoRegistro = N'verificada'
WHERE EstadoRegistro IN (N'aprobado', N'publicado');
GO

UPDATE dbo.Entidades SET EstadoRegistro = N'registrada'
WHERE EstadoRegistro IN (N'borrador', N'en_revision', N'rechazado');
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Entidades_EstadoRegistro')
    ALTER TABLE dbo.Entidades DROP CONSTRAINT CK_Entidades_EstadoRegistro;
GO

ALTER TABLE dbo.Entidades ADD CONSTRAINT CK_Entidades_EstadoRegistro
    CHECK (EstadoRegistro IN (N'registrada', N'verificada', N'ajustes_solicitados', N'archivado'));
GO
