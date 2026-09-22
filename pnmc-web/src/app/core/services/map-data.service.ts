import { inject, Injectable } from '@angular/core';
import { forkJoin, Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { FestivalesParaElMapaService } from './festivales-para-el-mapa.service';
import { EventosParaElMapaService } from './eventos-para-el-mapa.service';
import { MercadosParaElMapaService } from './mercados-para-el-mapa.service';
import { CatalogService } from './catalog.service';
import * as MapDomain from '../../features/map/domain/map-domain';

/**
 * Cuenta registros PLANOS por departamento, normalizando el nombre.
 *
 * Reutiliza `normalizeDepartmentName` de `MapDomain` —la misma que usan los demás contadores— para
 * que «Bogotá D.C.» y «BOGOTA DC» sigan cayendo en el mismo territorio. Lo único que cambia
 * respecto del contador de festivales es de dónde lee el departamento.
 */
function contarPorDepartamentoPlano(registros: readonly { department?: string }[]): Record<string, number> {
  return registros.reduce<Record<string, number>>((acc, registro) => {
    const normalizado = MapDomain.normalizeDepartmentName(registro?.department ?? '');
    if (!normalizado || normalizado === 'DESCONOCIDO') return acc;
    acc[normalizado] = (acc[normalizado] || 0) + 1;
    return acc;
  }, {});
}

@Injectable({
  providedIn: 'root'
})
export class MapDataService {
  private readonly festivales = inject(FestivalesParaElMapaService);
  private readonly eventos = inject(EventosParaElMapaService);
  private readonly mercados = inject(MercadosParaElMapaService);
  private readonly catalog = inject(CatalogService);

  fetchMapCountsBundle(): Observable<any> {
    return forkJoin({
      geoJson: this.catalog.fetchColombiaGeoJson().pipe(
        catchError(() => of({ type: 'FeatureCollection', features: [], municipalities: { type: 'FeatureCollection', features: [] } }))
      ),
      festivals: this.festivales.consultar().pipe(
        catchError(() => of({ records: [] }))
      ),
      // LA AGENDA ENTRA AL MAPA. Cada evento guarda su departamento y su municipio; nunca se había
      // llevado allí. Va en el mismo `forkJoin` para que el mapa se pinte una vez con todo, en vez
      // de dos veces con la mitad.
      agenda: this.eventos.consultar().pipe(
        catchError(() => of({ records: [] }))
      ),
      // MERCADOS ENTRA AL MAPA. Su capa existía con sus colores y su escala, y se alimentaba de una
      // lista vacía porque sus tablas se habían retirado; la decisión escrita entonces fue que
      // «cada proceso volverá con su modelo y su revisión propios», y ya volvió.
      mercados: this.mercados.consultar().pipe(
        catchError(() => of({ records: [] }))
      )
    }).pipe(
      map(({ geoJson, festivals, agenda, mercados }) => {
        const baseCounts = MapDomain.getBaseDepartmentCounts();

        const festivalRecords = festivals.records || [];
        const agendaRecords = agenda.records || [];
        const marketRecords = mercados.records || [];
        return {
          geoJson,
          baseCounts,
          festivalRecords,
          agendaRecords,
          /*
           * LAS TRES LISTAS VACIAS NO SON UN OLVIDO, Y CONVIENE QUE QUEDE ESCRITO AQUI.
           *
           * MERCADOS MUSICALES YA SALIO DE AQUI: volvió con su modelo,
           * su circuito de revisión y su ruta pública, que es justo la condición que este comentario
           * ponía. Quedan tres.
           *
           * Escuelas de música, redes de documentación y lutieres son tres de los cinco procesos
           * cuyas tablas retiró `V20260904_01`, con sus clases del modelo y sus rutas. No hay de dónde leerlos: la decisión vigente es que «Festival es el único proceso
           * habilitado; cada proceso volverá con su modelo y su revisión propios».
           *
           * Se conservan como listas vacías —y no se borran— porque el geovisor las agrega para su
           * vista General y quitarlas obligaría a reescribir esa aritmética por algo que va a
           * volver. Lo que SI cambió es la pantalla: esas cuatro capas ya no se ofrecen como si
           * fueran consultables, se anuncian «en preparación».
           */
          schoolRecords: [],
          marketRecords,
          redesRecords: [],
          luthierRecords: [],
          festivalCounts: { ...baseCounts, ...MapDomain.buildFestivalCounts(festivalRecords) },
          // SE CUENTA CON EL MISMO CONTADOR que Festivales: resuelve el departamento probando las
          // cuatro claves y normaliza el nombre. Un segundo contador aquí daría dos cifras para el
          // mismo territorio en cuanto uno de los dos cambiara.
          agendaCounts: { ...baseCounts, ...MapDomain.buildFestivalCounts(agendaRecords) },
          schoolCounts: baseCounts,
          /*
            MERCADOS SE CUENTA APARTE DE FESTIVALES, y no por capricho.

            `buildFestivalCounts` lee `record.fields.departamento`, y un mercado es un registro
            PLANO: `map-domain.ts` declara desde hace meses los alias de las columnas del
            cuestionario de caracterización y los aplana a `department`, `municipality`… Toda la
            maquinaria de mercados que ya existe —el recuento por departamento, los totales, la
            ficha del mapa— lee esa forma. Pasarle un registro plano al contador de festivales
            devolvía cero para todos los departamentos, y el mapa enseñaba la capa encendida con
            «0 mercados» teniéndolos.
          */
          marketCounts: { ...baseCounts, ...contarPorDepartamentoPlano(marketRecords) },
        };
      })
    );
  }
}
