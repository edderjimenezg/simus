import {
  GRUPOS_DE_GESTION_ADMINISTRATIVA,
  SECCIONES_DE_GESTION_ADMINISTRATIVA,
  gruposAdministrativosVisibles,
  seccionAdministrativaPorRuta,
} from './navegacion-administrativa';

describe('navegación del Espacio de Gestión Administrativa', () => {
  it('mantiene identificadores y rutas únicos', () => {
    const ids: string[] = SECCIONES_DE_GESTION_ADMINISTRATIVA.map(seccion => seccion.id);
    const rutas = SECCIONES_DE_GESTION_ADMINISTRATIVA.map(seccion => seccion.ruta);

    expect(new Set(ids).size).toBe(ids.length);
    expect(new Set(rutas).size).toBe(rutas.length);
  });

  it('solo publica secciones que tienen una capacidad montada', () => {
    const ids: string[] = SECCIONES_DE_GESTION_ADMINISTRATIVA.map(seccion => seccion.id);

    expect(ids).toContain('boletin');
    expect(ids).toContain('auditoria');
    expect(ids).toContain('analisis');
    expect(ids).not.toContain('communications');
    expect(ids).not.toContain('editorial');
  });

  it('reserva usuarios y sistema al webmaster sin ocultar auditoría al gestor interno', () => {
    const grupos = gruposAdministrativosVisibles(['gestor_interno']);
    const ids = grupos.flatMap(grupo => grupo.secciones.map(seccion => seccion.id));

    expect(ids).toContain('auditoria');
    expect(ids).not.toContain('usuarios');
    expect(ids).not.toContain('sistema');
  });

  it('resuelve una ruta enlazable a su sección', () => {
    expect(seccionAdministrativaPorRuta('solicitudes')?.id).toBe('solicitudes');
    expect(GRUPOS_DE_GESTION_ADMINISTRATIVA.map(grupo => grupo.titulo)).toEqual([
      'Bandeja de trabajo',
      'Ecosistema musical',
      'Publicaciones y mediateca',
      'Sitio web y comunicación',
      'Gobierno y control',
    ]);
  });
});
