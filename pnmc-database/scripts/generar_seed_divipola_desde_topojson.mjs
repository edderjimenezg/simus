import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const rootDir = path.resolve(__dirname, '../..');
const requireFromFrontend = createRequire(path.join(rootDir, 'pnmc-web/package.json'));
const { feature } = requireFromFrontend('topojson-client');
const sourcePath = path.join(rootDir, 'pnmc-api/src/PNMC.Api/Assets/geo/Departamentos-Municipos-COL.json');
const outputPath = path.join(rootDir, 'pnmc-database/seed/V20260519_02__divipola_seed.sql');

const topology = JSON.parse(fs.readFileSync(sourcePath, 'utf8'));
const collection = feature(topology, topology.objects.MGN_ADM_MPIO_GRAFICO);

const escapeSql = (value) => String(value ?? '').replace(/'/g, "''").trim();
const numberOrNull = (value) => Number.isFinite(value) ? value.toFixed(6) : 'NULL';

const collectPositions = (coordinates, positions = []) => {
  if (!Array.isArray(coordinates)) return positions;
  if (typeof coordinates[0] === 'number' && typeof coordinates[1] === 'number') {
    positions.push(coordinates);
    return positions;
  }
  coordinates.forEach((item) => collectPositions(item, positions));
  return positions;
};

const centroid = (geometry) => {
  const positions = collectPositions(geometry?.coordinates);
  if (positions.length === 0) return { latitud: null, longitud: null };
  const totals = positions.reduce((acc, [longitud, latitud]) => {
    acc.longitud += longitud;
    acc.latitud += latitud;
    return acc;
  }, { latitud: 0, longitud: 0 });
  return {
    latitud: totals.latitud / positions.length,
    longitud: totals.longitud / positions.length,
  };
};

const rows = collection.features
  .map((item) => {
    const props = item.properties || {};
    const { latitud, longitud } = centroid(item.geometry);
    return {
      CodigoDepartamento: String(props.dpto_ccdgo || '').padStart(2, '0'),
      NombreDepartamento: props.dpto_cnmbr || '',
      CodigoMunicipio: String(props.mpio_cdpmp || '').padStart(5, '0'),
      NombreMunicipio: props.mpio_cnmbr || '',
      TipoTerritorio: props.mpio_tipo || '',
      Latitud: latitud,
      Longitud: longitud,
    };
  })
  .filter((row) => row.CodigoDepartamento && row.CodigoMunicipio)
  .sort((left, right) => (
    left.CodigoDepartamento.localeCompare(right.CodigoDepartamento)
    || left.CodigoMunicipio.localeCompare(right.CodigoMunicipio)
  ));

const chunks = [];
for (let index = 0; index < rows.length; index += 250) {
  chunks.push(rows.slice(index, index + 250));
}

const lines = [
  '/*',
  '    PNMC - Carga inicial de Divipola.',
  '    Archivo generado desde pnmc-api/src/PNMC.Api/Assets/geo/Departamentos-Municipos-COL.json.',
  '    No editar manualmente: usar pnmc-database/scripts/generar_seed_divipola_desde_topojson.mjs.',
  '',
  '    ----------------------------------------------------------------------------------------',
  '    AQUI NO HAY `DELETE FROM dbo.Divipola`, Y LO HUBO HASTA EL 24 DE AGOSTO DE 2026.',
  '',
  '    Lo quitaba todo y volvia a insertar los 1.122 municipios. Sobre una base virgen funciona.',
  '    Sobre una base YA SEMBRADA, no: doce claves foraneas apuntan a dbo.Divipola -- Agenda,',
  '    Entidades, EscuelasMusica, Festivales, Lutieres, MercadosMusicales, MetricasMunicipioMapa,',
  '    RedesDocumentacion, RegistrosEcosistema, VersionesFestival, PropuestasCambioFestival y',
  '    VersionesFestivalLocalizaciones -- y el DELETE choca con la primera que tenga filas:',
  '',
  '      Msg 547  - The DELETE statement conflicted with the REFERENCE constraint',
  '                 "FK_Entidades_Divipola"',
  '      Msg 2627 - Violation of PRIMARY KEY constraint (05, 05001)  <- los INSERT de detras',
  '',
  '    Y `scripts/seed-local-db.sh` invoca `sqlcmd -b` con `set -e`, asi que ese primer error',
  '    ABORTA LA SIEMBRA ENTERA: las semillas siguientes no llegan a correr.',
  '',
  '    AHORA CONVERGE POR CLAVE NATURAL, que aqui es la clave primaria (departamento, municipio):',
  '    las filas se cargan en una tabla temporal y de ahi entran con MERGE. Inserta lo que falta,',
  '    actualiza lo que cambio, y NO BORRA.',
  '',
  '    NO SE BORRA A PROPOSITO: un `WHEN NOT MATCHED BY SOURCE THEN DELETE` devuelve exactamente',
  '    el Msg 547 que esto viene a cerrar. Consecuencia asumida y dicha: si el DANE retira un',
  '    municipio, este fichero deja de mencionarlo pero NO lo saca de la base. Retirar una fila de',
  '    Divipola referenciada es un trabajo con su propio guion, no un efecto colateral de sembrar.',
  '    ----------------------------------------------------------------------------------------',
  '*/',
  '',
  'SET NOCOUNT ON;',
  '',
  "IF OBJECT_ID(N'tempdb..#DivipolaOrigen') IS NOT NULL DROP TABLE #DivipolaOrigen;",
  '',
  'CREATE TABLE #DivipolaOrigen',
  '(',
  '    CodigoDepartamento char(2)       NOT NULL,',
  '    NombreDepartamento nvarchar(120) NOT NULL,',
  '    CodigoMunicipio    char(5)       NOT NULL,',
  '    NombreMunicipio    nvarchar(160) NOT NULL,',
  '    TipoTerritorio     nvarchar(80)  NULL,',
  '    Latitud            decimal(9, 6) NULL,',
  '    Longitud           decimal(9, 6) NULL,',
  '    PRIMARY KEY (CodigoDepartamento, CodigoMunicipio)',
  ');',
  '',
];

for (const chunk of chunks) {
  lines.push('INSERT INTO #DivipolaOrigen');
  lines.push('    (CodigoDepartamento, NombreDepartamento, CodigoMunicipio, NombreMunicipio, TipoTerritorio, Latitud, Longitud)');
  lines.push('VALUES');
  chunk.forEach((row, index) => {
    const suffix = index === chunk.length - 1 ? ';' : ',';
    lines.push(`    ('${escapeSql(row.CodigoDepartamento)}', N'${escapeSql(row.NombreDepartamento)}', '${escapeSql(row.CodigoMunicipio)}', N'${escapeSql(row.NombreMunicipio)}', N'${escapeSql(row.TipoTerritorio)}', ${numberOrNull(row.Latitud)}, ${numberOrNull(row.Longitud)})${suffix}`);
  });
  lines.push('');
}

lines.push(
  'MERGE dbo.Divipola AS destino',
  'USING #DivipolaOrigen AS origen',
  '    ON  destino.CodigoDepartamento = origen.CodigoDepartamento',
  '    AND destino.CodigoMunicipio    = origen.CodigoMunicipio',
  'WHEN MATCHED THEN',
  '    UPDATE SET',
  '        NombreDepartamento = origen.NombreDepartamento,',
  '        NombreMunicipio    = origen.NombreMunicipio,',
  '        TipoTerritorio     = origen.TipoTerritorio,',
  '        Latitud            = origen.Latitud,',
  '        Longitud           = origen.Longitud',
  'WHEN NOT MATCHED BY TARGET THEN',
  '    INSERT (CodigoDepartamento, NombreDepartamento, CodigoMunicipio, NombreMunicipio, TipoTerritorio, Latitud, Longitud)',
  '    VALUES (origen.CodigoDepartamento, origen.NombreDepartamento, origen.CodigoMunicipio, origen.NombreMunicipio, origen.TipoTerritorio, origen.Latitud, origen.Longitud);',
  '',
  'DROP TABLE #DivipolaOrigen;',
  '',
);

fs.writeFileSync(outputPath, `${lines.join('\n')}\n`, 'utf8');
console.log(`Generado ${path.relative(rootDir, outputPath)} con ${rows.length} registros.`);

if (rows.length < 1000) {
  console.error(`La carga generada parece incompleta: ${rows.length} registros.`);
  process.exit(1);
}
