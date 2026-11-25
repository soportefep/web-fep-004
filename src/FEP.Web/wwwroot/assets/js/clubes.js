/**
 * Página "Clubes federados" (migrada desde Clubes.asp).
 * Requiere assets/js/comun.js (cabecera, menú, pie y utilidades en window.FEP).
 *
 *   Desplegables → GET /api/v1/clubes/filtros                (respaldo: mock/clubes-filtros.json)
 *   Resultados   → GET /api/v1/clubes?federacion&provincia&busqueda&pagina&limite
 *                                                            (respaldo: mock/clubes.json)
 *
 * Como en el ASP, cambiar la federación o la provincia busca al momento (al cambiar de federación
 * se vacía la provincia y el desplegable muestra solo las de su comunidad); el texto se busca al
 * enviar o tras una pausa al escribir. Los criterios viajan en la URL (?federacion=1&pagina=2), así
 * que los resultados se pueden enlazar; también se acepta ?fed= de la web actual.
 *
 * Modo prototipo: el mock trae todos los clubes en una sola "página" sin filtros. Si la respuesta no
 * corresponde a los criterios pedidos (no es la API), se filtra y pagina en el navegador.
 * La ficha del club (Club.asp) aún no está migrada: enlaza a la web actual.
 * Todo texto de la API va por textContent.
 */
