/*
  Descripciones breves verificadas. Fuente: Ministerio de Cultura, Oferta institucional
  para entidades territoriales, 2023, pp. 31–32.
  No se completan los territorios sin delimitación explícita en esa fuente.
*/
DECLARE @Fuente nvarchar(max) = N'Ministerio de Cultura, Oferta institucional para entidades territoriales (2023), pp. 31–32. https://mng.mincultura.gov.co/prensa/noticias/Documents/Comunicaciones/2023/Kit-nuevos-mandatarios-QR/h.Oferta%20Institucional%20para%20Entidades%20Territoriales.pdf';

UPDATE ficha SET DefinicionBreve = datos.Texto, RelacionTerritorial = datos.Relacion, Fuentes = @Fuente, FechaActualizacion = SYSUTCDATETIME()
FROM dbo.FichasConceptualesTerritoriosSonoros ficha
JOIN dbo.TerritoriosSonoros territorio ON territorio.IdTerritorioSonoro = ficha.TerritorioSonoroId
JOIN (VALUES
 (N'canta-y-torbellino', N'Territorio sonoro asociado a Canta y Torbellino en Boyacá, Cundinamarca, Norte de Santander y Santander.', N'Boyacá, Cundinamarca, Norte de Santander y Santander.'),
 (N'cantos-pitos-y-tambores', N'Territorio sonoro asociado a Cantos, Pitos y Tambores en Atlántico, Bolívar, Cesar, Córdoba, La Guajira, Magdalena, Sucre, norte de Antioquia y norte de Chocó.', N'Atlántico, Bolívar, Cesar, Córdoba, La Guajira, Magdalena, Sucre, norte de Antioquia y norte de Chocó.'),
 (N'chirimia', N'Territorio sonoro asociado a la Chirimía en el departamento del Chocó.', N'Chocó.'),
 (N'flautas-cuerdas-y-tambores-surenos', N'Territorio sonoro asociado a Flautas, Cuerdas y Tambores Sureños en Cauca, Nariño y Putumayo.', N'Cauca, Nariño y Putumayo.'),
 (N'joropo', N'Territorio sonoro asociado al Joropo en Arauca, Casanare, Meta y Vichada.', N'Arauca, Casanare, Meta y Vichada.'),
 (N'marimba', N'Territorio sonoro asociado a la Marimba en Cauca, Nariño y Valle del Cauca.', N'Cauca, Nariño y Valle del Cauca.'),
 (N'rajalena-y-cucamba', N'Territorio sonoro asociado a Rajaleña y Cucamba en Huila, Tolima y Caquetá.', N'Huila, Tolima y Caquetá.'),
 (N'trova-y-parranda', N'Territorio sonoro asociado a Trova y Parranda en Antioquia, Caldas, Quindío y Risaralda.', N'Antioquia, Caldas, Quindío y Risaralda.'),
 (N'insular', N'Territorio sonoro insular de San Andrés, Providencia y Santa Catalina.', N'San Andrés, Providencia y Santa Catalina.'),
 (N'amazonas', N'Territorio sonoro de Amazonía en Caquetá, Guaviare, Vaupés, Guainía y Amazonas.', N'Caquetá, Guaviare, Vaupés, Guainía y Amazonas.')
) datos(Slug, Texto, Relacion) ON datos.Slug = territorio.Slug;
