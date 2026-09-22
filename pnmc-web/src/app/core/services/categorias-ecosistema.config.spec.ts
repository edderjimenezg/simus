import { Routes } from '@angular/router';
import { routes } from '../../app.routes';
import { CATEGORIAS_ECOSISTEMA } from './categorias-ecosistema.config';

/**
 * LA RUTA QUE DECLARA EL CATALOGO TIENE QUE EXISTIR EN EL ENRUTADOR.
 *
 * la dirección de producto entró a `/ecosistema/mercados-musicales` —el
 * destino que enlazaban el menú del portal y este mismo catálogo— y no vio nada de mercados: el
 * enrutador había registrado el directorio como `ecosistema/mercados`, con el nombre corto, así que
 * la dirección caía en el comodín de «página no encontrada». No lo detectó ninguna prueba porque
 * cada lado se probaba por su cuenta: el catálogo decía una cosa, el enrutador otra, y nadie
 * comparaba las dos listas.
 *
 * Esta prueba es esa comparación. No mira cómo se ve la página: mira que el destino que las tres
 * superficies del Ecosistema ofrecen —portada, menú del portal y vista previa del Inicio— sea uno
 * que el enrutador sepa resolver.
 */
describe('Las rutas del catálogo del Ecosistema', () => {
  /** Todos los `path` declarados, incluidos los de las rutas hijas. */
  function caminosDeclarados(rutas: Routes, prefijo = ''): string[] {
    return rutas.flatMap(ruta => {
      const camino = [prefijo, ruta.path ?? ''].filter(Boolean).join('/');
      return [camino, ...caminosDeclarados(ruta.children ?? [], camino)];
    });
  }

  const caminos = new Set(caminosDeclarados(routes));

  for (const categoria of CATEGORIAS_ECOSISTEMA) {
    if (!categoria.route) { continue; }

    it(`«${categoria.title}» lleva a una ruta que existe: ${categoria.route}`, () => {
      expect(caminos.has(categoria.route!))
        .withContext(`«${categoria.route}» no está en app.routes.ts, así que el enlace cae en «página no encontrada»`)
        .toBeTrue();
    });
  }

  it('un proceso marcado como disponible tiene siempre a dónde llevar', () => {
    // La otra mitad del mismo defecto: marcar un proceso como disponible es un cambio de una
    // palabra, y si nadie le da ruta el visitante llega a la misma pantalla de «no encontrado».
    const disponiblesSinRuta = CATEGORIAS_ECOSISTEMA
      .filter(categoria => categoria.status === 'Disponible' && !categoria.route)
      .map(categoria => categoria.title);

    expect(disponiblesSinRuta).toEqual([]);
  });
});
