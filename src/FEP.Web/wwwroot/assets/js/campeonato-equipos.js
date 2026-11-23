/**
 * Página "Campeonato por equipos" (migrada desde Camp_Equipos.asp?IdTorneo=).
 * Requiere assets/js/comun.js (cabecera, menú, pie y utilidades en window.FEP).
 *
 *   URL: campeonato-equipos.html?id=28676 (también acepta IdTorneo / idtorneo, como la web actual)
 *   Ficha → GET /api/v1/competicion/campeonatos-equipos/{id}
 *           (respaldo: mock/competicion-campeonatos-equipos-{id}.json)
 *
 * Bloques en el orden del ASP: nota de prensa, en directo (marcadores y vídeos), cuadros, notificaciones,
 * inscripción de equipos (o equipos inscritos si el plazo ha terminado), parejas inscritas, información
 * general, información adicional, cartel y noticias relacionadas. Solo se muestran los que tienen contenido
 * y el índice lateral se construye con ellos. Los cuadros, los equipos inscritos y los pasos de la
 * inscripción siguen en la web actual.
 *
 * Los textos de notificaciones, nota de prensa e información son HTML heredado del editor de la gestión.
 * NUNCA se pinta con innerHTML: se analiza con DOMParser (que no ejecuta scripts) y se reconstruye nodo a
 * nodo con una lista blanca (ver "Saneado", copia reducida del de micrositio.js). El resto de textos de la
 * API van por textContent.
 */
