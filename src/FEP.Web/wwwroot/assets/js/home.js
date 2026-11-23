/**
 * Home FEP: carrusel del hero, noticias, agenda de eventos y formación, avisos.
 * Requiere assets/js/comun.js (cabecera, menú, pie y utilidades en window.FEP).
 * Todo texto que llega de la API se pinta con textContent / atributos, nunca con innerHTML.
 *
 * Secciones ↔ endpoints
 *   "Lo que nos mueve" + slides del hero → GET /api/v1/noticias/destacadas
 *   "La próxima cita está aquí."         → GET /api/v1/eventos/destacados + /api/v1/formacion/convocatorias
 *   Tarjeta "Formación" (contador)       → GET /api/v1/formacion/convocatorias
 *   Franja avisos                        → GET /api/v1/avisos
 */
(function () {
'use strict';

const { $, $$, el, attrsEnlace, img, textoPlano, aFecha, bloqueFecha, fmtLargo, fmtCorto, reduceMotion, cargarLista } = window.FEP;
const INSCRIPCIONES_FORMACION = 'https://www.padelfederacion.es/Formacion_Inscripciones.asp';

/* ===========================================================================
   Hero: carrusel (foto fija del Figma + fotos de noticias destacadas)
   =========================================================================== */
const hero = {
  slides: [], actual: 0, timer: null, pausado: reduceMotion.matches, hover: false, INTERVALO: 7000,
};

const MAX_DIAPOSITIVAS = 2;

function initHero(noticias) {
  const cont = $('#hero-slides');
  // Diapositivas fijas del HTML (con pie enlazado si llevan data-titulo y data-enlace)
  hero.slides = $$('.hero__slide', cont).map((nodo) => {
    const { titulo, enlace } = nodo.dataset;
    return { nodo, titulo: titulo || null, ...(enlace ? attrsEnlace(enlace) : { href: null }) };
  });
  const fijas = new Set(hero.slides.map((s) => s.href).filter(Boolean));

  // Si hay hueco (menos de MAX_DIAPOSITIVAS fijas), se completa con noticias: las 2 primeras van a
  // "Lo que nos mueve" y las 2 siguientes (o, si no hay, esas mismas) al hero, sin repetir una noticia
  // que ya tenga diapositiva fija. Hoy hay 2 fijas y el máximo es 2: no se añade ninguna.
  const libres = Math.max(0, MAX_DIAPOSITIVAS - hero.slides.length);
  const candidatas = (noticias.length > 2 ? noticias.slice(2, 4) : noticias.slice(0, 2))
    .filter((n) => n.imagenUrl && !fijas.has(attrsEnlace(n.enlace, n.ventana).href))
    .slice(0, libres);
  const precargas = candidatas.map((n) => new Promise((ok) => {
    const im = new Image();
    im.onload = () => ok(n);
    im.onerror = () => ok(null);
    im.src = n.imagenUrl;
  }));

  Promise.all(precargas).then((cargadas) => {
    for (const n of cargadas.filter(Boolean)) {
      // Las fotos de noticias son pequeñas (650×434): van enmarcadas para no pixelarse.
      const fig = el('figure', { class: 'hero__slide hero__slide--news hero__slide--marco', 'aria-roledescription': 'diapositiva' },
        el('img', { class: 'hero__fondo', src: n.imagenUrl, alt: '', decoding: 'async' }),
        el('span', { class: 'hero__marco' }, el('img', { src: n.imagenUrl, alt: '', decoding: 'async' })));
      cont.append(fig);
      hero.slides.push({ nodo: fig, titulo: n.titulo, ...attrsEnlace(n.enlace, n.ventana) });
    }
    construirIndicadores();
  });
}

function construirIndicadores() {
  const dots = $('#hero-dots');
  const pausa = $('#hero-pause');
  const total = hero.slides.length;
  dots.replaceChildren();
  hero.slides.forEach((s, i) => {
    s.nodo.setAttribute('aria-label', `${i + 1} de ${total}`);
    if (total < 2) return;
    dots.append(el('button', {
      type: 'button', class: `hero__dot${i === hero.actual ? ' is-active' : ''}`,
      'aria-label': `Diapositiva ${i + 1}${s.titulo ? `: ${s.titulo}` : ''}`,
      'aria-current': i === hero.actual ? 'true' : null,
      onclick: () => { irA(i); reiniciar(); },
    }));
  });
  irA(hero.actual);   // estado inicial: pie enlazado de la primera diapositiva (antes solo se pintaba al cambiar)
  if (total < 2) return;

  pausa.hidden = false;
  actualizarPausa();
  pausa.addEventListener('click', () => { hero.pausado = !hero.pausado; actualizarPausa(); reiniciar(); });
  const raiz = $('.hero');
  raiz.addEventListener('mouseenter', () => { hero.hover = true; reiniciar(); });
  raiz.addEventListener('mouseleave', () => { hero.hover = false; reiniciar(); });
  raiz.addEventListener('focusin', () => { hero.hover = true; reiniciar(); });
  raiz.addEventListener('focusout', (e) => { if (!raiz.contains(e.relatedTarget)) { hero.hover = false; reiniciar(); } });
  document.addEventListener('visibilitychange', reiniciar);
  reiniciar();
}

function actualizarPausa() {
  const pausa = $('#hero-pause');
  pausa.setAttribute('aria-pressed', String(hero.pausado));
  $('.sr-only', pausa).textContent = hero.pausado ? 'Reanudar carrusel' : 'Pausar carrusel';
}

function irA(i) {
  const total = hero.slides.length;
  hero.actual = (i + total) % total;
  hero.slides.forEach((s, j) => {
    const activo = j === hero.actual;
    s.nodo.classList.toggle('is-active', activo);
    s.nodo.setAttribute('aria-hidden', String(!activo));
  });
  $$('#hero-dots .hero__dot').forEach((d, j) => {
    d.classList.toggle('is-active', j === hero.actual);
    if (j === hero.actual) d.setAttribute('aria-current', 'true'); else d.removeAttribute('aria-current');
  });
  const cap = $('#hero-caption');
  const s = hero.slides[hero.actual];
  if (s.titulo && s.href && s.href !== '#') {
    cap.textContent = `${s.titulo} ↗`;
    cap.href = s.href;
    if (s.target) { cap.target = s.target; cap.rel = s.rel; } else { cap.removeAttribute('target'); cap.removeAttribute('rel'); }
    cap.hidden = false;
  } else {
    cap.hidden = true;
  }
}

function reiniciar() {
  clearInterval(hero.timer);
  hero.timer = null;
  if (hero.slides.length < 2 || hero.pausado || hero.hover || document.hidden) return;
  hero.timer = setInterval(() => irA(hero.actual + 1), hero.INTERVALO);
}

/* ===========================================================================
   "Lo que nos mueve": carrusel de noticias destacadas, de 3 en 3
   (2 en tablet, 1 en móvil; lo marca la variable CSS --por-vista de .news__track).
   Usa scroll-snap nativo: se puede deslizar con el dedo/trackpad y el foco del teclado
   hace visible la tarjeta enfocada.
   =========================================================================== */
/** Id de la noticia: idNoticia de la API o, si no viene, el parámetro Idnoticia del enlace. */
function idNoticia(n) {
  if (Number.isFinite(n.idNoticia)) return n.idNoticia;
  const m = /[?&]idnoticia=(\d+)/i.exec(n.enlace ?? '');
  return m ? Number(m[1]) : null;
}
/** Orden "más reciente primero"; sin id se respeta el Orden de la portada. */
function porRecientes(a, b) {
  const ia = idNoticia(a);
  const ib = idNoticia(b);
  if (ia != null && ib != null && ia !== ib) return ib - ia;
  return (a.orden ?? 0) - (b.orden ?? 0);
}

function pintarNoticias(noticias) {
  const cont = $('#noticias-lista');
  cont.setAttribute('aria-busy', 'false');
  if (!noticias.length) {
    cont.replaceChildren();
    $('#noticias-vacio').hidden = false;
    $('#noticias-controles').hidden = true;
    return;
  }

  const total = noticias.length;
  cont.replaceChildren(...noticias.map((n, i) => {
    const attrs = attrsEnlace(n.enlace, n.ventana);
    return el('article', { class: 'news-card', role: 'group', 'aria-roledescription': 'noticia', 'aria-label': `${i + 1} de ${total}` },
      el('a', { ...attrs, tabindex: '-1', 'aria-hidden': 'true' },
        img(n.imagenUrl, '', { class: 'news-card__img', width: 644, height: 242 })),
      // La primera (la más reciente) se marca como "Última noticia". La API no trae sección/categoría:
      // el resto lleva la etiqueta genérica fiel al origen (Destacada='S').
      i === 0
        ? el('p', { class: 'tag-red tag-red--new', text: 'Última noticia' })
        : el('p', { class: 'tag-red', text: n.categoria || 'Destacada' }),
      el('h3', { class: 'news-card__title' }, el('a', attrs, n.titulo)),
      el('a', { ...attrs, class: 'news-card__more', 'aria-label': `Leer noticia: ${n.titulo}` },
        'Leer noticia ', el('span', { 'aria-hidden': 'true', text: '↗' })),
    );
  }));
  initSliderNoticias();
}

const sliderNoticias = { pagina: 0, timer: null, hover: false, iniciado: false, INTERVALO: 8000 };

/** Nº de tarjetas visibles a la vez (definido en CSS para cada punto de corte). */
const porVista = (track) => parseInt(getComputedStyle(track).getPropertyValue('--por-vista'), 10) || 1;
const paginasNoticias = (track) => Math.max(1, Math.ceil(track.children.length / porVista(track)));
/** Desplazamiento de una página: ancho de tarjeta + hueco, por tarjetas visibles. */
function pasoNoticias(track) {
  const tarjeta = track.firstElementChild;
  const gap = parseFloat(getComputedStyle(track).columnGap) || 0;
  return tarjeta ? (tarjeta.getBoundingClientRect().width + gap) * porVista(track) : track.clientWidth;
}
function paginaNoticiasActual(track) {
  const total = paginasNoticias(track);
  if (track.scrollLeft >= track.scrollWidth - track.clientWidth - 2) return total - 1; // al final
  return Math.min(total - 1, Math.round(track.scrollLeft / pasoNoticias(track)));
}

function irAPaginaNoticias(p) {
  const track = $('#noticias-lista');
  const total = paginasNoticias(track);
  const destino = ((p % total) + total) % total; // vuelve al principio tras la última página
  // El estado se actualiza ya, sin esperar al evento scroll (llega tarde o nunca si no hay animación).
  sliderNoticias.pagina = destino;
  marcarPuntoNoticias();
  track.scrollTo({ left: destino * pasoNoticias(track), behavior: reduceMotion.matches ? 'auto' : 'smooth' });
}

function marcarPuntoNoticias() {
  [...$('#noticias-dots').children].forEach((d, i) => {
    const activo = i === sliderNoticias.pagina;
    d.classList.toggle('is-active', activo);
    if (activo) d.setAttribute('aria-current', 'true'); else d.removeAttribute('aria-current');
  });
}

/** (Re)construye los puntos según las páginas actuales y marca la activa según el scroll real. */
function actualizarControlesNoticias() {
  const track = $('#noticias-lista');
  const total = paginasNoticias(track);
  const dots = $('#noticias-dots');
  $('#noticias-controles').hidden = total < 2;
  if (dots.children.length !== total) {
    dots.replaceChildren(...Array.from({ length: total }, (_, i) => el('button', {
      type: 'button', class: 'news-slider__dot', 'aria-label': `Página ${i + 1} de ${total}`,
      onclick: () => { irAPaginaNoticias(i); reiniciarSliderNoticias(); },
    })));
  }
  sliderNoticias.pagina = paginaNoticiasActual(track);
  marcarPuntoNoticias();
}

function reiniciarSliderNoticias() {
  clearInterval(sliderNoticias.timer);
  sliderNoticias.timer = null;
  const track = $('#noticias-lista');
  if (reduceMotion.matches || sliderNoticias.hover || document.hidden || paginasNoticias(track) < 2) return;
  sliderNoticias.timer = setInterval(() => irAPaginaNoticias(sliderNoticias.pagina + 1), sliderNoticias.INTERVALO);
}

function initSliderNoticias() {
  const track = $('#noticias-lista');
  actualizarControlesNoticias();
  reiniciarSliderNoticias();
  if (sliderNoticias.iniciado) return;
  sliderNoticias.iniciado = true;

  // Al deslizar con el dedo/trackpad: sincroniza el punto activo cuando el scroll se detiene.
  let espera = 0;
  track.addEventListener('scroll', () => {
    clearTimeout(espera);
    espera = setTimeout(actualizarControlesNoticias, 120);
  }, { passive: true });
  if ('ResizeObserver' in window) new ResizeObserver(actualizarControlesNoticias).observe(track);

  $('#noticias-prev').addEventListener('click', () => { irAPaginaNoticias(sliderNoticias.pagina - 1); reiniciarSliderNoticias(); });
  $('#noticias-next').addEventListener('click', () => { irAPaginaNoticias(sliderNoticias.pagina + 1); reiniciarSliderNoticias(); });

  // Se detiene con el ratón encima, con el foco dentro o con la pestaña oculta.
  const raiz = $('#noticias-slider');
  raiz.addEventListener('mouseenter', () => { sliderNoticias.hover = true; reiniciarSliderNoticias(); });
  raiz.addEventListener('mouseleave', () => { sliderNoticias.hover = false; reiniciarSliderNoticias(); });
  raiz.addEventListener('focusin', () => { sliderNoticias.hover = true; reiniciarSliderNoticias(); });
  raiz.addEventListener('focusout', (e) => { if (!raiz.contains(e.relatedTarget)) { sliderNoticias.hover = false; reiniciarSliderNoticias(); } });
  document.addEventListener('visibilitychange', reiniciarSliderNoticias);
}

/* ===========================================================================
   "La próxima cita está aquí." – eventos destacados + inscripciones abiertas de formación
   =========================================================================== */
const ETIQUETA_FORMACION = {
  monitor: 'Formación · Técnicos',
  foco: 'Formación · FOCO',
  juez: 'Formación · Jueces árbitros',
};
let citas = [];

function normalizaCitas(eventos, convocatorias) {
  const ev = eventos.map((e) => ({
    grupo: 'evento',
    etiqueta: e.categoria || 'Evento destacado',
    titulo: e.titulo,
    fecha: bloqueFecha(e.fechaInicio, e.fechaFin),
    lugar: [e.localidad, e.lugar].filter(Boolean).join(' · ') || textoPlano(e.texto),
    imagen: e.imagenUrl,
    enlace: attrsEnlace(e.enlace, e.ventana),
    cta: 'Ver competición',
    orden: e.orden ?? 0,
  }));
  const fo = convocatorias.map((c) => {
    const limite = aFecha(c.fechaLimiteInscripcion);
    return {
      grupo: `formacion-${c.categoria}`,
      etiqueta: ETIQUETA_FORMACION[c.categoria] || 'Formación',
      titulo: c.titulo,
      fecha: bloqueFecha(c.fechaInicio, c.fechaFin),
      lugar: [...new Set([c.localidad, c.lugar].filter(Boolean))].join(' · '),
      extra: limite ? `Inscripción hasta el ${fmtCorto.format(limite)}` : '',
      imagen: null,
      enlace: { href: INSCRIPCIONES_FORMACION },
      cta: 'Inscribirse',
      inicio: aFecha(c.fechaInicio)?.getTime() ?? Infinity,
    };
  }).sort((a, b) => a.inicio - b.inicio);
  return [...ev.sort((a, b) => a.orden - b.orden), ...fo];
}

/** Imagen de las convocatorias (la BD no la trae; el ASP usaba una fija por grupo). */
const IMG_FORMACION = 'assets/img/fep/destacados/formacionfep.jpg';

function pintarCitas() {
  const cont = $('#eventos-lista');
  const filtro = $('#filtro-categoria').value;
  // Como el bloque "Eventos destacados e inscripciones abiertas" del ASP: TODOS los eventos
  // destacados (por su orden) y después todas las inscripciones de formación abiertas.
  const lista = filtro ? citas.filter((c) => c.grupo === filtro) : citas;
  cont.setAttribute('aria-busy', 'false');
  $('#eventos-vacio').hidden = lista.length > 0;
  // Rejilla de tarjetas 3×3: cartel arriba (con la fecha encima, si la hay) y datos debajo.
  // Se muestran CITAS_VISIBLES; el resto, con el botón "Ver más citas".
  cont.replaceChildren(...lista.map((c, i) => el('li', { class: 'event-card', hidden: i >= CITAS_VISIBLES && !citasExpandidas },
    el('div', { class: 'event-card__media' },
      img(c.imagen || (c.grupo.startsWith('formacion') ? IMG_FORMACION : null), '', { class: 'event-card__img', width: 320, height: 400 }),
      c.fecha ? el('p', { class: 'event-card__date' },
        el('time', { class: 'event-card__days', datetime: c.fecha.iso, text: c.fecha.dias }),
        el('span', { class: 'event-card__month', text: c.fecha.mes })) : null),
    el('div', { class: 'event-card__body' },
      el('span', { class: 'event__tag', text: c.etiqueta }),
      // El enlace del título cubre toda la tarjeta (::after), así se puede pulsar en cualquier parte.
      el('h3', { class: 'event-card__title' }, el('a', { ...c.enlace, class: 'event-card__link' }, c.titulo)),
      c.lugar ? el('p', { class: 'event-card__place', text: c.lugar }) : null,
      c.extra ? el('p', { class: 'event-card__place', text: c.extra }) : null,
      el('span', { class: 'link-red event-card__cta', 'aria-hidden': 'true' }, `${c.cta} `, el('span', { text: '↗' }))),
  )));

  const mas = $('#eventos-mas');
  const ocultas = lista.length - CITAS_VISIBLES;
  mas.hidden = ocultas <= 0 || citasExpandidas;
  mas.textContent = `Ver más citas (${ocultas})`;
}

const CITAS_VISIBLES = 9;   // 3 × 3
let citasExpandidas = false;

function initFiltros() {
  const select = $('#filtro-categoria');
  const fep = $('#filtro-fep');
  const grupos = [...new Set(citas.map((c) => c.grupo))];
  const nombres = { evento: 'Eventos destacados', ...Object.fromEntries(Object.entries(ETIQUETA_FORMACION).map(([k, v]) => [`formacion-${k}`, v])) };
  for (const g of grupos) select.append(el('option', { value: g, text: nombres[g] ?? g }));
  $('#eventos-mas').addEventListener('click', () => {
    citasExpandidas = true;
    const primeraOculta = $('#eventos-lista > li[hidden]');
    pintarCitas();
    primeraOculta && $(`#eventos-lista > li:nth-child(${CITAS_VISIBLES + 1}) a`)?.focus();
  });
  select.addEventListener('change', () => {
    citasExpandidas = false;
    fep.classList.toggle('is-active', !select.value);
    fep.setAttribute('aria-pressed', String(!select.value));
    pintarCitas();
  });
  fep.addEventListener('click', () => {
    select.value = '';
    select.dispatchEvent(new Event('change'));
  });
}

/* ===========================================================================
   Tarjeta Formación: resumen de convocatorias abiertas
   =========================================================================== */
function pintarConvocatoriasTarjeta(convocatorias) {
  const ul = $('#convocatorias-lista');
  if (!convocatorias.length) return;
  const proxima = [...convocatorias]
    .filter((c) => aFecha(c.fechaLimiteInscripcion))
    .sort((a, b) => aFecha(a.fechaLimiteInscripcion) - aFecha(b.fechaLimiteInscripcion))[0];
  const n = convocatorias.length;
  ul.replaceChildren(el('li', {},
    el('a', { href: INSCRIPCIONES_FORMACION },
      el('strong', { text: `${n} ${n === 1 ? 'convocatoria abierta' : 'convocatorias abiertas'}` }),
      proxima ? el('span', { class: 'opp__deadline', text: `Próximo cierre: ${fmtLargo.format(aFecha(proxima.fechaLimiteInscripcion))}` }) : null)));
  ul.hidden = false;
}

/* ===========================================================================
   Franja secundaria: avisos y patrocinadores (no aparecen en el Figma)
   =========================================================================== */
function pintarAvisos(avisos) {
  if (!avisos.length) return;
  const fmt = new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit' });
  $('#avisos-lista').replaceChildren(...avisos.slice(0, 5).map((a) => {
    const f = aFecha(a.fecha);
    const attrs = attrsEnlace(a.enlace, a.ventana);
    return el('li', {},
      f ? el('time', { datetime: String(a.fecha), text: fmt.format(f) }) : null,
      attrs.href !== '#' ? el('a', attrs, a.texto) : el('span', { class: 'aviso__txt', text: a.texto }));
  }));
  $('#avisos-bloque').hidden = false;
  $('#extras').hidden = false;
}

/* ===========================================================================
   Arranque de la Home (la cabecera, el menú y el pie los inicia comun.js)
   =========================================================================== */
function init() {
  cargarLista('/api/v1/noticias/destacadas').then((noticias) => {
    // La más reciente primero (IdNoticia más alto): así una noticia nueva aparece siempre la
    // primera en "Lo que nos mueve", que está arriba, antes de los eventos.
    const lista = (noticias ?? []).slice().sort(porRecientes);
    pintarNoticias(lista);
    initHero(lista);
  });

  Promise.all([cargarLista('/api/v1/eventos/destacados'), cargarLista('/api/v1/formacion/convocatorias')])
    .then(([eventos, convocatorias]) => {
      citas = normalizaCitas(eventos ?? [], convocatorias ?? []);
      initFiltros();
      pintarCitas();
      pintarConvocatoriasTarjeta(convocatorias ?? []);
    });

  cargarLista('/api/v1/avisos').then((a) => a && pintarAvisos(a));
  // Patrocinadores: la columna derecha (#patrocinadores-lateral) la pinta comun.js, como en las
  // páginas interiores; al final de la página siguen las barras de imagen estáticas de index.html.
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
})();
