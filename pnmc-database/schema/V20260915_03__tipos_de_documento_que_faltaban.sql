/*
  SIMUS · Los dos tipos de documento que el alta externa aceptaba y el catálogo no tenía

  LA REGLA. `ExternalOrganizationEndpoints` llevaba los tipos de documento escritos a mano y la
  lista tenía DIEZ; `dbo.TiposDocumento` tiene ocho. Los dos que faltaban son «Documento de
  identificación extranjero» (die) y «Carné diplomático» (cd). Es decir: el alta de una organización
  aceptaba dos códigos que el catálogo no reconoce, y cualquier pantalla que lea de la tabla —desde
  hoy, el perfil de una cuenta administrativa— enseñaría el código en crudo.

  SE AÑADEN A LA TABLA Y NO SE QUITAN DEL ALTA. Son tipos de documento reales del país; el defecto
  no es que el alta los acepte, es que el catálogo no los tenía.

  ES ADITIVA: dos filas nuevas, ninguna existente se toca.
*/

IF NOT EXISTS (SELECT 1 FROM dbo.TiposDocumento WHERE CodigoTipoDocumento = N'die')
    INSERT INTO dbo.TiposDocumento (CodigoTipoDocumento, NombreTipoDocumento, OrdenVisualizacion, Activo)
    VALUES (N'die', N'Documento de identificación extranjero', 9, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.TiposDocumento WHERE CodigoTipoDocumento = N'cd')
    INSERT INTO dbo.TiposDocumento (CodigoTipoDocumento, NombreTipoDocumento, OrdenVisualizacion, Activo)
    VALUES (N'cd', N'Carné diplomático', 10, 1);
GO
