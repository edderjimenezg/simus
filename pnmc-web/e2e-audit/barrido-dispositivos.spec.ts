import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { RUTAS_PUBLICAS } from './rutas';
import { AUDITORIA_DOM } from './auditoria-dom';

/**
 * Barrido de observacion: recorre cada ruta publica en cada configuracion de
 * dispositivo y mide sobre el DOM renderizado.
 *
 * Deliberadamente NO falla: el veredicto se emite al consolidar, no aqui. Una
 * prueba que se pone roja a la primera captura impediria terminar el barrido,
 * y lo que se busca es el mapa completo.
 */

// La salida vive FUERA del repositorio a proposito: 90 JSON y 90 capturas en
// `git status` le ensuciarian el arbol a la sesion que esta trabajando en
// paralelo. Se puede redirigir con PNMC_AUDIT_OUT.
const SALIDA =
  process.env.PNMC_AUDIT_OUT ||
  path.join(__dirname, '.informe');

test.describe.configure({ mode: 'serial' });

for (const r of RUTAS_PUBLICAS) {
  test(r.id, async ({ page }, testInfo) => {
    const dispositivo = testInfo.project.name;
    const erroresDeConsola: string[] = [];
    page.on('pageerror', (e) => erroresDeConsola.push(String(e).slice(0, 200)));

    // El geovisor abre un tutorial de 7 pasos que tapa la pantalla. Se marca
    // como visto para poder auditar lo que hay DEBAJO. Que aparezca —y que
    // reaparezca cada dia, porque la clave es una fecha— es un hallazgo aparte,
    // ya documentado con captura.
    await page.addInitScript(() => {
      const hoy = new Date().toISOString().slice(0, 10);
      try {
        window.localStorage.setItem('pnmc_last_tutorial_date', hoy);
      } catch {
        /* almacenamiento no disponible */
      }
    });

    await page.goto(r.url, { waitUntil: 'domcontentloaded' });
    // El arranque espera al CMS hasta 2,5 s (app.config.ts). Se le da margen.
    await page.waitForTimeout(3000);

    const informe = (await page.evaluate(AUDITORIA_DOM)) as Record<string, unknown>;
    informe['ruta'] = r.url;
    informe['id'] = r.id;
    informe['dispositivo'] = dispositivo;
    informe['viewport'] = page.viewportSize();
    informe['erroresDeConsola'] = erroresDeConsola;

    fs.mkdirSync(SALIDA, { recursive: true });
    fs.writeFileSync(
      path.join(SALIDA, dispositivo + '__' + r.id + '.json'),
      JSON.stringify(informe, null, 2),
      'utf8',
    );

    await page.screenshot({
      path: path.join(SALIDA, 'capturas', dispositivo + '__' + r.id + '.png'),
      fullPage: false,
    });

    const d = informe['desborde'] as { hay: boolean; scrollWidth: number; innerWidth: number; exceso: number };
    if (d.hay) {
      console.log(
        '  [DESBORDE] ' + dispositivo + ' ' + r.url + ' -> ' + d.scrollWidth + 'px de ancho en una ventana de ' + d.innerWidth + 'px (exceso ' + d.exceso + 'px)',
      );
    }
    const rec = informe['recorte'] as { hay: boolean; paginaNoDesplazable: boolean; elementos: unknown[] };
    if (rec.hay) {
      console.log(
        '  [RECORTE] ' + dispositivo + ' ' + r.url + ' -> ' + rec.elementos.length + ' elementos cortados por un overflow:hidden' +
        (rec.paginaNoDesplazable ? ' Y LA PAGINA NO SE PUEDE DESPLAZAR' : ''),
      );
    }
    const c = informe['contraste'] as { fallos: number };
    if (c.fallos > 0) {
      console.log('  [CONTRASTE] ' + dispositivo + ' ' + r.url + ' -> ' + c.fallos + ' textos por debajo del minimo');
    }

    expect(informe).toBeTruthy();
  });
}
