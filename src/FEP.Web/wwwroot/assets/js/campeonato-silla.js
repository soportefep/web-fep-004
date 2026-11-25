/**
 * Página "Torneo de pádel en silla de ruedas" (migrada desde Camp_Silla.asp?IdTorneo=, a la que Camp_Parejas.asp
 * redirigía los torneos con Circuito='SILLA').
 * Requiere assets/js/comun.js (cabecera, menú, pie y utilidades en window.FEP).
 *
 *   URL: campeonato-silla.html?id=30180 (también acepta IdTorneo / idtorneo, como la web actual)
 *   Ficha → GET /api/v1/competicion/campeonatos-silla/{id}
 *           (respaldo: mock/competicion-campeonatos-silla-{id}.json)
 *
 * Bloques en el orden del ASP: marcadores en directo, nota de prensa, cuadros, cuadro de honor, notificaciones,
 * inscritos, inscripción (solo con el plazo abierto), información general, información adicional, cartel y
 * noticias relacionadas. Solo se muestran los que tienen contenido y el índice lateral se construye con ellos.
 * Los cuadros, los listados de inscritos y el formulario de inscripción siguen en la web actual.
 *
 * Los textos de notificaciones, nota de prensa, condiciones e información son HTML heredado del editor de la
 * gestión. NUNCA se pinta con innerHTML: se analiza con DOMParser (que no ejecuta scripts) y se reconstruye nodo a
 * nodo con una lista blanca (ver "Saneado", la misma copia reducida de micrositio.js que usa campeonato-equipos.js).
 * El resto de textos de la API van por textContent.
 */
