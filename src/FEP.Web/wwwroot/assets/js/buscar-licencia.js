/**
 * Página "Busca y consulta tu licencia" (migrada desde Licencias_Buscar.asp, /Licencias_Buscar; la sección #consulta
 * sustituye a Licencias_TuLicencia.asp, /Consulta_Tu_Licencia, cuyo acceso con número y clave sigue en la web actual:
 * es HTML estático, sin JS propio).
 * Requiere assets/js/comun.js (cabecera, menú, pie y utilidades en window.FEP).
 *
 *   Federaciones → GET /api/v1/federacion/federaciones-autonomicas   (respaldo: mock/federacion-federaciones-autonomicas.json)
 *   Resultados   → GET /api/v1/licencias?tipo&federacion&busqueda&fechaNacimiento
 *                                                                    (sin caché; respaldo: mock/licencias.json)
 *
 * Como en el ASP: tipo de licencia (jugador/a, técnico/a, juez-árbitro, silla de ruedas), federación (obligatoria para
 * jugador/a; los técnicos y jueces-árbitros no se buscan por federación), fecha de nacimiento (día, mes y año) y texto
 * del nombre o apellidos; hace falta el texto, la fecha o ambos. La tabla muestra licencia, apellidos y nombre; en
 * jugador/a el número enlaza a sus estadísticas (estadisticas.html, ya migrada).
 *
 * Protección de datos: los criterios (con la fecha de nacimiento) no se escriben en la URL de la página; solo se acepta
 * ?tipo= para preseleccionar el tipo (enlaces desde otras páginas). La API devuelve como mucho 100 licencias.
 *
 * Modo prototipo: mock/licencias.json trae dos licencias de ejemplo sin criterios. Si la respuesta no corresponde a lo
 * pedido (no es la API), se filtra por tipo y nombre en el navegador y se avisa de que son datos de ejemplo.
 * Todo texto de la API va por textContent.
 */
