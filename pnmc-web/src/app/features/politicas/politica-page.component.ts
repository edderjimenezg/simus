import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AdminService } from '../../core/services/admin.service';
import { PoliticaPublica } from '../../core/services/politicas-de-datos';

/**
 * El texto de una política, leído por cualquiera y sin cuenta.
 *
 * <b>POR QUE ES ANONIMA.</b> El artículo 12 de la Ley 1581 de 2012 obliga a informar al titular de
 * las finalidades y de sus derechos <i>antes</i> de pedirle la autorización, y quien va a
 * registrarse todavía no tiene cuenta. Detrás de una sesión, esa información llegaría después de
 * haberla dado. Y quien solo quiere saber qué hace este sistema con los datos de la gente no tiene
 * por qué crearse una cuenta para averiguarlo.
 *
 * <b>EL TEXTO LO SIRVE EL SERVIDOR, NO ESTA ESCRITO AQUI.</b> Es el mismo que el formulario de alta
 * muestra y el mismo que queda copiado dentro de cada autorización como prueba. Si esta página
 * llevara su propia redacción, alguien podría leer aquí una cosa, aceptar otra en el formulario y
 * que el sistema guardara una tercera.
 *
 * <b>LA VERSION SE ENSEÑA.</b> Sin ella, una persona no puede comprobar que lo que lee hoy es lo
 * mismo que aceptó, que es exactamente la comprobación que «Mis autorizaciones» permite hacer.
 */
@Component({
  selector: 'app-politica-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './politica-page.component.html',
})
export class PoliticaPageComponent implements OnInit {
  private readonly ruta = inject(ActivatedRoute);
  private readonly admin = inject(AdminService);

  readonly politica = signal<PoliticaPublica | null>(null);
  readonly cargando = signal(true);
  readonly noExiste = signal(false);

  ngOnInit(): void {
    this.ruta.paramMap.subscribe(parametros => {
      const clave = parametros.get('clave') ?? '';
      this.cargando.set(true);
      this.noExiste.set(false);

      this.admin.cargarPolitica(clave).subscribe({
        next: politica => {
          this.politica.set(politica);
          this.cargando.set(false);
        },
        error: () => {
          // UN 404 NO ES UN ERROR QUE HAYA QUE DISCULPAR. Significa que esa finalidad no existe en
          // este sistema —`participacion` y `directorio` no se crearon porque no hay pantalla ni
          // endpoint que las ejerza— y lo honesto es decirlo y ofrecer las que sí están.
          this.noExiste.set(true);
          this.cargando.set(false);
        },
      });
    });
  }
}
