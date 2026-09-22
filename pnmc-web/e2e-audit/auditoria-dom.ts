/**
 * Rutina de auditoria que se ejecuta DENTRO de la pagina.
 *
 * Todo lo que devuelve esta medido sobre el DOM renderizado, no deducido de
 * las clases del codigo fuente. Esa es la diferencia entre esta pasada y la
 * lectura estatica previa.
 */
export const AUDITORIA_DOM = () => {
  const parse = (c: string): number[] | null => {
    const m = c.match(/rgba?\(([^)]+)\)/);
    if (!m) return null;
    const p = m[1].split(',').map((s) => parseFloat(s.trim()));
    return [p[0], p[1], p[2], p.length > 3 ? p[3] : 1];
  };

  const mezclar = (frente: number[], fondo: number[]): number[] => {
    const a = frente[3];
    return [
      frente[0] * a + fondo[0] * (1 - a),
      frente[1] * a + fondo[1] * (1 - a),
      frente[2] * a + fondo[2] * (1 - a),
      1,
    ];
  };

  const luminancia = (c: number[]): number => {
    const f = (v: number) => {
      const s = v / 255;
      return s <= 0.03928 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4);
    };
    return 0.2126 * f(c[0]) + 0.7152 * f(c[1]) + 0.0722 * f(c[2]);
  };

  const ratio = (a: number[], b: number[]): number => {
    const l1 = luminancia(a);
    const l2 = luminancia(b);
    return (Math.max(l1, l2) + 0.05) / (Math.min(l1, l2) + 0.05);
  };

  // CORRECCION 1 — la barra de depuracion del entorno de desarrollo no es la
  // aplicacion. La primera version del instrumento se midio a si misma: los
  // "3 elementos recortados en todas las rutas y todos los tamanos" eran esta
  // barra. No existe en produccion (fileReplacements de angular.json).
  const esInstrumental = (el: Element): boolean =>
    !!el.closest('[class*="agentation"], [id*="agentation"], .pnmc-agentation-toolbar');

  // CORRECCION 2 — si hay un gradiente o una foto en la cadena de ancestros,
  // el color de fondo es INDETERMINADO. La primera version seguia subiendo
  // hasta el fondo de la pagina y reportaba "blanco sobre blanco 1:1" sobre
  // textos que en realidad se leen a 16:1 encima de una foto oscurecida.
  const fondoEfectivo = (
    el: Element,
  ): { color: number[]; indeterminado: boolean } => {
    let acumulado: number[] | null = null;
    let nodo: Element | null = el;
    while (nodo) {
      const cs = getComputedStyle(nodo);
      const bg = parse(cs.backgroundColor);
      const opaco = !!bg && bg[3] >= 1;
      if (cs.backgroundImage && cs.backgroundImage !== 'none' && !opaco) {
        return { color: acumulado || [255, 255, 255, 1], indeterminado: true };
      }
      if (bg && bg[3] > 0) {
        acumulado = acumulado ? mezclar(acumulado, bg) : bg;
        if (bg[3] >= 1) return { color: acumulado, indeterminado: false };
      }
      nodo = nodo.parentElement;
    }
    return { color: acumulado || [255, 255, 255, 1], indeterminado: false };
  };

  // CORRECCION 5 — el fondo tambien puede ser un <img> hermano en posicion
  // absoluta, no solo un `background-image`. Es el patron de las tarjetas con
  // fotografia: <img absolute inset-0> + capas de degradado + texto encima.
  // Sin esto el instrumento seguia informando "blanco sobre blanco 1:1" de un
  // titulo que en realidad se lee a 16:1 sobre una foto oscurecida.
  const cubiertoPorImagen = (el: Element): boolean => {
    const r = el.getBoundingClientRect();
    const cubre = (c: Element): boolean => {
      const rc = c.getBoundingClientRect();
      return rc.left <= r.left + 1 && rc.right >= r.right - 1 && rc.top <= r.top + 1 && rc.bottom >= r.bottom - 1;
    };

    // Se sube por TODOS los ancestros, no solo hasta el primero posicionado:
    // en el patron de tarjeta con fotografia, el <img> es HERMANO del div que
    // contiene el texto, no descendiente suyo. Detenerse antes era justo lo
    // que dejaba pasar el falso "blanco sobre blanco".
    let nodo: Element | null = el.parentElement;
    let saltos = 0;
    while (nodo && saltos < 8) {
      for (const capa of Array.from(nodo.querySelectorAll('img, picture, video, canvas'))) {
        if (capa.contains(el)) continue;
        if (cubre(capa)) return true;
      }
      for (const c of Array.from(nodo.querySelectorAll('*'))) {
        if (c === el || c.contains(el)) continue;
        const ccs = getComputedStyle(c);
        if (!ccs.backgroundImage || ccs.backgroundImage === 'none') continue;
        if (cubre(c)) return true;
      }
      nodo = nodo.parentElement;
      saltos++;
    }
    return false;
  };

  // CORRECCION 6 — lo marcado como decorativo con aria-hidden esta exento del
  // criterio de contraste 1.4.3. Antes se contaba la marca de agua gigante de
  // las cabeceras de seccion como si fuera contenido.
  const esDecorativo = (el: Element): boolean => !!el.closest('[aria-hidden="true"]');

  // CORRECCION 3 — "visible" tiene que significar visible PARA UNA PERSONA.
  // Un texto dentro de un panel con max-height:0, o dentro de una capa que
  // solo se opacifica al pasar el cursor, tiene rect no nulo y sin embargo
  // nadie lo ve en reposo.
  const visible = (el: Element): boolean => {
    const r = el.getBoundingClientRect();
    if (r.width === 0 || r.height === 0) return false;
    if (esInstrumental(el)) return false;

    let nodo: Element | null = el;
    let saltos = 0;
    while (nodo && saltos < 12) {
      const cs = getComputedStyle(nodo);
      if (cs.visibility === 'hidden' || cs.display === 'none') return false;
      if (parseFloat(cs.opacity) <= 0.05) return false;
      // panel colapsado (patron group-hover:max-h-*)
      if (nodo !== el) {
        const mh = parseFloat(cs.maxHeight);
        if (!isNaN(mh) && mh <= 0) return false;
        const rr = nodo.getBoundingClientRect();
        if (rr.height <= 0 || rr.width <= 0) return false;
      }
      nodo = nodo.parentElement;
      saltos++;
    }
    return true;
  };

  // CORRECCION 4 — la opacidad acumulada de los ancestros afecta al color del
  // texto. El instrumento leia el `color` computado e ignoraba un `opacity-50`
  // en el propio div, subestimando el contraste real.
  const opacidadAcumulada = (el: Element): number => {
    let o = 1;
    let nodo: Element | null = el;
    let saltos = 0;
    while (nodo && saltos < 12) {
      const v = parseFloat(getComputedStyle(nodo).opacity);
      if (!isNaN(v)) o *= v;
      nodo = nodo.parentElement;
      saltos++;
    }
    return o;
  };

  const ruta = (el: Element): string => {
    const partes: string[] = [];
    let n: Element | null = el;
    let saltos = 0;
    while (n && saltos < 4) {
      let s = n.tagName.toLowerCase();
      if (n.id) {
        partes.unshift(s + '#' + n.id);
        break;
      }
      const cls = (n.getAttribute('class') || '')
        .split(/\s+/)
        .filter(Boolean)
        .slice(0, 2)
        .join('.');
      if (cls) s += '.' + cls;
      partes.unshift(s);
      n = n.parentElement;
      saltos++;
    }
    return partes.join(' > ');
  };

  // 1. Desborde horizontal: la prueba objetiva de "no cabe en este dispositivo"
  const anchoVentana = window.innerWidth;
  const desborde = {
    hay: document.documentElement.scrollWidth > anchoVentana + 1,
    scrollWidth: document.documentElement.scrollWidth,
    innerWidth: anchoVentana,
    exceso: document.documentElement.scrollWidth - anchoVentana,
    culpables: [] as Array<{ selector: string; ancho: number; derecha: number }>,
  };
  if (desborde.hay) {
    const vistos = new Set<string>();
    document.querySelectorAll('body *').forEach((el) => {
      if (!visible(el)) return;
      const r = el.getBoundingClientRect();
      if (r.right > anchoVentana + 1 || r.width > anchoVentana + 1) {
        const k = ruta(el);
        if (vistos.has(k)) return;
        vistos.add(k);
        desborde.culpables.push({
          selector: k,
          ancho: Math.round(r.width),
          derecha: Math.round(r.right),
        });
      }
    });
    desborde.culpables = desborde.culpables.slice(0, 12);
  }

  // 1-bis. RECORTE: el fallo que el desborde no ve.
  // Si un ancestro lleva overflow:hidden, el contenido que se sale no produce
  // scrollWidth mayor: simplemente queda cortado e inalcanzable. Es peor que
  // desbordar, porque no hay barra de desplazamiento que lo rescate.
  // CORRECCION 7 — un cajon de navegacion CERRADO no es contenido perdido.
  // El patron es un contenedor `position: fixed` desplazado fuera de la ventana
  // con `transform`, que se abre con un boton. Sus hijos caen en coordenadas
  // negativas y el detector los contaba como recortados: en el panel
  // administrativo daba 15 falsos positivos por pantalla, que eran la propia
  // barra lateral esperando a que la abrieran.
  const enCajonCerrado = (el: Element): boolean => {
    let nodo: Element | null = el;
    let saltos = 0;
    while (nodo && saltos < 10) {
      const cs = getComputedStyle(nodo) as CSSStyleDeclaration & { translate?: string };
      // Tailwind v4 emite `-translate-x-full` con la propiedad CSS `translate`,
      // no con `transform`. Mirar solo `transform` dejaba el cajon sin detectar.
      const desplazado =
        (cs.transform && cs.transform !== 'none') ||
        (!!cs.translate && cs.translate !== 'none' && cs.translate !== '');
      if (cs.position === 'fixed' && desplazado) {
        const r = nodo.getBoundingClientRect();
        const fuera = r.right <= 1 || r.left >= window.innerWidth - 1 || r.bottom <= 1 || r.top >= window.innerHeight - 1;
        if (fuera) return true;
      }
      nodo = nodo.parentElement;
      saltos++;
    }
    return false;
  };

  const recorte = {
    hay: false,
    paginaNoDesplazable:
      document.documentElement.scrollHeight <= window.innerHeight + 1 &&
      document.body.scrollHeight <= window.innerHeight + 1,
    elementos: [] as Array<{ selector: string; ancho: number; derecha: number; recortadoPor: string }>,
  };
  {
    const vistos = new Set<string>();
    document.querySelectorAll('body *').forEach((el) => {
      if (!visible(el)) return;
      const r = el.getBoundingClientRect();
      if (r.width < 8 || r.height < 8) return;
      if (esInstrumental(el)) return;
      if (enCajonCerrado(el)) return;
      if (r.right <= anchoVentana + 1 && r.left >= -1) return;
      // CORRECCION 8 — hay que parar en el PRIMER ancestro que gestione el
      // desbordamiento, sea cual sea. La version anterior saltaba por encima de
      // los contenedores con `overflow-x: auto` y seguia subiendo hasta el
      // `overflow-hidden` de la raiz, informando como "cortada" una tabla que
      // en realidad se desplaza dentro de su propio contenedor. Es la
      // diferencia entre contenido PERDIDO y contenido ALCANZABLE.
      let p: Element | null = el.parentElement;
      let recortadoPor = '';
      while (p) {
        const cs = getComputedStyle(p);
        const ox = cs.overflowX;
        if (ox === 'auto' || ox === 'scroll') return; // alcanzable desplazando
        if (ox === 'hidden' || ox === 'clip') {
          recortadoPor = ruta(p);
          break;
        }
        p = p.parentElement;
      }
      if (!recortadoPor) return;
      const k = ruta(el);
      if (vistos.has(k)) return;
      vistos.add(k);
      recorte.hay = true;
      recorte.elementos.push({
        selector: k,
        ancho: Math.round(r.width),
        derecha: Math.round(r.right),
        recortadoPor,
      });
    });
    recorte.elementos = recorte.elementos.slice(0, 15);
  }

  // 2. Encabezados y landmarks
  const enc: Record<string, unknown> = {};
  ['h1', 'h2', 'h3', 'h4', 'h5', 'h6'].forEach((t) => {
    enc[t] = Array.from(document.querySelectorAll(t)).filter(visible).length;
  });
  enc['textosH1'] = Array.from(document.querySelectorAll('h1'))
    .filter(visible)
    .map((h) => (h.textContent || '').trim().slice(0, 60));

  const mains = Array.from(document.querySelectorAll('main, [role="main"]'));
  const landmarks = {
    main: mains.length,
    mainAnidados: mains.filter((m) => m.parentElement && m.parentElement.closest('main, [role="main"]')).length,
    enlaceDeSalto: !!document.querySelector('a[href="#main"], a[href="#content"], .skip-link, a[class*="skip"]'),
    ariaLive: document.querySelectorAll('[aria-live], [role="status"], [role="alert"]').length,
  };

  // 3. Contraste medido sobre el DOM
  const fallos: Array<Record<string, unknown>> = [];
  const indeterminados: Array<Record<string, unknown>> = [];
  const vistosC = new Set<string>();
  document
    .querySelectorAll('p, span, a, button, li, h1, h2, h3, h4, h5, h6, label, small, div, td, th')
    .forEach((el) => {
      const propio = Array.from(el.childNodes)
        .filter((n) => n.nodeType === 3)
        .map((n) => (n.textContent || '').trim())
        .join(' ')
        .trim();
      if (propio.length < 2) return;
      if (!visible(el)) return;
      if (esDecorativo(el)) return;

      const cs = getComputedStyle(el);
      const col = parse(cs.color);
      if (!col) return;
      const bruto = fondoEfectivo(el);
      const bg = bruto.color;
      const indeterminado = bruto.indeterminado || cubiertoPorImagen(el);
      // Sobre gradiente o foto no se puede afirmar nada desde el DOM: se
      // registra aparte, nunca como fallo. Es la correccion que retira los
      // falsos "blanco sobre blanco".
      if (indeterminado) {
        indeterminados.push({ selector: ruta(el), texto: propio.slice(0, 45), color: cs.color });
        return;
      }

      const alfaTotal = col[3] * opacidadAcumulada(el);
      const colorFinal = alfaTotal < 1 ? mezclar([col[0], col[1], col[2], alfaTotal], bg) : col;
      const rr = ratio(colorFinal, bg);

      const px = parseFloat(cs.fontSize);
      const peso = parseInt(cs.fontWeight, 10) || 400;
      const grande = px >= 24 || (px >= 18.66 && peso >= 700);
      const minimo = grande ? 3 : 4.5;
      if (rr >= minimo) return;

      const k = ruta(el) + '|' + cs.color + '|' + Math.round(rr * 100);
      if (vistosC.has(k)) return;
      vistosC.add(k);
      fallos.push({
        selector: ruta(el),
        texto: propio.slice(0, 45),
        color: cs.color,
        opacidadAcumulada: Math.round(opacidadAcumulada(el) * 100) / 100,
        fondo: 'rgb(' + Math.round(bg[0]) + ', ' + Math.round(bg[1]) + ', ' + Math.round(bg[2]) + ')',
        px: Math.round(px * 10) / 10,
        ratio: Math.round(rr * 100) / 100,
        minimo,
      });
    });

  // 4. Objetivos tactiles (WCAG 2.5.8) y nombres accesibles
  const tactiles: Array<Record<string, unknown>> = [];
  const sinNombre: Array<Record<string, unknown>> = [];
  let totalInteractivos = 0;
  document
    .querySelectorAll('a[href], button, input, select, textarea, [role="button"], [role="link"], [tabindex="0"]')
    .forEach((el) => {
      if (!visible(el)) return;
      totalInteractivos++;
      const r = el.getBoundingClientRect();
      if (r.width < 24 || r.height < 24) {
        tactiles.push({
          selector: ruta(el),
          ancho: Math.round(r.width),
          alto: Math.round(r.height),
          texto: (el.textContent || '').trim().slice(0, 30),
        });
      }
      const img = el.querySelector('img') as HTMLImageElement | null;
      const nombre =
        (el.textContent || '').trim() ||
        el.getAttribute('aria-label') ||
        el.getAttribute('title') ||
        (img ? img.alt : '') ||
        (el.getAttribute('aria-labelledby') ? 'via-labelledby' : '');
      if (!nombre) sinNombre.push({ selector: ruta(el), tag: el.tagName.toLowerCase() });
    });

  // 5. Inventario de controles
  const enlaces = Array.from(document.querySelectorAll('a[href]'));
  const controles = {
    botones: document.querySelectorAll('button').length,
    enlaces: enlaces.length,
    enlacesInternos: enlaces.filter((a) => {
      const h = a.getAttribute('href') || '';
      return h.startsWith('/') || h.startsWith(location.origin);
    }).length,
    rolBoton: document.querySelectorAll('[role="button"]').length,
    rolEnlace: document.querySelectorAll('[role="link"]').length,
    interactivosVisibles: totalInteractivos,
    imagenes: document.querySelectorAll('img').length,
    imagenesSinDimension: Array.from(document.querySelectorAll('img')).filter(
      (i) => !i.getAttribute('width') || !i.getAttribute('height'),
    ).length,
    imagenesSinAlt: Array.from(document.querySelectorAll('img')).filter(
      (i) => i.getAttribute('alt') === null,
    ).length,
  };

  return {
    titulo: document.title,
    lang: document.documentElement.lang,
    alturaPagina: document.documentElement.scrollHeight,
    desborde,
    recorte,
    encabezados: enc,
    landmarks,
    contraste: {
      fallos: fallos.length,
      indeterminados: indeterminados.length,
      peores: fallos.sort((a, b) => (a['ratio'] as number) - (b['ratio'] as number)).slice(0, 15),
    },
    tactiles: { total: totalInteractivos, pequenos: tactiles.length, ejemplos: tactiles.slice(0, 10) },
    sinNombreAccesible: { total: sinNombre.length, ejemplos: sinNombre.slice(0, 10) },
    controles,
  };
};
