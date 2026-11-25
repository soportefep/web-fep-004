/**
 * Código común a todas las páginas de la web FEP: utilidades, cabecera (buscador, menú lateral),
 * menú principal con submenús (+) y redes sociales del pie.
 * Todo texto que llega de la API se pinta con textContent / atributos, nunca con innerHTML.
 *
 *   Menú principal (enlaces + submenús)  → GET /api/v1/menu
 *   Redes sociales del pie               → GET /api/v1/redes-sociales
 *
 * Orden de carga en cada página (scripts clásicos con defer, funcionan también con file://):
 *   mock/mocks.js → assets/js/api.js → assets/js/comun.js → assets/js/<página>.js
 * Expone window.FEP con las utilidades que usan los scripts de cada página.
 */
(function () {
'use strict';

const { get, comoLista, urlSegura } = window.FEPApi;

const $ = (sel, ctx = document) => ctx.querySelector(sel);
const $$ = (sel, ctx = document) => [...ctx.querySelectorAll(sel)];
const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
const PLACEHOLDER = 'assets/img/placeholder.svg';

/** Crea un elemento. `props`: atributos/propiedades; `hijos`: nodos o strings (como texto). */
function el(tag, props = {}, ...hijos) {
  const n = document.createElement(tag);
  for (const [k, v] of Object.entries(props)) {
    if (v == null || v === false) continue;
    if (k === 'class') n.className = v;
    else if (k === 'text') n.textContent = v;
    else if (k === 'dataset') Object.assign(n.dataset, v);
    else if (k.startsWith('on')) n.addEventListener(k.slice(2), v);
    else n.setAttribute(k, v === true ? '' : String(v));
  }
  for (const h of hijos.flat()) {
    if (h == null || h === false) continue;
    n.append(h instanceof Node ? h : document.createTextNode(String(h)));
  }
  return n;
}

/**
 * Páginas de la web actual que ya están migradas: los enlaces que llegan de la BD (menú, banners…)
 * apuntando a ellas se redirigen a la página nueva. Clave: ruta de la web actual en minúsculas,
 * sin "/" inicial ni ".asp". Añadir aquí cada página según se migre.
 */
const PAGINAS_MIGRADAS = {
  'organos_de_gobierno': 'organos-de-gobierno.html',
  'organos_gobierno': 'organos-de-gobierno.html',
  'equipo_fep': 'equipo-fep.html',
  'ley_de_transparencia': 'ley-de-transparencia.html',
  'transparencia': 'ley-de-transparencia.html',   // enlace de la BD que en la web actual da 404
  'federaciones': 'federaciones.html',
  'federaciones_territoriales': 'federaciones.html', // web.config: ^Federaciones → Federaciones.asp
  'clubes': 'clubes.html',
  'home': 'index.html',
  'microsite': 'micrositio.html',                 // Microsite.asp?Id=7&IdOpcion=19 → micrositio.html?id=7&opcion=19
  'datos_federacion': 'federacion.html',          // Datos_Federacion.asp?Id=6 → federacion.html?id=6 (y /Federacion/6/andalucia)
  'campeonatos': 'campeonatos.html',              // web.config: ^Campeonatos → Campeonatos.asp (formulario POST: ano, modalidad, tipo)
  'programa_impulso': 'programa-impulso.html',    // web.config: ^Programa_Impulso → Impulso.asp (formulario POST: ano)
  'impulso': 'programa-impulso.html',
  // Fichas de torneo: sin mock ni API, cada página muestra un aviso con enlace a la misma ficha en la web actual.
  'camp_equipos': 'campeonato-equipos.html',      // Camp_Equipos.asp?IdTorneo=28676 → campeonato-equipos.html?id=28676
  'camp_silla': 'campeonato-silla.html',          // Camp_Silla.asp?IdTorneo=30180 → campeonato-silla.html?id=30180 (banners, eventos)
  // Camp_Parejas.asp redirigía según el torneo: silla → Camp_Silla.asp (campeonatos.js lo resuelve antes),
  // veteranos → Camp_Parejas_Veteranos.asp; el resto lo pintaba él. Las dos van a la misma ficha nueva.
  'camp_parejas': 'campeonato-parejas.html',      // Camp_Parejas.asp?IdTorneo=28714 → campeonato-parejas.html?id=28714
  'camp_parejas_veteranos': 'campeonato-parejas.html',
  // Cuadros: Previo.asp / PrevioEquipos.asp y las páginas a las que redirigían (Cuadro32.asp, CuadroEquipos16.asp,
  // CuadroPrevia16-4.asp, Liguilla4.asp, EquiposEnfrentamientos.asp…; ver RUTA_CUADRO) → cuadro.html
  'previo': 'cuadro.html',
  'previoequipos': 'cuadro.html',
  'calendario_menores_ffaa': 'calendario-menores-ffaa.html', // Calendario_Menores_FFAA(.asp); ?ano/?fed del ASP no se usaban
  'calendario_veteranos_ffaa': 'calendario-veteranos-ffaa.html', // Calendario_Veteranos_FFAA(.asp); ídem
  'calendarios': 'calendariofep.html',            // Pestaña "Calendarios" del menú: por defecto, el calendario FEP
  'calendariofep': 'calendariofep.html',          // web.config: ^CalendarioFEP → CalendarioFEP.asp (formulario POST: ano)
  'calendarioft': 'calendarioft.html',            // web.config: ^CalendarioFT → CalendarioFT.asp (formulario POST: fed, ano)
  'organizacion_torneos': 'organizacion-torneos.html', // /Organizacion_Torneos(.asp) (menú "Organizar un torneo", banner del calendario); sin parámetros
  'organizacion_fueraplazo': 'organizacion-torneos.html', // Organizacion_FueraPlazo.asp: la página nueva muestra la convocatoria cerrada
  'rankings': 'rankings.html',                    // web.config: /Rankings → Rankings.asp (índice; enlace "Rankings" del menú de la BD)
  'ranking_absoluto': 'ranking-absoluto.html',    // Ranking_Absoluto(.asp) (enlace de SFEP_Rankings_Web); la página no recibía parámetros
  'ranking_menores': 'ranking-menores.html',      // Ranking_Menores(.asp) (enlace de SFEP_Rankings_Web); la página no recibía parámetros
  'ranking_equipos_absolutos': 'ranking-equipos-absolutos.html', // Ranking_Equipos_Absolutos(.asp) (enlace de SFEP_Rankings_Web); sin parámetros
  'ranking_silla_ruedas': 'ranking-silla-ruedas.html', // Ranking_Silla_Ruedas(.asp) (enlace de SFEP_Rankings_Web); la página no recibía parámetros
  'ranking_veteranos': 'ranking-veteranos.html',  // Ranking_Veteranos(.asp) (enlace de SFEP_Rankings_Web); la página no recibía parámetros
  'estadisticas': 'estadisticas.html',            // web.config: ^Estadisticas → Estadisticas.asp (?id={licencia} → ?licencia=; el DNI no se traslada)
  // Buscar licencia y Consulta tu licencia van juntas en buscar-licencia.html (cada una en su sección del índice).
  'licencias_buscar': 'buscar-licencia.html',     // /Licencias_Buscar(.asp) (menú Licencias); formulario POST, sin query que trasladar
  'consulta_tu_licencia': 'buscar-licencia.html#consulta', // web.config: ^Consulta_Tu_Licencia → Licencias_TuLicencia.asp (acceso con clave: sigue en la web actual)
  'licencias_tulicencia': 'buscar-licencia.html#consulta',
};

/** URL limpia de la ficha de federación (web.config: ^Federacion/([0-9]+)/([_0-9a-z-]+) → Datos_Federacion.asp). */
const RUTA_FICHA_FEDERACION = /^federacion\/(\d{1,2})\/[_0-9a-z-]+\/?$/;
/** Páginas de cuadros de la web actual (todas leen lo mismo; las exportaciones *Excell siguen allí). */
const RUTA_CUADRO = /^(?:previo(?:equipos)?|cuadro(?:equipos|previa|preprevia)?\d+(?:-\d+)?|liguilla\d(?:equipos)?|equiposenfrentamientos)$/;
const ID_FEDERACION = /^(0|[1-9]\d?)$/;

/** Micrositios de acceso restringido (asambleas): siguen en la web actual (Microsite.asp los protegía con cookie). */
const MICROSITIOS_PRIVADOS = new Set(['25', '26']);
const ID_POSITIVO = /^[1-9]\d{0,9}$/;

/**
 * Páginas migradas que reciben parámetros: cómo se trasladan (nombre antiguo en minúsculas → nombre
 * nuevo; ASP no distingue mayúsculas en Request("Id")) y, opcionalmente, si el enlace se puede migrar.
 * Si `valida` devuelve false, el enlace se queda en la web actual. `pagina` (opcional) elige otra página
 * migrada según los parámetros ya traducidos (puede quitar los que no use). Las páginas sin entrada aquí
 * descartan la query (como hasta ahora).
 */
const QUERY_MIGRADA = {
  'microsite': {
    parametros: { id: 'id', idopcion: 'opcion' },
    valida: (q) => ID_POSITIVO.test(q.get('id') ?? '') && !MICROSITIOS_PRIVADOS.has(q.get('id'))
      && (!q.has('opcion') || ID_POSITIVO.test(q.get('opcion'))),
  },
  'datos_federacion': {
    parametros: { id: 'id' },
    valida: (q) => ID_FEDERACION.test(q.get('id') ?? ''),
  },
  // campeonatos.js entiende los nombres antiguos (ano, modalidad = tipo de torneo, tipo = categoría).
  'campeonatos': {
    parametros: { ano: 'anio', anio: 'anio', modalidad: 'tipo', tipo: 'categoria', categoria: 'categoria' },
  },
  // programa-impulso.js entiende ano (formulario antiguo) y anio.
  'programa_impulso': { parametros: { ano: 'anio', anio: 'anio' } },
  'impulso': { parametros: { ano: 'anio', anio: 'anio' } },
  // calendario-fep.js entiende ano (formulario antiguo) y anio. Con fed distinto de 0 el ASP mostraba el
  // calendario de una federación territorial: va a calendarioft.html (CalendarioFT.asp, migrado).
  'calendariofep': {
    parametros: { ano: 'anio', anio: 'anio', fed: 'federacion' },
    valida: (q) => !q.has('federacion') || ID_FEDERACION.test(q.get('federacion')),
    pagina: (q) => paginaCalendario(q),
  },
  // calendario-ft.js entiende fed/ano (formulario antiguo) y federacion/anio. fed=1000 era "ninguna" (se pide
  // elegir) y fed=0 el calendario FEP (calendariofep.html).
  'calendarioft': {
    parametros: { fed: 'federacion', federacion: 'federacion', ano: 'anio', anio: 'anio' },
    valida: (q) => {
      if (q.get('federacion') === '1000') q.delete('federacion');
      return !q.has('federacion') || ID_FEDERACION.test(q.get('federacion'));
    },
    pagina: (q) => paginaCalendario(q, { sinFederacion: 'calendarioft.html' }),
  },
  // Cuadros: sin IdTorneo válido el enlace se queda en la web actual. cuadro.html entiende además avance/posicion
  // (abre el detalle de una eliminatoria por equipos, como EquiposEnfrentamientos.asp).
  'previo': {
    parametros: { idtorneo: 'torneo', genero: 'genero', cat: 'categoria', nivel: 'nivel', cuadro: 'cuadro', avance: 'avance', posicion: 'posicion' },
    valida: (q) => ID_POSITIVO.test(q.get('torneo') ?? ''),
  },
  // Ficha de torneo de pádel en silla de ruedas: sin IdTorneo válido el enlace se queda en la web actual.
  'camp_silla': {
    parametros: { idtorneo: 'id', id: 'id' },
    valida: (q) => ID_POSITIVO.test(q.get('id') ?? ''),
  },
  // Fichas de torneos por equipos y por parejas: ídem.
  'camp_equipos': { parametros: { idtorneo: 'id', id: 'id' }, valida: (q) => ID_POSITIVO.test(q.get('id') ?? '') },
  'camp_parejas': { parametros: { idtorneo: 'id', id: 'id' }, valida: (q) => ID_POSITIVO.test(q.get('id') ?? '') },
  'camp_parejas_veteranos': { parametros: { idtorneo: 'id', id: 'id' }, valida: (q) => ID_POSITIVO.test(q.get('id') ?? '') },
  // Estadísticas de jugador/a: Estadisticas?id={licencia} (rankings, BD) → estadisticas.html?licencia=. Sin licencia,
  // la página muestra el buscador; una licencia que no es un número se queda en la web actual.
  'estadisticas': {
    parametros: { id: 'licencia', licencia: 'licencia' },
    valida: (q) => !q.has('licencia') || /^[1-9]\d{0,8}$/.test(q.get('licencia')),
  },
};

/**
 * CalendarioFEP.asp y CalendarioFT.asp eran el mismo calendario con distinta federación (fed): 0 = FEP
 * (calendariofep.html, sin el parámetro), 1–22 = federación territorial (calendarioft.html?federacion=).
 * `sinFederacion`: página cuando el enlace no trae federación (por defecto, el calendario FEP).
 */
function paginaCalendario(q, { sinFederacion = PAGINAS_MIGRADAS.calendariofep } = {}) {
  const fed = q.get('federacion');
  if (fed === null) return sinFederacion;
  if (Number(fed) === 0) {
    q.delete('federacion');
    return PAGINAS_MIGRADAS.calendariofep;
  }
  return PAGINAS_MIGRADAS.calendarioft;
}

function paginaMigrada(href) {
  try {
    const url = new URL(href);
    if (!/(^|\.)padelfederacion\.es$/i.test(url.hostname)) return null;
    let clave = url.pathname.replace(/^\/+/, '').replace(/\.asp$/i, '').toLowerCase();
    if (RUTA_CUADRO.test(clave)) clave = 'previo';
    const ficha = RUTA_FICHA_FEDERACION.exec(clave);
    if (ficha) return `${PAGINAS_MIGRADAS.datos_federacion}?id=${Number(ficha[1])}${url.hash}`;
    const destino = PAGINAS_MIGRADAS[clave];
    if (!destino) return null;
    const conQuery = QUERY_MIGRADA[clave];
    if (!conQuery) return destino + url.hash;

    const antiguos = new Map([...url.searchParams].map(([k, v]) => [k.toLowerCase(), v.trim()]));
    const nuevos = new URLSearchParams();
    for (const [antiguo, nuevo] of Object.entries(conQuery.parametros)) {
      const valor = antiguos.get(antiguo);
      if (valor) nuevos.set(nuevo, valor);
    }
    if (conQuery.valida && !conQuery.valida(nuevos)) return null;
    const pagina = conQuery.pagina?.(nuevos) ?? destino;
    const q = nuevos.toString();
    return `${pagina}${q ? `?${q}` : ''}${url.hash}`;
  } catch {
    return null;
  }
}

/** Atributos de un enlace a partir de enlace/ventana de la BD. */
function attrsEnlace(enlace, ventana) {
  const href = urlSegura(enlace);
  const migrada = href ? paginaMigrada(href) : null;
  if (migrada) return { href: migrada, target: null, rel: null };
  const nueva = typeof ventana === 'string' && ventana.trim().toLowerCase() === '_blank';
  return { href: href ?? '#', target: nueva ? '_blank' : null, rel: nueva ? 'noopener' : null };
}

/** Imagen con respaldo local si la URL remota falla. */
function img(src, alt, props = {}) {
  const i = el('img', { src: src || PLACEHOLDER, alt: alt ?? '', loading: 'lazy', decoding: 'async', ...props });
  i.addEventListener('error', () => { if (!i.src.endsWith(PLACEHOLDER)) i.src = PLACEHOLDER; }, { once: true });
  return i;
}

/** Texto plano a partir del HTML heredado de la BD (DOMParser no ejecuta scripts). */
function textoPlano(html, max = 160) {
  if (!html) return '';
  const doc = new DOMParser().parseFromString(String(html), 'text/html');
  const t = (doc.body.textContent ?? '').replace(/\s+/g, ' ').trim();
  return t.length > max ? `${t.slice(0, max - 1).trimEnd()}…` : t;
}

const normaliza = (s) => String(s ?? '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim();

/* ---------------------------------------------------------------------------
   Fechas
   --------------------------------------------------------------------------- */
const MESES = ['ENE', 'FEB', 'MAR', 'ABR', 'MAY', 'JUN', 'JUL', 'AGO', 'SEP', 'OCT', 'NOV', 'DIC'];

/** "2026-11-06" o "2026-11-06T10:00:00" → Date local (sin desfase de zona horaria). */
function aFecha(valor) {
  if (!valor) return null;
  const m = /^(\d{4})-(\d{2})-(\d{2})(?:T(\d{2}):(\d{2}))?/.exec(String(valor));
  if (!m) return null;
  return new Date(+m[1], +m[2] - 1, +m[3], +(m[4] ?? 0), +(m[5] ?? 0));
}
const dd = (d) => String(d.getDate()).padStart(2, '0');

/** Bloque de fecha grande del Figma: { dias: "06–08", mes: "NOV 2026", iso } */
function bloqueFecha(ini, fin) {
  const a = aFecha(ini);
  if (!a) return null;
  const b = aFecha(fin) ?? a;
  const mismoDia = a.toDateString() === b.toDateString();
  const mismoMes = a.getMonth() === b.getMonth() && a.getFullYear() === b.getFullYear();
  const dias = mismoDia ? dd(a) : `${dd(a)}–${dd(b)}`;
  const mes = mismoMes ? `${MESES[a.getMonth()]} ${a.getFullYear()}`
    : a.getFullYear() === b.getFullYear() ? `${MESES[a.getMonth()]}–${MESES[b.getMonth()]} ${b.getFullYear()}`
      : `${MESES[a.getMonth()]} ${a.getFullYear()}–${MESES[b.getMonth()]} ${b.getFullYear()}`;
  return { dias, mes, iso: String(ini).slice(0, 10) };
}

const fmtLargo = new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'long', year: 'numeric' });
const fmtCorto = new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'long' });

