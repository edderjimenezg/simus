/**
 * Optimizador de las imágenes de public/.
 *
 * Las fotos entraron tal como salieron de la cámara o de WhatsApp: ~2390 px de
 * ancho y ~900 KB de media, para mostrarse a unos cientos de píxeles. Este
 * script hace dos cosas:
 *
 *   1. Re-codifica el original EN SU MISMA RUTA Y CON SU MISMO NOMBRE, acotado
 *      a MAX_WIDTH. Conservar la ruta es deliberado: las fotos de los álbumes
 *      las sirve la API, así que renombrarlas rompería referencias que no están
 *      en el código.
 *   2. Genera un hermano `<nombre>.thumb.webp` para las rejillas y la tira de
 *      miniaturas, donde el recuadro mide menos de 100 px.
 *
 * Es idempotente: en la segunda corrida los archivos ya cumplen y se omiten.
 * Nunca escribe un archivo más pesado que el que reemplaza.
 *
 * Uso:
 *   node tools/optimize-images.mjs --dry-run    solo reporta
 *   node tools/optimize-images.mjs              aplica
 */
import { readdir, readFile, writeFile, stat } from 'node:fs/promises';
import { join, extname, dirname, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import sharp from 'sharp';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const TARGETS = ['public/Galeria', 'public/editorial'];

/** Ancho máximo del original re-codificado. Cubre pantallas grandes en el visor. */
const MAX_WIDTH = 1600;
/** Ancho de la miniatura. La rejilla más grande usa recuadros de ~400 px. */
const THUMB_WIDTH = 400;
const JPEG_QUALITY = 82;
const THUMB_QUALITY = 70;
/**
 * Bytes por píxel por encima de los cuales una imagen se re-codifica aunque ya
 * sea angosta. Un JPEG bien comprimido ronda 0,1–0,3; muy por encima de eso hay
 * calidad que nadie percibe pero que el visitante sí descarga.
 */
const MAX_BYTES_PER_PIXEL = 0.3;

const DRY_RUN = process.argv.includes('--dry-run');
const IMAGE_EXT = new Set(['.jpg', '.jpeg', '.png']);

const mb = (bytes) => (bytes / 1048576).toFixed(1);
const kb = (bytes) => Math.round(bytes / 1024);

async function* walk(dir) {
  let entries;
  try {
    entries = await readdir(dir, { withFileTypes: true });
  } catch {
    return; // Carpeta ausente: no es un error, simplemente no hay nada que optimizar.
  }
  for (const entry of entries) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) {
      yield* walk(full);
    } else if (IMAGE_EXT.has(extname(entry.name).toLowerCase())) {
      // Las miniaturas generadas no se vuelven a procesar.
      if (!entry.name.includes('.thumb.')) yield full;
    }
  }
}

const stats = {
  files: 0, skipped: 0, resized: 0, thumbs: 0,
  bytesBefore: 0, bytesAfter: 0, thumbBytes: 0, failed: [],
};

for (const target of TARGETS) {
  for await (const file of walk(join(ROOT, target))) {
    stats.files++;
    const rel = relative(ROOT, file);
    try {
      const original = await readFile(file);
      const meta = await sharp(original).metadata();
      stats.bytesBefore += original.length;

      // --- 1. Original acotado, misma ruta y mismo nombre ---
      const pixels = (meta.width ?? 0) * (meta.height ?? 0);
      const bytesPerPixel = pixels > 0 ? original.length / pixels : 0;
      const needsResize = (meta.width ?? 0) > MAX_WIDTH || bytesPerPixel > MAX_BYTES_PER_PIXEL;
      let finalSize = original.length;

      if (needsResize) {
        const pipeline = sharp(original).rotate().resize({
          width: MAX_WIDTH, withoutEnlargement: true,
        });
        const out = meta.format === 'png'
          ? await pipeline.png({ compressionLevel: 9 }).toBuffer()
          : await pipeline.jpeg({ quality: JPEG_QUALITY, mozjpeg: true }).toBuffer();

        // Salvaguarda: jamás sustituir por algo más pesado.
        if (out.length < original.length) {
          if (!DRY_RUN) await writeFile(file, out);
          finalSize = out.length;
          stats.resized++;
        } else {
          stats.skipped++;
        }
      } else {
        stats.skipped++;
      }
      stats.bytesAfter += finalSize;

      // --- 2. Miniatura hermana ---
      const thumbPath = file.replace(/\.(jpe?g|png)$/i, '.thumb.webp');
      let thumbExists = false;
      try { await stat(thumbPath); thumbExists = true; } catch { /* aún no existe */ }

      if (!thumbExists) {
        const thumb = await sharp(original)
          .rotate()
          .resize({ width: THUMB_WIDTH, withoutEnlargement: true })
          .webp({ quality: THUMB_QUALITY })
          .toBuffer();
        if (!DRY_RUN) await writeFile(thumbPath, thumb);
        stats.thumbs++;
        stats.thumbBytes += thumb.length;
      }
    } catch (error) {
      stats.failed.push(`${rel}: ${error.message}`);
    }
  }
}

const saved = stats.bytesBefore - stats.bytesAfter;
console.log(`\n${DRY_RUN ? '[SIMULACIÓN] ' : ''}Optimización de imágenes\n`);
console.log(`  Analizadas       ${stats.files}`);
console.log(`  Re-codificadas   ${stats.resized}`);
console.log(`  Ya cumplían      ${stats.skipped}`);
console.log(`  Miniaturas       ${stats.thumbs} (${mb(stats.thumbBytes)} MB, ${stats.thumbs ? kb(stats.thumbBytes / stats.thumbs) : 0} KB de media)`);
console.log(`\n  Antes            ${mb(stats.bytesBefore)} MB`);
console.log(`  Después          ${mb(stats.bytesAfter)} MB`);
console.log(`  Ahorro           ${mb(saved)} MB (${stats.bytesBefore ? Math.round((saved / stats.bytesBefore) * 100) : 0} %)`);

if (stats.failed.length) {
  console.log(`\n  Fallaron ${stats.failed.length}:`);
  stats.failed.forEach((f) => console.log(`    ${f}`));
  process.exitCode = 1;
}
