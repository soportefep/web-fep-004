/**
 * Página "Federaciones autonómicas" (migrada desde Federaciones.asp).
 * Requiere assets/js/comun.js (cabecera, menú, pie y utilidades en window.FEP).
 *
 *   Contenido → GET /api/v1/federacion/federaciones-autonomicas
 *               (respaldo: mock/federacion-federaciones-autonomicas.json y mocks.js)
 *
 * Secciones con ancla: #federaciones (las 19 autonómicas), #federacion-espanola y #reserva-zoom
 * (texto, solicitud por email y Google Calendar incrustado con carga diferida).
 * La ficha de cada federación (Datos_Federacion.asp) está migrada: enlaza a federacion.html?id={id}.
 * Todo texto de la API va por textContent.
 */
(function () {
'use strict';

const { $, $$, el, get, urlSegura, aFecha } = window.FEP;

const fmtFecha = new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'long', year: 'numeric' });
const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const ORIGEN_CALENDARIO = 'https://calendar.google.com';
const MOVIL = window.matchMedia('(max-width: 640px)');

/* ---------------------------------------------------------------------------
   Utilidades
   --------------------------------------------------------------------------- */
/** Imagen: absolutas (API real) validadas con urlSegura; relativas (mocks) tal cual. */
function urlImagen(ruta) {
  if (!ruta || typeof ruta !== 'string') return null;
  const limpia = ruta.trim();
  if (/^https?:\/\//i.test(limpia)) return urlSegura(limpia);
  if (/^[a-z][a-z0-9+.-]*:|^\/\//i.test(limpia)) return null; // javascript:, data:, //host… no
  return limpia;
}

/** "https://www.fap.es/" → "fap.es" */
function dominio(url) {
  try { return new URL(url).hostname.replace(/^www\./, ''); } catch { return url; }
}

/** Ficha de la federación (migrada desde Datos_Federacion.asp). */
const urlFicha = (id) => (Number.isInteger(id) ? `federacion.html?id=${encodeURIComponent(id)}` : null);

const plural = (n, uno, varios) => `${n} ${n === 1 ? uno : varios}`;
const enlaceExterno = (href, ...hijos) => el('a', { href, target: '_blank', rel: 'noopener' },
  ...hijos, el('span', { class: 'sr-only', text: ' (se abre en una pestaña nueva)' }));

/* ---------------------------------------------------------------------------
   Federaciones
   --------------------------------------------------------------------------- */
function tarjeta(f) {
  const bandera = urlImagen(f.imagenUrl);
  const web = urlSegura(f.web);
  const ficha = urlFicha(f.id);
  const idTitulo = `federacion-${f.id}`;
  return el('li', { class: `fed-card${f.nacional ? ' fed-card--nacional' : ''}` },
    bandera
      ? el('img', { class: 'fed-card__flag', src: bandera, alt: '', width: 148, height: 122, loading: 'lazy', decoding: 'async' })
      : el('span', { class: 'fed-card__flag', 'aria-hidden': 'true' }),
    el('div', { class: 'fed-card__body' },
      el('h3', { class: 'fed-card__title', id: idTitulo, text: f.territorio }),
      el('p', { class: 'fed-card__name', text: f.nombre }),
      el('ul', { class: 'fed-card__links' },
        web ? el('li', {}, el('a', { class: 'org-contact', href: web, target: '_blank', rel: 'noopener' },
          dominio(web), el('span', { 'aria-hidden': 'true', text: ' ↗' }),
          el('span', { class: 'sr-only', text: ` (web de la ${f.nombre}; se abre en una pestaña nueva)` }))) : null,
        !f.nacional ? el('li', {}, el('a', { class: 'link-red link-red--sm', href: `clubes.html?federacion=${encodeURIComponent(f.id)}` },
          'Clubes', el('span', { class: 'sr-only', text: ` de la ${f.nombre}` }), el('span', { 'aria-hidden': 'true', text: '→' }))) : null,
        ficha ? el('li', {}, el('a', { class: 'link-red link-red--sm', href: ficha },
          'Ficha', el('span', { class: 'sr-only', text: ` de la ${f.nombre}` }), el('span', { 'aria-hidden': 'true', text: '→' }))) : null)));
}

function cabecera(clave, num, titulo, lead, meta) {
  const idTitulo = `${clave}-titulo`;
  return {
    idTitulo,
    nodo: el('header', { class: 'organo__head' },
      el('p', { class: 'organo__num', 'aria-hidden': 'true', text: String(num).padStart(2, '0') }),
      el('div', {},
        el('h2', { class: 'h2 organo__title', id: idTitulo, text: titulo }),
        lead ? el('p', { class: 'organo__lead', text: lead }) : null,
        meta ? el('p', { class: 'organo__meta', text: meta }) : null)),
  };
}

function seccionFederaciones(autonomicas, num) {
  const conWeb = autonomicas.filter((f) => urlSegura(f.web)).length;
  const { idTitulo, nodo } = cabecera('federaciones', num, 'Federaciones autonómicas', null,
    `${plural(autonomicas.length, 'federación', 'federaciones')} · ${plural(conWeb, 'web oficial', 'webs oficiales')}`);
  return el('section', { class: 'organo', id: 'federaciones', 'aria-labelledby': idTitulo },
    nodo, el('ul', { class: 'fed-grid' }, autonomicas.map(tarjeta)));
}

function seccionNacional(nacionales, num) {
  const { idTitulo, nodo } = cabecera('federacion-espanola', num, 'Federación Española de Pádel', null, null);
  return el('section', { class: 'organo', id: 'federacion-espanola', 'aria-labelledby': idTitulo },
    nodo, el('ul', { class: 'fed-grid fed-grid--nacional' }, nacionales.map(tarjeta)));
}

/* ---------------------------------------------------------------------------
   Reserva de Zoom con Google Calendar incrustado
   --------------------------------------------------------------------------- */
/** Solo se incrusta un calendario de https://calendar.google.com; en móvil, vista de agenda. */
function urlCalendario(url) {
  const segura = urlSegura(url);
  if (!segura) return null;
  const u = new URL(segura);
  if (u.origin !== ORIGEN_CALENDARIO || !u.pathname.startsWith('/calendar/embed')) return null;
  if (MOVIL.matches) u.searchParams.set('mode', 'AGENDA');
  return u.href;
}

function seccionZoom(z, num) {
  const email = z.email && EMAIL.test(z.email) ? z.email : null;
  const imagen = urlImagen(z.imagenUrl);
  const calendario = z.calendario ? urlCalendario(z.calendario.url) : null;
  const { idTitulo, nodo } = cabecera('reserva-zoom', num, z.titulo, null,
    calendario ? 'Calendario de disponibilidad actualizado por la FEP' : null);

  return el('section', { class: 'organo', id: 'reserva-zoom', 'aria-labelledby': idTitulo },
    nodo,
    imagen ? el('img', { class: 'fed-zoom__banner', src: imagen, alt: z.imagenAlt || '', width: 809, height: 135, loading: 'lazy', decoding: 'async' }) : null,
    (z.parrafos ?? []).filter(Boolean).map((p) => el('p', { class: 'transp-text', text: p })),
    email ? el('p', { class: 'fed-zoom__cta' },
      el('a', { class: 'btn btn--red btn--lg', href: `mailto:${email}?subject=${encodeURIComponent('Reserva de Zoom')}` },
        'Solicitar la reserva ', el('span', { class: 'btn__arrow', 'aria-hidden': 'true', text: '→' })),
      el('span', { class: 'fed-zoom__email' }, 'o escribe a ', el('a', { href: `mailto:${email}`, text: email }))) : null,
    calendario ? el('figure', { class: 'fed-calendar' },
      // Carga diferida: el iframe de Google solo se pide cuando se acerca a la pantalla.
      el('iframe', {
        class: 'fed-calendar__frame', src: calendario, title: z.calendario.titulo || 'Calendario de reservas de Zoom',
        loading: 'lazy', referrerpolicy: 'strict-origin-when-cross-origin',
      }),
      el('figcaption', {},
        el('span', { text: z.calendario.zonaHoraria === 'Europe/Madrid' ? 'Horario peninsular (Madrid).' : '' }),
        enlaceExterno(calendario, 'Abrir el calendario en Google Calendar ', el('span', { 'aria-hidden': 'true', text: '↗' })))) : null);
}

/* ---------------------------------------------------------------------------
   Índice lateral y resumen
   --------------------------------------------------------------------------- */
function pintarIndice(entradas) {
  $('#federaciones-indice').replaceChildren(...entradas.map(([clave, texto, n]) => el('li', {},
    el('a', { href: `#${clave}`, dataset: { clave } },
      el('span', { text: texto }), n != null ? el('span', { class: 'page-toc__count', text: String(n) }) : null))));

  if (!('IntersectionObserver' in window)) return;
  const enlaces = new Map($$('#federaciones-indice a').map((a) => [a.dataset.clave, a]));
  const obs = new IntersectionObserver((vistas) => {
    for (const v of vistas) {
      if (!v.isIntersecting) continue;
      enlaces.forEach((a, clave) => {
        const activo = clave === v.target.id;
        a.classList.toggle('is-active', activo);
        if (activo) a.setAttribute('aria-current', 'location'); else a.removeAttribute('aria-current');
      });
    }
  }, { rootMargin: '-35% 0px -55% 0px' });
  entradas.forEach(([clave]) => { const s = document.getElementById(clave); if (s) obs.observe(s); });
}

/* ---------------------------------------------------------------------------
   Arranque
   --------------------------------------------------------------------------- */
async function init() {
  const cont = $('#federaciones-contenido');
  try {
    const datos = await get('/api/v1/federacion/federaciones-autonomicas');
    const todas = (datos?.federaciones ?? []).filter((f) => f && Number.isInteger(f.id) && f.territorio && f.nombre);
    const autonomicas = todas.filter((f) => !f.nacional);
    const nacionales = todas.filter((f) => f.nacional);
    if (!autonomicas.length) throw new Error('Respuesta sin federaciones');

    if (datos.introduccion) $('#federaciones-intro').textContent = datos.introduccion;

    const secciones = [];
    const indice = [];
    let num = 0;
    secciones.push(seccionFederaciones(autonomicas, ++num));
    indice.push(['federaciones', 'Federaciones autonómicas', autonomicas.length]);
    if (nacionales.length) {
      secciones.push(seccionNacional(nacionales, ++num));
      indice.push(['federacion-espanola', 'Federación Española', null]);
    }
    const zoom = datos.reservaZoom;
    if (zoom?.titulo) {
      secciones.push(seccionZoom(zoom, ++num));
      indice.push(['reserva-zoom', 'Reserva de Zoom', null]);
    }

    $('.org-skeleton', cont)?.remove();
    cont.prepend(...secciones);
    if (datos.actualizado && aFecha(datos.actualizado)) {
      cont.append(el('p', { class: 'org-updated' }, 'Información actualizada el ',
        el('time', { datetime: datos.actualizado, text: fmtFecha.format(aFecha(datos.actualizado)) }), '.'));
    }
    pintarIndice(indice);
    $('#federaciones-resumen').replaceChildren(
      el('li', {}, el('strong', { text: String(autonomicas.length) }), ' federaciones autonómicas'),
      zoom?.calendario ? el('li', {}, el('strong', { text: 'Zoom' }), ' compartido con las federaciones') : null);

    if (location.hash) document.getElementById(decodeURIComponent(location.hash.slice(1)))?.scrollIntoView();
  } catch (e) {
    console.warn('[FEP] federaciones autonómicas:', e.message);
    $('.org-skeleton', cont)?.remove();
    $('#federaciones-error').hidden = false;
  } finally {
    cont.setAttribute('aria-busy', 'false');
  }
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
})();
