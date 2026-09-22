/*
    PNMC - El alcance territorial de una organizacion puede estar SIN DEFINIR.

    POR QUE HACE FALTA UN CUARTO VALOR.  El territorio es del PROCESO, no de la organizacion: una
    misma fundacion monta festivales en municipios distintos, y en departamentos distintos, y el
    alcance de cada uno se declara al crearlo.  Preguntarle a la organizacion "cual es tu alcance"
    en el formulario de registro es pedirle que resuma en una palabra algo que todavia no ha
    ocurrido.

    QUE PASABA SIN ESTE VALOR.  `CK_Entidades_NivelCobertura` admitia tres, y de los tres solo
    `nacional` toleraba territorio vacio.  Un registro publico que no pregunta departamento ni
    municipio tenia entonces una sola salida legal: escribir `nacional` en toda organizacion que
    naciera desde fuera.  Habria sido barato -cero lineas- y habria dejado cada fundacion de un
    municipio declarada de alcance nacional en la base, alimentando la consola con una afirmacion
    que nadie hizo.  Un dato inventado es peor que un dato ausente, porque no se distingue de uno
    verdadero.

    DONDE SE VE, Y DONDE NO.  Ninguna ruta publica lee `Entidades.NivelCobertura`: el geovisor y
    los directorios se alimentan de las tablas de los seis procesos, que tienen su propia
    cobertura y la conservan intacta.  Este valor solo aparece en la consola institucional y en el
    panel de la propia organizacion, que es donde se completara.

    NO TOCA NINGUNA FILA EXISTENTE.  Las 17 entidades ya sembradas conservan su nivel; el valor
    nuevo solo lo escribe el alta externa a partir de hoy.
*/

IF EXISTS (SELECT 1 FROM sys.check_constraints
           WHERE name = N'CK_Entidades_NivelCobertura'
             AND parent_object_id = OBJECT_ID(N'dbo.Entidades', N'U'))
BEGIN
    ALTER TABLE dbo.Entidades DROP CONSTRAINT CK_Entidades_NivelCobertura;
END;
GO

/*
    LA RESTRICCION SE REHACE ENTERA, no se "amplia".  Una CHECK no se puede modificar en SQL
    Server: se suelta y se vuelve a crear.  Por eso el bloque de arriba va sin condicion sobre el
    contenido y este de abajo comprueba que no exista antes de crearla: las dos mitades juntas son
    idempotentes, que es lo que `scripts/seed-local-db.sh` necesita, porque aplica TODO `schema/`
    en cada ejecucion.

    LA REGLA DEL VALOR NUEVO ES LA MISMA QUE LA DE `nacional`: sin departamento y sin municipio.
    No es que "todavia no se sepa el territorio"; es que la organizacion no tiene uno propio hasta
    que alguien lo declare, y mientras tanto las dos columnas tienen que estar vacias para que
    nadie las lea como una ubicacion.
*/
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints
               WHERE name = N'CK_Entidades_NivelCobertura'
                 AND parent_object_id = OBJECT_ID(N'dbo.Entidades', N'U'))
BEGIN
    ALTER TABLE dbo.Entidades ADD CONSTRAINT CK_Entidades_NivelCobertura CHECK (
        NivelCobertura IN (N'sin_definir', N'nacional', N'departamental', N'municipal')
        AND (
            (NivelCobertura = N'sin_definir'   AND CodigoDepartamento IS NULL     AND CodigoMunicipio IS NULL)
            OR (NivelCobertura = N'nacional'      AND CodigoDepartamento IS NULL     AND CodigoMunicipio IS NULL)
            OR (NivelCobertura = N'departamental' AND CodigoDepartamento IS NOT NULL AND CodigoMunicipio IS NULL)
            OR (NivelCobertura = N'municipal'     AND CodigoDepartamento IS NOT NULL AND CodigoMunicipio IS NOT NULL)
        )
    );
END;
GO
