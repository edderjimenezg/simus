import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { ejesDataGlobal } from '../../../../core/services/ejes-data.config';
import { EjesPageComponent } from './ejes-page.component';

describe('EjesPageComponent', () => {
  let fixture: ComponentFixture<EjesPageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EjesPageComponent],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(EjesPageComponent);
    fixture.detectChanges();
  });

  it('starts with every component collapsed', () => {
    const triggers = fixture.debugElement.queryAll(By.css('button[aria-controls^="component-content-"]'));
    const expectedTotal = ejesDataGlobal.reduce((total, eje) => total + eje.components.length, 0);

    expect(triggers.length).toBe(expectedTotal);
    expect(triggers.every(trigger => trigger.attributes['aria-expanded'] === 'false')).toBeTrue();
  });

  it('shows the complete source-page description inline without repeated titles', () => {
    const firstComponent = ejesDataGlobal[0].components[0];
    const firstTrigger = fixture.debugElement.query(By.css('button[aria-controls="component-content-c1-1"]'));

    firstTrigger.nativeElement.click();
    fixture.detectChanges();

    const content = fixture.debugElement.query(By.css('#component-content-c1-1'));
    const paragraphs = content.queryAll(By.css('p'));

    expect(firstTrigger.attributes['aria-expanded']).toBe('true');
    expect(paragraphs.map(paragraph => paragraph.nativeElement.textContent.trim())).toEqual(firstComponent.fullText);
    expect(content.queryAll(By.css('h4')).length).toBe(0);
    expect(content.nativeElement.textContent).not.toContain('Explorar componente');
  });

  it('keeps a same-tab link to every component subpage', () => {
    const links = fixture.debugElement.queryAll(By.css('a[href^="/ejes/"]'));
    const expectedTotal = ejesDataGlobal.reduce((total, eje) => total + eje.components.length, 0);

    expect(links.length).toBe(expectedTotal);
    expect(links[0].attributes['href']).toBe('/ejes/apropiacion-y-derechos');
    expect(links[0].attributes['target']).toBeUndefined();
  });

  it('ubica cada componente exclusivamente dentro de su eje propietario', () => {
    const links = fixture.debugElement.queryAll(By.css('a[href^="/ejes/"]'));
    const expected = ejesDataGlobal.flatMap((eje) => eje.components
      .map((component) => `/ejes/${component.slug}`));

    expect(links.map((link) => link.attributes['href'])).toEqual(expected);
  });

  it('closes an expanded component when its heading is selected again', () => {
    const firstTrigger = fixture.debugElement.query(By.css('button[aria-controls="component-content-c1-1"]'));

    firstTrigger.nativeElement.click();
    fixture.detectChanges();
    firstTrigger.nativeElement.click();
    fixture.detectChanges();

    expect(firstTrigger.attributes['aria-expanded']).toBe('false');
  });
});
