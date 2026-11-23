/**
 * Página "Ley de Transparencia" (migrada desde Ley_De_Transparencia.asp).
 * Requiere assets/js/comun.js (cabecera, menú, pie y utilidades en window.FEP).
 *
 *   Contenido → GET /api/v1/federacion/transparencia
 *               (respaldo: mock/federacion-transparencia.json y mocks.js)
 *
 * Cada categoría se pinta como una sección con ancla (#institucional, #economica…) y un índice de
 * sus apartados; cada apartado (#presupuestos, #cuentas-anuales…) muestra sus párrafos, listas,
 * personas, importes, documentos y enlaces. Todo texto de la API va por textContent.
 */
(function () {
'use strict';

const { $, $$, el, get, urlSegura, aFecha, attrsEnlace } = window.FEP;

const fmtFecha = new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'long', year: 'numeric' });
const fmtEuros = new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR', minimumFractionDigits: 2 });
const DOCS_VISIBLES = 5;              // el resto, plegados en "Ver documentos anteriores"
const PARTICULAS = new Set(['de', 'del', 'la', 'las', 'los', 'y', 'e', 'i', 'san']);

/* ---------------------------------------------------------------------------
   Utilidades
   --------------------------------------------------------------------------- */
const lista = (v) => (Array.isArray(v) ? v.filter((x) => x != null && x !== '') : []);
const plural = (n, uno, varios) => `${n} ${n === 1 ? uno : varios}`;
const docsDe = (apartados) => apartados.reduce((s, a) => s + a.documentos.length, 0);

/** Iniciales para el avatar: "Javier Rodríguez Piris" → "JR". */
function iniciales(nombre) {
  const palabras = String(nombre ?? '').trim().split(/[\s-]+/)
    .filter((p) => p && !PARTICULAS.has(p.toLowerCase()) && /\p{L}/u.test(p));
  return palabras.slice(0, 2).map((p) => p[0].toUpperCase()).join('') || '·';
}

/** Importe en euros con formato español ("248.256,18 €"); null si no es un número. */
/**
 * Importe formateado, o null si no hay importe. Ojo: Number(null) y Number('') valen 0, así que se
 * descartan antes; y un importe 0 tampoco se pinta (en el original nunca se publicaba "0 €": el
 * personal administrativo y técnico no lleva retribución en esta página).
 */
function euros(valor) {
  if (valor == null || valor === '') return null;
  const n = typeof valor === 'number' ? valor : Number(valor);
  return Number.isFinite(n) && n > 0 ? fmtEuros.format(n) : null;
}

/** "2024" → "Ejercicio 2024"; "2025-2026" → "Periodo 2025-2026". */
function textoPeriodo(p) {
  if (!p) return null;
  return /^\d{4}$/.test(String(p)) ? `Ejercicio ${p}` : `Periodo ${p}`;
}

/* ---------------------------------------------------------------------------
   Bloques de un apartado
   --------------------------------------------------------------------------- */
function bloqueParrafos(parrafos) {
  return parrafos.map((t) => el('p', { class: 'transp-text', text: t }));
}

function bloqueLista(items) {
  return items.length ? el('ul', { class: 'transp-list' }, items.map((t) => el('li', { text: t }))) : null;
}

function tarjetaPersona(p) {
  const funciones = lista(p.funciones);
  const retribucion = euros(p.retribucionAnualBruta);
  return el('li', { class: 'person-card' },
    el('span', { class: 'person-card__avatar', 'aria-hidden': 'true', text: iniciales(p.nombre) }),
    el('div', { class: 'person-card__body' },
      p.cargo ? el('p', { class: 'person-card__role', text: p.cargo }) : null,
      el('p', { class: 'person-card__name', text: p.nombre }),
      funciones.length === 1 ? el('p', { class: 'person-card__detail', text: funciones[0] }) : null,
      funciones.length > 1 ? el('ul', { class: 'person-card__funcs', 'aria-label': `Funciones de ${p.nombre}` },
        funciones.map((f) => el('li', { text: f }))) : null,
      retribucion ? el('p', { class: 'person-card__money' }, 'Retribución anual bruta: ', el('strong', { text: retribucion })) : null,
      p.nota ? el('p', { class: 'person-card__detail', text: p.nota }) : null));
}