/* ===========================================================================
   Cabecera: sombra al hacer scroll, buscador y menú lateral (móvil)
   =========================================================================== */
function initCabecera() {
  const header = $('#cabecera');
  if (!header) return;
  const onScroll = () => header.classList.toggle('is-scrolled', window.scrollY > 8);
  window.addEventListener('scroll', onScroll, { passive: true });
  onScroll();

  // Páginas con la cabecera de la portada nueva (body.ua): sin buscador desplegable ni menú lateral
  if (!$('#buscar-abrir') || !$('#menu-principal')) return;

  // Buscador
  const btnBuscar = $('#buscar-abrir');
  const buscador = $('#buscador');
  btnBuscar.addEventListener('click', () => {
    const abrir = buscador.hidden;
    buscador.hidden = !abrir;
    btnBuscar.setAttribute('aria-expanded', String(abrir));
    if (abrir) $('#texto-busqueda').focus();
  });

  // Menú lateral
  const panel = $('#menu-principal');
  const abrirBtn = $('#menu-abrir');
  const cerrarBtn = $('#menu-cerrar');
  const fondo = $('#menu-fondo');
  const abrir = () => {
    panel.classList.add('is-open');
    fondo.hidden = false;
    abrirBtn.setAttribute('aria-expanded', 'true');
    document.body.style.overflow = 'hidden';
    document.body.classList.add('menu-abierto');
    cerrarBtn.focus();
  };
  const cerrar = (devolverFoco = true) => {
    if (!panel.classList.contains('is-open')) return;
    panel.classList.remove('is-open');
    fondo.hidden = true;
    abrirBtn.setAttribute('aria-expanded', 'false');
    document.body.style.overflow = '';
    document.body.classList.remove('menu-abierto');
    if (devolverFoco) abrirBtn.focus();
  };
  abrirBtn.addEventListener('click', abrir);
  cerrarBtn.addEventListener('click', () => cerrar());
  fondo.addEventListener('click', () => cerrar());
  document.addEventListener('keydown', (e) => {
    if (e.key !== 'Escape') return;
    // Con un submenú abierto, Esc lo cierra y devuelve el foco a su botón (+).
    const abierto = $('#nav-principal .nav__item.is-open');
    if (abierto) {
      cerrarSubmenus();
      if (abierto.contains(document.activeElement)) $('.nav__toggle', abierto).focus();
      return;
    }
    cerrar();
    if (!buscador.hidden) { buscador.hidden = true; btnBuscar.setAttribute('aria-expanded', 'false'); btnBuscar.focus(); }
  });
  // Trampa de foco dentro del panel abierto
  panel.addEventListener('keydown', (e) => {
    if (e.key !== 'Tab' || !panel.classList.contains('is-open')) return;
    const focos = $$('a[href], button:not([disabled])', panel).filter((n) => n.offsetParent !== null);
    if (!focos.length) return;
    const [primero, ultimo] = [focos[0], focos[focos.length - 1]];
    if (e.shiftKey && document.activeElement === primero) { e.preventDefault(); ultimo.focus(); }
    else if (!e.shiftKey && document.activeElement === ultimo) { e.preventDefault(); primero.focus(); }
  });
  window.matchMedia('(min-width: 1280px)').addEventListener('change', (m) => { cerrarSubmenus(); if (m.matches) cerrar(false); });
}

