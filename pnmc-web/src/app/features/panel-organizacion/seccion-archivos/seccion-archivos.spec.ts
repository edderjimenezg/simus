import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { SeccionArchivosComponent } from './seccion-archivos.component';
import { GestionDelBancoComponent } from '../../../shared/components/banco-de-archivos/gestion-del-banco.component';

/**
 * «Mis archivos», en el espacio de la organización.
 *
 * <b>ES LA MISMA PANTALLA QUE LA DE LA CONSOLA, CON OTRO CANAL.</b> El servidor decide qué devuelve
 * según por dónde se pregunta, y esta sección solo declara por dónde. Parece poco que probar, pero
 * es exactamente lo que rompería en silencio: con el canal equivocado, una organización vería el
 * banco del Programa entero. Dos pantallas gemelas, en cambio, divergirían en el primer arreglo.
 */
describe('«Mis archivos» de una organización', () => {
  let fixture: ComponentFixture<SeccionArchivosComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SeccionArchivosComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    fixture = TestBed.createComponent(SeccionArchivosComponent);
    fixture.detectChanges();
  });

  it('monta el banco compartido y le dice que el canal es el externo', () => {
    const banco = fixture.debugElement.children[0];
    expect(banco.componentInstance instanceof GestionDelBancoComponent).toBeTrue();
    expect((banco.componentInstance as GestionDelBancoComponent).canal).toBe('externo');
  });
});
