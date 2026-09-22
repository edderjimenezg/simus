/*
    SIMUS · Organizaciones · La foto de perfil

    QUE CIERRA. `dbo.Entidades` no tenia NINGUNA columna de imagen: ni logo, ni foto, ni avatar. En
    pantalla lo que se veia eran las iniciales sobre un circulo de color -«FD» para «Fundacion de
    Prueba»-, que es un respaldo razonable y era lo unico que habia.

    ES PUBLICA, y lo decidio la direccion de producto. Por eso apunta al
    banco de archivos, que sirve por `/publico/archivos/{id}` sin cuenta y con ETag: la misma
    imagen que la organizacion ve en su panel es la que puede acompanar sus Festivales en el portal.

    NO SE GUARDA UNA RUTA, SE GUARDA EL ARCHIVO. Una columna de texto con una direccion es confiar
    en que el fichero siga ahi el ano que viene y en que alguien la escribio bien; la foranea al
    banco hace que exista de verdad, con su tipo comprobado por firma y su texto alternativo.

    LAS INICIALES NO SE RETIRAN. Siguen siendo el respaldo de quien no sube foto, que sera la
    mayoria durante mucho tiempo.

    NO ES DESTRUCTIVO: una columna anulable y su foranea.
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH(N'dbo.Entidades', N'ArchivoFotoId') IS NULL
BEGIN
    ALTER TABLE dbo.Entidades ADD ArchivoFotoId int NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Entidades_ArchivoFoto')
BEGIN
    ALTER TABLE dbo.Entidades ADD CONSTRAINT FK_Entidades_ArchivoFoto
        FOREIGN KEY (ArchivoFotoId) REFERENCES dbo.Archivos(IdArchivo);
END;
GO
