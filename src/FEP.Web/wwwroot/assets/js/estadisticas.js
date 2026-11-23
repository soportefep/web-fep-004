/**
 * Página "Estadísticas de jugador/a" (migrada desde Estadisticas.asp?id={licencia} y CargarPruebas.asp, que cargaba por
 * AJAX las pruebas de cada ranking). Requiere assets/js/comun.js (cabecera, menú, pie y utilidades en window.FEP).
 *
 *   URL: estadisticas.html?licencia=12762 (también acepta id, como Estadisticas.asp; comun.js traslada los enlaces
 *        Estadisticas?id= de la BD y de las páginas de ranking)
 *   Estadísticas → GET /api/v1/competicion/jugadores/{licencia}/estadisticas
 *                  (respaldo: mock/competicion-jugadores-{licencia}-estadisticas.json; en modo prototipo solo hay
 *                  mocks de las licencias 3889, 12762 y 310047)
 *
 * Secciones con ancla: #buscar, #ficha (datos y balance), #rankings (con las pruebas de cada uno, plegables), #evolucion
 * (gráfico de barras HTML/CSS que se lee como texto, sin Highcharts, y tabla) y #equipos. La búsqueda por DNI del ASP no
 * se migra: el formulario enlaza a la web actual. Todo texto de la API va por textContent.
 */
(function () {
'use strict';

const { $, $$, el, get, urlSegura, aFecha, fmtLargo, paginaMigrada } = window.FEP;
const { API_BASE, LEGACY_BASE } = window.FEPApi;

const LICENCIA = /^[1-9]\d{0,8}$/;
const SITIO = 'Federación Española de Pádel';
const EQUIPOS_VISIBLES = 10;     // el resto, tras "Ver más"
const PARTICULAS = new Set(['de', 'del', 'la', 'las', 'los', 'y', 'i', 'e', 'da', 'dos', 'van', 'von']);
const PAGINAS_RANKING = {
  absoluto: 'ranking-absoluto.html',
  menores: 'ranking-menores.html',
  veteranos: 'ranking-veteranos.html',
  'silla-ruedas': 'ranking-silla-ruedas.html',
};
const fmtMes = new Intl.DateTimeFormat('es-ES', { month: 'short', year: 'numeric' });
const fmtDiaMes = new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'short' });

let peticion = 0;                // descarta respuestas de búsquedas anteriores
const ENTRADILLA = document.getElementById('est-entradilla')?.textContent ?? '';

/* ---------------------------------------------------------------------------
   Utilidades
   --------------------------------------------------------------------------- */
/** 8863 → "8.863" (Intl con es-ES no agrupa los números de 4 cifras; la web actual sí). */
const num = (n) => (Number.isFinite(n) ? String(Math.trunc(n)).replace(/\B(?=(\d{3})+(?!\d))/g, '.') : '—');
const pct = (v) => (Number.isFinite(v) ? `${v.toFixed(1).replace('.', ',')} %` : '—');
const plural = (n, uno, varios) => `${num(n)} ${n === 1 ? uno : varios}`;
const entero = (v) => (Number.isFinite(v) && v >= 0 ? Math.trunc(v) : 0);

/** "ALVARO MATADOR GARCIA DE VINUESA" → "Alvaro Matador Garcia de Vinuesa" (la ficha guarda los nombres en mayúsculas). */
function nombrePropio(texto) {
  return String(texto ?? '').toLocaleLowerCase('es-ES')
    .replace(/[\p{L}\p{N}ªº]+/gu, (p, i) => (i > 0 && PARTICULAS.has(p) ? p : p.charAt(0).toLocaleUpperCase('es-ES') + p.slice(1)));
}

const iniciales = (nombre) => String(nombre ?? '').split(/\s+/).filter(Boolean).slice(0, 2).map((p) => p.charAt(0)).join('').toUpperCase();

/** ?licencia=12762, ?id=12762 o ?Id=12762 (Request() de ASP no distingue mayúsculas). */
function leerLicencia(search = location.search) {
  const q = new Map([...new URLSearchParams(search)].map(([k, v]) => [k.toLowerCase(), v.trim()]));
  const v = q.get('licencia') ?? q.get('id') ?? '';
  return LICENCIA.test(v) ? Number(v) : null;
}

