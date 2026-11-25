/**
 * Home FEP · Prototipo 003 ("Editorial deportivo").
 * Mismos datos y endpoints que la home del prototipo 1 (home.js), con otra maquetación:
 *   Portada partida (carrusel)       → diapositivas fijas del HTML
 *   Teletipo "Próximas citas"        → eventos destacados + convocatorias de formación
 *   02 "Lo que nos mueve" (periódico) → GET /api/v1/noticias/destacadas
 *   03 "La próxima cita" (marcador)   → GET /api/v1/eventos/destacados + /api/v1/formacion/convocatorias
 *   Formación (contador)              → GET /api/v1/formacion/convocatorias
 *   Avisos                            → GET /api/v1/avisos
 * Requiere assets/js/comun.js (cabecera, menú, pie, patrocinadores y utilidades en window.FEP).
 * Todo texto que llega de la API se pinta con textContent / atributos, nunca con innerHTML.
 */
(function () {
'use strict';

const { $, $$, el, attrsEnlace, img, textoPlano, aFecha, bloqueFecha, fmtLargo, fmtCorto, reduceMotion, cargarLista, get, comoLista } = window.FEP;
const INSCRIPCIONES_FORMACION = 'https://www.padelfederacion.es/Formacion_Inscripciones.asp';
const IMG_FORMACION = 'assets/img/fep/destacados/formacionfep.jpg';
const dosCifras = (n) => String(n).padStart(2, '0');

/* ===========================================================================
   Portada: carrusel de destacados con contador 01 / 02 y flechas
   =========================================================================== */
const hero = { slides: [], actual: 0, timer: null, pausado: reduceMotion.matches, hover: false, INTERVALO: 7000 };

function initHero() {
  hero.slides = $$('.ed-slide', $('#hero-slides')).map((nodo) => {
    const { titulo, enlace } = nodo.dataset;
    return { nodo, titulo: titulo || null, ...(enlace ? attrsEnlace(enlace) : { href: null }) };
  });
  const total = hero.slides.length;
  $('#hero-total').textContent = dosCifras(total);
  irA(0);
  if (total < 2) { $('.ed-hero__ctrl').hidden = true; return; }

  $('#hero-prev').addEventListener('click', () => { irA(hero.actual - 1); reiniciar(); });
  $('#hero-next').addEventListener('click', () => { irA(hero.actual + 1); reiniciar(); });
  const pausa = $('#hero-pause');
  actualizarPausa();
  pausa.addEventListener('click', () => { hero.pausado = !hero.pausado; actualizarPausa(); reiniciar(); });
  const raiz = $('.ed-hero__media');
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
  pausa.classList.toggle('is-paused', hero.pausado);
  $('.sr-only', pausa).textContent = hero.pausado ? 'Reanudar carrusel' : 'Pausar carrusel';
}

function irA(i) {
  const total = hero.slides.length;
  hero.actual = (i + total) % total;
  hero.slides.forEach((s, j) => {
    const activo = j === hero.actual;
    s.nodo.classList.toggle('is-active', activo);
    s.nodo.setAttribute('aria-hidden', String(!activo));
    s.nodo.setAttribute('aria-label', `${j + 1} de ${total}`);
  });
  $('#hero-n').textContent = dosCifras(hero.actual + 1);
  const cap = $('#hero-caption');
  const s = hero.slides[hero.actual];
  if (s.titulo && s.href && s.href !== '#') {
    cap.replaceChildren(el('span', { class: 'ed-hero__caption-txt', text: s.titulo }), el('span', { 'aria-hidden': 'true', text: ' ↗' }));
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
   02 · "Lo que nos mueve": noticia principal a lo ancho + fila de noticias
   =========================================================================== */
function idNoticia(n) {
  if (Number.isFinite(n.idNoticia)) return n.idNoticia;
  const m = /[?&]idnoticia=(\d+)/i.exec(n.enlace ?? '');
  return m ? Number(m[1]) : null;
}
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
    cont.hidden = true;
    $('#noticias-vacio').hidden = false;
    return;
  }
  const [primera, ...resto] = noticias;
  const a1 = attrsEnlace(primera.enlace, primera.ventana);
  const principal = el('article', { class: 'ed-news__lead' },
    el('div', { class: 'ed-news__lead-media' }, img(primera.imagenUrl, '', { class: 'ed-news__lead-img', width: 650, height: 434 })),
    el('div', { class: 'ed-news__lead-body' },
      el('p', { class: 'ed-news__flag', text: 'Última noticia' }),
      el('h3', { class: 'ed-display ed-news__lead-title' }, el('a', { ...a1, class: 'ed-cover' }, primera.titulo)),
      el('span', { class: 'ed-news__read', 'aria-hidden': 'true' }, 'Leer noticia ', el('span', { text: '↗' }))));

  const fila = el('ol', { class: 'ed-news__row' }, resto.map((n, i) => {
    const at = attrsEnlace(n.enlace, n.ventana);
    return el('li', { class: 'ed-news__item' },
      el('div', { class: 'ed-news__thumb-wrap' }, img(n.imagenUrl, '', { class: 'ed-news__thumb', width: 320, height: 214 })),
      el('p', { class: 'ed-news__meta' },
        el('span', { class: 'ed-news__n', 'aria-hidden': 'true', text: dosCifras(i + 2) }),
        el('span', { text: n.categoria || 'Destacada' })),
      el('h3', { class: 'ed-news__title' }, el('a', { ...at, class: 'ed-cover' }, n.titulo)));
  }));
  cont.replaceChildren(principal, ...(resto.length ? [fila] : []));
}

/* ===========================================================================
   03 · "La próxima cita": marcador (tabla) + teletipo superior
   =========================================================================== */
const ETIQUETA_FORMACION = {
  monitor: 'Formación · Técnicos',
  foco: 'Formación · FOCO',
  juez: 'Formación · Jueces árbitros',
};
const NOMBRE_GRUPO = { evento: 'Eventos destacados', ...Object.fromEntries(Object.entries(ETIQUETA_FORMACION).map(([k, v]) => [`formacion-${k}`, v])) };
const CITAS_VISIBLES = 6;
let citas = [];
let filtro = '';
let citasExpandidas = false;

function normalizaCitas(eventos, convocatorias) {
  const ev = eventos.map((e) => ({
    grupo: 'evento',
    etiqueta: e.categoria || 'Evento destacado',
    titulo: e.titulo,
    fecha: bloqueFecha(e.fechaInicio, e.fechaFin),
    texto: textoPlano(e.texto),
    lugar: [e.localidad, e.lugar].filter(Boolean).join(' · '),
    imagen: e.imagenUrl,
    enlace: attrsEnlace(e.enlace, e.ventana),
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
      imagen: IMG_FORMACION,
      enlace: { href: INSCRIPCIONES_FORMACION },
      inicio: aFecha(c.fechaInicio)?.getTime() ?? Infinity,
    };
  }).sort((a, b) => a.inicio - b.inicio);
  return [...ev.sort((a, b) => a.orden - b.orden), ...fo];
}

/** Los eventos de la portada traen fecha y sede juntas en "texto" (p. ej. "06 NOV - 8 NOV - ZARAGOZA - CLUB"). */
function partesTexto(c) {
  if (c.fecha || !c.texto) return { fecha: null, sede: c.lugar || c.texto || '' };
  // Formatos vistos: "06 NOV - 8 NOV - SEDE", "20 - 22 NOV SEDE", "20 - 22 NOVIEMBRE SEDE", "13 DIC - SEDE"
  const MES = '[A-Za-zÁÉÍÓÚáéíóú]{3,}\\.?';
  const re = new RegExp(`^\\s*(\\d{1,2}(?:\\s*${MES})?(?:\\s*-\\s*\\d{1,2}\\s*${MES})?)\\s*(?:-\\s*)?(.+)$`);
  const m = re.exec(c.texto);
  return m && /[A-Za-z]/.test(m[1])
    ? { fecha: m[1].replace(/\s*-\s*/, ' – ').toUpperCase(), sede: c.lugar || m[2] }
    : { fecha: null, sede: c.lugar || c.texto };
}

function pintarCitas() {
  const cont = $('#eventos-lista');
  const lista = filtro ? citas.filter((c) => c.grupo === filtro) : citas;
  cont.setAttribute('aria-busy', 'false');
  $('#eventos-vacio').hidden = lista.length > 0;

  cont.replaceChildren(...lista.map((c, i) => {
    const p = partesTexto(c);
    const fecha = c.fecha
      ? el('time', { class: 'ed-board__date', datetime: c.fecha.iso }, el('strong', { text: c.fecha.dias }), ' ', c.fecha.mes)
      : el('span', { class: 'ed-board__date', text: p.fecha || '—' });
    return el('div', { class: `ed-board__row${c.grupo.startsWith('formacion') ? ' is-formacion' : ''}`, role: 'row', hidden: i >= CITAS_VISIBLES && !citasExpandidas },
      el('span', { role: 'cell', class: 'ed-board__c-date' }, fecha),
      el('span', { role: 'cell', class: 'ed-board__c-title' },
        img(c.imagen, '', { class: 'ed-board__img', width: 48, height: 60 }),
        el('span', {},
          el('a', { ...c.enlace, class: 'ed-cover ed-board__title' }, c.titulo),
          c.extra ? el('span', { class: 'ed-board__extra', text: c.extra }) : null)),
      el('span', { role: 'cell', class: 'ed-board__c-place', text: p.sede }),
      el('span', { role: 'cell', class: 'ed-board__c-type' }, el('span', { class: 'ed-board__type', text: c.etiqueta })),
      el('span', { role: 'cell', class: 'ed-board__c-go', 'aria-hidden': 'true', text: '↗' }));
  }));

  const mas = $('#eventos-mas');
  const ocultas = lista.length - CITAS_VISIBLES;
  mas.hidden = ocultas <= 0 || citasExpandidas;
  mas.textContent = `Ver más citas (${ocultas})`;
}

function initFiltros() {
  const caja = $('#filtros-citas');
  const territoriales = $('#filtro-territoriales');
  const grupos = [...new Set(citas.map((c) => c.grupo))];
  for (const g of grupos) {
    caja.insertBefore(el('button', { type: 'button', class: 'ed-tab', 'data-grupo': g, 'aria-pressed': 'false', text: NOMBRE_GRUPO[g] ?? g }), territoriales);
  }
  caja.addEventListener('click', (e) => {
    const b = e.target.closest('button.ed-tab');
    if (!b) return;
    filtro = b.dataset.grupo;
    citasExpandidas = false;
    $$('button.ed-tab', caja).forEach((t) => {
      const activo = t === b;
      t.classList.toggle('is-active', activo);
      t.setAttribute('aria-pressed', String(activo));
    });
    pintarCitas();
  });
  $('#eventos-mas').addEventListener('click', () => {
    citasExpandidas = true;
    pintarCitas();
    $(`#eventos-lista > .ed-board__row:nth-child(${CITAS_VISIBLES + 1}) a`)?.focus();
  });
}

/** Teletipo: la lista se duplica para que el desplazamiento continuo no tenga saltos (la copia no es accesible). */
function pintarTeletipo() {
  if (!citas.length) return;
  const item = (c, copia) => el('li', copia ? { 'aria-hidden': 'true' } : {},
    el('a', { ...c.enlace, ...(copia ? { tabindex: '-1' } : {}) },
      el('span', { class: 'ed-ticker__tag', text: c.grupo.startsWith('formacion') ? 'Formación' : 'Competición' }),
      c.titulo));
  $('#ticker-lista').replaceChildren(...citas.map((c) => item(c, false)), ...citas.map((c) => item(c, true)));
  $('#ticker').hidden = false;
}

/* ===========================================================================
   Formación: resumen de convocatorias abiertas
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
      proxima ? el('span', { text: `Próximo cierre: ${fmtLargo.format(aFecha(proxima.fechaLimiteInscripcion))}` }) : null)));
  ul.hidden = false;
}

/* ===========================================================================
   Avisos (solo si los hay)
   =========================================================================== */
function pintarAvisos(avisos) {
  if (!avisos.length) return;
  const fmt = new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit' });
  $('#avisos-lista').replaceChildren(...avisos.slice(0, 5).map((a) => {
    const f = aFecha(a.fecha);
    const attrs = attrsEnlace(a.enlace, a.ventana);
    return el('li', {},
      f ? el('time', { datetime: String(a.fecha), text: fmt.format(f) }) : null,
      attrs.href !== '#' ? el('a', attrs, a.texto) : el('span', { text: a.texto }));
  }));
  $('#avisos-bloque').hidden = false;
  $('#extras').hidden = false;
}

/* ===========================================================================
   06 · En redes: últimos reels de Instagram (carrusel horizontal)
   GET /api/v1/redes-sociales/instagram-reels → { perfilUrl, reelsUrl, datosDePrueba, reels: [
     { id, descripcion, fecha, miniaturaUrl, enlace } ] }
   La API real los saca de la Instagram Graph API en el servidor; aquí solo se pintan.
   Los enlaces solo se aceptan si van a instagram.com (si no, al perfil).
   =========================================================================== */
const PERFIL_INSTAGRAM = 'https://www.instagram.com/padelfederacion/';
const MAX_REELS = 12;
const DIAS_NUEVO = 14;   // etiqueta "Nuevo" en los reels publicados en los últimos 14 días
const fmtReel = new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'short' });