/* ===========================================================================
   Menú principal. Arquitectura nueva (análisis de prioridades del usuario, nov. 2026):
   5 secciones ordenadas por frecuencia de uso, en lugar de las 8 del menú antiguo de la BD
   (/api/v1/menu ya no define la estructura). Calendario y Rankings van primero dentro de
   Competición y, en móvil, como accesos directos en la cabecera del panel.
   Los enlaces son rutas de la web actual: attrsEnlace() los lleva a la página nueva si ya
   está migrada (PAGINAS_MIGRADAS) y, si no, a la web actual.
   =========================================================================== */
const MENU_FEP = [
  {
    clave: 'competicion', texto: 'Competición', enlace: 'CalendarioFEP',
    grupos: [
      // Una sola pestaña "Calendarios": abre el calendario FEP y, desde su submenú (.subnav), los
      // territoriales y los de menores y veteranos de las FFAA. `paginas`: también marcan Competición como activa.
      { titulo: 'Calendarios', enlaces: [
        ['Calendarios', 'Calendarios'],
      ], paginas: ['calendarioft.html', 'calendario-menores-ffaa.html', 'calendario-veteranos-ffaa.html'] },
      // Igual con "Rankings": abre el ranking absoluto y, desde su submenú (.subnav), el resto. El índice
      // (rankings.html) sigue siendo el destino de los enlaces antiguos a /Rankings.
      { titulo: 'Rankings', enlaces: [
        ['Rankings', 'Ranking_Absoluto'],
        // Estadísticas de jugador/a: se llega desde los rankings (enlace de cada jugador), así que vive con ellos.
        ['Estadísticas de jugador/a', 'Estadisticas'],
      ], paginas: ['rankings.html', 'ranking-equipos-absolutos.html', 'ranking-menores.html', 'ranking-veteranos.html', 'ranking-silla-ruedas.html'] },
      // Campeonatos: solo los Campeonatos de España. El resto se reubica: Estadísticas → Rankings,
      // Retransmisiones → Actualidad, Programa Impulso y Pádel adaptado → La Federación · Programas.
      { titulo: 'Campeonatos', enlaces: [
        ['Campeonatos de España', 'Campeonatos'],
      ], paginas: ['campeonato-silla.html', 'campeonato-equipos.html', 'cuadro.html'] },
      { titulo: 'Para organizadores', enlaces: [
        ['Organizar un torneo', 'Organizacion_Torneos'],
        ['Solicitar arbitraje', 'Solicitud_Arbitraje'],
        ['Oferta hotelera', 'hoteles_Concertados'],
        ['Árbitros y categorías', 'Registro_Arbitros_Categorias'],
      ] },
    ],
  },
  {
    clave: 'licencias', texto: 'Licencias', enlace: 'Licencias_Buscar',
    // "Tu licencia / Modificación" se consulta desde la página de buscar licencia (sección #consulta, que de momento
    // enlaza al acceso con clave de la web actual) hasta que exista "Mi Portal" (ACCESOS_PERFIL); "Solicitar
    // certificados" irá dentro de Mi Portal, donde cada persona federada (jugador/a, árbitro, técnico/a…) entra a su perfil.
    grupos: [
      { enlaces: [
        // "Buscar licencia" y "Tu licencia / Modificación" (Consulta_Tu_Licencia) unidas en una sola página (DECISIÓN FEP: texto).
        ['Buscar / consulta tu licencia', 'Licencias_Buscar'],
        ['Portal de atención al federado', 'Portal-del-Federado'],
        ['Registro Nacional de Árbitros', 'Registro_Nacional_Arbitros'],
        ['Registro Nacional de Técnicos', 'Registro_Nacional_Tecnicos'],
      ] },
    ],
  },
  {
    clave: 'formacion', texto: 'Formación', enlace: 'Formacion_Calendario',
    grupos: [
      { enlaces: [
        ['Calendario de cursos', 'Formacion_Calendario'],
        ['Inscripciones', 'Formacion_Inscripciones'],
        ['Cursos de formación', 'Acciones_Formativas'],
        ['Campus online', 'http://www.formacionfep.es/campus/', '_blank'],
        ['Exámenes', 'Acceso-Examenes'],
        ['Vídeos de formación', 'Acceso-VideosFormacion'],
        ['Solicitud de prácticas', 'Solicitud_Practicas'],
      ] },
    ],
  },
  {
    clave: 'actualidad', texto: 'Actualidad', enlace: 'Noticias',
    grupos: [
      { enlaces: [
        ['Noticias', 'Noticias'],
        ['Multimedia', 'Archivos_Multimedia'],
        ['Retransmisiones', 'Retransmisiones'],
        ['Mujer y pádel', 'Post_Mujer_y_Padel'],
        ['Mujer y pádel · Multimedia', 'mUJERyDeporte'],
      ] },
    ],
  },
  {
    clave: 'federacion', texto: 'La Federación', enlace: 'Organos_De_Gobierno',
    grupos: [
      { titulo: 'Institucional', enlaces: [
        ['Órganos de gobierno', 'Organos_De_Gobierno'],
        ['Equipo FEP', 'Equipo_FEP'],
        ['Federaciones autonómicas', 'Federaciones'],
        ['Clubes', 'Clubes'],
        ['Homologaciones', 'microsite.asp?id=7'],
        ['Transparencia', 'Ley_De_Transparencia'],
      ] },
      { titulo: 'Normativa', enlaces: [
        ['Reglamentos y normativas', 'Reglamentos'],
        ['Circulares', 'Circulares'],
        ['Estatutos', 'Estatutos'],
        ['Memorias deportivas', 'Memorias_Deportivas'],
        ['Dopaje', 'Dopaje'],
        ['Mantenimiento de pistas', 'Mantenimiento_Pistas'],
      ] },
      // Programas de la FEP (antes en Competición · Campeonatos)
      { titulo: 'Programas', enlaces: [
        ['Programa Impulso', 'Programa_Impulso'],
        ['Pádel adaptado', 'Padel-Adaptado'],
      ] },
    ],
  },
];

