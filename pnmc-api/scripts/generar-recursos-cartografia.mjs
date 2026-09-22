#!/usr/bin/env node

/**
 * Genera la cartografia liviana que consume el mapa publico.
 *
 * Fuente autoritativa:
 *   src/PNMC.Api/Assets/geo/Departamentos-Municipos-COL.json
 *
 * Salidas:
 *   src/PNMC.Api/Assets/geo/Departamentos-COL.simplified.topojson
 *   src/PNMC.Api/Assets/geo/municipalities/{codigoDepartamento}.topojson
 *
 * Ejecucion desde la raiz del repositorio:
 *   node pnmc-api/scripts/generar-recursos-cartografia.mjs
 *
 * La simplificacion se hace sobre los arcos TopoJSON compartidos, antes de
 * expandirlos a GeoJSON. De ese modo dos territorios vecinos siguen usando el
 * mismo borde y no aparecen grietas. Cada arco conserva sus extremos y ninguna
 * geometria, anillo o isla se elimina. Los arcos del departamento 88 usan la
 * tolerancia municipal conservadora (~19 m, menos de medio pixel en zoom 12)
 * para preservar el archipielago de San Andres, Providencia y Santa Catalina.
 * La generacion comprueba que conserve todos sus poligonos y anillos y que su
 * bbox no se desplace mas que esa tolerancia respecto de la fuente DANE.
 */

