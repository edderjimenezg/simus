/*
    PNMC · El módulo de un registro se dice de una sola manera

    QUE ES ESTO
    -----------
    El proyecto convergió hace tiempo en identificar cualquier registro por el par «módulo +
    identificador». Lo que la auditoría del 21 de septiembre encontró es que ese par se escribía de
    cuatro maneras distintas, porque no había ningún sitio donde estuviera declarado: eran 152
    cadenas sueltas repartidas por el código, y nada obligaba a que dos de ellas coincidieran.

      · Diecisiete tablas guardaban el módulo en una columna llamada `ModuloId`.
      · Cinco lo guardaban en una llamada `Dominio`.
      · Unas decían `festivales` y otras `festival`; unas `organizaciones` y otras `organizacion`;
        el catálogo editorial era `editorial` aquí y `catalogo-editorial` allá.
      · Y dentro de `ModuloId` convivían dos separadores: el guion bajo de `ediciones_festival` y el
        guion medio de `catalogo-editorial`.

    LO QUE ESO ROMPIA
    ----------------------------------------------
    El cruce entre la procedencia de un registro y su historial de revisión —la consulta que
    contesta «de dónde vino esto y qué se hizo con ello»— devolvía CERO filas, sobre 1 050 filas de
    procedencia y 38 de historial. No un resultado parcial: ninguno. La regla más transversal del
    proyecto, la de que todo registro guarda de dónde viene, no se podía cruzar con nada porque las
    dos mitades de la historia estaban escritas en vocabularios que no se hablaban entre sí.

    POR QUE PLURAL Y CON GUION MEDIO
    ---------------------------------
    No es una preferencia. El valor viaja como segmento de URL en tres rutas —
    `/admin/importaciones/{dominio}`, `/admin/borradores/{dominio}` y
    `/admin/data/records/{moduleId}`—, y en un camino se escribe en minúsculas con guion medio.
    Además la Importación Asistida ya usaba exactamente esa forma —`festivales`, `organizaciones`,
    `noticias`, `agenda`, `catalogo-editorial`— y los módulos de la consola también, con su lista
    atada por prueba al fichero de navegación, de modo que no podían moverse.

    Eso deja claro quién se mueve: los tres valores con guion bajo que introdujo la consolidación
    del 18 de septiembre —`versiones_festival`, `ediciones_festival` y `ediciones_mercado`—. La
    disidencia más reciente era la nuestra, no la heredada.

    LO QUE NO SE TOCA, Y POR QUE
    -----------------------------
    `SolicitudesVinculacionRegistros` guarda en su `ModuloId` valores como `festivales_retiro`, que
    no son el módulo de un registro sino la CLASE de solicitud. Es otro concepto reutilizando el
    mismo nombre de columna, y arreglarlo es un corte propio: aquí no se traduce porque el mapa de
    equivalencias solo reescribe coincidencias exactas de los valores antiguos, y ninguno de esos
    lo es.

    Tampoco se tocan los nombres de los campos de los contratos —`ImportacionDto.Dominio`,
    `NotificationCreateRequest.ModuleId`—: viajan como claves JSON y el frontend los lee en 82
    sitios. Eso es la decisión abierta número 11.

    SEGURIDAD
    ---------
    Es idempotente. Cuenta antes, traduce, cuenta después y se detiene si algo no cuadra. Las
    restricciones CHECK se retiran antes de traducir —porque solo admiten los valores viejos— y se
    vuelven a poner con el vocabulario nuevo.
*/

-- ────────────────────────────────────────────────────────────────────────────────
-- 2. Las restricciones CHECK se retiran: solo admiten el vocabulario viejo.
-- ────────────────────────────────────────────────────────────────────────────────
-- SE RETIRAN TAMBIEN LAS QUE SE LLAMAN `_Dominio`. `CK_LotesImportacion_Dominio` no solo acota
-- valores: al mencionar la columna la convierte en dependencia forzada, y SQL Server se niega a
-- renombrarla mientras exista. El síntoma —«participates in enforced dependencies»— no dice cuál
-- es el objeto que estorba, así que conviene dejarlo escrito aquí.
DECLARE @sql nvarchar(max) = N'';
SELECT @sql = @sql + N'ALTER TABLE dbo.' + QUOTENAME(OBJECT_NAME(cc.parent_object_id))
                   + N' DROP CONSTRAINT ' + QUOTENAME(cc.name) + N';' + CHAR(10)
FROM sys.check_constraints cc
WHERE cc.name LIKE N'CK[_]%[_]Modulo' OR cc.name LIKE N'CK[_]%[_]Dominio';
EXEC sp_executesql @sql;
PRINT '[V20260921_01] Restricciones de módulo retiradas para poder traducir y renombrar.';
GO

-- ────────────────────────────────────────────────────────────────────────────────
-- 3. La columna `Dominio` pasa a llamarse `ModuloId` en las cinco tablas disidentes.
-- ────────────────────────────────────────────────────────────────────────────────
IF COL_LENGTH('dbo.ProcedenciaDeRegistros', 'Dominio') IS NOT NULL
    EXEC sp_rename N'dbo.ProcedenciaDeRegistros.Dominio', N'ModuloId', N'COLUMN';
