import {
  ANCHO_MAXIMO,
  acercar,
  acotar,
  alcanzaParaElMarco,
  encajeInicial,
  franjaCentral,
  recortar,
  salidaDe,
  type Medida,
} from './recorte';

/*
  EL RECORTE CONTRA EL MARCO.

  Los marcos que se usan aquí no son inventados: son los que el navegador pinta hoy, medidos con
  Playwright sobre el sitio a 1440 px. La portada de página es la que duele —relación 4,44— y por
  eso aparece en casi todos los casos.

  Lo que estas pruebas defienden, en una frase: que el recorte de partida sea el que el sitio ya
  hacía solo, que no se pueda salir de la imagen, que NUNCA se pueda ampliar, y que la salida no
  invente píxeles que el origen no tiene.
*/
describe('recorte · la aritmética del marco', () => {
  // Medidos con el navegador sobre el sitio corriendo.
  const PORTADA_DE_PAGINA: Medida = { w: 1440, h: 324 };   // relación 4,44
  const HUELLA_Y_EVOLUCION: Medida = { w: 674, h: 273 };   // relación 2,47, la única fija
  const TARJETA: Medida = { w: 438, h: 318 };              // relación 1,38

  // Fotografías reales de la galería del sitio.
  const APAISADA: Medida = { w: 1600, h: 1070 };
  const VERTICAL: Medida = { w: 1369, h: 2048 };

  function relacion(natural: Medida, r: { w: number; h: number }): number {
    return (natural.w * r.w) / (natural.h * r.h);
  }

  describe('encajeInicial', () => {
    it('produce exactamente la relación del marco, venga la foto como venga', () => {
      for (const marco of [PORTADA_DE_PAGINA, HUELLA_Y_EVOLUCION, TARJETA]) {
        for (const natural of [APAISADA, VERTICAL, { w: 800, h: 800 }]) {
          const r = encajeInicial(natural, marco);
          expect(relacion(natural, r))
            .withContext(`marco ${marco.w}x${marco.h} sobre foto ${natural.w}x${natural.h}`)
            .toBeCloseTo(marco.w / marco.h, 4);
        }
      }
    });

    it('es el mismo encuadre que el sitio hace hoy solo: centrado y tocando dos bordes', () => {
      // `object-fit: cover` centra y llena. Abrir el editor y no tocar nada tiene que dejar la
      // imagen exactamente como estaba, o cada apertura movería el sitio sin que nadie lo pidiera.
      const anchoDeMas = encajeInicial(APAISADA, TARJETA);
      expect(anchoDeMas.h).toBe(1);
      expect(anchoDeMas.x).toBeCloseTo((1 - anchoDeMas.w) / 2, 6);

      const altoDeMas = encajeInicial(VERTICAL, TARJETA);
      expect(altoDeMas.w).toBe(1);
      expect(altoDeMas.y).toBeCloseTo((1 - altoDeMas.h) / 2, 6);
    });

    it('en la portada 4,44 sobre una foto vertical toma una franja del 12 % del alto', () => {
      // El caso que hace falta el recorte: una foto de 1369x2048 en un hueco de relación 4,44
      // conserva 308 px de alto de 2048. Sin elegir la franja, el sitio se queda con el centro,
      // que en una foto de una persona es la cintura.
      const r = encajeInicial(VERTICAL, PORTADA_DE_PAGINA);
      expect(r.w).toBe(1);
      expect(r.h).toBeCloseTo(0.1504, 3);
      expect(Math.round(VERTICAL.h * r.h)).toBe(308);
    });
  });

  describe('acotar', () => {
    it('nunca deja el recorte fuera de la imagen', () => {
      expect(acotar({ x: -0.5, y: -0.5, w: 0.4, h: 0.4 })).toEqual({ x: 0, y: 0, w: 0.4, h: 0.4 });
      expect(acotar({ x: 0.9, y: 0.95, w: 0.4, h: 0.3 }))
        .toEqual({ x: 0.6, y: 0.7, w: 0.4, h: 0.3 });
    });

    it('conserva el tamaño al toparse con el borde, no lo deforma', () => {
      // Es lo que hace que arrastrar se sienta bien: el recorte se PARA, no se encoge. Si se
      // encogiera, la relación cambiaría y el recorte dejaría de casar con el marco.
      const r = acotar({ x: -0.3, y: 0.2, w: 0.5, h: 0.5 });
      expect(r.w).toBe(0.5);
      expect(r.h).toBe(0.5);
    });
  });

  describe('acercar', () => {
    it('no permite alejarse más allá del encaje: la imagen nunca se amplía', () => {
      // ESTA ES LA PRUEBA DE EL CRITERIO: «más pequeño que el marco» deja de ser un
      // aviso y pasa a ser imposible. El caso vivo era home_hero_4, que pedía 687 px de ancho para
      // un hueco de 1670 y se ampliaba 2,43 veces sin que nada avisara.
      const minimo = encajeInicial(APAISADA, TARJETA);
      const alejado = acercar(minimo, 0.25, { x: 0.5, y: 0.5 }, minimo);

      expect(alejado.w).toBeCloseTo(minimo.w, 6);
      expect(alejado.h).toBeCloseTo(minimo.h, 6);
    });

    it('acercar reduce el recorte a la mitad', () => {
      const minimo = encajeInicial(APAISADA, TARJETA);
      const cerca = acercar(minimo, 2, { x: 0.5, y: 0.5 }, minimo);

      expect(cerca.w).toBeCloseTo(minimo.w / 2, 6);
      expect(cerca.h).toBeCloseTo(minimo.h / 2, 6);
    });

    it('acercar hacia un punto que NO es el centro lo deja donde estaba', () => {
      // ESTE CASO LO AÑADIÓ UN MUTANTE QUE SOBREVIVÍA. La prueba anterior acercaba hacia el
      // centro, y un acercamiento que ignora el punto y salta siempre al centro daba exactamente
      // el mismo resultado: el mutante pasaba las veintiuna en verde.
      //
      // Con un punto descentrado los dos se separan, y es además lo que la persona hace de
      // verdad: acerca sobre la cara que quiere conservar, no sobre el centro geométrico.
      const minimo = encajeInicial(APAISADA, TARJETA);
      const punto = { x: 0.30, y: 0.25 };
      const cerca = acercar(minimo, 2, punto, minimo);

      // El punto señalado ocupa la misma posición relativa dentro del recorte que antes.
      const antes = { x: (punto.x - minimo.x) / minimo.w, y: (punto.y - minimo.y) / minimo.h };
      const despues = { x: (punto.x - cerca.x) / cerca.w, y: (punto.y - cerca.y) / cerca.h };

      expect(despues.x).toBeCloseTo(antes.x, 5);
      expect(despues.y).toBeCloseTo(antes.y, 5);

      // Y no es el centro: si lo fuera, este caso no distinguiría nada.
      expect(cerca.x + cerca.w / 2).not.toBeCloseTo(0.5, 3);
    });

    it('mantiene la relación del marco al acercar', () => {
      // Si el acercamiento deformara el recorte, el archivo saldría con otra relación y el sitio
      // volvería a recortarlo por su cuenta: dos recortes encadenados y nadie eligió el segundo.
      const minimo = encajeInicial(VERTICAL, PORTADA_DE_PAGINA);
      const cerca = acercar(minimo, 3, { x: 0.3, y: 0.4 }, minimo);

      expect(relacion(VERTICAL, cerca)).toBeCloseTo(relacion(VERTICAL, minimo), 4);
    });
  });

  describe('salidaDe', () => {
    it('pide el doble del hueco cuando la imagen da para tanto', () => {
      const r = encajeInicial(APAISADA, TARJETA);
      // 438 x 2 = 876, y la foto tiene 1600 de ancho: alcanza.
      expect(salidaDe(TARJETA, APAISADA, r)).toEqual({ w: 876, h: 636 });
    });

    it('NO amplía: si el origen tiene menos, la salida tiene menos', () => {
      // Es el defecto que evita escribir un archivo de 2880 px a partir de uno de 687, que pesa
      // cuatro veces más y no aporta un solo píxel de detalle.
      const pequena: Medida = { w: 687, h: 1030 };
      const r = encajeInicial(pequena, TARJETA);
      const salida = salidaDe(TARJETA, pequena, r);

      expect(salida.w).toBeLessThanOrEqual(Math.round(pequena.w * r.w));
      expect(salida.w).toBeLessThan(TARJETA.w * 2);
    });

    it('respeta el techo de ancho del sitio', () => {
      const enorme: Medida = { w: 9000, h: 6000 };
      const marcoAncho: Medida = { w: 4000, h: 900 };
      const r = encajeInicial(enorme, marcoAncho);

      expect(salidaDe(marcoAncho, enorme, r).w).toBe(ANCHO_MAXIMO);
    });

    it('la salida conserva la relación del marco', () => {
      for (const marco of [PORTADA_DE_PAGINA, HUELLA_Y_EVOLUCION, TARJETA]) {
        const r = encajeInicial(APAISADA, marco);
        const s = salidaDe(marco, APAISADA, r);
        expect(s.w / s.h).withContext(`marco ${marco.w}x${marco.h}`).toBeCloseTo(marco.w / marco.h, 2);
      }
    });
  });

  describe('alcanzaParaElMarco', () => {
    it('rechaza el caso real que llevaba meses en el árbol', () => {
      // home_hero_4 pide a unsplash `w=687` para un hueco medido de 1670x1044.
      expect(alcanzaParaElMarco({ w: 687, h: 1030 }, { w: 1670, h: 1044 })).toBeFalse();
    });

    it('acepta una foto de la galería para cualquiera de los marcos del sitio', () => {
      for (const marco of [PORTADA_DE_PAGINA, HUELLA_Y_EVOLUCION, TARJETA]) {
        expect(alcanzaParaElMarco(APAISADA, marco))
          .withContext(`${marco.w}x${marco.h}`).toBeTrue();
      }
    });

    it('una foto vertical de la galería NO alcanza para la portada de página', () => {
      // ESTA PRUEBA SE ESCRIBIÓ AL REVÉS Y LA CORRIGIÓ EL CÓDIGO. La idea era «pierde el 85 % del
      // alto pero alcanza». Es falso, y por el ANCHO: la foto tiene 1369 px y el hueco 1440. Da
      // igual cuánto alto sobre.
      //
      // Es un resultado útil, no un estorbo: `1757624083659.jpg` —la foto más pesada de la
      // portada, 725 KB— es justamente de 1369x2048, y hoy alimenta una tarjeta. Con el bloqueo
      // puesto, nadie puede llevarla a una portada de página donde saldría ampliada.
      expect(VERTICAL.w).toBeLessThan(PORTADA_DE_PAGINA.w);
      expect(alcanzaParaElMarco(VERTICAL, PORTADA_DE_PAGINA)).toBeFalse();

      // Y sí alcanza para la tarjeta, que es el hueco donde el sitio la usa.
      expect(alcanzaParaElMarco(VERTICAL, TARJETA)).toBeTrue();
    });
  });

  describe('franjaCentral', () => {
    it('los hitos plegados ven el 16,5 % del ancho', () => {
      // 0,29 / 1,76. Es el número que decide que mande el estado abierto: recortando a 1,76 el
      // plegado toma una franja estrecha pero con resolución de sobra; al revés se tira para
      // siempre el contenido que el abierto necesita.
      expect(franjaCentral(1.76, 0.29)).toBeCloseTo(0.165, 3);
    });

    it('cuando las dos relaciones coinciden la franja es todo el ancho', () => {
      expect(franjaCentral(1.78, 1.78)).toBe(1);
    });

    it('nunca pasa del ancho completo', () => {
      expect(franjaCentral(0.5, 2)).toBe(1);
    });
  });

  describe('recortar · lo único que toca píxeles', () => {
    /** Un lienzo con dos mitades de color plano, para poder afirmar QUÉ trozo salió. */
    function fuente(w: number, h: number): HTMLCanvasElement {
      const c = document.createElement('canvas');
      c.width = w; c.height = h;
      const x = c.getContext('2d')!;
      x.fillStyle = '#ff0000';
      x.fillRect(0, 0, w / 2, h);
      x.fillStyle = '#0000ff';
      x.fillRect(w / 2, 0, w / 2, h);
      return c;
    }

    it('devuelve un WebP con las medidas pedidas', async () => {
      const c = fuente(800, 600);
      const bitmap = await createImageBitmap(c);
      const blob = await recortar(bitmap, { x: 0, y: 0, w: 1, h: 0.5 }, { w: 400, h: 150 });

      expect(blob).not.toBeNull();
      expect(blob!.type).toBe('image/webp');

      const salida = await createImageBitmap(blob!);
      expect(salida.width).toBe(400);
      expect(salida.height).toBe(150);
    });

    it('recorta el trozo que se le pide y no otro', async () => {
      // La mitad izquierda es roja y la derecha azul. Pidiendo solo la derecha, el resultado no
      // puede tener rojo. Sin esta afirmación, un recortador que ignorara el rectángulo y
      // dibujara la imagen entera pasaría la prueba de medidas de arriba.
      const c = fuente(800, 600);
      const bitmap = await createImageBitmap(c);
      const blob = await recortar(bitmap, { x: 0.5, y: 0, w: 0.5, h: 1 }, { w: 100, h: 150 });

      const salida = await createImageBitmap(blob!);
      const lienzo = document.createElement('canvas');
      lienzo.width = salida.width; lienzo.height = salida.height;
      const cx = lienzo.getContext('2d')!;
      cx.drawImage(salida, 0, 0);
      const centro = cx.getImageData(salida.width / 2, salida.height / 2, 1, 1).data;

      expect(centro[2]).withContext('el azul de la mitad derecha').toBeGreaterThan(180);
      expect(centro[0]).withContext('no debería quedar rojo').toBeLessThan(70);
    });

    it('en gris no queda color', async () => {
      // Las ocho ranuras que el sitio pinta con `grayscale(1)` se guardan ya desaturadas: pesa un
      // 56 % menos y el visitante ve lo mismo, porque el CSS lo iba a desaturar igual.
      const c = fuente(400, 400);
      const bitmap = await createImageBitmap(c);
      const blob = await recortar(bitmap, { x: 0, y: 0, w: 0.4, h: 1 }, { w: 80, h: 200 }, { grises: true });

      const salida = await createImageBitmap(blob!);
      const lienzo = document.createElement('canvas');
      lienzo.width = salida.width; lienzo.height = salida.height;
      const cx = lienzo.getContext('2d')!;
      cx.drawImage(salida, 0, 0);
      const p = cx.getImageData(salida.width / 2, salida.height / 2, 1, 1).data;

      // Gris significa que los tres canales coinciden. Se admite holgura por la codificación.
      expect(Math.abs(p[0] - p[1])).toBeLessThan(14);
      expect(Math.abs(p[1] - p[2])).toBeLessThan(14);
    });
  });
});