/** Accesos directos del panel móvil: las tres tareas más frecuentes. */
const ACCESOS_MOVIL = [
  ['Calendarios', 'Calendarios'],
  ['Rankings', 'Ranking_Absoluto'],
  ['Campeonatos', 'Campeonatos'], // "Tu licencia" pasa a Mi Portal
];

/** Enlaces secundarios que en escritorio están en la barra superior: en móvil, al pie del panel. */
const EXTRA_MOVIL = [
  ['Contacto', 'Contacto'],
  ['Extranet', 'ControlAcceso.asp'],
];

/** Ruta de la web actual ("Rankings", "microsite.asp?id=7"…) → atributos del enlace (página nueva si ya está migrada). */
const enlaceMenu = (ruta, ventana) => attrsEnlace(ruta, ventana);

/** Página actual (sin carpeta ni query) para marcar la sección activa. */
const paginaActual = () => (location.pathname.split('/').pop() || 'index.html').toLowerCase();

function pintarMenu() {
  const ul = $('#nav-principal');
  if (!ul) return;
  const actual = paginaActual();

  ul.replaceChildren(...MENU_FEP.map((sec) => {
    const attrs = enlaceMenu(sec.enlace);
    const a = el('a', { class: 'nav__link', ...attrs }, sec.texto);
    const li = el('li', { class: 'nav__item', dataset: { menuKey: sec.clave } }, a);
    const hrefs = sec.grupos.flatMap((g) => g.enlaces.map(([, r, v]) => enlaceMenu(r, v).href));
    hrefs.push(...sec.grupos.flatMap((g) => g.paginas ?? []));
    if (hrefs.some((h) => h.split(/[?#]/)[0].toLowerCase() === actual)) {
      li.classList.add('is-current');
      a.setAttribute('aria-current', 'true');
    }
    anadirSubmenu(li, a, sec.grupos);
    return li;
  }));

  // Panel móvil: accesos directos arriba y enlaces secundarios abajo.
  const nav = ul.closest('nav');
  if (nav && !$('.nav-panel__quick')) {
    nav.before(el('ul', { class: 'nav-panel__quick', 'aria-label': 'Accesos directos' },
      ACCESOS_MOVIL.map(([t, r]) => el('li', {}, el('a', enlaceMenu(r), t)))));
  }
  const extra = $('.nav-panel__extra');
  if (extra) extra.replaceChildren(...EXTRA_MOVIL.map(([t, r]) => el('li', {}, el('a', enlaceMenu(r), t))));
}

/* ---------------------------------------------------------------------------
   Submenús: botón (+) junto a cada opción. Al pulsarlo se abre un panel tipo
   "menú burger" (icono ☰ + nombre de la sección + enlaces, agrupados en columnas
   si la sección tiene grupos con título) y el + gira a ✕.
   Solo hay uno abierto a la vez; se cierra al pulsar fuera, con Esc o con el mismo botón.
   En el panel lateral (móvil/tablet) funciona como acordeón con el mismo (+).
   --------------------------------------------------------------------------- */
let subId = 0;

function anadirSubmenu(li, a, grupos) {
  const id = `submenu-${++subId}`;
  const nombre = a.textContent.trim();
  const total = grupos.reduce((n, g) => n + g.enlaces.length, 0);

  const columnas = grupos.map((g) => {
    const lista = el('ul', { class: 'nav__sub-list' });
    for (const [texto, ruta, ventana] of g.enlaces) {
      const attrs = enlaceMenu(ruta, ventana);
      lista.append(el('li', {}, el('a', attrs,
        el('span', { text: texto }),
        el('span', { class: 'nav__sub-arrow', 'aria-hidden': 'true', text: attrs.target ? '↗' : '→' }))));
    }
    return el('div', { class: 'nav__sub-col' },
      g.titulo ? el('p', { class: 'nav__sub-group', text: g.titulo }) : null, lista);
  });
  const panel = el('div', { class: `nav__sub${grupos.length > 1 ? ' nav__sub--mega' : ''}`, id, style: `--cols:${grupos.length}` },
    el('p', { class: 'nav__sub-head', 'aria-hidden': 'true' },
      el('span', { class: 'nav__burger-icon' }, el('span'), el('span'), el('span')),
      el('span', { class: 'nav__sub-title', text: nombre }),
      el('span', { class: 'nav__sub-count', text: `${total} secciones` })),
    el('div', { class: 'nav__sub-cols' }, columnas));

  const toggle = el('button', {
    type: 'button', class: 'nav__toggle', 'aria-expanded': 'false', 'aria-controls': id,
    'aria-label': `Abrir el submenú de ${nombre}`,
  }, el('span', { class: 'nav__plus', 'aria-hidden': 'true' }));
  toggle.addEventListener('click', (e) => {
    e.stopPropagation();
    const abrir = !li.classList.contains('is-open');
    cerrarSubmenus();
    if (abrir) abrirSubmenu(li);
  });

  li.classList.add('has-sub');
  li.append(toggle, panel);
}

function abrirSubmenu(li) {
  const toggle = $('.nav__toggle', li);
  const panel = $('.nav__sub', li);
  li.classList.add('is-open');
  toggle.setAttribute('aria-expanded', 'true');
  toggle.setAttribute('aria-label', toggle.getAttribute('aria-label').replace('Abrir', 'Cerrar'));
  // Escritorio: si el panel se sale por la derecha de la pantalla, se alinea a la derecha.
  panel.classList.remove('nav__sub--right');
  if (panel.getBoundingClientRect().right > document.documentElement.clientWidth - 8) {
    panel.classList.add('nav__sub--right');
  }
}

function cerrarSubmenus(excepto = null) {
  for (const li of $$('#nav-principal .nav__item.is-open')) {
    if (li === excepto) continue;
    li.classList.remove('is-open');
    const toggle = $('.nav__toggle', li);
    toggle.setAttribute('aria-expanded', 'false');
    toggle.setAttribute('aria-label', toggle.getAttribute('aria-label').replace('Cerrar', 'Abrir'));
  }
}

// Clic fuera del menú: cierra el submenú abierto.
document.addEventListener('click', (e) => {
  if (!e.target.closest('#nav-principal .nav__item.has-sub')) cerrarSubmenus();
});

/* ===========================================================================
   Botones "Mi Portal" y "Mi Club" de la cabecera: botones simples, sin desplegable.
     · Mi Portal → personas federadas (jugadores, técnicos y árbitros): un único acceso que
                   lleva a cada uno a su perfil.
     · Mi Club   → clubes.
   La gestión interna va al enlace "Extranet" de la barra superior (ControlAcceso.asp).
   Cuando exista la página de cada acceso, basta con poner su `href` aquí; mientras sea null,
   el botón se muestra como "Próximamente" y no navega.
   --------------------------------------------------------------------------- */
const ACCESOS_PERFIL = {
  'mi-portal': { titulo: 'Mi Portal', desc: 'Jugadores, técnicos y árbitros', href: 'mi-portal.html' },
  'mi-club': { titulo: 'Mi Club', desc: 'Acceso para clubes', href: 'mi-club.html' },
};

function initAreaPrivada() {
  for (const [id, acceso] of Object.entries(ACCESOS_PERFIL)) {
    const boton = document.getElementById(id);
    if (!boton) continue;
    if (acceso.href) {
      boton.href = acceso.href;
      boton.title = `${acceso.titulo}: ${acceso.desc.toLowerCase()}`;
      boton.setAttribute('aria-label', `${acceso.titulo} (${acceso.desc.toLowerCase()})`);
      continue;
    }
    boton.removeAttribute('href');
    boton.setAttribute('role', 'link');
    boton.setAttribute('aria-disabled', 'true');
    boton.setAttribute('aria-label', `${acceso.titulo} (próximamente)`);
    boton.title = `${acceso.titulo} · Próximamente`;
    boton.classList.add('acceso--pronto');
  }
}

/* ===========================================================================
   Redes sociales (pie)
   =========================================================================== */
// Iconos de redes (24×24, relleno currentColor). Se eligen por el nombre o por la clase "fa fa-…" de la BD.
const ICONOS_RED = {
  instagram: 'M7 2h10a5 5 0 0 1 5 5v10a5 5 0 0 1-5 5H7a5 5 0 0 1-5-5V7a5 5 0 0 1 5-5zm0 2a3 3 0 0 0-3 3v10a3 3 0 0 0 3 3h10a3 3 0 0 0 3-3V7a3 3 0 0 0-3-3zm5 3.5a4.5 4.5 0 1 1 0 9 4.5 4.5 0 0 1 0-9zm0 2a2.5 2.5 0 1 0 0 5 2.5 2.5 0 0 0 0-5zM17.3 5.4a1.2 1.2 0 1 1 0 2.4 1.2 1.2 0 0 1 0-2.4z',
  youtube: 'M6 5h12a4 4 0 0 1 4 4v6a4 4 0 0 1-4 4H6a4 4 0 0 1-4-4V9a4 4 0 0 1 4-4zm4 3.8v6.4l5.6-3.2z',
  x: 'M3.5 3h5.1l4.1 5.6L17.6 3h2.6l-6.3 7.3L20.8 21h-5.1l-4.5-6.1L5.9 21H3.3l6.7-7.7z',
  linkedin: 'M4.5 2h15A2.5 2.5 0 0 1 22 4.5v15a2.5 2.5 0 0 1-2.5 2.5h-15A2.5 2.5 0 0 1 2 19.5v-15A2.5 2.5 0 0 1 4.5 2zM6 9.5V18h2.7V9.5zm1.35-4.2a1.55 1.55 0 1 0 0 3.1 1.55 1.55 0 0 0 0-3.1zM10.6 9.5V18h2.7v-4.4c0-1.2.5-2 1.6-2 1 0 1.4.7 1.4 2V18H19v-5c0-2.5-1.3-3.7-3.2-3.7-1.2 0-2 .6-2.5 1.3V9.5z',
  facebook: 'M13.5 22v-8h2.7l.4-3.2h-3.1v-2c0-.9.3-1.6 1.6-1.6h1.7V4.3c-.3 0-1.3-.1-2.5-.1-2.5 0-4.1 1.5-4.1 4.2v2.4H7.4V14h2.8v8z',
  tiktok: 'M14 2h3a4.5 4.5 0 0 0 4 4v3a7.4 7.4 0 0 1-4-1.3V15a6 6 0 1 1-6-6h.5v3.1H11a2.9 2.9 0 1 0 3 2.9z',
};
const ICONO_RED_GENERICO = 'M10.6 13.4a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1.4 1.4 1.4 1.4 1.4-1.4a2 2 0 0 1 2.9 2.9l-3 3a2 2 0 0 1-2.9 0zm2.8-2.8a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1.4-1.4-1.4-1.4-1.4 1.4a2 2 0 0 1-2.9-2.9l3-3a2 2 0 0 1 2.9 0z';

function iconoRed(nombre, icono) {
  const n = normaliza(`${nombre} ${icono ?? ''}`);
  const clave = Object.keys(ICONOS_RED).find((k) => k === 'x'
    ? /(^|[\s-])(x|twitter|x-twitter)(\s|$)/.test(n) : n.includes(k));
  const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
  svg.setAttribute('viewBox', '0 0 24 24');
  svg.setAttribute('aria-hidden', 'true');
  const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
  path.setAttribute('d', clave ? ICONOS_RED[clave] : ICONO_RED_GENERICO);
  path.setAttribute('fill-rule', 'evenodd');
  svg.append(path);
  return svg;
}

/** Redes del pie como botones redondos con icono. Sin datos de la API se usan los enlaces del HTML. */
function pintarRedes(redes) {
  const lista = $('#redes-lista');
  if (!lista) return;
  let validas = (redes ?? [])
    .map((r) => ({ ...r, href: urlSegura(r.url) }))
    .filter((r) => r.href && r.nombre)
    .sort((a, b) => (a.orden ?? 0) - (b.orden ?? 0));
  if (!validas.length) {
    validas = $$('a', lista).map((a) => ({ nombre: a.textContent.replace('↗', '').trim(), href: a.href }));
  }
  if (!validas.length) return;
  lista.replaceChildren(...validas.map((r) => el('li', {},
    el('a', { class: 'footer__social-link', href: r.href, target: '_blank', rel: 'noopener', title: r.nombre },
      iconoRed(r.nombre, r.icono),
      el('span', { class: 'sr-only', text: `${r.nombre} (se abre en una pestaña nueva)` })))));
}

/* ===========================================================================
   Patrocinadores en la columna derecha (portada e interiores), en 3 bloques:
     1. Patrocinadores principales  (barra A: barrapatroa5.jpg) → logos grandes, primeros
     2. Patrocinadores              (barra B: barrapatrob22.jpg)
     3. Productos homologados       (barra C: barrapatroc8.jpg)
   Cada logo es un recorte de su barra (SVG con viewBox): no hacen falta imágenes sueltas y,
   si se cambia una barra, basta con recolocar el recorte (x, y, w, h en % de la imagen).
   SFEP_Patrocinadores (/api/v1/patrocinadores) no distingue niveles, por eso no se usa aquí.
   =========================================================================== */
const BARRAS_PATRO = {
  a: { src: 'assets/img/fep/publicidad/barrapatroa5.jpg', w: 1068, h: 266 },
  b: { src: 'assets/img/fep/publicidad/barrapatrob22.jpg', w: 1068, h: 266 },
  c: { src: 'assets/img/fep/publicidad/barrapatroc8.jpg', w: 1074, h: 98 },
};

const NIVELES_PATRO = [
  {
    clave: 'principales', titulo: 'Patrocinadores principales', barra: 'a',
    logos: [
      { nombre: 'HEAD, pelota oficial', href: 'https://www.head.com/es_ES/shop-padel', r: [5.6, 13.2, 23.4, 32] },
      { nombre: 'Bullpadel, Selección Española', href: 'https://www.bullpadel.com/es/', r: [63.7, 14.3, 32.3, 30.1] },
      { nombre: 'Babolat, Circuito Nacional de Menores', href: 'https://www.babolat.com/es', r: [62.3, 56.4, 33.7, 33.1] },
      { nombre: 'Padel Pro Shop', href: 'https://padelproshop.com/', r: [8, 55.6, 22, 34.6] },
      { nombre: 'Consejo Superior de Deportes', href: 'https://www.csd.gob.es/', r: [35.9, 35.7, 23.4, 33.1], institucional: true },
    ],
  },
  {
    clave: 'secundarios', titulo: 'Patrocinadores', barra: 'b',
    logos: [
      { nombre: 'CUPRA', href: 'https://www.cupra.com/es-es/', r: [4.7, 30, 11.2, 44] },
      { nombre: 'Iberdrola', href: 'https://www.iberdrola.com/compromiso-social/deporte-femenino', r: [51.8, 63.2, 23.9, 26.3] },
      { nombre: 'Rafa Nadal Academy', href: 'https://www.rafanadalacademy.com/', r: [19.9, 14.3, 13.6, 41.4] },
      { nombre: 'Padel Galis', href: 'https://padelgalis.com/', r: [48.9, 14.3, 27.9, 32] },
      { nombre: 'JOMA', href: 'https://www.joma-sport.com/', r: [78.5, 18, 19.9, 24.4] },
      { nombre: 'SIUX', href: 'https://www.siuxpadel.com/', r: [35.9, 19.5, 10.3, 36.8] },
      { nombre: 'Halcón Viajes', href: 'https://www.halconviajes.com/', r: [81.9, 53.4, 12.6, 37.6] },
      { nombre: 'TacticBo', href: 'https://tacticbo.com/', r: [19.2, 71.4, 12.6, 15] },
      { nombre: 'BVMPRS Brand', href: 'https://www.bumpersbrand.com/', r: [33.5, 71.4, 17.3, 15] },
    ],
  },
  {
    clave: 'homologados', titulo: 'Productos homologados', barra: 'c',
    logos: [
      { nombre: 'Realturf', href: 'https://realturf.com/es/', r: [12.4, 22, 14, 62] },
      // FieldTurf: solo imagen, sin enlace (decisión FEP).
      { nombre: 'FieldTurf', href: null, r: [34.5, 22, 19.5, 62] },
      // PENDIENTE: NATURF sin enlace hasta confirmar su web oficial (naturf.com no es suya).
      { nombre: 'NATURF', href: null, r: [70.3, 18, 17.4, 66] },
    ],
  },
];

const SVG_NS = 'http://www.w3.org/2000/svg';
let clipId = 0;

/** Logo recortado de su barra: <svg viewBox> que se ajusta a la caja sin deformarse. */
function logoRecortado(barra, [x, y, w, h], nombre) {
  const caja = [x * barra.w / 100, y * barra.h / 100, w * barra.w / 100, h * barra.h / 100].map((n) => n.toFixed(1));
  const svg = document.createElementNS(SVG_NS, 'svg');
  svg.setAttribute('viewBox', caja.join(' '));
  svg.setAttribute('preserveAspectRatio', 'xMidYMid meet');
  svg.setAttribute('role', 'img');
  svg.setAttribute('aria-label', nombre);
  // Con "meet" el SVG también pinta lo que queda fuera del viewBox en los márgenes:
  // el clipPath deja ver solo el logo, sin los vecinos de la barra.
  const idClip = `patro-clip-${++clipId}`;
  const clip = document.createElementNS(SVG_NS, 'clipPath');
  clip.id = idClip;
  const rect = document.createElementNS(SVG_NS, 'rect');
  ['x', 'y', 'width', 'height'].forEach((k, i) => rect.setAttribute(k, caja[i]));
  clip.append(rect);
  const image = document.createElementNS(SVG_NS, 'image');
  image.setAttribute('href', barra.src);
  image.setAttribute('width', barra.w);
  image.setAttribute('height', barra.h);
  image.setAttribute('clip-path', `url(#${idClip})`);
  svg.append(clip, image);
  return svg;
}

function pintarPatrocinadoresLateral() {
  const aside = $('#patrocinadores-lateral');
  if (!aside) return;
  const bloques = NIVELES_PATRO.map((nivel) => {
    const barra = BARRAS_PATRO[nivel.barra];
    const idTitulo = `patro-${nivel.clave}-titulo`;
    const lista = el('ul', { class: 'patro-nivel__list' }, nivel.logos.map((p) => {
      const logo = logoRecortado(barra, p.r, p.nombre);
      const rel = p.institucional ? 'noopener' : 'noopener sponsored';
      return el('li', {}, p.href
        ? el('a', { class: 'patro-tile', href: p.href, target: '_blank', rel, title: p.nombre },
          logo, el('span', { class: 'sr-only', text: ' (se abre en una pestaña nueva)' }))
        : el('span', { class: 'patro-tile', title: p.nombre }, logo));
    }));
    return el('section', { class: `patro-nivel patro-nivel--${nivel.clave}`, 'aria-labelledby': idTitulo },
      el('h3', { class: 'patro-nivel__title', id: idTitulo, text: nivel.titulo }), lista);
  });
  // Se sustituye la lista antigua (#patrocinadores-lista) por los tres bloques.
  aside.replaceChildren($('#patro-lateral-titulo') ?? el('p', { class: 'page-sponsors__title', id: 'patro-lateral-titulo', text: 'Patrocinadores FEP' }), ...bloques);
  aside.hidden = false;
}

/* ===========================================================================
   Federaciones autonómicas en la barra superior: una bandera por federación territorial
   (17 comunidades + Ceuta y Melilla) que enlaza a su web; si no tiene web, a su ficha
   (federacion.html?id=). Datos: GET /api/v1/federacion/federaciones-autonomicas
   (respaldo: mock/federacion-federaciones-autonomicas.json).
   =========================================================================== */
async function pintarFederaciones() {
  const lista = $('#fed-strip-lista');
  if (!lista) return;
  let datos;
  try { datos = await get('/api/v1/federacion/federaciones-autonomicas'); } catch (e) { console.warn('[FEP] federaciones:', e.message); return; }
  const feds = comoLista(datos?.federaciones ?? datos)
    .filter((f) => !f.nacional && f.id !== 0 && f.imagenUrl)
    .sort((a, b) => String(a.territorio).localeCompare(String(b.territorio), 'es'));
  if (!feds.length) return;
  lista.replaceChildren(...feds.map((f) => {
    const web = urlSegura(f.web);
    const nombre = f.nombre || f.territorio;
    const attrs = web
      ? { href: web, target: '_blank', rel: 'noopener', title: `${nombre} (web oficial)` }
      : { href: `federacion.html?id=${Number(f.id)}`, title: nombre };
    return el('li', {}, el('a', { class: 'fed-strip__flag', ...attrs },
      img(f.imagenUrl, '', { width: 28, height: 20, loading: 'eager' }),
      el('span', { class: 'sr-only', text: web ? `${nombre} (se abre en una pestaña nueva)` : nombre })));
  }));
  $('#fed-strip')?.removeAttribute('hidden');
}

/* ===========================================================================
   Arranque común
   =========================================================================== */
const cargarLista = (ruta) => get(ruta).then(comoLista).catch((e) => { console.warn(`[FEP] ${ruta}:`, e.message); return null; });

function initComun() {
  // PoC con el diseño de la portada nueva (body.ua): cabecera, pie e idiomas de la portada, no los de esta capa
  const ua = document.body.classList.contains('ua');
  if (!ua) cargarIdiomas();
  const anio = $('#anio');
  if (anio) anio.textContent = String(new Date().getFullYear());
  initCabecera();
  initAreaPrivada();
  // "Extranet" (barra superior) → SFEP, la gestión interna nueva (página aislada, prototipo con datos de prueba)
  $$('.topbar__extranet').forEach((a) => { a.href = 'sfep.html'; });
  pintarMenu();
  cargarLista('/api/v1/redes-sociales').then((r) => pintarRedes(r));
  pintarPatrocinadoresLateral();
  pintarFederaciones();
  if (!ua) initVolverArriba();
  cargarAsistente(ua);
}

/**
 * Botón flotante "Volver arriba" (flecha ↑) en todas las páginas: aparece al bajar más de una pantalla
 * y sube al inicio (suave salvo con "reducir movimiento"). Va encima de la burbuja del asistente.
 */
function initVolverArriba() {
  if ($('.volver-arriba')) return;
  const svg = '<svg viewBox="0 0 24 24" width="22" height="22" aria-hidden="true" focusable="false">'
    + '<path d="M12 19V5M5 12l7-7 7 7" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"/></svg>';
  const btn = el('button', { type: 'button', class: 'volver-arriba', 'aria-label': 'Volver arriba', title: 'Volver arriba', hidden: true });
  btn.innerHTML = svg;
  btn.addEventListener('click', () => {
    window.scrollTo({ top: 0, behavior: reduceMotion.matches ? 'auto' : 'smooth' });
    // El foco vuelve al principio de la página (logo) para quien navega con teclado.
    $('.header__logo')?.focus({ preventScroll: true });
  });
  document.body.append(btn);

  let pendiente = false;
  const actualizar = () => {
    pendiente = false;
    const mostrar = window.scrollY > Math.max(400, window.innerHeight * 0.8);
    if (btn.hidden === mostrar) btn.hidden = !mostrar;
  };
  window.addEventListener('scroll', () => {
    if (!pendiente) { pendiente = true; requestAnimationFrame(actualizar); }
  }, { passive: true });
  actualizar();
}

/**
 * Idiomas de la web (assets/js/i18n.js): selector con banderas en la barra superior, idioma automático según el
 * país de conexión o el navegador, y traducción de los textos fijos de la interfaz (diccionarios en assets/i18n/).
 */
function cargarIdiomas() {
  if (window.FEP_I18N || $('script[src*="assets/js/i18n.js"]')) return;
  const comun = document.querySelector('script[src*="assets/js/comun.js"]');
  const base = comun ? new URL('../', comun.src).href : new URL('assets/', document.baseURI).href;
  document.head.append(el('script', { src: `${base}js/i18n.js` }));
}

/** Burbuja del asistente virtual en todas las páginas (assets/js/asistente.js + assets/css/asistente.css). */
function cargarAsistente(ua = false) {
  const comun = document.querySelector('script[src*="assets/js/comun.js"]');
  const base = comun ? new URL('../', comun.src).href : new URL('assets/', document.baseURI).href;
  // En las páginas con el diseño de la portada nueva, FEPi lleva el mismo estilo que en la portada
  document.head.append(el('link', { rel: 'stylesheet', href: `${base}css/${ua ? 'asistente-unapagina' : 'asistente'}.css` }));
  document.body.append(el('script', { src: `${base}js/asistente.js`, defer: true }));
}

window.FEP = Object.freeze({
  $, $$, el, attrsEnlace, img, textoPlano, normaliza, aFecha, bloqueFecha, fmtLargo, fmtCorto,
  reduceMotion, PLACEHOLDER, cargarLista, get, comoLista, urlSegura, MICROSITIOS_PRIVADOS, paginaMigrada,
});

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', initComun);
else initComun();
})();
