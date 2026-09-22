/*
    PNMC · Ecosistema · La historia no se reescribe

    POR QUE EXISTE
    -----------------------------
    `RevisionInstitucionalFestivalesEndpoints.cs` arma la ficha de revision resolviendo el
    nombre de la organizacion por JOIN CONTRA EL PRESENTE:

        var organizacion = await dbContext.EntityProfiles... .Select(item => item.Name)...

    y a esa misma respuesta le cuelga el historial entero. De modo que el dia en que una
    organizacion se cambie el nombre, TODAS sus entradas de historial pasadas —incluidas las de
    hace dos anos— empiezan a decir el nombre nuevo. Nadie ve el cambio: la pantalla sigue
    respondiendo 200 y la fila sigue ahi. Lo mismo vale para quien responde por la organizacion.

    LO QUE SE GUARDA, Y LO QUE SE DECIDE NO GUARDAR
    -----------------------------------------------
    Se copian al escribir cuatro datos: el identificador de la organizacion, su NOMBRE en ese
    momento, el nombre de quien RESPONDIA por ella en ese momento, y el nombre de quien HIZO la
    accion.

    NO se copia el numero de documento del responsable, y es una decision, no un olvido. El plan
    lo pedia. Una entrada de historial no necesita la cedula de un ciudadano para ser veraz sobre
    lo que paso: le basta con los nombres tal y como estaban. Copiarla en cada cambio de estado
    multiplicaria ese dato personal por todo el historial —decenas de filas por registro, sin
    caducidad y sin nadie que las mire—, que es justo lo contrario del principio de minimizacion
    de la Ley 1581 de 2012. El documento vigente vive en `EntidadesResponsable`, una sola vez, y
    ahi se consulta cuando de verdad hace falta.

    POR QUE COLUMNAS Y NO `MetadataJson`
    ------------------------------------
    La columna ya existe y habria sido mas rapido. Pero un dato dentro de un JSON no se puede
    consultar, ni indexar, ni comprobar; y sobre todo no se puede echar de menos: nadie nota que
    dejo de escribirse. Una columna con nombre propio se ve vacia.

    TODAS ANULABLES
    ---------------
    El historial que ya existe se escribio sin estos datos y no se van a inventar hacia atras. Una
    fila antigua con la instantanea vacia dice la verdad —«esto no se guardo entonces»—; rellenarla
    con el valor de hoy seria exactamente la falsificacion que este bloque existe para impedir.

    ESTE FICHERO TIENE QUE ESTAR EN LA LISTA `SCHEMAS` DE scripts/seed-local-db.sh.
*/
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH(N'dbo.RegistrosRevisionHistorial', N'OrganizacionId') IS NULL
BEGIN
    ALTER TABLE dbo.RegistrosRevisionHistorial ADD OrganizacionId int NULL;
END;
GO

IF COL_LENGTH(N'dbo.RegistrosRevisionHistorial', N'OrganizacionNombre') IS NULL
BEGIN
    ALTER TABLE dbo.RegistrosRevisionHistorial ADD OrganizacionNombre nvarchar(240) NULL;
END;
GO

IF COL_LENGTH(N'dbo.RegistrosRevisionHistorial', N'ResponsableNombre') IS NULL
BEGIN
    ALTER TABLE dbo.RegistrosRevisionHistorial ADD ResponsableNombre nvarchar(240) NULL;
END;
GO

IF COL_LENGTH(N'dbo.RegistrosRevisionHistorial', N'ActorNombre') IS NULL
BEGIN
    ALTER TABLE dbo.RegistrosRevisionHistorial ADD ActorNombre nvarchar(240) NULL;
END;
GO

/*
    SIN FORANEA A `Entidades` A PROPOSITO. `OrganizacionId` es parte de la instantanea, no un
    vinculo vivo: si manana esa organizacion se retira, la fila de historial tiene que seguir
    diciendo a nombre de quien se hizo aquello. Una foranea obligaria a lo contrario —borrar o
    anular la referencia— y el historial dejaria de contar lo que paso.
*/
