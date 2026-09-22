import { Injectable, inject } from '@angular/core';
import { Observable, expand, map, reduce, takeWhile } from 'rxjs';
import { ApiClientService } from '../http/api-client.service';
import { RegistroDeFestivalParaElMapa } from './festivales-para-el-mapa.service';

/**
 * Los eventos publicados de la Agenda, en la forma que consume el geovisor.
 *
 * <b>POR QUE LA AGENDA Y NO OTRA COSA.</b> El mapa anunciaba seis capas y cinco marcaban cero. Las
 * cuatro que sobran corresponden a los procesos cuyas tablas retiró <c>V20260904_01</c>: no es que
 * estuvieran desconectadas, es que no existen. La Agenda, en cambio, <b>sí tiene territorio y sí
 * tiene lectura pública</b> —cada evento guarda su departamento y su municipio, atados por CHECK a
 * su nivel de cobertura— y nunca se había llevado al mapa. Es la única capa nueva que se puede
 * conectar hoy sin reabrir el modelo retirado.
 *
 * <b>MISMA FORMA QUE FESTIVALES, A PROPOSITO.</b> Devuelve `RegistroDeFestivalParaElMapa`: el
 * geovisor lee esa forma —`fields.name`, `fields.dpt`, `fields.municipio`— en una treintena de
 * sitios, y darle una segunda forma obligaría a que cada uno de esos sitios supiera de cuál se
 * trata. El nombre del tipo se queda corto ahora que lo usan dos capas, y eso es preferible a
 * duplicar el tipo para renombrarlo.
 *
 * <b>SE PAGINA DE VERDAD, Y ESO COSTO UN DEFECTO SILENCIOSO.</b> Este servicio pedía
 * <c>limit=500&offset=0</c>, que es como pagina la lectura pública de Festivales. La de Agenda
 * pagina con <c>pagina</c> y <c>tamano</c>: los dos parámetros que se enviaban no existen para ella,
 * los descartaba sin quejarse y devolvía su página por omisión —doce eventos—. Con nueve eventos en
 * la base no se notaba nada; con doscientos noventa y uno, el mapa contaba doce.
 *
 * <b>Y EL TOPE POR PAGINA ES CINCUENTA</b>, declarado en el endpoint y con motivo: protege una
 * lectura pública paginada. Pedir más no lo sube, lo recorta en silencio. Así que se recorren las
 * páginas hasta completar el total, que es lo que el mapa necesita: cuenta por departamento sobre
 * el conjunto, y una página parcial daría cifras que no son las del país.
 *
 * <b>SOLO LO PUBLICADO, Y ESO LO DECIDE EL SERVIDOR.</b> Se pide la ruta pública, la misma que
 * alimenta la página de Agenda. Un evento en borrador no está en el portal y tampoco puede estar en
 * el mapa: el mapa es una lectura pública más.
 *
 * <b>UN EVENTO NACIONAL NO TIENE MUNICIPIO, Y ESO NO ES UN HUECO.</b> `NivelCobertura` lo declara.
 * El geovisor ya sabe repartir un registro sin municipio por su departamento, y uno sin
 * departamento no se dibuja: es lo mismo que hace con un Festival de cobertura nacional.
 */

/** El evento tal como lo entrega la lectura pública. */
interface EventoPublico {
  readonly id?: number | string;
  readonly titulo?: string;
  readonly descripcion?: string | null;
  readonly nombreDepartamento?: string | null;
  readonly nombreMunicipio?: string | null;
  readonly nivelCobertura?: string | null;
  readonly organizador?: string | null;
  readonly url?: string | null;
  readonly categoria?: string | null;
  readonly fechaInicio?: string | null;
  readonly modalidad?: string | null;
  readonly lugar?: string | null;
}

const texto = (valor: unknown): string =>
  typeof valor === 'string' ? valor.trim() : typeof valor === 'number' ? String(valor) : '';

@Injectable({ providedIn: 'root' })
export class EventosParaElMapaService {
  private readonly api = inject(ApiClientService);

  /** Lo que admite el endpoint por página, declarado allí. Pedir más no sube el tope. */
  private static readonly POR_PAGINA = 50;

