/**
 * Calendarios de pruebas de las federaciones autonómicas puntuables para los rankings FEP
 * (migrado desde Calendario_Menores_FFAA.asp y Calendario_Veteranos_FFAA.asp).
 * Requiere assets/js/comun.js (cabecera, menú, pie y utilidades en window.FEP).
 *
 *   Contenido → GET /api/v1/competicion/calendarios-autonomicos/{categoria}
 *               (respaldo: mock/competicion-calendarios-autonomicos-{categoria}.json y mocks.js)
 *
 * La categoría (menores | veteranos) se lee de data-categoria en #cal-contenido, así que la misma página
 * sirve para los dos calendarios cambiando solo el HTML. Dos vistas de las pruebas:
 *   - "Por federación" (como en la web actual): una sección por federación con ancla (#andalucia…).
 *   - "Por fecha": todas las pruebas por mes de inicio (?vista=fecha).
 * Todo texto de la API va por textContent (el()).
 */
(function () {
'use strict';

const { $, $$, el, get, aFecha, bloqueFecha, fmtLargo } = window.FEP;

const CATEGORIAS = new Set(['menores', 'veteranos']);
const VISTAS = new Set(['federacion', 'fecha']);
const fmtMes = new Intl.DateTimeFormat('es-ES', { month: 'long', year: 'numeric' });
const fmtNum = new Intl.NumberFormat('es-ES');

const plural = (n, uno, varios) => `${fmtNum.format(n)} ${n === 1 ? uno : varios}`;
const mayuscula = (s) => (s ? s[0].toUpperCase() + s.slice(1) : s);
const limpio = (s) => (typeof s === 'string' ? s.replace(/\s+/g, ' ').trim() : '');

let datos = null;          // respuesta de la API ya filtrada
let vista = 'federacion';
let observador = null;     // IntersectionObserver del índice lateral

/* ---------------------------------------------------------------------------
   Utilidades
   --------------------------------------------------------------------------- */
/** Pruebas de todas las federaciones con su federación al lado, en orden cronológico (estable). */
function todasLasPruebas() {
  return datos.federaciones
    .flatMap((f) => f.pruebas.map((p, i) => ({ p, f, i })))
    .sort((a, b) => String(a.p.fechaInicio ?? '9999').localeCompare(String(b.p.fechaInicio ?? '9999'))
      || String(a.p.fechaFin ?? '').localeCompare(String(b.p.fechaFin ?? ''))
      || a.f.territorio.localeCompare(b.f.territorio, 'es') || a.i - b.i);
}

/** Pruebas cuya fecha de fin aún no ha pasado (hoy incluido). */
function porDisputar(pruebas) {
  const hoy = new Date();
  hoy.setHours(0, 0, 0, 0);
  return pruebas.filter(({ p }) => { const fin = aFecha(p.fechaFin ?? p.fechaInicio); return fin && fin >= hoy; });
}

function leerVista() {
  const v = new URLSearchParams(location.search).get('vista');
  return VISTAS.has(v) ? v : 'federacion';
}

function guardarVista() {
  const url = new URL(location.href);
  if (vista === 'federacion') url.searchParams.delete('vista'); else url.searchParams.set('vista', vista);
  url.hash = '';
  history.replaceState(null, '', url);
}

/* ---------------------------------------------------------------------------
   Presentación (reglamento, momentos de incorporación al ranking y participación)
   --------------------------------------------------------------------------- */
function pintarPresentacion() {
  const act = datos.actualizacionesRanking;
  const momentos = (act?.momentos ?? []).filter((m) => m && m.titulo);
  const nodos = [
    ...datos.introduccion.map((t) => el('p', { class: 'transp-text', text: t })),
    momentos.length ? el('div', { class: 'cal-steps' },
      act.texto ? el('p', { class: 'transp-text', text: act.texto }) : null,
      el('ol', { class: 'cpe-steps' }, momentos.map((m) => {
        const f = aFecha(m.fecha);
        return el('li', { class: 'cpe-step' },
          el('div', { class: 'cpe-step__body' },
            el('p', { class: 'cal-step__title', text: m.titulo }),
            m.descripcion ? el('p', { class: 'cpe-step__note', text: m.descripcion }) : null,
            (f || m.maximoTorneos) ? el('p', { class: 'cal-step__meta' },
              m.maximoTorneos ? el('span', { class: 'org-tag', text: `Hasta ${plural(m.maximoTorneos, 'torneo', 'torneos')}` }) : null,
              f ? el('span', { class: 'org-tag org-tag--red' }, 'Actualización: ', el('time', { datetime: m.fecha, text: fmtLargo.format(f) })) : null) : null));
      }))) : null,
    datos.participacion ? el('p', { class: 'cpe-alert' }, el('strong', { text: 'Participación. ' }), datos.participacion) : null,
  ].filter(Boolean);

  if (!nodos.length) return;
  $('#cal-presentacion').replaceChildren(...nodos);
  $('#presentacion').hidden = false;
}

/* ---------------------------------------------------------------------------
   Pruebas
   --------------------------------------------------------------------------- */
function etiquetas(p) {
  return [
    p.enRanking ? el('li', {}, el('span', { class: 'org-tag cal-tag--ranking', text: 'En el ranking' })) : null,
    p.campeonatoAutonomico ? el('li', {}, el('span', { class: 'org-tag cal-tag--campeonato', text: 'Campeonato autonómico' })) : null,
  ].filter(Boolean);
}

/** Tarjeta de una prueba (reutiliza .camp-item de Campeonatos). Con conFederacion, indica de qué federación es. */
function tarjeta(p, f, conFederacion) {
  const fecha = bloqueFecha(p.fechaInicio, p.fechaFin);
  const nombre = limpio(p.nombre);
  const sede = limpio(p.sede);
  const titulo = nombre || sede || (p.enRanking ? 'Prueba puntuable' : 'Prueba autonómica');
  const lugar = [conFederacion ? f.territorio : null, nombre ? sede : null].filter(Boolean).join(' · ');
  const fechas = limpio(p.fechas);
  const tags = etiquetas(p);
  const fases = (p.fases ?? []).filter((x) => x && x.fechas);

  return el('article', { class: 'camp-item cal-item' },
    fecha
      ? el('p', { class: 'camp-item__date' },
        el('time', { class: 'event__days', datetime: fecha.iso, text: fecha.dias }),
        el('span', { class: 'event__month', text: fecha.mes }))
      : el('p', { class: 'camp-item__date', 'aria-hidden': 'true' }),
    el('div', { class: 'camp-item__body' },
      tags.length ? el('ul', { class: 'camp-item__tags', 'aria-label': 'Características' }, tags) : null,
      el('h4', { class: 'camp-item__title', text: titulo }),
      // Fechas tal como las da la federación: solo si dicen algo que el bloque de fecha no (días sueltos, fases).
      fechas.includes(',') ? el('p', { class: 'cal-item__fechas', text: fechas }) : null,
      lugar ? el('p', { class: 'camp-item__place', text: lugar }) : null,
      fases.length ? el('ul', { class: 'cal-fases', 'aria-label': 'Fases' }, fases.map((x) => el('li', {},
        el('strong', { text: limpio(x.fechas) }),
        [limpio(x.descripcion), limpio(x.sede)].filter(Boolean).map((t) => ` · ${t}`).join('')))) : null));
}

function seccionFederacion(f) {
  const idTitulo = `${f.clave}-titulo`;
  const ficha = Number.isInteger(f.idFederacion) ? `federacion.html?id=${f.idFederacion}` : null;
  return el('section', { class: 'cal-fed', id: f.clave, 'aria-labelledby': idTitulo },
    el('header', { class: 'cal-fed__head' },
      el('div', {},
        el('h3', { class: 'cal-fed__title', id: idTitulo }, f.territorio,
          el('span', { class: 'org-count', text: fmtNum.format(f.pruebas.length) })),
        f.nombre && f.nombre !== f.territorio ? el('p', { class: 'cal-fed__name', text: f.nombre }) : null),
      ficha ? el('a', { class: 'cal-fed__link', href: ficha }, 'Ficha de la federación ', el('span', { 'aria-hidden': 'true', text: '→' })) : null),
    f.pruebas.length
      ? el('ul', { class: 'camp-items' }, f.pruebas.map((p) => el('li', {}, tarjeta(p, f, false))))
      : el('p', { class: 'cal-fed__empty', text: 'Esta federación no ha incluido pruebas en el calendario de la temporada.' }));
}

/** Grupos por mes de inicio: [[clave, título, [{p, f}]]] en orden cronológico. */
function agruparPorMes() {
  const grupos = new Map();
  for (const item of todasLasPruebas()) {
    const d = aFecha(item.p.fechaInicio);
    const clave = d ? `mes-${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}` : 'sin-fecha';
    if (!grupos.has(clave)) grupos.set(clave, { titulo: d ? mayuscula(fmtMes.format(d)) : 'Sin fecha', items: [] });
    grupos.get(clave).items.push(item);
  }
  return [...grupos].map(([clave, g]) => [clave, g.titulo, g.items]);
}

function pintarPruebas() {
  const lista = $('#cal-lista');
  let entradas;
  if (vista === 'fecha') {
    const meses = agruparPorMes();
    lista.replaceChildren(...meses.map(([clave, titulo, items]) => el('section', { class: 'camp-month cal-month', id: clave, 'aria-labelledby': `${clave}-titulo` },
      el('h3', { class: 'camp-month__title', id: `${clave}-titulo` }, titulo, ' ', el('span', { class: 'org-count', text: fmtNum.format(items.length) })),
      el('ul', { class: 'camp-items' }, items.map(({ p, f }) => el('li', {}, tarjeta(p, f, true)))))));
    entradas = meses.map(([clave, titulo, items]) => ({ id: clave, texto: titulo, n: items.length }));
  } else {
    lista.replaceChildren(...datos.federaciones.map(seccionFederacion));
    entradas = datos.federaciones.map((f) => ({ id: f.clave, texto: f.territorio, n: f.pruebas.length }));
  }

  $$('.cal-view .chip').forEach((b) => {
    const activo = b.dataset.vista === vista;
    b.classList.toggle('is-active', activo);
    b.setAttribute('aria-pressed', String(activo));
  });
  const total = datos.federaciones.reduce((s, f) => s + f.pruebas.length, 0);
  $('#cal-estado').textContent = vista === 'fecha'
    ? `${plural(total, 'prueba', 'pruebas')} ordenadas por fecha.`
    : `${plural(total, 'prueba', 'pruebas')} agrupadas por federación.`;
  pintarIndice(entradas);
}

function pintarLeyenda() {
  $('#cal-leyenda').replaceChildren(
    el('li', {}, el('span', { class: 'org-tag cal-tag--ranking', text: 'En el ranking' }), ' ',
      datos.leyenda || `Incluida actualmente en el ${datos.ranking || 'ranking FEP'}.`),
    el('li', {}, el('span', { class: 'org-tag cal-tag--campeonato', text: 'Campeonato autonómico' }), ' ',
      'Otorga el título de campeón autonómico: consulta a la federación los criterios de participación.'));
}

/* ---------------------------------------------------------------------------
   Índice lateral y resumen
   --------------------------------------------------------------------------- */
function pintarIndice(entradas) {
  const items = [
    $('#presentacion').hidden ? null : { id: 'presentacion', texto: 'Cómo puntúan', n: null },
    ...entradas,
  ].filter(Boolean);
  $('#cal-indice').replaceChildren(...items.map((e) => el('li', {},
    el('a', { href: `#${e.id}`, dataset: { clave: e.id } },
      el('span', { text: e.texto }),
      e.n == null ? null : el('span', { class: 'page-toc__count', text: fmtNum.format(e.n) })))));

  // Marca en el índice la sección visible.
  observador?.disconnect();
  if (!('IntersectionObserver' in window)) return;
  const enlaces = new Map($$('#cal-indice a').map((a) => [a.dataset.clave, a]));
  observador = new IntersectionObserver((obs) => {
    for (const e of obs) {
      if (!e.isIntersecting) continue;
      enlaces.forEach((a, clave) => {
        const activo = clave === e.target.id;
        a.classList.toggle('is-active', activo);
        if (activo) a.setAttribute('aria-current', 'location'); else a.removeAttribute('aria-current');
      });
    }
  }, { rootMargin: '-35% 0px -55% 0px' });
  items.forEach((e) => { const s = document.getElementById(e.id); if (s) observador.observe(s); });
}

function pintarResumen() {
  const pruebas = todasLasPruebas();
  const conPruebas = datos.federaciones.filter((f) => f.pruebas.length).length;
  const enRanking = pruebas.filter(({ p }) => p.enRanking).length;
  const campeonatos = pruebas.filter(({ p }) => p.campeonatoAutonomico).length;
  const pendientes = porDisputar(pruebas).length;
  const resumen = [
    [conPruebas, 'federaciones'],
    [pruebas.length, 'pruebas'],
    [enRanking, 'en el ranking'],
    campeonatos ? [campeonatos, campeonatos === 1 ? 'campeonato autonómico' : 'campeonatos autonómicos'] : null,
    pendientes ? [pendientes, 'por disputar'] : null,
  ].filter(Boolean);
  $('#cal-resumen').replaceChildren(...resumen.map(([n, t]) => el('li', {}, el('strong', { text: fmtNum.format(n) }), ` ${t}`)));
}

/* ---------------------------------------------------------------------------
   Arranque
   --------------------------------------------------------------------------- */
/** Descarta federaciones o pruebas mal formadas y garantiza listas. */
function normalizar(r) {
  const federaciones = (Array.isArray(r?.federaciones) ? r.federaciones : [])
    .filter((f) => f && f.clave && (f.territorio || f.nombre))
    .map((f) => ({
      ...f,
      territorio: limpio(f.territorio) || limpio(f.nombre),
      pruebas: (Array.isArray(f.pruebas) ? f.pruebas : []).filter((p) => p && (p.fechas || p.fechaInicio)),
    }));
  return {
    ...r,
    introduccion: (Array.isArray(r?.introduccion) ? r.introduccion : []).filter((t) => typeof t === 'string' && t.trim()),
    federaciones,
  };
}

async function init() {
  const cont = $('#cal-contenido');
  const categoria = (cont.dataset.categoria || '').toLowerCase();
  try {
    if (!CATEGORIAS.has(categoria)) throw new Error(`Categoría no válida: ${categoria}`);
    datos = normalizar(await get(`/api/v1/competicion/calendarios-autonomicos/${categoria}`));
    if (!datos.federaciones.length) throw new Error('Respuesta sin federaciones');

    $('#cal-skeleton')?.remove();
    if (datos.temporada) $('#cal-temporada').textContent = String(datos.temporada);
    $('#cal-total').textContent = fmtNum.format(datos.federaciones.reduce((s, f) => s + f.pruebas.length, 0));
    pintarPresentacion();
    pintarLeyenda();
    $('#pruebas').hidden = false;

    vista = leerVista();
    pintarPruebas();
    pintarResumen();
    $$('.cal-view .chip').forEach((b) => b.addEventListener('click', () => {
      if (b.dataset.vista === vista) return;
      vista = b.dataset.vista;
      guardarVista();
      pintarPruebas();
    }));

    const act = aFecha(datos.actualizado);
    if (act) {
      const nota = $('#cal-actualizado');
      nota.replaceChildren('Información actualizada el ', el('time', { datetime: datos.actualizado, text: fmtLargo.format(act) }), '.');
      nota.hidden = false;
    }

    // Si se llega con un ancla (#andalucia…), desplazarse ahora que la sección existe.
    if (location.hash) document.getElementById(decodeURIComponent(location.hash.slice(1)))?.scrollIntoView();
  } catch (e) {
    console.warn('[FEP] calendario autonómico:', e.message);
    $('#cal-skeleton')?.remove();
    $('#cal-error').hidden = false;
  } finally {
    cont.setAttribute('aria-busy', 'false');
  }
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
})();
