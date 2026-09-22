export interface CategoriaEcosistema {
  title: string;
  /**
   * A dónde lleva la tarjeta. Solo la declara el proceso que TIENE pantalla.
   *
   * ERA OBLIGATORIA Y LAS SEIS LA TENIAN, pero cinco apuntaban a rutas que no existen en el
   * enrutador: `ecosistema/escuelas`, `escenarios`, `mercados-musicales`, `redes-documentacion`
   * y `luteria`. No llegaban a romperse porque los dos consumidores comprueban antes
   * `status !== 'Disponible'`, así que era una trampa dormida: el día que alguien marcara uno
   * como disponible —el gesto natural al terminarlo— la tarjeta llevaría al comodín de
   * «página no encontrada», y el fallo aparecería lejos de ese cambio de una palabra.
   *
   * Ahora el tipo lo impide: sin ruta no hay a dónde ir, y `abrirCategoria` no puede navegar.
   */
  route?: string;
  layer: string;
  description: string;
  status: 'Disponible' | 'Próximamente';
  icon: 'school' | 'groups' | 'agents' | 'spaces' | 'festivals' | 'markets' | 'networks' | 'luthier';
  /**
   * De donde sale el recuento de la tarjeta.
   *
   * Solo Festivales tiene clave en esta etapa. Los demás procesos no tienen registros ni API
   * activos; una tarjeta sin cifra expresa esa condición mejor que un cero sin contexto.
   */
  countKey?: 'schools' | 'festivals' | 'markets' | 'networks' | 'lutiers';

  /**
   * Marca los seis procesos proyectados del Ecosistema. Solo Festivales está implementado en la
   * base y en las rutas actuales; los demás se presentan como estructura futura, sin tablas ni
   * contratos activos.
   *
   * DESDE EL 30 DE AGOSTO DE 2026 LOS SEIS SON TODA LA LISTA. Hasta ese dia habia ocho: sobraban
   * «Agrupaciones» y «Agentes», que no tienen ni tabla ni endpoint y cuyo boton caia en una
   * pagina en construccion. Las retiro la direccion de producto al pedir un solo inventario: «que
   * tengamos el mismo lugar de llegada, ahora son diferentes». El menu superior y la vista previa
   * del Inicio ya ofrecian solo estos seis —la vista previa filtra justamente por `proceso`—, asi
   * que la portada era la unica de las tres superficies que ofrecia otra cosa.
   */
  proceso?: boolean;

  /**
   * Si HOY existe un endpoint externo para que una organizacion lo cree.
   *
   * Solo Festivales. Los otros cinco muestran el boton desactivado en vez de esconderlo, para que
   * se vea que el sitio esta pensado para los seis y que faltan cinco formularios, no para que
   * parezca que solo existe uno.
   */
  altaExterna?: boolean;
}

/**
 * Los SEIS procesos del ecosistema musical, en el orden en que se leen.
 *
 * UNA SOLA LISTA PARA LAS TRES SUPERFICIES que ofrecen el ecosistema: la portada de
 * `/ecosistema`, el desplegable del menu superior y la vista previa del Inicio. Antes eran tres
 * inventarios distintos —ocho, seis y seis— y el visitante llegaba a un sitio u otro segun por
 * donde entrara.
 */
/*
  SE SIGUEN POR EL TITULO Y NO POR LA RUTA en las tres superficies que las pintan. Un proceso que
  todavía no tiene directorio público no tiene `route`, así que `track category.route` daba
  `undefined` para varios a la vez y Angular lo avisaba con NG0955 en cada pintado: con claves
  repetidas deja de poder reutilizar los nodos y vuelve a crearlos enteros. El título es único y
  existe siempre.
*/
export const CATEGORIAS_ECOSISTEMA: CategoriaEcosistema[] = [
  { title: 'Escuelas de música', layer: 'Escuelas de Música', description: 'Proceso en definición; todavía no tiene registros ni directorio público.', status: 'Próximamente', icon: 'school', proceso: true },
  { title: 'Escenarios', layer: 'General', description: 'Infraestructura y lugares para creación y circulación.', status: 'Próximamente', icon: 'spaces', proceso: true },
  { title: 'Festivales', route: 'ecosistema/festivales', layer: 'Festivales', description: 'Celebraciones, encuentros y circuitos de circulación.', status: 'Disponible', icon: 'festivals', countKey: 'festivals', proceso: true, altaExterna: true },
  // MERCADOS MUSICALES DEJA DE ESTAR «PROXIMAMENTE»: tiene modelo,
  // circuito de revisión, alta desde la organización, directorio público y capa en el mapa. Es el
  // segundo proceso disponible, y el primero de los cinco que estaban en definición que se
  // completa. Con `altaExterna` porque una organización puede registrarlo desde su panel.
  { title: 'Mercados musicales', route: 'ecosistema/mercados-musicales', layer: 'Mercados Musicales', description: 'Nodos de intercambio, visibilización y profesionalización.', status: 'Disponible', icon: 'markets', countKey: 'markets', proceso: true, altaExterna: true },
  { title: 'Redes y documentación', layer: 'Redes de Documentación', description: 'Proceso en definición; todavía no tiene registros ni directorio público.', status: 'Próximamente', icon: 'networks', proceso: true },
  { title: 'Lutería', layer: 'Lutieres', description: 'Proceso en definición; todavía no tiene registros ni directorio público.', status: 'Próximamente', icon: 'luthier', proceso: true },
];
