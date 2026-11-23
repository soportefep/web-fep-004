/**
 * Página "Ranking de Equipos Absolutos" (migrada desde Ranking_Equipos_Absolutos.asp, que pintaba con 32 consultas el
 * resultado de los Campeonatos Absolutos por equipos de 2017 a 2024, una temporada debajo de otra).
 * Requiere assets/js/comun.js (cabecera, menú, pie y utilidades en window.FEP).
 *
 *   Clasificación → GET /api/v1/competicion/rankings/equipos-absolutos
 *                   (respaldo: mock/competicion-rankings-equipos-absolutos.json)
 *
 * Una sola petición con todas las temporadas (~250 equipos): el selector de temporada cambia la vista sin volver a pedir
 * nada. Por defecto, la más reciente; la elegida viaja en la URL (?anio=2023). Debajo, el historial de campeones de cada
 * temporada y categoría (calculado con los mismos datos).
 *
 * Escudos (refs/images/{club}.jpg): muchos clubes no tienen (404 en la web actual, que pintaba la imagen rota); si no
 * carga, se sustituye por las iniciales del equipo. La ficha del club (Club.asp) sigue en la web actual.
 * Todo texto de la API va por textContent.
 */
(function () {
'use strict';

const { $, $$, el, get, urlSegura, paginaMigrada } = window.FEP;

const RUTA = '/api/v1/competicion/rankings/equipos-absolutos';
const ANIO = /^\d{4}$/;
const PARTICULAS = new Set(['de', 'del', 'la', 'las', 'los', 'y', 'i', 'e', 'el']);
const CATEGORIAS = [1, 2];
const GENEROS = ['M', 'F'];

let datos = null;              // respuesta de la API (temporadas válidas)
let anioActivo = null;

/* ---------------------------------------------------------------------------
   Utilidades
   --------------------------------------------------------------------------- */
const plural = (n, uno, varios) => `${n} ${n === 1 ? uno : varios}`;

/** "BARCELONA" → "Barcelona", "LAS PALMAS" → "Las Palmas" (las provincias llegan en mayúsculas de la BD). */
function nombrePropio(texto) {
  return String(texto ?? '').toLocaleLowerCase('es-ES')
    .replace(/[\p{L}\p{N}]+/gu, (p, i) => (i > 0 && PARTICULAS.has(p) ? p : p.charAt(0).toLocaleUpperCase('es-ES') + p.slice(1)));
}

/** Iniciales para el hueco del escudo ("REAL CLUB DE POLO BARCELONA" → "RC"). */
function iniciales(equipo) {
  const palabras = String(equipo ?? '').split(/[\s.\-]+/).filter((p) => /^[\p{L}\p{N}]/u.test(p) && !PARTICULAS.has(p.toLowerCase()));
  return palabras.slice(0, 2).map((p) => p.charAt(0)).join('').toLocaleUpperCase('es-ES') || '·';
}

/** Ficha del club: la página nueva si ya está migrada; si no, la web actual. */
function urlClub(p) {
  const url = urlSegura(p.clubUrl);
  return url ? (paginaMigrada(url) ?? url) : null;
}

/** Imagen absoluta de la API (http/https) o null. */
function urlImagen(u) {
  const url = urlSegura(u);
  return url && /^https?:/i.test(url) ? url : null;
}

const listaValida = (l) => l && Number.isInteger(l.categoria) && typeof l.genero === 'string' && typeof l.titulo === 'string';
const puestoValido = (p) => p && Number.isInteger(p.puesto) && typeof p.equipo === 'string' && p.equipo.trim();

/** Normaliza la respuesta: temporadas con año, listas y puestos válidos, de la más reciente a la más antigua. */
function normalizar(respuesta) {
  const temporadas = (respuesta?.temporadas ?? [])
    .filter((t) => t && Number.isInteger(t.anio))
    .map((t) => ({
      anio: t.anio,
      listas: (t.listas ?? []).filter(listaValida)
        .map((l) => ({ ...l, puestos: (l.puestos ?? []).filter(puestoValido).sort((a, b) => a.puesto - b.puesto) }))
        .filter((l) => l.puestos.length),
    }))
    .filter((t) => t.listas.length)
    .sort((a, b) => b.anio - a.anio);
  return { temporadas };
}

/* ---------------------------------------------------------------------------
   Estado ↔ URL
   --------------------------------------------------------------------------- */
function anioDeUrl() {
  const v = (new URLSearchParams(location.search).get('anio') ?? '').trim();
  return ANIO.test(v) ? Number(v) : null;
}

function escribirUrl() {
  const q = new URLSearchParams(location.search);
  if (anioActivo && anioActivo !== datos.temporadas[0]?.anio) q.set('anio', anioActivo); else q.delete('anio');
  const s = q.toString();
  try {
    history.replaceState(null, '', `${location.pathname}${s ? `?${s}` : ''}${location.hash}`);
  } catch {
    // Abierta desde disco (file://) algunos navegadores no permiten cambiar la URL: no es imprescindible.
  }
}

/* ---------------------------------------------------------------------------
   Selector de temporada
   --------------------------------------------------------------------------- */
function pintarAnios() {
  $('#rke-anios').replaceChildren(...datos.temporadas.map((t) => el('label', { class: 'rkc-genero' },
    el('input', { type: 'radio', name: 'anio', value: t.anio, checked: t.anio === anioActivo }),
    el('span', { text: String(t.anio) }))));
}

function initSelector() {
  $('#rke-form').addEventListener('submit', (e) => e.preventDefault());
  $('#rke-anios').addEventListener('change', (e) => {
    const anio = Number(e.target?.value);
    if (!datos.temporadas.some((t) => t.anio === anio)) return;
    anioActivo = anio;
    escribirUrl();
    pintarTemporada();
  });
}

/* ---------------------------------------------------------------------------
   Clasificación de una temporada
   --------------------------------------------------------------------------- */
function escudo(p) {
  const hueco = () => el('span', { class: 'rke-escudo rke-escudo--vacio', 'aria-hidden': 'true', text: iniciales(p.equipo) });
  const src = urlImagen(p.escudoUrl);
  if (!src) return hueco();
  // alt vacío: el nombre del equipo va al lado. Si el club no tiene escudo (404), se pinta el hueco con iniciales.
  const i = el('img', { class: 'rke-escudo', src, alt: '', width: 32, height: 32, loading: 'lazy', decoding: 'async' });
  i.addEventListener('error', () => i.replaceWith(hueco()), { once: true });
  return i;
}

function celdaFederacion(f) {
  if (!f || !Number.isInteger(f.id) || !f.territorio) return el('span', { class: 'org-muted', text: '—' });
  const bandera = urlImagen(f.banderaUrl);
  const img = bandera ? el('img', { class: 'rke-bandera', src: bandera, alt: '', width: 30, height: 20, loading: 'lazy', decoding: 'async' }) : null;
  img?.addEventListener('error', () => img.remove(), { once: true });
  return el('a', { class: 'rke-fed', href: `federacion.html?id=${f.id}`, title: f.nombre || null },
    img, el('span', { text: f.territorio }));
}

function filaPuesto(p) {
  const url = urlClub(p);
  return el('tr', {},
    el('td', { class: 'rkc-td rkc-td--pos', 'data-label': 'Puesto' },
      el('span', { class: p.puesto <= 3 ? 'rkc-pos rkc-pos--podio' : 'rkc-pos', text: `${p.puesto}.º` })),
    el('th', { scope: 'row', class: 'rkc-td rkc-td--nombre org-table__name' },
      el('span', { class: 'rke-equipo' }, escudo(p),
        url ? el('a', { href: url, text: p.equipo, title: 'Ver la ficha del club (web actual de la FEP)' }) : el('span', { text: p.equipo }))),
    el('td', { class: 'rkc-td rke-td--fed', 'data-label': 'Federación' }, celdaFederacion(p.federacion)),
    el('td', { class: 'rkc-td rke-td--prov', 'data-label': 'Provincia', text: p.provincia ? nombrePropio(p.provincia) : '—' }));
}

function bloqueLista(anio, l) {
  const id = `lista-${anio}-${l.categoria}${l.genero.toLowerCase()}`;
  const titulo = l.titulo.charAt(0).toLocaleUpperCase('es-ES') + l.titulo.slice(1);
  return el('section', { class: 'rkc-lista', id, 'aria-labelledby': `${id}-titulo` },
    el('div', { class: 'rkc-lista__head' },
      el('h3', { class: 'cpe-subtitle rkc-lista__title', id: `${id}-titulo` }, titulo,
        el('span', { class: 'org-count', text: plural(l.puestos.length, 'equipo', 'equipos') }))),
    el('div', { class: 'rkc-table-wrap' },
      el('table', { class: 'org-table rkc-table rke-table' },
        el('caption', { class: 'sr-only', text: `Clasificación ${anio}, ${l.titulo}` }),
        el('thead', {}, el('tr', {},
          el('th', { scope: 'col', class: 'rkc-th--pos', text: 'Puesto' }),
          el('th', { scope: 'col', text: 'Equipo' }),
          el('th', { scope: 'col', text: 'Federación' }),
          el('th', { scope: 'col', text: 'Provincia' }))),
        el('tbody', {}, l.puestos.map(filaPuesto)))));
}

function pintarTemporada() {
  const t = datos.temporadas.find((x) => x.anio === anioActivo);
  $('#rke-skeleton').hidden = true;
  $('#rke-listas').replaceChildren(...(t ? t.listas.map((l) => bloqueLista(t.anio, l)) : []));
  $('#rke-vacio').hidden = Boolean(t);
  $('#rke-estado').textContent = t
    ? `Temporada ${t.anio}: ${plural(t.listas.reduce((n, l) => n + l.puestos.length, 0), 'equipo clasificado', 'equipos clasificados')} en ${plural(t.listas.length, 'categoría', 'categorías')}.`
    : 'No hay clasificación publicada para esa temporada.';
}

/* ---------------------------------------------------------------------------
   Campeones por temporada
   --------------------------------------------------------------------------- */
function pintarCampeones() {
  const columnas = CATEGORIAS.flatMap((c) => GENEROS.map((g) => ({ c, g })))
    .filter(({ c, g }) => datos.temporadas.some((t) => t.listas.some((l) => l.categoria === c && l.genero === g)));
  const titulos = new Map();
  datos.temporadas.forEach((t) => t.listas.forEach((l) => titulos.set(`${l.categoria}${l.genero}`, l.titulo)));
  const nombreColumna = ({ c, g }) => {
    const t = titulos.get(`${c}${g}`) ?? '';
    return t.charAt(0).toLocaleUpperCase('es-ES') + t.slice(1);
  };

  const celda = (t, { c, g }) => {
    const l = t.listas.find((x) => x.categoria === c && x.genero === g);
    const primeros = (l?.puestos ?? []).filter((p) => p.puesto === 1);
    if (!primeros.length) return el('td', { 'data-label': nombreColumna({ c, g }) }, el('span', { class: 'org-muted', text: '—' }));
    return el('td', { 'data-label': nombreColumna({ c, g }) }, primeros.flatMap((p, i) => {
      const url = urlClub(p);
      return [i > 0 ? el('br') : null, url ? el('a', { href: url, text: p.equipo }) : p.equipo];
    }));
  };

  $('#campeones-contenido').replaceChildren(el('div', { class: 'rkc-table-wrap' },
    el('table', { class: 'org-table rke-campeones' },
      el('caption', { class: 'sr-only', text: 'Campeones de los Campeonatos Absolutos por equipos, por temporada y categoría' }),
      el('thead', {}, el('tr', {},
        el('th', { scope: 'col', text: 'Temporada' }),
        columnas.map((col) => el('th', { scope: 'col', text: nombreColumna(col) })))),
      el('tbody', {}, datos.temporadas.map((t) => el('tr', {},
        el('th', { scope: 'row', class: 'rke-campeones__anio' },
          el('a', { href: `?anio=${t.anio}#clasificacion`, text: String(t.anio), 'aria-label': `Temporada ${t.anio}: ver la clasificación completa`, dataset: { anio: String(t.anio) } })),
        columnas.map((col) => celda(t, col))))))));

  // Los enlaces de año cambian la temporada sin recargar la página.
  $('#campeones-contenido').addEventListener('click', (e) => {
    const a = e.target.closest('a[data-anio]');
    if (!a) return;
    e.preventDefault();
    anioActivo = Number(a.dataset.anio);
    pintarAnios();
    escribirUrl();
    pintarTemporada();
    $('#clasificacion').scrollIntoView({ block: 'start' });
  });
}

/* ---------------------------------------------------------------------------
   Resumen (cabecera)
   --------------------------------------------------------------------------- */
function pintarResumen() {
  const ts = datos.temporadas;
  if (!ts.length) return;
  const clubes = new Set(ts.flatMap((t) => t.listas.flatMap((l) => l.puestos.map((p) => p.club || p.equipo))));
  const items = [
    [String(ts.length), ts.length === 1 ? 'temporada' : `temporadas (${ts[ts.length - 1].anio}–${ts[0].anio})`],
    [String(clubes.size), clubes.size === 1 ? 'club clasificado' : 'clubes clasificados'],
    [String(ts[0].anio), 'última temporada publicada'],
  ];
  $('#rke-resumen').replaceChildren(...items.map(([n, t]) => el('li', {}, el('strong', { text: n }), ` ${t}`)));
}

/* ---------------------------------------------------------------------------
   Índice lateral
   --------------------------------------------------------------------------- */
function initIndice() {
  if (!('IntersectionObserver' in window)) return;
  const enlaces = new Map($$('#rke-indice a').map((a) => [a.dataset.seccion, a]));
  const obs = new IntersectionObserver((vistas) => {
    for (const v of vistas) {
      if (!v.isIntersecting) continue;
      enlaces.forEach((a, id) => {
        const activo = id === v.target.id;
        a.classList.toggle('is-active', activo);
        if (activo) a.setAttribute('aria-current', 'location'); else a.removeAttribute('aria-current');
      });
    }
  }, { rootMargin: '-35% 0px -55% 0px' });
  enlaces.forEach((_, id) => { const s = document.getElementById(id); if (s) obs.observe(s); });
}

/* ---------------------------------------------------------------------------
   Arranque
   --------------------------------------------------------------------------- */
function mostrarError(e) {
  console.warn('[FEP] ranking de equipos:', e?.message ?? e);
  $('#rke-skeleton').hidden = true;
  $('#rke-error').hidden = false;
  $('#rke-estado').textContent = 'No se ha podido cargar la clasificación.';
  $('#campeones-skeleton').hidden = true;
  $('#campeones-error').hidden = false;
}

async function init() {
  initIndice();
  initSelector();
  try {
    datos = normalizar(await get(RUTA));
    if (!datos.temporadas.length) throw new Error('Sin temporadas');
    const pedido = anioDeUrl();
    anioActivo = datos.temporadas.some((t) => t.anio === pedido) ? pedido : datos.temporadas[0].anio;
    pintarAnios();
    pintarTemporada();
    pintarCampeones();
    pintarResumen();
    escribirUrl();
    $('#campeones-skeleton').hidden = true;
    if (location.hash) document.getElementById(decodeURIComponent(location.hash.slice(1)))?.scrollIntoView();
  } catch (e) {
    mostrarError(e);
  } finally {
    $('#rke-resultados').setAttribute('aria-busy', 'false');
    $('#campeones').setAttribute('aria-busy', 'false');
  }
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
})();