/** Enlace válido de Instagram (https y dominio instagram.com) o null. */
function enlaceInstagram(url) {
  try {
    const u = new URL(url);
    return u.protocol === 'https:' && /(^|\.)instagram\.com$/i.test(u.hostname) ? u.href : null;
  } catch { return null; }
}

function pintarReels(datos) {
  const reels = (Array.isArray(datos?.reels) ? datos.reels : comoLista(datos)).slice(0, MAX_REELS);
  if (!reels.length) return;
  const perfil = enlaceInstagram(datos?.perfilUrl) || PERFIL_INSTAGRAM;
  const porDefecto = enlaceInstagram(datos?.reelsUrl) || perfil;
  $('#reels-perfil').href = perfil;
  $('#reels-aviso').hidden = !datos?.datosDePrueba;

  const total = reels.length;
  const limiteNuevo = Date.now() - DIAS_NUEVO * 864e5;
  $('#reels-lista').replaceChildren(...reels.map((r, i) => {
    const f = aFecha(r.fecha);
    const nuevo = f && f.getTime() >= limiteNuevo;
    const desc = textoPlano(r.descripcion || '', 110) || 'Reel de la Federación Española de Pádel';
    const fecha = f ? fmtReel.format(f).replace('.', '') : '';
    return el('li', { class: 'ed-reel', 'aria-roledescription': 'reel', 'aria-label': `${i + 1} de ${total}` },
      el('a', {
        class: 'ed-reel__link', href: enlaceInstagram(r.enlace) || porDefecto, target: '_blank', rel: 'noopener',
        'aria-label': `${nuevo ? 'Nuevo. ' : ''}Ver reel en Instagram${fecha ? ` del ${fecha}` : ''}: ${desc} (se abre en una pestaña nueva)`,
      },
        img(r.miniaturaUrl, '', { class: 'ed-reel__img', width: 360, height: 540, loading: 'lazy' }),
        nuevo ? el('span', { class: 'ed-reel__nuevo', 'aria-hidden': 'true', text: 'Nuevo' }) : null,
        el('span', { class: 'ed-reel__icono', 'aria-hidden': 'true' }),
        el('span', { class: 'ed-reel__desc', 'aria-hidden': 'true', text: desc })));
  }));
  $('#reels').hidden = false;
  initCarrilReels();
}

