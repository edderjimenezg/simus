import * as MapDomain from './map-domain';

/**
 * EL DIRECTORIO COMO ATAJO DEL MAPA.
 *
 * Hasta pulsar un registro del directorio abria un modal a
 * pantalla completa —`fixed inset-0` con velo y desenfoque— que tapaba el mapa entero.
 * Medido en el navegador antes del cambio: al pulsar «Centro de Documentacion Sonora
 * PNMC 01» aparecia un dialogo de 1280x639 con el texto «Detalle de Redes de
 * Documentacion» y el mapa dejaba de verse.
 *
 * Ahora el clic LLEVA AL MAPA: encuadra el municipio del registro y abre alli la
 * tarjeta de procesos. Lo que se decide en esta funcion es la parte que no necesita
 * navegador: adonde va la camara y si la tarjeta se abre ya o hay que esperar.
 *
 * POR QUE HAY UN ESTADO «esperar». El punto de cada municipio llega por red
 * (`/api/v1/publico/divipola`) y puede no estar cargado cuando se pulsa. Abrir la
 * tarjeta en ese instante la dejaria sin ancla, es decir flotando en el centro de la
 * pantalla y sin seguir al territorio cuando el mapa se mueve. Se espera a saber donde
 * esta —pero solo mientras la peticion este en curso: si fallo, se abre igual, porque
 * un clic que no hace nada es peor que una tarjeta sin ancla.
 */
describe('MapDomain.resolverDestinoDeNavegacion', () => {
  const PUNTOS = new Map([
    ['05031', { lat: 6.9092, lng: -75.0778, nombre: 'AMALFI' }],
    ['76001', { lat: 3.4372, lng: -76.5225, nombre: 'CALI' }],
  ]);

  it('con el punto cargado, la camara va al municipio y la tarjeta se abre', () => {
    const destino = MapDomain.resolverDestinoDeNavegacion('05031', PUNTOS, 'listo');

    expect(destino.camara).toBe('municipio');
    expect(destino.lat).toBe(6.9092);
    expect(destino.lng).toBe(-75.0778);
    expect(destino.tarjeta).toBe('abrir');
  });

  it('el acercamiento deja ver el rotulo del municipio', () => {
    // Los rotulos municipales aparecen POR ENCIMA de MUNICIPALITY_LABEL_MIN_ZOOM
    // (`labelVisibilityForZoom` compara con `>`). Un destino en el umbral exacto
    // dejaria el municipio sin nombre en pantalla, que es justo lo que se fue a ver.
    const destino = MapDomain.resolverDestinoDeNavegacion('05031', PUNTOS, 'listo');

    expect(destino.zoom).toBeGreaterThan(MapDomain.MUNICIPALITY_LABEL_MIN_ZOOM);
    expect(MapDomain.labelVisibilityForZoom(destino.zoom).municipalities).toBe(true);
  });

  it('acepta el codigo sin el cero de la izquierda', () => {
    // El registro trae «5031» y el catalogo indexa «05031». Sin normalizar, el
    // municipio existe y aun asi el mapa no sabria adonde ir.
    const destino = MapDomain.resolverDestinoDeNavegacion('5031', PUNTOS, 'listo');

    expect(destino.camara).toBe('municipio');
    expect(destino.lat).toBe(6.9092);
  });

  it('mientras la tabla de municipios se descarga, la cámara no se mueve', () => {
    // NO ENCUADRA EL DEPARTAMENTO MIENTRAS ESPERA, y eso se corrigió el 29 de agosto de
    // 2026 con la medición delante. Encuadrarlo dejaba dos movimientos compitiendo: el
    // del departamento se programa para el siguiente fotograma y el del municipio se
    // ejecuta en el acto, así que el primero llegaba después y borraba al segundo. El
    // rótulo del municipio quedaba a 124 px del centro del lienzo en vez de a 9, una de
    // cada dos veces.
    const destino = MapDomain.resolverDestinoDeNavegacion('05031', null, 'cargando');

    expect(destino.camara).toBe('ninguna');
    expect(destino.lat).toBeNull();
    expect(destino.lng).toBeNull();
    expect(destino.tarjeta).toBe('esperar');
  });

  it('esperar es el ÚNICO caso en que la cámara se queda quieta', () => {
    // Si «ninguna» se colara en un caso terminal, el clic no movería el mapa nunca y no
    // habría nada que lo dijera: la tarjeta se abriría igual.
    const terminales = [
      MapDomain.resolverDestinoDeNavegacion('05031', PUNTOS, 'listo'),
      MapDomain.resolverDestinoDeNavegacion('99999', PUNTOS, 'listo'),
      MapDomain.resolverDestinoDeNavegacion('05031', null, 'fallo'),
      MapDomain.resolverDestinoDeNavegacion('', PUNTOS, 'cargando'),
    ];

    expect(terminales.every((d) => d.camara !== 'ninguna')).toBeTrue();
    expect(terminales.every((d) => d.tarjeta === 'abrir')).toBeTrue();
  });

  it('si la descarga fallo, abre la tarjeta igual y no deja el clic muerto', () => {
    const destino = MapDomain.resolverDestinoDeNavegacion('05031', null, 'fallo');

    expect(destino.camara).toBe('departamento');
    expect(destino.tarjeta).toBe('abrir');
  });

  it('si nadie pidio la tabla, no se queda esperando una peticion que no existe', () => {
    const destino = MapDomain.resolverDestinoDeNavegacion('05031', null, 'sin_pedir');

    expect(destino.tarjeta).toBe('abrir');
  });

  it('con la tabla cargada pero sin ese municipio, abre la tarjeta sin ancla', () => {
    // Un municipio nuevo que DIVIPOLA todavia no publica. La lista de procesos existe
    // igual: la construye el directorio, no el catalogo de puntos.
    const destino = MapDomain.resolverDestinoDeNavegacion('99999', PUNTOS, 'listo');

    expect(destino.camara).toBe('departamento');
    expect(destino.lat).toBeNull();
    expect(destino.tarjeta).toBe('abrir');
  });

  it('sin codigo de municipio no espera a nadie, ni siquiera durante la descarga', () => {
    // Es el caso que colgaria el clic para siempre: sin codigo, ninguna descarga va a
    // producir un punto, asi que esperar seria esperar a nada.
    const destino = MapDomain.resolverDestinoDeNavegacion('', PUNTOS, 'cargando');

    expect(destino.camara).toBe('departamento');
    expect(destino.tarjeta).toBe('abrir');
  });

  it('no se inventa una coordenada cuando no la tiene', () => {
    const sinPunto = MapDomain.resolverDestinoDeNavegacion('99999', PUNTOS, 'listo');
    const sinCodigo = MapDomain.resolverDestinoDeNavegacion('', PUNTOS, 'listo');

    expect(sinPunto.lat).toBeNull();
    expect(sinPunto.lng).toBeNull();
    expect(sinCodigo.lat).toBeNull();
    expect(sinCodigo.lng).toBeNull();
  });

  it('devuelve la coordenada del municipio pedido y no la del primero de la tabla', () => {
    // Con un solo municipio en el fixture, devolver siempre `values().next()` pasaria
    // todas las pruebas de arriba.
    const destino = MapDomain.resolverDestinoDeNavegacion('76001', PUNTOS, 'listo');

    expect(destino.lat).toBe(3.4372);
    expect(destino.lng).toBe(-76.5225);
  });
});
