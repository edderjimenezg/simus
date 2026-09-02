import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    TestBed.inject(HttpTestingController).expectOne('/api/registro/preparacion').flush({ registroDisponible: false, territorioDisponible: false, documentos: [], impedimentos: [] });
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('shows the access form and uses contextual labels', async () => {
    const fixture = TestBed.createComponent(App);
    TestBed.inject(HttpTestingController).expectOne('/api/registro/preparacion').flush({ registroDisponible: false, territorioDisponible: false, documentos: [], impedimentos: [] });
    fixture.detectChanges();
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Gestiona la información');
    expect(compiled.querySelector('label[for="correo"]')?.textContent).toContain('Correo electrónico');
  });
});