  /**
   * Cuántas páginas se recorren como mucho.
   *
   * <b>UN TECHO EXPLICITO Y NO UN BUCLE ABIERTO.</b> Son mil eventos, holgado para el corte actual
   * y para varios más. El día que la Agenda lo pase, el mapa necesita una ruta que le dé los
   * conteos por departamento en vez de traerse los eventos uno a uno: veinte peticiones al abrir el
   * mapa ya es demasiado, y doscientas sería inaceptable. Prefiero que ese día el techo esté
   * escrito aquí a que el mapa siga contando de menos sin decirlo, que es como empezó esto.
   */
  private static readonly PAGINAS_MAXIMAS = 20;

  /**
   * Trae los eventos publicados, recorriendo las páginas.
   */
  consultar(): Observable<{ records: RegistroDeFestivalParaElMapa[] }> {
    return this.pedirPagina(1).pipe(
      expand((respuesta, indice) =>
        indice + 2 <= Math.min(respuesta.totalPaginas ?? 1, EventosParaElMapaService.PAGINAS_MAXIMAS)
          ? this.pedirPagina(indice + 2)
          : [],
      ),
      takeWhile(() => true),
      reduce((acumulado: EventoPublico[], respuesta) =>
        acumulado.concat(Array.isArray(respuesta?.items) ? respuesta.items : []), []),
      map((eventos) => ({
        records: eventos
          // SIN DEPARTAMENTO NO HAY NADA QUE DIBUJAR. Un evento nacional es legítimo y se cuenta en
          // la agenda, pero en un mapa por departamentos no tiene dónde ponerse: colocarlo en uno
          // cualquiera sería inventar un territorio.
          .filter((evento) => texto(evento?.nombreDepartamento).length > 0)
          .map((evento) => ({
            id: texto(evento?.id),
            fields: {
              name: texto(evento?.titulo),
              // Las cuatro claves del territorio, rellenas igual que en Festivales: el geovisor
              // prueba con todas porque sobrevivió a tres orígenes de datos distintos.
              dpt: texto(evento?.nombreDepartamento),
              departamento: texto(evento?.nombreDepartamento),
              municipio: texto(evento?.nombreMunicipio),
              coverageLevel: texto(evento?.nivelCobertura),
              desc: texto(evento?.descripcion),
              organizador: texto(evento?.organizador),
              sitio_web: texto(evento?.url),
              // LO PROPIO DE UN EVENTO, que un Festival no tiene: cuándo es y dónde exactamente.
              // La ficha del mapa lo enseña; los filtros del geovisor no lo usan.
              fecha: texto(evento?.fechaInicio),
              modalidad: texto(evento?.modalidad),
              lugar: texto(evento?.lugar),
              categoria: texto(evento?.categoria),
              // EL GEOVISOR FILTRA POR ESTAS DOS y las pide a todo registro. La Agenda no clasifica
              // por práctica ni por territorio sonoro, así que van vacías: filtrar por ellas deja
              // la capa fuera, que es lo correcto —no tiene ese dato, no que lo tenga en blanco—.
              'Prácticas musicales': '',
              'Territorios sonoros': '',
            },
          })),
      })),
    );
  }

  private pedirPagina(pagina: number): Observable<{ items?: EventoPublico[]; totalPaginas?: number }> {
    return this.api.get<{ items?: EventoPublico[]; totalPaginas?: number }>('/api/v1/publico/agenda', {
      // `cuando=todos` Y NO LA OMISION, QUE ES «proximos». El panel del mapa ofrece tres tramos
      // —Próximos, Este mes, Todos— y los resuelve por su cuenta sobre lo que recibe; pidiendo la
      // omisión, el servidor ya había descartado todo lo pasado, así que el botón «Todos» del mapa
      // enseñaba exactamente lo mismo que «Próximos» y la lectura del año entero era imposible.
      // Medido con los datos sembrados: 262 eventos de 462, y siete meses del calendario vacíos.
      params: { pagina, tamano: EventosParaElMapaService.POR_PAGINA, cuando: 'todos' },
      errorFallback: 'No fue posible cargar la agenda del mapa',
    });
  }
}
