/**
 * Página "Calendarios de las federaciones territoriales" (migrada desde CalendarioFT.asp, URL antigua /CalendarioFT).
 * Requiere assets/js/comun.js (cabecera, menú, pie y utilidades en window.FEP).
 *
 *   Federaciones + sus temporadas        → GET /api/v1/competicion/calendario/federaciones
 *                                          (respaldo: mock/competicion-calendario-federaciones.json)
 *   Temporadas + pruebas de una federación → GET /api/v1/competicion/calendario/federaciones/{id}?anio
 *                                          (respaldo: mock/competicion-calendario-federaciones-{id}.json)
 *
 * Como en el ASP, sin federación se pide elegir una ("Selecciona una Federación Territorial para ver su
 * Calendario"); cambiar de federación o de temporada vuelve a consultar. La federación y la temporada viajan en la
 * URL (?federacion=6&anio=2026) y también se aceptan los nombres del formulario antiguo (?fed=6&ano=2026; fed=1000
 * era "ninguna" y fed=0 el calendario FEP, que tiene su propia página). Categoría, texto y "pendientes de celebrar"
 * son filtros nuevos que se aplican en el navegador. Las pruebas se vuelven a filtrar por temporada en el navegador:
 * con la API no cambia nada y en modo prototipo el mock (todas las temporadas) se comporta como la API.
 * Misma presentación que el calendario FEP (calendario-fep.js). Todo texto de la API va por textContent.
 */