(function () {
'use strict';

const { $, $$, el, get, urlSegura, attrsEnlace, img, textoPlano, aFecha, bloqueFecha, fmtLargo } = window.FEP;
const { LEGACY_BASE } = window.FEPApi;

const ID = /^[1-9]\d{0,9}$/;
const SITIO = 'Federación Española de Pádel';
const DOCS_VISIBLES = 8;          // adjuntos visibles; el resto, plegados
const HONOR_VISIBLES = 10;        // filas del cuadro de honor visibles (cinco parejas); el resto, plegadas

const GENEROS = { masculino: 'Masculino', femenino: 'Femenino', mixto: 'Mixto' };
const GENEROS_CATEGORIA = { masculino: 'Masculina', femenino: 'Femenina', mixto: 'Mixta' };
const CATEGORIAS = { absoluto: 'Absoluto', menores: 'Menores', veteranos: 'Veteranos' };
const TIPOS_CUADRO = { final: 'Cuadro', consolacion: 'Consolación', liguilla: 'Liguilla', grupo: 'Grupo', previa: 'Previa' };
const PREFIJO_LICENCIA = 'PS';

/**
 * La misma ficha sirve para los torneos por parejas (Camp_Parejas.asp / Camp_Parejas_Veteranos.asp →
 * campeonato-parejas.html, <body data-modalidad="parejas">): Camp_Parejas.asp y Camp_Silla.asp leen las mismas
 * tablas. Cambian el endpoint, algunos textos y el enlace a la web actual.
 */
const MODALIDADES = {
  silla: {
    api: 'campeonatos-silla', paginaWeb: 'Camp_Silla.asp', eyebrow: 'Pádel en silla de ruedas',
    etiqueta: 'Silla de ruedas', log: 'torneo de silla de ruedas',
  },
  parejas: {
    api: 'campeonatos-parejas', paginaWeb: 'Camp_Parejas.asp', eyebrow: 'Campeonato por parejas',
    etiqueta: null, log: 'torneo por parejas',
  },
};
const MODO = MODALIDADES[document.body?.dataset.modalidad] ?? MODALIDADES.silla;
const ES_SILLA = MODO === MODALIDADES.silla;
/** Categorías de veteranos de la gestión ("Cat35") → "+35". */
const textoCategoria = (c) => {
  const m = /^cat\s*(\d{2})$/i.exec(limpio(c));
  return m ? `+${m[1]}` : limpio(c);
};

const fmtNum = new Intl.NumberFormat('es-ES');
const fmtPuntos = new Intl.NumberFormat('es-ES', { maximumFractionDigits: 2 });
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
/** ?id=30180, ?IdTorneo=30180 o ?idtorneo=30180 (Request() de ASP no distingue mayúsculas). */
function leerId() {
  const q = new Map([...new URLSearchParams(location.search)].map(([k, v]) => [k.toLowerCase(), v.trim()]));
  const id = q.get('id') ?? q.get('idtorneo') ?? '';
  return ID.test(id) ? id : null;
}

const urlWebActual = (id) => new URL(id ? `${MODO.paginaWeb}?IdTorneo=${id}`: 'Campeonatos', LEGACY_BASE).href;

/** URL http(s) válida (relativas → web actual). */
function urlHttp(valor) {
  const url = urlSegura(valor);
  return url && /^https?:/i.test(url) ? url : null;
}

/** URL de la web actual de la FEP (marcadores, cuadros, listados, formulario). */
function urlFep(valor) {
  const url = urlHttp(valor);
  return url && /(^|\.)padelfederacion\.es$/i.test(new URL(url).hostname) ? url : null;
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

/** Quita párrafos, títulos y elementos de lista vacíos (el editor dejaba muchos "&nbsp;" y <br />). */
function quitarVacios(raiz) {
  const candidatos = [...raiz.querySelectorAll('p, div, h3, h4, li, ul, ol, blockquote, strong, em, b, i, u')].reverse();
  for (const n of candidatos) {
    if (n.querySelector('img, iframe, table, hr')) continue;
    if (!n.textContent.replace(/[\s ]+/g, '')) n.remove();
  }
  // <br> sobrantes al principio y al final de cada bloque ("<p><br />COMPETICIÓN…</p>").
  const vacio = (x) => x && (x.nodeName === 'BR' || (x.nodeType === Node.TEXT_NODE && !x.data.trim()));
  for (const bloque of [raiz, ...raiz.querySelectorAll('p, div, li')]) {
    while (vacio(bloque.firstChild)) bloque.firstChild.remove();
    while (vacio(bloque.lastChild)) bloque.lastChild.remove();
  }
}

/** Pinta HTML heredado en `contenedor`; devuelve false si no queda nada visible. */
function pintarHtml(contenedor, html) {
  contenedor.replaceChildren(sanear(html));
  const hay = Boolean(contenedor.textContent.replace(/[\s ]+/g, '')) || Boolean(contenedor.querySelector('img, iframe, table'));
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

/** El torneo sigue en juego hasta el día siguiente a su último día. */
function enJuego(d) {
  const fin = aFecha(d.fechaFin ?? d.fechaInicio);
  return fin ? new Date(fin.getFullYear(), fin.getMonth(), fin.getDate() + 1) > new Date() : false;
}

/* ===========================================================================
   Cabecera
   =========================================================================== */
const DIAS = ['domingo', 'lunes', 'martes', 'miércoles', 'jueves', 'viernes', 'sábado'];
const fmtMes = new Intl.DateTimeFormat('es-ES', { month: 'long' });

/** "Del viernes 6 al domingo 8 de noviembre de 2026" (o "Viernes, 6 de noviembre de 2026" si es un solo día). */
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
  $('#cps-miga').textContent = titulo;
  document.title = `${titulo} · ${SITIO}`;

  const categoria = CATEGORIAS[d.categoria];
  $('#cps-eyebrow').textContent = [MODO.eyebrow, d.seleccion ? 'Selecciones autonómicas' : null, categoria].filter(Boolean).join(' · ');

  const sedes = lista(d.sedes).map((s) => limpio(s?.nombre)).filter(Boolean);
  const cuando = rangoFechas(aFecha(d.fechaInicio), aFecha(d.fechaFin));
  const donde = [limpio(d.ciudad), sedes.join(' y ')].filter(Boolean).join(', ');
  $('#cps-entradilla').textContent = [cuando, donde].filter(Boolean).join(' · ')
    || 'Cuadros, cuadro de honor, inscritos, inscripción e información del torneo.';

  const etiquetas = [
    { texto: 'Parejas', clase: 'org-tag' },
    MODO.etiqueta ? { texto: MODO.etiqueta, clase: 'org-tag' } : null,
    categoria ? { texto: categoria, clase: `org-tag cpe-tag--${d.categoria}` } : null,
    d.seleccion ? { texto: 'Selecciones autonómicas', clase: 'org-tag' } : null,
  ].filter(Boolean);
  $('#cps-etiquetas').replaceChildren(...etiquetas.map((t) => el('li', {}, el('span', { class: t.clase, text: t.texto }))));
  $('#cps-etiquetas').hidden = false;

  const fecha = bloqueFecha(d.fechaInicio, d.fechaFin);
  const parejas = lista(d.inscritosParejas).reduce((n, x) => n + (Number(x?.parejas) || 0), 0);
  const cuadros = lista(d.cuadros).reduce((n, g) => n + lista(g?.cuadros).length, 0);
  $('#cps-resumen').replaceChildren(...[
    fecha ? el('li', {}, el('strong', { text: fecha.dias }), ` ${fecha.mes}`) : null,
    parejas ? el('li', {}, el('strong', { text: fmtNum.format(parejas) }), ` ${parejas === 1 ? 'pareja inscrita' : 'parejas inscritas'}`) : null,
    cuadros ? el('li', {}, el('strong', { text: fmtNum.format(cuadros) }), ` ${cuadros === 1 ? 'cuadro' : 'cuadros'}`) : null,
  ].filter(Boolean));
  $('#cps-hero-acciones').hidden = false;
}

/* ===========================================================================
   Secciones
   =========================================================================== */
/**
 * Marcadores en directo (iframes de la web actual). El ASP mostraba siempre "Marcadores on-line", también en
 * torneos terminados, y los marcadores que se quedaban visibles daban error 500. Ahora solo mientras el torneo
 * está en juego.
 */
function pintarDirecto(d) {
  if (!enJuego(d)) return;
  const marcadores = lista(d.marcadores).map((m) => urlFep(m?.url)).filter(Boolean);
  $('#cps-marcadores-total').textContent = fmtNum.format(marcadores.length);
  $('#cps-marcadores').replaceChildren(...marcadores.map((url, i) => el('iframe', {
    src: url,
    title: `Marcador en directo del partido ${i + 1}`,
    loading: 'lazy',
    referrerpolicy: 'strict-origin-when-cross-origin',
    sandbox: 'allow-scripts allow-same-origin',
  })));
  $('#cps-marcadores').hidden = !marcadores.length;
  $('#cps-marcadores-vacio').hidden = marcadores.length > 0;
  mostrar('directo');
}

function pintarNota(d) {
  mostrar('nota-prensa', d.notaPrensaHtml ? pintarHtml($('#cps-nota'), d.notaPrensaHtml) : false);
}

function pintarCuadros(d) {
  if (!d.cuadrosPublicados) return;
  const grupos = lista(d.cuadros).filter((g) => GENEROS[g?.genero] && lista(g.cuadros).length);
  // La categoría solo se indica si no es la absoluta o si un género tiene varias.
  const porGenero = grupos.reduce((m, g) => m.set(g.genero, (m.get(g.genero) ?? 0) + 1), new Map());
  let total = 0;

  $('#cps-cuadros').replaceChildren(...grupos.map((g, n) => {
    const categoria = limpio(g.categoria);
    const conCategoria = categoria && (categoria.toUpperCase() !== 'ABSOLUTO' || porGenero.get(g.genero) > 1);
    // Previo.asp → cuadro.html (cuadros migrados); si el enlace no se puede migrar, la web actual.
    const enlaces = lista(g.cuadros).map((c) => {
      const href = urlFep(c?.url);
      return { ...c, href: href ? (window.FEP.paginaMigrada(href) ?? href) : null, titulo: limpio(c?.titulo) };
    }).filter((c) => c.href && c.titulo);
    total += enlaces.length;
    const idTitulo = `cuadros-${g.genero}-${n + 1}`;
    return el('section', { class: 'cpe-draws-group', 'aria-labelledby': idTitulo },
      el('h3', { class: 'cpe-draws-group__title', id: idTitulo },
        GENEROS[g.genero], conCategoria ? el('span', { class: 'org-tag', text: textoCategoria(categoria) }) : null,
        el('span', { class: 'org-count', text: fmtNum.format(enlaces.length) })),
      el('ul', { class: 'cpe-draws' }, enlaces.map((c) => el('li', {},
        el('a', { class: 'cpe-draw', href: c.href },
          el('span', { class: 'cpe-draw__body' },
            el('span', { class: 'cpe-draw__type', text: TIPOS_CUADRO[c.tipo] ?? 'Cuadro' }),
            el('span', { class: 'cpe-draw__title', text: c.titulo })),
          el('span', { class: 'cpe-draw__arrow', 'aria-hidden': 'true', text: '→' }))))));
  }));
  $('#cps-cuadros-total').textContent = fmtNum.format(total);
  $('#cps-cuadros-vacio').hidden = total > 0;
  mostrar('cuadros');
}

/** Cuadro de honor: una fila por jugador/a (las parejas comparten puesto y puntos), plegado a partir de 10. */
function pintarHonor(d) {
  const filas = lista(d.cuadroHonor).filter((h) => h && limpio(h.nombre) && Number.isFinite(Number(h.licencia)));
  if (!filas.length) return;
  $('#cps-honor-total').textContent = fmtNum.format(filas.length);

  const tbody = $('#cps-honor');
  tbody.replaceChildren(...filas.map((h, i) => {
    const url = urlFep(h.estadisticasUrl);
    const nombre = limpio(h.nombre);
    const puesto = limpio(h.puesto);
    // El puesto se repite en la segunda fila de cada pareja: se atenúa para que se lea por parejas.
    const repetido = i > 0 && puesto && puesto === limpio(filas[i - 1].puesto);
    return el('tr', { hidden: i >= HONOR_VISIBLES ? true : null, class: repetido ? 'cps-honor__pareja' : null },
      el('td', { class: 'org-table__name cps-honor__puesto', text: puesto || '—' }),
      el('td', {}, url
        ? el('a', { class: 'org-contact', href: url, target: '_blank', rel: 'noopener' }, nombre, nuevaPestana())
        : nombre),
      el('td', { class: 'org-table__detail', text: `${PREFIJO_LICENCIA}${h.licencia}` }),
      el('td', { class: 'cps-honor__pts', text: fmtPuntos.format(Number(h.puntos) || 0) }));
  }));

  const boton = $('#cps-honor-mas');
  const ocultas = filas.length - HONOR_VISIBLES;
  if (ocultas > 0) {
    const texto = (abierto) => (abierto ? 'Ver menos' : `Ver la clasificación completa (${plural(ocultas, 'puesto más', 'puestos más')})`);
    boton.textContent = texto(false);
    boton.hidden = false;
    boton.addEventListener('click', () => {
      const abrir = boton.getAttribute('aria-expanded') !== 'true';
      $$('tr', tbody).forEach((tr, i) => { if (i >= HONOR_VISIBLES) tr.hidden = !abrir; });
      boton.setAttribute('aria-expanded', String(abrir));
      boton.textContent = texto(abrir);
      if (!abrir) $('#cuadro-honor').scrollIntoView({ block: 'start' });
    });
  }
  mostrar('cuadro-honor');
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
  const hayTexto = d.notificacionesHtml ? pintarHtml($('#cps-notificaciones'), d.notificacionesHtml) : false;
  const adjuntos = lista(d.adjuntos).filter((a) => a && limpio(a.titulo));
  const ul = $('#cps-adjuntos');
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
   Inscritos e inscripción
   --------------------------------------------------------------------------- */
function pintarInscritos(d) {
  const filas = lista(d.inscritosParejas).filter((x) => GENEROS[x?.genero]);
  if (!filas.length) return;
  const fila = (etiqueta, url, texto) => el('tr', {},
    el('td', { class: 'org-table__name', text: etiqueta }),
    el('td', {}, url ? el('a', { class: 'org-contact', href: url }, texto) : texto));

  const e = d.entrenadores;
  const numEntrenadores = Number(e?.numero) || 0;
  $('#cps-inscritos').replaceChildren(
    ...filas.map((x) => fila(`Inscripciones ${GENEROS_CATEGORIA[x.genero].toLowerCase()}s`, urlFep(x.url),
      plural(Number(x.parejas) || 0, 'pareja', 'parejas'))),
    numEntrenadores > 0 ? fila('Entrenadores', urlFep(e.url), plural(numEntrenadores, 'entrenador o entrenadora', 'entrenadores')) : '');
  mostrar('inscritos');
}

function enlaceWeb(href, texto) {
  return el('a', { href, target: '_blank', rel: 'noopener' }, texto, nuevaPestana());
}

/** Pasos del formulario de la web actual (texto "Cómo realizar la inscripción" del ASP, reescrito). */
function pasosInscripcion(individual) {
  const quien = individual ? 'la jugadora o el jugador' : 'cada jugador o jugadora de la pareja';
  if (!ES_SILLA) {
    return [
      ['Número de licencia FEP',
        [`Introduce la licencia en vigor de ${quien}: el formulario completa el nombre y los apellidos. `,
          'Si no aparecen, la licencia no consta como renovada: ponte en contacto con tu federación autonómica. ',
          enlaceWeb('https://www.padelfederacion.es/Licencias_Buscar', 'Buscar mi número de licencia'), '.']],
      ['Datos para verificar la inscripción',
        ['Fecha de nacimiento (comprueba la categoría), y un móvil y un correo electrónico de contacto.']],
      ['Entrenador o entrenadora (opcional)',
        ['Número de licencia de técnico, renovada para esta temporada.']],
    ];
  }
  return [
    ['Número de licencia de pádel en silla de ruedas',
      [`Introduce la licencia de silla de ruedas FEP de ${quien} (empieza por ${PREFIJO_LICENCIA}): el formulario completa el nombre y los apellidos. `,
        'Si no aparecen, la licencia no consta como renovada: ponte en contacto con la ',
        enlaceWeb('https://www.padelfederacion.es/Contacto.asp', 'FEP'), ' o con tu federación autonómica. ',
        enlaceWeb('https://www.padelfederacion.es/Licencias_Buscar', 'Buscar mi número de licencia'), '.']],
    ['Datos para verificar la inscripción',
      ['Clasificación funcional (1 a 4), fecha de nacimiento y DNI, y un móvil y un correo electrónico de contacto.']],
    ['Entrenador o entrenadora (opcional)',
      ['Número de licencia de técnico, renovada para esta temporada.']],
  ];
}

function pintarInscripcion(d) {
  const i = d.inscripcion ?? {};
  const limite = inscripcionAbierta(i);
  const formulario = urlFep(i.formularioUrl) ?? urlWebActual(d.id);
  // El ASP solo mostraba la inscripción con el plazo abierto.
  if (!limite) return;

  const condiciones = el('div', { class: 'ms-rich' });
  const hayCondiciones = i.condicionesHtml ? pintarHtml(condiciones, i.condicionesHtml) : false;
  const info = el('div', { class: 'ms-rich' });
  const hayInfo = i.informacionHtml ? pintarHtml(info, i.informacionHtml) : false;
  info.hidden = false;

  $('#cps-inscripcion').replaceChildren(...[
    el('p', { class: 'camp-item__plazo cpe-plazo' },
      el('strong', { text: 'Inscripción abierta' }), ' hasta el ',
      el('time', { datetime: String(i.fechaLimite), text: fmtPlazo.format(limite) }), '.'),
    el('p', { class: 'transp-text', text: i.individual
      ? 'Inscripción individual.'
      : 'Inscripción por parejas: se inscribe la pareja completa, con los datos de las dos personas.' }),
    hayCondiciones ? el('h3', { class: 'cpe-subtitle', text: 'Condiciones de inscripción' }) : null,
    hayCondiciones ? el('div', { class: 'cps-condiciones' }, condiciones) : null,
    hayInfo ? el('details', { class: 'org-more cpe-info' },
      el('summary', { text: 'Información para la inscripción' }), info) : null,
    el('h3', { class: 'cpe-subtitle', text: 'Cómo realizar la inscripción' }),
    el('ol', { class: 'cpe-steps' }, pasosInscripcion(Boolean(i.individual)).map(([titulo, nota]) => el('li', { class: 'cpe-step' },
      el('div', { class: 'cpe-step__body' },
        el('p', { class: 'cpe-step__link', text: titulo }),
        el('p', { class: 'cpe-step__note' }, nota))))),
    el('p', { class: 'cps-formulario' },
      el('a', { class: 'btn btn--red', href: formulario }, 'Ir al formulario de inscripción ', el('span', { 'aria-hidden': 'true', text: '→' }))),
    el('p', { class: 'org-updated' }, 'El formulario está en la web actual de la FEP. Los datos se tratan según la ',
      enlaceWeb('https://www.padelfederacion.es/Politica_Privacidad', 'política de privacidad de la FEP'), '.'),
  ].filter(Boolean));

  $('#cps-ir-inscripcion').hidden = false;
  mostrar('inscripcion');
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
      const cats = lista(g.categorias).map(textoCategoria).filter(Boolean);
      return el('span', { class: 'fedf-contact__line', text: `${GENEROS_CATEGORIA[g.genero]}${cats.length ? `: ${cats.join(', ')}` : ''}` });
    })),
    dato('Cuota de inscripción', cuotas.length ? cuotas.map((c) => fmtEuros.format(c)).join(' / ') : null),
  ].filter(Boolean);

  $('#cps-datos').replaceChildren(...items);
  mostrar('informacion', items.length > 0);
}