import { mkdir, readFile, readdir, unlink, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const SCRIPT_DIRECTORY = path.dirname(fileURLToPath(import.meta.url));
const API_DIRECTORY = path.resolve(SCRIPT_DIRECTORY, '..');
const GEO_DIRECTORY = path.join(API_DIRECTORY, 'src', 'PNMC.Api', 'Assets', 'geo');
const SOURCE_PATH = path.join(GEO_DIRECTORY, 'Departamentos-Municipos-COL.json');
const DEPARTMENTS_OUTPUT_PATH = path.join(GEO_DIRECTORY, 'Departamentos-COL.simplified.topojson');
const MUNICIPALITIES_OUTPUT_DIRECTORY = path.join(GEO_DIRECTORY, 'municipalities');

const DEPARTMENTS_OBJECT = 'MGN_ADM_DPTO_POLITICO';
const MUNICIPALITIES_OBJECT = 'MGN_ADM_MPIO_GRAFICO';
const ARCHIPELAGO_CODE = '88';

// La unidad cuantizada de la fuente equivale aproximadamente a 0,38 m.
// 250 unidades son ~95 m: menos de un pixel aun en el zoom departamental 10.
// 50 unidades son ~19 m: menos de medio pixel en el zoom municipal 12.
const DEPARTMENT_TOLERANCE = 250;
const MUNICIPALITY_TOLERANCE = 50;
const ARCHIPELAGO_TOLERANCE = MUNICIPALITY_TOLERANCE;

function invariant(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

function normalizedDepartmentCode(value) {
  return String(value ?? '').replace(/\D+/g, '').padStart(2, '0').slice(-2);
}

function decodeArc(arc) {
  let x = 0;
  let y = 0;

  return arc.map(([deltaX, deltaY]) => {
    x += deltaX;
    y += deltaY;
    return [x, y];
  });
}

function encodeArc(points) {
  let previousX = 0;
  let previousY = 0;

  return points.map(([x, y]) => {
    const encoded = [x - previousX, y - previousY];
    previousX = x;
    previousY = y;
    return encoded;
  });
}

function squaredSegmentDistance(point, start, end) {
  const deltaX = end[0] - start[0];
  const deltaY = end[1] - start[1];
  const denominator = deltaX * deltaX + deltaY * deltaY;

  if (denominator === 0) {
    const distanceX = point[0] - start[0];
    const distanceY = point[1] - start[1];
    return distanceX * distanceX + distanceY * distanceY;
  }

  const projection = Math.max(0, Math.min(1,
    ((point[0] - start[0]) * deltaX + (point[1] - start[1]) * deltaY) / denominator));
  const projectedX = start[0] + projection * deltaX;
  const projectedY = start[1] + projection * deltaY;
  const distanceX = point[0] - projectedX;
  const distanceY = point[1] - projectedY;
  return distanceX * distanceX + distanceY * distanceY;
}

/**
 * Douglas-Peucker iterativo sobre un arco cuantizado.
 *
 * El algoritmo nunca cambia los extremos del arco. Si un arco cerrado pudiera
 * quedar con menos de cuatro vertices, se conserva completo: perder unos bytes
 * es preferible a convertir una isla pequena en una linea.
 */
function simplifyArc(arc, tolerance) {
  if (tolerance <= 0 || arc.length <= 2) return arc;

  const points = decodeArc(arc);
  const keep = new Uint8Array(points.length);
  const stack = [[0, points.length - 1]];
  const squaredTolerance = tolerance * tolerance;
  const isClosed = points[0][0] === points.at(-1)[0] && points[0][1] === points.at(-1)[1];
  keep[0] = 1;
  keep[points.length - 1] = 1;

  while (stack.length > 0) {
    const [startIndex, endIndex] = stack.pop();
    let farthestIndex = -1;
    let farthestDistance = squaredTolerance;

    for (let index = startIndex + 1; index < endIndex; index += 1) {
      const distance = squaredSegmentDistance(points[index], points[startIndex], points[endIndex]);
      if (distance > farthestDistance) {
        farthestDistance = distance;
        farthestIndex = index;
      }
    }

    if (farthestIndex >= 0) {
      keep[farthestIndex] = 1;
      stack.push([startIndex, farthestIndex], [farthestIndex, endIndex]);
    }
  }

  const simplified = points.filter((_, index) => keep[index] === 1);
  if (isClosed && simplified.length < 4) return arc;
  return encodeArc(simplified);
}

function visitArcReferences(value, visitor) {
  if (typeof value === 'number') {
    visitor(value < 0 ? ~value : value);
    return;
  }

  if (Array.isArray(value)) {
    value.forEach((nested) => visitArcReferences(nested, visitor));
  }
}

function remapArcReferences(value, arcIndexMap) {
  if (typeof value === 'number') {
    const reversed = value < 0;
    const originalIndex = reversed ? ~value : value;
    const outputIndex = arcIndexMap.get(originalIndex);
    invariant(outputIndex !== undefined, `No se encontro el arco ${originalIndex} durante el remapeo.`);
    return reversed ? ~outputIndex : outputIndex;
  }

  return value.map((nested) => remapArcReferences(nested, arcIndexMap));
}

function transformedPoint(topology, point) {
  const scale = topology.transform?.scale ?? [1, 1];
  const translate = topology.transform?.translate ?? [0, 0];
  return [
    point[0] * scale[0] + translate[0],
    point[1] * scale[1] + translate[1],
  ];
}

function geometryBbox(topology, geometry) {
  const bbox = [Infinity, Infinity, -Infinity, -Infinity];

  visitArcReferences(geometry.arcs, (arcIndex) => {
    for (const point of decodeArc(topology.arcs[arcIndex])) {
      const [longitude, latitude] = transformedPoint(topology, point);
      bbox[0] = Math.min(bbox[0], longitude);
      bbox[1] = Math.min(bbox[1], latitude);
      bbox[2] = Math.max(bbox[2], longitude);
      bbox[3] = Math.max(bbox[3], latitude);
    }
  });

  invariant(bbox.every(Number.isFinite), 'Se encontro una geometria sin coordenadas.');
  return bbox;
}

function combinedBbox(bboxes) {
  return bboxes.reduce((combined, bbox) => [
    Math.min(combined[0], bbox[0]),
    Math.min(combined[1], bbox[1]),
    Math.max(combined[2], bbox[2]),
    Math.max(combined[3], bbox[3]),
  ], [Infinity, Infinity, -Infinity, -Infinity]);
}

function departmentProperties(properties = {}) {
  return {
    dpto_ccdgo: normalizedDepartmentCode(properties.dpto_ccdgo),
    dpto_cnmbr: String(properties.dpto_cnmbr ?? ''),
  };
}

function municipalityProperties(properties = {}) {
  return {
    dpto_ccdgo: normalizedDepartmentCode(properties.dpto_ccdgo),
    mpio_ccdgo: String(properties.mpio_ccdgo ?? ''),
    mpio_cdpmp: String(properties.mpio_cdpmp ?? ''),
    dpto_cnmbr: String(properties.dpto_cnmbr ?? ''),
    mpio_cnmbr: String(properties.mpio_cnmbr ?? ''),
  };
}

function buildSubsetTopology(source, objectName, geometries, options) {
  const usedArcIndexes = new Set();
  geometries.forEach((geometry) => visitArcReferences(geometry.arcs, (index) => usedArcIndexes.add(index)));

  const orderedArcIndexes = [...usedArcIndexes].sort((left, right) => left - right);
  const arcIndexMap = new Map(orderedArcIndexes.map((sourceIndex, outputIndex) => [sourceIndex, outputIndex]));
  const outputArcs = orderedArcIndexes.map((sourceIndex) => simplifyArc(
    source.arcs[sourceIndex],
    options.protectedArcIndexes?.has(sourceIndex)
      ? (options.protectedArcTolerance ?? 0)
      : options.tolerance,
  ));

  const outputGeometries = geometries.map((geometry) => ({
    type: geometry.type,
    arcs: remapArcReferences(geometry.arcs, arcIndexMap),
    bbox: geometryBbox(source, geometry),
    properties: options.mapProperties(geometry.properties),
  }));

  return {
    type: 'Topology',
    bbox: combinedBbox(outputGeometries.map((geometry) => geometry.bbox)),
    transform: source.transform,
    objects: {
      [objectName]: {
        type: 'GeometryCollection',
        geometries: outputGeometries,
      },
    },
    arcs: outputArcs,
  };
}

function referencedArcPoints(topology, reference) {
  const reversed = reference < 0;
  const arcIndex = reversed ? ~reference : reference;
  const points = decodeArc(topology.arcs[arcIndex]);
  return reversed ? points.reverse() : points;
}

function stitchedRing(topology, references) {
  const ring = [];
  references.forEach((reference, index) => {
    const points = referencedArcPoints(topology, reference);
    ring.push(...(index === 0 ? points : points.slice(1)));
  });
  return ring;
}

function signedRingArea(ring) {
  let twiceArea = 0;
  for (let index = 0; index < ring.length - 1; index += 1) {
    twiceArea += ring[index][0] * ring[index + 1][1] - ring[index + 1][0] * ring[index][1];
  }
  return twiceArea / 2;
}

function geometryRings(geometry) {
  if (geometry.type === 'Polygon') return geometry.arcs;
  if (geometry.type === 'MultiPolygon') return geometry.arcs.flat();
  throw new Error(`Tipo de geometria no soportado: ${geometry.type}`);
}

function validateTopology(topology, objectName, expectedFeatureCount) {
  invariant(topology.type === 'Topology', 'La salida no es Topology.');
  const geometries = topology.objects?.[objectName]?.geometries;
  invariant(Array.isArray(geometries), `Falta el objeto ${objectName}.`);
  invariant(geometries.length === expectedFeatureCount,
    `${objectName}: se esperaban ${expectedFeatureCount} geometrías y se generaron ${geometries.length}.`);

  for (const geometry of geometries) {
    for (const references of geometryRings(geometry)) {
      const ring = stitchedRing(topology, references);
      invariant(ring.length >= 4, 'La simplificacion produjo un anillo con menos de cuatro vertices.');
      invariant(ring[0][0] === ring.at(-1)[0] && ring[0][1] === ring.at(-1)[1],
        'La simplificacion produjo un anillo abierto.');
      invariant(Math.abs(signedRingArea(ring)) > 0, 'La simplificacion produjo un anillo sin area.');
    }
  }
}

function almostEqual(left, right, epsilon = 1e-10) {
  return Math.abs(left - right) <= epsilon;
}

function geometryShapeCounts(geometry) {
  if (geometry.type === 'Polygon') {
    return { polygons: 1, rings: geometry.arcs.length };
  }

  if (geometry.type === 'MultiPolygon') {
    return {
      polygons: geometry.arcs.length,
      rings: geometry.arcs.reduce((total, polygon) => total + polygon.length, 0),
    };
  }

  throw new Error(`Tipo de geometria no soportado: ${geometry.type}`);
}

async function writeTopology(outputPath, topology) {
  await writeFile(outputPath, `${JSON.stringify(topology)}\n`, 'utf8');
}

const source = JSON.parse(await readFile(SOURCE_PATH, 'utf8'));
const departmentGeometries = source.objects?.[DEPARTMENTS_OBJECT]?.geometries;
const municipalityGeometries = source.objects?.[MUNICIPALITIES_OBJECT]?.geometries;
invariant(Array.isArray(departmentGeometries), `La fuente no contiene ${DEPARTMENTS_OBJECT}.`);
invariant(Array.isArray(municipalityGeometries), `La fuente no contiene ${MUNICIPALITIES_OBJECT}.`);
invariant(departmentGeometries.length === 33, `La fuente debe contener 33 departamentos, contiene ${departmentGeometries.length}.`);
invariant(municipalityGeometries.length === 1122, `La fuente debe contener 1122 municipios, contiene ${municipalityGeometries.length}.`);

const archipelagoGeometry = departmentGeometries.find(
  (geometry) => normalizedDepartmentCode(geometry.properties?.dpto_ccdgo) === ARCHIPELAGO_CODE,
);
invariant(archipelagoGeometry, 'No se encontro el departamento 88 (San Andres).');
const sourceArchipelagoBbox = geometryBbox(source, archipelagoGeometry);

const protectedDepartmentArcs = new Set();
visitArcReferences(archipelagoGeometry.arcs, (index) => protectedDepartmentArcs.add(index));

const departmentsTopology = buildSubsetTopology(source, DEPARTMENTS_OBJECT, departmentGeometries, {
  tolerance: DEPARTMENT_TOLERANCE,
  protectedArcIndexes: protectedDepartmentArcs,
  protectedArcTolerance: ARCHIPELAGO_TOLERANCE,
  mapProperties: departmentProperties,
});
validateTopology(departmentsTopology, DEPARTMENTS_OBJECT, 33);

const outputArchipelagoGeometry = departmentsTopology.objects[DEPARTMENTS_OBJECT].geometries.find(
  (geometry) => geometry.properties.dpto_ccdgo === ARCHIPELAGO_CODE,
);
invariant(outputArchipelagoGeometry, 'La salida no contiene el departamento 88.');
const outputArchipelagoBbox = geometryBbox(departmentsTopology, outputArchipelagoGeometry);
const archipelagoBboxTolerance = ARCHIPELAGO_TOLERANCE
  * Math.max(...source.transform.scale)
  + Number.EPSILON;
invariant(sourceArchipelagoBbox.every((value, index) =>
  almostEqual(value, outputArchipelagoBbox[index], archipelagoBboxTolerance)),
  `El bbox de San Andres cambio: ${sourceArchipelagoBbox.join(',')} -> ${outputArchipelagoBbox.join(',')}.`);
const sourceArchipelagoShapeCounts = geometryShapeCounts(archipelagoGeometry);
const outputArchipelagoShapeCounts = geometryShapeCounts(outputArchipelagoGeometry);
invariant(sourceArchipelagoShapeCounts.polygons === outputArchipelagoShapeCounts.polygons,
  'La simplificacion cambio el numero de islas/poligonos de San Andres.');
invariant(sourceArchipelagoShapeCounts.rings === outputArchipelagoShapeCounts.rings,
  'La simplificacion cambio el numero de anillos de San Andres.');

await mkdir(MUNICIPALITIES_OUTPUT_DIRECTORY, { recursive: true });
await writeTopology(DEPARTMENTS_OUTPUT_PATH, departmentsTopology);

const municipalitiesByDepartment = new Map();
for (const geometry of municipalityGeometries) {
  const departmentCode = normalizedDepartmentCode(geometry.properties?.dpto_ccdgo);
  const collection = municipalitiesByDepartment.get(departmentCode) ?? [];
  collection.push(geometry);
  municipalitiesByDepartment.set(departmentCode, collection);
}

invariant(municipalitiesByDepartment.size === 33,
  `Se esperaban chunks para 33 departamentos y se encontraron ${municipalitiesByDepartment.size}.`);

const expectedChunkNames = new Set();
let generatedMunicipalities = 0;
for (const [departmentCode, geometries] of [...municipalitiesByDepartment.entries()].sort()) {
  const municipalityTopology = buildSubsetTopology(source, MUNICIPALITIES_OBJECT, geometries, {
    tolerance: MUNICIPALITY_TOLERANCE,
    mapProperties: municipalityProperties,
  });
  validateTopology(municipalityTopology, MUNICIPALITIES_OBJECT, geometries.length);

  const chunkName = `${departmentCode}.topojson`;
  expectedChunkNames.add(chunkName);
  generatedMunicipalities += geometries.length;
  await writeTopology(path.join(MUNICIPALITIES_OUTPUT_DIRECTORY, chunkName), municipalityTopology);
}

// Elimina exclusivamente chunks generados que ya no correspondan a un codigo
// presente en la fuente. No toca otros tipos de archivo ni directorios.
for (const entry of await readdir(MUNICIPALITIES_OUTPUT_DIRECTORY, { withFileTypes: true })) {
  if (entry.isFile() && entry.name.endsWith('.topojson') && !expectedChunkNames.has(entry.name)) {
    await unlink(path.join(MUNICIPALITIES_OUTPUT_DIRECTORY, entry.name));
  }
}

invariant(generatedMunicipalities === 1122,
  `Se esperaban 1122 municipios y se generaron ${generatedMunicipalities}.`);

console.log(JSON.stringify({
  departments: departmentGeometries.length,
  municipalityChunks: municipalitiesByDepartment.size,
  municipalities: generatedMunicipalities,
  sanAndresBbox: outputArchipelagoBbox,
  sanAndresBboxMaxDelta: Math.max(...sourceArchipelagoBbox.map(
    (value, index) => Math.abs(value - outputArchipelagoBbox[index])),
  ),
  sanAndresShapeCounts: outputArchipelagoShapeCounts,
  outputs: {
    departments: path.relative(API_DIRECTORY, DEPARTMENTS_OUTPUT_PATH),
    municipalities: path.relative(API_DIRECTORY, MUNICIPALITIES_OUTPUT_DIRECTORY),
  },
}, null, 2));
