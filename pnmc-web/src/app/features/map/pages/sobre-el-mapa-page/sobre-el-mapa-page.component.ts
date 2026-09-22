import { Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CompactHeroComponent } from '../../../../shared/components/ui/compact-hero/compact-hero.component';

/**
 * «Sobre este mapa»: por qué el geovisor se dibuja como se dibuja.
 *
 * <b>POR QUE EXISTE UNA PAGINA Y NO UN GLOBO DE AYUDA.</b> El tutorial del mapa enseña a USARLO
 * —dónde se pulsa, qué hace cada control—; esto responde otra pregunta, que es por qué está hecho
 * así: por qué tres formas de dibujar y no una, por qué el área de un círculo crece con la raíz de
 * la cifra, por qué la rampa no es el arcoíris. Son decisiones cartográficas con más de un siglo de
 * discusión detrás, y un mapa público de una entidad del Estado debería poder responder de ellas.
 *
 * <b>LOS REFERENTES SON VERIFICABLES, Y ESO ACOTA LO QUE SE DICE.</b> Cada afirmación de esta página
 * se puede rastrear hasta una fuente concreta —una publicación, un catálogo oficial, un marco
 * internacional—. Donde la fuente matiza, se dice el matiz; donde no hay fuente, no se afirma.
 *
 * <b>NO DUPLICA EL MAPA.</b> Las cifras viven en el geovisor y cambian con los datos; aquí sólo está
 * el razonamiento, que no cambia. Por eso la página no consulta el API: si lo hiciera, dos sitios
 * dirían cuántos festivales hay y acabarían discrepando.
 */
@Component({
  selector: 'app-sobre-el-mapa-page',
  standalone: true,
  imports: [RouterLink, CompactHeroComponent],
  templateUrl: './sobre-el-mapa-page.component.html',
})
export class SobreElMapaPageComponent {
  private readonly router = inject(Router);

  volver(): void {
    void this.router.navigate(['/mapa-ecosistemico']);
  }

  /**
   * Las tres formas de dibujar, con lo que responde cada una y lo que deforma.
   *
   * <b>CADA UNA LLEVA SU SESGO ESCRITO.</b> No hay una proyección neutral ni una forma de dibujar
   * que no elija qué destacar: decirlo es lo que permite leer el mapa con criterio en vez de
   * creérselo entero.
   */
  readonly formasDeDibujar = [
    {
      nombre: 'Coropletas',
      responde: '¿Cuánto hay en cada departamento?',
      como: 'Tiñe cada polígono administrativo con una intensidad proporcional a su cifra.',
      sesgo: 'Un departamento grande y casi vacío pesa más en la vista que uno pequeño y lleno, '
        + 'porque el ojo suma superficie y no cifras. Por eso no basta sola.',
      referente: 'La primera de la que se tiene registro es la «carte figurative» de Charles Dupin '
        + '(1826), que sombreó los departamentos franceses según su nivel de instrucción. El sesgo '
        + 'de área es el defecto clásico de la técnica y lo recoge, entre otros, Mark Monmonier en '
        + '«How to Lie with Maps».',
    },
    {
      nombre: 'Símbolos proporcionales',
      responde: '¿Cuánto hay en cada municipio?',
      como: 'Un círculo por municipio, con el ÁREA proporcional a la cifra. El tamaño del '
        + 'territorio deja de influir: un municipio diminuto con veinte procesos se ve grande.',
      sesgo: 'Los círculos se pisan donde hay muchos juntos. Se dibujan translúcidos y con borde '
        + 'para que el solape se lea como solape y no tape lo de debajo.',
      referente: 'El diámetro crece con la RAÍZ CUADRADA de la cifra porque lo que el ojo compara '
        + 'es la superficie: escalar el diámetro haría que cuatro procesos se vieran dieciséis '
        + 'veces más grandes que uno. James Flannery mostró además que, incluso así, quien mira '
        + 'tiende a subestimar los círculos grandes, y propuso una corrección perceptual; aquí se '
        + 'usa la escala por área, que es la convención más extendida y la que la leyenda explica.',
    },
    {
      nombre: 'Mapa de calor',
      responde: '¿Dónde se concentra la actividad?',
      como: 'Una superficie continua de densidad que no conoce fronteras administrativas.',
      sesgo: 'Lo que llama «cerca» depende del acercamiento. Por eso la leyenda dice a cuántos '
        + 'kilómetros equivale el radio EN ESTA VISTA, y esa cifra cambia al acercarse.',
      referente: 'Su antecedente es el mapa de puntos con que John Snow situó los casos de cólera '
        + 'en el Soho de Londres en 1854: la concentración se leía sin que ningún límite '
        + 'administrativo la partiera. La versión continua se apoya en la estimación de densidad '
        + 'por núcleos, formulada por Rosenblatt y Parzen a mediados del siglo XX.',
    },
  ];