(function () {
'use strict';

const { $, $$, el, get, urlSegura, normaliza } = window.FEP;

const LIMITE = 50;                 // clubes por página (la API admite 1–100)
const MAX_TEXTO = 100;
const MAX_TERMINOS = 8;
const PAUSA_TEXTO_MS = 400;
const fmtNum = new Intl.NumberFormat('es-ES');
const PARTICULAS = new Set(['de', 'del', 'la', 'las', 'los', 'y', 'e', 'el']);

/** Estado de la búsqueda (lo que se pide a la API). */
const estado = { federacion: null, provincia: null, busqueda: '', pagina: 1 };
let filtros = { federaciones: [], provincias: [] };
let peticion = 0;          // descarta respuestas de búsquedas anteriores
let resumenPintado = false;

/* ---------------------------------------------------------------------------
   Utilidades
   --------------------------------------------------------------------------- */
/** "COM.VALENCIANA" → "Com.Valenciana"; "SANTA CRUZ DE TENERIFE" → "Santa Cruz de Tenerife". */
function nombrePropio(texto) {
  return String(texto ?? '').toLocaleLowerCase('es-ES')
    .replace(/[\p{L}\p{N}]+/gu, (p, i) => (i > 0 && PARTICULAS.has(p) ? p : p.charAt(0).toLocaleUpperCase('es-ES') + p.slice(1)));
}

/** Igual que la API: términos separados por espacios o guiones. */
const terminos = (texto) => String(texto ?? '').split(/[\s-]+/).filter(Boolean);

const entero = (v) => (/^\d+$/.test(String(v ?? '').trim()) ? Number(v) : null);
const plural = (n, uno, varios) => `${fmtNum.format(n)} ${n === 1 ? uno : varios}`;

/** Ficha del club en la web actual (Club.asp aún no está migrada). */
const urlFicha = (id) => urlSegura(`Club.asp?id=${encodeURIComponent(id)}`);

const federacionPorId = (id) => filtros.federaciones.find((f) => f.id === id) ?? null;
const provinciaPorId = (id) => filtros.provincias.find((p) => p.id === id) ?? null;

/* ---------------------------------------------------------------------------
   Estado ↔ URL
   --------------------------------------------------------------------------- */
function leerUrl() {
  const q = new URLSearchParams(location.search);
  // 0 era "----" en la web actual; fuera de rango la API respondería 400: se ignoran.
  const fed = entero(q.get('federacion') ?? q.get('fed'));
  estado.federacion = fed >= 1 && fed <= 22 ? fed : null;   // las 22 federaciones autonómicas (DECISIÓN FEP)
  const prov = entero(q.get('provincia'));
  estado.provincia = prov >= 1 && prov <= 999 ? prov : null;
  estado.busqueda = terminos((q.get('busqueda') ?? q.get('textobuscado') ?? '').slice(0, MAX_TEXTO)).slice(0, MAX_TERMINOS).join(' ');
  estado.pagina = Math.min(1000, Math.max(1, entero(q.get('pagina')) ?? 1));
}

function consulta(conPagina = true) {
  const q = new URLSearchParams();
  if (estado.federacion) q.set('federacion', estado.federacion);
  if (estado.provincia) q.set('provincia', estado.provincia);
  if (estado.busqueda) q.set('busqueda', estado.busqueda);
  if (conPagina && estado.pagina > 1) q.set('pagina', estado.pagina);
  return q;
}

function escribirUrl() {
  const q = consulta().toString();
  try {
    history.replaceState(null, '', `${location.pathname}${q ? `?${q}` : ''}${location.hash}`);
  } catch {
    // Abierta desde disco (file://) algunos navegadores no permiten cambiar la URL: no es imprescindible.
  }
}

/* ---------------------------------------------------------------------------
   Formulario y desplegables
   --------------------------------------------------------------------------- */
function opciones(select, lista, seleccionado) {
  select.replaceChildren(el('option', { value: '', text: 'Todas' }),
    ...lista.map((o) => el('option', { value: String(o.id), selected: o.id === seleccionado, text: nombrePropio(o.nombre) })));
}

/** Provincias del desplegable: las de la federación elegida o todas. */
function pintarProvincias() {
  const fed = federacionPorId(estado.federacion);
  const ids = fed ? new Set(fed.provincias ?? []) : null;
  const lista = ids ? filtros.provincias.filter((p) => ids.has(p.id)) : filtros.provincias;
  if (estado.provincia && !lista.some((p) => p.id === estado.provincia)) estado.provincia = null;
  opciones($('#clubes-provincia'), lista, estado.provincia);
}

function sincronizarFormulario() {
  opciones($('#clubes-federacion'), filtros.federaciones, estado.federacion);
  pintarProvincias();
  $('#clubes-texto').value = estado.busqueda;
}

/** Índice lateral: una entrada por federación (enlace con ?federacion=). */
function pintarIndice() {
  const lista = $('#clubes-indice');
  const entradas = [{ id: null, nombre: 'Todas' }, ...filtros.federaciones];
  lista.replaceChildren(...entradas.map((f) => el('li', {},
    el('a', { href: f.id ? `clubes.html?federacion=${f.id}` : 'clubes.html', dataset: { federacion: f.id ?? '' } },
      el('span', { text: f.id ? nombrePropio(f.nombre) : f.nombre })))));
  lista.addEventListener('click', (e) => {
    const a = e.target.closest('a[data-federacion]');
    if (!a || e.ctrlKey || e.metaKey || e.shiftKey || e.button !== 0) return;
    e.preventDefault();
    estado.federacion = entero(a.dataset.federacion);
    estado.provincia = null;
    estado.pagina = 1;
    sincronizarFormulario();
    buscar({ desplazar: true });
  });
  marcarIndice();
}

function marcarIndice() {
  for (const a of $$('#clubes-indice a')) {
    const activo = (entero(a.dataset.federacion) ?? null) === (estado.federacion ?? null);
    a.classList.toggle('is-active', activo);
    if (activo) a.setAttribute('aria-current', 'page'); else a.removeAttribute('aria-current');
  }
}

function initFormulario() {
  const form = $('#clubes-form');
  const texto = $('#clubes-texto');
  let pausa;

  $('#clubes-federacion').addEventListener('change', (e) => {
    estado.federacion = entero(e.target.value);
    estado.provincia = null;               // como tipo=federacion en el ASP
    estado.pagina = 1;
    pintarProvincias();
    buscar();
  });
  $('#clubes-provincia').addEventListener('change', (e) => {
    estado.provincia = entero(e.target.value);
    estado.pagina = 1;
    buscar();
  });

  const buscarTexto = () => {
    clearTimeout(pausa);
    const t = terminos(texto.value.slice(0, MAX_TEXTO));
    const nuevo = t.slice(0, MAX_TERMINOS).join(' ');
    if (nuevo === estado.busqueda) return;
    estado.busqueda = nuevo;
    estado.pagina = 1;
    buscar();
  };
  texto.addEventListener('input', () => {
    clearTimeout(pausa);
    const largo = texto.value.trim().length;
    if (largo === 0 || largo >= 2) pausa = setTimeout(buscarTexto, PAUSA_TEXTO_MS);
  });
  form.addEventListener('submit', (e) => { e.preventDefault(); buscarTexto(); });
  form.addEventListener('reset', () => {
    clearTimeout(pausa);
    Object.assign(estado, { federacion: null, provincia: null, busqueda: '', pagina: 1 });
    // El reset nativo restaura las opciones por defecto: se repintan después.
    setTimeout(() => { sincronizarFormulario(); buscar(); });
  });

  $('#clubes-anterior').addEventListener('click', () => irAPagina(estado.pagina - 1));
  $('#clubes-siguiente').addEventListener('click', () => irAPagina(estado.pagina + 1));
}

function irAPagina(n) {
  estado.pagina = Math.max(1, n);
  buscar({ desplazar: true });
}

/* ---------------------------------------------------------------------------
   Búsqueda
   --------------------------------------------------------------------------- */
/** ¿La respuesta corresponde a lo pedido? (la API devuelve los criterios aplicados; el mock no) */
function esRespuestaDeApi(datos) {
  return datos && Array.isArray(datos.items)
    && (datos.federacion ?? null) === (estado.federacion ?? null)
    && (datos.provincia ?? null) === (estado.provincia ?? null)
    && (datos.busqueda ?? '') === estado.busqueda
    && datos.pagina === estado.pagina
    && datos.limite === LIMITE;
}

/** Filtro y paginación en el navegador (solo en modo prototipo con el mock). */
function filtrarEnLocal(datos) {
  const fed = federacionPorId(estado.federacion);
  const provinciasFed = fed ? new Set(fed.provincias ?? []) : null;
  const t = terminos(estado.busqueda).map(normaliza);
  const todos = (datos?.items ?? []).filter((c) =>
    (!provinciasFed || provinciasFed.has(c.provinciaId))
    && (!estado.provincia || c.provinciaId === estado.provincia)
    && t.every((x) => normaliza(c.nombre).includes(x)));
  const desde = (estado.pagina - 1) * LIMITE;
  return {
    anio: datos?.anio, federacion: estado.federacion, provincia: estado.provincia, busqueda: estado.busqueda || null,
    pagina: estado.pagina, limite: LIMITE, total: todos.length, items: todos.slice(desde, desde + LIMITE),
  };
}

async function buscar({ desplazar = false } = {}) {
  const id = ++peticion;
  escribirUrl();
  marcarIndice();
  mostrarCargando();

  const q = consulta();
  q.set('limite', LIMITE);
  try {
    let datos = await get(`/api/v1/clubes?${q}`);
    if (id !== peticion) return;
    if (!esRespuestaDeApi(datos)) datos = filtrarEnLocal(datos);

    // Si la página pedida ya no existe (p. ej. enlace antiguo), se va a la última.
    const paginas = Math.max(1, Math.ceil((datos.total ?? 0) / LIMITE));
    if (estado.pagina > paginas && datos.total > 0) {
      estado.pagina = paginas;
      return buscar({ desplazar });
    }
    pintarResultados(datos, paginas);
    if (!resumenPintado && !estado.federacion && !estado.provincia && !estado.busqueda) pintarResumen(datos);
    if (desplazar) $('#resultados').scrollIntoView({ block: 'start' });
  } catch (e) {
    if (id !== peticion) return;
    console.warn('[FEP] clubes:', e.message);
    $('#clubes-skeleton').hidden = true;
    $('#clubes-tabla-wrap').hidden = true;
    $('#clubes-paginacion').hidden = true;
    $('#clubes-vacio').hidden = true;
    $('#clubes-error').hidden = false;
    $('#clubes-total').textContent = '–';
    $('#clubes-estado').textContent = 'No se han podido cargar los clubes.';
  } finally {
    if (id === peticion) $('#resultados').setAttribute('aria-busy', 'false');
  }
}

function mostrarCargando() {
  $('#resultados').setAttribute('aria-busy', 'true');
  $('#clubes-error').hidden = true;
  // Primera carga: skeleton. Después se deja la tabla anterior atenuada hasta que llega la nueva.
  if ($('#clubes-tabla-wrap').hidden && $('#clubes-vacio').hidden) $('#clubes-skeleton').hidden = false;
  $('#clubes-tabla-wrap').classList.add('is-loading');
}

/** Descripción de los criterios para el estado y el caption: "Federación Madrileña · «club pádel»". */
function descripcionCriterios() {
  const partes = [];
  const fed = federacionPorId(estado.federacion);
  const prov = provinciaPorId(estado.provincia);
  if (fed) partes.push(`federación ${nombrePropio(fed.nombre)}`);
  if (prov) partes.push(`provincia de ${nombrePropio(prov.nombre)}`);
  if (estado.busqueda) partes.push(`nombre con «${estado.busqueda}»`);
  return partes.join(', ');
}

function filaClub(c) {
  const ficha = urlFicha(c.id);
  return el('tr', {},
    el('td', { class: 'org-table__name clubs-table__club' },
      ficha ? el('a', { href: ficha, text: c.nombre }) : c.nombre),
    el('td', { class: 'clubs-table__lugar', text: nombrePropio(c.provincia) }),
    el('td', { class: 'clubs-table__lugar' }, c.localidad ? c.localidad : el('span', { class: 'org-muted', text: '—' })));
}

function pintarResultados(datos, paginas) {
  const items = (datos.items ?? []).filter((c) => c && c.id && c.nombre);
  const total = datos.total ?? items.length;
  const criterios = descripcionCriterios();
  const anio = datos.anio ? ` en ${datos.anio}` : '';

  $('#clubes-skeleton').hidden = true;
  $('#clubes-total').textContent = fmtNum.format(total);
  $('#clubes-estado').textContent = `${plural(total, 'club', 'clubes')} con la licencia renovada${anio}${criterios ? ` · ${criterios}` : ''}.`;
  $('#clubes-caption').textContent = `Clubes federados${criterios ? ` (${criterios})` : ''}, página ${estado.pagina} de ${paginas}`;

  const wrap = $('#clubes-tabla-wrap');
  wrap.classList.remove('is-loading');
  $('#clubes-filas').replaceChildren(...items.map(filaClub));
  wrap.hidden = !items.length;
  $('#clubes-vacio').hidden = items.length > 0;

  const pager = $('#clubes-paginacion');
  pager.hidden = paginas <= 1;
  $('#clubes-pagina').textContent = `Página ${estado.pagina} de ${paginas}`;
  $('#clubes-anterior').disabled = estado.pagina <= 1;
  $('#clubes-siguiente').disabled = estado.pagina >= paginas;
}

function pintarResumen(datos) {
  resumenPintado = true;
  const datosResumen = [
    [fmtNum.format(datos.total ?? 0), `clubes con licencia${datos.anio ? ` en ${datos.anio}` : ''}`],
    filtros.federaciones.length ? [String(filtros.federaciones.length), 'federaciones autonómicas'] : null,
  ].filter(Boolean);
  $('#clubes-resumen').replaceChildren(...datosResumen.map(([n, t]) => el('li', {}, el('strong', { text: n }), ` ${t}`)));
}

/* ---------------------------------------------------------------------------
   Arranque
   --------------------------------------------------------------------------- */
async function init() {
  leerUrl();
  initFormulario();
  try {
    const f = await get('/api/v1/clubes/filtros');
    filtros = {
      federaciones: (f?.federaciones ?? []).filter((x) => Number.isInteger(x?.id) && x.nombre),
      provincias: (f?.provincias ?? []).filter((x) => Number.isInteger(x?.id) && x.nombre),
    };
  } catch (e) {
    // Sin desplegables se puede seguir buscando por nombre.
    console.warn('[FEP] filtros de clubes:', e.message);
    $('#clubes-federacion').disabled = true;
    $('#clubes-provincia').disabled = true;
  }
  if (estado.federacion && !federacionPorId(estado.federacion) && filtros.federaciones.length) estado.federacion = null;
  sincronizarFormulario();
  if (filtros.federaciones.length) pintarIndice();
  buscar();
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
})();
