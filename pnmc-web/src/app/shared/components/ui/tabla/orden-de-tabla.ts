import { Signal, computed, signal } from '@angular/core';

/** Una columna de una tabla administrativa: su identificador, su rótulo y si ordena. */
export interface ColumnaDeTabla {
  readonly id: string;
  readonly etiqueta: string;
  /** Si la cabecera ofrece ordenar. Por omisión sí; la columna de acciones dice que no. */
  readonly ordenable?: boolean;
  /** Clases sueltas para esa cabecera —`text-right` en la de acciones—. */
  readonly clases?: string;
}

export type DireccionDeOrden = 'asc' | 'desc';

/**
 * El orden de una tabla: qué columna manda y en qué sentido.
 *
 * <b>POR QUE ESTO EXISTE Y NO SE ESCRIBE EN CADA PANEL.</b> El 15 de septiembre de 2026 se midió:
 * de las diez tablas de la consola, <b>solo dos ordenaban</b> —Festivales y Organizaciones—, y cada
 * una tenía su propia copia de `ordenarPor`, `flechaDe` y `ordenAriaDe`. Las otras ocho —Agenda,
 * Noticias, Categorías, Catálogo Editorial, Boletín, Usuarios, Auditoría y las dos de Salud del
 * sistema— pintaban cabeceras muertas. se detectó así: «continúan con las
 * tablas sin la opción de cliquear en la columna y reorganizar».
 *
 * Copiar tres métodos ocho veces es exactamente lo que el encargo de reestructuración prohíbe, y
 * además las copias divergen: la de Organizaciones dibujaba flecha en la columna de acciones, que
 * no ordena por nada.
 *
 * <b>SOLO GUARDA EL ESTADO.</b> Qué hacer con él lo decide cada panel, porque no todas ordenan
 * igual: Festivales y Organizaciones lo piden al servidor —ordenar la página que se tiene a la
 * vista mentiría, porque el resto del resultado no está aquí—, y las que traen todo de una vez
 * ordenan en memoria con `ordenarFilas`.
 *
 * <b>UNA SOLA REGLA PARA LAS DIEZ TABLAS: el primer golpe ordena ascendente y el segundo invierte,
 * sea cual sea la columna.</b> Se consideró que las fechas empezaran al revés —lo más reciente
 * primero— y se descartó: sería la única columna que se comporta distinta, y el panel de
 * Organizaciones ya tenía fijado por prueba que cambiar de columna nunca hereda el sentido de la
 * anterior, «porque nadie pide una columna nueva para verla del revés». Predecir el resultado de un
 * clic vale más que ahorrar el segundo clic en un caso.
 */
export class OrdenDeTabla {
  private readonly _columna = signal<string>('');
  private readonly _direccion = signal<DireccionDeOrden>('asc');

  readonly columna: Signal<string> = this._columna.asReadonly();
  readonly direccion: Signal<DireccionDeOrden> = this._direccion.asReadonly();

  /** Qué columna manda y en qué sentido, en un solo objeto, para pasarlo al servidor. */
  readonly estado = computed(() => ({ columna: this._columna(), direccion: this._direccion() }));

  private readonly columnaInicial: string;
  private readonly direccionInicial: DireccionDeOrden;

  /**
   * El nombre humano del orden propio de la lista, si lo tiene y no es el de ninguna columna.
   *
   * <b>ES LO QUE SUSTITUYE AL BOTON «PENDIENTES PRIMERO».</b> En Festivales, ese orden —lo que
   * espera decisión arriba— vivía en un botón de la barra, al lado de los filtros y con su mismo
   * aspecto, aunque no filtra nada: ordena. La dirección de producto pidió
   * «buscar una alternativa para el de pendientes primero». La alternativa es no sacarlo de la
   * tabla: el orden propio ES el estado de partida de la tabla y se vuelve a él desde la misma
   * cabecera que se usó para salir.
   */
  readonly ordenPropio: string | null;

  /** Si la tabla está en su orden propio, el que ninguna cabecera representa. */
  readonly enSuOrdenPropio = computed(() => this.ordenPropio !== null && this._columna() === this.columnaInicial);

  constructor(columnaInicial = '', direccionInicial: DireccionDeOrden = 'asc', ordenPropio: string | null = null) {
    this.columnaInicial = columnaInicial;
    this.direccionInicial = direccionInicial;
    this.ordenPropio = ordenPropio;
    this._columna.set(columnaInicial);
    this._direccion.set(direccionInicial);
  }

