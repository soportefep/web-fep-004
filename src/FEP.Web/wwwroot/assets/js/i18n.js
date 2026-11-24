/**
 * FEP · Idiomas de la web pública.
 *
 * Elección del idioma (de más a menos prioridad):
 *   1. ?lang=xx en la URL (enlaces compartidos).
 *   2. La elección manual del visitante (selector de banderas de la barra superior), guardada en este navegador.
 *   3. El PAÍS desde el que se conecta. El navegador no puede saberlo: en producción lo pone el servidor en
 *      <meta name="fep-pais" content="GB"> a partir de la cabecera de país del CDN o del proxy (p. ej.
 *      CF-IPCountry o X-Country-Code), es decir, el país de la IP… o de la VPN. En el prototipo se simula con
 *      ?pais=GB. Si el navegador usa otro idioma de ese país que tengamos (catalán en España, francés en
 *      Bélgica…), se usa ese.
 *   4. El idioma del navegador.
 *   5. Español.
 * Si el idioma se ha elegido solo (3 o 4), se avisa con una barra discreta para mantenerlo o cambiarlo.
 *
 * Traducción: diccionarios por idioma (assets/i18n/<código>.js) con el TEXTO ESPAÑOL como clave. Se traducen
 * los textos fijos de la interfaz (barra superior, cabecera, menú, buscador, pie…) sin tocar el HTML de cada
 * página: se recorre el DOM (textos y atributos aria-label, title, placeholder y alt) y se vigilan los cambios
 * (MutationObserver), así también se traduce lo que pinta JavaScript después. Lo que no está en el diccionario
 * (noticias, rankings, resultados… que vienen de la API) se queda en español; el selector lo indica.
 * Para añadir un idioma: un fichero assets/i18n/<código>.js y una entrada en IDIOMAS.
 */
