import type { Locator, Page } from '@playwright/test';

/**
 * ¿Está este elemento DENTRO de la ventana, o solo «visible»?
 *
 * `isVisible()` de Playwright devuelve `true` para un elemento desplazado fuera
 * de pantalla con `transform`/`translate`: sigue teniendo caja, sigue teniendo
 * tamaño, y su `display` y `visibility` son normales. El cajón de navegación
 * cerrado (`-translate-x-full`) cumple todo eso.
 *
 * La consecuencia práctica: comprobar `isVisible()` para decidir si hace falta
 * abrir el cajón daba siempre `true`, no se abría, y el clic posterior moría con
 * «element is outside of the viewport» tras veinte segundos.
 *
 * Es la MISMA trampa que apareció midiendo el ancho de la barra lateral, donde
 * `getBoundingClientRect().width` seguía dando 288 px con el cajón cerrado. En
 * ambos casos lo que cuenta es la POSICIÓN dentro de la ventana, no el tamaño
 * ni la visibilidad declarada.
 */
export async function dentroDeLaVentana(page: Page, locator: Locator): Promise<boolean> {
  const caja = await locator.boundingBox().catch(() => null);
  if (!caja) return false;
  const ventana = page.viewportSize();
  if (!ventana) return false;
  return (
    caja.x + caja.width > 1 &&
    caja.x < ventana.width - 1 &&
    caja.y + caja.height > 1 &&
    caja.y < ventana.height + caja.height
  );
}