/** Personas agrupadas por "grupo" (Equipo administrativo, Equipo técnico…), en el orden recibido. */
function bloquePersonas(personas, idApartado) {
  if (!personas.length) return null;
  const grupos = new Map();
  for (const p of personas) {
    const g = p.grupo || '';
    if (!grupos.has(g)) grupos.set(g, []);
    grupos.get(g).push(p);
  }
  let n = 0;
  return [...grupos].map(([grupo, miembros]) => {
    const fichas = el('ul', { class: 'person-grid' }, miembros.map(tarjetaPersona));
    if (!grupo) return fichas;
    const id = `${idApartado}-grupo-${++n}`;
    return el('section', { class: 'org-group', 'aria-labelledby': id },
      el('h4', { class: 'org-group__title', id }, grupo, el('span', { class: 'org-count', text: String(miembros.length) })),
      fichas);
  });
}

function bloqueImportes(importes, titulo) {
  const validos = importes.filter((i) => i && i.concepto && euros(i.importe));
  if (!validos.length) return null;
  return el('table', { class: 'org-table transp-amounts' },
    el('caption', { class: 'sr-only', text: titulo }),
    el('thead', {}, el('tr', {},
      el('th', { scope: 'col', text: 'Concepto' }),
      el('th', { scope: 'col', text: 'Importe' }))),
    el('tbody', {}, validos.map((i) => el('tr', {},
      el('td', { class: 'org-table__name', text: i.concepto }),
      el('td', { class: 'transp-amounts__value', text: euros(i.importe) })))));
}

function itemDocumento(d) {
  const url = urlSegura(d.url);
  const periodo = textoPeriodo(d.periodo);
  return el('li', { class: 'doc' },
    el('span', { class: 'doc__icon', 'aria-hidden': 'true', text: 'PDF' }),
    el('div', { class: 'doc__body' },
      url ? el('a', { class: 'doc__title', href: url, target: '_blank', rel: 'noopener' }, d.titulo)
        : el('span', { class: 'doc__title', text: d.titulo }),
      el('p', { class: 'doc__meta' },
        periodo ? el('span', { class: 'org-tag', text: periodo }) : null,
        d.descripcion ? el('span', { text: d.descripcion }) : null,
        el('span', { text: 'PDF' }))),
    url ? el('a', { class: 'doc__download', href: url, target: '_blank', rel: 'noopener', 'aria-label': `Descargar en PDF: ${d.titulo}` },
      'Descargar ', el('span', { 'aria-hidden': 'true', text: '↓' })) : null);
}

function bloqueDocumentos(docs) {
  if (!docs.length) return null;
  const visibles = docs.slice(0, DOCS_VISIBLES);
  const resto = docs.slice(DOCS_VISIBLES);
  return [
    el('ul', { class: 'doc-list' }, visibles.map(itemDocumento)),
    resto.length ? el('details', { class: 'org-more' },
      el('summary', { text: `Ver ${plural(resto.length, 'documento más', 'documentos más')}` }),
      el('ul', { class: 'doc-list' }, resto.map(itemDocumento))) : null,
  ];
}

function bloqueEnlaces(enlaces) {
  const validos = enlaces.filter((e) => e && e.texto && e.url)
    .map((e) => ({ texto: e.texto, ...attrsEnlace(e.url, null) }))
    .filter((e) => e.href !== '#');
  if (!validos.length) return null;
  return el('ul', { class: 'transp-links' }, validos.map((e) => el('li', {},
    el('a', { class: 'link-red', href: e.href, target: e.target, rel: e.rel }, e.texto, el('span', { 'aria-hidden': 'true', text: '→' })))));
}

/* ---------------------------------------------------------------------------
   Apartados y categorías
   --------------------------------------------------------------------------- */
function apartado(a) {
  const idTitulo = `${a.clave}-titulo`;
  return el('article', { class: 'transp-item', id: a.clave, 'aria-labelledby': idTitulo },
    el('header', { class: 'transp-item__head' },
      el('h3', { class: 'transp-item__title', id: idTitulo, text: a.titulo }),
      a.pendiente ? el('span', { class: 'org-tag org-tag--red', text: 'Pendiente de actualización' }) : null,
      a.documentos.length ? el('span', { class: 'org-count', text: plural(a.documentos.length, 'documento', 'documentos') }) : null),
    bloqueParrafos(a.parrafos),
    bloqueLista(a.lista),
    bloquePersonas(a.personas, a.clave),
    bloqueImportes(a.importes, a.titulo),
    bloqueDocumentos(a.documentos),
    bloqueEnlaces(a.enlaces),
    a.actualizacion ? el('p', { class: 'org-updated', text: `Información actualizada: ${a.actualizacion}.` }) : null);
}

