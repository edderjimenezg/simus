import type { EjeGroup } from '../services/ejes-data.config';

/**
 * Superpone los textos del CMS sobre la configuración compilada de los ejes.
 *
 * Los tres ejes viven en `ejes-data.config.ts` con su texto escrito a mano. El
 * panel ofrece editar **32** de esas cadenas —el título del eje, sus dos
 * párrafos, el propósito, y el título y la descripción de cada uno de sus diez
 * componentes— y hasta ahora **no las leía nadie**: la editora escribía,
 * guardaba, publicaba, y la página seguía igual. Verificado publicando un
 * marcador único en cada clave y renderizando el sitio.
 *
 * Aquí decía «23», y la cifra venía de `cms:huerfanas`, que cuenta las que NADIE
 * nombra literalmente en el código. Nueve de las treinta y dos sí aparecen
 * escritas —la portada las cita— así que la herramienta solo denunciaba las
 * otras veintitrés. Contar huérfanas y contar editables no es lo mismo, y el
 * comentario mezcló las dos. Las declaradas son 32 y una prueba lo fija.
 *
 * El patrón no es nuevo aquí: `NavigationService.getResolvedNavigationLinks()`
 * ya hacía esto mismo con el menú. Esta función lo repite para los ejes.
 *
 * Es una función pura y recibe el lector como argumento: así se prueba sin
 * levantar Angular, y el orden de las claves queda a la vista en un solo sitio.
 *
 * La convención de claves es posicional y depende del ORDEN del arreglo:
 *   eje0{N}_title       título del eje N (1-based)
 *   eje0{N}_desc1/2     los dos párrafos de `axisExplain`
 *   eje0{N}_purpose     el propósito
 *   eje0{N}_c{M}_title  nombre del componente M del eje N
 *   eje0{N}_c{M}_desc   su descripción corta
 *
 * Si algún día se reordenan los ejes o sus componentes, las claves cambian de
 * dueño en silencio. Es el precio de no meter la clave dentro de la config; a
 * cambio, el archivo de datos no sabe que existe un CMS.
 */
export function resolveEjes(
  compilados: readonly EjeGroup[],
  leer: (clave: string) => string,
  /**
   * Lector de IMAGENES, opcional. Sin el, la imagen de cada eje sigue siendo la
   * compilada, que es lo que hacia esta funcion antes.
   *
   * ES UN SEGUNDO PARAMETRO Y NO UNA SEGUNDA LLAMADA A `leer` a proposito: las
   * claves de imagen viven en otro catalogo y las sirve otro endpoint. Meterlas
   * en el mismo lector obligaria a que un solo diccionario mezclara textos y
   * URLs, y una clave escrita mal devolveria un texto donde se espera una imagen
   * sin que nada lo dijera.
   */
  leerImagen?: (clave: string) => string,
): EjeGroup[] {
  // El valor del CMS se usa TAL CUAL, cadena vacía incluida.
  //
  // Aquí había un `|| texto compilado` con la idea de que un vacío significaba
  // «no hay nada publicado». Es falso contra este servidor (PNMC-040): el
  // endpoint público filtra `Publicado != null`, así que una clave sin publicar
  // ni siquiera viaja en el diccionario y `leer` devuelve entonces el texto del
  // registro. Una cadena vacía solo puede venir de alguien que publicó el campo
  // en blanco, y sustituirla hacía que borrar no borrara: el panel confirmaba el
  // cambio y la página seguía igual.
  //
  // De la configuración compilada sigue saliendo la ESTRUCTURA —cuántos ejes,
  // cuántos párrafos, cuántos componentes y en qué orden—; el texto de estos
  // campos lo gobierna el panel. Que ninguna clave falte en el registro lo
  // vigila una prueba, porque una clave no registrada sí dejaría el campo vacío.
  return compilados.map((eje, i) => {
    const n = i + 1;
    return {
      ...eje,
      title: leer(`eje0${n}_title`),
      purpose: leer(`eje0${n}_purpose`),
      // La imagen del eje, administrable desde el panel. Cae en la compilada si
      // no se pasa lector —el detalle de componente no lo necesita— o si nadie
      // ha publicado esa ranura.
      videoImg: leerImagen ? leerImagen(`eje_0${n}_media`) : eje.videoImg,
      axisExplain: eje.axisExplain.map((_parrafo, j) => leer(`eje0${n}_desc${j + 1}`)),
      components: eje.components.map((componente, k) => {
        const m = k + 1;
        return {
          ...componente,
          name: leer(`eje0${n}_c${m}_title`),
          // `eje0N_cM_desc` gobierna el PRIMER párrafo de la ficha, que es el
          // que el visitante lee. Los demás siguen viniendo de la configuración:
          // el panel ofrece un campo por componente, no una lista.
          //
          // Estuvo sin conectar un tiempo y por una buena razón: en 7 de los 10
          // componentes el valor sembrado era una redacción MÁS CORTA que el
          // párrafo publicado, así que atarla habría recortado copia del portal
          // sin que nadie lo pidiera. La salida no era elegir entre los dos
          // textos sino quitar la discrepancia: el registro pasó a sembrar el
          // párrafo real —con el tope de caracteres subido donde no cabía— y
          // entonces conectar deja la página exactamente igual. Los resúmenes
          // cortos no se pierden: `details` conserva uno y el historial de Git
          // los otros. Una prueba vigila que el registro y la página no vuelvan
          // a separarse.
          fullText: [leer(`eje0${n}_c${m}_desc`), ...componente.fullText.slice(1)],
        };
      }),
    };
  });
}
