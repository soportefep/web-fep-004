/**
 * Página "Equipo FEP" (migrada desde Equipo_FEP.asp).
 * Requiere assets/js/comun.js (cabecera, menú, pie y utilidades en window.FEP).
 *
 *   Contenido → GET /api/v1/federacion/equipo
 *               (respaldo: mock/federacion-equipo.json y mocks.js)
 *
 * Cada área se pinta como una sección con ancla (#organigrama, #menores) con su organigrama en
 * HTML/CSS (estilos .ochart* compartidos con Órganos de gobierno), construido con las personas
 * transcritas de la imagen original, que sigue enlazada ("Ver el organigrama oficial en imagen").
 * Todo texto de la API va por textContent.
 */
(function () {
'use strict';

const { $, $$, el, get, urlSegura, aFecha } = window.FEP;

const fmtFecha = new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'long', year: 'numeric' });
const PARTICULAS = new Set(['de', 'del', 'la', 'las', 'los', 'y', 'e', 'i', 'san']);

/* ---------------------------------------------------------------------------
   Utilidades
   --------------------------------------------------------------------------- */
/** Iniciales para el avatar: "José Luis Gutiérrez del Álamo" → "JL". */
function iniciales(nombre) {
  const palabras = String(nombre ?? '').trim().split(/[\s-]+/)
    .filter((p) => p && !PARTICULAS.has(p.toLowerCase()) && /\p{L}/u.test(p));
  return palabras.slice(0, 2).map((p) => p[0].toUpperCase()).join('') || '·';
}

/**
 * URL de una imagen: las absolutas (API real) se validan con urlSegura; las relativas (mocks,
 * p. ej. "assets/img/fep/equipo/…") se usan tal cual, relativas a esta página.
 */