function seccionCategoria(c, i) {
  const idTitulo = `${c.clave}-titulo`;
  const docs = docsDe(c.apartados);
  return el('section', { class: 'organo', id: c.clave, 'aria-labelledby': idTitulo },
    el('header', { class: 'organo__head' },
      el('p', { class: 'organo__num', 'aria-hidden': 'true', text: String(i + 1).padStart(2, '0') }),
      el('div', {},
        el('h2', { class: 'h2 organo__title', id: idTitulo, text: c.titulo }),
        c.descripcion ? el('p', { class: 'organo__lead', text: c.descripcion }) : null,
        el('p', { class: 'organo__meta', text: [
          plural(c.apartados.length, 'apartado', 'apartados'),
          docs ? plural(docs, 'documento', 'documentos') : null,
        ].filter(Boolean).join(' · ') }))),
    // Índice de los apartados de la categoría
    c.apartados.length > 1 ? el('nav', { class: 'transp-index', 'aria-label': `Apartados de ${c.titulo}` },
      el('ul', {}, c.apartados.map((a) => el('li', {}, el('a', { href: `#${a.clave}`, text: a.titulo }))))) : null,
    c.apartados.map(apartado));
}

/** Normaliza la respuesta: descarta lo que no tiene clave/título y garantiza listas. */
function normalizar(datos) {
  return lista(datos?.categorias)
    .filter((c) => c.clave && c.titulo)
    .map((c) => ({
      ...c,
      apartados: lista(c.apartados).filter((a) => a.clave && a.titulo).map((a) => ({
        ...a,
        parrafos: lista(a.parrafos),
        lista: lista(a.lista),
        enlaces: lista(a.enlaces),
        personas: lista(a.personas).filter((p) => p.nombre),
        importes: lista(a.importes),
        documentos: lista(a.documentos).filter((d) => d.titulo),
      })),
    }))
    .filter((c) => c.apartados.length);
}

/* ---------------------------------------------------------------------------
   Índice lateral y resumen
   --------------------------------------------------------------------------- */
function pintarIndice(categorias) {
  $('#transparencia-indice').replaceChildren(...categorias.map((c) => el('li', {},
    el('a', { href: `#${c.clave}`, dataset: { clave: c.clave } },
      el('span', { text: c.titulo }), el('span', { class: 'page-toc__count', text: String(c.apartados.length) })))));

  // Marca en el índice la sección visible.
  if (!('IntersectionObserver' in window)) return;
  const enlaces = new Map($$('#transparencia-indice a').map((a) => [a.dataset.clave, a]));
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
  categorias.forEach((c) => { const s = document.getElementById(c.clave); if (s) obs.observe(s); });
}

function pintarResumen(categorias) {
  const apartados = categorias.flatMap((c) => c.apartados);
  const pendientes = apartados.filter((a) => a.pendiente).length;
  const datos = [
    [String(apartados.length), 'apartados'],
    [String(docsDe(apartados)), 'documentos publicados'],
    pendientes ? [String(pendientes), 'pendientes de actualización'] : null,
  ].filter(Boolean);
  $('#transparencia-resumen').replaceChildren(...datos.map(([n, t]) => el('li', {}, el('strong', { text: n }), ` ${t}`)));
}

/* ---------------------------------------------------------------------------
   Arranque
   --------------------------------------------------------------------------- */
async function init() {
  const cont = $('#transparencia-contenido');
  try {
    const datos = await get('/api/v1/federacion/transparencia');
    const categorias = normalizar(datos);
    if (!categorias.length) throw new Error('Respuesta sin categorías');

    $('.org-skeleton', cont)?.remove();
    cont.prepend(...categorias.map(seccionCategoria));
    if (datos.actualizado && aFecha(datos.actualizado)) {
      cont.append(el('p', { class: 'org-updated' }, 'Página revisada el ',
        el('time', { datetime: datos.actualizado, text: fmtFecha.format(aFecha(datos.actualizado)) }), '.'));
    }
    pintarIndice(categorias);
    pintarResumen(categorias);

    // Si se llega con un ancla (#presupuestos…), desplazarse ahora que la sección existe.
    if (location.hash) document.getElementById(decodeURIComponent(location.hash.slice(1)))?.scrollIntoView();
  } catch (e) {
    console.warn('[FEP] transparencia:', e.message);
    $('.org-skeleton', cont)?.remove();
    $('#transparencia-error').hidden = false;
  } finally {
    cont.setAttribute('aria-busy', 'false');
  }
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
})();
