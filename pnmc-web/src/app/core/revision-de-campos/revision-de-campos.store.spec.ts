import { of, throwError } from 'rxjs';
import { RevisionDeCamposStore } from './revision-de-campos.store';
import { ObservacionDeCampo, RevisionDeCampos } from './revision-de-campos';

/*
  QUÉ SE INSTRUMENTA AQUÍ.

  El almacén es lo que decide, para cada uno de los cuarenta y siete campos de la ficha, si hay una
  nota y cuál. Sus reglas no se ven en pantalla y fallan en silencio:

  1) LA LLAVE LLEVA LA EDICIÓN DENTRO. La nota sobre «Correo de contacto» de la edición de 2024 no
     es la de 2025, y el `campoId` es el mismo en las dos. Sin la edición en la llave, las notas de
     todas las ediciones se pegan a la primera que se abra — y eso no falla: pinta una nota donde
     no toca.

  2) UN TEXTO VACÍO RETIRA LA NOTA. Es como se quita desde la pantalla, y el servidor hace lo
     mismo. Si una de las dos mitades dejara de hacerlo, la nota volvería a aparecer al recargar.

  3) REESCRIBIR VUELVE A «PENDIENTE». Si el funcionario cambia el encargo, lo que la organización
     dio por atendido ya no vale.

  4) `paraGuardar()` MANDA TODAS LAS EDICIONES. El PUT reemplaza la lista entera: mandar solo las de
     la edición abierta borraría las demás sin decir nada.
*/

function nota(cambios: Partial<ObservacionDeCampo> = {}): ObservacionDeCampo {
  return {
    id: 0,
    ambito: 'principal',
    subregistroId: null,
    seccionId: 'generales',
    campoId: 'festival.nombre',
    campoEtiqueta: 'Nombre del festival',
    valorObservado: 'Festival de la Candelaria',
    nota: 'Escribe el nombre completo.',
    estado: 'pendiente',
    fechaAtencion: null,
    ...cambios,
  };
}

function revision(observaciones: ObservacionDeCampo[], estado: RevisionDeCampos['estado'] = 'borrador'): RevisionDeCampos {
  return {
    id: 7,
    moduloId: 'festivales', registroId: '91',
    registroNombre: 'Festival de la Candelaria',
    estado,
    observacionGeneral: null,
    revisorNombre: 'Funcionaria PNMC',
    destinatarioNombre: 'Persona de la organización',
    organizacionNombre: 'Corporación Candelaria',
    fechaActualizacion: null,
    fechaEnvio: estado === 'borrador' ? null : '2026-08-29T10:00:00',
    observaciones,
  };
}

