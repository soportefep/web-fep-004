/**
 * Página "Campeonatos y torneos" (migrada desde Campeonatos.asp).
 * Requiere assets/js/comun.js (cabecera, menú, pie y utilidades en window.FEP).
 *
 *   Temporadas + torneos → GET /api/v1/competicion/campeonatos?anio&tipo&categoria
 *                          (respaldo: mock/competicion-campeonatos.json)
 *
 * Como en el ASP, cambiar un desplegable vuelve a consultar al momento. Sin temporada se muestra la más
 * reciente. Los criterios viajan en la URL (?anio=2025&tipo=equipos&categoria=menores) y también se
 * aceptan los nombres del formulario antiguo (?ano=2025&modalidad=EQUIPOS&tipo=MENORES).
 * "Solo con inscripción abierta" es un filtro nuevo que se aplica en el navegador.
 *
 * Los filtros se vuelven a aplicar en el navegador sobre la respuesta: con la API no cambian nada (ya
 * vienen filtrados) y en modo prototipo el mock (todas las temporadas sin filtrar) se comporta como la API.
 * Fichas migradas: equipos (Camp_Equipos.asp → campeonato-equipos.html), parejas y veteranos (Camp_Parejas.asp /
 * Camp_Parejas_Veteranos.asp → campeonato-parejas.html) y silla de ruedas (Camp_Silla.asp → campeonato-silla.html).
 * Siguen fuera Camp_Clasificatorio.asp y las webs externas (circuito de menores…).
 * Todo texto de la API va por textContent.
 */
