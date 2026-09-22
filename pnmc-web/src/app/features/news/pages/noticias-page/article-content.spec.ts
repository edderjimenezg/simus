import { TestBed } from '@angular/core/testing';
import { DomSanitizer } from '@angular/platform-browser';
import { sanitizeArticleContent } from './article-content';

/**
 * PNMC-004 — segunda mitad, la del navegador.
 *
 * La página de noticias pintaba el cuerpo con `[innerHTML]` sobre el resultado de
 * `bypassSecurityTrustHtml(...)`. Ese método no limpia: desactiva el saneador de
 * Angular para esa cadena. Con el saneador del servidor sorteado (ver PNMC-004 en
 * el backend), un `<img onerror=...>` guardado en la base llegaba al navegador del
 * lector sin una sola barrera por el camino.
 *
 * Estas pruebas comprueban dos cosas: que el marcado activo no sobrevive, y —la
 * mitad que suele faltar— que el texto editorial legítimo sí sobrevive. Una función
 * que devolviera siempre cadena vacía pasaría la primera mitad en verde.
 */
describe('sanitizeArticleContent (PNMC-004)', () => {
  let sanitizer: DomSanitizer;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    sanitizer = TestBed.inject(DomSanitizer);
  });

  it('devuelve una cadena ya saneada, no un valor de confianza', () => {
    const result = sanitizeArticleContent(sanitizer, '<p>Hola</p>');

    // `bypassSecurityTrustHtml` devuelve un objeto SafeHtml: si alguien lo
    // reintroduce, esta comprobación se pone roja.
    expect(typeof result).toBe('string');
  });

  it('quita las etiquetas de script', () => {
    const result = sanitizeArticleContent(sanitizer, '<p>ok</p><script>alert(1)</script>');

    expect(result).not.toContain('<script');
    expect(result).not.toContain('alert(1)');
    expect(result).toContain('ok');
  });

  it('quita los atributos de evento', () => {
    const result = sanitizeArticleContent(sanitizer, '<img src="x" onerror="alert(1)">');

    expect(result.toLowerCase()).not.toContain('onerror');
  });

  it('quita los atributos de evento aunque no lleven espacio delante', () => {
    const result = sanitizeArticleContent(sanitizer, '<img/onerror="alert(1)" src="x">');

    expect(result.toLowerCase()).not.toContain('onerror');
  });

  it('neutraliza javascript: en un enlace', () => {
    const result = sanitizeArticleContent(sanitizer, '<a href="javascript:alert(1)">x</a>');

    expect(result).not.toContain('href="javascript:');
  });

  it('neutraliza javascript: escrito con entidades HTML', () => {
    const result = sanitizeArticleContent(sanitizer, '<a href="&#106;avascript:alert(1)">x</a>');

    expect(result).not.toContain('href="javascript:');
  });

  it('quita svg y su onload', () => {
    const result = sanitizeArticleContent(sanitizer, '<svg onload="alert(1)"></svg>');

    expect(result.toLowerCase()).not.toContain('onload');
    expect(result.toLowerCase()).not.toContain('<svg');
  });

  it('quita iframes', () => {
    const result = sanitizeArticleContent(sanitizer, '<iframe src="https://malo.example"></iframe>');

    expect(result.toLowerCase()).not.toContain('<iframe');
  });

  it('conserva el texto enriquecido legítimo', () => {
    const result = sanitizeArticleContent(
      sanitizer,
      '<p>Hola <strong>mundo</strong></p><ul><li>uno</li></ul>'
    );

    expect(result).toContain('<strong>mundo</strong>');
    expect(result).toContain('<li>uno</li>');
  });

  it('conserva los enlaces https', () => {
    const result = sanitizeArticleContent(sanitizer, '<a href="https://pnmc.gov.co/a">link</a>');

    expect(result).toContain('https://pnmc.gov.co/a');
    expect(result).toContain('link');
  });

  it('devuelve cadena vacía cuando no hay cuerpo', () => {
    expect(sanitizeArticleContent(sanitizer, '')).toBe('');
    expect(sanitizeArticleContent(sanitizer, null)).toBe('');
    expect(sanitizeArticleContent(sanitizer, undefined)).toBe('');
  });
});
