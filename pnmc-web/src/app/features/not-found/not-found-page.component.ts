import { Component, inject } from '@angular/core';
import { NavigationService } from '../../core/services/navigation.service';
import { TextosWebService } from '../../core/services/textos-web.service';

@Component({
  selector: 'app-not-found-page',
  standalone: true,
  template: `
    <!-- section y no main: el landmark principal unico lo declara el shell en
         app.component.html, y este componente se pinta dentro de el. -->

    <section
      class="min-h-screen flex flex-col items-center justify-center bg-[#291242] px-6 py-32 text-center text-white"
    >
      <p class="font-alternate text-[0.7rem] font-bold uppercase tracking-[0.35em] text-[#00DA5E]">
        {{ texto('notfound_eyebrow') }}
      </p>
      <h1 class="mt-4 font-gregor text-6xl sm:text-7xl md:text-8xl leading-none">
        {{ texto('notfound_title') }}
      </h1>
      <p class="mt-6 max-w-xl font-nunito text-sm sm:text-base text-white/70 leading-relaxed">
        {{ texto('notfound_desc') }}
      </p>

      <div class="mt-10 flex flex-col sm:flex-row items-center gap-4">
        <button
          type="button"
          (click)="goHome()"
          class="rounded-xl bg-[#00DA5E] px-7 py-3 font-alternate text-[0.72rem] font-bold uppercase tracking-widest text-[#291242] transition-all hover:bg-[#8BF784] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white focus-visible:ring-offset-2 focus-visible:ring-offset-[#291242] cursor-pointer"
        >
          {{ texto('notfound_cta_home') }}
        </button>
        <button
          type="button"
          (click)="goToMap()"
          class="rounded-xl border border-white/25 bg-white/10 px-7 py-3 font-alternate text-[0.72rem] font-bold uppercase tracking-widest text-white transition-all hover:border-white/40 hover:bg-white/20 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#00DA5E] focus-visible:ring-offset-2 focus-visible:ring-offset-[#291242] cursor-pointer"
        >
          {{ texto('notfound_cta_map') }}
        </button>
      </div>
    </section>
  `,
})
export class NotFoundPageComponent {
  private navigationService = inject(NavigationService);
  private webTexts = inject(TextosWebService);

  /** Texto editable del 404. Ver el grupo `general_404` del registro. */
  texto(clave: string): string {
    return this.webTexts.getWebText(clave);
  }

  goHome(): void {
    this.navigationService.navigate('home');
  }

  goToMap(): void {
    this.navigationService.navigate('mapa');
  }
}