(function () {
'use strict';

const { $, $$, el, get, urlSegura, paginaMigrada, aFecha, bloqueFecha } = window.FEP;

const TIPOS = { parejas: 'Parejas', equipos: 'Equipos' };
const CATEGORIAS = { absoluto: 'Absoluto', menores: 'Menores', veteranos: 'Veteranos' };
const PLAZOS = {
  inscripcion: 'Inscripción abierta',
  'inscripcion-equipos': 'Inscripción de equipos abierta',
  preinscripcion: 'Preinscripción abierta',
};
const ANIO_MIN = 1901;
const ANIO_MAX = 2100;
const fmtNum = new Intl.NumberFormat('es-ES');
const fmtMes = new Intl.DateTimeFormat('es-ES', { month: 'long', year: 'numeric' });
const fmtPlazo = new Intl.DateTimeFormat('es-ES', {
  weekday: 'long', day: 'numeric', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit',
});

/** Estado de la consulta: anio null = la temporada más reciente. */
const estado = { anio: null, tipo: '', categoria: '', abiertos: false };
let anios = [];
let anioMostrado = null;
let peticion = 0;          // descarta respuestas de consultas anteriores

/* ---------------------------------------------------------------------------
   Utilidades
   --------------------------------------------------------------------------- */
const entero = (v) => (/^\d+$/.test(String(v ?? '').trim()) ? Number(v) : null);
const plural = (n, uno, varios) => `${fmtNum.format(n)} ${n === 1 ? uno : varios}`;
const limpio = (t) => String(t ?? '').replace(/\s+/g, ' ').trim();
const mayuscula = (t) => (t ? t.charAt(0).toLocaleUpperCase('es-ES') + t.slice(1) : t);

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
  estado.anio = anio >= ANIO_MIN && anio <= ANIO_MAX ? anio : null;

  // Formulario antiguo: "modalidad" era el tipo (PAREJAS/EQUIPOS) y "tipo" la categoría (ABSOLUTO…).
  const valores = [q.get('tipo'), q.get('modalidad'), q.get('categoria')].map((v) => String(v ?? '').trim().toLowerCase());
  estado.tipo = valores.find((v) => v in TIPOS) ?? '';
  estado.categoria = valores.find((v) => v in CATEGORIAS) ?? '';
  estado.abiertos = q.get('abiertos') === '1';
}

function escribirUrl() {
  const q = new URLSearchParams();
  if (estado.anio) q.set('anio', estado.anio);
  if (estado.tipo) q.set('tipo', estado.tipo);
  if (estado.categoria) q.set('categoria', estado.categoria);
  if (estado.abiertos) q.set('abiertos', '1');
  const s = q.toString();
  try {
    history.replaceState(null, '', `${location.pathname}${s ? `?${s}` : ''}${location.hash}`);
  } catch {
    // Abierta desde disco (file://) algunos navegadores no permiten cambiar la URL: no es imprescindible.
  }
}

/* ---------------------------------------------------------------------------
   Formulario, temporadas e índice
   --------------------------------------------------------------------------- */
function pintarAnios() {
  const select = $('#camp-anio');
  select.replaceChildren(...anios.map((a) => el('option', { value: String(a), selected: a === anioMostrado, text: String(a) })));
  select.disabled = !anios.length;

  const lista = $('#camp-indice');
  lista.replaceChildren(...anios.map((a) => el('li', {},
    el('a', { href: `campeonatos.html?anio=${a}`, dataset: { anio: String(a) } }, el('span', { text: `Temporada ${a}` })))));
  marcarIndice();
}

function marcarIndice() {
  for (const a of $$('#camp-indice a')) {
    const activo = entero(a.dataset.anio) === anioMostrado;
    a.classList.toggle('is-active', activo);
    if (activo) a.setAttribute('aria-current', 'page'); else a.removeAttribute('aria-current');
  }
  const select = $('#camp-anio');
  if (anioMostrado && select.value !== String(anioMostrado)) select.value = String(anioMostrado);
}

function sincronizarFormulario() {
  $('#camp-tipo').value = estado.tipo;
  $('#camp-categoria').value = estado.categoria;
  $('#camp-abiertos').checked = estado.abiertos;
}

function initFormulario() {
  $('#camp-anio').addEventListener('change', (e) => { estado.anio = entero(e.target.value); buscar(); });
  $('#camp-tipo').addEventListener('change', (e) => { estado.tipo = e.target.value in TIPOS ? e.target.value : ''; buscar(); });
  $('#camp-categoria').addEventListener('change', (e) => { estado.categoria = e.target.value in CATEGORIAS ? e.target.value : ''; buscar(); });
  // Filtro local: no hace falta volver a pedir nada.
  $('#camp-abiertos').addEventListener('change', (e) => { estado.abiertos = e.target.checked; escribirUrl(); pintar(); });
  $('#camp-form').addEventListener('submit', (e) => e.preventDefault());
  $('#camp-form').addEventListener('reset', () => {
    Object.assign(estado, { anio: null, tipo: '', categoria: '', abiertos: false });
    // El reset nativo restaura las opciones por defecto: se sincroniza después.
    setTimeout(() => { sincronizarFormulario(); buscar(); });
  });

  $('#camp-indice').addEventListener('click', (e) => {
    const a = e.target.closest('a[data-anio]');
    if (!a || e.ctrlKey || e.metaKey || e.shiftKey || e.button !== 0) return;
    e.preventDefault();
    estado.anio = entero(a.dataset.anio);
    buscar({ desplazar: true });
  });
}

/* ---------------------------------------------------------------------------
   Consulta
   --------------------------------------------------------------------------- */
let ultimos = [];   // torneos de la temporada ya filtrados por tipo y categoría

async function buscar({ desplazar = false } = {}) {
  const id = ++peticion;
  escribirUrl();
  mostrarCargando();

  const q = new URLSearchParams();
  if (estado.anio) q.set('anio', estado.anio);
  if (estado.tipo) q.set('tipo', estado.tipo);
  if (estado.categoria) q.set('categoria', estado.categoria);
  const s = q.toString();

  try {
    const datos = await get(`/api/v1/competicion/campeonatos${s ? `?${s}` : ''}`);
    if (id !== peticion) return;

    const lista = (Array.isArray(datos?.anios) ? datos.anios : []).filter((a) => Number.isInteger(a));
    // Temporada pedida que no existe (enlace antiguo): se vuelve a la más reciente.
    if (estado.anio && lista.length && !lista.includes(estado.anio)) {
      estado.anio = null;
      return buscar({ desplazar });
    }
    anioMostrado = estado.anio ?? (Number.isInteger(datos?.anio) ? datos.anio : lista[0] ?? null);
    if (lista.join() !== anios.join()) { anios = lista; pintarAnios(); } else marcarIndice();

    ultimos = (Array.isArray(datos?.items) ? datos.items : []).filter((c) => c && Number.isInteger(c.id) && c.titulo
      && (c.anio ?? anioMostrado) === anioMostrado
      && (!estado.tipo || c.tipo === estado.tipo)
      && (!estado.categoria || (c.categoria === estado.categoria && !c.seleccion)));
    pintar();
    if (desplazar) $('#resultados').scrollIntoView({ block: 'start' });
  } catch (e) {
    if (id !== peticion) return;
    console.warn('[FEP] campeonatos:', e.message);
    $('#camp-skeleton').hidden = true;
    $('#camp-lista').hidden = true;
    $('#camp-vacio').hidden = true;
    $('#camp-error').hidden = false;
    $('#camp-total').textContent = '–';
    $('#camp-estado').textContent = 'No se han podido cargar los campeonatos.';
    $('#camp-anio').disabled = !anios.length;
  } finally {
    if (id === peticion) $('#resultados').setAttribute('aria-busy', 'false');
  }
}

function mostrarCargando() {
  $('#resultados').setAttribute('aria-busy', 'true');
  $('#camp-error').hidden = true;
  // Primera carga: skeleton. Después se deja la lista anterior atenuada hasta que llega la nueva.
  if ($('#camp-lista').hidden && $('#camp-vacio').hidden) $('#camp-skeleton').hidden = false;
  $('#camp-lista').classList.add('is-loading');
}

/* ---------------------------------------------------------------------------
   Pintado
   --------------------------------------------------------------------------- */
function descripcionCriterios() {
  return [
    estado.tipo ? `por ${estado.tipo}` : '',
    estado.categoria ? `categoría ${CATEGORIAS[estado.categoria].toLowerCase()}` : '',
    estado.abiertos ? 'con inscripción abierta' : '',
  ].filter(Boolean).join(', ');
}

function pintar() {
  const abiertos = ultimos.filter(plazoVigente);
  const items = estado.abiertos ? abiertos : ultimos;
  const criterios = descripcionCriterios();

  $('#camp-skeleton').hidden = true;
  $('#camp-titulo-lista').textContent = anioMostrado ? `Campeonatos ${anioMostrado}` : 'Campeonatos';
  $('#camp-total').textContent = fmtNum.format(items.length);
  $('#camp-estado').textContent = `${plural(items.length, 'torneo', 'torneos')}${anioMostrado ? ` en la temporada ${anioMostrado}` : ''}${criterios ? ` · ${criterios}` : ''}.`;

  const cont = $('#camp-lista');
  cont.classList.remove('is-loading');
  cont.replaceChildren(...agruparPorMes(items).map(([titulo, grupo]) => el('section', { class: 'camp-month', 'aria-label': titulo },
    el('h3', { class: 'camp-month__title' }, titulo, ' ', el('span', { class: 'org-count', text: fmtNum.format(grupo.length) })),
    el('ul', { class: 'camp-items' }, grupo.map((c) => el('li', {}, tarjeta(c)))))));
  cont.hidden = !items.length;
  $('#camp-vacio').hidden = items.length > 0;

  const resumen = [
    [fmtNum.format(ultimos.length), anioMostrado ? `torneos en ${anioMostrado}` : 'torneos'],
    [fmtNum.format(abiertos.length), 'con inscripción abierta'],
    anios.length ? [fmtNum.format(anios.length), 'temporadas'] : null,
  ].filter(Boolean);
  $('#camp-resumen').replaceChildren(...resumen.map(([n, t]) => el('li', {}, el('strong', { text: n }), ` ${t}`)));
}

/** Grupos por mes de inicio, en el orden de la API (del más reciente al más antiguo). */
function agruparPorMes(items) {
  const grupos = new Map();
  for (const c of items) {
    const f = aFecha(c.fechaInicio);
    const titulo = f ? mayuscula(fmtMes.format(f)) : 'Sin fecha';
    if (!grupos.has(titulo)) grupos.set(titulo, []);
    grupos.get(titulo).push(c);
  }
  return [...grupos];
}

/**
 * Torneo de silla de ruedas por parejas: la API enlaza Camp_Parejas.asp (Torneos_Previo.asp), que redirigía a
 * Camp_Silla.asp si Circuito='SILLA'. Esa ficha ya está migrada (campeonato-silla.html).
 */
function fichaSilla(c, legado) {
  if (!c.sillaRuedas) return null;
  try {
    const url = new URL(legado);
    if (!/^\/camp_parejas(\.asp)?$/i.test(url.pathname)) return null;
    const q = new Map([...url.searchParams].map(([k, v]) => [k.toLowerCase(), v.trim()]));
    return paginaMigrada(new URL(`Camp_Silla.asp?IdTorneo=${encodeURIComponent(q.get('idtorneo') ?? '')}${url.hash}`, url).href);
  } catch {
    return null;
  }
}

/**
 * Enlace a los cuadros (cuadro.html): torneos de la web de la FEP (no el circuito de menores, que tiene su propia
 * web) que ya han empezado o empiezan en la próxima semana, que es cuando se publican los cuadros.
 */
const DIAS_ANTES_CUADROS = 7;
function conCuadros(c, legado) {
  if (!legado) return false;
  try {
    const url = new URL(legado);
    if (!/^(www\.)?padelfederacion\.es$/i.test(url.hostname) || !/^\/camp_/i.test(url.pathname)) return false;
  } catch {
    return false;
  }
  const inicio = aFecha(c.fechaInicio);
  return Boolean(inicio) && inicio.getTime() - Date.now() <= DIAS_ANTES_CUADROS * 864e5;
}

function tarjeta(c) {
  const fecha = bloqueFecha(c.fechaInicio, c.fechaFin);
  const titulo = limpio(c.titulo);
  // Fichas ya migradas (los de silla de ruedas por parejas, que Camp_Parejas.asp redirigía a Camp_Silla.asp →
  // campeonato-silla.html); el resto, incluidos los torneos por equipos (Camp_Equipos.asp), sigue en la web actual.
  const legado = urlSegura(c.enlace);
  const href = legado ? (fichaSilla(c, legado) ?? paginaMigrada(legado) ?? legado) : null;
  const externo = Boolean(c.nuevaVentana);
  const plazo = plazoVigente(c);
  const clubes = (Array.isArray(c.clubes) ? c.clubes : []).map(limpio).filter(Boolean);
  const lugar = [limpio(c.ciudad), clubes.join(' y ')].filter(Boolean).join(' · ');

  const etiquetas = [
    TIPOS[c.tipo] ? { texto: TIPOS[c.tipo], clase: 'org-tag' } : null,
    CATEGORIAS[c.categoria] ? { texto: CATEGORIAS[c.categoria], clase: `org-tag camp-tag camp-tag--${c.categoria}` } : null,
    c.seleccion ? { texto: 'Selecciones autonómicas', clase: 'org-tag' } : null,
    c.sillaRuedas ? { texto: 'Silla de ruedas', clase: 'org-tag' } : null,
  ].filter(Boolean);

  return el('article', { class: `camp-item${plazo ? ' camp-item--abierto' : ''}` },
    fecha
      ? el('p', { class: 'camp-item__date' },
        el('time', { class: 'event__days', datetime: fecha.iso, text: fecha.dias }),
        el('span', { class: 'event__month', text: fecha.mes }))
      : el('p', { class: 'camp-item__date', 'aria-hidden': 'true' }),
    el('div', { class: 'camp-item__body' },
      etiquetas.length
        ? el('ul', { class: 'camp-item__tags', 'aria-label': 'Tipo y categoría' },
          etiquetas.map((t) => el('li', {}, el('span', { class: t.clase, text: t.texto }))))
        : null,
      el('h4', { class: 'camp-item__title' }, href
        ? el('a', { href, target: externo ? '_blank' : null, rel: externo ? 'noopener' : null },
          titulo,
          externo ? el('span', { class: 'sr-only', text: ' (se abre en una pestaña nueva)' }) : null,
          externo ? el('span', { class: 'camp-item__ext', 'aria-hidden': 'true', text: ' ↗' }) : null)
        : titulo),
      lugar ? el('p', { class: 'camp-item__place', text: lugar }) : null,
      conCuadros(c, legado)
        ? el('a', { class: 'camp-item__draws', href: `cuadro.html?torneo=${c.id}` },
          'Cuadros y resultados', el('span', { 'aria-hidden': 'true', text: ' →' }),
          el('span', { class: 'sr-only', text: ` de ${titulo}` }))
        : null,
      plazo
        ? el('p', { class: 'camp-item__plazo' },
          el('strong', { text: PLAZOS[plazo.tipo] }),
          ' hasta el ',
          el('time', { datetime: c.inscripcion.fechaLimite, text: fmtPlazo.format(plazo.fecha) }))
        : null));
}

/* ---------------------------------------------------------------------------
   Arranque
   --------------------------------------------------------------------------- */
function init() {
  leerUrl();
  sincronizarFormulario();
  initFormulario();
  buscar();
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
})();
