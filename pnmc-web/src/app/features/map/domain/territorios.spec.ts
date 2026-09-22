import * as MapDomain from './map-domain';

/**
 * Las dos piezas de dominio que hicieron falta para conectar la cartografía territorial
 * con los datos del geovisor.
 *
 * EL CONTEXTO, PORQUE EXPLICA LA FORMA. Al partir el TopoJSON monolítico de 28 MB en
 * departamentos + un fragmento por departamento, la colección de municipios embebida
 * quedó vacía. El componente seguía filtrándola y abrir un departamento no dibujaba un
 * solo municipio. Ninguna prueba se puso en rojo: filtrar una lista vacía devuelve una
 * lista vacía, y eso es correcto.
 *
 * Para pedir el fragmento hay que traducir: el desplegable habla de NOMBRES y el API de
 * CÓDIGOS DIVIPOLA. Esa traducción se calculaba dentro de `setRuntimeDepartmentCatalog`
 * y se tiraba. De ahí `getDepartmentCodeByName`.
 *
 * Y los rótulos existían pero nadie los encendía: el CSS los deja en `opacity: 0` hasta
 * que algo añade una clase al contenedor. De ahí `labelVisibilityForZoom`.
 */

const CATALOGO = [
  { properties: { dpto_ccdgo: '05', dpto_cnmbr: 'ANTIOQUIA' } },
  { properties: { dpto_ccdgo: '76', dpto_cnmbr: 'VALLE DEL CAUCA' } },
  { properties: { dpto_ccdgo: '08', dpto_cnmbr: 'ATLÁNTICO' } },
  { properties: { dpto_ccdgo: '88', dpto_cnmbr: 'ARCHIPIÉLAGO DE SAN ANDRÉS, PROVIDENCIA Y SANTA CATALINA' } },
];

describe('getDepartmentCodeByName · del nombre al código DIVIPOLA', () => {
  beforeEach(() => MapDomain.setRuntimeDepartmentCatalog(CATALOGO));
  afterAll(() => MapDomain.setRuntimeDepartmentCatalog([]));

  it('traduce el nombre que muestra el desplegable al código que pide el API', () => {
    expect(MapDomain.getDepartmentCodeByName('ANTIOQUIA')).toBe('05');
    expect(MapDomain.getDepartmentCodeByName('VALLE DEL CAUCA')).toBe('76');
  });

  it('no depende de tildes ni de mayúsculas, que es donde se rompía', () => {
    expect(MapDomain.getDepartmentCodeByName('atlántico')).toBe('08');
    expect(MapDomain.getDepartmentCodeByName('ATLANTICO')).toBe('08');
    expect(MapDomain.getDepartmentCodeByName('Atlantico')).toBe('08');
  });

  it('resuelve el archipiélago, que se escribe de varias maneras', () => {
    expect(MapDomain.getDepartmentCodeByName('ARCHIPIÉLAGO DE SAN ANDRÉS, PROVIDENCIA Y SANTA CATALINA')).toBe('88');
  });

  it('devuelve cadena vacía si no lo conoce, y no un código de otro', () => {
    // Importa: el componente usa este valor para decidir si pide el fragmento. Devolver
    // un codigo equivocado pintaria los municipios de otro departamento.
    expect(MapDomain.getDepartmentCodeByName('NARNIA')).toBe('');
    expect(MapDomain.getDepartmentCodeByName('')).toBe('');
    expect(MapDomain.getDepartmentCodeByName(undefined as any)).toBe('');
  });

  it('es el camino inverso exacto de getDepartmentNameByCode', () => {
    for (const feature of CATALOGO) {
      const codigo = feature.properties.dpto_ccdgo;
      expect(MapDomain.getDepartmentCodeByName(MapDomain.getDepartmentNameByCode(codigo))).toBe(codigo);
    }
  });
});

