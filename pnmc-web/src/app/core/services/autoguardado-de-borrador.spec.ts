import { AutoguardadoDeBorrador, TransporteDeBorrador } from './autoguardado-de-borrador';

/**
 * El bucle de autoguardado.
 *
 * LO QUE ESTAS PRUEBAS IMPIDEN. Que guardar una letra mande una petición por letra; que dos
 * guardados se solapen y se rechacen entre sí por citar la misma versión; que cerrar el formulario
 * sin querer siga mandando peticiones de un diálogo que ya no existe; y que un fallo del servidor
 * pase desapercibido justo cuando conviene copiar el texto a otro sitio.
 */

interface DatosDePrueba { titulo: string; }

/**
 * UN TRANSPORTE FALSO Y NO UN SERVICIO FALSO. El bucle dejó de saber por dónde viaja lo que guarda
 * —la consola institucional y el asistente de Festival usan rutas distintas— y la prueba refleja
 * ese contrato: tres operaciones y ninguna más.
 */
class TransporteFalso implements TransporteDeBorrador<DatosDePrueba> {
  guardados: unknown[] = [];
  versionesCitadas: (number | null)[] = [];
  descartes = 0;
  proximoFallo = false;
  private version = 0;
  guardado: { datos: DatosDePrueba; version: number; fechaActualizacion: string } | null = null;

  async leer() { return this.guardado; }

  async guardar(datos: DatosDePrueba, version: number | null) {
    this.versionesCitadas.push(version);
    if (this.proximoFallo) { this.proximoFallo = false; return null; }
    this.guardados.push(datos);
    this.version += 1;
    return { version: this.version, fechaActualizacion: '2026-09-11T10:00:00Z' };
  }

  async descartar(): Promise<void> { this.descartes += 1; }
}

function crear(transporte: TransporteFalso) {
  return new AutoguardadoDeBorrador<DatosDePrueba>(transporte);
}

/** Deja correr los temporizadores falsos y las promesas que disparan. */
async function pasarElTiempo(ms: number): Promise<void> {
  jasmine.clock().tick(ms);
  // DOS VUELTAS DE MICROTAREAS: el guardado encadena dos `await` antes de asentar su estado.
  await Promise.resolve();
  await Promise.resolve();
  await Promise.resolve();
}

describe('el autoguardado de borrador', () => {
  beforeEach(() => jasmine.clock().install());
  afterEach(() => jasmine.clock().uninstall());

  it('no manda nada hasta que se enciende', async () => {
    const servicio = new TransporteFalso();
    const auto = crear(servicio);

    auto.anotar({ titulo: 'A' });
    await pasarElTiempo(5000);

    // ESCRIBIR ANTES DE ENCENDER PISARIA EL BORRADOR DE LA VEZ ANTERIOR con el formulario vacío
    // que acaba de abrirse, que es justo lo que este mecanismo existe para evitar.
    expect(servicio.guardados.length).toBe(0);
  });

  it('espera a que se deje de escribir en vez de mandar una petición por letra', async () => {
    const servicio = new TransporteFalso();
    const auto = crear(servicio);
    auto.encender();

    auto.anotar({ titulo: 'E' });
    auto.anotar({ titulo: 'En' });
    auto.anotar({ titulo: 'Enc' });
    await pasarElTiempo(5000);

    expect(servicio.guardados.length).toBe(1);
    expect(servicio.guardados[0]).toEqual({ titulo: 'Enc' });
  });

  it('cita la versión que devolvió el guardado anterior', async () => {
    const servicio = new TransporteFalso();
    const auto = crear(servicio);
    auto.encender();

    auto.anotar({ titulo: 'A' });
    await pasarElTiempo(5000);
    auto.anotar({ titulo: 'AB' });
    await pasarElTiempo(5000);

    // SIN CITAR LA VERSION, dos pestañas de la misma persona se pisarían en silencio.
    expect(servicio.versionesCitadas).toEqual([null, 1]);
  });

  it('dice que falló, en vez de callarlo', async () => {
    const servicio = new TransporteFalso();
    servicio.proximoFallo = true;
    const auto = crear(servicio);
    auto.encender();

    auto.anotar({ titulo: 'A' });
    await pasarElTiempo(5000);

    // QUIEN ESTA ESCRIBIENDO NECESITA SABER que lo que ve ya no está respaldado.
    expect(auto.estado()).toBe('fallido');
  });

  it('apagarlo deja de mandar, pero no borra lo guardado', async () => {
    const servicio = new TransporteFalso();
    const auto = crear(servicio);
    auto.encender();

    auto.anotar({ titulo: 'A' });
    auto.apagar();
    await pasarElTiempo(5000);

    // CERRAR SIN QUERER es justo el accidente del que protege: borrar aquí lo dejaría inútil.
    expect(servicio.guardados.length).toBe(0);
    expect(servicio.descartes).toBe(0);
  });

  it('cerrarlo sí retira el borrador', async () => {
    const servicio = new TransporteFalso();
    const auto = crear(servicio);
    auto.encender();

    await auto.cerrar();

    // SE LLAMA AL GUARDAR DE VERDAD: conservarlo ofrecería recuperar algo que ya existe.
    expect(servicio.descartes).toBe(1);
  });

  it('no ofrece nada cuando el transporte no encuentra borrador', async () => {
    // DESCIFRAR EL JSON ES DEL TRANSPORTE, NO DEL BUCLE: cada uno guarda por su ruta y con su
    // forma, y quien sabe si lo guardado es legible es quien sabe hablar con ese servidor. El
    // transporte devuelve `null` y el bucle no ofrece recuperar nada.
    const auto = crear(new TransporteFalso());

    expect(await auto.recuperar()).toBeNull();
  });

  it('recupera lo guardado con su fecha y su versión', async () => {
    const transporte = new TransporteFalso();
    transporte.guardado = {
      datos: { titulo: 'A medias' }, version: 3, fechaActualizacion: '2026-09-11T10:00:00Z',
    };
    const auto = crear(transporte);

    const encontrado = await auto.recuperar();

    expect(encontrado?.datos).toEqual({ titulo: 'A medias' });
    expect(encontrado?.fecha).toBe('2026-09-11T10:00:00Z');
    // LA VERSION VIAJA con lo recuperado: es la que hay que citar en el primer guardado, y sin
    // ella el bucle mandaría `null` y el servidor aceptaría pisando lo que hubiera.
    expect(encontrado?.version).toBe(3);
  });
});
