/*
    PNMC - Agrupaciones como tipo de entidad del ecosistema.

    POR QUE.
    El piloto Festival tiene un creador externo: la Agrupacion. Es quien registra y
    edita sus Festivales y sus versiones, y es una de las ocho categorias que el portal
    ya publica en /ecosistema (categorias-ecosistema.config.ts la declara como ACTOR,
    frente a Festivales que es PROCESO: los actores crean procesos).

    Pero CK_Entidades_Tipo cierra dbo.Entidades a ocho valores y 'agrupacion' no esta
    entre ellos:

        organizacion, escuela_musica, lutier, festival,
        mercado_musical, espacio, colectivo, individuo

    POR QUE NO SE REUTILIZA 'colectivo'.
    Porque seria sembrar exactamente el defecto que este mismo bloque de trabajo acaba
    de corregir. El codigo escribia 'EnRevision' donde la base solo admitia
    'en_revision', y esa discrepancia de una sola palabra dejo la cola de revision
    institucional muerta durante meses, sin error visible. La seccion se llama
    Agrupaciones, la pantalla dira Agrupaciones y la persona dira agrupacion: guardar
    otra palabra en la base es aceptar de nuevo que el vocabulario del producto y el de
    los datos no coincidan. Quince minutos ahora, o una traduccion silenciosa que
    alguien tendra que descubrir midiendo.

    ALCANCE.
    Aditivo y reversible. No cambia el significado de ningun tipo existente, no toca
    ninguna fila y no obliga a migrar nada: las entidades ya creadas siguen siendo
    'organizacion'. Solo amplia lo que la tabla acepta.

    IDEMPOTENTE.
    Se puede aplicar sobre una base nueva y sobre una que ya lo tenga. El guion original
    (V20260521_01) crea la tabla solo si no existe, de modo que en una base ya creada su
    lista de tipos no se actualiza sola; por eso la restriccion se rehace aqui en vez de
    editar aquel fichero, que es una migracion ya aplicada.
*/

IF OBJECT_ID(N'dbo.Entidades', N'U') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = N'CK_Entidades_Tipo'
          AND parent_object_id = OBJECT_ID(N'dbo.Entidades', N'U')
    )
        ALTER TABLE dbo.Entidades DROP CONSTRAINT CK_Entidades_Tipo;

    ALTER TABLE dbo.Entidades
    ADD CONSTRAINT CK_Entidades_Tipo CHECK (TipoEntidad IN (
        N'organizacion', N'agrupacion', N'escuela_musica', N'lutier', N'festival',
        N'mercado_musical', N'espacio', N'colectivo', N'individuo'
    ));
END;
