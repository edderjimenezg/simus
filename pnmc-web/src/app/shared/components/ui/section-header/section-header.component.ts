import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-section-header',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="relative w-full text-left group" [class.mb-4]="compact" [class.lg:mb-6]="compact" [class.mb-8]="!compact" [class.lg:mb-12]="!compact">
      <!--
        \`max-w-full\` EN EL CONTENEDOR, NO SOLO EN LA MARCA DE AGUA.

        Un \`inline-block\` se ajusta a su contenido, asi que el \`max-w-full\` del hijo se medía
        contra un padre que ya se habia estirado: no limitaba nada. La palabra decorativa mide
        338 px —su tamano es fijo en \`rem\`, no depende del viewport— y a 320 px empujaba la
        pagina a 362. Limitando AQUI, el hijo ya tiene contra que recortarse.
      -->
      <div class="relative inline-block max-w-full">
        <!--
          aria-hidden: es una marca de agua decorativa detras del <h2>. Sin
          esto, un lector de pantalla anuncia la palabra suelta ("MAPA") y
          justo despues el encabezado real ("Ecosistema Musical de Colombia"),
          duplicando el titulo de cada seccion. Ocurre en cinco paginas.
          Marcarla como decorativa es ademas lo que la exime del criterio de
          contraste 1.4.3: su ratio efectivo sobre blanco es ~1,15:1.
        -->
        <div
          aria-hidden="true"
          class="max-w-full overflow-hidden font-gregor text-[4.5rem] lg:text-[8rem] select-none opacity-50 leading-none tracking-tight pointer-events-none text-left"
          style="color: #E6DAE5"
        >
          {{ backgroundText }}
        </div>
        <!--
          EL TITULO PUEDE ENVOLVER POR DEBAJO DE \`lg\`, Y ESO ARREGLA UN DESBORDE REAL.

          \`whitespace-nowrap\` mantenia el titulo y su guion decorativo en una sola linea. Como
          \`foregroundText\` sale del CMS, su ancho no lo decide este componente: medido el 11 sep
          2026, la PORTADA se iba a 444 px de ancho en un viewport de 375 —y a 444 tambien en 320,
          donde el desborde llegaba a 124 px—. Era el unico desborde horizontal del sitio publico,
          y este encabezado es COMPARTIDO: el defecto viajaba a cada seccion que lo usa.

          NO LO VIGILA EL TRINQUETE, y conviene saberlo: su ambito declarado son los \`.html\` bajo
          \`src\`, y esta plantilla vive en linea dentro de un \`.ts\`. Se encontro barriendo el sitio
          con un navegador real, no leyendo ficheros.

          La intencion del diseno —titulo y guion en la misma linea— se conserva desde \`lg\`.
        -->
        <div class="absolute bottom-0 left-0 z-10 flex flex-wrap items-end gap-3 text-left whitespace-normal md:gap-4 lg:flex-nowrap lg:whitespace-nowrap">
          <h2 class="font-gregor text-[#291242] uppercase tracking-tighter leading-none text-3xl lg:text-5xl">
            {{ foregroundText }}
          </h2>
          <div class="w-8 lg:w-12 h-1.5 bg-[#8BF784] rounded-full mb-1 opacity-80 group-hover:w-24 transition-all duration-500"></div>
        </div>
      </div>
    </div>
  `,
  styles: []
})
export class SectionHeaderComponent {
  @Input() backgroundText = '';
  @Input() foregroundText = '';
  @Input() compact = false;
}
