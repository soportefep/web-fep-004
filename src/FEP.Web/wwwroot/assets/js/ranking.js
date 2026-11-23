/**
 * Páginas de clasificación de un ranking (migrada desde Ranking_Absoluto.asp, que cargaba la tabla por AJAX desde
 * CargarRnkAbs.asp y CargarRnkAbsNombre.asp, desde Ranking_Menores.asp con CargarRnkMen.asp y desde
 * Ranking_Silla_Ruedas.asp y desde Ranking_Veteranos.asp con CargarRnkVet.asp). Común a las páginas de ranking: la
 * clave sale de <main data-ranking="absoluto|menores|silla-ruedas|veteranos">. Opcionales: el selector de género (#ranking-generos-opciones; sin
 * él se pide el ranking entero, p. ej. la lista única de silla de ruedas), el de categoría y la normativa (#calculo).
 * Las licencias se muestran con prefijoLicencia de la API ("PS1008" en silla de ruedas; data-prefijo-licencia del
 * <main> hasta que responda) y se buscan con o sin él.
 * Requiere assets/js/comun.js (cabecera, menú, pie y utilidades en window.FEP).
 *
 *   Clasificación → GET /api/v1/competicion/rankings/{clave}?genero&categoria&licencia&busqueda&limite
 *                   (respaldo: mock/competicion-rankings-{clave}.json)
 *   Normativa     → GET /api/v1/competicion/rankings/{clave}/normativa (solo si la página tiene #calculo)
 *                   (respaldo: mock/competicion-rankings-{clave}-normativa.json)
 *
 * Sin búsqueda se ve la lista del género elegido (por defecto el primero: masculino) y, en los rankings con
 * categorías (#ranking-categorias-opciones, p. ej. menores), de la categoría elegida (por defecto la primera), los
 * 100 primeros puestos, ampliables de 100 en 100 hasta 500. La búsqueda (número de licencia o nombre) mira en todos
 * los géneros y categorías, como la búsqueda por licencia de los ASP. Los criterios viajan en la URL (?genero=F,
 * ?categoria=cadete, ?licencia=, ?busqueda=).
 *
 * Modo prototipo: el mock trae el top 100 de cada género sin filtrar. Si la respuesta no corresponde a lo pedido (no
 * es la API), se filtra en el navegador. Las estadísticas de cada jugador van a estadisticas.html (Estadisticas.asp,
 * migrada); las de silla de ruedas (Silla-Ruedas-Est.asp) siguen en la web actual. La columna de evolución solo aparece si algún puesto la trae.
 * Todo texto de la API va por textContent.
 */
