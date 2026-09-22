export const RUTAS_PUBLICAS = [
  { id: 'home',                 url: '/' },
  { id: 'pnmc',                 url: '/pnmc' },
  { id: 'ejes',                 url: '/ejes' },
  { id: 'noticias',             url: '/noticias' },
  { id: 'agenda',               url: '/agenda' },
  { id: 'editorial',            url: '/editorial' },
  { id: 'galeria',              url: '/galeria' },
  { id: 'mapa',                 url: '/mapa' },
  // Estas tres se llamaban `/simus/*` y se auditaban con ese nombre. La sección
  // es «Ecosistema» y es del portal del PNMC; SIMUS es una plataforma externa
  // del Ministerio a la que solo enlazamos. Las rutas viejas siguen existiendo
  // como redirecciones, pero auditarlas guardaba la URL vieja en el informe y
  // no aseveraba a dónde acababa: se cambia por la canónica.
  { id: 'ecosistema',           url: '/ecosistema' },
  { id: 'ecosistema-escuelas',  url: '/ecosistema/escuelas' },
  { id: 'ecosistema-festivales', url: '/ecosistema/festivales' },
  { id: 'registro',             url: '/registro' },
  { id: 'estrategia-circulacion', url: '/estrategia/circulacion' },
  { id: 'estrategia-investigacion', url: '/estrategia/investigacion' },
  { id: 'no-encontrado',        url: '/esta-ruta-no-existe-auditoria' },
];
