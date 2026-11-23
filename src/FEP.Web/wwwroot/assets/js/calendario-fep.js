/**
 * Página "Calendario oficial FEP" (migrada desde CalendarioFEP.asp, URL antigua /CalendarioFEP).
 * Requiere assets/js/comun.js (cabecera, menú, pie y utilidades en window.FEP).
 *
 *   Temporadas + PDF + convocatoria + pruebas → GET /api/v1/competicion/calendario?anio
 *                                               (respaldo: mock/competicion-calendario.json)
 *
 * Como en el ASP, cambiar de temporada vuelve a consultar al momento. Sin temporada se muestra la que decide la
 * API (la del año en curso). La temporada viaja en la URL (?anio=2027) y también se acepta el nombre del
 * formulario antiguo (?ano=2027). Categoría, texto y "pendientes de celebrar" son filtros nuevos que se aplican
 * en el navegador. Las pruebas se vuelven a filtrar por temporada en el navegador: con la API no cambia nada (ya
 * vienen filtradas) y en modo prototipo el mock (todas las temporadas) se comporta como la API.
 * Todo texto de la API va por textContent.
 */
(function () {
'use strict';

const { $, $$, el, get, urlSegura, paginaMigrada, aFecha, bloqueFecha, fmtLargo, normaliza } = window.FEP;

const ANIO_MIN = 1901;
const ANIO_MAX = 2100;
const MAX_DOCS_VISIBLES = 4;
const fmtNum = new Intl.NumberFormat('es-ES');
const fmtMes = new Intl.DateTimeFormat('es-ES', { month: 'long', year: 'numeric' });
const fmtMesCorto = new Intl.DateTimeFormat('es-ES', { month: 'long' });

/** Estado: anio null = la temporada por defecto de la API. */
const estado = { anio: null, categoria: '', texto: '', proximas: false };
let temporadas = [];       // [{ anio, actualizado, archivo, url }]
let anioMostrado = null;
let pruebas = [];          // pruebas de la temporada mostrada (sin filtros del navegador)
let convocatoriaPintada = false;
let docsPintados = false;
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

/**
 * URL de una imagen: las absolutas (API real) se validan con urlSegura; las relativas (mock,
 * "assets/img/fep/…") se usan tal cual, relativas a esta página.
 */
function urlImagen(ruta) {
  if (!ruta || typeof ruta !== 'string') return null;
  const r = ruta.trim();
  if (/^https?:\/\//i.test(r)) return urlSegura(r);
  if (/^[a-z][a-z0-9+.-]*:|^\/\//i.test(r)) return null;
  return r;
}

/** Clase de la etiqueta de categoría ("Absoluta", "Menores FFAA", "SILLA RUEDAS"…). */
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
  estado.categoria = limpio(q.get('categoria')).slice(0, 60);
  estado.texto = limpio(q.get('q')).slice(0, 100);
  estado.proximas = q.get('proximas') === '1';
}

function escribirUrl() {
  const q = new URLSearchParams();
  if (estado.anio) q.set('anio', estado.anio);
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

/* ---------------------------------------------------------------------------
   Formulario
   --------------------------------------------------------------------------- */
function pintarAnios() {
  const select = $('#calf-anio');
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
  $('#calf-categoria').replaceChildren(
    el('option', { value: '', text: 'Todas' }),
    ...nombres.map((n) => el('option', { value: n, selected: n === estado.categoria, text: textoCategoria(n) })));
}

function sincronizarFormulario() {
  $('#calf-categoria').value = estado.categoria;
  $('#calf-texto').value = estado.texto;
  $('#calf-proximas').checked = estado.proximas;
}

function initFormulario() {
  $('#calf-anio').addEventListener('change', (e) => { estado.anio = entero(e.target.value); buscar(); });
  // Filtros locales: no hace falta volver a pedir nada.
  $('#calf-categoria').addEventListener('change', (e) => { estado.categoria = e.target.value; escribirUrl(); pintar(); });
  let espera;
  $('#calf-texto').addEventListener('input', (e) => {
    clearTimeout(espera);
    espera = setTimeout(() => { estado.texto = limpio(e.target.value).slice(0, 100); escribirUrl(); pintar(); }, 200);
  });
  $('#calf-proximas').addEventListener('change', (e) => { estado.proximas = e.target.checked; escribirUrl(); pintar(); });
  $('#calf-form').addEventListener('submit', (e) => e.preventDefault());
  $('#calf-form').addEventListener('reset', () => {
    Object.assign(estado, { anio: null, categoria: '', texto: '', proximas: false });
    // El reset nativo restaura las opciones por defecto: se sincroniza después.
    setTimeout(() => { sincronizarFormulario(); buscar(); });
  });
}

/* ---------------------------------------------------------------------------
   Consulta
   --------------------------------------------------------------------------- */
async function buscar() {
  const id = ++peticion;
  escribirUrl();
  mostrarCargando();

  try {
    const datos = await get(`/api/v1/competicion/calendario${estado.anio ? `?anio=${estado.anio}` : ''}`);
    if (id !== peticion) return;
    if (!datos || typeof datos !== 'object') throw new Error('Respuesta vacía');

    const lista = (Array.isArray(datos.temporadas) ? datos.temporadas : [])
      .filter((t) => t && Number.isInteger(t.anio))
      .sort((a, b) => b.anio - a.anio);
    // Temporada pedida que no existe (enlace antiguo): se vuelve a la de por defecto.
    if (estado.anio && lista.length && !lista.some((t) => t.anio === estado.anio)) {
      estado.anio = null;
      return buscar();
    }
    temporadas = lista;
    anioMostrado = estado.anio ?? (Number.isInteger(datos.anio) ? datos.anio : lista[0]?.anio ?? null);
    pintarAnios();

    if (!convocatoriaPintada) pintarConvocatoria(datos.licitacion);
    if (!docsPintados) pintarDocumentos();

    pruebas = (Array.isArray(datos.items) ? datos.items : [])
      .filter((p) => p && limpio(p.titulo) && (p.anio ?? anioMostrado) === anioMostrado);
    pintarCategorias();
    pintar();
  } catch (e) {
    if (id !== peticion) return;
    console.warn('[FEP] calendario:', e.message);
    $('#calf-skeleton').hidden = true;
    $('#calf-lista').hidden = true;
    $('#calf-vacio').hidden = true;
    $('#calf-temporada').hidden = true;
    $('#calf-error').hidden = false;
    $('#calf-total').textContent = '–';
    $('#calf-estado').textContent = 'No se ha podido cargar el calendario.';
    $('#calf-anio').disabled = !temporadas.length;
    if (!docsPintados) {
      $('#calf-docs-total').textContent = '–';
      $('#calf-docs-vacio').hidden = false;
    }
  } finally {
    if (id === peticion) {
      $('#calf-contenido').setAttribute('aria-busy', 'false');
      $('#pruebas').setAttribute('aria-busy', 'false');
    }
  }
}

function mostrarCargando() {
  $('#pruebas').setAttribute('aria-busy', 'true');
  $('#calf-error').hidden = true;
  // Primera carga: skeleton. Después se deja la lista anterior atenuada hasta que llega la nueva.
  if ($('#calf-lista').hidden && $('#calf-vacio').hidden) $('#calf-skeleton').hidden = false;
  $('#calf-lista').classList.add('is-loading');
}

/* ---------------------------------------------------------------------------
   Convocatoria para organizar torneos (se pinta una vez)
   --------------------------------------------------------------------------- */
function pintarConvocatoria(l) {
  convocatoriaPintada = true;
  const legado = urlSegura(l?.enlaceUrl);
  if (!l || !limpio(l.titulo) || !legado) return;   // cerrada: la sección sigue oculta

  $('#convocatoria-titulo').textContent = limpio(l.titulo);
  const texto = limpio(l.texto);
  $('#calf-licit-texto').textContent = texto;
  $('#calf-licit-texto').hidden = !texto;

  const enlace = $('#calf-licit-enlace');
  enlace.href = paginaMigrada(legado) ?? legado;
  enlace.setAttribute('aria-label', `Ver la convocatoria: ${limpio(l.titulo)}`);

  // El banner repite el título (texto dentro de la imagen): decorativo, alt vacío.
  const src = urlImagen(l.imagenUrl);
  const figura = $('#calf-licit-figura');
  if (src) {
    const img = el('img', { src, alt: '', width: 881, height: 117, decoding: 'async' });
    img.addEventListener('error', () => { figura.hidden = true; }, { once: true });
    figura.replaceChildren(img);
    figura.hidden = false;
  }
  $('#convocatoria').hidden = false;
}

/* ---------------------------------------------------------------------------
   Documentos: PDF de la temporada (sobre la lista) y de todas las temporadas
   --------------------------------------------------------------------------- */
function itemDocumento(t, { conTemporada = true } = {}) {
  const url = urlSegura(t.url);
  const f = aFecha(t.actualizado);
  const titulo = `Calendario oficial ${t.anio}`;
  return el('li', { class: 'doc' },
    el('span', { class: 'doc__icon', 'aria-hidden': 'true', text: 'PDF' }),
    el('div', { class: 'doc__body' },
      el('a', { class: 'doc__title', href: url, target: '_blank', rel: 'noopener' }, titulo,
        el('span', { class: 'sr-only', text: ' (PDF, se abre en una pestaña nueva)' })),
      el('p', { class: 'doc__meta' },
        el('span', { class: 'org-tag', text: 'PDF' }),
        conTemporada ? el('span', { text: `Temporada ${t.anio}` }) : null,
        f ? el('span', {}, 'Actualizado el ', el('time', { datetime: String(t.actualizado).slice(0, 10), text: fmtLargo.format(f) })) : null)),
    el('a', { class: 'doc__download', href: url, target: '_blank', rel: 'noopener', 'aria-label': `Descargar en PDF: ${titulo}` },
      'Descargar ', el('span', { 'aria-hidden': 'true', text: '↓' })));
}

function pintarDocumentos() {
  docsPintados = true;
  const conPdf = temporadas.filter((t) => urlSegura(t.url));
  $('#calf-docs-total').textContent = fmtNum.format(conPdf.length);
  $('#calf-docs-vacio').hidden = conPdf.length > 0;
  const visibles = conPdf.slice(0, MAX_DOCS_VISIBLES);
  const resto = conPdf.slice(MAX_DOCS_VISIBLES);
  const lista = $('#calf-docs');
  lista.replaceChildren(...visibles.map((t) => itemDocumento(t)));
  lista.hidden = !conPdf.length;
  if (resto.length) {
    lista.after(el('details', { class: 'org-more' },
      el('summary', { text: `Ver ${plural(resto.length, 'calendario anterior', 'calendarios anteriores')}` }),
      el('ul', { class: 'doc-list' }, resto.map((t) => itemDocumento(t)))));
  }
}

function pintarTemporada() {
  const t = temporadas.find((x) => x.anio === anioMostrado);
  const cont = $('#calf-temporada');
  if (!t) { cont.hidden = true; return; }
  const f = aFecha(t.actualizado);
  if (urlSegura(t.url)) {
    cont.replaceChildren(el('ul', { class: 'doc-list' }, itemDocumento(t, { conTemporada: false })));
  } else {
    cont.replaceChildren(el('p', { class: 'calf-season__nodoc' },
      `No hay calendario en PDF publicado para la temporada ${t.anio}`,
      f ? [' (actualizado el ', el('time', { datetime: String(t.actualizado).slice(0, 10), text: fmtLargo.format(f) }), ')'] : null,
      '.'));
  }
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
  const items = filtradas();
  const criterios = descripcionCriterios();

  $('#calf-skeleton').hidden = true;
  $('#calf-titulo-lista').textContent = anioMostrado ? `Calendario ${anioMostrado}` : 'Calendario';
  $('#calf-total').textContent = fmtNum.format(items.length);
  $('#calf-estado').textContent = anioMostrado
    ? `${plural(items.length, 'prueba', 'pruebas')} en la temporada ${anioMostrado}${criterios ? ` · ${criterios}` : ''}.`
    : 'Todavía no hay ninguna temporada publicada.';
  pintarTemporada();

  const grupos = agrupar(items);
  const cont = $('#calf-lista');
  cont.classList.remove('is-loading');
  cont.replaceChildren(...grupos.map((g) => el('section', { class: 'camp-month', id: g.id, 'aria-labelledby': `${g.id}-titulo` },
    el('h3', { class: 'camp-month__title', id: `${g.id}-titulo` }, g.titulo, ' ', el('span', { class: 'org-count', text: fmtNum.format(g.items.length) })),
    el('ul', { class: 'camp-items' }, g.items.map((p) => el('li', {}, tarjeta(p)))))));
  cont.hidden = !items.length;
  $('#calf-vacio').hidden = items.length > 0;

  pintarIndice(grupos);

  const pendientes = pruebas.filter((p) => pendiente(p) && !p.fechaPorConfirmar).length;
  const resumen = [
    [fmtNum.format(pruebas.length), anioMostrado ? `pruebas en ${anioMostrado}` : 'pruebas'],
    pendientes && pendientes < pruebas.length ? [fmtNum.format(pendientes), 'pendientes de celebrar'] : null,
    temporadas.length ? [fmtNum.format(temporadas.length), 'temporadas'] : null,
  ].filter(Boolean);
  $('#calf-resumen').replaceChildren(...resumen.map(([n, t]) => el('li', {}, el('strong', { text: n }), ` ${t}`)));
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
      // "Febrero de 2026" (como Campeonatos); en el índice, solo el mes salvo que sea de otro año que la temporada.
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
   Índice lateral: convocatoria, pruebas por mes y PDF; marca la sección visible
   --------------------------------------------------------------------------- */
function pintarIndice(grupos) {
  const entrada = (id, texto, cuenta) => el('li', {},
    el('a', { href: `#${id}`, dataset: { seccion: id } }, el('span', { text: texto }),
      cuenta != null ? el('span', { class: 'page-toc__count', text: String(cuenta) }) : null));
  const conPdf = temporadas.filter((t) => urlSegura(t.url)).length;
  $('#calf-indice').replaceChildren(...[
    $('#convocatoria').hidden ? null : entrada('convocatoria', 'Convocatoria'),
    entrada('pruebas', anioMostrado ? `Pruebas ${anioMostrado}` : 'Pruebas', pruebas.length),
    ...grupos.map((g) => entrada(g.id, g.corto, g.items.length)),
    entrada('documentos', 'Calendarios en PDF', conPdf),
  ].filter(Boolean));
  observarIndice();
}

function observarIndice() {
  if (!('IntersectionObserver' in window)) return;
  observador?.disconnect();
  const enlaces = new Map($$('#calf-indice a').map((a) => [a.dataset.seccion, a]));
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
function init() {
  leerUrl();
  sincronizarFormulario();
  initFormulario();
  buscar();
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
})();
