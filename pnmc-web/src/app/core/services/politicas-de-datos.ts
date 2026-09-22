/**
 * Las formas que viajan entre el servidor y las pantallas de autorizaciones.
 *
 * <b>VIVEN APARTE PORQUE LAS USAN CUATRO SITIOS.</b> El alta de organización, las páginas públicas
 * de cada política, la pantalla «Mis autorizaciones» del panel y el bloque de suscripción al
 * boletín responden a la misma pregunta —qué texto hay que aceptar, y qué aceptó esta persona— y
 * si cada una declarara su propia forma acabarían discrepando en cuanto el servidor añadiera un
 * campo.
 *
 * Reflejan `PoliticaPublicaDto`, `PoliticaParaRegistroDto`, `PreparacionDeRegistroDto` y
 * `AutorizacionDeDatosDto` de `pnmc-api/src/PNMC.Contracts/ApiContracts.cs`.
 */

/** Una política vigente, tal como el servidor la sirve. */
export interface PoliticaPublica {
  /** `tratamiento`, `terminos` o `boletin`. */
  readonly clave: string;
  /** Fecha en texto (AAAA-MM-DD) de cuándo empezó a mostrarse esta redacción. */
  readonly version: string;
  readonly titulo: string;
  /**
   * El texto entero, no un enlace a él.
   *
   * Es lo que la pantalla muestra y lo que el servidor copia dentro de la autorización como
   * prueba. La pantalla NUNCA escribe su propia redacción: si lo hiciera, lo que la persona lee y
   * lo que queda guardado serían dos cosas distintas.
   */
  readonly texto: string;
  /** El documento institucional completo del Ministerio, cuando la finalidad tiene uno. */
  readonly urlOficial: string | null;
  /** Su identificador: «PL-GSI-002 v0». */
  readonly referenciaOficial: string | null;
}

/** Una política en el contexto del alta, donde además importa si es exigible. */
export interface PoliticaParaRegistro extends PoliticaPublica {
  /**
   * Si el alta no se puede completar sin ella.
   *
   * Lo decide el servidor y no la pantalla: el boletín está en la lista pero no es obligatorio, y
   * que se pueda completar el registro sin marcarlo es lo que hace que sea una autorización libre
   * y no un peaje (Ley 1581 art. 9).
   */
  readonly obligatoria: boolean;
}

export interface PreparacionDeRegistro {
  readonly registroDisponible: boolean;
  readonly politicas: readonly PoliticaParaRegistro[];
  readonly impedimentos: readonly string[];
}

/** Lo que una cuenta ha autorizado, incluido lo que retiró. */
export interface AutorizacionDeDatos {
  readonly id: number;
  readonly finalidad: string;
  readonly titulo: string;
  readonly version: string;
  /**
   * El texto que la persona tenía delante.
   *
   * Es la prueba que el art. 8 num. 2 de la Ley 1581 le da derecho a pedir, y por eso viaja entero
   * y no resumido: «aceptaste el documento 2» no es una prueba de nada.
   */
  readonly textoAceptado: string;
  /**
   * El texto no se capturó en su momento y se reconstruyó.
   *
   * Solo lo llevan las autorizaciones trasladadas desde la estructura anterior, que guardaba a qué
   * documento apuntaban y no qué decía. La pantalla lo dice: una evidencia reconstruida no vale lo
   * mismo que una capturada y no debe presentarse con la misma cara.
   */
  readonly textoReconstruido: boolean;
  readonly origen: string;
  readonly fechaOtorgada: string;
  readonly fechaRevocacion: string | null;
  readonly vigente: boolean;
  readonly sePuedeRevocar: boolean;
  /** Por qué no se puede retirar por separado, cuando no se puede. */
  readonly motivoSinRevocar: string | null;
}