function initCarrilReels() {
  const track = $('#reels-lista');
  const prev = $('#reels-prev');
  const next = $('#reels-next');
  const paso = () => {
    const tarjeta = track.firstElementChild;
    const gap = parseFloat(getComputedStyle(track).columnGap) || 0;
    const ancho = tarjeta ? tarjeta.getBoundingClientRect().width + gap : track.clientWidth;
    return Math.max(ancho, Math.floor(track.clientWidth / ancho) * ancho);   // una "pantalla" de tarjetas completas
  };
  const mover = (dir) => track.scrollBy({ left: dir * paso(), behavior: reduceMotion.matches ? 'auto' : 'smooth' });
  // Las flechas se ocultan en los extremos (como en los carruseles tipo "stories")
  const actualizar = () => {
    prev.hidden = track.scrollLeft <= 2;
    next.hidden = track.scrollLeft >= track.scrollWidth - track.clientWidth - 2;
  };
  prev.addEventListener('click', () => mover(-1));
  next.addEventListener('click', () => mover(1));
  let espera = 0;
  track.addEventListener('scroll', () => { clearTimeout(espera); espera = setTimeout(actualizar, 80); }, { passive: true });
  if ('ResizeObserver' in window) new ResizeObserver(actualizar).observe(track);
  actualizar();
}

