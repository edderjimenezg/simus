-- =============================================================================================
-- El boletín deja de guardar su propia autorización (sin borrar nada)
-- =============================================================================================
--
-- QUE HACE. Suelta la CHECK `CK_BoletinSuscripciones_Autorizacion` y hace anulables
-- `AutorizacionOtorgada` y `AutorizacionTexto`. Nada más. No borra una fila ni una columna.
--
-- POR QUE HACE FALTA. Desde `V20260912_07` la autorización del boletín vive en
-- `dbo.AutorizacionesDatos`, con su texto, su versión y su fecha de revocación, igual que las
-- otras dos finalidades del sistema. El código ya no escribe las dos columnas de esta tabla. Pero
-- las dos son NOT NULL y sin valor por omisión, y encima hay una CHECK que exige
-- `AutorizacionOtorgada = 1`: cualquier alta nueva fallaría al insertar. La suscripción se queda
-- con lo que de verdad es suyo —a qué correo se envía, desde cuándo y si sigue activa— y deja de
-- ser también el sitio donde se prueba el consentimiento.
--
-- POR QUE ESTE GUION NO RETIRA LAS COLUMNAS NI LAS TABLAS ANTERIORES. Porque retirar y trasladar
-- no deben ocurrir en el mismo paso, y aquí hay un motivo de más: lo que se está moviendo son
-- declaraciones de voluntad de personas reales sobre sus datos personales, y no es información que
-- se pueda volver a generar si algo sale mal. Entre copiar y borrar tiene que caber una
-- comprobación de que el código lee de verdad de la estructura nueva.
--
-- Con este guion aplicado, el sistema funciona entero sobre `AutorizacionesDatos` y la estructura
-- anterior sigue ahí, intacta y consultable. El retiro va en `V20260912_09`, que es destructivo y
-- se aplica cuando se decida, no como efecto colateral de poner esto en marcha.
--
-- ES REVERSIBLE. Volver atrás es reponer la CHECK y el NOT NULL; ninguna fila cambia de valor.

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.BoletinSuscripciones') AND name = N'AutorizacionTexto')
BEGIN
    /*
      LA GUARDA: no se toca nada mientras el traslado no esté hecho. Si alguna suscripción con
      autorización otorgada no tiene su fila en el registro, este guion se detiene y la tabla se
      queda exactamente como estaba, todavía obligando a guardar la evidencia aquí.
    */
    DECLARE @conAutorizacion int = (SELECT COUNT(*) FROM dbo.BoletinSuscripciones WHERE AutorizacionOtorgada = 1);
    DECLARE @trasladadas int = (SELECT COUNT(*) FROM dbo.AutorizacionesDatos WHERE Finalidad = N'boletin');

    IF @trasladadas < @conAutorizacion
        THROW 51002, N'El traslado de autorizaciones del boletin no esta completo: no se relaja nada.', 1;

    /*
      PRIMERO LA CHECK. Exigía `AutorizacionOtorgada = 1` en toda fila: es la regla que
      `dbo.AutorizacionesDatos` sustituye, y mientras exista ninguna suscripción puede entrar sin
      declarar aquí una autorización que ya no se guarda aquí.
    */
    ALTER TABLE dbo.BoletinSuscripciones DROP CONSTRAINT IF EXISTS CK_BoletinSuscripciones_Autorizacion;

    ALTER TABLE dbo.BoletinSuscripciones ALTER COLUMN AutorizacionOtorgada bit NULL;
    ALTER TABLE dbo.BoletinSuscripciones ALTER COLUMN AutorizacionTexto nvarchar(400) NULL;
END
GO
