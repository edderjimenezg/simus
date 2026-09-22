/*
    Validaciones basicas de articulacion y lectura comun PNMC.
*/

SELECT 'RegistrosEcosistema' AS Tabla, COUNT(*) AS Registros FROM dbo.RegistrosEcosistema
UNION ALL SELECT 'RegistrosEcosistemaTerritoriosSonoros', COUNT(*) FROM dbo.RegistrosEcosistemaTerritoriosSonoros
UNION ALL SELECT 'RegistrosEcosistemaPracticasMusicales', COUNT(*) FROM dbo.RegistrosEcosistemaPracticasMusicales
UNION ALL SELECT 'MetricasDepartamentoMapa', COUNT(*) FROM dbo.MetricasDepartamentoMapa
UNION ALL SELECT 'MetricasMunicipioMapa', COUNT(*) FROM dbo.MetricasMunicipioMapa
UNION ALL SELECT 'AgendaEtiquetas', COUNT(*) FROM dbo.AgendaEtiquetas
UNION ALL SELECT 'AgendaArchivos', COUNT(*) FROM dbo.AgendaArchivos
UNION ALL SELECT 'NoticiasEtiquetas', COUNT(*) FROM dbo.NoticiasEtiquetas
UNION ALL SELECT 'NoticiasArchivos', COUNT(*) FROM dbo.NoticiasArchivos
UNION ALL SELECT 'AlbumesGaleriaEtiquetas', COUNT(*) FROM dbo.AlbumesGaleriaEtiquetas
UNION ALL SELECT 'AlbumesGaleriaArchivos', COUNT(*) FROM dbo.AlbumesGaleriaArchivos;

SELECT IdTipoRegistroEcosistema, IdRegistroOrigen, COUNT(*) AS Duplicados
FROM dbo.RegistrosEcosistema
GROUP BY IdTipoRegistroEcosistema, IdRegistroOrigen
HAVING COUNT(*) > 1;

SELECT TOP 10
    d.NombreDepartamento,
    m.CodigoDepartamento,
    m.EscuelasActivas,
    m.TotalEstudiantesEscuelas,
    m.FestivalesRegistrados,
    m.MercadosRegistrados,
    m.RedesDocumentacionActivas,
    m.LutieresRegistrados
FROM dbo.MetricasDepartamentoMapa m
INNER JOIN (
    SELECT CodigoDepartamento, MIN(NombreDepartamento) AS NombreDepartamento
    FROM dbo.Divipola
    GROUP BY CodigoDepartamento
) d ON d.CodigoDepartamento = m.CodigoDepartamento
WHERE m.EscuelasActivas > 0
   OR m.FestivalesRegistrados > 0
   OR m.MercadosRegistrados > 0
   OR m.RedesDocumentacionActivas > 0
   OR m.LutieresRegistrados > 0
ORDER BY d.NombreDepartamento;

SELECT name AS LlaveForanea
FROM sys.foreign_keys
WHERE OBJECT_NAME(parent_object_id) IN (
    'RegistrosEcosistema', 'RegistrosEcosistemaTerritoriosSonoros',
    'RegistrosEcosistemaPracticasMusicales', 'MetricasMunicipioMapa',
    'AgendaEtiquetas', 'AgendaArchivos', 'NoticiasEtiquetas',
    'NoticiasArchivos', 'AlbumesGaleriaEtiquetas', 'AlbumesGaleriaArchivos'
)
ORDER BY name;
