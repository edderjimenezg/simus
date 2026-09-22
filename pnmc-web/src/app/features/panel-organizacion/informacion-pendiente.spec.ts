import { PerfilOrganizacion } from './panel-organizacion.api';
import { datosPendientes, frasePendiente } from './informacion-pendiente';

/**
 * Qué le falta a una organización, y cómo se dice.
 *
 * <b>LO QUE ESTAS PRUEBAS IMPIDEN.</b> Que el §15.3 se resuelva con lo que él mismo prohíbe —«no
 * introducir porcentajes o gamificación salvo que aporten valor real»— y que la frase crezca hasta
 * ser una lista que nadie lee. «Perfil al 60 %» no le dice a nadie qué escribir; «te falta el
 * teléfono» sí.
 */
describe('la información pendiente de una organización', () => {
  const perfil = (parcial: Partial<PerfilOrganizacion> = {}): PerfilOrganizacion => ({
    id: '1', nombre: 'Fundación X', nombreLegal: null, numeroIdentificacion: null,
    tipoIdentificacion: null, descripcion: null, correoContacto: 'x@y.co',
    telefonoContacto: null, sitioWeb: null, facebook: null, instagram: null,
    otroEnlace: null, direccion: null, estadoRegistro: 'activa',
    estadoRegistroEtiqueta: 'Activa',
    fechaActualizacion: null, ...parcial,
  });

  it('sin perfil leído no afirma que falte nada', () => {
    // MUTANTE QUE MATA: tratar `null` como «un perfil vacío». Diría que falta todo justo antes de
    // descubrir que no falta nada, y mandaría a rellenar campos llenos.
    expect(datosPendientes(null)).toEqual([]);
    expect(frasePendiente(null)).toBe('');
  });

  it('un campo con solo espacios cuenta como vacío', () => {
    // Guardar un espacio no es aportar un teléfono.
    const pendientes = datosPendientes(perfil({ telefonoContacto: '   ' }));
    expect(pendientes.some(dato => dato.campo === 'telefonoContacto')).toBeTrue();
  });

  it('los nombra en orden de utilidad, no alfabético', () => {
    // Primero lo que permite ponerse en contacto; quien solo rellene dos campos debería rellenar
    // esos dos.
    const pendientes = datosPendientes(perfil());
    expect(pendientes.map(dato => dato.campo)).toEqual([
      'telefonoContacto', 'direccion', 'descripcion', 'sitioWeb', 'instagram', 'facebook',
    ]);
  });

  it('nombra hasta dos y resume el resto, sin porcentajes', () => {
    const frase = frasePendiente(perfil());

    expect(frase).toBe('Completa el teléfono y la dirección y 4 datos más para mejorar tu información.');
    // MUTANTE QUE MATA: enumerarlos todos. «Completa el teléfono, la dirección, la descripción, el
    // sitio web, Instagram y Facebook» es una lista que nadie lee y que además parece que falta todo.
    expect(frase).not.toContain('Facebook');
    expect(frase).not.toMatch(/\d+\s*%/);
  });

  it('con dos pendientes los nombra los dos y no cuenta nada más', () => {
    const frase = frasePendiente(perfil({
      descripcion: 'Somos una fundación.', sitioWeb: 'https://x.co',
      instagram: '@x', facebook: 'fb/x',
    }));

    expect(frase).toBe('Completa el teléfono y la dirección para mejorar tu información.');
    expect(frase).not.toContain('datos más');
  });

  it('con uno solo, la frase no dice «y»', () => {
    const frase = frasePendiente(perfil({
      direccion: 'Calle 1', descripcion: 'Somos una fundación.',
      sitioWeb: 'https://x.co', instagram: '@x', facebook: 'fb/x',
    }));

    expect(frase).toBe('Completa el teléfono para mejorar tu información.');
  });

  it('con «1 dato más» lo dice en singular', () => {
    const frase = frasePendiente(perfil({
      sitioWeb: 'https://x.co', instagram: '@x', facebook: 'fb/x',
    }));

    expect(frase).toBe('Completa el teléfono y la dirección y 1 dato más para mejorar tu información.');
  });

  it('completo del todo, no hay nada que decir', () => {
    const frase = frasePendiente(perfil({
      telefonoContacto: '3000000000', direccion: 'Calle 1', descripcion: 'Somos una fundación.',
      sitioWeb: 'https://x.co', instagram: '@x', facebook: 'fb/x',
    }));

    // Devuelve cadena vacía para que la pantalla no tenga que volver a decidirlo.
    expect(frase).toBe('');
    expect(datosPendientes(perfil({
      telefonoContacto: '3000000000', direccion: 'Calle 1', descripcion: 'Somos una fundación.',
      sitioWeb: 'https://x.co', instagram: '@x', facebook: 'fb/x',
    }))).toEqual([]);
  });
});