(function () {
'use strict';

const { $, $$, el, get, urlSegura, normaliza, paginaMigrada } = window.FEP;

const MAX_TEXTO = 100;
const MIN_TEXTO = 2;
const MAX_TERMINOS = 6;
const ANIO_MINIMO = 1920;          // el desplegable del ASP empezaba en 1930; la API admite desde 1900
const TIPOS = {
  jugador: { nombre: 'jugador/a', federacion: 'obligatoria' },
  tecnico: { nombre: 'técnico/a', federacion: null },
  arbitro: { nombre: 'juez-árbitro', federacion: null },
  'silla-ruedas': { nombre: 'pádel en silla de ruedas', federacion: 'opcional' },
};
const LETRAS_TIPO = { j: 'jugador', t: 'tecnico', a: 'arbitro', s: 'silla-ruedas' };
const MESES = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];
const PARTICULAS = new Set(['de', 'del', 'la', 'las', 'los', 'y', 'i', 'e', 'da', 'dos', 'van', 'von']);
const fmtNum = new Intl.NumberFormat('es-ES');

let federaciones = [];
let peticion = 0;                  // descarta respuestas de búsquedas anteriores

/* ---------------------------------------------------------------------------
   Utilidades
   --------------------------------------------------------------------------- */
/** "GARCIA DE VINUESA" → "Garcia de Vinuesa" (la ficha guarda los nombres en mayúsculas; mismo criterio que estadísticas). */
function nombrePropio(texto) {
  return String(texto ?? '').toLocaleLowerCase('es-ES')
    .replace(/[\p{L}\p{N}ªº]+/gu, (p, i) => (i > 0 && PARTICULAS.has(p) ? p : p.charAt(0).toLocaleUpperCase('es-ES') + p.slice(1)));
}

/** Igual que la API: términos separados por espacios o guiones. */
const terminos = (texto) => String(texto ?? '').split(/[\s-]+/).filter(Boolean);
const entero = (v) => (/^\d+$/.test(String(v ?? '').trim()) ? Number(v) : null);
const plural = (n, uno, varios) => `${fmtNum.format(n)} ${n === 1 ? uno : varios}`;
const dos = (n) => String(n).padStart(2, '0');

/** ?tipo=jugador|tecnico|arbitro|silla-ruedas (o J/T/A/S, como el ASP). */
function tipoDeUrl() {
  const v = (new URLSearchParams(location.search).get('tipo') ?? '').trim().toLowerCase();
  return TIPOS[v] ? v : (LETRAS_TIPO[v] ?? 'jugador');
}

/** Enlace de la API a la web actual: la página nueva si ya está migrada (Estadisticas?id= → estadisticas.html?licencia=). */
function enlace(url) {
  const segura = urlSegura(url);
  return segura ? (paginaMigrada(segura) ?? segura) : null;
}

/* ---------------------------------------------------------------------------
   Formulario
   --------------------------------------------------------------------------- */
function rellenarFecha() {
  $('#lic-dia').append(...Array.from({ length: 31 }, (_, i) => el('option', { value: String(i + 1), text: String(i + 1) })));
  $('#lic-mes').append(...MESES.map((m, i) => el('option', { value: String(i + 1), text: m })));
  const hoy = new Date().getFullYear();
  $('#lic-anio').append(...Array.from({ length: hoy - ANIO_MINIMO + 1 }, (_, i) => el('option', { value: String(hoy - i), text: String(hoy - i) })));
}

function pintarFederaciones() {
  const select = $('#lic-federacion');
  const tipo = $('#lic-tipo').value;
  const previa = select.value;
  const vacia = TIPOS[tipo]?.federacion === 'opcional' ? 'Todas las federaciones' : 'Elige tu federación';
  select.replaceChildren(el('option', { value: '', text: vacia }),
    ...federaciones.map((f) => el('option', { value: String(f.id), text: f.nombre })));
  if (federaciones.some((f) => String(f.id) === previa)) select.value = previa;
}

/** Muestra u oculta la federación según el tipo (en el ASP, cambiatipo()). */
function aplicarTipo() {
  const tipo = $('#lic-tipo').value;
  const modo = TIPOS[tipo]?.federacion ?? null;
  $('#lic-campo-federacion').hidden = !modo;
  $('#lic-federacion').required = modo === 'obligatoria';
  $('#lic-federacion-req').textContent = modo === 'obligatoria' ? '(obligatoria)' : '(opcional)';
  pintarFederaciones();
}

function marcarError(campo, mensaje) {
  for (const n of $$('#lic-form [aria-invalid]')) n.removeAttribute('aria-invalid');
  const error = $('#lic-error');
  if (!mensaje) {
    error.hidden = true;
    error.textContent = '';
    return;
  }
  error.textContent = mensaje;
  error.hidden = false;
  if (campo) {
    campo.setAttribute('aria-invalid', 'true');
    campo.focus();
  }
}

/** Lee y valida el formulario (las mismas reglas que la API). Devuelve los criterios o null si hay un error. */
function leerCriterios() {
  const tipo = $('#lic-tipo').value in TIPOS ? $('#lic-tipo').value : 'jugador';
  const modo = TIPOS[tipo].federacion;
  const federacion = modo ? entero($('#lic-federacion').value) : null;
  const texto = $('#lic-texto');
  const t = terminos(texto.value.slice(0, MAX_TEXTO));
  const [dia, mes, anio] = ['#lic-dia', '#lic-mes', '#lic-anio'].map((s) => entero($(s).value));
  const conFecha = [dia, mes, anio].some((v) => v !== null);

  if (modo === 'obligatoria' && !federacion) {
    marcarError($('#lic-federacion'), 'Selecciona la federación que expidió tu licencia.');
    return null;
  }
  if (conFecha && [dia, mes, anio].some((v) => v === null)) {
    const falta = dia === null ? '#lic-dia' : mes === null ? '#lic-mes' : '#lic-anio';
    marcarError($(falta), 'Completa el día, el mes y el año de nacimiento (o deja los tres en blanco).');
    return null;
  }
  let fecha = null;
  if (conFecha) {
    const d = new Date(anio, mes - 1, dia);
    if (d.getMonth() !== mes - 1 || d.getDate() !== dia) {
      marcarError($('#lic-dia'), `El ${dia} de ${MESES[mes - 1].toLowerCase()} de ${anio} no existe.`);
      return null;
    }
    if (d > new Date()) {
      marcarError($('#lic-anio'), 'La fecha de nacimiento no puede ser posterior a hoy.');
      return null;
    }
    fecha = `${anio}-${dos(mes)}-${dos(dia)}`;
  }
  if (t.length > MAX_TERMINOS) {
    marcarError(texto, `Escribe como mucho ${MAX_TERMINOS} palabras.`);
    return null;
  }
  if (t.length && t.join('').length < MIN_TEXTO) {
    marcarError(texto, `Escribe al menos ${MIN_TEXTO} letras del nombre o los apellidos.`);
    return null;
  }
  if (!t.length && !fecha) {
    marcarError(texto, 'Escribe tu nombre o apellidos, indica tu fecha de nacimiento o ambas cosas.');
    return null;
  }
  marcarError(null, null);
  return { tipo, federacion, busqueda: t.join(' '), fecha };
}

function initFormulario() {
  const form = $('#lic-form');
  $('#lic-tipo').addEventListener('change', () => { marcarError(null, null); aplicarTipo(); });
  form.addEventListener('submit', (e) => {
    e.preventDefault();
    const criterios = leerCriterios();
    if (criterios) buscar(criterios);
  });
  form.addEventListener('reset', () => {
    peticion++;
    marcarError(null, null);
    // El reset nativo restaura los valores por defecto: se aplica el tipo después.
    setTimeout(() => { aplicarTipo(); mostrarInicio(); });
  });
}

/* ---------------------------------------------------------------------------
   Búsqueda
   --------------------------------------------------------------------------- */
/** ¿La respuesta corresponde a lo pedido? (la API devuelve los criterios aplicados; el mock no) */
function esRespuestaDeApi(datos, c) {
  return datos && Array.isArray(datos.items)
    && datos.tipo === c.tipo
    && (datos.federacion ?? null) === (c.federacion ?? null)
    && (datos.busqueda ?? '') === c.busqueda
    && datos.conFechaNacimiento === Boolean(c.fecha);
}

/** Filtro en el navegador (solo en modo prototipo con el mock): tipo y nombre; federación y fecha no se pueden comprobar. */
function filtrarEnLocal(datos, c) {
  const t = terminos(c.busqueda).map(normaliza);
  const items = (datos?.tipo === c.tipo ? datos.items ?? [] : []).filter((x) =>
    t.every((p) => normaliza(`${x.apellido1 ?? ''} ${x.apellido2 ?? ''} ${x.nombre ?? ''}`).includes(p)));
  return { ...datos, tipo: c.tipo, federacion: c.federacion, busqueda: c.busqueda || null, conFechaNacimiento: Boolean(c.fecha),
    total: items.length, items, ejemplo: true };
}

async function buscar(c) {
  const id = ++peticion;
  mostrarCargando();

  const q = new URLSearchParams({ tipo: c.tipo });
  if (c.federacion) q.set('federacion', c.federacion);
  if (c.busqueda) q.set('busqueda', c.busqueda);
  if (c.fecha) q.set('fechaNacimiento', c.fecha);
  try {
    let datos = await get(`/api/v1/licencias?${q}`);
    if (id !== peticion) return;
    if (!esRespuestaDeApi(datos, c)) datos = filtrarEnLocal(datos, c);
    pintarResultados(datos, c);
    $('#resultados').scrollIntoView({ block: 'start' });
  } catch (e) {
    if (id !== peticion) return;
    console.warn('[FEP] licencias:', e.message);
    ocultarTodo();
    $('#lic-error-carga').hidden = false;
    $('#lic-estado').textContent = 'No se ha podido hacer la búsqueda.';
  } finally {
    if (id === peticion) $('#resultados').setAttribute('aria-busy', 'false');
  }
}

function ocultarTodo() {
  for (const s of ['#lic-inicio', '#lic-skeleton', '#lic-mas', '#lic-tabla-wrap', '#lic-vacio', '#lic-error-carga', '#lic-nota-estadisticas', '#lic-total']) {
    $(s).hidden = true;
  }
}

function mostrarInicio() {
  ocultarTodo();
  $('#lic-inicio').hidden = false;
  $('#lic-estado').textContent = '';
  $('#lic-filas').replaceChildren();
}

function mostrarCargando() {
  $('#resultados').setAttribute('aria-busy', 'true');
  const habiaTabla = !$('#lic-tabla-wrap').hidden;
  ocultarTodo();
  if (habiaTabla) {
    $('#lic-tabla-wrap').hidden = false;
    $('#lic-tabla-wrap').classList.add('is-loading');
  } else {
    $('#lic-skeleton').hidden = false;
  }
  $('#lic-estado').textContent = 'Buscando en la base de datos de la FEP…';
}

/** "licencias de jugador/a · Federación Madrileña de Pádel · «garcia» · con fecha de nacimiento" */
function descripcionCriterios(c) {
  const partes = [`licencias de ${TIPOS[c.tipo].nombre}`];
  const fed = federaciones.find((f) => f.id === c.federacion);
  if (fed) partes.push(fed.nombre);
  if (c.busqueda) partes.push(`«${c.busqueda}»`);
  if (c.fecha) partes.push('con fecha de nacimiento');
  return partes.join(' · ');
}

function filaLicencia(x, tipo) {
  const url = tipo === 'jugador' ? enlace(x.estadisticasUrl) : null;
  const nombre = [x.nombre, x.apellido1, x.apellido2].filter(Boolean).map(nombrePropio).join(' ');
  return el('tr', {},
    el('td', { class: 'lic-table__licencia', 'data-label': 'Licencia' },
      url ? el('a', { href: url, title: `Estadísticas de ${nombre}` }, x.licencia) : x.licencia),
    el('td', { 'data-label': 'Primer apellido', text: nombrePropio(x.apellido1) }),
    el('td', { 'data-label': 'Segundo apellido' }, x.apellido2 ? nombrePropio(x.apellido2) : el('span', { class: 'org-muted', text: '—' })),
    el('td', { 'data-label': 'Nombre', text: nombrePropio(x.nombre) }));
}

function pintarResultados(datos, c) {
  const items = (datos.items ?? []).filter((x) => x && x.licencia);
  const total = Number.isFinite(datos.total) ? datos.total : items.length;
  const criterios = descripcionCriterios(c);
  ocultarTodo();

  $('#lic-total').textContent = fmtNum.format(total);
  $('#lic-total').hidden = false;
  $('#lic-estado').textContent = `${plural(total, 'licencia encontrada', 'licencias encontradas')} · ${criterios}${datos.ejemplo ? ' (datos de ejemplo: la API no está disponible)' : ''}.`;
  $('#lic-caption').textContent = `Licencias encontradas: ${criterios}`;

  const wrap = $('#lic-tabla-wrap');
  wrap.classList.remove('is-loading');
  $('#lic-filas').replaceChildren(...items.map((x) => filaLicencia(x, c.tipo)));
  wrap.hidden = !items.length;
  $('#lic-vacio').hidden = items.length > 0;
  $('#lic-nota-estadisticas').hidden = !(items.length && c.tipo === 'jugador');

  // La API devuelve como mucho 100: si hay más, se pide afinar (el ASP las listaba todas).
  const mas = $('#lic-mas');
  if (total > items.length && items.length) {
    mas.replaceChildren(el('p', {},
      el('strong', { text: `Se muestran las ${fmtNum.format(items.length)} primeras de ${fmtNum.format(total)} licencias.` }),
      ' Añade más palabras del nombre o los apellidos, o tu fecha de nacimiento, para encontrar la tuya.'));
    mas.hidden = false;
  }
}

/* ---------------------------------------------------------------------------
   Índice lateral: marca la sección visible
   --------------------------------------------------------------------------- */
function initIndice() {
  if (!('IntersectionObserver' in window)) return;
  const enlaces = $$('#lic-indice a');
  const obs = new IntersectionObserver((entradas) => {
    for (const e of entradas) {
      if (!e.isIntersecting) continue;
      for (const a of enlaces) {
        const activo = a.getAttribute('href') === `#${e.target.id}`;
        a.classList.toggle('is-active', activo);
        if (activo) a.setAttribute('aria-current', 'true'); else a.removeAttribute('aria-current');
      }
    }
  }, { rootMargin: '-35% 0px -55% 0px' });
  for (const s of ['buscar', 'resultados', 'consulta', 'ayuda']) {
    const n = document.getElementById(s);
    if (n) obs.observe(n);
  }
}

/* ---------------------------------------------------------------------------
   Arranque
   --------------------------------------------------------------------------- */
async function init() {
  rellenarFecha();
  $('#lic-tipo').value = tipoDeUrl();
  initFormulario();
  initIndice();
  aplicarTipo();
  try {
    const d = await get('/api/v1/federacion/federaciones-autonomicas');
    federaciones = (d?.federaciones ?? [])
      .filter((f) => f && !f.nacional && Number.isInteger(f.id) && f.id >= 1 && f.id <= 22 && f.nombre)
      .map((f) => ({ id: f.id, nombre: f.nombre }))
      .sort((a, b) => a.nombre.localeCompare(b.nombre, 'es'));
  } catch (e) {
    // Sin federaciones no se puede buscar una licencia de jugador/a: se avisa y se enlaza a la web actual.
    console.warn('[FEP] federaciones:', e.message);
    $('#lic-federacion-ayuda').replaceChildren('No se ha podido cargar la lista de federaciones. Puedes buscar tu licencia en la ',
      el('a', { href: 'https://www.padelfederacion.es/Licencias_Buscar', text: 'web actual de la FEP' }), '.');
  }
  pintarFederaciones();
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
})();
