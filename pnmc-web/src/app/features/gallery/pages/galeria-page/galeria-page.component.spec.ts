import { ComponentFixture, TestBed } from '@angular/core/testing';
import { GaleriaPageComponent } from './galeria-page.component';

describe('GaleriaPageComponent', () => {
  let fixture: ComponentFixture<GaleriaPageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [GaleriaPageComponent] }).compileComponents();
    fixture = TestBed.createComponent(GaleriaPageComponent);
    fixture.detectChanges();
  });

  it('declara una estructura pública vacía sin álbumes heredados', () => {
    const contenido = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(contenido).toContain('Galería');
    expect(contenido).toContain('Aún no hay contenido visual publicado');
  });
});
