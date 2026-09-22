import { SecurityContext } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';

/**
 * PNMC-004 — Cuerpo de una noticia listo para `[innerHTML]`.
 *
 * Antes, la página llamaba a `bypassSecurityTrustHtml(article.content)`. Ese método
 * no sanea nada: le dice a Angular «confía en esta cadena y píntala tal cual», y
 * desactiva justo la defensa que impide que un `<img onerror>` guardado en la base
 * se ejecute en el navegador de quien lee la noticia. El nombre engaña —parece que
 * marca algo como seguro— pero lo que hace es renunciar a comprobarlo.
 *
 * Aquí se hace lo contrario: se pasa el HTML por el saneador de Angular
 * (`sanitize(SecurityContext.HTML, …)`), que devuelve una cadena normal ya limpia.
 * Al ser una cadena y no un `SafeHtml`, Angular la vuelve a sanear al enlazarla con
 * `[innerHTML]`; eso es deliberado, es barato y deja el camino sin un solo punto
 * donde la única barrera sea un `bypass…`.
 *
 * La barrera de verdad está en el servidor (PNMC.Infrastructure.Common.HtmlSanitizer,
 * lista blanca sobre analizador HTML5): esta es la segunda, no la única.
 */
export function sanitizeArticleContent(
  sanitizer: DomSanitizer,
  content: string | null | undefined
): string {
  if (!content) {
    return '';
  }

  return sanitizer.sanitize(SecurityContext.HTML, content) ?? '';
}
