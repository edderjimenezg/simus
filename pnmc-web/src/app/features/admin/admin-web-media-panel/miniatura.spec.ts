import { versionLigera, pesoLegible, etiquetaDeEstado } from './miniatura';

/*
  LO QUE EL PANEL DESCARGA PARA PINTAR UNA RANURA SIN PUBLICAR.

  Comprobado: las cuatro portadas del Home no tienen nada publicado, así
  que el panel enseña sus URLs compiladas. Tres traen `w=1470` y una `w=1015`, para cuatro
  recuadros que en pantalla miden 320 px de ancho.
*/
describe('versionLigera · el peso de las imágenes de fábrica en el panel', () => {
  it('reescribe el ancho de una URL de unsplash', () => {
    expect(versionLigera('https://images.unsplash.com/photo-123?q=80&w=1470&auto=format&fit=crop'))
      .toBe('https://images.unsplash.com/photo-123?q=80&w=320&auto=format&fit=crop');
  });

  it('respeta el ancho que se le pida', () => {
    expect(versionLigera('https://images.unsplash.com/photo-123?w=1015', 640))
      .toBe('https://images.unsplash.com/photo-123?w=640');
  });

  it('deja intacta una URL que no es de unsplash', () => {
    // `/assets/branding/pnmc-blanco.png` no acepta parámetros: añadírselos daría un 404 y un
    // hueco en el panel, que es peor que descargar de más.
    expect(versionLigera('/assets/branding/pnmc-blanco.png'))
      .toBe('/assets/branding/pnmc-blanco.png');
    expect(versionLigera('https://otro.sitio/foto.jpg?w=1470'))
      .toBe('https://otro.sitio/foto.jpg?w=1470');
  });

  it('deja intacta una URL de unsplash que no declara ancho', () => {
    expect(versionLigera('https://images.unsplash.com/photo-123?q=80'))
      .toBe('https://images.unsplash.com/photo-123?q=80');
  });

  it('no toca otros parámetros que terminen en w', () => {
    // `raw=1470` y `bw=1470` no son el ancho. Un `\d+` sin anclar al nombre exacto los pisaría.
    expect(versionLigera('https://images.unsplash.com/photo-123?bw=1470&w=1015'))
      .toBe('https://images.unsplash.com/photo-123?bw=1470&w=320');
  });
});

describe('pesoLegible y etiquetaDeEstado', () => {
  it('el peso se lee en la unidad que corresponde', () => {
    expect(pesoLegible(0)).toBe('—');
    expect(pesoLegible(null)).toBe('—');
    expect(pesoLegible(900)).toBe('900 B');
    expect(pesoLegible(2048)).toBe('2 KB');
    expect(pesoLegible(3 * 1024 * 1024)).toBe('3.0 MB');
  });

  it('cada estado tiene su rótulo', () => {
    expect(etiquetaDeEstado('publicado')).toBe('En el sitio');
    expect(etiquetaDeEstado('retirado')).toBe('Retirada');
    expect(etiquetaDeEstado('no_publicado')).toBe('Sin publicar');
  });
});
