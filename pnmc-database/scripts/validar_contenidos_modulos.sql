/*
    Validaciones basicas de contenidos y modulos PNMC.
*/

SELECT 'Agenda' AS Tabla, COUNT(*) AS Registros FROM dbo.Agenda
UNION ALL SELECT 'Noticias', COUNT(*) FROM dbo.Noticias
UNION ALL SELECT 'AlbumesGaleria', COUNT(*) FROM dbo.AlbumesGaleria
UNION ALL SELECT 'Festivales', COUNT(*) FROM dbo.Festivales
UNION ALL SELECT 'EscuelasMusica', COUNT(*) FROM dbo.EscuelasMusica
UNION ALL SELECT 'MercadosMusicales', COUNT(*) FROM dbo.MercadosMusicales
UNION ALL SELECT 'RedesDocumentacion', COUNT(*) FROM dbo.RedesDocumentacion
UNION ALL SELECT 'Lutieres', COUNT(*) FROM dbo.Lutieres;

SELECT 'Agenda sin usuario creador valido' AS Validacion, COUNT(*) AS Hallazgos
FROM dbo.Agenda a
LEFT JOIN dbo.Usuarios u ON u.IdUsuario = a.IdUsuarioCreador
WHERE u.IdUsuario IS NULL
UNION ALL
SELECT 'Noticias con slug duplicado', COUNT(*)
FROM (
    SELECT Slug
    FROM dbo.Noticias
    GROUP BY Slug
    HAVING COUNT(*) > 1
) duplicados
UNION ALL
SELECT 'Festivales municipales sin Divipola valida', COUNT(*)
FROM dbo.Festivales f
LEFT JOIN dbo.Divipola d ON d.CodigoDepartamento = f.CodigoDepartamento AND d.CodigoMunicipio = f.CodigoMunicipio
WHERE f.NivelCobertura = N'municipal' AND d.CodigoMunicipio IS NULL
UNION ALL
SELECT 'Escuelas municipales sin Divipola valida', COUNT(*)
FROM dbo.EscuelasMusica e
LEFT JOIN dbo.Divipola d ON d.CodigoDepartamento = e.CodigoDepartamento AND d.CodigoMunicipio = e.CodigoMunicipio
WHERE e.NivelCobertura = N'municipal' AND d.CodigoMunicipio IS NULL;

SELECT name AS LlaveForanea
FROM sys.foreign_keys
WHERE OBJECT_NAME(parent_object_id) IN (
    'Agenda', 'Noticias', 'AlbumesGaleria', 'Festivales',
    'EscuelasMusica', 'MercadosMusicales', 'RedesDocumentacion', 'Lutieres'
)
ORDER BY name;