(function () {
'use strict';

const { $, $$, el, get, urlSegura, attrsEnlace, img, textoPlano, aFecha, bloqueFecha, fmtLargo } = window.FEP;
const { LEGACY_BASE } = window.FEPApi;

const ID = /^[1-9]\d{0,9}$/;
const SITIO = 'Federación Española de Pádel';
const DOCS_VISIBLES = 8;          // adjuntos visibles; el resto, plegados

const GENEROS = { masculino: 'Masculino', femenino: 'Femenino', mixto: 'Mixto' };
const GENEROS_CATEGORIA = { masculino: 'Masculina', femenino: 'Femenina', mixto: 'Mixta' };
const CATEGORIAS = { absoluto: 'Absoluto', menores: 'Menores', veteranos: 'Veteranos' };
const TIPOS_CUADRO = { final: 'Cuadro', consolacion: 'Consolación', liguilla: 'Liguilla', grupo: 'Grupo', previa: 'Previa' };

const fmtNum = new Intl.NumberFormat('es-ES');
const fmtEuros = new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR', minimumFractionDigits: 0, maximumFractionDigits: 2 });
const fmtDia = new Intl.DateTimeFormat('es-ES', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });
const fmtPlazo = new Intl.DateTimeFormat('es-ES', {
  weekday: 'long', day: 'numeric', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit',
});

const limpio = (t) => String(t ?? '').replace(/\s+/g, ' ').trim();
const lista = (v) => (Array.isArray(v) ? v : []);
const plural = (n, uno, varios) => `${fmtNum.format(n)} ${n === 1 ? uno : varios}`;
const nuevaPestana = () => el('span', { class: 'sr-only', text: ' (se abre en una pestaña nueva)' });

/* ---------------------------------------------------------------------------
   Parámetros y URLs
   --------------------------------------------------------------------------- */
/** ?id=28676, ?IdTorneo=28676 o ?idtorneo=28676 (Request() de ASP no distingue mayúsculas). */
function leerId() {
  const q = new Map([...new URLSearchParams(location.search)].map(([k, v]) => [k.toLowerCase(), v.trim()]));
  const id = q.get('id') ?? q.get('idtorneo') ?? '';
  return ID.test(id) ? id : null;
}

const urlWebActual = (id) => new URL(id ? `Camp_Equipos.asp?IdTorneo=${id}` : 'Campeonatos', LEGACY_BASE).href;

/** URL http(s) válida (relativas → web actual). */
function urlHttp(valor) {
  const url = urlSegura(valor);
  return url && /^https?:/i.test(url) ? url : null;
}

/**
 * URL de una imagen: las absolutas (API real) se validan; las relativas del mock ("assets/img/fep/…") se usan
 * tal cual, relativas a esta página.
 */
function urlImagen(ruta) {
  if (!ruta || typeof ruta !== 'string') return null;
  const r = ruta.trim();
  if (/^https?:\/\//i.test(r)) return urlSegura(r);
  if (/^[a-z][a-z0-9+.-]*:|^\/\//i.test(r)) return null;
  return r;
}

/* ===========================================================================
   Saneado del HTML heredado (lista blanca)
   =========================================================================== */
const PERMITIDAS = new Set([
  'p', 'div', 'h3', 'h4', 'ul', 'ol', 'li', 'a', 'strong', 'em', 'b', 'i', 'u', 'sub', 'sup', 'br', 'hr',
  'blockquote', 'table', 'caption', 'thead', 'tbody', 'tfoot', 'tr', 'th', 'td', 'img', 'iframe',
]);
// Los títulos del contenido cuelgan del h2 de la sección (h1/h2 → h3; h5/h6 → h4).
const RENOMBRAR = { h1: 'h3', h2: 'h3', h5: 'h4', h6: 'h4', cite: 'em', address: 'p', center: 'p', pre: 'p' };
// Se eliminan con todo su contenido; el resto de etiquetas desconocidas (span, font…) se "desenvuelven".
const DESCARTAR = new Set([
  'script', 'style', 'head', 'title', 'meta', 'link', 'base', 'object', 'embed', 'applet', 'form', 'input',
  'button', 'select', 'option', 'textarea', 'svg', 'math', 'template', 'noscript', 'frame', 'frameset',
  'audio', 'video', 'source', 'canvas', 'map', 'area', 'dialog', 'colgroup', 'col',
]);
// Marcas internas del editor antiguo que a veces quedan visibles por HTML mal cerrado.
const MARCAS_EDITOR = /^\s*(?:fiogf49gjkf0d|fjrigjwwe9r0\S*)\s*$/;
const PROFUNDIDAD_MAXIMA = 60;

const oculto = (n) => n.hasAttribute('hidden')
  || /display\s*:\s*none|visibility\s*:\s*hidden/i.test(n.getAttribute('style') ?? '');

/** URL http(s) para imágenes e iframes (http → https para evitar contenido mixto). */
function urlRecurso(valor) {
  const url = urlHttp(valor);
  return url ? url.replace(/^http:/i, 'https:') : null;
}

/** Solo se incrustan vídeos de YouTube (/embed/). */
function youtube(valor) {
  const url = urlRecurso(valor);
  if (!url) return null;
  const u = new URL(url);
  return /^(www\.)?youtube(-nocookie)?\.com$/i.test(u.hostname) && u.pathname.startsWith('/embed/') ? u.href : null;
}

/** HTML de la BD → fragmento seguro. */
function sanear(html) {
  const doc = new DOMParser().parseFromString(String(html ?? ''), 'text/html');
  const frag = document.createDocumentFragment();
  copiarHijos(doc.body, frag, { profundidad: 0, enThead: false });
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
  if (n.nodeType === Node.TEXT_NODE) return MARCAS_EDITOR.test(n.data) ? null : document.createTextNode(n.data);
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
    case 'iframe': {
      const src = youtube(n.getAttribute('src'));
      return src ? videoEmbebido(src, (n.getAttribute('title') ?? '').trim() || 'Vídeo de YouTube') : null;
    }
    case 'table': {
      const tabla = el('table', { class: 'ms-table' });
      copiarHijos(n, tabla, ctx);
      // Región desplazable con teclado si la tabla no cabe (sin scroll horizontal de la página).
      return el('div', { class: 'ms-table-wrap', tabindex: '0', role: 'region', 'aria-label': 'Tabla' }, tabla);
    }
    case 'td':
    case 'th': {
      const celda = ctx.enThead || tag === 'th' ? el('th', { scope: ctx.enThead ? 'col' : null }) : el('td');
      for (const a of ['colspan', 'rowspan']) {
        const v = n.getAttribute(a);
        if (v && /^[1-9]\d?$/.test(v.trim())) celda.setAttribute(a, v.trim());
      }
      copiarHijos(n, celda, ctx);
      return celda;
    }
    case 'ol': {
      const ol = el('ol');
      const tipo = n.getAttribute('type');
      if (tipo && /^[1aAiI]$/.test(tipo)) ol.setAttribute('type', tipo);
      copiarHijos(n, ol, ctx);
      return ol;
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
  if (!crudo || crudo.startsWith('#')) return desenvolver(n, ctx);
  // Relativas → web actual; páginas ya migradas → página nueva.
  const { href, target } = attrsEnlace(crudo, n.getAttribute('target'));
  if (href === '#') return desenvolver(n, ctx);
  const a = el('a', { href });
  if (target === '_blank' || (/^https?:/i.test(href) && !/(^|\.)padelfederacion\.es$/i.test(new URL(href).hostname))) {
    a.target = '_blank';
    a.rel = 'noopener';
  }
  copiarHijos(n, a, ctx);
  if (!a.textContent.trim() && !a.querySelector('img')) return null;
  if (a.target === '_blank') a.append(nuevaPestana());
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

/** Quita párrafos, títulos y elementos de lista vacíos (el editor dejaba muchos "&nbsp;"). */
function quitarVacios(raiz) {
  const candidatos = [...raiz.querySelectorAll('p, div, h3, h4, li, ul, ol, blockquote, strong, em, b, i, u')].reverse();
  for (const n of candidatos) {
    if (n.querySelector('img, iframe, table, hr')) continue;
    if (!n.textContent.replace(/[\s\u00a0]+/g, '')) n.remove();
  }
  const vacio = (x) => x && (x.nodeName === 'BR' || (x.nodeType === Node.TEXT_NODE && !x.data.trim()));
  while (vacio(raiz.firstChild)) raiz.firstChild.remove();
  while (vacio(raiz.lastChild)) raiz.lastChild.remove();
}

/** Pinta HTML heredado en `contenedor`; devuelve false si no queda nada visible. */
function pintarHtml(contenedor, html) {
  contenedor.replaceChildren(sanear(html));
  const hay = Boolean(contenedor.textContent.replace(/[\s\u00a0]+/g, '')) || Boolean(contenedor.querySelector('img, iframe, table'));
  contenedor.hidden = !hay;
  return hay;
}

/* ---------------------------------------------------------------------------
   Piezas comunes
   --------------------------------------------------------------------------- */
function videoEmbebido(src, titulo) {
  return el('div', { class: 'ms-embed' }, el('iframe', {
    src,
    title: titulo,
    loading: 'lazy',
    referrerpolicy: 'strict-origin-when-cross-origin',
    allow: 'encrypted-media; picture-in-picture; fullscreen',
    sandbox: 'allow-scripts allow-same-origin allow-popups allow-presentation',
    allowfullscreen: true,
  }));
}

function mostrar(id, si = true) {
  const s = document.getElementById(id);
  if (s) s.hidden = !si;
}

/** Plazo de inscripción abierto según el reloj del navegador (la API y el mock pueden ir con retraso). */
function inscripcionAbierta(i) {
  const fecha = aFecha(i?.fechaLimite);
  return fecha && fecha > new Date() ? fecha : null;
}

/* ===========================================================================
   Cabecera
   =========================================================================== */
const DIAS = ['domingo', 'lunes', 'martes', 'miércoles', 'jueves', 'viernes', 'sábado'];
const fmtMes = new Intl.DateTimeFormat('es-ES', { month: 'long' });

/** "Del viernes 13 al domingo 15 de noviembre de 2026" (o "Viernes, 13 de noviembre de 2026" si es un solo día). */
function rangoFechas(ini, fin) {
  if (!ini) return null;
  if (!fin || fin.toDateString() === ini.toDateString()) return fmtDia.format(ini).replace(/^./, (c) => c.toUpperCase());
  const dia = (f) => `${DIAS[f.getDay()]} ${f.getDate()}`;
  const mes = (f) => `${fmtMes.format(f)} de ${f.getFullYear()}`;
  if (ini.getFullYear() !== fin.getFullYear()) return `Del ${dia(ini)} de ${mes(ini)} al ${dia(fin)} de ${mes(fin)}`;
  if (ini.getMonth() !== fin.getMonth()) return `Del ${dia(ini)} de ${fmtMes.format(ini)} al ${dia(fin)} de ${mes(fin)}`;
  return `Del ${dia(ini)} al ${dia(fin)} de ${mes(fin)}`;
}
function pintarCabecera(d) {
  const titulo = limpio(d.titulo);
  $('#titulo-pagina').textContent = `${titulo.replace(/[.\s]+$/, '')}.`;
  $('#cpe-miga').textContent = titulo;
  document.title = `${titulo} · ${SITIO}`;

  const categoria = CATEGORIAS[d.categoria];
  $('#cpe-eyebrow').textContent = [d.seleccion ? 'Selecciones autonómicas' : 'Campeonato por equipos', categoria].filter(Boolean).join(' · ');

  const sedes = lista(d.sedes).map((s) => limpio(s?.nombre)).filter(Boolean);
  const ini = aFecha(d.fechaInicio);
  const fin = aFecha(d.fechaFin);
  const cuando = rangoFechas(ini, fin);
  const donde = [limpio(d.ciudad), sedes.join(' y ')].filter(Boolean).join(', ');
  $('#cpe-entradilla').textContent = [cuando, donde].filter(Boolean).join(' · ')
    || 'Cuadros, notificaciones, equipos inscritos e información del campeonato.';

  const etiquetas = [
    { texto: 'Equipos', clase: 'org-tag' },
    categoria ? { texto: categoria, clase: `org-tag cpe-tag--${d.categoria}` } : null,
    d.seleccion ? { texto: 'Selecciones autonómicas', clase: 'org-tag' } : null,
    d.sillaRuedas ? { texto: 'Silla de ruedas', clase: 'org-tag' } : null,
  ].filter(Boolean);
  $('#cpe-etiquetas').replaceChildren(...etiquetas.map((t) => el('li', {}, el('span', { class: t.clase, text: t.texto }))));
  $('#cpe-etiquetas').hidden = false;

  const fecha = bloqueFecha(d.fechaInicio, d.fechaFin);
  const cuotas = lista(d.cuotas).filter((c) => typeof c === 'number');
  const cuadros = lista(d.cuadros).reduce((n, g) => n + lista(g?.cuadros).length, 0);
  $('#cpe-resumen').replaceChildren(...[
    fecha ? el('li', {}, el('strong', { text: fecha.dias }), ` ${fecha.mes}`) : null,
    cuotas.length ? el('li', {}, el('strong', { text: cuotas.map((c) => fmtEuros.format(c)).join(' / ') }), ' cuota por equipo') : null,
    cuadros ? el('li', {}, el('strong', { text: fmtNum.format(cuadros) }), ` ${cuadros === 1 ? 'cuadro' : 'cuadros'}`) : null,
  ].filter(Boolean));
  $('#cpe-hero-acciones').hidden = false;
}

/* ===========================================================================
   Secciones
   =========================================================================== */
function pintarNota(d) {
  mostrar('nota-prensa', d.notaPrensaHtml ? pintarHtml($('#cpe-nota'), d.notaPrensaHtml) : false);
}

/** En directo: marcadores (iframes de la web actual) y retransmisiones de YouTube. */
function pintarDirecto(d) {
  const marcadores = lista(d.marcadores)
    .map((m) => ({ ...m, url: urlHttp(m?.url) }))
    .filter((m) => m.url && /(^|\.)padelfederacion\.es$/i.test(new URL(m.url).hostname));
  const videos = lista(d.videos).map((v) => ({ titulo: limpio(v?.titulo), src: youtube(v?.url) })).filter((v) => v.titulo && v.src);

  // El ASP mostraba siempre "Marcadores on-line" (también en campeonatos de años pasados). Ahora la sección
  // sale si hay marcadores o vídeos, o mientras el campeonato no ha terminado.
  const fin = aFecha(d.fechaFin ?? d.fechaInicio);
  const vigente = fin ? new Date(fin.getFullYear(), fin.getMonth(), fin.getDate() + 1) > new Date() : false;
  if (!marcadores.length && !videos.length && !vigente) return;

  $('#cpe-marcadores-total').textContent = fmtNum.format(marcadores.length);
  $('#cpe-marcadores').replaceChildren(...marcadores.map((m, i) => el('iframe', {
    src: m.url,
    title: `Marcador en directo del partido ${i + 1}`,
    loading: 'lazy',
    referrerpolicy: 'strict-origin-when-cross-origin',
    sandbox: 'allow-scripts allow-same-origin',
  })));
  $('#cpe-marcadores').hidden = !marcadores.length;
  $('#cpe-marcadores-vacio').hidden = marcadores.length > 0;

  const cont = $('#cpe-videos');
  cont.replaceChildren(...videos.map((v) => el('figure', { class: 'cpe-video' },
    el('figcaption', { class: 'cpe-video__title', text: v.titulo }),
    videoEmbebido(v.src, `Retransmisión: ${v.titulo}`))));
  if (videos.length) cont.prepend(el('h3', { class: 'cpe-subtitle', text: 'Retransmisiones' }));
  cont.hidden = !videos.length;
  mostrar('directo');
}

function pintarCuadros(d) {
  if (!d.cuadrosPublicados) return;
  const grupos = lista(d.cuadros).filter((g) => GENEROS[g?.genero] && lista(g.cuadros).length);
  // La categoría solo se indica si no es la absoluta o si un género tiene varias.
  const porGenero = grupos.reduce((m, g) => m.set(g.genero, (m.get(g.genero) ?? 0) + 1), new Map());
  let total = 0;

  $('#cpe-cuadros').replaceChildren(...grupos.map((g) => {
    const categoria = limpio(g.categoria);
    const conCategoria = categoria && (categoria.toUpperCase() !== 'ABSOLUTO' || porGenero.get(g.genero) > 1);
    // PrevioEquipos.asp → cuadro.html (cuadros migrados); si el enlace no se puede migrar, la web actual.
    const enlaces = lista(g.cuadros).map((c) => {
      const href = urlHttp(c?.url);
      return { ...c, href: href ? (window.FEP.paginaMigrada(href) ?? href) : null, titulo: limpio(c?.titulo) };
    }).filter((c) => c.href && c.titulo);
    total += enlaces.length;
    const idTitulo = `cuadros-${g.genero}-${categoria.toLowerCase().replace(/[^a-z0-9]+/g, '-')}`;
    return el('section', { class: 'cpe-draws-group', 'aria-labelledby': idTitulo },
      el('h3', { class: 'cpe-draws-group__title', id: idTitulo },
        GENEROS[g.genero], conCategoria ? el('span', { class: 'org-tag', text: categoria }) : null,
        el('span', { class: 'org-count', text: fmtNum.format(enlaces.length) })),
      el('ul', { class: 'cpe-draws' }, enlaces.map((c) => el('li', {},
        el('a', { class: 'cpe-draw', href: c.href },
          el('span', { class: 'cpe-draw__body' },
            el('span', { class: 'cpe-draw__type', text: TIPOS_CUADRO[c.tipo] ?? 'Cuadro' }),
            el('span', { class: 'cpe-draw__title', text: c.titulo })),
          el('span', { class: 'cpe-draw__arrow', 'aria-hidden': 'true', text: '→' }))))));
  }));
  $('#cpe-cuadros-total').textContent = fmtNum.format(total);
  $('#cpe-cuadros-vacio').hidden = total > 0;
  mostrar('cuadros');
}

/* ---------------------------------------------------------------------------
   Notificaciones y adjuntos
   --------------------------------------------------------------------------- */
// [etiqueta del icono, descripción, ¿es descarga?]
const TIPOS_DOC = {
  pdf: ['PDF', 'Documento PDF', true],
  xls: ['XLS', 'Hoja de cálculo', true],
  doc: ['DOC', 'Documento de Word', true],
  ppt: ['PPT', 'Presentación', true],
  pic: ['IMG', 'Imagen', false],
  web: ['WEB', 'Enlace', false],
  you: ['YT', 'Vídeo de YouTube', false],
};
const EXTENSIONES = {
  pdf: 'pdf', xls: 'xls', xlsx: 'xls', csv: 'xls', doc: 'doc', docx: 'doc', ppt: 'ppt', pptx: 'ppt',
  jpg: 'pic', jpeg: 'pic', png: 'pic', gif: 'pic', webp: 'pic',
};

/** Tipo del fichero por su extensión (la gestión a veces declara "web" para una imagen) o, si no, el declarado. */
function tipoAdjunto(a) {
  const ext = /\.([a-z0-9]{2,4})$/i.exec(String(a.archivo ?? '').split(/[?#]/)[0])?.[1]?.toLowerCase();
  return EXTENSIONES[ext] ?? (TIPOS_DOC[a.tipo] ? a.tipo : null);
}

function itemAdjunto(a) {
  const url = urlHttp(a.url);
  const titulo = limpio(a.titulo);
  const [icono, descripcion, descarga] = TIPOS_DOC[tipoAdjunto(a)] ?? ['DOC', 'Archivo', true];
  const accion = descarga ? 'Descargar' : 'Abrir';
  return el('li', { class: 'doc' },
    el('span', { class: 'doc__icon', 'aria-hidden': 'true', text: icono }),
    el('div', { class: 'doc__body' },
      url ? el('a', { class: 'doc__title', href: url, target: '_blank', rel: 'noopener' }, titulo, nuevaPestana())
        : el('span', { class: 'doc__title', text: titulo }),
      el('p', { class: 'doc__meta' }, el('span', { class: 'org-tag', text: descripcion }))),
    url ? el('a', {
      class: 'doc__download', href: url, target: '_blank', rel: 'noopener',
      'aria-label': `${accion} (${descripcion}): ${titulo}`,
    }, `${accion} `, el('span', { 'aria-hidden': 'true', text: descarga ? '↓' : '↗' })) : null);
}

function pintarNotificaciones(d) {
  const hayTexto = d.notificacionesHtml ? pintarHtml($('#cpe-notificaciones'), d.notificacionesHtml) : false;
  const adjuntos = lista(d.adjuntos).filter((a) => a && limpio(a.titulo));
  const ul = $('#cpe-adjuntos');
  ul.replaceChildren(...adjuntos.slice(0, DOCS_VISIBLES).map(itemAdjunto));
  ul.hidden = !adjuntos.length;
  const resto = adjuntos.slice(DOCS_VISIBLES);
  if (resto.length) {
    ul.after(el('details', { class: 'org-more' },
      el('summary', { text: `Ver ${plural(resto.length, 'documento más', 'documentos más')}` }),
      el('ul', { class: 'doc-list' }, resto.map(itemAdjunto))));
  }
  mostrar('notificaciones', hayTexto || adjuntos.length > 0);
}

/* ---------------------------------------------------------------------------
   Inscripción de equipos
   --------------------------------------------------------------------------- */
function enlaceWebActual(href, texto) {
  return el('a', { class: 'cpe-step__link', href }, texto, el('span', { class: 'cpe-step__arrow', 'aria-hidden': 'true', text: ' →' }));
}

function pintarInscripcion(d) {
  const i = d.inscripcion ?? {};
  const cont = $('#cpe-inscripcion');
  const limite = inscripcionAbierta(i);
  const quien = i.por === 'clubes' ? 'del club' : 'de la federación';

  if (limite) {
    $('#inscripcion-titulo').textContent = 'Inscripción de equipos';
    const info = el('div', { class: 'ms-rich' });
    const hayInfo = i.informacionHtml ? pintarHtml(info, i.informacionHtml) : false;
    info.hidden = false;
    const acciones = lista(i.acciones)
      .filter((a) => a && limpio(a.texto) && urlHttp(a.url))
      .filter((a) => !a.hasta || (aFecha(a.hasta) ?? 0) > new Date());

    cont.replaceChildren(...[
      el('p', { class: 'camp-item__plazo cpe-plazo' },
        el('strong', { text: 'Inscripción abierta' }), ' hasta el ',
        el('time', { datetime: String(i.fechaLimite), text: fmtPlazo.format(limite) }), '.'),
      el('p', { class: 'transp-text' },
        `La inscripción se hace en la web actual de la FEP con el código ${quien} para este campeonato.`),
      hayInfo ? el('details', { class: 'org-more cpe-info' },
        el('summary', { text: 'Información para la inscripción' }), info) : null,
      acciones.length ? el('h3', { class: 'cpe-subtitle', text: '¿Qué deseas hacer?' }) : null,
      acciones.length ? el('ol', { class: 'cpe-steps' }, acciones.map((a) => {
        const hasta = aFecha(a.hasta);
        return el('li', { class: 'cpe-step' },
          el('div', { class: 'cpe-step__body' },
            enlaceWebActual(urlHttp(a.url), limpio(a.texto)),
            a.nota ? el('p', { class: 'cpe-step__note', text: limpio(a.nota) }) : null,
            hasta ? el('p', { class: 'cpe-step__until' }, 'Hasta el ', el('time', { datetime: String(a.hasta), text: fmtPlazo.format(hasta) })) : null));
      })) : null,
    ].filter(Boolean));
  } else {
    // Plazo terminado (o sin fecha): listado de equipos inscritos, como el ASP.
    $('#inscripcion-titulo').textContent = 'Equipos inscritos';
    const puntua = aFecha(i.fechaLimitePuntuacion);
    const url = urlHttp(i.equiposInscritosUrl);
    cont.replaceChildren(...[
      puntua && puntua > new Date()
        ? el('p', { class: 'camp-item__plazo cpe-plazo' },
          'El plazo para cambiar los jugadores y jugadoras que puntúan en los equipos inscritos termina el ',
          el('time', { datetime: String(i.fechaLimitePuntuacion), text: fmtPlazo.format(puntua) }), '.')
        : null,
      url ? el('ol', { class: 'cpe-steps cpe-steps--single' }, el('li', { class: 'cpe-step' },
        el('div', { class: 'cpe-step__body' },
          enlaceWebActual(url, 'Ver los equipos inscritos y sus integrantes'),
          el('p', { class: 'cpe-step__note', text: 'Listado en la web actual de la FEP.' })))) : null,
    ].filter(Boolean));
    if (!cont.childNodes.length) return;
  }
  mostrar('inscripcion');
}

function pintarInscritos(d) {
  const filas = lista(d.inscritosParejas).filter((x) => GENEROS[x?.genero]);
  if (!filas.length) return;
  $('#cpe-inscritos').replaceChildren(...filas.map((x) => {
    const url = urlHttp(x.url);
    const texto = plural(Number(x.parejas) || 0, 'pareja', 'parejas');
    return el('tr', {},
      el('td', { class: 'org-table__name', text: `Inscripciones ${GENEROS_CATEGORIA[x.genero].toLowerCase()}s` }),
      el('td', {}, url ? el('a', { class: 'org-contact', href: url }, texto) : texto));
  }));
  mostrar('inscritos');
}

/* ---------------------------------------------------------------------------
   Información general, adicional, cartel y noticias
   --------------------------------------------------------------------------- */
function dato(etiqueta, ...contenido) {
  const partes = contenido.flat().filter(Boolean);
  if (!partes.length) return null;
  return el('div', { class: 'fedf-contact__item' }, el('dt', { text: etiqueta }), el('dd', {}, partes));
}

function pintarInformacion(d) {
  const ini = aFecha(d.fechaInicio);
  const fin = aFecha(d.fechaFin);
  const sedes = lista(d.sedes).filter((s) => limpio(s?.nombre));
  const generos = lista(d.generos).filter((g) => GENEROS_CATEGORIA[g?.genero]);
  const cuotas = lista(d.cuotas).filter((c) => typeof c === 'number');

  const items = [
    dato('Fechas',
      ini ? el('span', { class: 'fedf-contact__line' }, 'Inicio: ', el('time', { datetime: String(d.fechaInicio), text: fmtDia.format(ini) })) : null,
      fin ? el('span', { class: 'fedf-contact__line' }, 'Final: ', el('time', { datetime: String(d.fechaFin), text: fmtDia.format(fin) })) : null),
    dato('Lugar', limpio(d.ciudad)),
    dato(sedes.length > 1 ? 'Sedes' : 'Club', sedes.map((s) => {
      const web = urlHttp(s.web);
      const mapa = Number.isFinite(s.latitud) && Number.isFinite(s.longitud)
        ? `https://www.openstreetmap.org/?mlat=${s.latitud}&mlon=${s.longitud}#map=17/${s.latitud}/${s.longitud}` : null;
      return el('span', { class: 'fedf-contact__line' }, limpio(s.nombre),
        web ? [' · ', el('a', { href: web, target: '_blank', rel: 'noopener' }, 'Web del club', nuevaPestana())] : null,
        mapa ? [' · ', el('a', { href: mapa, target: '_blank', rel: 'noopener' }, 'Ver en el mapa', nuevaPestana())] : null);
    })),
    dato('Categorías', generos.map((g) => {
      const cats = lista(g.categorias).map(limpio).filter(Boolean);
      return el('span', { class: 'fedf-contact__line', text: `${GENEROS_CATEGORIA[g.genero]}${cats.length ? `: ${cats.join(', ')}` : ''}` });
    })),
    dato('Cuota de inscripción', cuotas.length ? `${cuotas.map((c) => fmtEuros.format(c)).join(' / ')} por equipo` : null),
  ].filter(Boolean);

  $('#cpe-datos').replaceChildren(...items);
  mostrar('informacion', items.length > 0);
}

function pintarInfoAdicional(d) {
  mostrar('informacion-adicional', d.infoAdicionalHtml ? pintarHtml($('#cpe-info-adicional'), d.infoAdicionalHtml) : false);
}

function pintarCartel(d) {
  const src = urlImagen(d.cartelUrl);
  if (!src) return;
  const titulo = limpio(d.titulo);
  const imagen = el('img', { src, alt: `Cartel del ${titulo}`, loading: 'lazy', decoding: 'async' });
  // Si el cartel no carga, la sección desaparece (no hay más información en ella).
  imagen.addEventListener('error', () => { mostrar('cartel', false); construirIndice(); }, { once: true });
  const original = urlHttp(d.cartelUrl);
  $('#cpe-cartel').replaceChildren(...[
    original ? el('a', { class: 'org-figure__link', href: original, target: '_blank', rel: 'noopener' }, imagen, nuevaPestana()) : imagen,
    original ? el('figcaption', {}, el('span', { text: 'Cartel oficial de la prueba.' }),
      el('a', { href: original, target: '_blank', rel: 'noopener' }, 'Ver a tamaño completo', el('span', { 'aria-hidden': 'true', text: ' ↗' }), nuevaPestana())) : null,
  ].filter(Boolean));
  mostrar('cartel');
}

function pintarNoticias(d) {
  const noticias = lista(d.noticias).filter((n) => n && limpio(n.titulo));
  if (!noticias.length) return;
  $('#cpe-noticias-total').textContent = fmtNum.format(noticias.length);
  $('#cpe-noticias').replaceChildren(...noticias.map((n) => {
    const { href, target, rel } = attrsEnlace(n.enlace, n.ventana);
    const fecha = aFecha(n.fecha);
    const resumen = textoPlano(n.resumen, 180);
    const titulo = limpio(n.titulo);
    return el('li', { class: 'cpe-news__item' },
      img(urlImagen(n.imagenUrl), '', { class: 'cpe-news__img', width: 320, height: 180 }),
      el('div', { class: 'cpe-news__body' },
        fecha ? el('time', { class: 'cpe-news__meta', datetime: String(n.fecha).slice(0, 10), text: fmtLargo.format(fecha) }) : null,
        el('h3', { class: 'cpe-news__title' }, href !== '#'
          ? el('a', { href, target, rel }, titulo, target ? nuevaPestana() : null)
          : titulo),
        resumen ? el('p', { class: 'cpe-news__text', text: resumen }) : null));
  }));
  mostrar('noticias');
}

function pintarPie(d, id) {
  const url = urlHttp(d.urlWebActual) ?? urlWebActual(id);
  $('#cpe-pie').replaceChildren('Ficha publicada por la FEP. ',
    el('a', { href: url, target: '_blank', rel: 'noopener' }, 'Ver esta ficha en la web actual',
      el('span', { 'aria-hidden': 'true', text: ' ↗' }), nuevaPestana()));
  $('#cpe-pie').hidden = false;
}

/* ===========================================================================
   Índice lateral (solo las secciones visibles) y compartir
   =========================================================================== */
let observador = null;

function construirIndice() {
  const secciones = $$('#cpe-contenido > .cpe-section').filter((s) => !s.hidden);
  $('#cpe-indice').replaceChildren(...secciones.map((s) => el('li', {},
    el('a', { href: `#${s.id}`, dataset: { seccion: s.id } }, el('span', { text: s.dataset.indice })))));
  $('#cpe-indice').closest('.page-toc').hidden = !secciones.length;

  if (!('IntersectionObserver' in window)) return;
  observador?.disconnect();
  const enlaces = new Map($$('#cpe-indice a').map((a) => [a.dataset.seccion, a]));
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
  secciones.forEach((s) => observador.observe(s));
}

/** Sustituye a los enlaces "Compartir en" del ASP (Twitter/Facebook/Google+ a una URL equivocada). */
function initCompartir() {
  const estado = $('#cpe-compartir-estado');
  $('#cpe-compartir').addEventListener('click', async () => {
    const datos = { title: document.title, url: location.href };
    try {
      if (navigator.share) { await navigator.share(datos); return; }
      await navigator.clipboard.writeText(location.href);
      estado.textContent = 'Enlace copiado.';
    } catch (e) {
      if (e?.name !== 'AbortError') estado.textContent = 'No se ha podido compartir; copia la dirección de la barra del navegador.';
    }
  });
}

/* ===========================================================================
   Arranque
   =========================================================================== */
function mostrarError(texto, id) {
  $('#cpe-skeleton').hidden = true;
  const p = $('#cpe-error');
  p.replaceChildren(`${texto} Puedes consultarlo en la `, el('a', { href: urlWebActual(id) }, 'web actual de la FEP'), '.');
  p.hidden = false;
  $('#cpe-indice').closest('.page-toc').hidden = true;
  $('#cpe-contenido').setAttribute('aria-busy', 'false');
}

async function init() {
  const id = leerId();
  if (!id) {
    mostrarError('No se ha indicado qué campeonato mostrar.', null);
    return;
  }

  let d;
  try {
    d = await get(`/api/v1/competicion/campeonatos-equipos/${id}`);
    if (!d || String(d.id) !== id || !limpio(d.titulo)) throw new Error('Respuesta de otro campeonato o vacía');
  } catch (e) {
    console.warn('[FEP] campeonato por equipos:', e.message);
    mostrarError('No se ha podido cargar este campeonato o no está publicado.', id);
    return;
  }

  pintarCabecera(d);
  for (const pintar of [pintarNota, pintarDirecto, pintarCuadros, pintarNotificaciones, pintarInscripcion,
    pintarInscritos, pintarInformacion, pintarInfoAdicional, pintarCartel, pintarNoticias]) {
    try {
      pintar(d);
    } catch (e) {
      // Un bloque con datos inesperados no impide ver el resto de la ficha.
      console.warn(`[FEP] campeonato por equipos (${pintar.name}):`, e.message);
    }
  }
  pintarPie(d, id);
  $('#cpe-skeleton').hidden = true;
  $('#cpe-contenido').setAttribute('aria-busy', 'false');
  construirIndice();
  initCompartir();

  // Enlace con ancla (#cuadros…): las secciones se muestran después de cargar, así que se vuelve a saltar.
  if (location.hash) document.getElementById(location.hash.slice(1))?.scrollIntoView();
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
})();