IF COL_LENGTH('dbo.BorradoresProceso', 'Dominio') IS NOT NULL
    EXEC sp_rename N'dbo.BorradoresProceso.Dominio', N'ModuloId', N'COLUMN';
IF COL_LENGTH('dbo.LotesImportacion', 'Dominio') IS NOT NULL
    EXEC sp_rename N'dbo.LotesImportacion.Dominio', N'ModuloId', N'COLUMN';
IF COL_LENGTH('dbo.ReclamacionesAdministracion', 'Dominio') IS NOT NULL
    EXEC sp_rename N'dbo.ReclamacionesAdministracion.Dominio', N'ModuloId', N'COLUMN';
IF COL_LENGTH('dbo.TransferenciasAdministracion', 'Dominio') IS NOT NULL
    EXEC sp_rename N'dbo.TransferenciasAdministracion.Dominio', N'ModuloId', N'COLUMN';
GO

-- Los índices siguen a la columna, pero sus NOMBRES seguirían diciendo «Dominio».
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LotesImportacion_Dominio_Fecha' AND object_id = OBJECT_ID('dbo.LotesImportacion'))
    EXEC sp_rename N'dbo.LotesImportacion.IX_LotesImportacion_Dominio_Fecha', N'IX_LotesImportacion_Modulo_Fecha', N'INDEX';
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProcedenciaDeRegistros_Dominio' AND object_id = OBJECT_ID('dbo.ProcedenciaDeRegistros'))
    EXEC sp_rename N'dbo.ProcedenciaDeRegistros.IX_ProcedenciaDeRegistros_Dominio', N'IX_ProcedenciaDeRegistros_Modulo', N'INDEX';
GO

-- ────────────────────────────────────────────────────────────────────────────────
-- 4. La traducción del vocabulario, en TODAS las tablas que guardan un módulo.
--
--    Se aplica el mismo mapa en todas, y solo reescribe coincidencias EXACTAS de los
--    valores antiguos. Por eso `festivales_retiro` —que es la clase de una solicitud y
--    no un módulo— sobrevive intacto, igual que `reclamaciones-administracion`.
-- ────────────────────────────────────────────────────────────────────────────────
-- LA LISTA VIAJA DENTRO DEL SQL GENERADO, Y NO EN UNA TABLA TEMPORAL. El primer intento la puso en
-- `#equivalencias`, creada en un lote y usada en el siguiente. Funcionó contra la base local y falló
-- contra el arnés de pruebas con «Invalid object name '#equivalencias'»: una tabla temporal solo
-- sobrevive entre lotes si todos viajan por la misma conexión, y eso es una suposición sobre quién
-- ejecuta el guion que un guion de esquema no debería hacer.
DECLARE @traduce nvarchar(max) = N'';
SELECT @traduce = @traduce
    + N'UPDATE t SET t.ModuloId = e.Nuevo FROM dbo.' + QUOTENAME(t.name) + N' t '
    + N'JOIN (VALUES '
    + N'(N''festival'',N''festivales''),'
    + N'(N''festivals'',N''festivales''),'
    + N'(N''organizacion'',N''organizaciones''),'
    + N'(N''noticia'',N''noticias''),'
    + N'(N''mercado'',N''mercados''),'
    + N'(N''editorial'',N''catalogo-editorial''),'
    + N'(N''edicion_festival'',N''ediciones-festival''),'
    + N'(N''ediciones_festival'',N''ediciones-festival''),'
    + N'(N''versiones_festival'',N''versiones-festival''),'
    + N'(N''edicion_mercado'',N''ediciones-mercado''),'
    + N'(N''ediciones_mercado'',N''ediciones-mercado'')'
    + N') AS e(Viejo, Nuevo) ON e.Viejo = t.ModuloId;' + CHAR(10)
FROM sys.tables t
JOIN sys.columns c ON c.object_id = t.object_id AND c.name = N'ModuloId';
EXEC sp_executesql @traduce;
PRINT '[V20260921_01] Vocabulario traducido en todas las tablas que guardan un módulo.';
GO

-- ────────────────────────────────────────────────────────────────────────────────
-- 5. Las restricciones vuelven, con el vocabulario nuevo.
-- ────────────────────────────────────────────────────────────────────────────────
DECLARE @lista nvarchar(400) =
    N'(N''festivales'', N''versiones-festival'', N''ediciones-festival'', N''ediciones-mercado'')';
DECLARE @pon nvarchar(max) = N'';
SELECT @pon = @pon
    + N'ALTER TABLE dbo.' + QUOTENAME(t.name) + N' WITH CHECK ADD CONSTRAINT '
    + QUOTENAME(N'CK_' + t.name + N'_Modulo') + N' CHECK (ModuloId IN ' + @lista + N');' + CHAR(10)
