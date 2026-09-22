import { ESTRATEGIAS_COMPILADAS, resolveStrategy } from './resolve-strategy';
import { WEB_TEXT_GROUPS, WEB_TEXT_KEY_INDEX } from './registro-de-textos-web';

/** Las claves que la función le pide al CMS para una estrategia dada. */
function clavesPedidas(tipo: 'circulacion' | 'investigacion'): string[] {
  const pedidas: string[] = [];
  resolveStrategy(ESTRATEGIAS_COMPILADAS[tipo], (clave) => { pedidas.push(clave); return ''; });
  return pedidas;
}

describe('resolveStrategy', () => {
  it('Territorios Sonoros gobierna sus dos textos y NO pinta un relato en blanco', () => {
    // Hasta tenía `cmsPrefix: null`, y eso dejaba su FOTO administrable
    // sin ningún bloque del panel donde editarse: el editor de imágenes vive dentro del bloque de
    // los textos que la acompañan.
    //
    // Se le conectaron las DOS que la página pinta, con el texto que ya venía mostrando. El
    // relato de tres partes que sí tiene Celebra la Música se queda fuera: esa página no lo
    // renderiza, así que declarar sus siete claves obligaría a escribir copia institucional que
    // hoy no existe solo para llenar campos.
    const territorios = resolveStrategy(ESTRATEGIAS_COMPILADAS.investigacion, (k) => `<${k}>`);

    expect(clavesPedidas('investigacion'))
      .toEqual(['strategy_territorios_hero_desc', 'strategy_territorios_intro']);
    expect(territorios.title).toBe('Territorios Sonoros');
    expect(territorios.description).toBe('<strategy_territorios_hero_desc>');
    expect(territorios.text).toBe('<strategy_territorios_intro>');

    // `null`, NO un relato de tres cadenas vacías: la plantilla decide con
    // `@if (strategy().narrative)`, y un objeto vacío pintaría el bloque entero en blanco —con
    // sus títulos y sus separadores— sobre la página pública.
    expect(territorios.narrative).toBeNull();
  });

  it('las dos estrategias declaran en el panel exactamente lo que leen', () => {
    // El grupo del panel y lo que la función pide tienen que ser el mismo conjunto en las dos
    // direcciones: un campo declarado y no leído es una pestaña cosmética, y un campo leído y no
    // declarado devuelve cadena vacía y borra texto de la página pública.
    for (const [tipo, grupoId] of [
      ['circulacion', 'strategy_celebra_details'],
      ['investigacion', 'strategy_territorios_details'],
    ] as const) {
      const grupo = WEB_TEXT_GROUPS.find((g) => g.id === grupoId);
      const declaradas = [...grupo!.fields.map((f) => f.key)].sort();
      const leidas = [...clavesPedidas(tipo)].sort();

      expect(leidas).withContext(grupoId).toEqual(declaradas);
    }
  });

  it('mapea cada clave de Celebra la Música a su lugar', () => {
    const celebra = resolveStrategy(ESTRATEGIAS_COMPILADAS.circulacion, (k) => `<${k}>`);

    expect(celebra.description).toBe('<strategy_celebra_hero_desc>');
    expect(celebra.text).toBe('<strategy_celebra_intro>');
    expect(celebra.narrative?.sectionTitle).toBe('<strategy_celebra_section_title>');
    expect(celebra.narrative?.mission).toBe('<strategy_celebra_mission>');
    expect(celebra.narrative?.edition).toEqual([
      '<strategy_celebra_edition_intro>',
      '<strategy_celebra_edition_vision>',
      '<strategy_celebra_edition_closing>',
    ]);
  });

  it('«Estrategias» deja de ser una pestaña cosmética: sus 7 campos tienen lector', () => {
    // Esta es la prueba que importa. Era la única pestaña del panel cuyos campos
    // no leía ningún componente: la editora escribía, publicaba, y la página
    // seguía igual. Si alguien añade un octavo campo al grupo y no lo conecta,
    // esto lo dice aquí y no meses después.
    const grupo = WEB_TEXT_GROUPS.find((g) => g.id === 'strategy_celebra_details');
    const declaradas = grupo!.fields.map((f) => f.key);
    const leidas = new Set(clavesPedidas('circulacion'));

    expect(declaradas.filter((k) => !leidas.has(k))).toEqual([]);
    expect(declaradas.length).toBe(7);
  });

  it('todas las claves que pide existen en el registro del panel', () => {
    const desconocidas = clavesPedidas('circulacion').filter((k) => !WEB_TEXT_KEY_INDEX.has(k));

    expect(desconocidas).toEqual([]);
  });

  it('respeta un valor publicado en blanco en vez de sustituirlo', () => {
    // PNMC-040: el endpoint público solo devuelve claves con `Publicado != null`,
    // así que una cadena vacía significa una sola cosa —alguien la publicó así— y
    // el sitio la obedece. Quien evita el hueco es la plantilla, que no pinta un
    // título vacío; no esta función inventando un texto de reemplazo.
    const celebra = resolveStrategy(ESTRATEGIAS_COMPILADAS.circulacion, (k) =>
      k === 'strategy_celebra_section_title' ? '' : 'texto',
    );

    expect(celebra.narrative?.sectionTitle).toBe('');
  });

  it('no muta la configuración compilada', () => {
    const antes = JSON.stringify(ESTRATEGIAS_COMPILADAS);
    const celebra = resolveStrategy(ESTRATEGIAS_COMPILADAS.circulacion, () => 'otra cosa');
    celebra.pillars.push('añadido por la prueba');

    expect(JSON.stringify(ESTRATEGIAS_COMPILADAS)).toBe(antes);
  });
});
