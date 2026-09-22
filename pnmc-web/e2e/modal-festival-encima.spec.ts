import { test, expect } from '@playwright/test';

/**
 * El modal de «Crear un Festival» tiene que quedar POR ENCIMA de la barra de navegación.
 *
 * <b>Por qué esto no lo puede ver ninguna prueba de Karma.</b> El defecto no está en el modal: está
 * en la relación entre el modal y el armazón de la aplicación. Karma monta el componente solo, sin
 * `app.component.html`, así que la barra no existe y el `z-index` del modal no compite con nada. En
 * el navegador de verdad sí compite, y perdía.
 *
 * <b>El defecto, con su mecanismo.</b> `<main id="contenido-principal">` lleva la clase
 * `animate-page-entrance`, que es `animation: pageSlideUp 0.6s ... forwards` sobre `opacity`
 * (`src/styles.css:277-290`). Una animación de `opacity` que sigue aplicándose —y con `forwards`
 * sigue aplicándose para siempre— convierte a `<main>` en un contexto de apilamiento. A partir de
 * ahí, el `z-[4500]` del modal ya no se compara con el `z-[3000]` de la barra: se compara con sus
 * hermanos DENTRO de `<main>`, y `<main>` entero, que es estático y sin `z-index`, se pinta por
 * debajo de cualquier elemento posicionado con z-index positivo. La barra tapaba los primeros
 * píxeles del diálogo y el título «Crear un Festival» no se veía.
 *
 * Subir el número no arreglaba nada: ya se subió de `z-[100]` a `z-[4500]`
 * y el diálogo siguió cortado. Por eso esta prueba no mira el `z-index` —que estaba bien— sino
 * QUIÉN PINTA ENCIMA, que es lo que se ve.
 *
 * Necesita el entorno levantado: `ng serve` en 4300 y la API en 8180. Vive fuera de `npm test`.
 */

const CUENTA = {
  email: process.env.PNMC_EXTERNO_EMAIL ?? 'externo@pnmc.local',
  password: process.env.PNMC_EXTERNO_PASSWORD ?? 'admin',
};

// EL DIÁLOGO CAMBIÓ DE NOMBRE, cuando el alta y la ficha se unificaron en
// un solo formulario de diez pasos: `crear-festival-modal` dejó de existir y su sitio lo ocupa
// `ficha-festival`. Lo que esta prueba mide —quién pinta encima— no cambió.
const DIALOGO = '[data-testid="ficha-festival-modal"]';
const FONDO = '[data-testid="ficha-festival-fondo"]';

test('el modal de Crear un Festival se pinta por encima de la barra de navegación', { tag: '@backend' }, async ({ page }) => {
  // Se entra por la API y no por el formulario: lo que se está midiendo es el apilamiento del
  // modal, y recorrer la pantalla de acceso solo añadiría motivos ajenos por los que fallar.
  // `page.request` comparte el tarro de cookies del navegador, así que la sesión queda puesta.
  const entrada = await page.request.post('/api/v1/external/auth/login', {
    data: { email: CUENTA.email, password: CUENTA.password },
  });
  expect(entrada.status(), 'la cuenta externa de prueba tiene que poder entrar').toBe(200);

  await page.goto('/ecosistema/mi-panel?pestana=ecosistema');

  // SE ESPERA A QUE TERMINE LA ANIMACIÓN DE ENTRADA, y conviene saber por qué en vez de leerlo como
  // una espera defensiva. Mientras `pageSlideUp` corre, `<main>` tiene `opacity` por debajo de 1
  // —se midió 0,9979— y ES un contexto de apilamiento durante esos 600 ms; el modal quedaría
  // atrapado igual que antes. Fuera de esa ventana ya no lo es, que es la corrección. La ventana no
  // se alcanza a mano: hay que pulsar un botón, y `<main>` no se vuelve a crear al cambiar de ruta,
  // así que la animación solo corre una vez por carga del documento.
  await page.waitForFunction(() => {
    const principal = document.getElementById('contenido-principal');
    return !!principal && principal.getAnimations().every(a => a.playState === 'finished' || a.playState === 'idle');
  });

  const abrir = page.locator('[data-testid="abrir-crear-festival"]');
  await expect(abrir).toBeVisible();
  await abrir.click();

  await expect(page.locator(DIALOGO)).toBeVisible();

  // 1. EL TÍTULO SE LEE. Es lo que el usuario dijo que no veía: «el formulario sale cortado».
  await expect(page.getByRole('heading', { name: 'Crear un Festival' })).toBeVisible();

  // 2. NADIE PINTA POR ENCIMA DEL DIÁLOGO. Se preguntan cuatro puntos de su propio recuadro; si la
  //    barra estuviera delante, el punto de arriba devolvería el `<nav>` y no el diálogo.
  const tapado = await page.evaluate((selector) => {
    const dialogo = document.querySelector(selector) as HTMLElement | null;
    if (!dialogo) return ['no hay diálogo'];
    const r = dialogo.getBoundingClientRect();
    const puntos: Array<[string, number, number]> = [
      ['borde superior', r.left + r.width / 2, r.top + 3],
      ['borde izquierdo', r.left + 3, r.top + r.height / 2],
      ['centro', r.left + r.width / 2, r.top + r.height / 2],
      ['borde inferior', r.left + r.width / 2, r.bottom - 3],
    ];
    return puntos
      .filter(([, x, y]) => {
        const encima = document.elementFromPoint(x, y);
        return !encima || !dialogo.contains(encima);
      })
      .map(([nombre, x, y]) => {
        const encima = document.elementFromPoint(x, y) as HTMLElement | null;
        return `${nombre} (${Math.round(x)},${Math.round(y)}) lo tapa <${encima?.tagName.toLowerCase() ?? 'nada'}>`;
      });
  }, DIALOGO);
  expect(tapado, 'algo se pinta por delante del diálogo').toEqual([]);

  // 3. EL FONDO ATENÚA TAMBIÉN LA FRANJA DE LA BARRA. Es la otra mitad de lo que se pidió el 28 de
  //    agosto: «el formulario debe estar encima de la página completa». Con la barra delante, este
  //    punto —a 6 px del borde superior de la ventana, dentro de la barra— devolvía el <nav>.
  const enLaFranjaDeLaBarra = await page.evaluate((selector) => {
    const fondo = document.querySelector(selector) as HTMLElement | null;
    const encima = document.elementFromPoint(window.innerWidth / 2, 6) as HTMLElement | null;
    return { esElFondo: !!fondo && !!encima && fondo.contains(encima), etiqueta: encima?.tagName.toLowerCase() ?? 'nada' };
  }, FONDO);
  expect(enLaFranjaDeLaBarra.esElFondo, `en la franja de la barra manda <${enLaFranjaDeLaBarra.etiqueta}>`).toBe(true);
});
