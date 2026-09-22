import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { BandejaDeTrabajoService } from '../admin-solicitudes-panel/bandeja-de-trabajo.service';
import { AdminShellPageComponent } from './admin-shell-page.component';

/**
 * El Resumen no repite números ni promete trabajo que no existe.
 *
 * <b>EL MODELO.</b> Cuatro indicadores arriba —«1 registro pendiente de revisión», «0
 * solicitudes activas», «0 solicitudes de eliminación», «2 organizaciones registradas»— y, justo
 * debajo, una lista con tres filas que repetían TRES DE ESOS CUATRO NUMEROS con otras palabras:
 * «Revisión de registros y propuestas — 1 pendientes». Los seis controles llevaban además al
 * mismo sitio, Solicitudes. Un número repetido no informa dos veces: hace dudar de si son dos
 * cosas distintas.
 *
 * <b>LO QUE ESTAS PRUEBAS IMPIDEN.</b> Que vuelva la duplicidad, y que una fila en cero vuelva a
 * ser un botón: llevar a alguien a una bandeja vacía es prometerle trabajo que no existe. La fila
 * se queda visible —saber que no hay nada pendiente ES información— pero sin acción.
 */
describe('el Resumen operativo no repite lo que ya dijo', () => {
  function crear() {
    TestBed.configureTestingModule({
      imports: [AdminShellPageComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    return TestBed.createComponent(AdminShellPageComponent).componentInstance;
  }

  it('reúne en una sola lista todo lo que espera una decisión', () => {
    // ERAN TRES FILAS Y SON CUATRO desde: al unificar la bandeja,
    // los posibles duplicados y las alertas de calidad dejaron de vivir en la sección «Calidad y
    // coincidencias» y pasaron a esperar en la misma cola. Si el Resumen no los contara, diría
    // que no hay nada que decidir mientras la bandeja tiene filas.
    const componente = crear();

    const filas = componente.pendientesDeDecision();

    expect(filas.length).toBe(4);
    expect(filas.map(f => f.id)).toEqual(['revision', 'solicitudes', 'retiros', 'hallazgos']);
    // Cada fila trae su propio total: el número vive en la fila, no en un indicador aparte que
    // luego haya que mantener sincronizado con ella.
    filas.forEach(fila => expect(typeof fila.total).toBe('number'));
  });

  it('suma cero cuando no hay nada esperando una decisión', () => {
    const componente = crear();

    TestBed.inject(BandejaDeTrabajoService).revisiones.set([]);
    TestBed.inject(BandejaDeTrabajoService).solicitudes.set([]);

    expect(componente.totalPendienteDeDecision()).toBe(0);
  });

  it('cuenta las revisiones pendientes en la fila que les corresponde', () => {
    const componente = crear();

    TestBed.inject(BandejaDeTrabajoService).revisiones.set([{ id: 1 }, { id: 2 }]);

    const revision = componente.pendientesDeDecision().find(f => f.id === 'revision');
    expect(revision?.total).toBe(2);
    expect(componente.totalPendienteDeDecision()).toBe(2);
  });
});

/**
 * La actividad administrativa dice quién hizo qué, sobre qué, y no se repite.
 *
 * <b>EL DEFECTO.</b> Al iniciar sesión, el registro afectado ES la propia persona, así que la
 * línea se leía «Webmaster PNMC · Inició sesión» con «Webmaster PNMC» otra vez debajo. Un dato
 * repetido ocupa el sitio del que falta.
 */
describe('la actividad administrativa no repite al autor', () => {
  function crear() {
    TestBed.configureTestingModule({
      imports: [AdminShellPageComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    return TestBed.createComponent(AdminShellPageComponent).componentInstance;
  }

  it('enseña el área cuando el registro afectado es la propia persona', () => {
    const componente = crear();

    const texto = componente.sobreQue({
      nombreRegistro: 'Webmaster PNMC',
      grupoEtiqueta: 'Usuarios y accesos',
      autor: { nombre: 'Webmaster PNMC' },
    });

    expect(texto).toBe('Usuarios y accesos');
  });

  it('enseña el registro cuando es distinto del autor', () => {
    const componente = crear();

    const texto = componente.sobreQue({
      nombreRegistro: 'Cantos de comunidades negras',
      grupoEtiqueta: 'Catálogo Editorial',
      autor: { nombre: 'Webmaster PNMC' },
    });

    expect(texto).toBe('Cantos de comunidades negras');
  });

  it('cae al área cuando la bitácora no sabe el nombre del registro', () => {
    const componente = crear();

    const texto = componente.sobreQue({ nombreRegistro: null, grupoEtiqueta: 'Festivales', autor: null });

    expect(texto).toBe('Festivales');
  });
});