const urlWebActual = (licencia) => new URL(licencia ? `Estadisticas?id=${licencia}` : 'Estadisticas', LEGACY_BASE).href;
const urlPropia = (licencia) => `estadisticas.html?licencia=${encodeURIComponent(licencia)}`;
const nuevaPestana = () => el('span', { class: 'sr-only', text: ' (se abre en una pestaña nueva)' });

/** Enlace de la API a la web actual: la página nueva si ya está migrada; si no, la web actual. */
function enlace(url) {
  const segura = urlSegura(url);
  return segura ? (paginaMigrada(segura) ?? segura) : null;
}

/** Imagen: absolutas (API real) validadas con urlSegura; relativas (mocks) tal cual. */
function urlImagen(ruta) {
  if (!ruta || typeof ruta !== 'string') return null;
  const limpia = ruta.trim();
  if (/^https?:\/\//i.test(limpia)) return urlSegura(limpia);
  if (/^[a-z][a-z0-9+.-]*:|^\/\//i.test(limpia)) return null;
  return limpia;
}

const fechaLarga = (iso) => { const f = aFecha(iso); return f ? fmtLargo.format(f) : null; };
const mesAnio = (iso) => { const f = aFecha(iso); return f ? fmtMes.format(f).replace('.', '') : '—'; };

/* ---------------------------------------------------------------------------
   Secciones
   --------------------------------------------------------------------------- */
function seccion(clave, n, titulo, lead, meta, ...hijos) {
  const idTitulo = `${clave}-titulo`;
  return el('section', { class: 'organo', id: clave, 'aria-labelledby': idTitulo },
    el('header', { class: 'organo__head' },
      el('p', { class: 'organo__num', 'aria-hidden': 'true', text: String(n).padStart(2, '0') }),
      el('div', {},
        el('h2', { class: 'h2 organo__title', id: idTitulo, text: titulo }),
        lead ? el('p', { class: 'organo__lead', text: lead }) : null,
        meta ? el('p', { class: 'organo__meta', text: meta }) : null)),
    ...hijos);
}

const dato = (etiqueta, ...valor) => el('div', { class: 'fedf-contact__item' }, el('dt', { text: etiqueta }), el('dd', {}, ...valor));

function foto(d) {
  const nombre = nombrePropio(d.nombre);
  const avatar = el('span', { class: 'est-avatar', 'aria-hidden': 'true', text: iniciales(nombre) || '?' });
  const url = urlImagen(d.fotoUrl);
  if (!url) return avatar;
  const imagen = el('img', { class: 'est-foto', src: url, alt: `Foto de ${nombre}`, width: 120, height: 150, loading: 'lazy', decoding: 'async' });
  imagen.addEventListener('error', () => imagen.replaceWith(avatar), { once: true });
  return imagen;
}

function kpiBalance(etiqueta, r, unidad) {
  const total = entero(r?.total ?? (entero(r?.ganados) + entero(r?.perdidos)));
  if (!total) {
    return el('li', { class: 'fedf-kpi est-kpi' },
      el('p', { class: 'fedf-kpi__label', text: etiqueta }),
      el('p', { class: 'fedf-kpi__value', text: '—' }),
      el('p', { class: 'fedf-kpi__detail', text: `Sin ${unidad} registrados en el periodo.` }));
  }
  const ganados = entero(r.ganados);
  const perdidos = entero(r.perdidos);
  const pg = Number.isFinite(r.porcentajeGanados) ? r.porcentajeGanados : (ganados / total) * 100;
  const pp = Number.isFinite(r.porcentajePerdidos) ? r.porcentajePerdidos : 100 - pg;
  return el('li', { class: 'fedf-kpi est-kpi' },
    el('p', { class: 'fedf-kpi__label', text: etiqueta }),
    el('p', { class: 'fedf-kpi__value' }, num(ganados), el('span', { class: 'est-kpi__de', text: ` de ${num(total)}` })),
    el('p', { class: 'fedf-stack est-stack', 'aria-hidden': 'true' },
      el('span', { class: 'fedf-stack__part est-stack__gan', style: `--ancho: ${pg.toFixed(2)}%` }),
      el('span', { class: 'fedf-stack__part est-stack__per', style: `--ancho: ${pp.toFixed(2)}%` })),
    el('p', { class: 'fedf-kpi__detail' },
      el('strong', { text: `${pct(pg)} ganados` }), ` · ${num(perdidos)} perdidos (${pct(pp)})`));
}

function seccionFicha(d, n) {
  const pareja = d.ultimaPareja && LICENCIA.test(String(d.ultimaPareja.licencia))
    ? el('a', { href: urlPropia(d.ultimaPareja.licencia), text: nombrePropio(d.ultimaPareja.nombre) })
    : null;
  const mejor = d.mejorClasificacion;
  const textoMejor = mejor && (mejor.ronda || mejor.categoria)
    ? `${[mejor.ronda, mejor.categoria].filter(Boolean).join(' · ')} (${plural(entero(mejor.veces), 'vez', 'veces')})`
    : null;

  const datos = [
    dato('Número de licencia', String(d.licencia)),
    d.categoria ? dato('Categoría por edad', d.categoria) : null,
    d.clubAutonomico ? dato(`Club competición autonómica${d.anioLicencia ? ` (${d.anioLicencia})` : ''}`, d.clubAutonomico) : null,
    d.clubNacional ? dato(`Club competición nacional${d.anioLicencia ? ` (${d.anioLicencia})` : ''}`, d.clubNacional) : null,
    dato('Última pareja', pareja ?? 'Sin datos en el periodo'),
    dato('Mejor clasificación en torneo', textoMejor ?? 'Sin datos en el periodo'),
  ].filter(Boolean);

  const desde = fechaLarga(d.desde);
  const b = d.balance ?? {};
  return seccion('ficha', n, 'Ficha y balance del último año', null,
    desde ? `Balance, pareja y mejor clasificación: pruebas del Ranking Único disputadas desde el ${desde}.` : 'Balance, pareja y mejor clasificación: pruebas del Ranking Único.',
    el('div', { class: 'est-ficha' },
      el('div', { class: 'est-ficha__foto' }, foto(d)),
      el('dl', { class: 'fedf-contact est-datos' }, datos)),
    el('h3', { class: 'org-group__title est-subtitulo', text: 'Balance' }),
    el('ul', { class: 'fedf-kpis est-kpis' },
      kpiBalance('Partidos ganados', b.partidos, 'partidos'),
      kpiBalance('Sets ganados', b.sets, 'sets'),
      kpiBalance('Juegos ganados', b.juegos, 'juegos')),
    el('p', { class: 'organo__meta est-nota', text: 'Solo se cuentan los partidos con resultado registrado en la FEP: muchos torneos autonómicos e internacionales suman puntos al ranking pero no publican sus resultados, y en ese caso el balance aparece vacío.' }));
}

/* Rankings: tabla resumen y, por ranking, las pruebas del periodo (desglose de CargarPruebas.asp) */
function nombreRanking(r) {
  return r.lista ? `${r.ranking} · ${r.lista}` : (r.categoriaBd && r.ranking === 'Otro ranking' ? `${r.ranking} (${r.categoriaBd})` : r.ranking);
}

function filaRanking(r, licencia) {
  const pagina = PAGINAS_RANKING[r.clave];
  return el('tr', {},
    el('th', { scope: 'row', class: 'est-td--ranking' },
      el('span', { class: 'est-ranking__nombre', text: r.ranking }),
      r.lista ? el('span', { class: 'est-ranking__lista', text: r.lista }) : (r.categoriaBd ? el('span', { class: 'est-ranking__lista', text: r.categoriaBd }) : null),
      pagina ? el('a', { class: 'est-ranking__ver', href: `${pagina}?licencia=${encodeURIComponent(licencia)}` },
        'Ver en el ranking', el('span', { 'aria-hidden': 'true', text: ' →' })) : null),
    el('td', { class: 'est-num est-td--pos', 'data-label': 'Puesto' },
      Number.isInteger(r.posicion) ? el('strong', { class: `rkc-pos${r.posicion <= 3 ? ' rkc-pos--podio' : ''}`, text: `${r.posicion}.º` }) : 'Sin puesto'),
    el('td', { class: 'est-num', 'data-label': 'Torneos jugados', text: num(r.torneosTotales) }),
    el('td', { class: 'est-num', 'data-label': 'Puntos totales', text: num(r.puntosTotales) }),
    el('td', { class: 'est-num', 'data-label': 'Torneos ranking', text: num(r.torneosRanking) }),
    el('td', { class: 'est-num', 'data-label': 'Puntos ranking' }, el('strong', { text: num(r.puntosRanking) })));
}

function bandera(f) {
  if (!f) return el('span', { text: '—' });
  const nombre = f.id === 0 ? 'FEP' : (f.territorio || f.nombre || `Federación ${f.id}`);
  const url = urlImagen(f.banderaUrl);
  const img = url ? el('img', { class: 'rke-bandera', src: url, alt: '', width: 30, height: 20, loading: 'lazy', decoding: 'async' }) : null;
  img?.addEventListener('error', () => { img.hidden = true; }, { once: true });
  return el('span', { class: 'rke-fed est-fed', title: f.nombre || nombre }, img, el('span', { text: nombre }));
}

function filaPrueba(p) {
  const anterior = p.porcentaje !== 100;
  return el('tr', {},
    el('td', { class: 'est-td--fecha', 'data-label': 'Fecha' }, p.fecha ? el('time', { datetime: String(p.fecha).slice(0, 10), text: mesAnio(p.fecha) }) : '—'),
    el('td', { class: 'est-td--fed', 'data-label': 'Federación' }, bandera(p.federacion)),
    el('th', { scope: 'row', class: 'est-td--torneo', text: p.torneo }),
    el('td', { 'data-label': 'Ronda' }, p.ronda ?? '—', p.observaciones ? el('span', { class: 'org-tag est-obs', text: p.observaciones }) : null),
    el('td', { class: 'est-num', 'data-label': 'Puntos' },
      el('strong', { text: num(p.puntosComputados ?? p.puntos) }),
      anterior ? el('span', { class: 'est-pct', text: ` (${p.porcentaje} % de ${num(p.puntos)})` }) : null,
      anterior ? el('span', { class: 'org-tag org-tag--red est-obs', text: 'Categoría anterior' }) : null));
}

function pruebasRanking(r, abierta) {
  const pruebas = Array.isArray(r.pruebas) ? r.pruebas : [];
  const desde = fechaLarga(r.desde);
  const actualizado = fechaLarga(r.actualizado);
  const meta = [
    desde ? `Pruebas disputadas desde el ${desde}, que por fecha pueden sumar en este ranking.` : 'Todas las pruebas de este ranking.',
    actualizado ? `Ranking actualizado el ${actualizado}.` : null,
  ].filter(Boolean).join(' ');
  return el('details', { class: 'org-more est-pruebas', open: abierta || null },
    el('summary', {}, `Pruebas · ${nombreRanking(r)}`, el('span', { class: 'org-count est-count', text: String(pruebas.length) })),
    el('p', { class: 'organo__meta', text: meta }),
    pruebas.length
      ? el('div', { class: 'est-table-wrap' },
        el('table', { class: 'org-table est-table est-table--pruebas' },
          el('caption', { class: 'sr-only', text: `Pruebas de ${nombreRanking(r)}` }),
          el('thead', {}, el('tr', {},
            el('th', { scope: 'col', text: 'Fecha' }),
            el('th', { scope: 'col', text: 'Federación' }),
            el('th', { scope: 'col', text: 'Torneo' }),
            el('th', { scope: 'col', text: 'Ronda' }),
            el('th', { scope: 'col', class: 'est-num', text: 'Puntos' }))),
          el('tbody', {}, pruebas.map(filaPrueba))))
      : el('p', { class: 'empty', text: 'No hay pruebas de este ranking en el periodo.' }));
}

function seccionRankings(d, n) {
  const rankings = Array.isArray(d.rankings) ? d.rankings : [];
  return seccion('rankings', n, 'Rankings nacionales',
    'Puesto en cada ranking de la FEP en el que participa. Despliega cada ranking para ver las pruebas que puntúan.', null,
    el('div', { class: 'est-table-wrap' },
      el('table', { class: 'org-table est-table est-table--rankings' },
        el('caption', { class: 'sr-only', text: 'Rankings nacionales' }),
        el('thead', {}, el('tr', {},
          el('th', { scope: 'col', text: 'Ranking' }),
          el('th', { scope: 'col', class: 'est-num', text: 'Puesto' }),
          el('th', { scope: 'col', class: 'est-num', text: 'Torneos jugados' }),
          el('th', { scope: 'col', class: 'est-num', text: 'Puntos totales' }),
          el('th', { scope: 'col', class: 'est-num', text: 'Torneos ranking' }),
          el('th', { scope: 'col', class: 'est-num', text: 'Puntos ranking' }))),
        el('tbody', {}, rankings.map((r) => filaRanking(r, d.licencia))))),
    el('dl', { class: 'rkc-leyenda' },
      el('div', {}, el('dt', { text: 'Torneos y puntos totales' }), el('dd', { text: 'Todos los torneos disputados en el periodo y los puntos obtenidos en ellos.' })),
      el('div', {}, el('dt', { text: 'Torneos y puntos ranking' }), el('dd', { text: 'Los que cuentan para el ranking según su normativa.' }))),
    ...rankings.map((r) => pruebasRanking(r, rankings.length === 1)));
}

/* Evolución: columnas de puntos (una por publicación) con el puesto, y tabla con los datos */
function seccionEvolucion(d, n) {
  const serie = (d.evolucion?.puntos ?? []).filter((p) => p && aFecha(p.fecha));
  if (!serie.length) return null;
  const max = Math.max(1, ...serie.map((p) => entero(p.puntos)));
  const primero = serie[0];
  const ultimo = serie[serie.length - 1];
  const meta = `Últimas ${plural(serie.length, 'publicación', 'publicaciones')} del Ranking Único, del ${fechaLarga(primero.fecha)} al ${fechaLarga(ultimo.fecha)}.`;

  const columnas = el('ol', { class: 'fedf-chart est-chart', 'aria-describedby': 'evolucion-nota' }, serie.map((p, i) => {
    const alto = Math.max(1, Math.round((entero(p.puntos) / max) * 100));
    const fecha = fmtDiaMes.format(aFecha(p.fecha)).replace('.', '');
    const puesto = Number.isInteger(p.posicion) ? `${p.posicion}.º` : 's. p.';
    return el('li', { class: `fedf-chart__col${i === serie.length - 1 ? ' is-current' : ''}` },
      el('span', { class: 'fedf-chart__value', 'aria-hidden': 'true', text: num(p.puntos) }),
      el('span', { class: 'fedf-chart__bar', style: `--alto: ${alto}%`, 'aria-hidden': 'true' }),
      el('span', { class: 'fedf-chart__year', 'aria-hidden': 'true', text: fecha }),
      el('span', { class: 'est-chart__pos', 'aria-hidden': 'true', text: puesto }),
      el('span', { class: 'sr-only', text: `${fechaLarga(p.fecha)}: ${Number.isInteger(p.posicion) ? `puesto ${p.posicion}` : 'sin puesto'}, ${num(p.puntos)} puntos` }));
  }));

  const filas = serie.map((p, i) => {
    const previo = serie[i - 1]?.posicion;
    const cambio = Number.isInteger(p.posicion) && Number.isInteger(previo) ? previo - p.posicion : null;
    return el('tr', {},
      el('th', { scope: 'row' }, el('time', { datetime: String(p.fecha).slice(0, 10), text: fechaLarga(p.fecha) })),
      el('td', { class: 'est-num', text: Number.isInteger(p.posicion) ? `${p.posicion}.º` : 'Sin puesto' }),
      el('td', { class: 'est-num', text: num(p.puntos) }),
      el('td', { class: 'est-num' }, cambio == null || cambio === 0
        ? el('span', { class: 'rkc-evo rkc-evo--igual', text: cambio === 0 ? '=' : '—' })
        : el('span', { class: `rkc-evo rkc-evo--${cambio > 0 ? 'sube' : 'baja'}`, text: `${cambio > 0 ? '▲' : '▼'} ${Math.abs(cambio)}` })));
  });

  return seccion('evolucion', n, 'Evolución en el Ranking Único', 'Puntos (barras) y puesto (debajo de cada fecha) en cada actualización del ranking.', meta,
    el('figure', { class: 'fedf-figure' },
      el('div', { class: 'fedf-chart-wrap', tabindex: '0', role: 'region', 'aria-label': 'Puntos y puesto en el Ranking Único por publicación' }, columnas),
      el('figcaption', { id: 'evolucion-nota', text: 'La barra roja es la última publicación. Debajo de cada fecha, el puesto en el ranking.' })),
    el('details', { class: 'org-more' },
      el('summary', { text: 'Ver los datos en una tabla' }),
      el('table', { class: 'org-table transp-amounts est-table est-table--evolucion' },
        el('caption', { text: 'Evolución en el Ranking Único' }),
        el('thead', {}, el('tr', {},
          el('th', { scope: 'col', text: 'Publicación' }),
          el('th', { scope: 'col', class: 'est-num', text: 'Puesto' }),
          el('th', { scope: 'col', class: 'est-num', text: 'Puntos' }),
          el('th', { scope: 'col', class: 'est-num', text: 'Cambio' }))),
        el('tbody', {}, filas))));
}

/* Campeonatos de España por equipos */
const siNo = (v) => el('span', { class: `est-sino ${v ? 'est-sino--si' : 'est-sino--no'}`, text: v ? 'Sí' : 'No' });

function filaEquipo(e, oculta) {
  const href = enlace(e.enlace);
  const externo = href && e.nuevaVentana;
  return el('tr', { hidden: oculta || null, dataset: oculta ? { extra: '1' } : undefined },
    el('td', { class: 'est-td--fecha', 'data-label': 'Fecha' }, e.fecha
      ? el('time', { datetime: String(e.fecha).slice(0, 10), text: mesAnio(e.fecha) })
      : String(e.anio ?? '—')),
    el('th', { scope: 'row', class: 'est-td--torneo' }, href
      ? el('a', { href, target: externo ? '_blank' : null, rel: externo ? 'noopener' : null }, e.torneo, externo ? nuevaPestana() : null)
      : e.torneo),
    el('td', { class: 'est-td--sino', 'data-label': 'Puntúa' }, siNo(e.puntua)),
    el('td', { class: 'est-td--sino', 'data-label': 'Juega' }, siNo(e.juega)));
}

function seccionEquipos(d, n) {
  const equipos = Array.isArray(d.equipos) ? d.equipos : [];
  if (!equipos.length) {
    return seccion('equipos', n, 'Campeonatos por equipos', null, null,
      el('p', { class: 'empty', text: 'No ha participado en ningún campeonato de España por equipos desde 2013.' }));
  }
  const tbody = el('tbody', {}, equipos.map((e, i) => filaEquipo(e, i >= EQUIPOS_VISIBLES)));
  const restantes = equipos.length - EQUIPOS_VISIBLES;
  let boton = null;
  if (restantes > 0) {
    boton = el('button', { type: 'button', class: 'clubs-pager__btn est-mas', 'aria-expanded': 'false', 'aria-controls': 'equipos-tabla',
      text: `Ver ${plural(restantes, 'campeonato más', 'campeonatos más')}` });
    boton.addEventListener('click', () => {
      const abrir = boton.getAttribute('aria-expanded') !== 'true';
      $$('tr[data-extra]', tbody).forEach((tr) => { tr.hidden = !abrir; });
      boton.setAttribute('aria-expanded', String(abrir));
      boton.textContent = abrir ? 'Ver menos' : `Ver ${plural(restantes, 'campeonato más', 'campeonatos más')}`;
    });
  }
  return seccion('equipos', n, 'Campeonatos por equipos',
    `${plural(equipos.length, 'campeonato de España', 'campeonatos de España')} por equipos desde 2013 en los que juega o puntúa.`, null,
    el('div', { class: 'est-table-wrap' },
      el('table', { class: 'org-table est-table est-table--equipos', id: 'equipos-tabla' },
        el('caption', { class: 'sr-only', text: 'Campeonatos por equipos' }),
        el('thead', {}, el('tr', {},
          el('th', { scope: 'col', text: 'Fecha' }),
          el('th', { scope: 'col', text: 'Campeonato' }),
          el('th', { scope: 'col', text: 'Puntúa' }),
          el('th', { scope: 'col', text: 'Juega' }))),
        tbody)),
    boton ? el('div', { class: 'rkc-mas' }, boton) : null,
    el('p', { class: 'organo__meta', text: '"Puntúa": el jugador o jugadora suma para la clasificación del equipo; "Juega": está en la alineación.' }));
}

/* ---------------------------------------------------------------------------
   Cabecera de página, índice y pie
   --------------------------------------------------------------------------- */
function pintarCabecera(d) {
  const nombre = nombrePropio(d.nombre) || `Licencia ${d.licencia}`;
  $('#titulo-pagina').textContent = `${nombre.replace(/[.\s]+$/, '')}.`;
  $('#est-miga').textContent = nombre;
  $('#est-eyebrow').textContent = `Estadísticas · Licencia ${d.licencia}`;
  const clubes = [...new Set([d.clubNacional, d.clubAutonomico].filter(Boolean))];
  $('#est-entradilla').textContent = [
    d.categoria ? `Categoría por edad ${d.categoria}` : null,
    clubes.length ? `${clubes.length > 1 ? 'clubes' : 'club'} ${clubes.join(' y ')}` : null,
  ].filter(Boolean).join(' · ') || 'Estadísticas de la base de datos de la FEP.';

  const puestos = (d.rankings ?? []).filter((r) => Number.isInteger(r.posicion)).slice(0, 3);
  const partidos = d.balance?.partidos;
  $('#est-resumen').replaceChildren(
    ...puestos.map((r) => el('li', {}, el('strong', { text: `${r.posicion}.º` }), ` ${r.lista ? `${r.ranking.replace(/^Ranking (de |Único )?/, '')} ${r.lista.toLowerCase()}` : r.ranking}`)),
    entero(partidos?.total) ? el('li', {}, el('strong', { text: num(partidos.ganados) }), ` de ${plural(entero(partidos.total), 'partido ganado', 'partidos ganados')}`) : null);
  document.title = `${nombre} · Estadísticas · ${SITIO}`;
}

let observador = null;
function pintarIndice(entradas) {
  $('#est-indice').replaceChildren(...entradas.map(([clave, texto]) => el('li', {},
    el('a', { href: `#${clave}`, dataset: { clave } }, el('span', { text: texto })))));
  observador?.disconnect();
  if (!('IntersectionObserver' in window)) return;
  const enlaces = new Map($$('#est-indice a').map((a) => [a.dataset.clave, a]));
  observador = new IntersectionObserver((vistas) => {
    for (const v of vistas) {
      if (!v.isIntersecting) continue;
      enlaces.forEach((a, clave) => {
        const activo = clave === v.target.id;
        a.classList.toggle('is-active', activo);
        if (activo) a.setAttribute('aria-current', 'location'); else a.removeAttribute('aria-current');
      });
    }
  }, { rootMargin: '-35% 0px -55% 0px' });
  entradas.forEach(([clave]) => { const s = document.getElementById(clave); if (s) observador.observe(s); });
}

function pie(d) {
  const consultado = aFecha(d.consultado);
  return el('p', { class: 'org-updated' },
    consultado ? ['Datos consultados el ', el('time', { datetime: String(d.consultado).slice(0, 16), text: fmtLargo.format(consultado) }), '. '] : null,
    el('a', { class: 'fedf-legacy', href: urlSegura(d.urlWebActual) ?? urlWebActual(d.licencia), target: '_blank', rel: 'noopener' },
      'Ver estas estadísticas en la web actual', el('span', { 'aria-hidden': 'true', text: ' ↗' }), nuevaPestana()));
}

/* ---------------------------------------------------------------------------
   Estados
   --------------------------------------------------------------------------- */
function restablecerCabecera() {
  $('#titulo-pagina').textContent = 'Estadísticas de jugador/a.';
  $('#est-miga').textContent = 'Estadísticas';
  $('#est-eyebrow').textContent = 'Estadísticas de jugador/a';
  $('#est-entradilla').textContent = ENTRADILLA;
  $('#est-resumen').replaceChildren();
  document.title = `Estadísticas de jugador/a · ${SITIO}`;
}

function estado(cual) {
  $('#est-inicio').hidden = cual !== 'inicio';
  $('#est-skeleton').hidden = cual !== 'cargando';
  $('#est-error').hidden = cual !== 'error';
  $('#est-resultado').setAttribute('aria-busy', String(cual === 'cargando'));
  if (cual !== 'datos') $('#est-secciones').replaceChildren();
}

function mostrarError(licencia, problema) {
  const aviso = $('#est-error');
  const web = el('a', { href: urlWebActual(licencia) }, 'web actual de la FEP');
  if (problema?.motivo === 'licencia-no-encontrada') {
    aviso.replaceChildren(el('p', {}, el('strong', { text: `No existe ninguna licencia de jugador/a con el número ${licencia}.` })),
      el('p', {}, 'Revisa el número o prueba con otro. Si el error persiste, escribe a ',
        el('a', { href: 'mailto:web@padelfederacion.es', text: 'web@padelfederacion.es' }), '.'));
  } else if (problema?.motivo === 'sin-ranking') {
    aviso.replaceChildren(el('p', {}, el('strong', { text: `La licencia ${licencia} no figura en ningún ranking nacional de la FEP.` })),
      el('p', { text: 'Las estadísticas solo se publican para los jugadores y jugadoras que participan en algún ranking nacional.' }));
  } else {
    aviso.replaceChildren(el('p', {}, `No se han podido cargar las estadísticas de la licencia ${licencia}. Puedes consultarlas en la `, web, '.'));
  }
  estado('error');
  restablecerCabecera();
  pintarIndice([['buscar', 'Buscar jugador/a']]);
}

/** Tras un fallo, pregunta a la API el motivo (404 con ProblemDetails: licencia inexistente o sin ranking). */
async function motivoError(ruta) {
  if (location.protocol === 'file:' && !API_BASE) return null;
  try {
    const res = await fetch(API_BASE + ruta, { headers: { Accept: 'application/problem+json, application/json' } });
    if (res.status !== 404) return null;
    const p = await res.json();
    return typeof p?.motivo === 'string' ? p : null;
  } catch {
    return null;
  }
}

/* ---------------------------------------------------------------------------
   Carga
   --------------------------------------------------------------------------- */
async function cargar(licencia) {
  const esta = ++peticion;
  $('#est-licencia').value = String(licencia);
  estado('cargando');
  const ruta = `/api/v1/competicion/jugadores/${licencia}/estadisticas`;
  let d = null;
  try {
    d = await get(ruta);
  } catch (e) {
    console.warn('[FEP] estadísticas:', e.message);
  }
  if (esta !== peticion) return;
  if (!d || d.licencia !== licencia || !d.nombre) {
    const problema = await motivoError(ruta);
    if (esta === peticion) mostrarError(licencia, problema);
    return;
  }

  pintarCabecera(d);
  let n = 0;
  const secciones = [
    ['ficha', 'Ficha y balance', seccionFicha(d, ++n)],
    ['rankings', 'Rankings', seccionRankings(d, ++n)],
  ];
  const evolucion = seccionEvolucion(d, n + 1);
  if (evolucion) { n++; secciones.push(['evolucion', 'Evolución', evolucion]); }
  secciones.push(['equipos', 'Equipos', seccionEquipos(d, ++n)]);

  estado('datos');
  $('#est-secciones').replaceChildren(...secciones.map(([, , nodo]) => nodo), pie(d));
  pintarIndice([['buscar', 'Buscar jugador/a'], ...secciones.map(([clave, texto]) => [clave, texto])]);
  if (location.hash) document.getElementById(decodeURIComponent(location.hash.slice(1)))?.scrollIntoView();
}

function inicio() {
  ++peticion;
  $('#est-licencia').value = '';
  estado('inicio');
  restablecerCabecera();
  pintarIndice([['buscar', 'Buscar jugador/a']]);
}

/* ---------------------------------------------------------------------------
   Formulario
   --------------------------------------------------------------------------- */
function errorCampo(texto) {
  const input = $('#est-licencia');
  const error = $('#est-licencia-error');
  error.textContent = texto ?? '';
  error.hidden = !texto;
  if (texto) input.setAttribute('aria-invalid', 'true'); else input.removeAttribute('aria-invalid');
}

function initFormulario() {
  const input = $('#est-licencia');
  $('#est-form').addEventListener('submit', (e) => {
    e.preventDefault();
    const valor = input.value.replace(/\s+/g, '');
    if (!LICENCIA.test(valor)) {
      errorCampo(valor ? 'El número de licencia solo puede tener cifras (hasta 9) y no puede empezar por 0.' : 'Escribe un número de licencia.');
      input.focus();
      return;
    }
    errorCampo(null);
    const licencia = Number(valor);
    if (leerLicencia() !== licencia) history.pushState({ licencia }, '', urlPropia(licencia));
    cargar(licencia);
  });
  input.addEventListener('input', () => { if (!$('#est-licencia-error').hidden) errorCampo(null); });
  window.addEventListener('popstate', () => {
    const licencia = leerLicencia();
    if (licencia == null) inicio(); else cargar(licencia);
  });
}

/* ---------------------------------------------------------------------------
   Arranque
   --------------------------------------------------------------------------- */
function init() {
  initFormulario();
  const licencia = leerLicencia();
  if (licencia == null) {
    inicio();
    return;
  }
  // ?id= (enlace antiguo) → ?licencia= en la barra de direcciones, sin recargar.
  if (!new URLSearchParams(location.search).has('licencia')) history.replaceState({ licencia }, '', urlPropia(licencia) + location.hash);
  cargar(licencia);
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
})();
