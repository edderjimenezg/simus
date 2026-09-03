export interface ElementoNavegacionPortal {
  ruta: string;
  etiqueta: string;
  disponible: boolean;
  motivo?: string;
}

export const NAVEGACION_PORTAL: ElementoNavegacionPortal[] = [
  { ruta: '/', etiqueta: 'Inicio', disponible: true },
  { ruta: '/ecosistema-musical', etiqueta: 'Ecosistema musical', disponible: false, motivo: 'Esta sección mostrará las capas del ecosistema musical cuando el contenido institucional esté publicado.' },
  { ruta: '/mapa-ecosistemico', etiqueta: 'Mapa ecosistémico', disponible: false, motivo: 'El mapa depende de que los Festivales publicados ya existan como fuente de datos real. Se habilita después de esa capa.' },
  { ruta: '/festivales', etiqueta: 'Festivales', disponible: false, motivo: 'Hoy los Festivales solo existen como borrador privado de cada organización. Esta sección se habilita cuando exista un recorrido de publicación y una lectura pública.' },
  { ruta: '/agenda', etiqueta: 'Agenda', disponible: false, motivo: 'La agenda depende del modelo de ediciones anuales de cada Festival, que todavía no se ha construido.' },
  { ruta: '/noticias', etiqueta: 'Noticias', disponible: false, motivo: 'Sección de noticias institucionales, pendiente de conectar con su fuente de contenido.' },
  { ruta: '/editorial', etiqueta: 'Editorial', disponible: false, motivo: 'Sección de contenidos editoriales, pendiente de conectar con su fuente de contenido.' },
  { ruta: '/institucional', etiqueta: 'Institucional', disponible: true },
  { ruta: '/ayuda', etiqueta: 'Ayuda', disponible: true }
];