FROM sys.tables t
WHERE t.name IN (N'PracticasMusicalesDeRegistro', N'TerritoriosSonorosDeRegistro',
                 N'ExpresionesArtisticasDeRegistro', N'ModalidadesParticipacionDeRegistro',
                 N'TiposIngresoDeRegistro', N'LocalizacionesDeRegistro',
                 N'EntidadesAliadasDeRegistro', N'ArchivosDeRegistro')
  AND NOT EXISTS (SELECT 1 FROM sys.check_constraints cc WHERE cc.name = N'CK_' + t.name + N'_Modulo');
EXEC sp_executesql @pon;

-- La de lotes vuelve con su propio conjunto: son los cinco dominios que la Importación Asistida
-- sabe leer, y ya estaban escritos en la forma canónica —fue una de las pruebas de que esa era la
-- forma buena—.
IF OBJECT_ID('dbo.LotesImportacion', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_LotesImportacion_Modulo')
BEGIN
    ALTER TABLE dbo.LotesImportacion WITH CHECK ADD CONSTRAINT CK_LotesImportacion_Modulo
        CHECK (ModuloId IN (N'festivales', N'organizaciones', N'noticias', N'agenda', N'catalogo-editorial'));
END

PRINT '[V20260921_01] Restricciones de módulo repuestas con el vocabulario nuevo.';
GO

-- ────────────────────────────────────────────────────────────────────────────────
-- 6. La tabla de procedencia se pluraliza como las demás genéricas.
--
--    `RevisionesDeRegistro`, `PracticasMusicalesDeRegistro`, `LocalizacionesDeRegistro`:
--    el sustantivo en plural y el complemento en singular. La de procedencia lo hacía al
--    revés.
-- ────────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.ProcedenciaDeRegistros', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.ProcedenciasDeRegistro', 'U') IS NULL
BEGIN
    IF COL_LENGTH('dbo.ProcedenciaDeRegistros', 'IdProcedencia') IS NOT NULL
        PRINT '[V20260921_01] La clave ya se llama IdProcedencia.';
    EXEC sp_rename N'dbo.ProcedenciaDeRegistros', N'ProcedenciasDeRegistro';
END;
GO

IF OBJECT_ID('dbo.ProcedenciasDeRegistro', 'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'PK_ProcedenciaDeRegistros' AND parent_object_id = OBJECT_ID('dbo.ProcedenciasDeRegistro'))
        EXEC sp_rename N'PK_ProcedenciaDeRegistros', N'PK_ProcedenciasDeRegistro', N'OBJECT';
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_ProcedenciaDeRegistros' AND object_id = OBJECT_ID('dbo.ProcedenciasDeRegistro'))
        EXEC sp_rename N'dbo.ProcedenciasDeRegistro.UQ_ProcedenciaDeRegistros', N'UQ_ProcedenciasDeRegistro', N'INDEX';
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProcedenciaDeRegistros_Modulo' AND object_id = OBJECT_ID('dbo.ProcedenciasDeRegistro'))
        EXEC sp_rename N'dbo.ProcedenciasDeRegistro.IX_ProcedenciaDeRegistros_Modulo', N'IX_ProcedenciasDeRegistro_Modulo', N'INDEX';
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProcedenciaDeRegistros_Organizacion' AND object_id = OBJECT_ID('dbo.ProcedenciasDeRegistro'))
        EXEC sp_rename N'dbo.ProcedenciasDeRegistro.IX_ProcedenciaDeRegistros_Organizacion', N'IX_ProcedenciasDeRegistro_Organizacion', N'INDEX';
END;
GO

-- ────────────────────────────────────────────────────────────────────────────────
-- 7. La comprobación: no sobrevive ni un valor del vocabulario viejo.
--
--    Es más fuerte que contar filas antes y después. Un recuento igual no dice que la traducción
--    ocurriera —también cuadra si no se tradujo nada—; esto sí, porque falla si queda uno solo.
-- ────────────────────────────────────────────────────────────────────────────────
DECLARE @quedan nvarchar(max) = N'';
DECLARE @viejos nvarchar(400) = N'(N''festival'', N''festivals'', N''organizacion'', N''noticia'', '
    + N'N''mercado'', N''editorial'', N''edicion_festival'', N''ediciones_festival'', '
    + N'N''versiones_festival'', N''edicion_mercado'', N''ediciones_mercado'')';
DECLARE @conteo int = 0;
DECLARE @cuenta nvarchar(max) = N'SELECT @n = 0;' + CHAR(10);
SELECT @cuenta = @cuenta
    + N'SELECT @n = @n + (SELECT COUNT(*) FROM dbo.' + QUOTENAME(t.name)
    + N' WHERE ModuloId IN ' + @viejos + N');' + CHAR(10)
FROM sys.tables t
JOIN sys.columns c ON c.object_id = t.object_id AND c.name = N'ModuloId';
EXEC sp_executesql @cuenta, N'@n int OUTPUT', @n = @conteo OUTPUT;

IF @conteo > 0
    THROW 50040, 'Quedan filas con el vocabulario viejo de módulo. La traducción no está completa.', 1;

PRINT '[V20260921_01] Ninguna fila conserva el vocabulario viejo.';
GO

PRINT '[V20260921_01] El módulo de un registro se dice de una sola manera.';
GO
