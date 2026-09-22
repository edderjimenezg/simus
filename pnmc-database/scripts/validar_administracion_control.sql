/*
    Validaciones basicas de administracion y control PNMC.
*/

SELECT 'Roles' AS Tabla, COUNT(*) AS Registros FROM dbo.Roles
UNION ALL
SELECT 'Usuarios', COUNT(*) FROM dbo.Usuarios
UNION ALL
SELECT 'EstadosContenido', COUNT(*) FROM dbo.EstadosContenido
UNION ALL
SELECT 'Categorias', COUNT(*) FROM dbo.Categorias
UNION ALL
SELECT 'Etiquetas', COUNT(*) FROM dbo.Etiquetas
UNION ALL
SELECT 'Archivos', COUNT(*) FROM dbo.Archivos
UNION ALL
SELECT 'BitacoraAuditoria', COUNT(*) FROM dbo.BitacoraAuditoria;

SELECT r.NombreRol, COUNT(u.IdUsuario) AS Usuarios
FROM dbo.Roles r
LEFT JOIN dbo.Usuarios u ON u.IdRol = r.IdRol
GROUP BY r.NombreRol
ORDER BY r.NombreRol;

SELECT CodigoModulo, COUNT(*) AS Categorias
FROM dbo.Categorias
GROUP BY CodigoModulo
ORDER BY CodigoModulo;

SELECT CodigoEstado, NombreEstado
FROM dbo.EstadosContenido
ORDER BY IdEstadoContenido;

SELECT TablaAfectada, Accion, COUNT(*) AS Eventos
FROM dbo.BitacoraAuditoria
GROUP BY TablaAfectada, Accion
ORDER BY TablaAfectada, Accion;