describe('RevisionDeCamposStore · las notas por campo', () => {
  let almacen: RevisionDeCamposStore;

  beforeEach(() => {
    almacen = new RevisionDeCamposStore();
    almacen.modo.set('revision');
  });

  it('la nota de una edición no aparece en otra', () => {
    almacen.sembrar(revision([
      nota({ id: 1, ambito: 'subregistro', subregistroId: 501, campoId: 'perfilVersionado.correoContacto', nota: 'De 2025.' }),
      nota({ id: 2, ambito: 'subregistro', subregistroId: 502, campoId: 'perfilVersionado.correoContacto', nota: 'De 2026.' }),
    ]), '91');

    almacen.versionAbierta.set(501);
    expect(almacen.nota('perfilVersionado.correoContacto')?.nota).toBe('De 2025.');

    almacen.versionAbierta.set(502);
    expect(almacen.nota('perfilVersionado.correoContacto')?.nota).toBe('De 2026.');

    // Y UNA EDICIÓN SIN NOTA NO HEREDA LA DE LA ANTERIOR, que es como se ve el defecto si la llave
    // pierde el identificador: la tercera edición abriría con la nota de la primera.
    almacen.versionAbierta.set(503);
    expect(almacen.nota('perfilVersionado.correoContacto')).toBeUndefined();
  });

  it('la nota del Festival se ve con cualquier edición abierta', () => {
    // ES LA OTRA MITAD DE LA REGLA. La cabecera es una sola para todas las ediciones: su nota no
    // puede depender de cuál se esté mirando.
    almacen.sembrar(revision([nota({ id: 1 })]), '91');

    almacen.versionAbierta.set(501);
    expect(almacen.nota('festival.nombre')?.nota).toBe('Escribe el nombre completo.');
    almacen.versionAbierta.set(502);
    expect(almacen.nota('festival.nombre')?.nota).toBe('Escribe el nombre completo.');
  });

  it('escribir con el texto vacío retira la nota', () => {
    almacen.sembrar(revision([nota({ id: 1 })]), '91');

    almacen.escribir({
      campoId: 'festival.nombre', seccionId: 'generales',
      campoEtiqueta: 'Nombre del festival', valorObservado: 'Festival de la Candelaria',
    }, '   ');

    expect(almacen.nota('festival.nombre')).toBeUndefined();
    expect(almacen.cuantasNotas()).toBe(0);
    expect(almacen.sinGuardar()).withContext('retirar una nota es un cambio sin guardar').toBeTrue();
  });

  it('reescribir una nota ya atendida la devuelve a pendiente', () => {
    almacen.sembrar(revision([
      nota({ id: 1, estado: 'atendida', fechaAtencion: '2026-08-29T12:00:00' }),
    ]), '91');

    almacen.escribir({
      campoId: 'festival.nombre', seccionId: 'generales',
      campoEtiqueta: 'Nombre del festival', valorObservado: 'Festival de la Candelaria',
    }, 'Ahora pido otra cosa.');

    const despues = almacen.nota('festival.nombre')!;
    expect(despues.estado).toBe('pendiente');
    expect(despues.fechaAtencion).toBeNull();
    // EL IDENTIFICADOR NO CAMBIA: es la misma fila, y es lo que sostiene el enlace con el servidor.
    expect(despues.id).toBe(1);
  });

  it('reescribir la MISMA nota no la devuelve a pendiente', () => {
    // SIN ESTA RAMA, abrir la caja y pulsar «Anotar» sin tocar nada desharía el trabajo de la
    // organización. La comparación es por texto y no por si se pulsó el botón.
    almacen.sembrar(revision([
      nota({ id: 1, estado: 'atendida', fechaAtencion: '2026-08-29T12:00:00' }),
    ]), '91');

    almacen.escribir({
      campoId: 'festival.nombre', seccionId: 'generales',
      campoEtiqueta: 'Nombre del festival', valorObservado: 'Festival de la Candelaria',
    }, 'Escribe el nombre completo.');

    expect(almacen.nota('festival.nombre')!.estado).toBe('atendida');
  });

  it('lo que se manda al servidor lleva las notas de TODAS las ediciones', () => {
    // POR QUE EXISTE: el PUT reemplaza la lista entera. Mandar solo las de la edición
    // abierta borraría las de las demás en silencio, y quien anotó la de 2024 y pasó a la de 2025
    // perdería la primera al guardar.
    almacen.sembrar(revision([
      nota({ id: 1, ambito: 'subregistro', subregistroId: 501, campoId: 'perfilVersionado.nombre', nota: 'De 2025.' }),
      nota({ id: 2, ambito: 'subregistro', subregistroId: 502, campoId: 'perfilVersionado.nombre', nota: 'De 2026.' }),
      nota({ id: 3 }),
    ]), '91');
    almacen.versionAbierta.set(502);

    const cuerpo = almacen.paraGuardar('  Una observación general.  ');

    expect(cuerpo.observaciones.length).toBe(3);
    expect(cuerpo.observaciones.map(item => item.campoId + '|' + item.subregistroId).sort())
      .toEqual(['perfilVersionado.nombre|501', 'perfilVersionado.nombre|502', 'festival.nombre|null'].sort());
    expect(cuerpo.observacionGeneral).toBe('Una observación general.');
  });

  it('una observación general en blanco viaja como nulo y no como cadena vacía', () => {
    // `ObservacionGeneral` es nvarchar(2400) NULL. Una cadena vacía guardada es un dato que dice
    // «hay observación» y está en blanco: el servidor la limpia, y aquí se limpia también para que
    // la pantalla no dependa de que el servidor lo haga.
    almacen.sembrar(revision([]), '91');
    expect(almacen.paraGuardar('   ').observacionGeneral).toBeNull();
  });

  it('las notas se agrupan por sección, para el resumen de cada paso', () => {
    almacen.sembrar(revision([
      nota({ id: 1, seccionId: 'organizador', campoId: 'perfilVersionado.director', ambito: 'subregistro', subregistroId: 501 }),
      nota({ id: 2, seccionId: 'financiacion', campoId: 'perfilVersionado.usaEstampillaProcultura', ambito: 'subregistro', subregistroId: 501 }),
      nota({ id: 3, seccionId: 'generales' }),
    ]), '91');
    almacen.versionAbierta.set(501);

    // UN PASO AGRUPA VARIAS SECCIONES: «Organización» son «Organizador» y «Fuente de financiación».
    expect(almacen.cuantasEn(['organizador', 'financiacion'])).toBe(2);
    expect(almacen.cuantasEn(['generales'])).toBe(1);
    expect(almacen.cuantasEn(['practicas'])).toBe(0);
  });

  it('el distintivo cuenta lo pendiente y no lo ya atendido', () => {
    almacen.sembrar(revision([
      nota({ id: 1, seccionId: 'generales', campoId: 'festival.nombre', estado: 'atendida' }),
      nota({ id: 2, seccionId: 'generales', campoId: 'festival.descripcion' }),
    ]), '91');

    expect(almacen.cuantasEn(['generales'])).withContext('el total no cambia').toBe(2);
    expect(almacen.cuantasPendientesEn(['generales'])).withContext('lo atendido deja de reclamar').toBe(1);
  });

  it('marcar atendida cambia la nota y no toca las demás', () => {
    almacen.sembrar(revision([nota({ id: 1 }), nota({ id: 2, campoId: 'festival.descripcion' })], 'enviada'), '91');

    almacen.marcarAtendida(1, true, '2026-08-29T15:00:00');

    expect(almacen.notas().find(item => item.id === 1)!.estado).toBe('atendida');
    expect(almacen.notas().find(item => item.id === 2)!.estado).toBe('pendiente');
  });

  it('marcar atendida escribe lo que dijo el SERVIDOR, no lo que dijo la pantalla', () => {
    // EL SERVIDOR MANDA. Pintar lo que se pulsó y confiar en que la petición salga bien deja a la
    // organización creyendo que dejó constancia de algo que no quedó escrito.
    almacen.sembrar(revision([nota({ id: 44 })], 'enviada'), '91');
    const pedidos: { id: number; atendida: boolean }[] = [];
    almacen.alAtender = (id, atendida) => {
      pedidos.push({ id, atendida });
      return of({ ...nota({ id, estado: 'atendida', fechaAtencion: '2026-08-29T15:00:00' }) });
    };

    const casilla = { checked: true };
    almacen.atender(44, casilla);

    expect(pedidos).toEqual([{ id: 44, atendida: true }]);
    expect(almacen.notas()[0].estado).toBe('atendida');
    expect(almacen.notas()[0].fechaAtencion).toBe('2026-08-29T15:00:00');
    expect(casilla.checked).toBeTrue();
  });

  it('si el servidor no acepta la marca, la casilla vuelve a su sitio', () => {
    // LO DESTAPÓ EL RECORRIDO DE PUNTA A PUNTA. El `<input>` no está atado
    // a ninguna señal: `[checked]` solo se reescribe cuando el valor ATADO cambia, y al fallar sigue
    // siendo `pendiente`, así que Angular no vuelve a escribirlo y la casilla se queda marcada.
    almacen.sembrar(revision([nota({ id: 44 })], 'enviada'), '91');
    almacen.alAtender = () => throwError(() => ({ message: 'la sesión caducó' }));

    const casilla = { checked: true };
    almacen.atender(44, casilla);

    expect(casilla.checked).withContext('lo que el servidor no aceptó no se pinta como aceptado').toBeFalse();
    expect(almacen.notas()[0].estado).toBe('pendiente');
    expect(almacen.error()).toBe('la sesión caducó');
  });

  it('sin nadie que escriba en el servidor, la casilla ni siquiera se marca', () => {
    // ES EL CASO DEL PANEL QUE OLVIDA CONECTAR `alAtender`: sin esta rama, la casilla quedaría
    // marcada para siempre sobre una nota que nadie guardó nunca.
    almacen.sembrar(revision([nota({ id: 44 })], 'enviada'), '91');
    almacen.alAtender = null;

    const casilla = { checked: true };
    almacen.atender(44, casilla);

    expect(casilla.checked).toBeFalse();
    expect(almacen.notas()[0].estado).toBe('pendiente');
  });

  it('con la solicitud ya enviada el funcionario no puede seguir escribiendo', () => {
    almacen.sembrar(revision([nota({ id: 1 })], 'enviada'), '91');
    expect(almacen.yaEnviada()).toBeTrue();

    almacen.sembrar(revision([nota({ id: 1 })], 'borrador'), '91');
    expect(almacen.yaEnviada()).toBeFalse();
  });

  it('sembrar pisa el borrador vivo', () => {
    // NOTAS FANTASMA: escritas, no guardadas y con aspecto de guardadas. Conservar lo de pantalla
    // al recibir la respuesta del servidor es exactamente como se producen.
    almacen.sembrar(revision([]), '91');
    almacen.escribir({
      campoId: 'festival.nombre', seccionId: 'generales',
      campoEtiqueta: 'Nombre del festival', valorObservado: null,
    }, 'Sin guardar todavía.');
    expect(almacen.cuantasNotas()).toBe(1);
    expect(almacen.sinGuardar()).toBeTrue();

    almacen.sembrar(revision([]), '91');

    expect(almacen.cuantasNotas()).toBe(0);
    expect(almacen.sinGuardar()).toBeFalse();
  });
});