  /**
   * Pulsar una cabecera: si ya mandaba, invierte; si no, pasa a mandar ella.
   *
   * <b>Y UN TERCER GOLPE DEVUELVE LA LISTA A SU ORDEN PROPIO, cuando lo tiene.</b> Sin eso, una
   * tabla cuyo orden de partida no es el de ninguna columna —«sin publicar primero», «pendientes
   * primero»— no tiene forma de volver a él una vez se ordena por una: había que recargar la
   * pantalla, o poner un botón aparte en la barra, que es lo que se ha retirado. Las tablas cuyo
   * orden de partida SI es una columna —la bitácora por fecha, el boletín por alta— siguen con dos
   * estados: en ellas, un tercero sería un golpe que no cambia nada.
   *
   * Devuelve el estado resultante para que un panel que ordena en el servidor pueda pedirlo sin
   * volver a leer las señales.
   */
  alternar(columna: string): { columna: string; direccion: DireccionDeOrden } {
    if (this._columna() === columna) {
      if (this._direccion() === 'asc') {
        this._direccion.set('desc');
      } else if (this.ordenPropio !== null) {
        this._columna.set(this.columnaInicial);
        this._direccion.set(this.direccionInicial);
      } else {
        this._direccion.set('asc');
      }
    } else {
      this._columna.set(columna);
      this._direccion.set('asc');
    }
    return this.estado();
  }

  /** Fija el orden sin alternar: lo usa un panel cuyo servidor decide otro orden del pedido. */
  fijar(columna: string, direccion: DireccionDeOrden): void {
    this._columna.set(columna);
    this._direccion.set(direccion);
  }

  /**
   * La flecha de esa cabecera.
   *
   * LA COLUMNA QUE NO MANDA LLEVA «↕» Y NO NADA: es la pista de que se puede ordenar por ella. El
   * contrato de estilo la deja tenue y la enciende al posarse encima.
   */
  flechaDe(columna: string): string {
    if (this._columna() !== columna) { return '↕'; }
    return this._direccion() === 'desc' ? '↓' : '↑';
  }

  /**
   * El valor de `aria-sort` de esa cabecera.
   *
   * VA APARTE DE LA FLECHA PORQUE LA FLECHA NO SE ANUNCIA. Un lector de pantalla no lee «↑»; sin
   * `aria-sort`, la tabla queda ordenada para quien la ve y sin ordenar para quien la escucha.
   */
  ariaDe(columna: string): 'none' | 'ascending' | 'descending' {
    if (this._columna() !== columna) { return 'none'; }
    return this._direccion() === 'desc' ? 'descending' : 'ascending';
  }

  /**
   * El título del botón, que es lo que dice qué va a pasar al pulsarlo.
   *
   * EL TERCER GOLPE SE ANUNCIA POR SU NOMBRE. «Ordenar por Estado» tres veces seguidas no deja
   * adivinar que la tercera devuelve la lista a «Pendientes primero»; el título lo dice.
   */
  tituloDe(columna: ColumnaDeTabla): string {
    if (this._columna() !== columna.id) { return `Ordenar por ${columna.etiqueta}`; }
    if (this._direccion() === 'asc') { return `Ordenar por ${columna.etiqueta}, al revés`; }
    return this.ordenPropio !== null
      ? `Volver a «${this.ordenPropio}»`
      : `Ordenar por ${columna.etiqueta}`;
  }

  /**
   * Ordena en memoria, para las tablas que traen todo su contenido de una vez.
   *
   * `clave` saca de cada fila el valor por el que se compara. Los textos se comparan con las reglas
   * del español —`localeCompare` con «es», que es lo que pone «ñ» entre «n» y «o» y trata los
   * acentos como la RAE—, y no con `<`, que ordenaría por código de carácter y dejaría «Ñ» detrás
   * de «Z».
   *
   * LO QUE NO TIENE VALOR VA AL FINAL, mande quien mande y en el sentido que sea: una fila sin
   * fecha no es «la más antigua», es una fila a la que le falta el dato, y colarla primera al
   * ordenar por fecha haría pensar que es la más vieja del listado.
   */
  ordenarFilas<T>(filas: readonly T[], clave: (fila: T) => string | number | Date | null | undefined): T[] {
    const columna = this._columna();
    if (!columna) { return [...filas]; }
    const signo = this._direccion() === 'desc' ? -1 : 1;

    return [...filas].sort((a, b) => {
      const va = clave(a);
      const vb = clave(b);
      const aVacio = va === null || va === undefined || va === '';
      const bVacio = vb === null || vb === undefined || vb === '';
      if (aVacio && bVacio) { return 0; }
      if (aVacio) { return 1; }
      if (bVacio) { return -1; }

      if (va instanceof Date || vb instanceof Date) {
        return (Number(new Date(va as Date)) - Number(new Date(vb as Date))) * signo;
      }
      if (typeof va === 'number' && typeof vb === 'number') {
        return (va - vb) * signo;
      }
      return String(va).localeCompare(String(vb), 'es', { numeric: true, sensitivity: 'base' }) * signo;
    });
  }
}
