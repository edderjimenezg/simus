import { ColumnaDeTabla, OrdenDeTabla } from './orden-de-tabla';

/**
 * Una sola regla de orden para las diez tablas de la consola.
 *
 * <b>LO QUE FIJA ESTE FICHERO.</b> Primero ascendente, segundo al revés. Y, en las tablas cuyo
 * orden de partida no es el de ninguna columna —«sin publicar primero» en Agenda y Noticias,
 * «pendientes primero» en Festivales—, un tercero que devuelve a ese orden propio. Esa tercera
 * posición sustituye al botón que Festivales tenía en la barra, y que la dirección de producto señaló
 * porque se parecía a un filtro sin serlo.
 */
describe('el orden de una tabla administrativa', () => {
  const COLUMNA: ColumnaDeTabla = { id: 'titulo', etiqueta: 'Título' };

  describe('cuando su orden de partida ES el de una columna', () => {
    it('alterna entre los dos sentidos y no tiene un tercer estado', () => {
      // La bitácora abre por fecha descendente y el boletín por alta descendente: un tercer golpe
      // que «volviera al orden propio» las dejaría igual, y sería un clic que no hace nada.
      const orden = new OrdenDeTabla('fecha', 'desc');

      orden.alternar('fecha');
      expect(orden.direccion()).toBe('asc');
      orden.alternar('fecha');
      expect(orden.direccion()).toBe('desc');
      orden.alternar('fecha');
      expect(orden.columna()).toBe('fecha');
      expect(orden.direccion()).toBe('asc');
    });
  });

  describe('cuando tiene un orden propio que ninguna columna representa', () => {
    function crear(): OrdenDeTabla {
      return new OrdenDeTabla('', 'asc', 'Sin publicar primero');
    }

    it('abre sin ninguna cabecera marcada', () => {
      const orden = crear();

      expect(orden.columna()).toBe('');
      expect(orden.enSuOrdenPropio()).toBe(true);
      expect(orden.flechaDe('titulo')).toBe('↕');
      expect(orden.ariaDe('titulo')).toBe('none');
    });

    it('el tercer golpe devuelve la lista a su orden propio', () => {
      const orden = crear();

      orden.alternar('titulo');
      expect(orden.columna()).toBe('titulo');
      expect(orden.direccion()).toBe('asc');

      orden.alternar('titulo');
      expect(orden.direccion()).toBe('desc');

      orden.alternar('titulo');
      expect(orden.columna()).toBe('');
      expect(orden.enSuOrdenPropio()).toBe(true);
    });

    it('el título del botón dice por su nombre a qué se vuelve', () => {
      const orden = crear();

      expect(orden.tituloDe(COLUMNA)).toBe('Ordenar por Título');
      orden.alternar('titulo');
      expect(orden.tituloDe(COLUMNA)).toBe('Ordenar por Título, al revés');
      orden.alternar('titulo');
      // TRES GOLPES SEGUIDOS NO DEJAN ADIVINAR QUE EL TERCERO DESHACE EL ORDEN: hay que decirlo.
      expect(orden.tituloDe(COLUMNA)).toBe('Volver a «Sin publicar primero»');
    });

    it('cambiar de columna nunca hereda el sentido de la anterior', () => {
      const orden = crear();

      orden.alternar('titulo');
      orden.alternar('titulo');
      expect(orden.direccion()).toBe('desc');

      orden.alternar('estado');
      expect(orden.direccion()).toBe('asc');
    });
  });

  describe('ordenando filas en memoria', () => {
    it('lo que no tiene valor va al final, mande el sentido que mande', () => {
      const orden = new OrdenDeTabla('nombre', 'asc');
      const filas = [{ nombre: 'Zamba' }, { nombre: '' }, { nombre: 'Abrazo' }];

      expect(orden.ordenarFilas(filas, f => f.nombre).map(f => f.nombre)).toEqual(['Abrazo', 'Zamba', '']);

      orden.alternar('nombre');
      expect(orden.ordenarFilas(filas, f => f.nombre).map(f => f.nombre)).toEqual(['Zamba', 'Abrazo', '']);
    });

    it('ordena con las reglas del español y no por código de carácter', () => {
      // Con `<`, la «Ñ» quedaría detrás de la «Z».
      const orden = new OrdenDeTabla('nombre', 'asc');
      const filas = [{ nombre: 'Zapata' }, { nombre: 'Ñandú' }, { nombre: 'Nubes' }];

      expect(orden.ordenarFilas(filas, f => f.nombre).map(f => f.nombre)).toEqual(['Nubes', 'Ñandú', 'Zapata']);
    });
  });
});
