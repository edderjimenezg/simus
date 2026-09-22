import { resolveEjes } from './resolve-ejes';
import { ejesDataGlobal } from '../services/ejes-data.config';
import { DEFAULT_TEXTS, WEB_TEXT_GROUPS, WEB_TEXT_KEY_INDEX } from './registro-de-textos-web';
import type { EjeGroup } from '../services/ejes-data.config';

const ejeDePrueba: EjeGroup[] = [
  {
    id: '01',
    title: 'TÍTULO COMPILADO',
    axisExplain: ['Primer párrafo compilado.', 'Segundo párrafo compilado.'],
    purpose: 'Propósito compilado.',
    videoImg: 'x.jpg',
    components: [
      { id: 'c1-1', slug: 'componente-compilado', name: 'Componente compilado', details: 'Detalle compilado.', fullText: ['Detalle compilado. Y sigue.'] },
    ],
  },
];

describe('resolveEjes', () => {
  it('la estructura sigue saliendo de la configuración compilada', () => {
    // El panel gobierna el TEXTO; cuántos párrafos y cuántos componentes hay, no.
    const [eje] = resolveEjes(ejeDePrueba, (k) => `<${k}>`);
    expect(eje.axisExplain.length).toBe(2);
    expect(eje.components.length).toBe(1);
    expect(eje.components[0].details).toBe('Detalle compilado.');
  });

  it('un valor publicado en blanco se respeta: borrar borra', () => {
    // PNMC-040. Antes esto caía al texto compilado y el resultado era que
    // borrar no borraba: el panel confirmaba el cambio y la página seguía igual.
    // El endpoint público solo devuelve claves con `Publicado != null`, así que
    // una cadena vacía significa una sola cosa —alguien la publicó así—.
    const [eje] = resolveEjes(ejeDePrueba, (k) => (k === 'eje01_purpose' ? '' : 'X'));
    expect(eje.purpose).toBe('');
  });

  it('mapea cada clave a su lugar, incluidos los componentes', () => {
    const [eje] = resolveEjes(ejeDePrueba, (k) => `<${k}>`);
    expect(eje.title).toBe('<eje01_title>');
    expect(eje.axisExplain).toEqual(['<eje01_desc1>', '<eje01_desc2>']);
    expect(eje.purpose).toBe('<eje01_purpose>');
    expect(eje.components[0].name).toBe('<eje01_c1_title>');
    expect(eje.components[0].fullText[0]).toBe('<eje01_c1_desc>');
  });

  it('`_desc` gobierna solo el primer párrafo; los demás siguen compilados', () => {
    // El panel ofrece UN campo por componente, no una lista de párrafos. Que
    // los siguientes no se toquen es parte del contrato, no un olvido.
    const conDos: EjeGroup[] = [{
      ...ejeDePrueba[0],
      components: [{
        ...ejeDePrueba[0].components[0],
        fullText: ['Primero compilado.', 'Segundo compilado.'],
      }],
    }];

    const [eje] = resolveEjes(conDos, (k) => `<${k}>`);

    expect(eje.components[0].fullText).toEqual(['<eje01_c1_desc>', 'Segundo compilado.']);
  });

  it('el panel siembra EXACTAMENTE el párrafo que la página muestra', () => {
    // Esta prueba es la que permite que `_desc` esté conectada.
    //
    // Estuvo suelta un tiempo porque en 7 de los 10 componentes el valor
    // sembrado era una redacción más corta que el párrafo publicado: atarla
    // habría recortado copia del portal. Se alineó el registro con la página
    // —no al revés—, y desde entonces conectar no cambia nada a la vista.
    //
    // Si alguien edita `ejes-data.config.ts` y no vuelve a alinear el registro,
    // la discrepancia reaparece y el síntoma sería que el sitio cambia solo al
    // sembrar una base nueva. Salta aquí.
    const desalineadas = ejesDataGlobal.flatMap((eje, i) =>
      eje.components
        .map((componente, k) => ({ clave: `eje0${i + 1}_c${k + 1}_desc`, componente }))
        .filter(({ clave, componente }) => DEFAULT_TEXTS[clave] !== componente.fullText[0])
        .map(({ clave }) => clave),
    );

    expect(desalineadas).toEqual([]);
  });

  it('no muta la configuración compilada', () => {
    const antes = JSON.stringify(ejeDePrueba);
    resolveEjes(ejeDePrueba, () => 'otra cosa');
    expect(JSON.stringify(ejeDePrueba)).toBe(antes);
  });

  it('todas las claves que produce existen en el registro del panel', () => {
    // La red de seguridad de la convención posicional: si alguien añade un
    // componente a un eje y no declara su clave, esto lo dice aquí y no en
    // producción, donde el síntoma sería «edito y no pasa nada».
    const pedidas = new Set<string>();
    resolveEjes(ejesDataGlobal, (clave) => { pedidas.add(clave); return ''; });

    const desconocidas = [...pedidas].filter((k) => !WEB_TEXT_KEY_INDEX.has(k));
    expect(desconocidas).toEqual([]);
    // Exacto, no «más de 20»: con la cota floja, borrar el eje 3 entero dejaba
    // 24 claves y la prueba seguía verde.
    expect(pedidas.size).toBe(32);
  });

  it('y al revés: ninguna clave declarada se queda sin que nadie la pida', () => {
    // La dirección que faltaba, y la que de verdad protege al equipo editorial.
    //
    // La prueba de arriba comprueba configuración → registro: que toda clave que
    // el código pide esté declarada. Pero el error caro es el contrario:
    // **declarar `eje04_title` sin tocar la configuración de ejes**. Esa clave
    // llega a la base, aparece en el panel, se puede escribir y publicar… y
    // ninguna página la lee. El síntoma es «edito y no pasa nada».
    //
    // `cms:huerfanas` no puede atraparlo: exime estas claves por patrón, y el
    // patrón lleva TRES números —eje, párrafo y componente— y el del componente
    // cambia por eje (2, 6, 2). Acotarlo allí exigiría repetir ese reparto en un
    // segundo archivo, que es justo la desincronización que la puerta persigue.
    // Aquí no hace falta: se pregunta al propio resolvedor.
    const pedidas = new Set<string>();
    resolveEjes(ejesDataGlobal, (clave) => { pedidas.add(clave); return ''; });

    // POR GRUPO Y NO POR SECCIÓN. Hasta «Ejes» tenía exactamente los
    // tres grupos que este resolvedor gobierna, así que filtrar por sección daba lo mismo. Ese
    // día entraron dos grupos más —el encabezado de la página y la ficha de componente— que los
    // leen sus propias plantillas, y filtrar por sección los reclamaba aquí.
    //
    // La guarda no se afloja: lo que se comprueba es que ninguna clave DE ESTOS TRES GRUPOS se
    // quede sin lector. Las de los otros dos son literales en la plantilla y las cubre
    // `npm run cms:huerfanas`, que es la puerta que persigue exactamente eso.
    const GOBERNADOS = ['eje1_details', 'eje2_details', 'eje3_details'];
    const declaradas = WEB_TEXT_GROUPS
      .filter((grupo) => GOBERNADOS.includes(grupo.id))
      .flatMap((grupo) => grupo.fields.map((campo) => campo.key));

    expect(declaradas.filter((clave) => !pedidas.has(clave))).toEqual([]);
    expect(declaradas.length).toBe(32);
  });
});
