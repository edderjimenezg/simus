/*
    PNMC · Las propuestas de cambio de Festival pasan a las tablas genéricas

    QUE ES ESTO
    -----------
    La segunda de las dos deudas que dejó escrita el corte de Mercados. La primera —las revisiones—
    se saldó en `V20260917_02`. Esta es la otra: `PropuestasCambioFestival` es una SOMBRA COMPLETA
    del perfil público —una columna por cada columna, más dos tablas de relaciones—, mientras que
    `PropuestasDeCambio` guarda SOLO LO QUE CAMBIA.

    POR QUE ES UN CAMBIO DE FORMA Y NO SOLO DE TABLA
    ------------------------------------------------
    En la sombra, «lo que se propone» es el registro entero y hay que compararlo con la versión
    vigente para saber qué cambia. En la forma nueva, lo que se guarda ES la comparación. Migrar,
    por tanto, no es copiar filas: es CALCULAR, para cada propuesta, en qué se diferencia de la
    versión sobre la que se hizo, y guardar solo esos campos.

    EL «ANTES» SALE DE `VersionOrigenId`, QUE ES LO CORRECTO
    --------------------------------------------------------
    Cada propuesta declara sobre qué versión se hizo. Ese es su «antes», y no la versión vigente de
    hoy: una propuesta de hace un mes se comparó contra lo que el público leía entonces, y
    reescribirla contra lo de ahora falsificaría el expediente.

    LOS DIECISEIS CAMPOS, Y NI UNO MAS
    ----------------------------------
    Son los que la sombra tenía, que es lo que se podía proponer. Las dos listas de catálogo
    —prácticas musicales y territorios sonoros— viajan como identificadores separados por comas,
    ordenados, que es como las guarda el lado de Mercados.

    LOS ESTADOS SE TRADUCEN
    -----------------------
      · `Borrador`          -> `borrador`
      · `EnRevision`        -> `en_revision`
      · `AjustesSolicitados`-> `ajustes_solicitados`
      · `Publicada`         -> `aplicada`
      · `Rechazada`         -> `rechazada`

    `Publicada` pasa a `aplicada` porque es lo que significa en el vocabulario nuevo: la propuesta se
    aplicó. Que en Festival aplicarla haya creado una versión y en Mercados reescriba el registro es
    una diferencia de cómo se aplica, no de en qué quedó la propuesta.

    QUE **NO** HACE, Y ES DELIBERADO
    --------------------------------
    NO BORRA `PropuestasCambioFestival` NI SUS DOS TABLAS DE RELACIONES. Igual que en el corte de las
    revisiones: borrar tablas con datos es irreversible y no se hace sin validación explícita. Quedan
    ahí, sin que nadie las lea, y son la marcha atrás de esta versión.

    Tampoco toca `VersionNuevaId`: la versión que nació de una propuesta aprobada sigue existiendo y
    sigue siendo la vigente. Lo que se migra es el expediente, no su resultado.

    ESTE FICHERO TIENE QUE ESTAR EN LA LISTA `SCHEMAS` DE scripts/seed-local-db.sh.
*/
SET QUOTED_IDENTIFIER ON;
GO

/*
    SOBRE QUE REALIZACION DEL REGISTRO SE PROPONE
    ---------------------------------------------
    `PropuestasDeCambio` nació sin esta columna porque un mercado no tiene realizaciones: lo que se
    propone es sobre su ficha y punto. Un Festival sí las tiene —lo que el público lee es una
    VERSION de su perfil—, y la propuesta declara sobre cuál se hizo: ese es su «antes», y no la
    versión vigente de hoy.

    Es el mismo concepto que `SubregistroId` en las observaciones de revisión, con el mismo nombre y
    el mismo tipo. Nulo cuando el registro no tiene realizaciones.
*/
IF COL_LENGTH('dbo.PropuestasDeCambio', 'SubregistroId') IS NULL
BEGIN
    ALTER TABLE dbo.PropuestasDeCambio ADD SubregistroId nvarchar(120) NULL;
END;
GO

