/*
    SIMUS · Banco de archivos · De quién es cada archivo

    QUE CIERRA. `dbo.Archivos` guarda quien lo subio (`IdUsuarioCarga`) y no de que ORGANIZACION
    venia. Mientras el unico canal de subida fue el institucional daba igual: todos los archivos
    eran del PNMC. Al abrir el banco al canal externo, esa columna pasa
    a hacer falta por dos motivos distintos:

      1. PROCEDENCIA. Es la regla transversal del proyecto: todo registro guarda de donde viene,
         quien lo hizo y quien responde por el, y las tres son cosas distintas. Sin esto, un afiche
         subido por una organizacion quedaria indistinguible de uno cargado por el Programa.

      2. CUOTA. El canal institucional es de confianza y no necesita tope; el externo si. Sin saber
         de que organizacion es cada archivo no hay forma de decir «esta ya subio demasiado», y el
         banco queda expuesto a que una cuenta lo llene a dos megas por vez.

    ES NULA A PROPOSITO. Los archivos que ya existen los subio el Programa desde la consola y no
    pertenecen a ninguna organizacion externa; ponerles una a posteriori seria inventar una
    procedencia. Nulo aqui significa «institucional», que es lo que de verdad eran.

    NO ES DESTRUCTIVO: solo anade una columna anulable, su foranea y un indice.
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH(N'dbo.Archivos', N'OrganizacionId') IS NULL
BEGIN
    ALTER TABLE dbo.Archivos ADD OrganizacionId int NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Archivos_Organizacion')
BEGIN
    ALTER TABLE dbo.Archivos ADD CONSTRAINT FK_Archivos_Organizacion
        FOREIGN KEY (OrganizacionId) REFERENCES dbo.Entidades(IdEntidad);
END;
GO

/*
  EL INDICE ES PARA LA CUOTA, que es una suma por organizacion en cada subida. Sin el, cada intento
  recorreria la tabla entera de archivos para responder «cuanto lleva esta».
*/
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Archivos_Organizacion')
BEGIN
    CREATE INDEX IX_Archivos_Organizacion ON dbo.Archivos(OrganizacionId) WHERE OrganizacionId IS NOT NULL;
END;
GO
