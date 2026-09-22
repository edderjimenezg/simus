import { nombrePropio } from './nombre-propio';

/**
 * El nombre territorial, escrito como se escribe.
 *
 * <b>EL DEFECTO QUE FIJA.</b> El 14 de septiembre de 2026 la ficha de la organización enseñaba
 * «HUILA» y «LA PLATA» a pantalla completa, porque es como el DANE publica `dbo.Divipola` y la
 * ficha pintaba el valor crudo. Los casos raros de esta tabla —una sigla con punto, un guion
 * entre dos nombres y un nombre con coma y conjunción— son los que se comprueban aquí.
 */
describe('nombrePropio', () => {
  it('escribe el nombre en su forma normal', () => {
    expect(nombrePropio('HUILA')).toBe('Huila');
    expect(nombrePropio('LA PLATA')).toBe('La Plata');
    expect(nombrePropio('SANTANDER')).toBe('Santander');
  });

  it('deja en minúscula los enlaces que no abren el nombre', () => {
    expect(nombrePropio('CIÉNAGA DE ORO')).toBe('Ciénaga de Oro');
    expect(nombrePropio('VALLE DEL CAUCA')).toBe('Valle del Cauca');
    expect(nombrePropio('EL CARMEN DE VIBORAL')).toBe('El Carmen de Viboral');
  });

  it('no estropea las siglas con punto', () => {
    expect(nombrePropio('BOGOTÁ, D.C.')).toBe('Bogotá, D.C.');
  });

  it('respeta los guiones y las comas de la fuente', () => {
    expect(nombrePropio('MIRITÍ - PARANÁ')).toBe('Mirití - Paraná');
    expect(nombrePropio('ARCHIPIÉLAGO DE SAN ANDRÉS, PROVIDENCIA Y SANTA CATALINA'))
      .toBe('Archipiélago de San Andrés, Providencia y Santa Catalina');
  });

  it('no recompone un nombre que ya venía escrito a mano', () => {
    // Si se recompusiera, «Fundación de Prueba» seguiría igual pero «FUNDACIÓN MusicAndina»
    // perdería la mayúscula interior. La regla es no tocar lo que no viene en mayúsculas.
    expect(nombrePropio('Fundación de Prueba')).toBe('Fundación de Prueba');
    expect(nombrePropio('Bogotá, D.C.')).toBe('Bogotá, D.C.');
  });

  it('un valor vacío vuelve vacío', () => {
    expect(nombrePropio('')).toBe('');
    expect(nombrePropio(null)).toBe('');
    expect(nombrePropio(undefined)).toBe('');
  });
});
