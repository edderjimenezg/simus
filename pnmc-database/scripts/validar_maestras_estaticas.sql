/*
    Validaciones basicas de bases maestras estaticas PNMC.
*/

SELECT 'Divipola' AS Tabla, COUNT(*) AS Registros FROM dbo.Divipola
UNION ALL
SELECT 'TerritoriosSonoros', COUNT(*) FROM dbo.TerritoriosSonoros
UNION ALL
SELECT 'PracticasMusicales', COUNT(*) FROM dbo.PracticasMusicales
UNION ALL
SELECT 'TiposRegistroEcosistema', COUNT(*) FROM dbo.TiposRegistroEcosistema;

SELECT CodigoMunicipio, COUNT(*) AS Repeticiones
FROM dbo.Divipola
GROUP BY CodigoMunicipio
HAVING COUNT(*) > 1;

SELECT CodigoDepartamento, COUNT(*) AS Municipios
FROM dbo.Divipola
GROUP BY CodigoDepartamento
ORDER BY CodigoDepartamento;

SELECT CodigoTipoRegistro, NombreTipoRegistro
FROM dbo.TiposRegistroEcosistema
ORDER BY IdTipoRegistroEcosistema;
