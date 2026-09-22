import { expect, test } from '@playwright/test';

const ESTILOS_CARTOGRAFIA = [
  ['simus-estilo-leaflet', '/mapa/leaflet/leaflet.css'],
  ['simus-estilo-maplibre', '/mapa/maplibre/maplibre-gl.css'],
  ['simus-estilo-agrupaciones', '/mapa/markercluster/MarkerCluster.css'],
  ['simus-estilo-agrupaciones-tema', '/mapa/markercluster/MarkerCluster.Default.css'],
] as const;

test('los estilos cartográficos se difieren hasta abrir el mapa y existen en producción', async ({ page, request }) => {
  await page.goto('/');

  for (const [id] of ESTILOS_CARTOGRAFIA) {
    await expect(page.locator(`#${id}`)).toHaveCount(0);
  }

  await page.goto('/mapa-ecosistemico');

  for (const [id, ruta] of ESTILOS_CARTOGRAFIA) {
    await expect(page.locator(`#${id}`)).toHaveAttribute('href', ruta);
    const respuesta = await request.get(ruta);
    expect(respuesta.status(), `${ruta} no quedó incluido en el paquete`).toBe(200);
  }
});
