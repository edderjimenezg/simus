-- ═══════════════════════════════════════════════════════════════════════════════════════════
-- OBSERVACIONES_CONTACTO EN LA PROPUESTA DE CAMBIO, mismo patrón que V20260828_02 ya aplicó a
-- dbo.Festivales y dbo.VersionesFestival: notas sobre CÓMO contactar, distintas de los datos de
-- contacto mismos. dbo.PropuestasCambioFestival ya tiene TelefonoContacto/Instagram/Facebook/
-- SitioWeb/OtroEnlace desde su creación, pero se quedó sin esta columna -por eso el PUT de
-- /propuesta-cambio no podía guardarla, aunque el propio contrato de la solicitud ya la traía-.
-- ═══════════════════════════════════════════════════════════════════════════════════════════

IF COL_LENGTH('dbo.PropuestasCambioFestival', 'ObservacionesContacto') IS NULL
    ALTER TABLE dbo.PropuestasCambioFestival ADD ObservacionesContacto nvarchar(600) NULL;
