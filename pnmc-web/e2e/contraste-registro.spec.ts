import { test, expect, type Page } from '@playwright/test';

/**
 * Contraste de /registro, medido en el navegador.
 *
 * Nace de un defecto real: el panel de acceso pasó a fondo oscuro y la hoja de
 * estilos de la página lo resolvió forzando el color de CUALQUIER h2, p o label
 * que hubiera dentro. Funcionó para lo común y dejó un aviso —el de DIVIPOLA—
 * en blanco sobre fondo casi blanco: 1.05:1, invisible. Ninguna prueba unitaria
 * podía verlo: el defecto no está en el componente sino en la cascada, y solo
 * existe cuando el navegador junta las dos cosas.
 *
 * Por qué se mide así y no leyendo `background-color`:
 *
 *   1. Un panel `bg-slate-900/40` no tiene el fondo que declara. Hay que
 *      componer la cadena de ancestros como hace el navegador.
 *   2. Tailwind v4 emite `oklab(... / .6)` para `text-white/60`. Sacar los
 *      números con una expresión regular dio seis falsos positivos en la
 *      primera versión de esta medición. Aquí los colores se PINTAN en un
 *      canvas y se lee el píxel, que es la única lectura que no se puede
 *      discutir.
 *
 * Umbrales de WCAG 2.1 AA (1.4.3): 4.5:1 para texto normal, 3:1 para texto
 * grande —24px, o 18.66px en negrita—.
 */

interface Medicion {
  texto: string;
  etiqueta: string;
  ratio: number;
  minimo: number;
  fondo: string;
}

async function medirContraste(page: Page): Promise<Medicion[]> {
  return page.evaluate(() => {
    const lienzo = document.createElement('canvas');
    lienzo.width = lienzo.height = 1;
    const ctx = lienzo.getContext('2d', { willReadFrequently: true })!;

    const pintar = (capas: string[]) => {
      ctx.clearRect(0, 0, 1, 1);
      ctx.fillStyle = '#fff';
      ctx.fillRect(0, 0, 1, 1);
      for (const capa of capas) {
        ctx.fillStyle = capa;
        ctx.fillRect(0, 0, 1, 1);
      }
      const [r, g, b] = ctx.getImageData(0, 0, 1, 1).data;
      return { r, g, b };
    };
    const esOpaco = (color: string) => {
      ctx.clearRect(0, 0, 1, 1);
      ctx.fillStyle = color;
      ctx.fillRect(0, 0, 1, 1);
      return ctx.getImageData(0, 0, 1, 1).data[3] === 255;
    };
    const luminancia = ({ r, g, b }: { r: number; g: number; b: number }) => {
      const canal = (v: number) => {
        const x = v / 255;
        return x <= 0.03928 ? x / 12.92 : Math.pow((x + 0.055) / 1.055, 2.4);
      };
      return 0.2126 * canal(r) + 0.7152 * canal(g) + 0.0722 * canal(b);
    };

    const raiz = document.querySelector('.portal-acceso');
    if (!raiz) return [];

    const salida: Medicion[] = [];
    for (const el of raiz.querySelectorAll<HTMLElement>('h1,h2,h3,h4,p,label,button,strong,li,a,dt,dd')) {
      // Solo el texto propio: contar el de los hijos mediría el color del
      // padre sobre palabras que en realidad pinta otro elemento.
      const propio = [...el.childNodes]
        .filter((n) => n.nodeType === Node.TEXT_NODE)
        .map((n) => (n.textContent ?? '').trim())
        .join(' ')
        .trim();
      if (!propio) continue;

      const caja = el.getBoundingClientRect();
      if (!caja.width || !caja.height) continue;
      const estilo = getComputedStyle(el);
      if (estilo.visibility === 'hidden' || estilo.opacity === '0') continue;

      const capas: string[] = [];
      for (let n: HTMLElement | null = el; n; n = n.parentElement) {
        const fondo = getComputedStyle(n).backgroundColor;
        if (fondo && fondo !== 'transparent' && !/[,/]\s*0\)$/.test(fondo)) {
          capas.unshift(fondo);
          if (esOpaco(fondo)) break;
        }
      }

      const colorFondo = pintar(capas);
      const colorTexto = pintar([...capas, estilo.color]);
      const l1 = luminancia(colorTexto);
      const l2 = luminancia(colorFondo);
      const px = parseFloat(estilo.fontSize);
      const grande = px >= 24 || (px >= 18.66 && Number(estilo.fontWeight) >= 700);

      salida.push({
        texto: propio.slice(0, 70),
        etiqueta: el.tagName.toLowerCase(),
        ratio: Number(((Math.max(l1, l2) + 0.05) / (Math.min(l1, l2) + 0.05)).toFixed(2)),
        minimo: grande ? 3 : 4.5,
        fondo: `rgb(${colorFondo.r},${colorFondo.g},${colorFondo.b})`,
      });
    }
    return salida;
  });
}

/** Cada pantalla del portal es un estado distinto; el defecto vivía en una sola. */
const PANTALLAS: { nombre: string; abrir?: (page: Page) => Promise<void> }[] = [
  { nombre: 'datos de la organización' },
  {
    nombre: 'ingresar',
    abrir: (page) => page.getByRole('tab', { name: 'Ingresar' }).click(),
  },
];

for (const pantalla of PANTALLAS) {
  test(`/registro · «${pantalla.nombre}» cumple el contraste mínimo`, async ({ page }) => {
    await page.goto('/registro', { waitUntil: 'networkidle' });
    if (pantalla.abrir) await pantalla.abrir(page);
    await expect(page.locator('.portal-acceso')).toBeVisible();

    const medidas = await medirContraste(page);

    // Que la medición encuentre algo es parte de la prueba: si un cambio de
    // marcado dejara el selector sin nada que medir, esto pasaría en verde
    // sin haber mirado un solo texto.
    expect(medidas.length).toBeGreaterThan(8);

    const flojos = medidas
      .filter((m) => m.ratio < m.minimo)
      .sort((a, b) => a.ratio - b.ratio)
      .map((m) => `${m.ratio}:1 (mín ${m.minimo}) <${m.etiqueta}> sobre ${m.fondo} — «${m.texto}»`);

    expect(flojos, `Textos por debajo del mínimo WCAG AA:\n  ${flojos.join('\n  ')}`).toEqual([]);
  });
}
