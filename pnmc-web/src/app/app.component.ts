import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { NavigationComponent } from './shared/components/layout/navigation/navigation.component';
import { FooterComponent } from './shared/components/layout/footer/footer.component';
import { FloatingButtonComponent } from './shared/components/ui/floating-button/floating-button.component';
import { NavigationService, PAGE_IDS } from './core/services/navigation.service';
import { ExternalSessionService } from './core/services/external-session.service';
import { RecorridoGuiadoService } from './core/services/recorrido-guiado.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    RouterOutlet,
    NavigationComponent,
    FooterComponent,
    FloatingButtonComponent,
    CommonModule
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent {
  title = 'PNMC';
  public navigationService = inject(NavigationService);

  /**
   * LA SESION SE CONSULTA EN EL ARMAZON, no en cada pagina.
   *
   * La barra de navegacion vive aqui y tiene que decidir en cada carga si pinta «Iniciar sesion» o
   * el panel de la organizacion. Antes solo `/registro` preguntaba por la sesion, asi que una
   * organizacion que acababa de entrar volvia al inicio y encontraba otra vez el boton de entrar:
   * la sesion existia en la cookie y en el servidor, pero el sitio no la miraba.
   */
  private readonly sesionExterna = inject(ExternalSessionService);
  
  // Estado de scroll reactivo
  scrolled = signal(false);

  constructor() {
    this.sesionExterna.cargarUnaVez();
  }

  @HostListener('window:scroll', [])
  onWindowScroll() {
    this.scrolled.set(window.scrollY > 20);
  }

  showNavigation = computed(() => {
    const page = this.navigationService.activePage();
    return page !== PAGE_IDS.admin;
  });

  isSolidNavigation = computed(() => {
    const page = this.navigationService.activePage();
    // Ecosistema conserva un hero fotográfico (como PNMC), así que el nav
    // debe comportarse igual: transparente sobre la foto y sólido al hacer scroll.
    // Mapa/Editorial/Galería/Noticias/Agenda ya no tienen hero, así que necesitan
    // el nav sólido desde el inicio (un nav transparente sería ilegible sobre fondo blanco).
    return page === PAGE_IDS.mapa
      || page === PAGE_IDS.editorial
      || page === PAGE_IDS.galeria
      || page === PAGE_IDS.noticias
      || page === PAGE_IDS.agenda
      // El panel de la organizacion externa va sobre fondo claro desde el 27 de agosto de
      // 2026. Con la barra transparente, su logotipo blanco quedaba encima del titulo.
      || page === PAGE_IDS.miPanel
      // La 404 tampoco tiene hero fotografico, asi que sigue la misma regla.
      || page === PAGE_IDS.noEncontrado
      // La pantalla de acceso NO es un hero foto de punta a punta como Inicio o Ecosistema:
      // es dos columnas desde el primer pixel, y la mitad derecha es clara. Un nav transparente
      // pintaba sus botones -y su propio logotipo- en blanco sobre esa mitad clara, ilegibles.
      // El criterio es este: «algunos botones de arriba
      // de la barra de navegación no se ven».
      || page === PAGE_IDS.registro;
  });

  showGlobalFooter = computed(() => {
    const page = this.navigationService.activePage();
    // La 404 SI lleva pie de pagina. Antes se resolvia como `home`, y `home`
    // no lo lleva: una pagina de error sin ninguna salida es una via muerta.
    return page !== PAGE_IDS.home &&
           page !== PAGE_IDS.admin &&
           page !== PAGE_IDS.registro &&
           page !== PAGE_IDS.mapa;
  });

  /**
   * El botón flotante se aparta durante un recorrido guiado.
   *
   * No es preferencia estética: este botón está fijo en la esquina inferior derecha y la tarjeta
   * del recorrido se coloca donde quepa, que en pantallas estrechas es a menudo esa misma esquina.
   * Medido a 390 px, el botón caía encima de «Siguiente». Y un botón que invita a irse a otra parte
   * no pinta nada durante una explicación paso a paso.
   */
  private readonly recorridoGuiado = inject(RecorridoGuiadoService);

  showFloatingButton = computed(() =>
    this.navigationService.activePage() !== PAGE_IDS.registro && !this.recorridoGuiado.hayRecorrido());

  /**
   * Texto que la región `aria-live` anuncia al cambiar de ruta.
   *
   * Se deriva del título del documento, que `NavigationService` ya mantiene al
   * día. Reutilizarlo evita una segunda fuente de verdad para el nombre de cada
   * página — que es justo el problema que tiene el resto de la navegación.
   *
   * Antes no existía nada: en una aplicación de página única el navegador no
   * recarga el documento, así que un lector de pantalla no daba ninguna señal
   * de haber llegado a otro sitio.
   */
  anuncioDeRuta = computed(() => {
    const pagina = this.navigationService.activePage();
    if (!pagina) return '';
    return `Has llegado a ${this.navigationService.nombreDePagina(pagina)}`;
  });
}
