import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SobreElPnmcPageComponent } from './sobre-el-pnmc-page.component';

describe('SobreElPnmcPageComponent visual hierarchy', () => {
  let fixture: ComponentFixture<SobreElPnmcPageComponent>;

  beforeEach(async () => {
    localStorage.removeItem('pnmc_web_texts');
    localStorage.removeItem('pnmc_web_team_members');

    await TestBed.configureTestingModule({
      imports: [SobreElPnmcPageComponent],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(SobreElPnmcPageComponent);
    fixture.detectChanges();
  });

  afterEach(() => {
    localStorage.removeItem('pnmc_web_texts');
    localStorage.removeItem('pnmc_web_team_members');
  });

  // ESTA PRUEBA SOSTENIA LO CONTRARIO. Fijaba la cifra traslúcida de 7 rem detrás del texto de cada
  // objetivo; el criterio pide volver al diseño aprobado,
  // donde el objetivo abre con una chapa que lleva su icono. Se reescribe para vigilar eso.
  it('cada objetivo abre con la chapa de su icono, sin cifra de fondo', () => {
    const raiz = fixture.nativeElement as HTMLElement;
    const chapas = raiz.querySelectorAll<HTMLElement>('[data-testid="objective-icon"]');

    expect(chapas.length).toBe(3);
    chapas.forEach(chapa => {
      expect(chapa.className).toContain('h-12');
      expect(chapa.className).toContain('rounded-2xl');
      expect(chapa.querySelector('svg')).not.toBeNull();
    });
    expect(raiz.querySelectorAll('[data-testid="objective-number"]').length).toBe(0);
  });

  // Y ESTA FIJABA LA MARCA DE AGUA: un icono de 168 px al 80 % asomando por la esquina de cada
  // tarjeta de enfoque, y el fondo de la banda con sus cuatro capas. Las dos cosas se retiraron por
  // el mismo motivo y en el mismo corte, así que la prueba vigila ahora lo que sí debe haber.
  it('cada enfoque abre con su icono sobre el título, y la banda va sobre fondo plano', () => {
    const raiz = fixture.nativeElement as HTMLElement;
    const iconos = raiz.querySelectorAll<SVGElement>('[data-testid="approach-icon"]');
    const banda = raiz.querySelector<HTMLElement>('[data-testid="approaches-banner"] section');
    const titulo = raiz.querySelector<HTMLElement>('[data-testid="approaches-title"]');

    expect(iconos.length).toBe(3);
    iconos.forEach(icono => {
      expect(icono.getAttribute('width')).toBe('32');
      expect(icono.classList.contains('absolute')).toBeFalse();
      expect(icono.className.baseVal).toContain('text-[#8BF784]');
    });
    expect(raiz.querySelectorAll('[data-testid="approach-watermark"]').length).toBe(0);
    expect(banda?.className).toContain('bg-[#291242]');
    expect(banda?.className).not.toContain('background-image');
    expect(titulo?.className).toContain('font-alternate');
    expect(titulo?.className).toContain('leading-tight');
  });
});
