import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TextosWebService } from '../../../../core/services/textos-web.service';

@Component({
  selector: 'app-footer',
  standalone: true,
  // `RouterLink` para las dos políticas propias: sin importarlo, `routerLink` se queda en un
  // atributo inerte —el compilador no se queja— y el enlace no lleva a ninguna parte.
  imports: [CommonModule, RouterLink],
  templateUrl: './footer.component.html',
  styleUrls: ['./footer.component.css']
})
export class FooterComponent {
  private webTexts = inject(TextosWebService);
  currentYear = new Date().getFullYear();

  // NOTA: destinos institucionales por defecto. Reemplazar por las URLs oficiales
  // definitivas del PNMC/Ministerio cuando el equipo de contenido las confirme.
  private readonly minculturaBase = 'https://www.mincultura.gov.co';

  socialNetworks: { label: string; href: string }[] = [
    { label: 'YouTube', href: 'https://www.youtube.com/@MinCulturasCol' },
    { label: 'Instagram', href: 'https://www.instagram.com/minculturascol' },
    { label: 'Facebook', href: 'https://www.facebook.com/MinCulturasCol' },
    { label: 'X', href: 'https://x.com/MinCulturasCol' },
    { label: 'WhatsApp', href: 'https://wa.me/573138000000' },
    { label: 'TikTok', href: 'https://www.tiktok.com/@minculturascol' },
  ];

  citizenshipServices: { label: string; href: string }[] = [
    { label: 'PQRSD', href: `${this.minculturaBase}/atencion-al-ciudadano/Paginas/PQRS.aspx` },
    { label: 'Preguntas Frecuentes', href: `${this.minculturaBase}/atencion-al-ciudadano/preguntas-frecuentes` },
    { label: 'Glosario', href: `${this.minculturaBase}/atencion-al-ciudadano/glosario` },
    { label: 'Trámites y servicios', href: `${this.minculturaBase}/atencion-al-ciudadano/tramites-y-servicios` },
  ];

  /**
   * Lo relativo al sitio. Dos de estas cinco son NUESTRAS y las otras tres del Ministerio.
   *
   * <b>POR QUE DOS DEJAN DE SALIR FUERA.</b> «Política de privacidad y protección de datos» y
   * «Términos y condiciones» llevaban a dos páginas de mincultura.gov.co. Son documentos
   * institucionales legítimos, pero no son lo que ESTE sistema le dijo a esta persona antes de
   * pedirle sus datos: ese texto es el que sirve `GET /publico/politicas/{clave}` y el que queda
   * copiado dentro de cada autorización como prueba (Ley 1581 art. 17 lit. f). Quien busca «qué me
   * dijeron sobre mis datos» tiene que llegar a él, no a un PDF general del Ministerio.
   *
   * Las páginas propias enlazan además el documento institucional completo, así que no se pierde
   * nada: se gana el eslabón que faltaba.
   */
  aboutSite: { label: string; href: string; interno?: boolean }[] = [
    { label: 'Políticas', href: `${this.minculturaBase}/politicas` },
    { label: 'Política de tratamiento de datos', href: '/politicas/tratamiento', interno: true },
    { label: 'Mapa del sitio', href: `${this.minculturaBase}/mapa-del-sitio` },
    { label: 'Términos de uso', href: '/politicas/terminos', interno: true },
    { label: 'Accesibilidad', href: `${this.minculturaBase}/accesibilidad` },
  ];

  getWebText(key: string): string {
    return this.webTexts.getWebText(key);
  }
  /**
   * Imagen editable. Devuelve la publicada en el CMS o, si no hay ninguna, la
   * compilada del registro: nunca cadena vacia, porque un `src` vacio hace que
   * el navegador vuelva a pedir la pagina. Ver `registro-de-imagenes-web.ts`.
   */
  imagen(clave: string): string {
    return this.webTexts.getWebImage(clave);
  }

  /** Texto alternativo de esa misma ranura. */
  imagenAlt(clave: string): string {
    return this.webTexts.getWebImageAlt(clave);
  }
}
