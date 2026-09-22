/*
  SIMUS · Catálogo Editorial — los enlaces que no llevaban a ningún sitio

  QUE PASABA. De los 38 accesos de tipo `enlace` que trajo el acervo, TRECE no eran una dirección:
  eran una celda de hoja de cálculo con más cosas dentro. Dos formas distintas, las dos medidas:

    · La anotación de quien catalogó, pegada detrás de la dirección:
      «http://…/clarinete.pdf Revisada el 05 de febrero de w2018».
    · Varias direcciones en una sola celda, separadas por « ; »: una ficha trae tres vídeos de
      YouTube y otra cuatro sitios de «Así suena…».

  POR QUE IMPORTA. Un `<a href>` construido con eso no abre nada: el navegador se lleva la frase
  entera como parte de la dirección. Y un enlace que no lleva a ningún sitio es peor que no
  ofrecerlo, porque quien consulta cree que la obra no está y en realidad sí está.

  QUE HACE ESTE GUION. Primero separa: cada dirección de una celda múltiple pasa a ser su propio
  acceso, que es lo que era. Después limpia: de cada acceso se queda la dirección —hasta el primer
  espacio— y lo que sobra se guarda como `Nota`, que es donde vive una anotación de catalogación.
  No se pierde nada de lo que escribió quien catalogó; cambia de sitio.

  EL SEPARADOR ES CHAR(30) Y NO UNA BARRA. `STRING_SPLIT` en SQL Server 2019 solo admite un
  carácter, así que el « ; » se sustituye antes por uno que no puede aparecer en una dirección: el
  separador de registro de ASCII. Con «|» habría riesgo, aunque sea pequeño, de partir una URL que
  lo lleve dentro.

  IDEMPOTENTE: al volver a pasarlo no hay celdas con « ; » —ya se separaron— ni direcciones con
  espacios —ya se limpiaron—, así que las dos sentencias no alcanzan ninguna fila.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

/* ─── 1. Cada dirección de una celda múltiple pasa a ser su propio acceso ─── */
;WITH Multiples AS (
    SELECT a.IdAccesoEditorial, a.PublicacionEditorialId, a.Etiqueta, a.Url
    FROM dbo.AccesosEditoriales a
    WHERE a.Tipo = N'enlace' AND a.Url LIKE N'% ; %'
),
Trozos AS (
    SELECT m.PublicacionEditorialId, m.Etiqueta,
           LTRIM(RTRIM(s.value)) AS Direccion,
           ROW_NUMBER() OVER (PARTITION BY m.IdAccesoEditorial ORDER BY (SELECT NULL)) AS Posicion
    FROM Multiples m
    CROSS APPLY STRING_SPLIT(REPLACE(m.Url, N' ; ', CHAR(30)), CHAR(30)) s
    WHERE LTRIM(RTRIM(s.value)) <> N''
)
INSERT INTO dbo.AccesosEditoriales (PublicacionEditorialId, Tipo, Url, Etiqueta, Orden,
                                    DerechosEstado, DerechosPermitePublicarFicha, DerechosPermitePublicarArchivo)
SELECT t.PublicacionEditorialId, N'enlace', LEFT(t.Direccion, 1000), t.Etiqueta, 100 + t.Posicion,
       /* Heredan los derechos que ya tiene el acceso del que salieron: son la misma obra. */
       N'verificado', 1, 0
FROM Trozos t
WHERE t.Posicion > 1
  AND NOT EXISTS (SELECT 1 FROM dbo.AccesosEditoriales x
                  WHERE x.PublicacionEditorialId = t.PublicacionEditorialId
                    AND x.Tipo = N'enlace' AND x.Url = LEFT(t.Direccion, 1000));
GO

/* ─── 2. La dirección se queda sola; la anotación pasa a `Nota` ─── */
UPDATE dbo.AccesosEditoriales
SET Nota = LEFT(
        LTRIM(SUBSTRING(Url, CHARINDEX(N' ', Url) + 1, LEN(Url))) +
        CASE WHEN Nota IS NULL THEN N'' ELSE N' · ' + Nota END, 600),
    Url = LEFT(Url, CHARINDEX(N' ', Url) - 1)
WHERE Tipo = N'enlace'
  AND Url LIKE N'% %'
  AND CHARINDEX(N' ', Url) > 1;
GO

/* ─── 3. Lo que después de limpiar no es una dirección, no se ofrece como enlace ───
   Nada debería caer aquí; existe para que una celda con texto suelto no se convierta en un enlace
   roto en silencio. Se conserva la fila como nota de consulta, sin dirección. */
UPDATE dbo.AccesosEditoriales
SET Nota = LEFT(ISNULL(Url + N' · ', N'') + ISNULL(Nota, N''), 600),
    Url = NULL,
    Tipo = N'ubicacion'
WHERE Tipo = N'enlace'
  AND Url IS NOT NULL
  AND Url NOT LIKE N'http://%'
  AND Url NOT LIKE N'https://%';
GO