function urlImagen(ruta) {
  if (!ruta || typeof ruta !== 'string') return null;
  const limpia = ruta.trim();
  if (/^https?:\/\//i.test(limpia)) return urlSegura(limpia);
  if (/^[a-z][a-z0-9+.-]*:|^\/\//i.test(limpia)) return null; // javascript:, data:, //host… no
  return limpia;
}

const plural = (n, uno, varios) => `${n} ${n === 1 ? uno : varios}`;
const totalPersonas = (a) => a.grupos.reduce((s, g) => s + (g.miembros?.length ?? 0), 0);

/* ---------------------------------------------------------------------------
   Grupos y áreas
   --------------------------------------------------------------------------- */
function grupo(g, idArea) {
  const id = `${idArea}-${g.clave}`;
  return el('section', { class: 'org-group', 'aria-labelledby': id },
    el('h3', { class: 'org-group__title', id }, g.titulo,
      el('span', { class: 'org-count', text: String(g.miembros.length) })),
    el('ul', { class: 'person-grid' }, g.miembros.map((m) => el('li', { class: 'person-card' },
      el('span', { class: 'person-card__avatar', 'aria-hidden': 'true', text: iniciales(m.nombre) }),
      el('div', { class: 'person-card__body' },
        m.cargo ? el('p', { class: 'person-card__role', text: m.cargo }) : null,
        el('p', { class: 'person-card__name', text: m.nombre }))))));
}

/* ---------------------------------------------------------------------------
   Organigramas en HTML/CSS (sustituyen a las imágenes PNG de la web antigua).
   Reutilizan los estilos .ochart* de Órganos de gobierno.
   --------------------------------------------------------------------------- */
const ORGANOS = 'organos-de-gobierno.html';

/** Persona del organigrama. tipo: cabeza | rama | hoja. */
function nodo(m, tipo) {
  return el(m.href ? 'a' : 'div', { class: `ochart-node ochart-node--${tipo}`, href: m.href },
    tipo === 'cabeza' ? el('span', { class: 'ochart-node__avatar', 'aria-hidden': 'true', text: iniciales(m.nombre) }) : null,
    el('div', { class: 'ochart-node__body' },
      m.cargo ? el('p', { class: 'ochart-node__role', text: m.cargo }) : null,
      el('p', { class: 'ochart-node__name', text: m.nombre }),
      m.detalle ? el('p', { class: 'ochart-node__detail', text: m.detalle }) : null));
}

/** Unidad u órgano (Asamblea, Junta…) enlazado a su página. */
function nodoUnidad({ etiqueta, titulo, detalle, href, clase = '' }) {
  return el(href ? 'a' : 'div', { class: `ochart-node ochart-node--unidad${clase}`, href },
    el('div', { class: 'ochart-node__body' },
      etiqueta ? el('p', { class: 'ochart-node__role', text: etiqueta }) : null,
      el('p', { class: 'ochart-node__name', text: titulo }),
      detalle ? el('p', { class: 'ochart-node__detail', text: detalle }) : null,
      href ? el('span', { class: 'ochart-node__more', 'aria-hidden': 'true', text: 'Ver composición →' }) : null));
}

/** Vocales/personas bajo una rama; "titulo" se muestra como etiqueta al inicio de la lista. */
function hojas(miembros, titulo) {
  return el('ul', { class: 'ochart-leaves' },
    titulo ? el('li', { class: 'is-label' }, el('span', { class: 'ochart__row-title' }, titulo,
      el('span', { class: 'org-count', text: String(miembros.length) }))) : null,
    miembros.map((m) => el('li', {}, nodo(m, 'hoja'))));
}

function rama(cabecera, hijos) {
  return el('li', { class: 'ochart__branch' }, cabecera, hijos);
}

function envoltorio(a, ...contenido) {
  const id = `${a.clave}-esquema`;
  const imagen = urlImagen(a.imagenUrl);
  return [
    el('section', { class: 'ochart-wrap', 'aria-labelledby': id },
      el('h3', { class: 'org-group__title', id }, 'Organigrama',
        el('span', { class: 'org-count', text: String(totalPersonas(a)) })),
      el('div', { class: 'ochart' }, ...contenido)),
    imagen ? el('p', { class: 'ochart-source' },
      el('a', { href: imagen, target: '_blank', rel: 'noopener' },
        'Ver el organigrama oficial en imagen', el('span', { 'aria-hidden': 'true', text: ' ↗' }),
        el('span', { class: 'sr-only', text: ' (se abre en una pestaña nueva)' }))) : null,
  ];
}

const miembrosDe = (a, clave) => a?.grupos.find((g) => g.clave === clave)?.miembros ?? [];

/**
 * Organigrama general: Asamblea General (+ Comisión Delegada) → Presidente →
 * Dirección Deportiva Federativa | Junta Directiva | Secretaría General.
 * Devuelve null si faltan datos clave (se pintan entonces las fichas por grupo).
 */
function organigramaGeneral(a, menores) {
  const junta = miembrosDe(a, 'junta-directiva');
  const presidente = junta.find((m) => /^president/i.test(m.cargo ?? ''));
  const secretaria = junta.find((m) => /secretar/i.test(m.cargo ?? ''));
  const [direccion, ...deportiva] = miembrosDe(a, 'area-deportiva');
  if (!presidente) return null;

  const seleccionMenores = miembrosDe(menores, 'seleccionadores');
  const hojasDeportivas = [
    ...deportiva,
    ...(seleccionMenores.length ? [{ cargo: 'Selección de menores', nombre: seleccionMenores.map((m) => m.nombre).join(' · '), detalle: 'Ver equipo de menores ↓', href: '#menores' }] : []),
  ];
  const administrativo = miembrosDe(a, 'equipo-administrativo');
  const ramas = [
    direccion ? rama(nodo(direccion, 'rama'), hojasDeportivas.length ? hojas(hojasDeportivas) : null) : null,
    junta.length ? rama(nodoUnidad({ etiqueta: 'Órgano de gobierno', titulo: 'Junta Directiva', detalle: plural(junta.length, 'miembro', 'miembros'), href: `${ORGANOS}#junta-directiva`, clase: ' ochart-node--rama' }), null) : null,
    secretaria ? rama(nodo(secretaria, 'rama'), administrativo.length ? hojas(administrativo, 'Equipo administrativo') : null) : null,
  ].filter(Boolean);

  return envoltorio(a,
    el('div', { class: 'ochart__gov' },
      nodoUnidad({ etiqueta: 'Máximo órgano', titulo: 'Asamblea General', href: `${ORGANOS}#asamblea` }),
      nodoUnidad({ etiqueta: 'Órgano delegado', titulo: 'Comisión Delegada', href: `${ORGANOS}#comision-delegada`, clase: ' ochart-node--lateral' })),
    el('div', { class: 'ochart__head' }, nodo({ ...presidente, detalle: null }, 'cabeza')),
    el('ul', { class: 'ochart__branches', style: `--ramas:${ramas.length}` }, ramas));
}

/** Equipo de menores: Selección de menores → seleccionadores → equipo técnico. */
function organigramaMenores(a) {
  const seleccionadores = miembrosDe(a, 'seleccionadores');
  const tecnico = miembrosDe(a, 'equipo-tecnico');
  if (!seleccionadores.length) return null;
  return envoltorio(a,
    el('div', { class: 'ochart__head' }, nodoUnidad({ etiqueta: 'Selección española', titulo: 'Selecciones de menores', clase: ' ochart-node--cabeza' })),
    el('ul', { class: 'ochart__branches', style: `--ramas:${seleccionadores.length}` },
      seleccionadores.map((m) => rama(nodo(m, 'rama'), null))),
    tecnico.length ? el('div', { class: 'ochart__row ochart__row--tras-ramas' },
      el('p', { class: 'ochart__row-title' }, 'Equipo técnico', el('span', { class: 'org-count', text: String(tecnico.length) })),
      el('ul', { class: 'ochart-leaves ochart-leaves--fila', style: `--ramas:${Math.min(tecnico.length, 5)}` },
        tecnico.map((m) => el('li', {}, nodo(m, 'hoja'))))) : null);
}

function seccionArea(a, i, areas) {
  const idTitulo = `${a.clave}-titulo`;
  const grupos = (a.grupos ?? []).filter((g) => g && g.titulo && g.miembros?.length);
  const esquema = a.clave === 'organigrama' ? organigramaGeneral(a, areas.find((x) => x.clave === 'menores'))
    : a.clave === 'menores' ? organigramaMenores(a) : null;

  return el('section', { class: 'organo', id: a.clave, 'aria-labelledby': idTitulo },
    el('header', { class: 'organo__head' },
      el('p', { class: 'organo__num', 'aria-hidden': 'true', text: String(i + 1).padStart(2, '0') }),
      el('div', {},
        el('h2', { class: 'h2 organo__title', id: idTitulo, text: a.titulo }),
        a.descripcion ? el('p', { class: 'organo__lead', text: a.descripcion }) : null,
        el('p', { class: 'organo__meta', text: plural(totalPersonas(a), 'persona', 'personas') }))),
    // Organigrama en HTML (antes, una imagen PNG); si un área no lo admite, fichas por grupo.
    esquema ?? grupos.map((g) => grupo(g, a.clave)));
}

/* ---------------------------------------------------------------------------
   Índice lateral y resumen
   --------------------------------------------------------------------------- */
function pintarIndice(areas) {
  $('#equipo-indice').replaceChildren(...areas.map((a) => el('li', {},
    el('a', { href: `#${a.clave}`, dataset: { clave: a.clave } },
      el('span', { text: a.titulo }), el('span', { class: 'page-toc__count', text: String(totalPersonas(a)) })))));

  // Marca en el índice la sección visible.
  if (!('IntersectionObserver' in window)) return;
  const enlaces = new Map($$('#equipo-indice a').map((a) => [a.dataset.clave, a]));
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
  areas.forEach((a) => { const s = document.getElementById(a.clave); if (s) obs.observe(s); });
}

function pintarResumen(areas) {
  const personas = areas.reduce((s, a) => s + totalPersonas(a), 0);
  const grupos = areas.reduce((s, a) => s + a.grupos.filter((g) => g.miembros?.length).length, 0);
  const datos = [[String(personas), 'personas'], [String(grupos), 'equipos y áreas']];
  $('#equipo-resumen').replaceChildren(...datos.map(([n, t]) => el('li', {}, el('strong', { text: n }), ` ${t}`)));
}

/* ---------------------------------------------------------------------------
   Arranque
   --------------------------------------------------------------------------- */
async function init() {
  const cont = $('#equipo-contenido');
  try {
    const datos = await get('/api/v1/federacion/equipo');
    const areas = (datos?.areas ?? [])
      .filter((a) => a && a.clave && a.titulo)
      .map((a) => ({ ...a, grupos: Array.isArray(a.grupos) ? a.grupos : [] }));
    if (!areas.length) throw new Error('Respuesta sin áreas');

    $('.org-skeleton', cont)?.remove();
    cont.prepend(...areas.map(seccionArea));
    if (datos.actualizado && aFecha(datos.actualizado)) {
      $('.page-note', cont).before(el('p', { class: 'org-updated' }, 'Información actualizada el ',
        el('time', { datetime: datos.actualizado, text: fmtFecha.format(aFecha(datos.actualizado)) }), '.'));
    }
    pintarIndice(areas);
    pintarResumen(areas);

    // Si se llega con un ancla (#menores…), desplazarse ahora que la sección existe.
    if (location.hash) document.getElementById(decodeURIComponent(location.hash.slice(1)))?.scrollIntoView();
  } catch (e) {
    console.warn('[FEP] equipo FEP:', e.message);
    $('.org-skeleton', cont)?.remove();
    $('#equipo-error').hidden = false;
  } finally {
    cont.setAttribute('aria-busy', 'false');
  }
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
})();