(function () {
'use strict';

const { $, $$, el, get, urlSegura, paginaMigrada, aFecha, bloqueFecha, fmtLargo, normaliza } = window.FEP;

const ANIO_MIN = 1901;
const ANIO_MAX = 2100;
const FED_MIN = 1;
const FED_MAX = 22;   // DECISIÓN FEP: 22 federaciones (1–22; Castilla-La Mancha es la 22)
const FED_NINGUNA_ASP = 1000;   // opción "-----" del desplegable del ASP
const fmtNum = new Intl.NumberFormat('es-ES');
const fmtMes = new Intl.DateTimeFormat('es-ES', { month: 'long', year: 'numeric' });
const fmtMesCorto = new Intl.DateTimeFormat('es-ES', { month: 'long' });
const TITULO_BASE = document.title;

/** Estado: federacion null = ninguna elegida; anio null = la temporada por defecto de la API. */
const estado = { federacion: null, anio: null, categoria: '', texto: '', proximas: false };
let federaciones = [];     // [{ id, territorio, nombre, abreviatura, temporadas }] por territorio
let federacionesCargadas = false;
let federacionActual = null;
let temporadas = [];       // [{ anio, actualizado, archivo, url }] de la federación mostrada
let anioMostrado = null;
let pruebas = [];          // pruebas de la temporada mostrada (sin filtros del navegador)
let peticion = 0;          // descarta respuestas de consultas anteriores
let observador = null;

/* ---------------------------------------------------------------------------
   Utilidades
   --------------------------------------------------------------------------- */
const entero = (v) => (/^\d+$/.test(String(v ?? '').trim()) ? Number(v) : null);
const plural = (n, uno, varios) => `${fmtNum.format(n)} ${n === 1 ? uno : varios}`;
const limpio = (t) => String(t ?? '').replace(/\s+/g, ' ').trim();
const mayuscula = (t) => (t ? t.charAt(0).toLocaleUpperCase('es-ES') + t.slice(1) : t);
const hoy = () => { const d = new Date(); return new Date(d.getFullYear(), d.getMonth(), d.getDate()); };
const anios = (f) => (Array.isArray(f?.temporadas) ? f.temporadas.filter(Number.isInteger) : []);
const nombreFederacion = (f) => limpio(f?.territorio) || limpio(f?.abreviatura) || `Federación ${f?.id}`;

/** Clase de la etiqueta de categoría ("Absoluta", "Menores", "Veteranos", "Sub 23"…), como el calendario FEP. */
function claseCategoria(categoria) {
  const c = normaliza(categoria);
  if (c.startsWith('absolut')) return 'org-tag camp-tag camp-tag--absoluto';
  if (c.startsWith('menor')) return 'org-tag camp-tag camp-tag--menores';
  if (c.startsWith('veteran')) return 'org-tag camp-tag camp-tag--veteranos';
  if (c.startsWith('silla')) return 'org-tag calf-tag--silla';
  return 'org-tag';
}

/** Texto de la categoría: "SILLA RUEDAS" (así en la BD) → "Silla de ruedas". */
function textoCategoria(categoria) {
  const t = limpio(categoria);
  return normaliza(t) === 'silla ruedas' ? 'Silla de ruedas' : t;
}

/** La prueba aún no ha terminado (o no tiene fecha) según el reloj del navegador. */
function pendiente(p) {
  if (p.fechaPorConfirmar) return true;
  const fin = aFecha(p.fechaFin) ?? aFecha(p.fechaInicio);
  return !fin || fin >= hoy();
}

/* ---------------------------------------------------------------------------
   Estado ↔ URL
   --------------------------------------------------------------------------- */
function leerUrl() {
  const q = new URLSearchParams(location.search);
  const anio = entero(q.get('anio') ?? q.get('ano'));
  estado.anio = anio >= ANIO_MIN && anio <= ANIO_MAX ? anio : null;
  const fed = entero(q.get('federacion') ?? q.get('fed'));
  // fed=0 en CalendarioFT.asp mostraba el calendario de la FEP: tiene su propia página.
  if (fed === 0) {
    location.replace(`calendariofep.html${estado.anio ? `?anio=${estado.anio}` : ''}`);
    return false;
  }
  estado.federacion = fed >= FED_MIN && fed <= FED_MAX && fed !== FED_NINGUNA_ASP ? fed : null;
  estado.categoria = limpio(q.get('categoria')).slice(0, 60);
  estado.texto = limpio(q.get('q')).slice(0, 100);
  estado.proximas = q.get('proximas') === '1';
  return true;
}

function escribirUrl() {
  const q = new URLSearchParams();
  if (estado.federacion) q.set('federacion', estado.federacion);
  if (estado.federacion && estado.anio) q.set('anio', estado.anio);
  if (estado.categoria) q.set('categoria', estado.categoria);
  if (estado.texto) q.set('q', estado.texto);
  if (estado.proximas) q.set('proximas', '1');
  const s = q.toString();
  try {
    history.replaceState(null, '', `${location.pathname}${s ? `?${s}` : ''}${location.hash}`);
  } catch {
    // Abierta desde disco (file://) algunos navegadores no permiten cambiar la URL: no es imprescindible.
  }
}

/** Enlace a esta página para una federación (navegable y compartible). */
const hrefFederacion = (id) => `calendarioft.html?federacion=${id}`;

/* ---------------------------------------------------------------------------
   Federaciones (desplegable y rejilla)
   --------------------------------------------------------------------------- */
async function cargarFederaciones() {
  try {
    const datos = await get('/api/v1/competicion/calendario/federaciones');
    federaciones = (Array.isArray(datos) ? datos : [])
      .filter((f) => f && Number.isInteger(f.id) && f.id >= FED_MIN && f.id <= FED_MAX)
      .sort((a, b) => nombreFederacion(a).localeCompare(nombreFederacion(b), 'es'));
    federacionesCargadas = true;
  } catch (e) {
    console.warn('[FEP] federaciones del calendario:', e.message);
    $('#calt-feds-error').hidden = false;
    $('#calt-feds-total').textContent = '–';
  }
  pintarFederaciones();
}

function pintarFederaciones() {
  const select = $('#calt-federacion');
  // La federación de la URL que no está en la lista (o si la lista no ha cargado) se mantiene con su nombre.
  const lista = federaciones.slice();
  if (federacionActual && !lista.some((f) => f.id === federacionActual.id)) lista.push(federacionActual);
  select.replaceChildren(
    el('option', { value: '', text: 'Selecciona una federación' }),
    ...lista.map((f) => el('option', {
      value: String(f.id),
      selected: f.id === estado.federacion,
      text: anios(f).length || !federacionesCargadas ? nombreFederacion(f) : `${nombreFederacion(f)} (sin calendario)`,
    })));
  select.disabled = !lista.length && !estado.federacion;

  if (!federacionesCargadas) return;
  const conCalendario = federaciones.filter((f) => anios(f).length);
  $('#calt-feds-total').textContent = fmtNum.format(conCalendario.length);
  $('#calt-feds').replaceChildren(...federaciones.map((f) => {
    const n = anios(f);
    const actual = f.id === estado.federacion;
    const meta = n.length
      ? `${plural(n.length, 'temporada', 'temporadas')} · ${n.length > 1 ? `${Math.min(...n)}–${Math.max(...n)}` : n[0]}`
      : 'Sin calendario publicado';
    const contenido = [
      el('span', { class: 'calt-fed__name', text: nombreFederacion(f) }),
      el('span', { class: 'calt-fed__meta', text: meta }),
    ];
    return el('li', {}, n.length
      ? el('a', {
        class: `calt-fed${actual ? ' is-active' : ''}`,
        href: hrefFederacion(f.id),
        'aria-current': actual ? 'page' : null,
        dataset: { federacion: String(f.id) },
      }, ...contenido)
      : el('div', { class: 'calt-fed calt-fed--vacia' }, ...contenido));
  }));
  pintarResumen();
}

/* ---------------------------------------------------------------------------
   Formulario
   --------------------------------------------------------------------------- */
function pintarAnios() {
  const select = $('#calt-anio');
  select.replaceChildren(...temporadas.map((t) => el('option', { value: String(t.anio), selected: t.anio === anioMostrado, text: String(t.anio) })));
  select.disabled = !temporadas.length;
}

function pintarCategorias() {
  const nombres = [...new Set(pruebas.map((p) => limpio(p.categoria)).filter(Boolean))]
    .sort((a, b) => textoCategoria(a).localeCompare(textoCategoria(b), 'es'));
  // Categoría de un enlace o de otra temporada que aquí no existe: se quita el filtro.
  if (estado.categoria && !nombres.includes(estado.categoria)) {
    estado.categoria = '';
    escribirUrl();
  }
  const select = $('#calt-categoria');
  select.replaceChildren(
    el('option', { value: '', text: 'Todas' }),
    ...nombres.map((n) => el('option', { value: n, selected: n === estado.categoria, text: textoCategoria(n) })));
  select.disabled = !nombres.length;
}

function sincronizarFormulario() {
  $('#calt-categoria').value = estado.categoria;
  $('#calt-texto').value = estado.texto;
  $('#calt-proximas').checked = estado.proximas;
}

function elegirFederacion(id) {
  // Cada federación tiene sus temporadas y categorías: se vuelve a la temporada por defecto y sin categoría.
  Object.assign(estado, { federacion: id, anio: null, categoria: '' });
  federacionActual = federaciones.find((f) => f.id === id) ?? null;
  $('#calt-federacion').value = id ? String(id) : '';
  buscar();
}

function initFormulario() {
  $('#calt-federacion').addEventListener('change', (e) => elegirFederacion(entero(e.target.value)));
  $('#calt-anio').addEventListener('change', (e) => { estado.anio = entero(e.target.value); buscar(); });
  // Filtros locales: no hace falta volver a pedir nada.
  $('#calt-categoria').addEventListener('change', (e) => { estado.categoria = e.target.value; escribirUrl(); pintar(); });
  let espera;
  $('#calt-texto').addEventListener('input', (e) => {
    clearTimeout(espera);
    espera = setTimeout(() => { estado.texto = limpio(e.target.value).slice(0, 100); escribirUrl(); pintar(); }, 200);
  });
  $('#calt-proximas').addEventListener('change', (e) => { estado.proximas = e.target.checked; escribirUrl(); pintar(); });
  $('#calt-form').addEventListener('submit', (e) => e.preventDefault());
  $('#calt-form').addEventListener('reset', () => {
    // Limpiar filtros mantiene la federación elegida y vuelve a su temporada por defecto.
    Object.assign(estado, { anio: null, categoria: '', texto: '', proximas: false });
    // El reset nativo restaura las opciones por defecto: se sincroniza después.
    setTimeout(() => {
      $('#calt-federacion').value = estado.federacion ? String(estado.federacion) : '';
      sincronizarFormulario();
      buscar();
    });
  });
  // Rejilla de federaciones: sin recargar la página (el enlace sigue siendo navegable y se puede abrir aparte).
  $('#calt-feds').addEventListener('click', (e) => {
    const a = e.target.closest('a[data-federacion]');
    if (!a || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
    e.preventDefault();
    elegirFederacion(entero(a.dataset.federacion));
    $('#filtros-calendario').scrollIntoView({ block: 'start' });
  });
}

/* ---------------------------------------------------------------------------
   Consulta
   --------------------------------------------------------------------------- */
async function buscar() {
  const id = ++peticion;
  escribirUrl();

  if (!estado.federacion) {
    mostrarSinFederacion();
    return;
  }
  mostrarCargando();

  try {
    const fed = estado.federacion;
    const datos = await get(`/api/v1/competicion/calendario/federaciones/${fed}${estado.anio ? `?anio=${estado.anio}` : ''}`);
    if (id !== peticion) return;
    if (!datos || typeof datos !== 'object') throw new Error('Respuesta vacía');

    federacionActual = datos.federacion && Number.isInteger(datos.federacion.id)
      ? datos.federacion
      : (federaciones.find((f) => f.id === fed) ?? { id: fed, territorio: null, temporadas: [] });

    const lista = (Array.isArray(datos.temporadas) ? datos.temporadas : [])
      .filter((t) => t && Number.isInteger(t.anio))
      .sort((a, b) => b.anio - a.anio);
    // Temporada pedida que la federación no tiene (enlace antiguo con un año de la FEP): la de por defecto.
    if (estado.anio && lista.length && !lista.some((t) => t.anio === estado.anio)) {
      estado.anio = null;
      return buscar();
    }
    temporadas = lista;
    anioMostrado = estado.anio ?? (Number.isInteger(datos.anio) ? datos.anio : lista[0]?.anio ?? null);
    pintarAnios();
    pintarFederaciones();

    pruebas = (Array.isArray(datos.items) ? datos.items : [])
      .filter((p) => p && limpio(p.titulo) && (p.anio ?? anioMostrado) === anioMostrado);
    pintarCategorias();
    pintar();
  } catch (e) {
    if (id !== peticion) return;
    console.warn('[FEP] calendario territorial:', e.message);
    mostrarError();
  } finally {
    if (id === peticion) {
      $('#calt-contenido').setAttribute('aria-busy', 'false');
      $('#pruebas').setAttribute('aria-busy', 'false');
    }
  }
}

function mostrarCargando() {
  $('#pruebas').setAttribute('aria-busy', 'true');
  $('#calt-error').hidden = true;
  $('#calt-elige').hidden = true;
  $('#calt-sin-calendario').hidden = true;
  $('#calt-estado').textContent = 'Cargando el calendario…';
  // Primera carga: skeleton. Después se deja la lista anterior atenuada hasta que llega la nueva.
  if ($('#calt-lista').hidden && $('#calt-vacio').hidden) $('#calt-skeleton').hidden = false;
  $('#calt-lista').classList.add('is-loading');
}

function mostrarError() {
  $('#calt-skeleton').hidden = true;
  $('#calt-lista').hidden = true;
  $('#calt-vacio').hidden = true;
  $('#calt-temporada').hidden = true;
  $('#calt-error').hidden = false;
  $('#calt-total').textContent = '–';
  $('#calt-estado').textContent = 'No se ha podido cargar el calendario.';
  $('#calt-anio').disabled = !temporadas.length;
}

/** Sin federación elegida (como el ASP con fed=1000): se pide elegir una. */
function mostrarSinFederacion() {
  federacionActual = null;
  temporadas = [];
  anioMostrado = null;
  pruebas = [];
  pintarAnios();
  pintarCategorias();
  for (const s of ['#calt-skeleton', '#calt-lista', '#calt-vacio', '#calt-error', '#calt-sin-calendario', '#calt-temporada']) $(s).hidden = true;
  $('#calt-elige').hidden = false;
  $('#calt-titulo-lista').textContent = 'Pruebas';
  $('#calt-total').textContent = '–';
  $('#calt-estado').textContent = 'Selecciona una federación territorial para ver su calendario.';
  document.title = TITULO_BASE;
  actualizarFicha();
  pintarFederaciones();
  pintarIndice([]);
  $('#calt-contenido').setAttribute('aria-busy', 'false');
}

/** Enlace a la ficha de la federación (federacion.html?id=) en el aviso "sin calendario"; sin federación, al listado. */
function actualizarFicha() {
  $('#calt-ficha-vacia').href = federacionActual ? `federacion.html?id=${federacionActual.id}` : 'federaciones.html';
}

/* ---------------------------------------------------------------------------
   PDF de la temporada (solo si la federación lo ha publicado; el ASP no los mostraba)
   --------------------------------------------------------------------------- */
function pintarTemporada() {
  const t = temporadas.find((x) => x.anio === anioMostrado);
  const cont = $('#calt-temporada');
  const url = urlSegura(t?.url);
  if (!t || !url) { cont.hidden = true; return; }
  const f = aFecha(t.actualizado);
  const titulo = `Calendario ${nombreFederacion(federacionActual)} ${t.anio}`;
  cont.replaceChildren(el('ul', { class: 'doc-list' }, el('li', { class: 'doc' },
    el('span', { class: 'doc__icon', 'aria-hidden': 'true', text: 'PDF' }),
    el('div', { class: 'doc__body' },
      el('a', { class: 'doc__title', href: url, target: '_blank', rel: 'noopener' }, titulo,
        el('span', { class: 'sr-only', text: ' (PDF, se abre en una pestaña nueva)' })),
      el('p', { class: 'doc__meta' },
        el('span', { class: 'org-tag', text: 'PDF' }),
        f ? el('span', {}, 'Actualizado el ', el('time', { datetime: String(t.actualizado).slice(0, 10), text: fmtLargo.format(f) })) : null)),
    el('a', { class: 'doc__download', href: url, target: '_blank', rel: 'noopener', 'aria-label': `Descargar en PDF: ${titulo}` },
      'Descargar ', el('span', { 'aria-hidden': 'true', text: '↓' })))));
  cont.hidden = false;
}

/* ---------------------------------------------------------------------------
   Pruebas
   --------------------------------------------------------------------------- */
function filtradas() {
  const texto = normaliza(estado.texto);
  return pruebas.filter((p) => (!estado.categoria || limpio(p.categoria) === estado.categoria)
    && (!estado.proximas || pendiente(p))
    && (!texto || normaliza(`${p.titulo} ${p.sede ?? ''} ${p.categoria ?? ''}`).includes(texto)));
}

function descripcionCriterios() {
  return [
    estado.categoria ? `categoría ${textoCategoria(estado.categoria).toLowerCase()}` : '',
    estado.texto ? `con «${estado.texto}»` : '',
    estado.proximas ? 'pendientes de celebrar' : '',
  ].filter(Boolean).join(', ');
}

function pintar() {
  const nombre = nombreFederacion(federacionActual);
  const items = filtradas();
  const criterios = descripcionCriterios();
  const sinCalendario = !temporadas.length;

  $('#calt-skeleton').hidden = true;
  $('#calt-elige').hidden = true;
  $('#calt-error').hidden = true;
  $('#calt-titulo-lista').textContent = anioMostrado ? `${nombre} · ${anioMostrado}` : nombre;
  $('#calt-total').textContent = fmtNum.format(items.length);
  $('#calt-estado').textContent = sinCalendario
    ? `${nombre}: sin calendario publicado.`
    : `${plural(items.length, 'prueba', 'pruebas')} de ${nombre} en la temporada ${anioMostrado}${criterios ? ` · ${criterios}` : ''}.`;
  document.title = `Calendario ${nombre}${anioMostrado ? ` ${anioMostrado}` : ''} · Federación Española de Pádel`;
  actualizarFicha();
  pintarTemporada();

  const grupos = agrupar(items);
  const cont = $('#calt-lista');
  cont.classList.remove('is-loading');
  cont.replaceChildren(...grupos.map((g) => el('section', { class: 'camp-month', id: g.id, 'aria-labelledby': `${g.id}-titulo` },
    el('h3', { class: 'camp-month__title', id: `${g.id}-titulo` }, g.titulo, ' ', el('span', { class: 'org-count', text: fmtNum.format(g.items.length) })),
    el('ul', { class: 'camp-items' }, g.items.map((p) => el('li', {}, tarjeta(p)))))));
  cont.hidden = !items.length;
  $('#calt-sin-calendario').hidden = !sinCalendario;
  $('#calt-vacio').hidden = items.length > 0 || sinCalendario;

  pintarIndice(grupos);
  pintarResumen();
}

/** Cifras de la cabecera: de la federación elegida o, sin federación, del conjunto. */
function pintarResumen() {
  let resumen;
  if (estado.federacion && federacionActual) {
    const pendientes = pruebas.filter((p) => pendiente(p) && !p.fechaPorConfirmar).length;
    resumen = [
      [fmtNum.format(pruebas.length), anioMostrado ? `pruebas en ${anioMostrado}` : 'pruebas'],
      pendientes && pendientes < pruebas.length ? [fmtNum.format(pendientes), 'pendientes de celebrar'] : null,
      temporadas.length ? [fmtNum.format(temporadas.length), temporadas.length === 1 ? 'temporada' : 'temporadas'] : null,
    ];
  } else if (federacionesCargadas) {
    const con = federaciones.filter((f) => anios(f).length).length;
    resumen = [
      [fmtNum.format(con), con === 1 ? 'federación con calendario' : 'federaciones con calendario'],
      [fmtNum.format(federaciones.length), 'federaciones territoriales'],
    ];
  } else {
    resumen = [];
  }
  $('#calt-resumen').replaceChildren(...resumen.filter(Boolean).map(([n, t]) => el('li', {}, el('strong', { text: n }), ` ${t}`)));
}

/**
 * Grupos por mes de inicio, en el orden de la API (por fecha). Las pruebas sin fecha real (sin fecha o con la
 * fecha comodín 1 ene – 31 dic) van juntas al final, como "Fecha por confirmar".
 */
function agrupar(items) {
  const grupos = new Map();
  const sinFecha = [];
  for (const p of items) {
    const f = aFecha(p.fechaInicio);
    if (p.fechaPorConfirmar || !f) { sinFecha.push(p); continue; }
    const clave = `${f.getFullYear()}-${String(f.getMonth() + 1).padStart(2, '0')}`;
    if (!grupos.has(clave)) {
      // "Febrero de 2026"; en el índice, solo el mes salvo que sea de otro año que la temporada.
      const corto = f.getFullYear() === anioMostrado ? mayuscula(fmtMesCorto.format(f)) : mayuscula(fmtMes.format(f));
      grupos.set(clave, { id: `mes-${clave}`, titulo: mayuscula(fmtMes.format(f)), corto, items: [] });
    }
    grupos.get(clave).items.push(p);
  }
  const lista = [...grupos.values()];
  if (sinFecha.length) lista.push({ id: 'mes-por-confirmar', titulo: 'Fecha por confirmar', corto: 'Por confirmar', items: sinFecha });
  return lista;
}

function tarjeta(p) {
  const fecha = p.fechaPorConfirmar ? null : bloqueFecha(p.fechaInicio, p.fechaFin);
  const titulo = limpio(p.titulo);
  // Fichas migradas vía paginaMigrada; el resto (incluido Camp_Equipos.asp) sigue en la web actual.
  const legado = urlSegura(p.enlace);
  const href = legado ? (paginaMigrada(legado) ?? legado) : null;
  const externo = Boolean(p.nuevaVentana);
  const categoria = limpio(p.categoria);
  const sede = limpio(p.sede);

  return el('article', { class: 'camp-item' },
    fecha
      ? el('p', { class: 'camp-item__date' },
        el('time', { class: 'event__days', datetime: fecha.iso, text: fecha.dias }),
        el('span', { class: 'event__month', text: fecha.mes }))
      : el('p', { class: 'camp-item__date' }, el('span', { class: 'calf-date-tbd', text: 'Fecha por confirmar' })),
    el('div', { class: 'camp-item__body' },
      categoria
        ? el('ul', { class: 'camp-item__tags', 'aria-label': 'Categoría' },
          el('li', {}, el('span', { class: claseCategoria(categoria), text: textoCategoria(categoria) })))
        : null,
      el('h4', { class: 'camp-item__title' }, href
        ? el('a', { href, target: externo ? '_blank' : null, rel: externo ? 'noopener' : null },
          titulo,
          externo ? el('span', { class: 'sr-only', text: ' (se abre en una pestaña nueva)' }) : null,
          externo ? el('span', { class: 'camp-item__ext', 'aria-hidden': 'true', text: ' ↗' }) : null)
        : titulo),
      sede ? el('p', { class: 'camp-item__place' }, el('span', { class: 'sr-only', text: 'Sede: ' }), sede) : null));
}

/* ---------------------------------------------------------------------------
   Índice lateral: elección, pruebas por mes y federaciones; marca la sección visible
   --------------------------------------------------------------------------- */
function pintarIndice(grupos) {
  const entrada = (id, texto, cuenta) => el('li', {},
    el('a', { href: `#${id}`, dataset: { seccion: id } }, el('span', { text: texto }),
      cuenta != null ? el('span', { class: 'page-toc__count', text: String(cuenta) }) : null));
  $('#calt-indice').replaceChildren(...[
    entrada('filtros-calendario', 'Federación y temporada'),
    estado.federacion && temporadas.length ? entrada('pruebas', anioMostrado ? `Pruebas ${anioMostrado}` : 'Pruebas', pruebas.length) : null,
    ...grupos.map((g) => entrada(g.id, g.corto, g.items.length)),
    entrada('federaciones', 'Federaciones'),
  ].filter(Boolean));
  observarIndice();
}

function observarIndice() {
  if (!('IntersectionObserver' in window)) return;
  observador?.disconnect();
  const enlaces = new Map($$('#calt-indice a').map((a) => [a.dataset.seccion, a]));
  observador = new IntersectionObserver((entradas) => {
    for (const e of entradas) {
      if (!e.isIntersecting) continue;
      enlaces.forEach((a, clave) => {
        const activo = clave === e.target.id;
        a.classList.toggle('is-active', activo);
        if (activo) a.setAttribute('aria-current', 'location'); else a.removeAttribute('aria-current');
      });
    }
  }, { rootMargin: '-35% 0px -55% 0px' });
  enlaces.forEach((_, clave) => { const s = document.getElementById(clave); if (s) observador.observe(s); });
}

/* ---------------------------------------------------------------------------
   Arranque
   --------------------------------------------------------------------------- */
async function init() {
  if (!leerUrl()) return;
  sincronizarFormulario();
  initFormulario();
  await cargarFederaciones();
  // Federación de un enlace que no existe (p. ej. fed=4): como el ASP con fed=1000, se pide elegir una.
  if (estado.federacion && federacionesCargadas && !federaciones.some((f) => f.id === estado.federacion)) {
    estado.federacion = null;
  }
  federacionActual = federaciones.find((f) => f.id === estado.federacion) ?? null;
  buscar();
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
})();
