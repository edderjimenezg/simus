-- =============================================================================================
-- Los tres dominios de contenido entran a la Importación Asistida
-- =============================================================================================
--
-- QUE HACE ESTE GUION. Amplía `CK_LotesImportacion_Dominio` para admitir `agenda` y
-- `catalogo-editorial`, que es lo único que la base necesita saber de un dominio nuevo.
--
-- POR QUE `noticias` NO SE AÑADE AQUI: ya estaba. El guion V20260912_05 lo dejó listado cuando el
-- núcleo genérico se construyó, y el API no lo implementaba. Es al revés de lo que suele fallar
-- —normalmente el código va por delante de la base— y el resultado era el mismo: una lista que no
-- decía la verdad. Desde esta versión los cuatro nombres de la CHECK tienen dominio detrás.
--
-- NO ES DESTRUCTIVO. Solo amplía lo que se admite; ninguna fila existente deja de ser válida.

SET QUOTED_IDENTIFIER ON;
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_LotesImportacion_Dominio')
    ALTER TABLE dbo.LotesImportacion DROP CONSTRAINT CK_LotesImportacion_Dominio;
GO

-- SE LISTAN Y NO SE ABRE LA COLUMNA, por el mismo motivo que la primera vez: un dominio sin
-- implementación no puede escribir lotes por error, y añadir uno obliga a pasar por aquí, que es
-- justo el sitio donde conviene acordarse de que hay que declararlo también en el API.
ALTER TABLE dbo.LotesImportacion ADD CONSTRAINT CK_LotesImportacion_Dominio
    CHECK (Dominio IN (N'festivales', N'organizaciones', N'noticias', N'agenda', N'catalogo-editorial'));
GO
