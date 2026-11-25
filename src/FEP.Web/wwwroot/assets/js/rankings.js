/**
 * Página "Rankings" (migrada desde Rankings.asp): índice de los rankings publicados por la FEP.
 * Requiere assets/js/comun.js (cabecera, menú, pie y utilidades en window.FEP).
 *
 *   Contenido → GET /api/v1/competicion/rankings
 *               (respaldo: mock/competicion-rankings.json y mocks.js)
 *
 * Una tarjeta por ranking (imagen + título), con ancla #ranking-{clave}.
 * Destino de cada tarjeta:
 *   1. si la página del ranking ya está migrada (PAGINAS_MIGRADAS de comun.js), la página nueva;
 *   2. si es un Ranking_*.asp de la web actual, la página nueva previsible ranking-{…}.html
 *      (SUPUESTO para las que aún no están migradas; hoy lo están los cinco rankings del índice);
 *   3. si no, el enlace tal cual (los de otro dominio, en pestaña nueva).
 * Todo texto de la API va por textContent.
 */
(function () {
'use strict';

const { $, $$, el, get, comoLista, urlSegura, paginaMigrada } = window.FEP;

/** Ranking_Xxx_Yyy(.asp) en la raíz de la web actual. */
const RUTA_RANKING = /^\/ranking_([a-z0-9]+(?:_[a-z0-9]+)*)(?:\.asp)?\/?$/i;
const CLAVE = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

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

/** Página nueva previsible de un Ranking_*.asp: Ranking_Silla_Ruedas → ranking-silla-ruedas.html. */
function paginaPrevista(url) {
  if (!/(^|\.)padelfederacion\.es$/i.test(url.hostname)) return null;
  const m = RUTA_RANKING.exec(url.pathname);
  return m ? `ranking-${m[1].toLowerCase().replace(/_/g, '-')}.html` : null;
}

/** { href, nueva } del ranking o null si no tiene enlace utilizable. */
function destino(r) {
  const href = urlSegura(r.enlaceUrl) ?? urlSegura(r.enlace);
  if (!href || href.startsWith('mailto:')) return null;
  const migrada = paginaMigrada(href);
  if (migrada) return { href: migrada, nueva: false };
  const url = new URL(href);
  const prevista = paginaPrevista(url);
  if (prevista) return { href: prevista, nueva: false };
  const propia = /(^|\.)padelfederacion\.es$/i.test(url.hostname);
  return { href, nueva: !propia && r.nuevaVentana === true };
}

const plural = (n, uno, varios) => `${n} ${n === 1 ? uno : varios}`;

/* ---------------------------------------------------------------------------
   Rankings
   --------------------------------------------------------------------------- */
/** Etiqueta de categoría que se muestra sobre la imagen (por la clave del ranking). */
const CATEGORIAS = {
  absoluto: 'Absoluto', menores: 'Menores', 'equipos-absolutos': 'Equipos',
  'silla-ruedas': 'Pádel adaptado', veteranos: 'Veteranos',
};

function tarjeta(r) {
  const imagen = urlImagen(r.imagenUrl);
  const ir = destino(r);
  const idTitulo = `ranking-${r.clave}-titulo`;
  const categoria = CATEGORIAS[r.clave];
  const contenido = [
    // La imagen solo repite el título sobre fondo negro: decorativa. La tarjeta es del mismo
    // negro, así la imagen se funde con ella.
    el('div', { class: 'rnk-card__media' },
      imagen
        ? el('img', { class: 'rnk-card__img', src: imagen, alt: '', width: 228, height: 169, loading: 'lazy', decoding: 'async' })
        : el('span', { class: 'rnk-card__img', 'aria-hidden': 'true' }),
      categoria ? el('span', { class: 'rnk-card__chip', text: categoria }) : null),
    el('div', { class: 'rnk-card__body' },
      el('div', {},
        el('h3', { class: 'rnk-card__title', id: idTitulo, text: r.titulo }),
        el('p', { class: 'rnk-card__sub', text: 'Clasificación oficial FEP' })),
      ir ? el('span', { class: 'rnk-card__go', 'aria-hidden': 'true', text: ir.nueva ? '↗' : '→' }) : null,
      ir ? el('span', { class: 'sr-only', text: 'Ver ranking' }) : null),
  ];
  const caja = ir
    ? el('a', { class: 'rnk-card', href: ir.href, target: ir.nueva ? '_blank' : null, rel: ir.nueva ? 'noopener' : null },
      ...contenido, ir.nueva ? el('span', { class: 'sr-only', text: ' (se abre en una pestaña nueva)' }) : null)
    : el('div', { class: 'rnk-card rnk-card--sin-enlace' }, ...contenido);
  return el('li', { class: 'rnk-grid__item', id: `ranking-${r.clave}` }, caja);
}

function seccionRankings(rankings) {
  const idTitulo = 'rankings-titulo';
  return el('section', { class: 'organo', id: 'rankings', 'aria-labelledby': idTitulo },
    el('header', { class: 'organo__head' },
      el('p', { class: 'organo__num', 'aria-hidden': 'true', text: '01' }),
      el('div', {},
        el('h2', { class: 'h2 organo__title', id: idTitulo, text: 'Rankings FEP' }),
        el('p', { class: 'organo__meta', text: plural(rankings.length, 'ranking publicado', 'rankings publicados') }))),
    el('ul', { class: 'rnk-grid' }, rankings.map((r) => tarjeta(r))));
}

/* ---------------------------------------------------------------------------
   Índice lateral
   --------------------------------------------------------------------------- */
function pintarIndice(rankings) {
  $('#rankings-indice').replaceChildren(...rankings.map((r) => el('li', {},
    el('a', { href: `#ranking-${r.clave}`, dataset: { clave: `ranking-${r.clave}` } }, el('span', { text: r.titulo })))));

  if (!('IntersectionObserver' in window)) return;
  const enlaces = new Map($$('#rankings-indice a').map((a) => [a.dataset.clave, a]));
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
  rankings.forEach((r) => { const s = document.getElementById(`ranking-${r.clave}`); if (s) obs.observe(s); });
}

/* ---------------------------------------------------------------------------
   Arranque
   --------------------------------------------------------------------------- */
async function init() {
  const cont = $('#rankings-contenido');
  try {
    const vistas = new Set();
    const rankings = comoLista(await get('/api/v1/competicion/rankings'))
      .filter((r) => r && typeof r.titulo === 'string' && r.titulo.trim()
        && typeof r.clave === 'string' && CLAVE.test(r.clave) && !vistas.has(r.clave) && vistas.add(r.clave))
      .sort((a, b) => (a.orden ?? 0) - (b.orden ?? 0));
    if (!rankings.length) throw new Error('Respuesta sin rankings');

    $('.org-skeleton', cont)?.remove();
    cont.prepend(seccionRankings(rankings));
    pintarIndice(rankings);
    $('#rankings-resumen').replaceChildren(
      el('li', {}, el('strong', { text: String(rankings.length) }), ` ${rankings.length === 1 ? 'ranking' : 'rankings'}`));

    if (location.hash) document.getElementById(decodeURIComponent(location.hash.slice(1)))?.scrollIntoView();
  } catch (e) {
    console.warn('[FEP] rankings:', e.message);
    $('.org-skeleton', cont)?.remove();
    $('#rankings-error').hidden = false;
  } finally {
    cont.setAttribute('aria-busy', 'false');
  }
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
})();
