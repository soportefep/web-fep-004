/**
 * Página "Programa Impulso" (migrada desde Impulso.asp, URL antigua /Programa_Impulso).
 * Requiere assets/js/comun.js (cabecera, menú, pie y utilidades en window.FEP).
 *
 *   Presentación + documentos + torneos → GET /api/v1/competicion/programa-impulso?anio
 *                                         (respaldo: mock/competicion-programa-impulso.json)
 *
 * La presentación y los PDF (bases y calendario) se pintan con la primera respuesta; al cambiar de
 * temporada solo se vuelve a pintar la lista de torneos. Sin temporada se muestra la más reciente. La
 * temporada viaja en la URL (?anio=2023) y también se acepta el nombre del formulario antiguo (?ano=2023).
 * Los torneos se vuelven a filtrar por temporada en el navegador: con la API no cambia nada (ya vienen
 * filtrados) y en modo prototipo el mock (todas las temporadas) se comporta como la API.
 * Todo texto de la API va por textContent.
 */
(function () {
'use strict';

const { $, $$, el, get, urlSegura, paginaMigrada, aFecha, bloqueFecha, fmtLargo } = window.FEP;

const ANIO_MIN = 1901;
const ANIO_MAX = 2100;
const fmtNum = new Intl.NumberFormat('es-ES');
const fmtPlazo = new Intl.DateTimeFormat('es-ES', {
  weekday: 'long', day: 'numeric', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit',
});
const PLAZOS = {
  inscripcion: 'Inscripción abierta',
  'inscripcion-equipos': 'Inscripción de equipos abierta',
  preinscripcion: 'Preinscripción abierta',
};
const TIPOS_DOC = { pdf: 'PDF', xls: 'XLS', doc: 'DOC' };

let anioPedido = null;     // null = la temporada más reciente
let anioMostrado = null;
let anios = [];
let documentos = 0;
let contenidoPintado = false;
let peticion = 0;          // descarta respuestas de consultas anteriores

/* ---------------------------------------------------------------------------
   Utilidades
   --------------------------------------------------------------------------- */
const entero = (v) => (/^\d+$/.test(String(v ?? '').trim()) ? Number(v) : null);
const plural = (n, uno, varios) => `${fmtNum.format(n)} ${n === 1 ? uno : varios}`;
const limpio = (t) => String(t ?? '').replace(/\s+/g, ' ').trim();

/**
 * URL de una imagen: las absolutas (API real) se validan con urlSegura; las relativas (mock,
 * "assets/img/fep/…") se usan tal cual, relativas a esta página.
 */
function urlImagen(ruta) {
  if (!ruta || typeof ruta !== 'string') return null;
  const r = ruta.trim();
  if (/^https?:\/\//i.test(r)) return urlSegura(r);
  if (/^[a-z][a-z0-9+.-]*:|^\/\//i.test(r)) return null;
  return r;
}

/** Plazo de inscripción todavía abierto según el reloj del navegador (la API y el mock pueden ir con retraso). */
function plazoVigente(c) {
  const p = c.inscripcion;
  const fecha = aFecha(p?.fechaLimite);
  return fecha && fecha > new Date() && PLAZOS[p.tipo] ? { tipo: p.tipo, fecha } : null;
}

/* ---------------------------------------------------------------------------
   Estado ↔ URL
   --------------------------------------------------------------------------- */
function leerUrl() {
  const q = new URLSearchParams(location.search);
  const anio = entero(q.get('anio') ?? q.get('ano'));
  anioPedido = anio >= ANIO_MIN && anio <= ANIO_MAX ? anio : null;
}

function escribirUrl() {
  const q = new URLSearchParams();
  if (anioPedido) q.set('anio', anioPedido);
  const s = q.toString();
  try {
    history.replaceState(null, '', `${location.pathname}${s ? `?${s}` : ''}${location.hash}`);
  } catch {
    // Abierta desde disco (file://) algunos navegadores no permiten cambiar la URL: no es imprescindible.
  }
}

/* ---------------------------------------------------------------------------
   Presentación y documentos (se pintan una vez)
   --------------------------------------------------------------------------- */
function pintarContenido(d) {
  const intro = $('#imp-intro');
  const parrafos = (Array.isArray(d.introduccion) ? d.introduccion : []).map(limpio).filter(Boolean);
  const imagen = urlImagen(d.imagenUrl);
  const alt = limpio(d.imagenAlt) || `Logotipo: ${limpio(d.titulo) || 'Programa Impulso'}`;

  intro.replaceChildren(
    imagen ? el('figure', { class: 'imp-logo' },
      el('img', { src: imagen, alt, width: 960, height: 382, decoding: 'async' })) : null,
    el('div', { class: 'imp-intro__text' }, parrafos.map((p) => el('p', { class: 'transp-text', text: p }))));
  // Si la imagen no carga, se quita (no aporta información que no esté en el texto).
  $('img', intro)?.addEventListener('error', (e) => e.target.closest('figure')?.remove(), { once: true });
  intro.hidden = false;
  $('#imp-skeleton').remove();

  const docs = (Array.isArray(d.documentos) ? d.documentos : []).filter((x) => x && x.titulo);
  documentos = docs.length;
  const temporadas = [...new Set(docs.map((x) => x.temporada).filter(Number.isInteger))];
  $('#imp-docs-titulo').textContent = temporadas.length === 1 ? `Bases y calendario ${temporadas[0]}` : 'Bases y calendario';
  $('#imp-docs-total').textContent = fmtNum.format(docs.length);
  $('#imp-docs').replaceChildren(...docs.map(itemDocumento));
  $('#imp-docs').hidden = !docs.length;
  $('#imp-docs-vacio').hidden = docs.length > 0;
  const cuenta = $('#imp-indice-docs');
  cuenta.textContent = String(docs.length);
  cuenta.hidden = false;

  if (d.actualizado && aFecha(d.actualizado)) {
    $('#documentos').append(el('p', { class: 'org-updated' }, 'Información revisada el ',
      el('time', { datetime: d.actualizado, text: fmtLargo.format(aFecha(d.actualizado)) }), '.'));
  }
  contenidoPintado = true;
}

function itemDocumento(x) {
  const url = urlSegura(x.url);
  const f = aFecha(x.fecha);
  const tipo = TIPOS_DOC[String(x.tipo ?? '').toLowerCase()] ?? 'DOC';
  const titulo = limpio(x.titulo);
  return el('li', { class: 'doc' },
    el('span', { class: 'doc__icon', 'aria-hidden': 'true', text: tipo }),
    el('div', { class: 'doc__body' },
      url ? el('a', { class: 'doc__title', href: url, target: '_blank', rel: 'noopener' }, titulo,
        el('span', { class: 'sr-only', text: ` (${tipo}, se abre en una pestaña nueva)` }))
        : el('span', { class: 'doc__title', text: titulo }),
      x.descripcion ? el('p', { class: 'imp-doc__desc', text: limpio(x.descripcion) }) : null,
      el('p', { class: 'doc__meta' },
        el('span', { class: 'org-tag', text: tipo }),
        Number.isInteger(x.temporada) ? el('span', { text: `Temporada ${x.temporada}` }) : null,
        f ? el('span', {}, 'Versión del ', el('time', { datetime: String(x.fecha), text: fmtLargo.format(f) })) : null)),
    url ? el('a', { class: 'doc__download', href: url, target: '_blank', rel: 'noopener', 'aria-label': `Descargar en ${tipo}: ${titulo}` },
      'Descargar ', el('span', { 'aria-hidden': 'true', text: '↓' })) : null);
}

/* ---------------------------------------------------------------------------
   Temporadas
   --------------------------------------------------------------------------- */
function pintarAnios() {
  const select = $('#imp-anio');
  select.replaceChildren(...anios.map((a) => el('option', { value: String(a), selected: a === anioMostrado, text: String(a) })));
  select.disabled = !anios.length;
}

function initFormulario() {
  $('#imp-anio').addEventListener('change', (e) => { anioPedido = entero(e.target.value); buscar(); });
  $('#imp-form').addEventListener('submit', (e) => e.preventDefault());
}

/* ---------------------------------------------------------------------------
   Consulta
   --------------------------------------------------------------------------- */
async function buscar() {
  const id = ++peticion;
  escribirUrl();
  mostrarCargando();

  try {
    const datos = await get(`/api/v1/competicion/programa-impulso${anioPedido ? `?anio=${anioPedido}` : ''}`);
    if (id !== peticion) return;
    if (!datos || typeof datos !== 'object') throw new Error('Respuesta vacía');

    if (!contenidoPintado) pintarContenido(datos);

    const lista = (Array.isArray(datos.anios) ? datos.anios : []).filter((a) => Number.isInteger(a));
    // Temporada pedida que no existe (enlace antiguo): se vuelve a la más reciente.
    if (anioPedido && lista.length && !lista.includes(anioPedido)) {
      anioPedido = null;
      return buscar();
    }
    anioMostrado = anioPedido ?? (Number.isInteger(datos.anio) ? datos.anio : lista[0] ?? null);
    if (lista.join() !== anios.join()) anios = lista;
    pintarAnios();

    const items = (Array.isArray(datos.items) ? datos.items : [])
      .filter((c) => c && Number.isInteger(c.id) && c.titulo && (c.anio ?? anioMostrado) === anioMostrado);
    pintarTorneos(items);
  } catch (e) {
    if (id !== peticion) return;
    console.warn('[FEP] programa impulso:', e.message);
    if (!contenidoPintado) {
      $('#imp-skeleton')?.remove();
      $('#imp-error').hidden = false;
      $('#imp-docs-total').textContent = '–';
      $('#imp-docs-vacio').hidden = false;
    }
    $('#imp-torneos-skeleton').hidden = true;
    $('#imp-lista').hidden = true;
    $('#imp-vacio').hidden = true;
    $('#imp-torneos-error').hidden = false;
    $('#imp-total').textContent = '–';
    $('#imp-estado').textContent = 'No se han podido cargar los torneos.';
    $('#imp-anio').disabled = !anios.length;
  } finally {
    if (id === peticion) {
      $('#imp-contenido').setAttribute('aria-busy', 'false');
      $('#torneos').setAttribute('aria-busy', 'false');
    }
  }
}

function mostrarCargando() {
  $('#torneos').setAttribute('aria-busy', 'true');
  $('#imp-torneos-error').hidden = true;
  // Primera carga: skeleton. Después se deja la lista anterior atenuada hasta que llega la nueva.
  if ($('#imp-lista').hidden && $('#imp-vacio').hidden) $('#imp-torneos-skeleton').hidden = false;
  $('#imp-lista').classList.add('is-loading');
}

/* ---------------------------------------------------------------------------
   Torneos
   --------------------------------------------------------------------------- */
function pintarTorneos(items) {
  $('#imp-torneos-skeleton').hidden = true;
  $('#imp-titulo-lista').textContent = anioMostrado ? `Torneos del programa ${anioMostrado}` : 'Torneos del programa';
  $('#imp-total').textContent = fmtNum.format(items.length);
  $('#imp-estado').textContent = anioMostrado
    ? `${plural(items.length, 'torneo', 'torneos')} en la temporada ${anioMostrado}.`
    : 'Todavía no hay torneos del programa publicados.';

  const cont = $('#imp-lista');
  cont.classList.remove('is-loading');
  cont.replaceChildren(el('ul', { class: 'camp-items' }, items.map((c) => el('li', {}, tarjeta(c)))));
  cont.hidden = !items.length;
  $('#imp-vacio').hidden = items.length > 0;

  const cuenta = $('#imp-indice-torneos');
  cuenta.textContent = String(items.length);
  cuenta.hidden = false;

  const resumen = [
    [fmtNum.format(items.length), anioMostrado ? `torneos en ${anioMostrado}` : 'torneos'],
    anios.length ? [fmtNum.format(anios.length), 'temporadas'] : null,
    documentos ? [fmtNum.format(documentos), documentos === 1 ? 'documento' : 'documentos'] : null,
  ].filter(Boolean);
  $('#imp-resumen').replaceChildren(...resumen.map(([n, t]) => el('li', {}, el('strong', { text: n }), ` ${t}`)));
}

function tarjeta(c) {
  const fecha = bloqueFecha(c.fechaInicio, c.fechaFin);
  const titulo = limpio(c.titulo);
  // Fichas migradas vía paginaMigrada; el resto (incluido Camp_Equipos.asp) sigue en la web actual.
  const legado = urlSegura(c.enlace);
  const href = legado ? (paginaMigrada(legado) ?? legado) : null;
  const externo = Boolean(c.nuevaVentana);
  const plazo = plazoVigente(c);
  const clubes = (Array.isArray(c.clubes) ? c.clubes : []).map(limpio).filter(Boolean);
  const lugar = [limpio(c.ciudad), clubes.join(' y ')].filter(Boolean).join(' · ');

  return el('article', { class: `camp-item${plazo ? ' camp-item--abierto' : ''}` },
    fecha
      ? el('p', { class: 'camp-item__date' },
        el('time', { class: 'event__days', datetime: fecha.iso, text: fecha.dias }),
        el('span', { class: 'event__month', text: fecha.mes }))
      : el('p', { class: 'camp-item__date', 'aria-hidden': 'true' }),
    el('div', { class: 'camp-item__body' },
      el('h3', { class: 'camp-item__title imp-item__title' }, href
        ? el('a', { href, target: externo ? '_blank' : null, rel: externo ? 'noopener' : null },
          titulo,
          externo ? el('span', { class: 'sr-only', text: ' (se abre en una pestaña nueva)' }) : null,
          externo ? el('span', { class: 'camp-item__ext', 'aria-hidden': 'true', text: ' ↗' }) : null)
        : titulo),
      lugar ? el('p', { class: 'camp-item__place', text: lugar }) : null,
      plazo
        ? el('p', { class: 'camp-item__plazo' },
          el('strong', { text: PLAZOS[plazo.tipo] }),
          ' hasta el ',
          el('time', { datetime: c.inscripcion.fechaLimite, text: fmtPlazo.format(plazo.fecha) }))
        : null));
}

/* ---------------------------------------------------------------------------
   Índice lateral: marca la sección visible
   --------------------------------------------------------------------------- */
function initIndice() {
  if (!('IntersectionObserver' in window)) return;
  const enlaces = new Map($$('#imp-indice a').map((a) => [a.dataset.seccion, a]));
  const obs = new IntersectionObserver((entradas) => {
    for (const e of entradas) {
      if (!e.isIntersecting) continue;
      enlaces.forEach((a, clave) => {
        const activo = clave === e.target.id;
        a.classList.toggle('is-active', activo);
        if (activo) a.setAttribute('aria-current', 'location'); else a.removeAttribute('aria-current');
      });
    }
  }, { rootMargin: '-35% 0px -55% 0px' });
  enlaces.forEach((_, clave) => { const s = document.getElementById(clave); if (s) obs.observe(s); });
}

/* ---------------------------------------------------------------------------
   Arranque
   --------------------------------------------------------------------------- */
function init() {
  leerUrl();
  initFormulario();
  initIndice();
  buscar();
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
})();