(function () {
'use strict';

/* ---------------------------------------------------------------------------
   Idiomas disponibles (nombre en su propio idioma) y banderas
   La bandera solo ayuda a reconocerlo: el nombre del idioma va siempre al lado.
   --------------------------------------------------------------------------- */
const IDIOMAS = [
  { codigo: 'es', nombre: 'Español' },
  { codigo: 'ca', nombre: 'Català' },
  { codigo: 'eu', nombre: 'Euskara' },
  { codigo: 'gl', nombre: 'Galego' },
  { codigo: 'en', nombre: 'English' },
  { codigo: 'fr', nombre: 'Français' },
  { codigo: 'pt', nombre: 'Português' },
  { codigo: 'it', nombre: 'Italiano' },
  { codigo: 'de', nombre: 'Deutsch' },
  { codigo: 'nl', nombre: 'Nederlands' },
  { codigo: 'sv', nombre: 'Svenska' },
  { codigo: 'ar', nombre: 'العربية', dir: 'rtl' },
  { codigo: 'zh', nombre: '中文' },
  { codigo: 'ja', nombre: '日本語' },
];
const POR_CODIGO = new Map(IDIOMAS.map((i) => [i.codigo, i]));
const soportado = (c) => !!c && POR_CODIGO.has(c);

const BANDERAS = {
  es: '<svg viewBox="0 0 30 20"><rect width="30" height="20" fill="#AA151B"/><rect y="5" width="30" height="10" fill="#F1BF00"/></svg>',
  ca: '<svg viewBox="0 0 30 20"><rect width="30" height="20" fill="#FCDD09"/><g fill="#DA121A"><rect y="2.22" width="30" height="2.22"/><rect y="6.67" width="30" height="2.22"/><rect y="11.11" width="30" height="2.22"/><rect y="15.56" width="30" height="2.22"/></g></svg>',
  eu: '<svg viewBox="0 0 30 20"><rect width="30" height="20" fill="#D52B1E"/><path d="M0 0L30 20M30 0L0 20" stroke="#009B48" stroke-width="3"/><path d="M15 0V20M0 10H30" stroke="#fff" stroke-width="2.6"/></svg>',
  gl: '<svg viewBox="0 0 30 20"><rect width="30" height="20" fill="#fff"/><path d="M0 0L30 20" stroke="#0099CC" stroke-width="4.5"/></svg>',
  en: '<svg viewBox="0 0 60 30"><rect width="60" height="30" fill="#012169"/><path d="M0 0L60 30M60 0L0 30" stroke="#fff" stroke-width="6"/><path d="M0 0L60 30M60 0L0 30" stroke="#C8102E" stroke-width="2"/><path d="M30 0V30M0 15H60" stroke="#fff" stroke-width="10"/><path d="M30 0V30M0 15H60" stroke="#C8102E" stroke-width="6"/></svg>',
  fr: '<svg viewBox="0 0 30 20"><rect width="10" height="20" fill="#0055A4"/><rect x="10" width="10" height="20" fill="#fff"/><rect x="20" width="10" height="20" fill="#EF4135"/></svg>',
  pt: '<svg viewBox="0 0 30 20"><rect width="12" height="20" fill="#046A38"/><rect x="12" width="18" height="20" fill="#DA291C"/><circle cx="12" cy="10" r="4.5" fill="#FFE900" stroke="#000" stroke-width=".3"/></svg>',
  it: '<svg viewBox="0 0 30 20"><rect width="10" height="20" fill="#009246"/><rect x="10" width="10" height="20" fill="#fff"/><rect x="20" width="10" height="20" fill="#CE2B37"/></svg>',
  de: '<svg viewBox="0 0 30 20"><rect width="30" height="6.67" fill="#000"/><rect y="6.67" width="30" height="6.67" fill="#DD0000"/><rect y="13.33" width="30" height="6.67" fill="#FFCE00"/></svg>',
  nl: '<svg viewBox="0 0 30 20"><rect width="30" height="6.67" fill="#AE1C28"/><rect y="6.67" width="30" height="6.67" fill="#fff"/><rect y="13.33" width="30" height="6.67" fill="#21468B"/></svg>',
  sv: '<svg viewBox="0 0 30 20"><rect width="30" height="20" fill="#006AA7"/><path d="M11 0V20M0 10H30" stroke="#FECC00" stroke-width="4"/></svg>',
  // El árabe se habla en muchos países: en vez de la bandera de uno, un distintivo con la letra ع
  ar: '<svg viewBox="0 0 30 20"><rect width="30" height="20" fill="#0B6E4F"/><text x="15" y="15" font-size="13" text-anchor="middle" fill="#fff" font-family="Arial, sans-serif">ع</text></svg>',
  zh: '<svg viewBox="0 0 30 20"><rect width="30" height="20" fill="#EE1C25"/><polygon fill="#FFFF00" points="5.5,2.5 6.18,4.57 8.35,4.57 6.59,5.86 7.26,7.93 5.5,6.65 3.74,7.93 4.41,5.86 2.65,4.57 4.82,4.57"/></svg>',
  ja: '<svg viewBox="0 0 30 20"><rect width="30" height="20" fill="#fff"/><circle cx="15" cy="10" r="6" fill="#BC002D"/></svg>',
};
const bandera = (codigo) => (BANDERAS[codigo] ?? '').replace('<svg ', '<svg class="idioma-bandera" aria-hidden="true" focusable="false" preserveAspectRatio="xMidYMid slice" ');

/** Idiomas de cada país (por orden de preferencia). Los países que no están aquí, inglés. */
const IDIOMA_PAIS = (() => {
  const m = {};
  const poner = (idiomas, paises) => paises.split(' ').forEach((p) => { m[p] = idiomas; });
  poner(['es', 'ca', 'eu', 'gl'], 'ES');
  poner(['ca', 'es'], 'AD');
  poner(['es'], 'MX AR CL CO PE UY PY VE EC BO CR PA DO GT HN SV NI CU PR GQ');
  poner(['en'], 'GB IE US CA AU NZ ZA IN SG MT GI');
  poner(['fr', 'nl', 'de'], 'BE');
  poner(['fr'], 'FR MC LU SN CI');
  poner(['de', 'fr', 'it'], 'CH');
  poner(['de'], 'DE AT LI');
  poner(['pt'], 'PT BR AO MZ CV');
  poner(['it'], 'IT SM VA');
  poner(['nl'], 'NL SR');
  poner(['sv'], 'SE');
  poner(['sv', 'en'], 'FI');
  poner(['ar', 'en'], 'QA AE SA KW BH OM');
  poner(['ar', 'fr'], 'EG JO LB IQ LY SY YE PS MR');
  poner(['zh'], 'CN TW HK MO');
  poner(['ja'], 'JP');
  // Marruecos, Argelia y Túnez: árabe y francés
  poner(['ar', 'fr'], 'MA DZ TN');
  return m;
})();

/* ---------------------------------------------------------------------------
   Estado y utilidades
   --------------------------------------------------------------------------- */
const CLAVE = 'fep-idioma';
const CLAVE_AVISO = 'fep-idioma-aviso';
const leer = (k) => { try { return localStorage.getItem(k); } catch { return null; } };
const guardar = (k, v) => { try { if (v == null) localStorage.removeItem(k); else localStorage.setItem(k, v); } catch { /* sin almacenamiento */ } };

const DICCIONARIOS = {};
let actual = 'es';
let dic = null;
const base = (() => {
  const s = document.currentScript?.src || document.querySelector('script[src*="assets/js/i18n.js"]')?.src;
  return s ? new URL('../', s).href : new URL('assets/', document.baseURI).href;
})();

/** Texto de la propia interfaz de idiomas, traducido al idioma actual. */
function t(clave, vars = {}) {
  const plantilla = (actual !== 'es' && dic?.[clave]) || clave;
  return plantilla.replace(/\{(\w+)\}/g, (_, k) => vars[k] ?? '');
}
/** Nombre del idioma en el idioma actual (p. ej. "inglés", "English", "anglais"). */
function nombreIdioma(codigo, en = actual) {
  try { return new Intl.DisplayNames([en], { type: 'language' }).of(codigo); } catch { return POR_CODIGO.get(codigo)?.nombre ?? codigo; }
}
function nombrePais(pais, en = actual) {
  try { return new Intl.DisplayNames([en], { type: 'region' }).of(pais); } catch { return pais; }
}

/* ---------------------------------------------------------------------------
   Traducción por diccionario (clave = texto en español)
   --------------------------------------------------------------------------- */
// Textos con una parte variable
const PATRONES = [
  { re: /^(?<x>.+) \(se abre en una pestaña nueva\)$/, clave: '{x} (se abre en una pestaña nueva)', traducir: ['x'] },
  { re: /^(?<n>\d+) secciones$/, clave: '{n} secciones' },
  { re: /^Abrir el submenú de (?<x>.+)$/, clave: 'Abrir el submenú de {x}', traducir: ['x'] },
  // Solo la coletilla (texto oculto para lectores de pantalla detrás de un enlace externo)
  { re: /^\(se abre en una pestaña nueva\)$/, clave: '{x} (se abre en una pestaña nueva)', soloResto: true },
  { re: /^(?<x>.+) \(próximamente\)$/, clave: '{x} (próximamente)', traducir: ['x'] },
  // Portada
  { re: /^(?<a>\d+) de (?<b>\d+)$/, clave: '{a} de {b}' },
  { re: /^Ver más citas \((?<n>\d+)\)$/, clave: 'Ver más citas ({n})' },
  { re: /^(?<n>\d+) convocatorias abiertas$/, clave: '{n} convocatorias abiertas' },
  { re: /^Próximo cierre: (?<x>.+)$/, clave: 'Próximo cierre: {x}', fecha: ['x'] },
  { re: /^Inscripción hasta el (?<x>.+)$/, clave: 'Inscripción hasta el {x}', fecha: ['x'] },
];
// Fechas que la página escribe en español ("20 de noviembre de 2026", "20 de noviembre") → formato del idioma actual
const MESES_ES = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'];
function fechaLocal(texto) {
  const m = /^(\d{1,2}) de ([a-z]+)(?: de (\d{4}))?$/i.exec(texto.trim());
  const mes = m ? MESES_ES.indexOf(m[2].toLowerCase()) : -1;
  if (mes < 0) return texto;
  const conAnio = !!m[3];
  const f = new Date(conAnio ? +m[3] : 2000, mes, +m[1]);
  try {
    return new Intl.DateTimeFormat(actual === 'en' ? 'en-GB' : actual, { day: 'numeric', month: 'long', ...(conAnio ? { year: 'numeric' } : {}) }).format(f);
  } catch { return texto; }
}
function buscar(core) {
  if (!dic) return null;
  if (Object.prototype.hasOwnProperty.call(dic, core)) return dic[core];
  for (const p of PATRONES) {
    const m = p.re.exec(core);
    const plantilla = m && dic[p.clave];
    if (!plantilla) continue;
    if (p.soloResto) return plantilla.replace('{x}', '').trim();
    return plantilla.replace(/\{(\w+)\}/g, (_, k) => {
      const v = m.groups[k];
      if (p.fecha?.includes(k)) return fechaLocal(v);
      return p.traducir?.includes(k) ? (buscar(v) ?? v) : v;
    });
  }
  return null;
}
/** Traduce un texto completo conservando los espacios de los extremos; null si no hay traducción. */
function traducir(texto) {
  const core = texto.replace(/\s+/g, ' ').trim();
  if (!core) return null;
  const r = buscar(core);
  if (r == null) return null;
  // En chino y japonés no se separan las palabras con espacios: se quitan los de los extremos
  // cuando la traducción empieza o acaba en uno de esos caracteres ("ひとつの<em>コミュニティ。</em>").
  const cjk = /[　-鿿＀-￯]/;
  const antes = cjk.test(r[0]) ? '' : texto.match(/^\s*/)[0];
  const despues = cjk.test(r[r.length - 1]) ? '' : texto.match(/\s*$/)[0];
  return antes + r + despues;
}

const ATRIBUTOS = ['aria-label', 'title', 'placeholder', 'alt'];
const original = new WeakMap();   // nodo de texto -> texto en español
const puesto = new WeakMap();     // nodo de texto -> último texto que pusimos
const originalAttr = new WeakMap(); // elemento -> { atributo: valor en español }
const puestoAttr = new WeakMap();
const EXCLUIR = 'script, style, noscript, textarea, code, pre, svg, [translate="no"], .notranslate, [contenteditable="true"]';

function aceptar(elemento) {
  return !!elemento && !elemento.closest(EXCLUIR);
}
function procesarTexto(n) {
  if (!aceptar(n.parentElement)) return;
  if (!original.has(n) || n.nodeValue !== puesto.get(n)) original.set(n, n.nodeValue);   // primera vez o lo cambió la página
  const o = original.get(n);
  const v = actual === 'es' ? o : (traducir(o) ?? o);
  if (n.nodeValue !== v) n.nodeValue = v;
  puesto.set(n, v);
}
function procesarAtributo(e, a) {
  if (!e.hasAttribute(a) || !aceptar(e)) return;
  const orig = originalAttr.get(e) ?? {};
  const puestos = puestoAttr.get(e) ?? {};
  const valor = e.getAttribute(a);
  if (!(a in orig) || valor !== puestos[a]) orig[a] = valor;
  const v = actual === 'es' ? orig[a] : (traducir(orig[a]) ?? orig[a]);
  if (valor !== v) e.setAttribute(a, v);
  puestos[a] = v;
  originalAttr.set(e, orig);
  puestoAttr.set(e, puestos);
}
function recorrer(raiz) {
  if (!raiz) return;
  if (raiz.nodeType === Node.TEXT_NODE) { procesarTexto(raiz); return; }
  if (raiz.nodeType !== Node.ELEMENT_NODE) return;
  const w = document.createTreeWalker(raiz, NodeFilter.SHOW_TEXT);
  for (let n = w.nextNode(); n; n = w.nextNode()) procesarTexto(n);
  const sel = ATRIBUTOS.map((a) => `[${a}]`).join(',');
  for (const e of [raiz, ...raiz.querySelectorAll(sel)]) ATRIBUTOS.forEach((a) => procesarAtributo(e, a));
}

// Título de la pestaña: "Página · Federación Española de Pádel" (cada parte por separado)
let tituloOriginal = document.title;
let tituloPuesto = null;
function traducirTitulo() {
  if (document.title !== tituloPuesto) tituloOriginal = document.title;
  const v = actual === 'es' ? tituloOriginal : tituloOriginal.split(' · ').map((p) => buscar(p.trim()) ?? p).join(' · ');
  if (document.title !== v) document.title = v;
  tituloPuesto = v;
}

let observador = null;
function vigilar() {
  if (observador) return;
  observador = new MutationObserver((cambios) => {
    for (const c of cambios) {
      if (c.type === 'childList') c.addedNodes.forEach((n) => recorrer(n));
      else if (c.type === 'characterData') procesarTexto(c.target);
      else if (c.type === 'attributes') procesarAtributo(c.target, c.attributeName);
    }
    if (document.title !== tituloPuesto) traducirTitulo();
  });
  observador.observe(document.documentElement, { childList: true, subtree: true, characterData: true, attributes: true, attributeFilter: ATRIBUTOS });
}

/* ---------------------------------------------------------------------------
   Carga de diccionarios (un <script> por idioma: funciona también abriendo la web desde disco)
   --------------------------------------------------------------------------- */
const cargas = {};
function cargarDiccionario(codigo) {
  if (codigo === 'es' || DICCIONARIOS[codigo]) return Promise.resolve(DICCIONARIOS[codigo] ?? null);
  cargas[codigo] ??= new Promise((ok, mal) => {
    const s = document.createElement('script');
    s.src = `${base}i18n/${codigo}.js`;
    s.onload = () => (DICCIONARIOS[codigo] ? ok(DICCIONARIOS[codigo]) : mal(new Error(`Diccionario ${codigo} vacío`)));
    s.onerror = () => { delete cargas[codigo]; mal(new Error(`No se pudo cargar el diccionario ${codigo}`)); };
    document.head.append(s);
  });
  return cargas[codigo];
}

/* ---------------------------------------------------------------------------
   Aplicar un idioma
   --------------------------------------------------------------------------- */
async function aplicar(codigo) {
  if (!soportado(codigo)) codigo = 'es';
  try { dic = await cargarDiccionario(codigo); } catch (e) { console.warn('[FEP] idiomas:', e.message); codigo = 'es'; dic = null; }
  actual = codigo;
  const info = POR_CODIGO.get(codigo);
  document.documentElement.lang = codigo;
  document.documentElement.dir = info.dir ?? 'ltr';
  document.documentElement.classList.toggle('idioma-rtl', info.dir === 'rtl');
  recorrer(document.body);
  traducirTitulo();
  vigilar();
  pintarBoton();
  pintarPanel();
  document.dispatchEvent(new CustomEvent('fep:idioma', { detail: { idioma: codigo } }));
}

/* ---------------------------------------------------------------------------
   Detección automática
   --------------------------------------------------------------------------- */
function detectar() {
  const q = new URLSearchParams(location.search);
  const porUrl = q.get('lang')?.toLowerCase();
  if (soportado(porUrl)) return { codigo: porUrl, motivo: 'url' };
  const guardado = leer(CLAVE);
  if (soportado(guardado)) return { codigo: guardado, motivo: 'manual' };
  const navegador = (navigator.languages?.length ? navigator.languages : [navigator.language || 'es']).map((l) => String(l).toLowerCase().split('-')[0]);
  const pais = (q.get('pais') || document.querySelector('meta[name="fep-pais"]')?.content || '').trim().toUpperCase();
  if (/^[A-Z]{2}$/.test(pais)) {
    const delPais = IDIOMA_PAIS[pais] ?? ['en'];
    const codigo = navegador.find((l) => delPais.includes(l) && soportado(l)) ?? delPais.find(soportado) ?? 'en';
    return { codigo, motivo: 'pais', pais };
  }
  const n = navegador.find(soportado);
  return n ? { codigo: n, motivo: 'navegador' } : { codigo: 'en', motivo: 'navegador' };
}

/* ---------------------------------------------------------------------------
   Selector (botón de la barra superior + panel con banderas)
   --------------------------------------------------------------------------- */
let boton = null;
let panel = null;
function crearSelector() {
  boton = document.querySelector('.topbar__lang');
  if (!boton) return;
  boton.removeAttribute('aria-disabled');
  boton.classList.add('idioma-boton');
  boton.setAttribute('aria-haspopup', 'true');
  boton.setAttribute('aria-expanded', 'false');
  boton.setAttribute('aria-controls', 'idiomas-panel');
  boton.setAttribute('translate', 'no');
  panel = document.createElement('div');
  panel.className = 'idiomas';
  panel.id = 'idiomas-panel';
  panel.hidden = true;
  panel.setAttribute('translate', 'no');
  (boton.closest('li') ?? boton.parentElement).append(panel);
  boton.addEventListener('click', () => (panel.hidden ? abrirPanel() : cerrarPanel()));
  document.addEventListener('click', (e) => { if (!panel.hidden && !panel.contains(e.target) && !boton.contains(e.target)) cerrarPanel(false); });
  document.addEventListener('keydown', (e) => { if (e.key === 'Escape' && !panel.hidden) cerrarPanel(); });
  panel.addEventListener('keydown', (e) => {
    if (!['ArrowDown', 'ArrowUp', 'ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(e.key)) return;
    const ops = [...panel.querySelectorAll('.idiomas__op')];
    const i = ops.indexOf(document.activeElement);
    if (i < 0) return;
    e.preventDefault();
    const sig = { ArrowDown: i + 2, ArrowUp: i - 2, ArrowRight: i + 1, ArrowLeft: i - 1, Home: 0, End: ops.length - 1 }[e.key];
    ops[(sig + ops.length) % ops.length]?.focus();
  });
}
function pintarBoton() {
  if (!boton) return;
  boton.innerHTML = `${bandera(actual)}<span class="idioma-boton__codigo">${actual.toUpperCase()}</span><span aria-hidden="true">⌄</span>`;
  boton.setAttribute('aria-label', t('Idioma: {idioma}', { idioma: POR_CODIGO.get(actual).nombre }));
  boton.title = t('Elige idioma');
}
function pintarPanel() {
  if (!panel) return;
  panel.setAttribute('role', 'dialog');
  panel.setAttribute('aria-label', t('Elige idioma'));
  panel.innerHTML = '';
  const titulo = document.createElement('p');
  titulo.className = 'idiomas__titulo';
  titulo.textContent = t('Elige idioma');
  const lista = document.createElement('ul');
  lista.className = 'idiomas__lista';
  for (const i of IDIOMAS) {
    const li = document.createElement('li');
    const b = document.createElement('button');
    b.type = 'button';
    b.className = 'idiomas__op';
    b.lang = i.codigo;
    if (i.dir) b.dir = i.dir;
    if (i.codigo === actual) b.setAttribute('aria-current', 'true');
    b.innerHTML = `${bandera(i.codigo)}<span class="idiomas__nombre"></span>`;
    b.querySelector('.idiomas__nombre').textContent = i.nombre;
    b.addEventListener('click', () => elegir(i.codigo));
    li.append(b);
    lista.append(li);
  }
  const nota = document.createElement('p');
  nota.className = 'idiomas__nota';
  nota.textContent = t('Noticias, documentos, rankings y resultados se publican en español.');
  panel.append(titulo, lista, nota);
}
function abrirPanel() {
  panel.hidden = false;
  boton.setAttribute('aria-expanded', 'true');
  (panel.querySelector('[aria-current="true"]') ?? panel.querySelector('.idiomas__op'))?.focus();
}
function cerrarPanel(devolverFoco = true) {
  if (!panel || panel.hidden) return;
  panel.hidden = true;
  boton.setAttribute('aria-expanded', 'false');
  if (devolverFoco) boton.focus();
}
async function elegir(codigo) {
  guardar(CLAVE, codigo);          // la elección manual manda en todas las páginas
  quitarAviso();
  cerrarPanel();
  await aplicar(codigo);
  boton?.focus();
}

/* ---------------------------------------------------------------------------
   Aviso cuando el idioma se ha elegido automáticamente
   --------------------------------------------------------------------------- */
let aviso = null;
function quitarAviso() { aviso?.remove(); aviso = null; }
function mostrarAviso({ motivo, pais }) {
  if (actual === 'es' || !['pais', 'navegador'].includes(motivo)) return;
  if (leer(CLAVE_AVISO) === `${actual}|${pais ?? ''}`) return;   // ya lo vio
  const idioma = nombreIdioma(actual);
  aviso = document.createElement('div');
  aviso.className = 'idioma-aviso';
  aviso.setAttribute('role', 'region');
  aviso.setAttribute('aria-label', t('Idioma'));
  aviso.setAttribute('translate', 'no');
  const texto = document.createElement('p');
  texto.className = 'idioma-aviso__texto';
  texto.innerHTML = bandera(actual);
  texto.append(motivo === 'pais'
    ? t('Te mostramos la web en {idioma} porque te conectas desde {pais}.', { idioma, pais: nombrePais(pais) })
    : t('Te mostramos la web en {idioma}, el idioma de tu navegador.', { idioma }));
  const acciones = document.createElement('div');
  acciones.className = 'idioma-aviso__acciones';
  const boton1 = (txt, clase, fn, lang) => {
    const b = document.createElement('button');
    b.type = 'button';
    b.className = `idioma-aviso__btn ${clase}`;
    b.textContent = txt;
    if (lang) b.lang = lang;
    b.addEventListener('click', fn);
    return b;
  };
  acciones.append(
    boton1(t('De acuerdo'), 'idioma-aviso__btn--ok', () => { guardar(CLAVE_AVISO, `${actual}|${pais ?? ''}`); quitarAviso(); }),
    boton1('Ver en español', '', () => elegir('es'), 'es'),
    boton1(t('Otros idiomas'), '', () => { guardar(CLAVE_AVISO, `${actual}|${pais ?? ''}`); quitarAviso(); abrirPanel(); }));
  aviso.append(texto, acciones);
  document.body.prepend(aviso);
}

/* ---------------------------------------------------------------------------
   Arranque y API
   --------------------------------------------------------------------------- */
window.FEP_I18N = Object.freeze({
  IDIOMAS,
  registrar(codigo, diccionario) { DICCIONARIOS[codigo] = Object.freeze({ ...diccionario }); },
  idioma: () => actual,
  cambiar: elegir,
  t,
});

async function iniciar() {
  const css = document.createElement('link');
  css.rel = 'stylesheet';
  css.href = `${base}css/idiomas.css`;
  document.head.append(css);
  crearSelector();
  const d = detectar();
  await aplicar(d.codigo);
  mostrarAviso(d);
}
if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', iniciar);
else iniciar();
})();
