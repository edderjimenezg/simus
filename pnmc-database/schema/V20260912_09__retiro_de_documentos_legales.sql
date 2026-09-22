-- =============================================================================================
-- Se retira la estructura anterior de documentos legales, ya trasladada
-- =============================================================================================
--
-- ESTE ES EL UNICO GUION DESTRUCTIVO DE LOS TRES, y va el ultimo a proposito.
--
--   `V20260912_07` es enteramente aditivo: crea `dbo.PoliticasDatos` y `dbo.AutorizacionesDatos` y
--   copia dentro las 29 autorizaciones que ya existian, sin tocar nada de lo anterior.
--   `V20260912_08` solo relaja: suelta una CHECK y hace anulables dos columnas, para que el sistema
--   pueda funcionar entero sobre la estructura nueva con la anterior todavia intacta.
--   Este retira lo que, con los dos anteriores aplicados, ya no guarda nada que no este mejor
--   guardado en otro sitio.
--
-- Entre copiar y borrar cabe asi una comprobacion de que el codigo lee de verdad de las tablas
-- nuevas y de que ninguna cifra bailo. Solo entonces se retira.
--
-- Es la forma habitual de hacer un cambio destructivo de esquema —ampliar, trasladar, cambiar el
-- codigo, comprobar, contraer— y aqui tiene una razon de mas: lo que se esta moviendo son
-- declaraciones de voluntad de personas reales sobre sus datos personales. No es informacion que
-- se pueda volver a generar si sale mal.
--
-- QUE SE RETIRA.
--
--   `dbo.AceptacionesDocumentosLegales` y `dbo.DocumentosLegales`. Sus 28 filas viven ya en
--   `AutorizacionesDatos` y `PoliticasDatos`, con MAS informacion de la que tenian: el texto que
--   se mostro, la version copiada en la propia autorizacion, el correo del titular y la finalidad.
--   Conservarlas seria mantener una segunda respuesta, peor, a la pregunta que la estructura nueva
--   responde entera.
--
--   `dbo.BoletinSuscripciones.AutorizacionOtorgada` y `.AutorizacionTexto`. La suscripcion
--   conserva lo que de verdad es suyo —a que correo se envia, desde cuando, si sigue activa—; la
--   autorizacion que la ampara pasa a estar donde estan todas.
--
-- LA GUARDA NO ES CEREMONIA. Si el recuento de destino es menor que el de origen, el guion se
-- detiene y no retira nada: es preferible una base con las dos estructuras y una migracion en rojo
-- que una base limpia a la que le faltan autorizaciones de personas.

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'AceptacionesDocumentosLegales')
BEGIN
    DECLARE @origen int = (SELECT COUNT(*) FROM dbo.AceptacionesDocumentosLegales);
    DECLARE @destino int = (SELECT COUNT(*) FROM dbo.AutorizacionesDatos WHERE Origen = N'registro');

    IF @destino < @origen
        THROW 51001, N'El traslado de aceptaciones no esta completo: no se retira nada.', 1;

    DROP TABLE dbo.AceptacionesDocumentosLegales;
    DROP TABLE dbo.DocumentosLegales;
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.BoletinSuscripciones') AND name = N'AutorizacionTexto')
BEGIN
    DECLARE @conAutorizacion int = (SELECT COUNT(*) FROM dbo.BoletinSuscripciones WHERE AutorizacionOtorgada = 1);
    DECLARE @trasladadas int = (SELECT COUNT(*) FROM dbo.AutorizacionesDatos WHERE Finalidad = N'boletin');

    IF @trasladadas < @conAutorizacion
        THROW 51002, N'El traslado de autorizaciones del boletin no esta completo: no se retira nada.', 1;

    /*
      PRIMERO LAS DEPENDENCIAS. `CK_BoletinSuscripciones_Autorizacion` exigia que una suscripcion
      con autorizacion otorgada llevara texto: la regla que la tabla `AutorizacionesDatos`
      sustituye. Mientras exista, SQL Server no deja soltar la columna de la que habla.
    */
    ALTER TABLE dbo.BoletinSuscripciones DROP CONSTRAINT IF EXISTS CK_BoletinSuscripciones_Autorizacion;
    ALTER TABLE dbo.BoletinSuscripciones DROP CONSTRAINT IF EXISTS DF_BoletinSuscripciones_AutorizacionOtorgada;
    ALTER TABLE dbo.BoletinSuscripciones DROP COLUMN AutorizacionOtorgada, AutorizacionTexto;
END
GO
