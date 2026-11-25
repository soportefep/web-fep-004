/**
 * Página "Micrositio" (migrada desde Microsite.asp, el mini-CMS de micrositios: Homologaciones,
 * Elecciones 2024…). Requiere assets/js/comun.js (cabecera, menú, pie y utilidades en window.FEP).
 *
 *   URL: micrositio.html?id=7&opcion=19 (también acepta Id / IdOpcion, como Microsite.asp)
 *   Cabecera + menú  → GET /api/v1/micrositios/{id}                    (mock/micrositios-{id}.json)
 *   Contenido        → GET /api/v1/micrositios/{id}/opciones/{opcion}  (mock/micrositios-{id}-opciones-{opcion}.json)
 *
 * El índice lateral es el menú del micrositio; al elegir un apartado se carga sin recargar la página
 * (history.pushState; atrás/adelante funcionan). Los micrositios privados (asambleas) se enlazan a la
 * web actual.
 *
 * El cuerpo de cada apartado es HTML heredado del editor de la gestión (CKEditor), guardado tal cual en
 * la BD. NUNCA se pinta con innerHTML: se analiza con DOMParser (que no ejecuta scripts) y se
 * reconstruye nodo a nodo con una lista blanca de etiquetas y atributos (ver "Saneado").
 */
(function () {
'use strict';

const { $, el, get, urlSegura, attrsEnlace, aFecha, fmtLargo, reduceMotion, normaliza, MICROSITIOS_PRIVADOS } = window.FEP;
const { LEGACY_BASE } = window.FEPApi;

const ID = /^[1-9]\d{0,9}$/;
const TOC_VISIBLES = 12;        // apartados visibles en el índice; el resto, plegados
const DOCS_VISIBLES = 8;        // documentos visibles; el resto, plegados
const SITIO = 'Federación Española de Pádel';

const estado = { id: null, micrositio: null, opciones: new Map(), actual: null, peticion: 0 };

/* ---------------------------------------------------------------------------
   Parámetros y URLs
   --------------------------------------------------------------------------- */
/** ?id=7&opcion=19, ?Id=7&IdOpcion=19 o ?ID=7… (Request() de ASP no distingue mayúsculas). */
function leerParametros(search = location.search) {
  const q = new Map([...new URLSearchParams(search)].map(([k, v]) => [k.toLowerCase(), v.trim()]));
  const id = q.get('id') ?? '';
  const opcion = q.get('opcion') ?? q.get('idopcion') ?? '';
  return { id: ID.test(id) ? id : null, opcion: ID.test(opcion) ? opcion : null };
}

const urlPagina = (id, opcion) => `micrositio.html?id=${id}${opcion ? `&opcion=${opcion}` : ''}`;
const urlWebActual = (id, opcion) =>
  new URL(id ? `Microsite.asp?Id=${id}${opcion ? `&IdOpcion=${opcion}` : ''}` : '', LEGACY_BASE).href;

/* ===========================================================================
   Saneado del HTML heredado (lista blanca)
   =========================================================================== */
// Etiquetas que se conservan (sin atributos salvo los indicados en copiarElemento).
const PERMITIDAS = new Set([
  'p', 'div', 'h3', 'h4', 'ul', 'ol', 'li', 'a', 'strong', 'em', 'b', 'i', 'u', 'sub', 'sup', 'br', 'hr',
  'blockquote', 'table', 'caption', 'thead', 'tbody', 'tfoot', 'tr', 'th', 'td', 'img', 'iframe',
]);
// Equivalencias: los títulos del contenido cuelgan del h2 del apartado (h1/h2 → h3; h5/h6 → h4).
const RENOMBRAR = { h1: 'h3', h2: 'h3', h5: 'h4', h6: 'h4', cite: 'em', address: 'p', center: 'p', pre: 'p' };
// Se eliminan con todo su contenido. El resto de etiquetas desconocidas (span, font, html, body…) se
// "desenvuelven": desaparece la etiqueta pero se conserva su contenido.
const DESCARTAR = new Set([
  'script', 'style', 'head', 'title', 'meta', 'link', 'base', 'object', 'embed', 'applet', 'form', 'input',
  'button', 'select', 'option', 'textarea', 'svg', 'math', 'template', 'noscript', 'frame', 'frameset',
  'audio', 'video', 'source', 'canvas', 'map', 'area', 'dialog',
]);
// Marcas internas que el editor antiguo dejaba en <div style="display:none"> y que a veces quedan
// visibles por HTML mal cerrado (p. ej. Homologaciones › Pistas).
const MARCAS_EDITOR = /^\s*(?:fiogf49gjkf0d|fjrigjwwe9r0\S*|SFEP_Microsites_Posts:Post)\s*$/;
// Restos de la antigua web WordPress: enlaces que dan 404.
const ENLACE_WORDPRESS = /(^|\/)single\.php([?#]|$)/i;
const PROFUNDIDAD_MAXIMA = 60;

const oculto = (n) => n.hasAttribute('hidden')
  || /display\s*:\s*none|visibility\s*:\s*hidden/i.test(n.getAttribute('style') ?? '');

/** URL http(s) para imágenes e iframes (relativas → web actual; http → https para evitar contenido mixto). */
function urlRecurso(valor) {
  const url = urlSegura(valor);
  if (!url || !/^https?:/i.test(url)) return null;
  return url.replace(/^http:/i, 'https:');
}

/** Solo se incrustan vídeos de YouTube y calendarios de Google. */
function iframePermitido(valor) {
  const url = urlRecurso(valor);
  if (!url) return null;
  const u = new URL(url);
  if (/^(www\.)?youtube(-nocookie)?\.com$/i.test(u.hostname) && u.pathname.startsWith('/embed/')) {
    return { src: u.href, titulo: 'Vídeo de YouTube' };
  }
  if (u.hostname === 'calendar.google.com' && u.pathname.startsWith('/calendar/embed')) {
    return { src: u.href, titulo: 'Calendario de Google' };
  }
  return null;
}

/**
 * Convierte el HTML de la BD en un fragmento seguro. `informe.rotos` recoge los enlaces descartados
 * por apuntar a single.php (WordPress).
 */
function sanear(html, informe) {
  const doc = new DOMParser().parseFromString(String(html ?? ''), 'text/html');
  const frag = document.createDocumentFragment();
  copiarHijos(doc.body, frag, { profundidad: 0, enThead: false, informe });
  quitarVacios(frag);
  return frag;
}

function copiarHijos(origen, destino, ctx) {
  for (const hijo of origen.childNodes) {
    const copia = copiarNodo(hijo, ctx);
    if (copia) destino.append(copia);
  }
}

function desenvolver(n, ctx) {
  const f = document.createDocumentFragment();
  copiarHijos(n, f, ctx);
  return f;
}

function copiarNodo(n, ctx) {
  if (n.nodeType === Node.TEXT_NODE) {
    return MARCAS_EDITOR.test(n.data) ? null : document.createTextNode(n.data);
  }
  if (n.nodeType !== Node.ELEMENT_NODE || ctx.profundidad > PROFUNDIDAD_MAXIMA) return null;

  const original = n.localName;
  if (DESCARTAR.has(original) || oculto(n)) return null;
  const tag = RENOMBRAR[original] ?? original;
  const sub = { ...ctx, profundidad: ctx.profundidad + 1, enThead: ctx.enThead || tag === 'thead' };
  if (!PERMITIDAS.has(tag)) return desenvolver(n, sub);
  return copiarElemento(n, tag, sub);
}

function copiarElemento(n, tag, ctx) {
  switch (tag) {
    case 'a': return copiarEnlace(n, ctx);
    case 'img': return copiarImagen(n);
    case 'iframe': return copiarIframe(n);
    case 'table': {
      const tabla = el('table', { class: 'ms-table' });
      copiarHijos(n, tabla, ctx);
      // Región desplazable con teclado si la tabla no cabe (sin scroll horizontal de la página).
      return el('div', { class: 'ms-table-wrap', tabindex: '0', role: 'region', 'aria-label': 'Tabla' }, tabla);
    }
    case 'td':
    case 'th': {
      // Las cabeceras hechas con <td> dentro de <thead> pasan a <th scope="col">.
      const celda = ctx.enThead || tag === 'th' ? el('th', { scope: ctx.enThead ? 'col' : null }) : el('td');
      for (const a of ['colspan', 'rowspan']) {
        const v = n.getAttribute(a);
        if (v && /^[1-9]\d?$/.test(v.trim())) celda.setAttribute(a, v.trim());
      }
      copiarHijos(n, celda, ctx);
      return celda;
    }
    case 'ol': {
      const lista = el('ol');
      const tipo = n.getAttribute('type');
      if (tipo && /^[1aAiI]$/.test(tipo)) lista.setAttribute('type', tipo);
      copiarHijos(n, lista, ctx);
      return lista;
    }
    default: {
      const copia = document.createElement(tag);
      if (tag !== 'br' && tag !== 'hr') copiarHijos(n, copia, ctx);
      return copia;
    }
  }
}

function copiarEnlace(n, ctx) {
  const crudo = (n.getAttribute('href') ?? '').trim();
  // Sin destino, anclas internas del editor o restos de WordPress: se conserva solo el texto.
  if (!crudo || crudo.startsWith('#')) return desenvolver(n, ctx);
  if (ENLACE_WORDPRESS.test(crudo)) {
    ctx.informe.rotos.push(crudo);
    return desenvolver(n, ctx);
  }
  // Relativas → web actual; páginas ya migradas (incluido Microsite.asp) → página nueva.
  const { href, target } = attrsEnlace(crudo, n.getAttribute('target'));
  if (href === '#') return desenvolver(n, ctx);
  const a = el('a', { href });
  if (href.startsWith('micrositio.html?')) a.dataset.micrositio = '';
  else if (target === '_blank' || (/^https?:/i.test(href) && !/(^|\.)padelfederacion\.es$/i.test(new URL(href).hostname))) {
    a.target = '_blank';
    a.rel = 'noopener';
  }
  copiarHijos(n, a, ctx);
  if (!a.textContent.trim() && !a.querySelector('img')) return null;
  if (a.target === '_blank') a.append(el('span', { class: 'sr-only', text: ' (se abre en una pestaña nueva)' }));
  return a;
}

function copiarImagen(n) {
  const src = urlRecurso(n.getAttribute('src'));
  if (!src) return null;
  const imagen = el('img', { src, alt: (n.getAttribute('alt') ?? '').trim(), loading: 'lazy', decoding: 'async' });
  for (const a of ['width', 'height']) {
    const v = (n.getAttribute(a) ?? '').trim();
    if (/^\d{1,4}$/.test(v)) imagen.setAttribute(a, v);
  }
  imagen.addEventListener('error', () => imagen.classList.add('is-broken'), { once: true });
  return imagen;
}

function copiarIframe(n) {
  const permitido = iframePermitido(n.getAttribute('src'));
  if (!permitido) return null;
  return el('div', { class: 'ms-embed' }, el('iframe', {
    src: permitido.src,
    title: (n.getAttribute('title') ?? '').trim() || permitido.titulo,
    loading: 'lazy',
    referrerpolicy: 'strict-origin-when-cross-origin',
    allow: 'encrypted-media; picture-in-picture; fullscreen',
    sandbox: 'allow-scripts allow-same-origin allow-popups allow-presentation',
    allowfullscreen: true,
  }));
}

/** Quita párrafos, títulos y elementos de lista vacíos (el editor dejaba muchos "&nbsp;"). */
function quitarVacios(raiz) {
  const candidatos = [...raiz.querySelectorAll('p, div, h3, h4, li, ul, ol, blockquote, strong, em, b, i, u')].reverse();
  for (const n of candidatos) {
    if (n.querySelector('img, iframe, table, hr')) continue;
    if (!n.textContent.replace(/[\s\u00a0]+/g, '')) n.remove();
  }
  // Saltos de línea sueltos al principio y al final del contenido
  while (raiz.firstChild && (raiz.firstChild.nodeName === 'BR' || (raiz.firstChild.nodeType === Node.TEXT_NODE && !raiz.firstChild.data.trim()))) raiz.firstChild.remove();
  while (raiz.lastChild && (raiz.lastChild.nodeName === 'BR' || (raiz.lastChild.nodeType === Node.TEXT_NODE && !raiz.lastChild.data.trim()))) raiz.lastChild.remove();
}

/* ===========================================================================
   Documentos adjuntos
   =========================================================================== */
// [etiqueta del icono, descripción, ¿es descarga?]
const TIPOS = {
  pdf: ['PDF', 'Documento PDF', true],
  xls: ['XLS', 'Hoja de cálculo', true],
  doc: ['DOC', 'Documento de Word', true],
  ppt: ['PPT', 'Presentación', true],
  avi: ['VÍD', 'Vídeo', true],
  web: ['WEB', 'Página web', false],
  fac: ['FB', 'Facebook', false],
  pic: ['FOTO', 'Álbum de fotos (Picasa)', false],
  fli: ['FOTO', 'Álbum de fotos (Flickr)', false],
  you: ['YT', 'Vídeo de YouTube', false],
};
const EXTENSIONES = { pdf: 'pdf', xls: 'xls', xlsx: 'xls', doc: 'doc', docx: 'doc', ppt: 'ppt', pptx: 'ppt', avi: 'avi', mp4: 'avi' };

/** Tipo declarado en la gestión o, si falta, deducido de la extensión del fichero. */
function tipoArchivo(a) {
  if (a.tipo && TIPOS[a.tipo]) return a.tipo;
  const ext = /\.([a-z0-9]{2,4})$/i.exec(String(a.archivo ?? '').split(/[?#]/)[0])?.[1]?.toLowerCase();
  return EXTENSIONES[ext] ?? (/^https?:|^www\./i.test(a.archivo ?? '') ? 'web' : null);
}

function itemArchivo(a) {
  const url = urlSegura(a.url);
  const [icono, descripcion, descarga] = TIPOS[tipoArchivo(a)] ?? ['DOC', 'Archivo', true];
  const accion = descarga ? 'Descargar' : 'Abrir';
  return el('li', { class: 'doc' },
    el('span', { class: 'doc__icon', 'aria-hidden': 'true', text: icono }),
    el('div', { class: 'doc__body' },
      url ? el('a', { class: 'doc__title', href: url, target: '_blank', rel: 'noopener' }, a.titulo,
        el('span', { class: 'sr-only', text: ' (se abre en una pestaña nueva)' }))
        : el('span', { class: 'doc__title', text: a.titulo }),
      el('p', { class: 'doc__meta' }, el('span', { class: 'org-tag', text: descripcion }))),
    url ? el('a', {
      class: 'doc__download', href: url, target: '_blank', rel: 'noopener',
      'aria-label': `${accion} (${descripcion}): ${a.titulo}`,
    }, `${accion} `, el('span', { 'aria-hidden': 'true', text: descarga ? '↓' : '↗' })) : null);
}

function pintarArchivos(archivos) {
  const seccion = $('#ms-archivos');
  const validos = (archivos ?? []).filter((a) => a && a.titulo);
  seccion.hidden = !validos.length;
  if (!validos.length) return;
  $('#ms-archivos-total').textContent = String(validos.length);
  const resto = validos.slice(DOCS_VISIBLES);
  $('#ms-archivos-lista').replaceChildren(...validos.slice(0, DOCS_VISIBLES).map(itemArchivo));
  $('#ms-archivos-mas')?.remove();
  if (resto.length) {
    seccion.append(el('details', { class: 'org-more', id: 'ms-archivos-mas' },
      el('summary', { text: `Ver ${resto.length} ${resto.length === 1 ? 'documento más' : 'documentos más'}` }),
      el('ul', { class: 'doc-list' }, resto.map(itemArchivo))));
  }
}

/* ===========================================================================
   Cabecera, índice y estados
   =========================================================================== */
function pintarCabecera(m) {
  $('#titulo-pagina').textContent = `${m.titulo.replace(/[.\s]+$/, '')}.`;
  $('#ms-miga').textContent = m.titulo;
  $('#ms-entradilla').textContent = `Apartados y documentos del micrositio «${m.titulo}» de la ${SITIO}.`;
  const actualizado = aFecha(m.actualizado);
  const n = m.opciones.length;
  $('#ms-resumen').replaceChildren(...[
    el('li', {}, el('strong', { text: String(n) }), ` ${n === 1 ? 'apartado' : 'apartados'}`),
    actualizado ? el('li', {}, 'Actualizado el ', el('time', { datetime: String(m.actualizado).slice(0, 10), text: fmtLargo.format(actualizado) })) : null,
  ].filter(Boolean));
}

function enlaceIndice(o) {
  return el('li', {}, el('a', { href: urlPagina(estado.id, o.id), dataset: { micrositio: '', opcion: String(o.id) } },
    el('span', { text: o.titulo })));
}

function pintarIndice(opciones) {
  $('#ms-indice').replaceChildren(...opciones.slice(0, TOC_VISIBLES).map(enlaceIndice));
  const resto = opciones.slice(TOC_VISIBLES);
  if (resto.length) {
    $('#ms-indice').after(el('details', { class: 'org-more ms-toc__more', id: 'ms-indice-mas' },
      el('summary', { text: `Ver ${resto.length} apartados más` }),
      el('ul', { class: 'page-toc__list' }, resto.map(enlaceIndice))));
  }
}

function marcarIndice(opcion) {
  let activo = null;
  for (const a of document.querySelectorAll('.ms-toc a[data-opcion]')) {
    const es = a.dataset.opcion === opcion;
    a.classList.toggle('is-active', es);
    if (es) { a.setAttribute('aria-current', 'page'); activo = a; } else a.removeAttribute('aria-current');
  }
  if (!activo) return;
  const mas = activo.closest('details');
  if (mas) mas.open = true;
  // Móvil: el índice es una fila de chips con scroll horizontal; se centra el activo.
  const lista = activo.closest('.page-toc__list');
  if (lista && lista.scrollWidth > lista.clientWidth) {
    lista.scrollLeft = activo.offsetLeft - (lista.clientWidth - activo.offsetWidth) / 2;
  }
}

function cargando(si) {
  $('#ms-contenido').setAttribute('aria-busy', String(si));
  $('#ms-skeleton').hidden = !si;
  if (si) { $('#ms-articulo').hidden = true; $('#ms-error').hidden = true; }
}

/** Mensaje de error con enlace a la página equivalente de la web actual. */
function mostrarError(texto, url) {
  cargando(false);
  $('#ms-articulo').hidden = true;
  const p = $('#ms-error');
  p.replaceChildren(`${texto} Puedes consultarlo en la `, el('a', { href: url }, 'web actual de la FEP'), '.');
  p.hidden = false;
}

/* ===========================================================================
   Apartado (opción)
   =========================================================================== */
function pintarOpcion(d) {
  const m = estado.micrositio;
  const titulo = (d.subtitulo || d.titulo || '').trim();
  $('#ms-titulo').textContent = titulo;
  const kicker = $('#ms-apartado');
  kicker.textContent = d.titulo && normaliza(d.titulo) !== normaliza(titulo) ? d.titulo : '';
  kicker.hidden = !kicker.textContent;

  const informe = { rotos: [] };
  const cuerpo = sanear(d.html, informe);
  const contenedor = $('#ms-cuerpo');
  contenedor.replaceChildren(cuerpo);
  contenedor.hidden = !contenedor.textContent.replace(/[\s\u00a0]+/g, '') && !contenedor.querySelector('img, iframe, table');
  if (informe.rotos.length) console.info('[FEP] micrositio: enlaces a single.php (WordPress) descartados:', informe.rotos);

  // Formulario de inscripción (tipo "formulario"): sigue en la web actual.
  const aviso = $('#ms-formulario');
  aviso.hidden = d.tipo !== 'formulario';
  $('#ms-formulario-enlace').href = urlWebActual(estado.id, d.id);

  pintarArchivos(d.archivos);
  if ($('#ms-cuerpo').hidden && $('#ms-archivos').hidden && aviso.hidden) {
    $('#ms-cuerpo').replaceChildren(el('p', { class: 'empty', text: 'Este apartado no tiene contenido publicado.' }));
    $('#ms-cuerpo').hidden = false;
  }

  $('#ms-pie').replaceChildren(`Contenido publicado por la FEP en el micrositio «${m.titulo}». `,
    el('a', { href: urlWebActual(estado.id, d.id), target: '_blank', rel: 'noopener' },
      'Ver este apartado en la web actual', el('span', { 'aria-hidden': 'true', text: ' ↗' }),
      el('span', { class: 'sr-only', text: ' (se abre en una pestaña nueva)' })));

  document.title = `${titulo} · ${m.titulo} · ${SITIO}`;
  cargando(false);
  $('#ms-articulo').hidden = false;
}

/** Muestra un apartado. `historial`: 'push' (clic), 'replace' (entrada) o null (atrás/adelante). */
async function irA(opcion, { historial = null, foco = false } = {}) {
  const id = estado.id;
  if (!estado.opciones.has(opcion)) {
    marcarIndice(null);
    mostrarError('Este apartado no existe o ya no está publicado.', urlWebActual(id, opcion));
    return;
  }
  estado.actual = opcion;
  marcarIndice(opcion);
  const url = `${urlPagina(id, opcion)}${location.hash}`;
  if (historial === 'push') history.pushState({ id, opcion }, '', url);
  else if (historial === 'replace') history.replaceState({ id, opcion }, '', url);

  const n = ++estado.peticion;
  cargando(true);
  try {
    const d = await get(`/api/v1/micrositios/${id}/opciones/${opcion}`);
    if (n !== estado.peticion) return;                 // ya se ha pedido otro apartado
    if (!d || String(d.id) !== opcion) throw new Error('Respuesta de otro apartado');
    pintarOpcion(d);
    if (foco) {
      const titulo = $('#ms-titulo');
      titulo.focus({ preventScroll: true });
      if (titulo.getBoundingClientRect().top < 0) titulo.scrollIntoView({ behavior: reduceMotion.matches ? 'auto' : 'smooth' });
    }
  } catch (e) {
    if (n !== estado.peticion) return;
    console.warn('[FEP] micrositio (apartado):', e.message);
    mostrarError('No se ha podido cargar este apartado.', urlWebActual(id, opcion));
  }
}

/* ---------------------------------------------------------------------------
   Navegación dentro del micrositio sin recargar
   --------------------------------------------------------------------------- */
document.addEventListener('click', (e) => {
  const a = e.target.closest('a[data-micrositio]');
  if (!a || !estado.micrositio || e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
  const destino = leerParametros(new URL(a.href, location.href).search);
  if (destino.id !== estado.id) return;               // otro micrositio: navegación normal
  e.preventDefault();
  const opcion = destino.opcion ?? String(estado.micrositio.opcionInicial ?? '');
  if (opcion === estado.actual) return;
  irA(opcion, { historial: 'push', foco: true });
});

window.addEventListener('popstate', () => {
  const p = leerParametros();
  if (p.id !== estado.id) { location.reload(); return; }
  irA(p.opcion ?? String(estado.micrositio?.opcionInicial ?? ''));
});

/* ---------------------------------------------------------------------------
   Arranque
   --------------------------------------------------------------------------- */
async function init() {
  const p = leerParametros();
  estado.id = p.id;
  if (!p.id) {
    mostrarError('No se ha indicado qué micrositio mostrar.', urlWebActual(null));
    return;
  }
  if (MICROSITIOS_PRIVADOS.has(p.id)) {
    $('#ms-indice').closest('.page-toc').hidden = true;
    mostrarError('Este micrositio es de acceso restringido (con identificación).', urlWebActual(p.id, p.opcion));
    return;
  }

  try {
    const m = await get(`/api/v1/micrositios/${p.id}`);
    if (!m || !m.titulo || String(m.id) !== p.id) throw new Error('Respuesta sin micrositio');
    const opciones = (m.opciones ?? []).filter((o) => o && o.id && o.titulo)
      .sort((a, b) => (a.orden ?? 0) - (b.orden ?? 0));
    m.opciones = opciones;
    estado.micrositio = m;
    estado.opciones = new Map(opciones.map((o) => [String(o.id), o]));
  } catch (e) {
    console.warn('[FEP] micrositio:', e.message);
    mostrarError('No se ha podido cargar este micrositio o no está publicado.', urlWebActual(p.id, p.opcion));
    return;
  }

  const m = estado.micrositio;
  pintarCabecera(m);
  if (!m.opciones.length) {
    mostrarError('Este micrositio no tiene apartados publicados.', urlWebActual(p.id));
    return;
  }
  pintarIndice(m.opciones);
  irA(p.opcion ?? String(m.opcionInicial ?? m.opciones[0].id), { historial: 'replace' });
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
})();