function pintarInfoAdicional(d) {
  mostrar('informacion-adicional', d.infoAdicionalHtml ? pintarHtml($('#cps-info-adicional'), d.infoAdicionalHtml) : false);
}

function pintarCartel(d) {
  const src = urlImagen(d.cartelUrl);
  if (!src) return;
  const titulo = limpio(d.titulo);
  const imagen = el('img', { src, alt: `Cartel del ${titulo}`, loading: 'lazy', decoding: 'async' });
  // Si el cartel no carga, la sección desaparece (no hay más información en ella).
  imagen.addEventListener('error', () => { mostrar('cartel', false); construirIndice(); }, { once: true });
  const original = urlHttp(d.cartelUrl);
  $('#cps-cartel').replaceChildren(...[
    original ? el('a', { class: 'org-figure__link', href: original, target: '_blank', rel: 'noopener' }, imagen, nuevaPestana()) : imagen,
    original ? el('figcaption', {}, el('span', { text: 'Cartel oficial de la prueba.' }),
      el('a', { href: original, target: '_blank', rel: 'noopener' }, 'Ver a tamaño completo', el('span', { 'aria-hidden': 'true', text: ' ↗' }), nuevaPestana())) : null,
  ].filter(Boolean));
  mostrar('cartel');
}

function pintarNoticias(d) {
  const noticias = lista(d.noticias).filter((n) => n && limpio(n.titulo));
  if (!noticias.length) return;
  $('#cps-noticias-total').textContent = fmtNum.format(noticias.length);
  $('#cps-noticias').replaceChildren(...noticias.map((n) => {
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
  const url = urlFep(d.urlWebActual) ?? urlWebActual(id);
  $('#cps-pie').replaceChildren('Ficha publicada por la FEP. ',
    el('a', { href: url, target: '_blank', rel: 'noopener' }, 'Ver esta ficha en la web actual',
      el('span', { 'aria-hidden': 'true', text: ' ↗' }), nuevaPestana()));
  $('#cps-pie').hidden = false;
}

/* ===========================================================================
   Índice lateral (solo las secciones visibles) y compartir
   =========================================================================== */
let observador = null;

function construirIndice() {
  const secciones = $$('#cps-contenido > .cpe-section').filter((s) => !s.hidden);
  $('#cps-indice').replaceChildren(...secciones.map((s) => el('li', {},
    el('a', { href: `#${s.id}`, dataset: { seccion: s.id } }, el('span', { text: s.dataset.indice })))));
  $('#cps-indice').closest('.page-toc').hidden = !secciones.length;

  if (!('IntersectionObserver' in window)) return;
  observador?.disconnect();
  const enlaces = new Map($$('#cps-indice a').map((a) => [a.dataset.seccion, a]));
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

/** Sustituye a los enlaces "Compartir en" del ASP (Twitter/Facebook/Google+ a Camp_Parejas.asp por http). */
function initCompartir() {
  const estado = $('#cps-compartir-estado');
  $('#cps-compartir').addEventListener('click', async () => {
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
  $('#cps-skeleton').hidden = true;
  const p = $('#cps-error');
  p.replaceChildren(`${texto} Puedes consultarlo en la `, el('a', { href: urlWebActual(id) }, 'web actual de la FEP'), '.');
  p.hidden = false;
  $('#cps-indice').closest('.page-toc').hidden = true;
  $('#cps-contenido').setAttribute('aria-busy', 'false');
}

async function init() {
  const id = leerId();
  if (!id) {
    mostrarError('No se ha indicado qué torneo mostrar.', null);
    return;
  }

  let d;
  try {
    d = await get(`/api/v1/competicion/${MODO.api}/${id}`);
    if (!d || String(d.id) !== id || !limpio(d.titulo)) throw new Error('Respuesta de otro torneo o vacía');
  } catch (e) {
    console.warn(`[FEP] ${MODO.log}:`, e.message);
    mostrarError('No se ha podido cargar este torneo o no está publicado.', id);
    return;
  }

  pintarCabecera(d);
  for (const pintar of [pintarDirecto, pintarNota, pintarCuadros, pintarHonor, pintarNotificaciones, pintarInscritos,
    pintarInscripcion, pintarInformacion, pintarInfoAdicional, pintarCartel, pintarNoticias]) {
    try {
      pintar(d);
    } catch (e) {
      // Un bloque con datos inesperados no impide ver el resto de la ficha.
      console.warn(`[FEP] ${MODO.log} (${pintar.name}):`, e.message);
    }
  }
  pintarPie(d, id);
  $('#cps-skeleton').hidden = true;
  $('#cps-contenido').setAttribute('aria-busy', 'false');
  construirIndice();
  initCompartir();

  // Enlace con ancla (#cuadros…): las secciones se muestran después de cargar, así que se vuelve a saltar.
  if (location.hash) document.getElementById(location.hash.slice(1))?.scrollIntoView();
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
})();
