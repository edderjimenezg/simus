import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { CatalogoEditorialService } from '../../../../core/services/catalogo-editorial.service';
import { RecursoEditorialDeDiseno } from '../../../../core/services/adaptadores-del-diseno';
import { EditorialPageComponent } from './editorial-page.component';

/**
 * El listado del Catálogo Editorial.
 *
 * <b>QUE FIJA ESTA PRUEBA, Y POR QUE NO EXISTIA.</b> La pantalla se portó del diseño aprobado sin
 * pruebas propias, y su defecto más visible vivía justo ahí: <b>nueve series del acervo tienen
 * varios volúmenes con el mismo título</b>, así que dos publicaciones llamadas «Acento» daban dos
 * tarjetas y dos filas idénticas. El criterio es este: «Acento dice solo Acento, y es
 * Acento, arreglos para banda-escuela, volumen I». El subtítulo y el volumen son lo único que las
 * separa sin abrirlas, y tienen que verse <b>en las dos vistas</b>, no solo en el mosaico.
 */
function recurso(cambios: Partial<RecursoEditorialDeDiseno> = {}): RecursoEditorialDeDiseno {
  return {
    id: 'PNMC-ED-012', title: 'Acento', year: '2011', section: 'Repertorio',
    sectionPath: 'Repertorio > Banda', publicationType: 'Libro', practice: 'Banda',
    category: 'Formación', subcategory: '', regionalScope: 'Nacional',
    author: 'Ana Restrepo', corporateAuthor: '', additionalCredits: '',
    displayAuthor: 'Ana Restrepo', isbn: '', ismn: '', formatSize: '', pages: '', duration: '',
    summary: '', coverText: '', thumbnail: '',
    keywords: [], subtitle: 'Arreglos para banda-escuela', volume: 'Volumen I', series: '',
    language: 'es', edtfDate: '2011', dateNote: '', secondaryCategory: '', scopeText: 'Nacional',
    mediaFormat: '', licence: '', typology: [], credits: [], identifiers: [], access: [],
    ...cambios,
  };
}

describe('EditorialPageComponent', () => {
  let fixture: ComponentFixture<EditorialPageComponent>;

  const montar = (items: RecursoEditorialDeDiseno[]) => {
    TestBed.overrideProvider(CatalogoEditorialService, {
      useValue: { listarParaElPortal: () => of({ items }) } as Partial<CatalogoEditorialService>,
    });
    fixture = TestBed.createComponent(EditorialPageComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EditorialPageComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('enseña el subtítulo y el volumen en el mosaico, que es donde se eligen las obras', () => {
    const raiz = montar([recurso()]);

    expect(raiz.textContent).toContain('Arreglos para banda-escuela');
    expect(raiz.textContent).toContain('Volumen I');
  });

  it('los enseña también en la vista de lista, donde ni siquiera hay portada que distinga', () => {
    montar([
      recurso({ id: 'PNMC-ED-012', subtitle: 'Arreglos para banda-escuela', volume: 'Volumen I' }),
      recurso({ id: 'PNMC-ED-013', subtitle: 'Arreglos para banda-escuela', volume: 'Volumen II' }),
    ]);
    fixture.componentInstance.setViewMode('table');
    fixture.detectChanges();

    const filas = (fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr');
    expect(filas.length).toBeGreaterThan(1);
    // Las dos se llaman «Acento»: lo que las separa es el volumen, y tiene que estar en la fila.
    expect(filas[0].textContent).toContain('Volumen I');
    expect(filas[1].textContent).toContain('Volumen II');
  });

  it('una publicación sin subtítulo no deja una línea vacía debajo del título', () => {
    const raiz = montar([recurso({ subtitle: '', volume: '' })]);

    expect(raiz.textContent).toContain('Acento');
    expect(raiz.textContent).not.toContain('Volumen');
  });
});
