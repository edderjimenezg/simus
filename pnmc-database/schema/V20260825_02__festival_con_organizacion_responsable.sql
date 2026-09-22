/*
    PNMC · Ecosistema · Todo festival tiene organizacion responsable

    QUE HACE
    --------
    1. Marca cual de las entidades es LA INSTITUCIONAL (`Entidades.EsInstitucional`), con un
       indice unico filtrado que impide que haya dos.
    2. Obliga a que `Festivales.OrganizacionPrincipalId` deje de admitir NULL.

    LA DECISION QUE HAY DETRAS, Y POR QUE NO ES SOLO TECNICA
    --------------------------------------------------------
    Los 35 festivales historicos no tenian organizacion responsable, y sobre ese NULL estaba
    construida una funcionalidad viva: `/externo/organizaciones/{id}/festivales/coincidencias`
    ofrece a una organizacion los festivales publicados SIN DUENO para que los reclame.
    Prohibir el NULL a secas habria dejado esa lista vacia para siempre y matado el reclamo en
    silencio.

    Lo que se decidio (25 ago 2026) es que **un festival nunca esta sin nadie que responda por
    el**: mientras ninguna organizacion de la comunidad lo reclame, responde la institucion. Asi
    que «adoptable» deja de significar «no tiene dueno» y pasa a significar «hoy lo tiene la
    institucion». Reclamar es un traspaso, no una apropiacion de algo abandonado — y eso es
    ademas lo que la ficha publica puede decir sin mentir.

    POR QUE UNA COLUMNA Y NO UN ID CABLEADO
    ---------------------------------------
    El predicado del reclamo necesita saber cual es la entidad institucional. Escribir `== 1` en
    el codigo ata la aplicacion al orden en que se sembro una base concreta. `EsInstitucional`
    lo dice en la tabla, y el indice unico filtrado impide la unica forma de romperlo, que es
    que alguien marque una segunda.

    LA CONVERGENCIA EN DOS PASADAS, DICHA EN VOZ ALTA
    -------------------------------------------------
    `seed-local-db.sh` aplica schema/ ANTES que seed/. Sobre la base que ya existe, en la primera
    pasada todavia hay 35 filas en NULL cuando este guion corre, asi que el ALTER se salta a si
    mismo; despues las semillas reescriben los festivales ya con dueno; y en la SEGUNDA pasada el
    ALTER encuentra cero NULL y aplica. Sobre una base nueva `Festivales` esta vacia y aplica a la
    primera.

    Se hace asi —y no con un UPDATE aqui— porque este fichero es DDL y en el momento en que corre
    no existe todavia ni el usuario creador ni la entidad institucional a la que apuntar: los crea
    `seed/V20260519_03`. Un guion de esquema que necesita datos sembrados para funcionar esta
    puesto en el sitio equivocado.

    LAS DOS PASADAS SALEN 0 EN CUALQUIER CASO, que es lo que exige el criterio de aceptacion y lo
    que exige la siembra local.

    ESTE FICHERO TIENE QUE ESTAR EN LA LISTA `SCHEMAS` DE scripts/seed-local-db.sh.
*/
SET QUOTED_IDENTIFIER ON;
GO

-- ---------------------------------------------------------------------------------------
-- 1. Cual es la entidad institucional
-- ---------------------------------------------------------------------------------------

IF COL_LENGTH(N'dbo.Entidades', N'EsInstitucional') IS NULL
BEGIN
    ALTER TABLE dbo.Entidades
        ADD EsInstitucional bit NOT NULL
            CONSTRAINT DF_Entidades_EsInstitucional DEFAULT (0) WITH VALUES;
END;
GO

-- Solo puede haber una. Filtrado y no un CHECK porque la restriccion es sobre el conjunto de
-- filas, no sobre una fila: un CHECK no puede ver a las demas.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'UQ_Entidades_EsInstitucional'
                 AND object_id = OBJECT_ID(N'dbo.Entidades', N'U'))
BEGIN
    EXEC(N'CREATE UNIQUE INDEX UQ_Entidades_EsInstitucional
        ON dbo.Entidades (EsInstitucional)
        WHERE EsInstitucional = 1;');
END;
GO

-- ---------------------------------------------------------------------------------------
-- 2. Ningun festival sin organizacion responsable
-- ---------------------------------------------------------------------------------------

/*
    EL INDICE HAY QUE SOLTARLO Y REHACERLO, y conviene decir por que en vez de dejarlo como un
    conjuro. `IX_Festivales_OrganizacionPrincipalId_EstadoRegistro` (V20260823_02:278) tiene esta
    columna como primera clave, y SQL Server rechaza `ALTER COLUMN` sobre cualquier columna de la
    que dependa un indice:

        The index '...' is dependent on column 'OrganizacionPrincipalId'.

    Se suelta, se cambia la nulabilidad y se vuelve a crear IDENTICO. No se aprovecha para
    cambiarlo: sirve a la bandeja de una organizacion —sus festivales por estado— y ese uso no
    cambia aqui.

    Va todo dentro de la misma guarda para que la reconstruccion no ocurra si el ALTER no va a
    ocurrir: soltar un indice y rehacerlo en cada pasada seria trabajo inutil sobre una tabla que
    ya esta como se quiere.
*/
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_SCHEMA = N'dbo'
             AND TABLE_NAME = N'Festivales'
             AND COLUMN_NAME = N'OrganizacionPrincipalId'
             AND IS_NULLABLE = N'YES')
   AND NOT EXISTS (SELECT 1 FROM dbo.Festivales WHERE OrganizacionPrincipalId IS NULL)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_Festivales_OrganizacionPrincipalId_EstadoRegistro'
                 AND object_id = OBJECT_ID(N'dbo.Festivales', N'U'))
    BEGIN
        DROP INDEX IX_Festivales_OrganizacionPrincipalId_EstadoRegistro ON dbo.Festivales;
    END;

    ALTER TABLE dbo.Festivales
        ALTER COLUMN OrganizacionPrincipalId int NOT NULL;

    EXEC(N'CREATE INDEX IX_Festivales_OrganizacionPrincipalId_EstadoRegistro
        ON dbo.Festivales (OrganizacionPrincipalId, EstadoRegistro);');
END;
GO