  /**
   * Las tres lecturas del lente, y por qué la clasificación no es un filtro.
   */
  readonly lentes = [
    {
      nombre: 'Por territorio',
      que: 'Colombia como está organizada administrativamente: departamentos y municipios.',
      porque: 'Es la división con la que se gobierna y se presupuesta, así que es la que permite '
        + 'hablar con una alcaldía o una gobernación. No es la única forma de leer el país.',
    },
    {
      nombre: 'Por territorios sonoros',
      que: 'Las catorce configuraciones culturales y musicales que reconoce el Plan Nacional de '
        + 'Música para la Convivencia.',
      porque: 'Un territorio sonoro no coincide con un departamento: lo atraviesa. Ver el mapa así '
        + 'enseña continuidades culturales que la división administrativa parte en pedazos, y es la '
        + 'razón por la que el mapa de calor existe: sus manchas siguen dónde está el proceso y no '
        + 'dónde acaba una frontera.',
    },
    {
      nombre: 'Por prácticas y géneros',
      que: 'Las dieciséis prácticas musicales del catálogo del Plan.',
      porque: 'Una práctica reúne procesos de regiones que no se parecen en nada más. Es la lectura '
        + 'que enseña parentescos por encima de la geografía.',
    },
  ];

  /**
   * De dónde sale cada cosa que el mapa afirma.
   *
   * <b>UN MAPA AFIRMA DOS COSAS SOBRE CADA PROCESO</b> —dónde está y de qué es—, y quien lo lee
   * tiene derecho a saber de dónde salen las dos.
   */
  readonly procedencias = [
    {
      dato: 'Los límites y los nombres del territorio',
      fuente: 'Marco Geoestadístico Nacional y DIVIPOLA del DANE',
      detalle: 'La cartografía departamental y municipal y los códigos de los 1.122 municipios '
        + 'salen del catálogo oficial del DANE, incorporado por una operación explícita y fechada. '
        + 'El geovisor no guarda una lista propia de territorios.',
    },
    {
      dato: 'Dónde se dibuja cada proceso',
      fuente: 'Centroide oficial del municipio (MGN 2025)',
      detalle: 'Ubica el MUNICIPIO, no el lugar exacto del proceso. Un punto de alfiler afirmaría '
        + 'una precisión que el dato no tiene; por eso el símbolo se ancla por su centro y no por '
        + 'una punta.',
    },
    {
      dato: 'Territorio sonoro y práctica',
      fuente: 'Lo que cada registro declara en su ficha',
      detalle: 'No se deduce del nombre ni de la descripción. Lo que no lo declara se cuenta '
        + 'aparte, como «Sin clasificar»: es la diferencia entre «no consta» y «desaparece».',
    },
    {
      dato: 'Los procesos y los eventos',
      fuente: 'Lecturas públicas del propio SIMUS',
      detalle: 'El mapa lee lo mismo que el portal: sólo lo publicado. Un registro en borrador no '
        + 'está en el sitio y tampoco puede estar en el mapa.',
    },
  ];

  /** Los marcos que sostienen el enfoque, más allá de la técnica de dibujo. */
  readonly marcos = [
    {
      ambito: 'Nacional',
      titulo: 'Plan Nacional de Música para la Convivencia',
      detalle: 'El PNMC, del Ministerio de las Culturas, las Artes y los Saberes, aporta el marco '
        + 'conceptual: los territorios sonoros, las prácticas musicales y la idea de ecosistema '
        + 'musical que ordena todo el sistema.',
    },
    {
      ambito: 'Nacional',
      titulo: 'DANE — Marco Geoestadístico Nacional',
      detalle: 'La cartografía oficial y el catálogo DIVIPOLA. Usar la fuente del Estado, y no una '
        + 'copia, es lo que permite que las cifras del mapa se puedan cruzar con cualquier otra '
        + 'estadística nacional.',
    },
    {
      ambito: 'Internacional',
      titulo: 'Marco de Estadísticas Culturales de la UNESCO (2009)',
      detalle: 'Ordena los dominios culturales y sus ciclos —creación, producción, difusión, '
        + 'transmisión—, que es lo que permite que «festival», «escuela» o «mercado» signifiquen lo '
        + 'mismo aquí que en una comparación internacional.',
    },
    {
      ambito: 'Internacional',
      titulo: 'Cartografía cultural (cultural mapping)',
      detalle: 'La práctica de registrar recursos culturales de un territorio con participación de '
        + 'quienes lo habitan, impulsada por la UNESCO como herramienta de política pública. Es la '
        + 'razón por la que este mapa se alimenta de registros declarados por las organizaciones y '
        + 'no de un inventario hecho desde el centro.',
    },
    {
      ambito: 'Diseño de la información',
      titulo: 'Escalas de color: ColorBrewer y la crítica al arcoíris',
      detalle: 'Las rampas secuenciales de un solo tono para intensidad, y una paleta cualitativa '
        + 'para categorías, siguen el criterio de ColorBrewer (Cynthia Brewer). El arcoíris se '
        + 'descarta a propósito: introduce fronteras donde el dato es continuo y se vuelve ilegible '
        + 'en escala de grises y para quien no distingue rojo y verde —el argumento que Borland y '
        + 'Taylor resumieron en «Rainbow Color Map (Still) Considered Harmful» (2007)—.',
    },
  ];
}