describe('getDepartmentLabelName · el nombre que cabe sobre el mapa', () => {
  beforeEach(() => MapDomain.setRuntimeDepartmentCatalog(CATALOGO));
  afterAll(() => MapDomain.setRuntimeDepartmentCatalog([]));

  it('al archipiélago le da la forma corta, no el nombre completo', () => {
    // El completo mide 56 caracteres y, con la tipografía del rótulo, 311 px en
    // pantalla: casi el triple que «NORTE DE SANTANDER», el departamento de nombre más
    // largo que existe, con 112. Medido con `mapa:rotulos`.
    expect(MapDomain.getDepartmentLabelName('SAN ANDRES Y PROVIDENCIA')).toBe('San Andrés y Providencia');
  });

  it('la forma corta es de verdad más corta que la del desplegable', () => {
    // Si las dos funciones devolvieran lo mismo, esta pieza no estaría haciendo nada y
    // el rótulo volvería a cruzar la cuenca sin que nadie se enterara.
    const rotulo = MapDomain.getDepartmentLabelName('SAN ANDRES Y PROVIDENCIA');
    const completo = MapDomain.getDepartmentDisplayName('SAN ANDRES Y PROVIDENCIA');

    expect(rotulo.length).toBeLessThan(completo.length);
    expect(completo).toContain('Archipiélago');
  });

  it('no depende de tildes ni de mayúsculas, como el resto de la familia', () => {
    for (const escrito of ['san andrés y providencia', 'SAN ANDRÉS Y PROVIDENCIA', 'San Andres y Providencia']) {
      expect(MapDomain.getDepartmentLabelName(escrito)).withContext(escrito).toBe('San Andrés y Providencia');
    }
  });

  it('a los demás departamentos los deja como están', () => {
    // La excepción es una y está justificada por su ancho. Acortar los demás cambiaría
    // el nombre que el mapa y el desplegable dicen del mismo territorio.
    for (const nombre of ['ANTIOQUIA', 'VALLE DEL CAUCA', 'ATLÁNTICO']) {
      expect(MapDomain.getDepartmentLabelName(nombre)).toBe(MapDomain.getDepartmentDisplayName(nombre));
    }
  });

  it('sin nombre no inventa uno', () => {
    expect(MapDomain.getDepartmentLabelName('')).toBe(MapDomain.getDepartmentDisplayName(''));
  });
});

describe('labelVisibilityForZoom · qué rótulos se leen a cada distancia', () => {
  it('a escala nacional se nombran los departamentos y no los municipios', () => {
    // El geovisor abre en torno a 5,5. Con el umbral departamental en 7 que pedia la
    // especificacion vieja no se leia un solo nombre al entrar.
    const nacional = MapDomain.labelVisibilityForZoom(5.5);
    expect(nacional.departments).toBeTrue();
    expect(nacional.municipalities).toBeFalse();
  });

  it('con un departamento abierto se nombran también sus municipios', () => {
    // Abrir un departamento encuadra entre 7 y 8,5.
    const departamental = MapDomain.labelVisibilityForZoom(7.5);
    expect(departamental.departments).toBeTrue();
    expect(departamental.municipalities).toBeTrue();
  });

  it('muy lejos no se nombra nada', () => {
    const lejos = MapDomain.labelVisibilityForZoom(4);
    expect(lejos.departments).toBeFalse();
    expect(lejos.municipalities).toBeFalse();
  });

  it('los municipios nunca aparecen antes que los departamentos', () => {
    // Si se invirtieran los umbrales, la vista nacional intentaria nombrar municipios.
    expect(MapDomain.MUNICIPALITY_LABEL_MIN_ZOOM).toBeGreaterThanOrEqual(MapDomain.DEPARTMENT_LABEL_MIN_ZOOM);
    for (const zoom of [4, 5, 6, 7, 8, 9, 12]) {
      const v = MapDomain.labelVisibilityForZoom(zoom);
      if (v.municipalities) expect(v.departments).toBeTrue();
    }
  });

  it('un zoom que no es un número no enciende nada', () => {
    for (const malo of [NaN, undefined, null, 'ocho']) {
      const v = MapDomain.labelVisibilityForZoom(malo as any);
      expect(v.departments).toBeFalse();
      expect(v.municipalities).toBeFalse();
    }
  });
});