/* ===========================================================================
   Patrocinadores repartidos en dos zonas
   comun.js pinta los tres bloques (principales, patrocinadores, productos homologados) en
   #patrocinadores-lateral. Los principales se quedan ahí (antes de "La próxima cita") y los otros
   dos se llevan a la franja de debajo del ranking (#patrocinadores-resto).
   =========================================================================== */
function repartirPatrocinadores() {
  const origen = $('#patrocinadores-lateral');
  const destino = $('#patrocinadores-resto');
  if (!origen || !destino) return;
  const mover = () => {
    const bloques = $$('.patro-nivel--secundarios, .patro-nivel--homologados', origen);
    if (!bloques.length) return false;
    destino.append(...bloques);
    // En la home la franja de arriba no lleva el título general "Patrocinadores FEP" y el bloque
    // principal se titula solo "Patrocinadores" (en las páginas interiores no cambia nada).
    $('#patro-lateral-titulo', origen)?.remove();
    const tituloPrincipal = $('.patro-nivel--principales .patro-nivel__title', origen);
    if (tituloPrincipal) {
      tituloPrincipal.textContent = 'Patrocinadores';
      origen.setAttribute('aria-labelledby', tituloPrincipal.id);
      origen.closest('section')?.setAttribute('aria-labelledby', tituloPrincipal.id);
    }
    $('#patrocinadores-resto-franja').hidden = false;
    return true;
  };
  if (mover()) return;
  // Si comun.js aún no los ha pintado, se espera a que lo haga.
  const obs = new MutationObserver(() => { if (mover()) obs.disconnect(); });
  obs.observe(origen, { childList: true });
}

/* ===========================================================================
   Arranque (cabecera, menú, pie y patrocinadores los inicia comun.js)
   =========================================================================== */
function init() {
  $('#ed-temporada').textContent = String(new Date().getFullYear());
  repartirPatrocinadores();
  initHero();

  cargarLista('/api/v1/noticias/destacadas').then((noticias) => {
    pintarNoticias((noticias ?? []).slice().sort(porRecientes));
  });

  Promise.all([cargarLista('/api/v1/eventos/destacados'), cargarLista('/api/v1/formacion/convocatorias')])
    .then(([eventos, convocatorias]) => {
      citas = normalizaCitas(eventos ?? [], convocatorias ?? []);
      initFiltros();
      pintarCitas();
      pintarTeletipo();
      pintarConvocatoriasTarjeta(convocatorias ?? []);
    });

  cargarLista('/api/v1/avisos').then((a) => a && pintarAvisos(a));

  get('/api/v1/redes-sociales/instagram-reels')
    .then(pintarReels)
    .catch((e) => console.warn('[FEP] reels de Instagram:', e.message));   // sin reels: la sección queda oculta
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
})();