(function () {
'use strict';

const { $, $$, el, get, urlSegura, normaliza, aFecha, fmtLargo, paginaMigrada } = window.FEP;

const main = $('main[data-ranking]');
const CLAVE = (main?.dataset.ranking ?? '').trim().toLowerCase();
const RUTA = `/api/v1/competicion/rankings/${encodeURIComponent(CLAVE)}`;

const LIMITE = 100;              // puestos por lista (como el TOP 100 del ASP)
const LIMITE_MAX = 500;          // máximo que admite la API
const MAX_TEXTO = 100;
const MAX_TERMINOS = 6;
const LICENCIA = /^\d{1,9}$/;
const fmtNum = new Intl.NumberFormat('es-ES');
const PARTICULAS = new Set(['de', 'del', 'la', 'las', 'los', 'y', 'i', 'e', 'da', 'dos', 'van', 'von']);

/** Estado de la consulta (lo que se pide a la API). */
const estado = { genero: null, categoria: null, licencia: null, busqueda: '', limite: LIMITE };
let generos = [];               // [{ valor, nombre }] según la API (o el HTML hasta que responda)
let categorias = [];            // ídem; vacía en los rankings sin categorías (absoluto)
let peticion = 0;               // descarta respuestas de consultas anteriores
// Prefijo con el que se muestran las licencias ("PS" en silla de ruedas): data-prefijo-licencia del <main> hasta que
// responda la API (prefijoLicencia). La API y la URL usan siempre el número sin prefijo.
let prefijo = limpiarPrefijo(main?.dataset.prefijoLicencia);

/* ---------------------------------------------------------------------------
   Utilidades
   --------------------------------------------------------------------------- */
const plural = (n, uno, varios) => `${fmtNum.format(n)} ${n === 1 ? uno : varios}`;
const entero = (v) => (Number.isInteger(v) ? v : null);

/** Igual que la API: términos separados por espacios o guiones. */
const terminos = (texto) => String(texto ?? '').split(/[\s-]+/).filter(Boolean);

/** "ENRIC SANMARTI BELETA" → "Enric Sanmarti Beleta" (los nombres llegan en mayúsculas de la ficha). */
function nombrePropio(texto) {
  return String(texto ?? '').toLocaleLowerCase('es-ES')
    .replace(/[\p{L}\p{N}ªº]+/gu, (p, i) => (i > 0 && PARTICULAS.has(p) ? p : p.charAt(0).toLocaleUpperCase('es-ES') + p.slice(1)));
}

const buscando = () => estado.licencia !== null || estado.busqueda !== '';

/** Prefijo de licencia válido (letras, p. ej. "PS") o ''. Función declarada: se usa antes de esta línea. */
function limpiarPrefijo(v) {
  const p = String(v ?? '').trim().toUpperCase();
  return /^[A-Z]{1,4}$/.test(p) ? p : '';
}

/** "PS1008" o "1008" → 1008 (número de licencia sin prefijo); null si no es una licencia. */
function leerLicencia(v) {
  let t = String(v ?? '').trim().toUpperCase();
  if (prefijo && t.startsWith(prefijo)) t = t.slice(prefijo.length).trim();
  return LICENCIA.test(t) && Number(t) > 0 ? Number(t) : null;
}

/** Licencia tal como se muestra ("PS1008" en silla de ruedas). */
const licenciaVisible = (n) => `${prefijo}${n}`;

/** Enlace a las estadísticas del jugador: la página nueva si ya está migrada; si no, la web actual. */
function urlEstadisticas(p) {
  const url = urlSegura(p.estadisticasUrl);
  return url ? (paginaMigrada(url) ?? url) : null;
}

/* ---------------------------------------------------------------------------
   Estado ↔ URL
   --------------------------------------------------------------------------- */
function normalizarGenero(v) {
  const g = String(v ?? '').trim().toUpperCase();
  return { M: 'M', MASCULINO: 'M', F: 'F', FEMENINO: 'F', X: 'X', MIXTO: 'X' }[g] ?? null;
}

/** Clave de categoría como la API (minúsculas); null si no tiene forma de clave. */
function normalizarCategoria(v) {
  const c = String(v ?? '').trim().toLowerCase();
  return /^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(c) ? c : null;
}

/** Categoría que se pide a la API: solo sin búsqueda y en rankings con categorías. */
const categoriaPedida = () => (!buscando() && categorias.length ? estado.categoria : null);

function leerUrl() {
  const q = new URLSearchParams(location.search);
  estado.genero = normalizarGenero(q.get('genero'));
  estado.categoria = normalizarCategoria(q.get('categoria'));
  estado.licencia = leerLicencia(q.get('licencia'));
  estado.busqueda = terminos((q.get('busqueda') ?? '').slice(0, MAX_TEXTO)).slice(0, MAX_TERMINOS).join(' ');
}

function escribirUrl() {
  const q = new URLSearchParams();
  if (buscando()) {
    if (estado.licencia !== null) q.set('licencia', estado.licencia);
    if (estado.busqueda) q.set('busqueda', estado.busqueda);
  } else {
    if (categorias.length && estado.categoria && estado.categoria !== categorias[0]?.valor) q.set('categoria', estado.categoria);
    if (estado.genero && estado.genero !== generos[0]?.valor) q.set('genero', estado.genero);
  }
  const s = q.toString();
  try {
    history.replaceState(null, '', `${location.pathname}${s ? `?${s}` : ''}${location.hash}`);
  } catch {
    // Abierta desde disco (file://) algunos navegadores no permiten cambiar la URL: no es imprescindible.
  }
}

/** Query de la API para el estado actual. */
function consulta() {
  const q = new URLSearchParams();
  if (buscando()) {
    if (estado.licencia !== null) q.set('licencia', estado.licencia);
    if (estado.busqueda) q.set('busqueda', estado.busqueda);
  } else {
    if (estado.genero) q.set('genero', estado.genero);
    if (categoriaPedida()) q.set('categoria', categoriaPedida());
  }
  q.set('limite', estado.limite);
  return q;
}

/* ---------------------------------------------------------------------------
   Formulario
   --------------------------------------------------------------------------- */
/** Selector de género (solo si la página lo tiene: silla de ruedas tiene una sola lista y no lo lleva). */
function pintarGeneros() {
  const caja = $('#ranking-generos-opciones');
  if (!caja) return;
  caja.replaceChildren(...generos.map((g) => el('label', { class: 'rkc-genero' },
    el('input', { type: 'radio', name: 'genero', value: g.valor, checked: !buscando() && g.valor === estado.genero }),
    el('span', { text: g.nombre }))));
}

/** Selector de categoría (solo si la página lo tiene: rankings con categorías). */
function pintarCategorias() {
  const caja = $('#ranking-categorias-opciones');
  if (!caja) return;
  caja.replaceChildren(...categorias.map((c) => el('label', { class: 'rkc-genero' },
    el('input', { type: 'radio', name: 'categoria', value: c.valor, checked: !buscando() && c.valor === estado.categoria }),
    el('span', { text: c.nombre }))));
}

/**
 * Marca género y categoría del estado sin volver a pintar los radios (así no se pierde el foco al elegir con el
 * teclado) y oculta "Volver a la clasificación".
 */
function marcarSeleccion() {
  $$('#ranking-generos-opciones input[name="genero"]').forEach((i) => { i.checked = i.value === estado.genero; });
  $$('#ranking-categorias-opciones input[name="categoria"]').forEach((i) => { i.checked = i.value === estado.categoria; });
  $('#ranking-limpiar').hidden = !buscando();
}

function sincronizarFormulario() {
  pintarCategorias();
  pintarGeneros();
  const texto = $('#ranking-texto');
  texto.value = estado.licencia !== null ? licenciaVisible(estado.licencia) : estado.busqueda;
  $('#ranking-limpiar').hidden = !buscando();
}

function initFormulario() {
  const form = $('#ranking-form');
  const texto = $('#ranking-texto');

  // Elegir género vuelve a la clasificación de ese género (sin búsqueda).
  $('#ranking-generos-opciones')?.addEventListener('change', (e) => {
    const g = normalizarGenero(e.target?.value);
    if (!g) return;
    Object.assign(estado, { genero: g, licencia: null, busqueda: '', limite: LIMITE });
    texto.value = '';
    marcarSeleccion();
    cargar();
  });

  // Elegir categoría, igual (rankings con categorías).
  $('#ranking-categorias-opciones')?.addEventListener('change', (e) => {
    const c = normalizarCategoria(e.target?.value);
    if (!c || !categorias.some((x) => x.valor === c)) return;
    Object.assign(estado, { categoria: c, licencia: null, busqueda: '', limite: LIMITE });
    texto.value = '';
    marcarSeleccion();
    cargar();
  });

  form.addEventListener('submit', (e) => {
    e.preventDefault();
    const valor = texto.value.trim().slice(0, MAX_TEXTO);
    const lic = leerLicencia(valor);
    if (lic !== null) {
      Object.assign(estado, { licencia: lic, busqueda: '' });
    } else {
      const t = terminos(valor).slice(0, MAX_TERMINOS);
      Object.assign(estado, { licencia: null, busqueda: t.join(' ') });
    }
    estado.limite = LIMITE;
    sincronizarFormulario();
    cargar({ desplazar: true });
  });

  $('#ranking-limpiar').addEventListener('click', () => {
    Object.assign(estado, { licencia: null, busqueda: '', limite: LIMITE });
    sincronizarFormulario();
    cargar();
    texto.focus();
  });
}

/* ---------------------------------------------------------------------------
   Datos: API o filtro local (modo prototipo)
   --------------------------------------------------------------------------- */
/** ¿La respuesta corresponde a lo pedido? (la API devuelve los criterios aplicados; el mock no) */
function esRespuestaDeApi(datos) {
  const generoPedido = buscando() ? null : estado.genero;
  return datos && Array.isArray(datos.listas)
    && (datos.genero ?? null) === generoPedido
    && (datos.categoria ?? null) === categoriaPedida()
    && (datos.licencia ?? null) === estado.licencia
    && (datos.busqueda ?? '') === estado.busqueda
    && datos.limite === estado.limite;
}

/** Filtro en el navegador (solo en modo prototipo con el mock). */
function filtrarEnLocal(datos) {
  const t = terminos(estado.busqueda).map(normaliza);
  const filtra = buscando();
  const categoria = categoriaPedida();
  const listas = (datos?.listas ?? [])
    .filter((l) => filtra || ((!estado.genero || l.genero === estado.genero) && (!categoria || l.categoria === categoria)))
    .map((l) => {
      const todos = (l.puestos ?? []).filter((p) =>
        (estado.licencia === null || p.licencia === estado.licencia)
        && t.every((x) => normaliza(p.nombre).includes(x)));
      return { ...l, coincidencias: filtra ? todos.length : l.coincidencias, puestos: todos.slice(0, estado.limite) };
    });
  return {
    ...datos,
    genero: filtra ? null : estado.genero,
    categoria,
    licencia: estado.licencia,
    busqueda: estado.busqueda || null,
    limite: estado.limite,
    listas,
  };
}

async function cargar({ desplazar = false } = {}) {
  const id = ++peticion;
  escribirUrl();
  mostrarCargando();
  try {
    let datos = await get(`${RUTA}?${consulta()}`);
    if (id !== peticion) return;
    if (!Array.isArray(datos?.listas)) throw new Error('Respuesta sin listas');

    // Los géneros (y categorías) del ranking los confirma la API (el HTML los trae por si no responde). Si cambian
    // y lo pedido ya no existe, se vuelve a pedir con el primero.
    // Sin selector de género (una sola lista, como silla de ruedas) no se filtra por género: se pide todo.
    const g = $('#ranking-generos-opciones')
      ? (datos.generos ?? []).filter((x) => x && typeof x.valor === 'string' && x.nombre) : [];
    if ('prefijoLicencia' in datos) prefijo = limpiarPrefijo(datos.prefijoLicencia);
    const c = $('#ranking-categorias-opciones')
      ? (datos.categorias ?? []).filter((x) => x && normalizarCategoria(x.valor) === x.valor && x.nombre) : [];
    const cambianGeneros = g.length && g.map((x) => x.valor).join() !== generos.map((x) => x.valor).join();
    const cambianCategorias = c.length && c.map((x) => x.valor).join() !== categorias.map((x) => x.valor).join();
    if (cambianGeneros || cambianCategorias) {
      const antes = consulta().toString();   // lo que se acaba de pedir
      if (cambianGeneros) generos = g;
      if (cambianCategorias) categorias = c;
      if (generos.length && (!estado.genero || !generos.some((x) => x.valor === estado.genero))) estado.genero = generos[0].valor;
      if (categorias.length && !categorias.some((x) => x.valor === estado.categoria)) estado.categoria = categorias[0].valor;
      sincronizarFormulario();
      if (consulta().toString() !== antes) return cargar({ desplazar });
      escribirUrl();
    }
    if (!esRespuestaDeApi(datos)) datos = filtrarEnLocal(datos);

    pintarListas(datos);
    if (!buscando()) pintarResumen(datos);
    if (desplazar) $('#ranking-resultados').scrollIntoView({ block: 'start' });
  } catch (e) {
    if (id !== peticion) return;
    console.warn('[FEP] ranking:', e.message);
    $('#ranking-skeleton').hidden = true;
    $('#ranking-listas').replaceChildren();
    $('#ranking-vacio').hidden = true;
    $('#ranking-error').hidden = false;
    $('#ranking-estado').textContent = 'No se ha podido cargar la clasificación.';
  } finally {
    if (id === peticion) $('#ranking-resultados').setAttribute('aria-busy', 'false');
  }
}

function mostrarCargando() {
  $('#ranking-resultados').setAttribute('aria-busy', 'true');
  $('#ranking-error').hidden = true;
  $('#ranking-listas').classList.add('is-loading');
}

/* ---------------------------------------------------------------------------
   Clasificación
   --------------------------------------------------------------------------- */
function evolucion(p) {
  const n = entero(p.evolucion);
  const j = entero(p.jornadasPuesto);
  if (n === null) return el('span', { class: 'org-muted', text: '—' });
  const tipo = n > 0 ? 'sube' : n < 0 ? 'baja' : 'igual';
  const simbolo = n > 0 ? '▲' : n < 0 ? '▼' : '=';
  const texto = n > 0 ? `Sube ${plural(n, 'puesto', 'puestos')}` : n < 0 ? `Baja ${plural(-n, 'puesto', 'puestos')}` : 'Mantiene el puesto';
  const semanas = j === null ? '' : `; ${plural(j, 'actualización', 'actualizaciones')} en esta posición`;
  return el('span', { class: `rkc-evo rkc-evo--${tipo}` },
    el('span', { class: 'rkc-evo__valor', 'aria-hidden': 'true', text: n === 0 ? simbolo : `${simbolo} ${fmtNum.format(Math.abs(n))}` }),
    j === null ? null : el('span', { class: 'rkc-evo__veces', 'aria-hidden': 'true', text: `(${fmtNum.format(j)})` }),
    el('span', { class: 'sr-only', text: `${texto}${semanas}` }));
}

const num = (v) => (entero(v) === null ? '—' : fmtNum.format(v));

function filaPuesto(p, conEvolucion) {
  const url = urlEstadisticas(p);
  const nombre = nombrePropio(p.nombre);
  return el('tr', {},
    el('td', { class: 'rkc-td rkc-td--pos', 'data-label': 'Puesto' }, el('span', { class: p.posicion <= 3 ? 'rkc-pos rkc-pos--podio' : 'rkc-pos', text: fmtNum.format(p.posicion) })),
    el('th', { scope: 'row', class: 'rkc-td rkc-td--nombre org-table__name' },
      url ? el('a', { href: url, text: nombre, title: /^https?:/i.test(url) ? 'Ver estadísticas del jugador/a (web actual de la FEP)' : 'Ver estadísticas del jugador/a' }) : nombre),
    el('td', { class: 'rkc-td rkc-td--lic', 'data-label': 'Licencia', text: licenciaVisible(p.licencia) }),
    el('td', { class: 'rkc-td rkc-td--num rkc-td--tt', 'data-label': 'Torneos totales', text: num(p.torneosTotales) }),
    el('td', { class: 'rkc-td rkc-td--num rkc-td--pt', 'data-label': 'Puntos totales', text: num(p.puntosTotales) }),
    el('td', { class: 'rkc-td rkc-td--num rkc-td--tr', 'data-label': 'Torneos ranking', text: num(p.torneosRanking) }),
    el('td', { class: 'rkc-td rkc-td--num rkc-td--pr', 'data-label': 'Puntos ranking' }, el('strong', { text: num(p.puntosRanking) })),
    conEvolucion ? el('td', { class: 'rkc-td rkc-td--evo', 'data-label': 'Evolución' }, evolucion(p)) : null);
}

function tabla(lista, puestos) {
  // La columna de evolución solo si algún puesto la trae (SFEP_RankingFinal.Sube puede venir vacía).
  const conEvolucion = puestos.some((p) => entero(p.evolucion) !== null);
  const desc = buscando() ? `resultados de la búsqueda en el ranking ${lista.titulo.toLowerCase()}` : `ranking ${lista.titulo.toLowerCase()}`;
  return el('div', { class: 'rkc-table-wrap' },
    el('table', { class: 'org-table rkc-table' },
      el('caption', { class: 'sr-only', text: `Clasificación: ${desc}, ${plural(puestos.length, 'puesto', 'puestos')}` }),
      el('thead', {}, el('tr', {},
        el('th', { scope: 'col', class: 'rkc-th--pos', text: 'Puesto' }),
        el('th', { scope: 'col', text: 'Jugador/a' }),
        el('th', { scope: 'col', class: 'rkc-th--num', text: 'Licencia' }),
        el('th', { scope: 'col', class: 'rkc-th--num' }, 'Torneos', el('br'), el('span', { class: 'rkc-th__sub', text: 'totales' })),
        el('th', { scope: 'col', class: 'rkc-th--num' }, 'Puntos', el('br'), el('span', { class: 'rkc-th__sub', text: 'totales' })),
        el('th', { scope: 'col', class: 'rkc-th--num' }, 'Torneos', el('br'), el('span', { class: 'rkc-th__sub', text: 'ranking' })),
        el('th', { scope: 'col', class: 'rkc-th--num' }, 'Puntos', el('br'), el('span', { class: 'rkc-th__sub', text: 'ranking' })),
        conEvolucion ? el('th', { scope: 'col', class: 'rkc-th--evo', text: 'Evolución' }) : null)),
      el('tbody', {}, puestos.map((p) => filaPuesto(p, conEvolucion)))));
}

function documento(lista) {
  const url = urlSegura(lista.documentoUrl);
  if (!url) return null;
  const titulo = `Ranking completo ${lista.titulo.toLowerCase()}`;
  return el('ul', { class: 'doc-list rkc-doc' }, el('li', { class: 'doc' },
    el('span', { class: 'doc__icon', 'aria-hidden': 'true', text: 'PDF' }),
    el('div', { class: 'doc__body' },
      el('a', { class: 'doc__title', href: url, target: '_blank', rel: 'noopener' }, titulo,
        el('span', { class: 'sr-only', text: ' (PDF, se abre en una pestaña nueva)' })),
      el('p', { class: 'doc__meta' },
        el('span', { class: 'org-tag', text: 'PDF' }),
        el('span', { text: 'Todos los puestos' }))),
    el('a', { class: 'doc__download', href: url, target: '_blank', rel: 'noopener', 'aria-label': `Descargar en PDF: ${titulo}` },
      'Descargar ', el('span', { 'aria-hidden': 'true', text: '↓' }))));
}

function datosLista(lista) {
  const f = aFecha(lista.actualizado);
  const items = [
    Number.isInteger(lista.jugadores) ? el('li', {}, el('strong', { text: fmtNum.format(lista.jugadores) }), ` ${lista.jugadores === 1 ? 'jugador/a' : 'jugadores/as'} en el ranking`) : null,
    Number.isInteger(lista.pruebas) ? el('li', {}, el('strong', { text: fmtNum.format(lista.pruebas) }), ` ${lista.pruebas === 1 ? 'prueba suma' : 'pruebas suman'} para el ranking`) : null,
    f ? el('li', {}, 'Actualizado el ', el('time', { datetime: String(lista.actualizado).slice(0, 10), text: fmtLargo.format(f) })) : null,
  ].filter(Boolean);
  return items.length ? el('ul', { class: 'rkc-datos' }, items) : null;
}

/** Botón para ampliar la lista (solo sin búsqueda, si la API puede dar más). */
function botonMas(lista, puestos) {
  if (buscando()) return null;
  const hayMas = (lista.coincidencias ?? 0) > puestos.length;
  if (!hayMas) return null;
  if (puestos.length < estado.limite || estado.limite >= LIMITE_MAX) {
    return el('p', { class: 'org-updated rkc-mas-nota', text: `Se muestran los ${fmtNum.format(puestos.length)} primeros puestos. El ranking completo está en el PDF.` });
  }
  return el('div', { class: 'rkc-mas' },
    el('button', {
      type: 'button', class: 'clubs-pager__btn',
      onclick: () => { estado.limite = Math.min(LIMITE_MAX, estado.limite + LIMITE); cargar(); },
    }, `Ver ${fmtNum.format(Math.min(LIMITE, LIMITE_MAX - estado.limite))} puestos más`),
    el('span', { class: 'clubs-pager__info', text: `${fmtNum.format(puestos.length)} de ${fmtNum.format(lista.coincidencias)}` }));
}

function bloqueLista(lista) {
  const puestos = (lista.puestos ?? []).filter((p) => p && Number.isInteger(p.posicion) && Number.isInteger(p.licencia) && p.nombre)
    .sort((a, b) => a.posicion - b.posicion);
  const idTitulo = `lista-${lista.tipoRanking}-titulo`;
  return el('section', { class: 'rkc-lista', id: `lista-${lista.tipoRanking}`, 'aria-labelledby': idTitulo },
    el('div', { class: 'rkc-lista__head' },
      el('h3', { class: 'cpe-subtitle rkc-lista__title', id: idTitulo },
        `Ranking ${lista.titulo.toLowerCase()}`,
        buscando() ? el('span', { class: 'org-count', text: plural(puestos.length, 'resultado', 'resultados') }) : null),
      datosLista(lista)),
    documento(lista),
    puestos.length ? tabla(lista, puestos) : null,
    // Lista declarada pero aún sin calcular (p. ej. veteranos +60 femenino; el ASP daba error 500).
    !puestos.length && !buscando() ? el('p', { class: 'empty', text: 'Todavía no hay jugadores ni jugadoras clasificados en este ranking.' }) : null,
    botonMas(lista, puestos));
}

function pintarListas(datos) {
  $('#ranking-skeleton').hidden = true;
  const cont = $('#ranking-listas');
  cont.classList.remove('is-loading');
  const listas = datos.listas.filter((l) => l && Number.isInteger(l.tipoRanking) && typeof l.titulo === 'string');
  // En una búsqueda solo se enseñan las listas con resultados.
  const visibles = buscando() ? listas.filter((l) => (l.puestos ?? []).length > 0) : listas;
  cont.replaceChildren(...visibles.map(bloqueLista));

  const total = visibles.reduce((n, l) => n + (l.puestos ?? []).length, 0);
  $('#ranking-vacio').hidden = total > 0 || !buscando();
  const estadoTxt = $('#ranking-estado');
  if (buscando()) {
    const que = estado.licencia !== null ? `la licencia ${licenciaVisible(estado.licencia)}` : `«${estado.busqueda}»`;
    estadoTxt.textContent = total
      ? `${plural(total, 'resultado', 'resultados')} para ${que}.`
      : `Ningún resultado para ${que}.`;
  } else {
    const l = visibles[0];
    const n = l ? (l.puestos ?? []).length : 0;
    estadoTxt.textContent = !l ? 'No hay clasificación publicada.'
      : n ? `Ranking ${l.titulo.toLowerCase()}: ${plural(n, 'primer puesto', 'primeros puestos')}.`
        : `Ranking ${l.titulo.toLowerCase()}: todavía sin clasificación.`;
  }
}

function pintarResumen(datos) {
  const categoria = categoriaPedida();
  const l = datos.listas.find((x) => x.genero === estado.genero && (!categoria || x.categoria === categoria)) ?? datos.listas[0];
  if (!l) return;
  const f = aFecha(l.actualizado);
  const pruebas = Number.isInteger(l.pruebas) ? l.pruebas : null;
  const total = datos.listas.every((x) => Number.isInteger(x.jugadores)) && !datos.genero
    ? datos.listas.reduce((n, x) => n + x.jugadores, 0) : null;
  const items = [
    total !== null ? [fmtNum.format(total), 'jugadores/as en el ranking']
      : Number.isInteger(l.jugadores) ? [fmtNum.format(l.jugadores), `en el ranking ${l.titulo.toLowerCase()}`] : null,
    pruebas !== null ? [fmtNum.format(pruebas), pruebas === 1 ? 'prueba puntuable' : 'pruebas puntuables'] : null,
    f ? [fmtLargo.format(f), 'última actualización'] : null,
  ].filter(Boolean);
  $('#ranking-resumen').replaceChildren(...items.map(([n, t]) => el('li', {}, el('strong', { text: n }), ` ${t}`)));
}

/* ---------------------------------------------------------------------------
   Normativa de cálculo
   --------------------------------------------------------------------------- */
const ID_SECCION = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

function bloqueNormativa(b) {
  switch (b?.tipo) {
    case 'parrafo':
      return b.texto ? el('p', { class: 'transp-text', text: b.texto }) : null;
    case 'nota':
      return b.texto ? el('p', { class: 'rkc-nota', text: b.texto }) : null;
    case 'lista': {
      const items = (b.elementos ?? []).filter((e) => e && (e.texto || e.destacado));
      return items.length ? el('ul', { class: 'transp-list' }, items.map((e) => el('li', {},
        e.destacado ? el('strong', { text: e.destacado }) : null,
        e.destacado && e.texto ? ': ' : null,
        e.texto ?? null))) : null;
    }
    case 'tabla': {
      const t = b.tabla;
      const columnas = (t?.columnas ?? []).filter((c) => typeof c === 'string');
      const filas = (t?.filas ?? []).filter(Array.isArray);
      if (!columnas.length || !filas.length) return null;
      return el('div', { class: 'rkc-table-wrap' },
        el('table', { class: 'org-table rkc-norma' },
          t.titulo ? el('caption', { text: t.titulo }) : null,
          el('thead', {}, el('tr', {}, columnas.map((c) => el('th', { scope: 'col', text: c })))),
          el('tbody', {}, filas.map((f) => el('tr', {}, columnas.map((_, i) => (i === 0
            ? el('th', { scope: 'row', text: String(f[i] ?? '') })
            : el('td', { class: 'rkc-norma__valor', text: String(f[i] ?? '') }))))))));
    }
    default:
      return null;
  }
}

function pintarNormativa(n) {
  const secciones = (n?.secciones ?? []).filter((s) => s && typeof s.titulo === 'string' && ID_SECCION.test(s.id ?? ''));
  if (!secciones.length) throw new Error('Normativa sin apartados');
  if (n.resumen) $('#calculo-resumen').textContent = n.resumen;
  if (n.titulo) $('#calculo-meta').textContent = n.titulo;

  const indice = el('nav', { class: 'transp-index', 'aria-label': 'Apartados del cálculo del ranking' },
    el('ul', {}, secciones.map((s) => el('li', {}, el('a', { href: `#calculo-${s.id}`, text: s.titulo })))));
  $('#calculo-contenido').replaceChildren(indice, ...secciones.map((s) => el('section', {
    class: 'transp-item', id: `calculo-${s.id}`, 'aria-labelledby': `calculo-${s.id}-titulo`,
  },
  el('div', { class: 'transp-item__head' }, el('h3', { class: 'transp-item__title', id: `calculo-${s.id}-titulo`, text: s.titulo })),
  ...(s.bloques ?? []).map(bloqueNormativa))));
}

async function cargarNormativa() {
  const cont = $('#calculo');
  try {
    pintarNormativa(await get(`${RUTA}/normativa`));
  } catch (e) {
    console.warn('[FEP] normativa del ranking:', e.message);
    $('#calculo-error').hidden = false;
  } finally {
    $('#calculo-skeleton').hidden = true;
    cont.setAttribute('aria-busy', 'false');
  }
}

/* ---------------------------------------------------------------------------
   Índice lateral
   --------------------------------------------------------------------------- */
function initIndice() {
  if (!('IntersectionObserver' in window)) return;
  const enlaces = new Map($$('#ranking-indice a').map((a) => [a.dataset.seccion, a]));
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
function init() {
  if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(CLAVE)) {
    console.warn('[FEP] ranking: falta data-ranking en <main>');
    $('#ranking-skeleton').hidden = true;
    $('#ranking-error').hidden = false;
    return;
  }
  leerUrl();
  // Géneros del HTML hasta que responda la API (la primera respuesta los sustituye si son otros).
  generos = $$('#ranking-generos-opciones input[name="genero"]')
    .map((i) => ({ valor: i.value, nombre: i.parentElement?.textContent.trim() || i.value }));
  if (!estado.genero || !generos.some((g) => g.valor === estado.genero)) estado.genero = generos[0]?.valor ?? null;
  categorias = $$('#ranking-categorias-opciones input[name="categoria"]')
    .map((i) => ({ valor: i.value, nombre: i.parentElement?.textContent.trim() || i.value }));
  if (!categorias.some((c) => c.valor === estado.categoria)) estado.categoria = categorias[0]?.valor ?? null;
  initFormulario();
  sincronizarFormulario();
  initIndice();
  cargar().then(() => {
    if (location.hash) document.getElementById(decodeURIComponent(location.hash.slice(1)))?.scrollIntoView();
  });
  // La normativa de cálculo solo existe en algunos rankings (absoluto); el ASP de menores no tenía.
  if ($('#calculo')) cargarNormativa();
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
})();