/*
    Y QUE REALIZACION NACIO DE APLICARLA
    ------------------------------------
    La simétrica de la anterior, en el lado de la salida. Al aplicar una propuesta sobre un mercado
    se reescribe su ficha y no nace nada; sobre un Festival nace la VERSION siguiente de su perfil.

    NO ES UN ADORNO DEL EXPEDIENTE: la lectura pública se apoya en ella para no servir una versión
    que nació de una propuesta que no acabó aprobada. Es el guardián de PNMC-054, y sin esta columna
    habría que dejarlo leyendo la tabla vieja, que es justo lo que esta versión viene a terminar.
*/
IF COL_LENGTH('dbo.PropuestasDeCambio', 'SubregistroResultanteId') IS NULL
BEGIN
    ALTER TABLE dbo.PropuestasDeCambio ADD SubregistroResultanteId nvarchar(120) NULL;
END;
GO

IF EXISTS (SELECT 1 FROM dbo.PropuestasDeCambio WHERE ModuloId = N'festivales')
BEGIN
    PRINT '[V20260917_03] Ya hay propuestas de festivales en la forma generica: no se copia nada.';
END
ELSE
BEGIN
    DECLARE @propuestasAntes int = (SELECT COUNT(*) FROM dbo.PropuestasCambioFestival);

    DECLARE @equivalencias TABLE (IdViejo int NOT NULL PRIMARY KEY, IdNuevo bigint NOT NULL);

    MERGE dbo.PropuestasDeCambio AS destino
    USING (SELECT p.*,
                  CASE p.Estado
                       WHEN N'Borrador'           THEN N'borrador'
                       WHEN N'EnRevision'         THEN N'en_revision'
                       WHEN N'AjustesSolicitados' THEN N'ajustes_solicitados'
                       WHEN N'Publicada'          THEN N'aplicada'
                       WHEN N'Rechazada'          THEN N'rechazada'
                       ELSE N'borrador'
                  END AS EstadoNuevo
             FROM dbo.PropuestasCambioFestival p) AS origen
       ON 1 = 0
    WHEN NOT MATCHED THEN
        INSERT (ModuloId, RegistroId, SubregistroId, SubregistroResultanteId, Estado,
                IdOrganizacion, IdUsuarioProponente,
                FechaCreacion, FechaActualizacion, FechaEnvio, FechaDecision)
        VALUES (N'festivales', CONVERT(nvarchar(120), origen.FestivalOrigenId),
                CONVERT(nvarchar(120), origen.VersionOrigenId),
                CASE WHEN origen.VersionNuevaId IS NULL THEN NULL
                     ELSE CONVERT(nvarchar(120), origen.VersionNuevaId) END,
                origen.EstadoNuevo,
                origen.OrganizacionId, origen.PersonaProponenteId,
                origen.FechaPropuesta, origen.FechaActualizacion,
                -- UN BORRADOR NO SE HA ENVIADO, y el CHECK de la tabla nueva lo exige: si la fila
                -- vieja tuviera fecha de envío con estado borrador, aquí se descarta.
                CASE WHEN origen.EstadoNuevo = N'borrador' THEN NULL
                     ELSE ISNULL(origen.FechaEnvioRevision, origen.FechaPropuesta) END,
                -- Y solo las dos que cierran llevan fecha de decisión.
                CASE WHEN origen.EstadoNuevo IN (N'aplicada', N'rechazada')
                     THEN ISNULL(origen.FechaActualizacion, origen.FechaPropuesta) ELSE NULL END)
    OUTPUT origen.IdPropuestaCambioFestival, inserted.IdPropuesta INTO @equivalencias (IdViejo, IdNuevo);

    -- ── LOS CAMPOS: SOLO LOS QUE SE DIFERENCIAN DE LA VERSION DE ORIGEN ─────────────────────────
    --
    -- Se comparan como texto y tratando la cadena vacía como ausencia, que es como los compara el
    -- servidor: `NULL` y `''` son «no hay valor», y marcarlos como un cambio llenaría el expediente
    -- de diferencias que nadie propuso.
    ;WITH pares AS (
        SELECT e.IdNuevo, c.SeccionId, c.CampoId, c.Etiqueta,
               NULLIF(LTRIM(RTRIM(c.Antes)), N'')    AS Antes,
               NULLIF(LTRIM(RTRIM(c.Propuesto)), N'') AS Propuesto
          FROM dbo.PropuestasCambioFestival p
          JOIN @equivalencias e ON e.IdViejo = p.IdPropuestaCambioFestival
          JOIN dbo.VersionesFestival v ON v.IdVersionFestival = p.VersionOrigenId
         CROSS APPLY (VALUES
            (N'generales',   N'nombre',                N'Nombre del festival',        CONVERT(nvarchar(max), v.Nombre),               CONVERT(nvarchar(max), p.Nombre)),
            (N'generales',   N'descripcion',           N'Descripción',                CONVERT(nvarchar(max), v.Descripcion),          CONVERT(nvarchar(max), p.Descripcion)),
            (N'generales',   N'periodicidad',          N'Periodicidad',               CONVERT(nvarchar(max), v.Periodicidad),         CONVERT(nvarchar(max), p.Periodicidad)),
            (N'generales',   N'periodicidadDetalle',   N'Detalle de la periodicidad', CONVERT(nvarchar(max), v.PeriodicidadDetalle),  CONVERT(nvarchar(max), p.PeriodicidadDetalle)),
            (N'territorio',  N'nivelCobertura',        N'Alcance territorial',        CONVERT(nvarchar(max), v.NivelCobertura),       CONVERT(nvarchar(max), p.NivelCobertura)),
            (N'territorio',  N'codigoDepartamento',    N'Departamento',               CONVERT(nvarchar(max), v.CodigoDepartamento),   CONVERT(nvarchar(max), p.CodigoDepartamento)),
            (N'territorio',  N'codigoMunicipio',       N'Municipio',                  CONVERT(nvarchar(max), v.CodigoMunicipio),      CONVERT(nvarchar(max), p.CodigoMunicipio)),
            (N'contacto',    N'correoContacto',        N'Correo de contacto',         CONVERT(nvarchar(max), v.CorreoContacto),       CONVERT(nvarchar(max), p.CorreoContacto)),
            (N'contacto',    N'telefonoContacto',      N'Teléfono de contacto',       CONVERT(nvarchar(max), v.TelefonoContacto),     CONVERT(nvarchar(max), p.TelefonoContacto)),
            (N'contacto',    N'instagram',             N'Instagram',                  CONVERT(nvarchar(max), v.Instagram),            CONVERT(nvarchar(max), p.Instagram)),
            (N'contacto',    N'facebook',              N'Facebook',                   CONVERT(nvarchar(max), v.Facebook),             CONVERT(nvarchar(max), p.Facebook)),
            (N'contacto',    N'sitioWeb',              N'Sitio web',                  CONVERT(nvarchar(max), v.SitioWeb),             CONVERT(nvarchar(max), p.SitioWeb)),
            (N'contacto',    N'otroEnlace',            N'Otro enlace',                CONVERT(nvarchar(max), v.OtroEnlace),           CONVERT(nvarchar(max), p.OtroEnlace)),
            (N'contacto',    N'observacionesContacto', N'Observaciones de contacto',  CONVERT(nvarchar(max), v.ObservacionesContacto), CONVERT(nvarchar(max), p.ObservacionesContacto)),
            (N'organizador', N'director',              N'Director o directora',       CONVERT(nvarchar(max), v.Director),             CONVERT(nvarchar(max), p.Director)),
            (N'organizador', N'tipoOrganizadorId',     N'Tipo de organización',       CONVERT(nvarchar(max), v.TipoOrganizadorId),    CONVERT(nvarchar(max), p.TipoOrganizadorId))
         ) AS c(SeccionId, CampoId, Etiqueta, Antes, Propuesto)
    )
    INSERT INTO dbo.PropuestasDeCambioCampos
        (IdPropuesta, SeccionId, CampoId, CampoEtiqueta, ValorAnterior, ValorPropuesto, FechaCreacion)
    SELECT IdNuevo, SeccionId, CampoId, Etiqueta, Antes, Propuesto, SYSUTCDATETIME()
      FROM pares
     WHERE (Antes IS NULL AND Propuesto IS NOT NULL)
        OR (Antes IS NOT NULL AND Propuesto IS NULL)
        OR (Antes <> Propuesto);

    -- ── LAS DOS LISTAS DE CATALOGO ──────────────────────────────────────────────────────────────
    --
    -- Se comparan ya montadas: identificadores separados por comas y en orden, que es la forma en
    -- que las guarda el lado de Mercados. Comparar conjunto contra conjunto evitaría depender del
    -- orden, pero guardarlas ordenadas hace que la comparación de texto baste y sea legible.
    ;WITH listas AS (
        SELECT e.IdNuevo, l.CampoId, l.Etiqueta, l.Antes, l.Propuesto
          FROM dbo.PropuestasCambioFestival p
          JOIN @equivalencias e ON e.IdViejo = p.IdPropuestaCambioFestival
         CROSS APPLY (VALUES
            (N'practicasMusicales', N'Prácticas musicales',
             (SELECT STRING_AGG(CONVERT(nvarchar(20), x.PracticaMusicalId), N',') WITHIN GROUP (ORDER BY x.PracticaMusicalId)
                FROM dbo.VersionesFestivalPracticasMusicales x WHERE x.VersionFestivalId = p.VersionOrigenId),
             (SELECT STRING_AGG(CONVERT(nvarchar(20), y.PracticaMusicalId), N',') WITHIN GROUP (ORDER BY y.PracticaMusicalId)
                FROM dbo.PropuestasCambioFestivalPracticasMusicales y WHERE y.PropuestaCambioFestivalId = p.IdPropuestaCambioFestival)),
            (N'territoriosSonoros', N'Territorios sonoros',
             (SELECT STRING_AGG(CONVERT(nvarchar(20), x.TerritorioSonoroId), N',') WITHIN GROUP (ORDER BY x.TerritorioSonoroId)
                FROM dbo.VersionesFestivalTerritoriosSonoros x WHERE x.VersionFestivalId = p.VersionOrigenId),
             (SELECT STRING_AGG(CONVERT(nvarchar(20), y.TerritorioSonoroId), N',') WITHIN GROUP (ORDER BY y.TerritorioSonoroId)
                FROM dbo.PropuestasCambioFestivalTerritoriosSonoros y WHERE y.PropuestaCambioFestivalId = p.IdPropuestaCambioFestival))
         ) AS l(CampoId, Etiqueta, Antes, Propuesto)
    )
    INSERT INTO dbo.PropuestasDeCambioCampos
        (IdPropuesta, SeccionId, CampoId, CampoEtiqueta, ValorAnterior, ValorPropuesto, FechaCreacion)
    SELECT IdNuevo, N'practicas', CampoId, Etiqueta, Antes, Propuesto, SYSUTCDATETIME()
      FROM listas
     WHERE (Antes IS NULL AND Propuesto IS NOT NULL)
        OR (Antes IS NOT NULL AND Propuesto IS NULL)
        OR (Antes <> Propuesto);

    DECLARE @propuestasCopiadas int = (SELECT COUNT(*) FROM @equivalencias);
    DECLARE @camposCopiados     int = (SELECT COUNT(*) FROM dbo.PropuestasDeCambioCampos c
                                        JOIN dbo.PropuestasDeCambio d ON d.IdPropuesta = c.IdPropuesta
                                       WHERE d.ModuloId = N'festivales');

    PRINT CONCAT('[V20260917_03] Propuestas: ', @propuestasAntes, ' en origen, ', @propuestasCopiadas, ' copiadas.');
    PRINT CONCAT('[V20260917_03] Campos que cambian, calculados contra su version de origen: ', @camposCopiados, '.');

    IF @propuestasCopiadas <> @propuestasAntes
    BEGIN
        THROW 50002, 'La copia de propuestas de Festival no cuadra con el origen. No se aplica.', 1;
    END
END;
GO
