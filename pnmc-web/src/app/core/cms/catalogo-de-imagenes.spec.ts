import {
  IMAGENES_DEL_BLOQUE,
  PRESENTACION_DE_LA_RANURA,
  PRESENTACION_NEUTRA,
  ROTAN_JUNTAS,
  WEB_IMAGE_GROUPS,
  WEB_IMAGE_KEYS,
  ranurasQueRotanCon,
} from './registro-de-imagenes-web';
import { WEB_TEXT_GROUPS } from './registro-de-textos-web';

/*
  QUE NINGUNA RANURA SE QUEDE FUERA DEL ESTUDIO, Y QUE NINGUNA NAZCA NEUTRA.

  Las dos tablas que sostienen el editor de imágenes dentro del bloque de texto —qué ranuras
  cuelgan de qué bloque, y cómo pinta el sitio cada una— son mapas escritos a mano. Un mapa a mano
  falla siempre de la misma forma: alguien añade la ranura 46 y no la apunta.

  Los dos modos de fallar son silenciosos y distintos:

   · fuera de `IMAGENES_DEL_BLOQUE`, la ranura EXISTE en el catálogo, el API la sirve y el panel
     de imágenes aparte la muestra, pero no hay ningún bloque del acordeón donde editarla. Nada
     falla; simplemente no se puede llegar.

   · fuera de `PRESENTACION_DE_LA_RANURA`, el recortador la enseña a todo color sobre gris
     oscuro. La persona elige bien una imagen que el sitio pinta en gris al 28 % sobre morado.
     Tampoco falla nada: sale una foto que no se parece a la que se eligió.
*/
describe('catálogo de imágenes · las dos tablas cubren las 44 ranuras', () => {
  const CLAVES = WEB_IMAGE_KEYS.map((x) => x.key);

  it('cada ranura del catálogo se edita dentro de algún bloque', () => {
    const enAlgunBloque = new Set(Object.values(IMAGENES_DEL_BLOQUE).flat());
    const sinBloque = CLAVES.filter((k) => !enAlgunBloque.has(k));

    expect(sinBloque)
      .withContext('estas ranuras no cuelgan de ningún bloque del acordeón: no hay forma de editarlas')
      .toEqual([]);
  });

  it('el mapa no nombra ranuras que no existen', () => {
    // El fallo simétrico, y el que produce una pantalla rota en vez de una ausencia: el bloque
    // pide al API un grupo de una clave inventada y la fila sale vacía sin decir por qué.
    const declaradas = new Set(CLAVES);
    const fantasmas = Object.values(IMAGENES_DEL_BLOQUE).flat().filter((k) => !declaradas.has(k));

    expect(fantasmas).toEqual([]);
  });

  it('la clave del mapa es siempre un grupo de TEXTO que existe', () => {
    // Se equivocó una vez en la primera escritura: se puso `marca`, que es el id del grupo de
    // IMAGEN, y las cuatro ranuras de marca se quedaron sin bloque sin que nada fallara.
    const grupos = new Set(WEB_TEXT_GROUPS.map((g) => g.id));
    const inventados = Object.keys(IMAGENES_DEL_BLOQUE).filter((id) => !grupos.has(id));

    expect(inventados).toEqual([]);
  });

  it('una ranura no se edita desde dos bloques a la vez', () => {
    // Dos bloques con la misma clave dan dos editores para el mismo archivo, con estados que se
    // pisan: subir en uno deja al otro enseñando la imagen anterior bajo el rótulo «hoy».
    const vistas = new Set<string>();
    const repetidas: string[] = [];
    for (const clave of Object.values(IMAGENES_DEL_BLOQUE).flat()) {
      if (vistas.has(clave)) { repetidas.push(clave); }
      vistas.add(clave);
    }

    expect(repetidas).toEqual([]);
  });

  it('cada ranura declara cómo la pinta el sitio, y ninguna cae en el neutro', () => {
    const neutras = CLAVES.filter((k) => !PRESENTACION_DE_LA_RANURA[k]);

    expect(neutras)
      .withContext('estas ranuras se recortarían sin el filtro ni el fondo con que el sitio las pinta')
      .toEqual([]);
  });

  it('el tratamiento neutro sigue existiendo como red, pero no lo usa ninguna ranura', () => {
    // No se borra: es lo que ve una ranura recién declarada antes de que alguien la mida, y
    // enseñar algo es mejor que romper el recortador. Lo que no puede es estar en uso.
    expect(PRESENTACION_NEUTRA.filtro).toBe('none');
    expect(Object.values(PRESENTACION_DE_LA_RANURA)).not.toContain(PRESENTACION_NEUTRA);

    // POR VALOR, NO SOLO POR IDENTIDAD. Comparar referencias deja pasar el caso realista: alguien
    // escribe el objeto a mano con los mismos cuatro campos porque «no sabe todavía cómo se pinta
    // esa ranura». Ninguna de las 44 medidas coincide con el neutro: los cuatro logotipos son lo
    // más cerca que hay, y su fondo es el morado de marca, no el gris.
    const comoElNeutro = Object.entries(PRESENTACION_DE_LA_RANURA)
      .filter(([, p]) => JSON.stringify(p) === JSON.stringify(PRESENTACION_NEUTRA))
      .map(([clave]) => clave);

    expect(comoElNeutro).toEqual([]);
  });

  it('la sección de una ranura es la de la página donde se ve', () => {
    // Hasta las tres portadas de página vivían en un solo grupo
    // declarado en «Sobre PNMC», así que la portada de /ejes se editaba desde la pestaña de
    // Sobre PNMC. Con el editor dentro del bloque de texto, eso deja de ser un detalle.
    const seccionDelGrupoDeTexto = new Map(WEB_TEXT_GROUPS.map((g) => [g.id, g.section]));
    const seccionDeLaRanura = new Map(WEB_IMAGE_KEYS.map((x) => [x.key, x.section]));
    const descuadradas: string[] = [];

    for (const [bloque, claves] of Object.entries(IMAGENES_DEL_BLOQUE)) {
      const seccion = seccionDelGrupoDeTexto.get(bloque);
      for (const clave of claves) {
        if (seccionDeLaRanura.get(clave) !== seccion) {
          descuadradas.push(`${clave} (${seccionDeLaRanura.get(clave)}) en ${bloque} (${seccion})`);
        }
      }
    }

    expect(descuadradas).toEqual([]);
  });

  it('los grupos de imagen no mezclan secciones', () => {
    const mezclados = WEB_IMAGE_GROUPS
      .filter((g) => new Set(g.images.map(() => g.section)).size !== 1)
      .map((g) => g.id);

    expect(mezclados).toEqual([]);
  });

  it('lo que rota junto son claves reales, y ninguna rota en dos grupos', () => {
    const declaradas = new Set(CLAVES);
    const vistas = new Set<string>();

    for (const grupo of ROTAN_JUNTAS) {
      expect(grupo.length).toBeGreaterThan(1);
      for (const clave of grupo) {
        expect(declaradas.has(clave)).withContext(clave + ' no existe en el catálogo').toBeTrue();
        expect(vistas.has(clave)).withContext(clave + ' rota en dos grupos').toBeFalse();
        vistas.add(clave);
      }
    }

    // Y la función devuelve el grupo entero, incluida la clave por la que se pregunta: el editor
    // espeja sobre TODAS, también sobre la que se está cambiando.
    for (const grupo of ROTAN_JUNTAS) {
      expect([...ranurasQueRotanCon(grupo[0])]).toEqual([...grupo]);
    }
    expect(ranurasQueRotanCon('no_existe')).toEqual([]);
  });
});
