/**
 * Página "Cuadros del torneo" (migrada desde Previo.asp / PrevioEquipos.asp y las ~40 páginas a las que redirigían:
 * Cuadro4…Cuadro128, CuadroPrevia*, CuadroPrePrevia*, CuadroEquipos*, Liguilla*, EquiposEnfrentamientos).
 * Requiere assets/js/comun.js (cabecera, menú, pie y utilidades en window.FEP).
 *
 *   Índice de cuadros  → GET /api/v1/competicion/cuadros/{torneo}
 *   Un cuadro          → GET /api/v1/competicion/cuadros/{torneo}/{genero}/{categoria}/{nivel}/{cuadro}
 *   Una eliminatoria   → GET …/{cuadro}/enfrentamientos/{avance}/{posicion}   (torneos por equipos)
 *   (respaldo: mock/competicion-cuadros-….json)
 *
 * Todas las páginas antiguas leían las mismas tablas y solo cambiaba el tamaño, así que aquí hay un único
 * renderizador. Modelo (SFEP_Torneos_Cuadros): "avance" es la ronda (0 = campeón, 1 = final, 2 = semifinales…) y
 * "posicion" el puesto dentro de ella. El partido p de la ronda a enfrenta a las posiciones 2p-1 y 2p, y quien gana
 * ocupa la posición p de la ronda a-1. En las liguillas "avance" es el número de partido (posiciones 1 y 2).
 *
 * URL: cuadro.html?torneo=28714&genero=M&categoria=Cat50&nivel=1&cuadro=Final (también IdTorneo, Genero, Cat, Nivel,
 * Cuadro, como la web actual). Solo con ?torneo= se abre el primer cuadro publicado. Con &avance=&posicion= se abre
 * además el detalle de esa eliminatoria (enlaces antiguos a EquiposEnfrentamientos.asp).
 * Todo texto de la API va por textContent; las notas (HTML de la BD) pasan por una lista blanca.
 */
(function () {
'use strict';

const { $, $$, el, get, urlSegura, paginaMigrada } = window.FEP;
const { LEGACY_BASE } = window.FEPApi;

const GENEROS = { M: 'Masculino', F: 'Femenino', X: 'Mixto' };
const ORDEN_GENERO = { M: 0, F: 1, X: 2 };
const NIVELES_ESPECIALES = { 21: 'Previa', 31: 'Pre-previa' };
const RONDAS = { 1: 'Final', 2: 'Semifinales', 4: 'Cuartos de final', 8: 'Octavos de final', 16: 'Dieciseisavos', 32: 'Treintaidosavos', 64: 'Ronda de 128' };
const RONDAS_CORTAS = { 1: 'Final', 2: 'Semis', 4: 'Cuartos', 8: 'Octavos', 16: '1/16', 32: '1/32', 64: '1/64' };
const VISTA_CLAVE = 'fep.cuadro.vista';
const ID = /^[1-9]\d{0,9}$/;
const TEXTO_LIBRE = /^[\p{L}\p{N} ._+-]{1,40}$/u;

const fmtNum = new Intl.NumberFormat('es-ES');
const fmtDia = new Intl.DateTimeFormat('es-ES', { weekday: 'short', day: 'numeric', month: 'short' });
const fmtHora = new Intl.DateTimeFormat('es-ES', { hour: '2-digit', minute: '2-digit' });
const fmtFecha = new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'long', year: 'numeric' });
const fmtFechaCorta = new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'long' });

const estado = { sel: null, indice: [], datos: null, torneo: null, vista: null, peticion: 0 };

/* ===========================================================================
   Utilidades
   =========================================================================== */
const limpio = (t) => String(t ?? '').replace(/\s+/g, ' ').trim();
const lista = (v) => (Array.isArray(v) ? v : []);
const clave = (t) => limpio(t).normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
const capital = (t) => limpio(t).toLowerCase().replace(/(^|[\s(-])(\p{L})/gu, (m, a, b) => a + b.toUpperCase());

function leerFecha(v) {
  const s = String(v ?? '');
  const m = /^(\d{4})-(\d{2})-(\d{2})(?:[T ](\d{2}):(\d{2}))?/.exec(s);
  if (!m) return null;
  const d = new Date(+m[1], +m[2] - 1, +m[3], +(m[4] ?? 0), +(m[5] ?? 0));
  return Number.isNaN(d.getTime()) ? null : { fecha: d, conHora: Boolean(m[4]) && (+m[4] || +m[5]) };
}

function categoriaTexto(c) {
  const m = /^cat\s*(\d{2})$/i.exec(limpio(c));
  return m ? `+${m[1]}` : capital(c);
}
function cuadroTexto(q) {
  const t = limpio(q);
  if (/^final$/i.test(t)) return 'Cuadro final';
  if (/^consola/i.test(t)) return 'Consolación';
  return capital(t);
}
function nivelTexto(n) {
  return NIVELES_ESPECIALES[n] ?? `${n}.ª categoría`;
}
const generoTexto = (g) => GENEROS[g] ?? g;

/** Parámetros de la URL (nombres nuevos y antiguos; ASP no distinguía mayúsculas). */
function leerUrl() {
  const q = new Map([...new URLSearchParams(location.search)].map(([k, v]) => [k.toLowerCase(), limpio(v)]));
  const torneo = q.get('torneo') ?? q.get('idtorneo') ?? q.get('id') ?? '';
  const genero = (q.get('genero') ?? '').charAt(0).toUpperCase();
  const categoria = q.get('categoria') ?? q.get('cat') ?? '';
  const nivel = q.get('nivel') ?? '';
  const cuadro = q.get('cuadro') ?? '';
  const avance = q.get('avance') ?? '';
  const posicion = q.get('posicion') ?? '';
  return {
    torneo: ID.test(torneo) ? Number(torneo) : null,
    cuadro: GENEROS[genero] && TEXTO_LIBRE.test(categoria) && /^\d{1,2}$/.test(nivel) && TEXTO_LIBRE.test(cuadro)
      ? { genero, categoria, nivel: Number(nivel), cuadro }
      : null,
    eliminatoria: /^\d{1,2}$/.test(avance) && /^\d{1,3}$/.test(posicion) ? { avance: +avance, posicion: +posicion } : null,
  };
}

function urlCuadro(torneo, c) {
  const q = new URLSearchParams({ torneo, genero: c.genero, categoria: c.categoria, nivel: c.nivel, cuadro: c.cuadro });
  return `cuadro.html?${q}`;
}
const mismo = (a, b) => a && b && a.genero === b.genero && clave(a.categoria) === clave(b.categoria)
  && Number(a.nivel) === Number(b.nivel) && clave(a.cuadro) === clave(b.cuadro);

function rutaApi(torneo, c, extra = '') {
  const seg = [c.genero, c.categoria, c.nivel, c.cuadro].map((s) => encodeURIComponent(String(s)));
  return `/api/v1/competicion/cuadros/${torneo}/${seg.join('/')}${extra}`;
}

/** Ficha del torneo: los torneos por equipos y por parejas siguen en la web actual (salvo los ya migrados). */
function urlFichaTorneo(t) {
  const pagina = t?.tipo === 'equipos' ? 'Camp_Equipos.asp' : 'Camp_Parejas.asp';
  const legado = new URL(`${pagina}?IdTorneo=${t?.id ?? ''}`, LEGACY_BASE).href;
  return paginaMigrada(legado) ?? legado;
}
function urlWebActual(torneo, c, tipo) {
  if (!torneo) return new URL('Campeonatos', LEGACY_BASE).href;
  const pagina = tipo === 'equipos' ? 'PrevioEquipos.asp' : 'Previo.asp';
  // Sin cuadro concreto: la ficha del torneo si se sabe su tipo; si no, el listado de campeonatos.
  if (!c) return tipo ? urlFichaTorneo({ id: torneo, tipo }) : new URL('Campeonatos', LEGACY_BASE).href;
  const q = new URLSearchParams({ IdTorneo: torneo, Genero: c.genero, Nivel: c.nivel, Cat: c.categoria, Cuadro: c.cuadro });
  return new URL(`${pagina}?${q}`, LEGACY_BASE).href;
}

/** "6/4-6/2", "3/0 y lesión", "2/1" → { sets: [[6,4],[6,2]], nota: "Lesión" }. */
function leerResultado(r) {
  const texto = limpio(r);
  if (!texto) return null;
  const sets = [...texto.matchAll(/(\d{1,2})\s*\/\s*(\d{1,2})/g)].map((m) => [Number(m[1]), Number(m[2])]);
  const resto = texto.replace(/(\d{1,2})\s*\/\s*(\d{1,2})/g, ' ').replace(/[-–,;()]+/g, ' ').replace(/(^|\s)y(\s|$)/gi, ' ');
  const nota = limpio(resto);
  return { texto, sets, nota: nota ? nota.charAt(0).toUpperCase() + nota.slice(1) : '' };
}

function textoHorario(h) {
  if (!h) return '';
  const f = leerFecha(h.dia);
  const partes = [];
  if (limpio(h.lugar)) partes.push(limpio(h.lugar));
  if (f) partes.push(fmtDia.format(f.fecha).replace('.', ''));
  if (limpio(h.obs)) partes.push(limpio(h.obs));
  else if (f?.conHora) partes.push(fmtHora.format(f.fecha));
  return partes.join(' · ');
}

function rangoFechas(t) {
  const a = leerFecha(t?.fechaInicio)?.fecha;
  const b = leerFecha(t?.fechaFin)?.fecha;
  if (!a) return '';
  if (!b || a.getTime() === b.getTime()) return fmtFecha.format(a);
  if (a.getFullYear() === b.getFullYear()) return `${fmtFechaCorta.format(a)} – ${fmtFecha.format(b)}`;
  return `${fmtFecha.format(a)} – ${fmtFecha.format(b)}`;
}

/* ===========================================================================
   Notas del cuadro (TextoCuadros, HTML de la BD) → lista blanca mínima
   =========================================================================== */
const ETIQUETAS_NOTA = new Set(['p', 'br', 'strong', 'b', 'em', 'i', 'u', 'sup', 'sub', 'ul', 'ol', 'li', 'a']);
const DESCARTAR_NOTA = new Set(['script', 'style', 'iframe', 'object', 'embed', 'form', 'input', 'button', 'svg', 'math', 'template', 'noscript', 'img']);

function sanearNotas(html) {
  const doc = new DOMParser().parseFromString(String(html ?? ''), 'text/html');
  const frag = document.createDocumentFragment();
  const copiar = (origen, destino, nivel) => {
    if (nivel > 40) return;
    for (const n of origen.childNodes) {
      if (n.nodeType === Node.TEXT_NODE) { destino.append(n.data); continue; }
      if (n.nodeType !== Node.ELEMENT_NODE) continue;
      const tag = n.localName;
      if (DESCARTAR_NOTA.has(tag)) continue;
      if (!ETIQUETAS_NOTA.has(tag)) {
        // div/h1…/font/span: se desenvuelven (los bloques como párrafo).
        if (/^(div|h\d|center|blockquote)$/.test(tag)) { const p = el('p'); copiar(n, p, nivel + 1); destino.append(p); } else copiar(n, destino, nivel + 1);
        continue;
      }
      if (tag === 'a') {
        const href = urlSegura(n.getAttribute('href'));
        const a = href ? el('a', { href, target: '_blank', rel: 'noopener' }) : el('span');
        copiar(n, a, nivel + 1);
        destino.append(a);
        continue;
      }
      const c = document.createElement(tag);
      copiar(n, c, nivel + 1);
      destino.append(c);
    }
  };
  copiar(doc.body, frag, 0);
  // Párrafos vacíos (&nbsp;) del editor antiguo.
  for (const p of [...frag.querySelectorAll('p')]) if (!limpio(p.textContent) && !p.querySelector('br + br')) p.remove();
  return frag;
}

/* ===========================================================================
   Modelo del cuadro
   =========================================================================== */
function construir(d) {
  const esEquipos = d.torneo?.tipo === 'equipos';
  const part = new Map();
  for (const p of lista(d.participantes)) {
    if (!Number.isInteger(p?.avance) || !Number.isInteger(p?.posicion)) continue;
    part.set(`${p.avance}-${p.posicion}`, { ...p, nombre: limpio(p.nombre) });
  }
  const partidos = new Map(lista(d.partidos).filter((m) => Number.isInteger(m?.avance) && Number.isInteger(m?.posicion))
    .map((m) => [`${m.avance}-${m.posicion}`, m]));

  // Cabezas de serie: se arrastran por nombre a todas las rondas.
  const semillas = new Map();
  for (const p of part.values()) if (p.cabezaSerie && esReal(p)) semillas.set(clave(p.nombre), p.cabezaSerie);

  const avances = [...new Set([...part.values()].map((p) => p.avance))].sort((a, b) => b - a);
  const modelo = { esEquipos, part, partidos, semillas, rondas: [], final: null, campeon: null, avances };
  if (!avances.length) return modelo;

  if (d.formato === 'liguilla') return modelo;

  const max = avances[0];
  const min = avances[avances.length - 1];
  for (let a = max; a > min; a--) {
    const n = 2 ** (a - 1);
    const encuentros = [];
    for (let p = 1; p <= n; p++) encuentros.push(encuentro(modelo, a, p));
    modelo.rondas.push({ avance: a, encuentros });
  }
  // Última columna: campeón (avance 0) o clasificados de una previa.
  const ult = [];
  for (let p = 1; p <= 2 ** min; p++) ult.push(part.get(`${min}-${p}`) ?? null);
  modelo.final = { avance: min, entradas: ult };
  if (min === 0 && ult[0] && esReal(ult[0])) modelo.campeon = ult[0];

  const total = modelo.rondas.length;
  modelo.rondas.forEach((r, i) => {
    const n = r.encuentros.length;
    r.nombre = min === 0 ? (RONDAS[n] ?? `Ronda ${i + 1}`) : `Ronda ${i + 1}`;
    r.corto = min === 0 ? (RONDAS_CORTAS[n] ?? `R${i + 1}`) : `R${i + 1}`;
    r.ultima = i === total - 1;
  });
  return modelo;
}

const esReal = (p) => p && p.nombre && (p.tipo === 'pareja' || p.tipo === 'equipo');
const esExento = (p) => p?.tipo === 'exento';
const mismoNombre = (a, b) => a && b && esReal(a) && esReal(b) && clave(a.nombre) === clave(b.nombre);

function encuentro(m, a, p) {
  const arriba = m.part.get(`${a}-${2 * p - 1}`) ?? null;
  const abajo = m.part.get(`${a}-${2 * p}`) ?? null;
  const siguiente = m.part.get(`${a - 1}-${p}`) ?? null;
  const partido = m.partidos.get(`${a}-${p}`) ?? null;
  let ganador = 0;
  if (mismoNombre(arriba, siguiente)) ganador = 1;
  else if (mismoNombre(abajo, siguiente)) ganador = 2;
  const exento = esExento(arriba) || esExento(abajo);
  return { avance: a, posicion: p, arriba, abajo, ganador, partido, resultado: leerResultado(partido?.resultado), exento };
}

/* ===========================================================================
   Tarjetas
   =========================================================================== */
function nombreParticipante(p, esEquipos) {
  if (!p) return el('span', { class: 'cdr-team__tbd', text: 'Por decidir' });
  if (p.tipo === 'exento') return el('span', { class: 'cdr-team__tbd', text: 'Exento' });
  if (p.tipo === 'previa') return el('span', { class: 'cdr-team__tbd' }, 'Previa', p.letra ? el('strong', { text: ` ${limpio(p.letra)}` }) : null);
  if (p.tipo === 'lucky') return el('span', { class: 'cdr-team__tbd', text: 'Lucky loser' });
  if (!esReal(p)) return el('span', { class: 'cdr-team__tbd', text: p.nombre || 'Por decidir' });
  if (esEquipos) return el('span', { class: 'cdr-team__name cdr-team__name--team', title: p.nombre, text: p.nombre });
  const jugadores = lista(p.jugadores).map(limpio).filter(Boolean);
  const js = jugadores.length ? jugadores : p.nombre.split(/\s+-\s+|\//).map(limpio).filter(Boolean);
  return el('span', { class: 'cdr-team__name cdr-team__name--pair', title: js.join(' / ') }, js.map((j) => el('span', { text: j })));
}

/** Juegos de cada set en la fila `lado` (1 arriba, 2 abajo). */
function juegosFila(e, lado, esEquipos) {
  const r = e.resultado;
  if (!r?.sets.length) return null;
  // Equipos: "2/1" es arriba/abajo. Parejas: el resultado se escribe desde quien gana.
  let primero;
  if (esEquipos) primero = lado === 1;
  else if (e.ganador) primero = lado === e.ganador;
  else return null;
  return r.sets.map(([g, p]) => (primero ? [g, p] : [p, g]));
}

function fila(e, lado, m) {
  const p = lado === 1 ? e.arriba : e.abajo;
  const gana = e.ganador === lado;
  const pierde = e.ganador && !gana;
  const semilla = esReal(p) ? m.semillas.get(clave(p.nombre)) : null;
  const juegos = juegosFila(e, lado, m.esEquipos);
  return el('div', {
    class: `cdr-team${gana ? ' is-winner' : ''}${pierde ? ' is-loser' : ''}${esReal(p) ? '' : ' is-empty'}`,
    dataset: esReal(p) ? { k: clave(p.nombre) } : {},
  },
  el('span', { class: 'cdr-seed', 'aria-label': semilla ? `Cabeza de serie ${semilla}` : null, text: semilla ? String(semilla) : '' }),
  nombreParticipante(p, m.esEquipos),
  juegos
    ? el('span', { class: `cdr-team__score${m.esEquipos ? ' cdr-team__score--tie' : ''}` },
      juegos.map(([a, b]) => el('span', { class: a > b ? 'is-won' : null, text: String(a) })))
    : null,
  gana ? el('span', { class: 'sr-only', text: ' (gana)' }) : null);
}

function tarjeta(e, m, ronda) {
  const r = e.resultado;
  const h = e.partido?.horario;
  const pendiente = !r && esReal(e.arriba) && esReal(e.abajo);
  const meta = [];
  if (r?.nota) meta.push(el('span', { class: 'cdr-badge', text: r.nota }));
  if (r && (!r.sets.length || (!e.ganador && !m.esEquipos))) meta.push(el('span', { class: 'cdr-match__raw', text: r.texto }));
  if (!r && h) {
    meta.push(el('span', { class: 'cdr-match__time' }, icono('reloj'), textoHorario(h)));
  }
  if (!r && h?.vs) meta.push(el('span', { class: 'cdr-badge cdr-badge--tv', text: 'TV' }));

  const art = el('article', {
    class: `cdr-match${r ? ' is-done' : ''}${pendiente ? ' is-pending' : ''}${e.exento ? ' is-bye' : ''}${e.partido?.enfrentamiento ? ' is-clickable' : ''}`,
    'aria-label': `${ronda.nombre}, partido ${e.posicion}`,
  },
  fila(e, 1, m),
  fila(e, 2, m),
  meta.length ? el('div', { class: 'cdr-match__meta' }, meta) : null,
  e.partido?.enfrentamiento && esReal(e.arriba) && esReal(e.abajo)
    ? el('button', {
      type: 'button', class: 'cdr-match__open',
      onclick: () => abrirEliminatoria(e, ronda),
    }, el('span', { class: 'sr-only', text: `Ver la eliminatoria ${e.arriba.nombre} contra ${e.abajo.nombre}` }),
    el('span', { 'aria-hidden': 'true', text: 'Partidos ›' }))
    : null);
  return art;
}

/** SVG con espacio de nombres (el() crea elementos HTML). */
function icono(nombre) {
  const ns = 'http://www.w3.org/2000/svg';
  const svg = document.createElementNS(ns, 'svg');
  svg.setAttribute('viewBox', '0 0 24 24');
  svg.setAttribute('width', '13');
  svg.setAttribute('height', '13');
  svg.setAttribute('aria-hidden', 'true');
  const d = { reloj: 'M12 7v5l3 2M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18Z', trofeo: 'M8 21h8M12 17v4M7 4h10v5a5 5 0 0 1-10 0V4Z' }[nombre];
  const path = document.createElementNS(ns, 'path');
  path.setAttribute('d', d);
  path.setAttribute('fill', 'none');
  path.setAttribute('stroke', 'currentColor');
  path.setAttribute('stroke-width', '2');
  path.setAttribute('stroke-linecap', 'round');
  svg.append(path);
  return svg;
}

function tarjetaFinal(p, m) {
  if (m.final.avance === 0) {
    return el('article', { class: `cdr-winner${esReal(p) ? '' : ' is-empty'}`, 'aria-label': 'Campeón' },
      el('span', { class: 'cdr-winner__icon' }, icono('trofeo')),
      el('div', { class: 'cdr-team', dataset: esReal(p) ? { k: clave(p.nombre) } : {} }, nombreParticipante(p, m.esEquipos)));
  }
  return el('article', { class: `cdr-match cdr-match--qualified${esReal(p) ? '' : ' is-empty'}` },
    el('div', { class: 'cdr-team is-winner', dataset: esReal(p) ? { k: clave(p.nombre) } : {} },
      el('span', { class: 'cdr-seed' }), nombreParticipante(p, m.esEquipos)));
}

/* ===========================================================================
   Vista "Cuadro"
   =========================================================================== */
function pintarCuadro(m) {
  const cont = $('#cdr-bracket');
  const cols = m.rondas.map((r) => {
    // Progreso de la ronda: partidos con resultado o ya decididos (gana uno) sobre los que se juegan (sin exentos).
    const jugables = r.encuentros.filter((e) => !e.exento);
    const jugados = jugables.filter((e) => e.resultado || e.ganador).length;
    const barra = el('span', { class: 'cdr-col__bar', 'aria-hidden': 'true' });
    barra.style.setProperty('--p', `${jugables.length ? Math.round((jugados / jugables.length) * 100) : 0}%`);
    return el('section', { class: 'cdr-col', 'aria-label': r.nombre },
      el('h3', { class: 'cdr-col__head' }, el('span', { text: r.nombre }),
        el('span', { class: 'cdr-col__count', text: jugables.length && jugados < jugables.length
          ? `${fmtNum.format(jugados)}/${plural(jugables.length, 'partido', 'partidos')}`
          : plural(jugables.length, 'partido', 'partidos') }),
        barra),
      el('div', { class: 'cdr-col__body' }, r.encuentros.map((e) => el('div', {
        class: `cdr-slot${e.ganador ? ' is-decided' : ''}`,
      }, tarjeta(e, m, r)))));
  });
  if (m.final) {
    const titulo = m.final.avance === 0 ? (m.esEquipos ? 'Campeón' : 'Campeones') : 'Clasificados';
    cols.push(el('section', { class: 'cdr-col cdr-col--final', 'aria-label': titulo },
      el('h3', { class: 'cdr-col__head' }, el('span', { text: titulo })),
      el('div', { class: 'cdr-col__body' }, m.final.entradas.map((p) => el('div', { class: 'cdr-slot' }, tarjetaFinal(p, m))))));
  }
  cols.forEach((c, i) => c.style.setProperty('--i', String(i)));
  cont.replaceChildren(...cols);
  cont.style.setProperty('--cdr-cols', String(cols.length));
  cont.classList.toggle('cdr-bracket--teams', m.esEquipos);
  // Ruta del campeón dibujada sobre los conectores (dorado).
  if (m.campeon) marcarRuta(cont, clave(m.campeon.nombre), 'is-champ');
  actualizarFlechas();
}

/**
 * Marca en los conectores el recorrido de un participante: `{pre}-in` en cada hueco donde aparece (línea que entra
 * desde la ronda anterior) y `{pre}-out` donde gana (línea que sale hacia la siguiente).
 */
function marcarRuta(b, k, pre) {
  for (const s of b.querySelectorAll(`.${pre}-in, .${pre}-out`)) s.classList.remove(`${pre}-in`, `${pre}-out`);
  if (!k) return;
  for (const f of b.querySelectorAll(`[data-k="${CSS.escape(k)}"]`)) {
    const slot = f.closest('.cdr-slot');
    if (!slot) continue;
    slot.classList.add(`${pre}-in`);
    if (f.classList.contains('is-winner')) slot.classList.add(`${pre}-out`);
  }
}

const plural = (n, uno, varios) => `${fmtNum.format(n)} ${n === 1 ? uno : varios}`;

function actualizarFlechas() {
  const b = $('#cdr-bracket');
  const max = b.scrollWidth - b.clientWidth;
  $('.cdr-scroll--prev').hidden = max <= 2 || b.scrollLeft <= 2;
  $('.cdr-scroll--next').hidden = max <= 2 || b.scrollLeft >= max - 2;
  $('#cdr-bracket-wrap').classList.toggle('has-more', max > 2 && b.scrollLeft < max - 2);
}

/* ===========================================================================
   Vista "Por rondas"
   =========================================================================== */
let rondaActiva = null;

function pintarRondas(m) {
  const tabs = $('#cdr-rondas-tabs');
  if (rondaActiva == null || !m.rondas.some((r) => r.avance === rondaActiva)) {
    // Primera ronda con partidos pendientes; si no hay, la final.
    const pendiente = m.rondas.find((r) => r.encuentros.some((e) => !e.resultado && esReal(e.arriba) && esReal(e.abajo)));
    rondaActiva = (pendiente ?? m.rondas[m.rondas.length - 1])?.avance ?? null;
  }
  tabs.replaceChildren(...m.rondas.map((r) => el('button', {
    type: 'button', role: 'tab', class: 'cdr-tab', id: `cdr-tab-${r.avance}`,
    'aria-selected': String(r.avance === rondaActiva), tabindex: r.avance === rondaActiva ? '0' : '-1',
    dataset: { avance: String(r.avance) },
    onclick: () => { rondaActiva = r.avance; pintarRondas(m); },
  }, el('span', { text: r.corto }), el('span', { class: 'sr-only', text: ` (${r.nombre})` }))));

  const r = m.rondas.find((x) => x.avance === rondaActiva);
  const panel = $('#cdr-rondas-panel');
  panel.setAttribute('aria-labelledby', `cdr-tab-${rondaActiva}`);
  if (!r) { panel.replaceChildren(); return; }
  const encuentros = r.encuentros.filter((e) => !(e.exento && !(esReal(e.arriba) || esReal(e.abajo))));
  panel.replaceChildren(
    el('h3', { class: 'cdr-rounds__title' }, r.nombre, ' ', el('span', { class: 'org-count', text: fmtNum.format(encuentros.filter((e) => !e.exento).length) })),
    el('div', { class: 'cdr-rounds__grid' }, encuentros.map((e) => tarjeta(e, m, r))));
}

/** Flechas del teclado en las pestañas (patrón ARIA). */
function initTeclasPestanas() {
  $('#cdr-rondas-tabs').addEventListener('keydown', (ev) => {
    const tabs = $$('#cdr-rondas-tabs [role="tab"]');
    const i = tabs.indexOf(document.activeElement);
    if (i < 0) return;
    let j = null;
    if (ev.key === 'ArrowRight') j = (i + 1) % tabs.length;
    else if (ev.key === 'ArrowLeft') j = (i - 1 + tabs.length) % tabs.length;
    else if (ev.key === 'Home') j = 0;
    else if (ev.key === 'End') j = tabs.length - 1;
    if (j == null) return;
    ev.preventDefault();
    tabs[j].click();
    $(`#${tabs[j].id}`)?.focus();
  });
}

/* ===========================================================================
   Liguilla
   =========================================================================== */
function pintarLiguilla(d, m) {
  const sec = $('#cdr-liguilla');
  sec.hidden = false;
  $('#cdr-liguilla-col').textContent = m.esEquipos ? 'Equipo' : 'Pareja';

  const filas = lista(d.clasificacion).filter((c) => c && limpio(c.nombre));
  $('#cdr-clasificacion').replaceChildren(...filas.map((c, i) => {
    const dif = (a, b) => {
      const v = (Number(a) || 0) - (Number(b) || 0);
      return v > 0 ? `+${v}` : String(v);
    };
    return el('tr', { class: i === 0 ? 'is-first' : null, dataset: { k: clave(c.nombre) } },
      el('td', {}, el('span', { class: 'cdr-pos', text: String(c.posicion ?? i + 1) })),
      el('th', { scope: 'row', class: 'cdr-table__name', text: limpio(c.nombre) }),
      el('td', { class: 'cdr-table__pts', text: String(c.puntos ?? 0) }),
      el('td', { text: String(c.pg ?? 0) }),
      el('td', { text: String(c.pp ?? 0) }),
      el('td', { text: dif(c.setsGanados, c.setsPerdidos) }),
      el('td', { text: dif(c.juegosGanados, c.juegosPerdidos) }));
  }));
  sec.querySelector('.cdr-table-wrap').hidden = !filas.length;
  sec.querySelector('.cdr-section__title').hidden = !filas.length;

  // Partidos: el "avance" es el número de partido; posiciones 1 y 2 son los dos lados.
  const encuentros = m.avances.slice().sort((a, b) => a - b).map((a) => {
    const arriba = m.part.get(`${a}-1`) ?? null;
    const abajo = m.part.get(`${a}-2`) ?? null;
    const partido = m.partidos.get(`${a}-1`) ?? m.partidos.get(`${a}-2`) ?? null;
    return { avance: a, posicion: 1, arriba, abajo, ganador: 0, partido, resultado: leerResultado(partido?.resultado), exento: false };
  }).filter((e) => esReal(e.arriba) || esReal(e.abajo));
  const equipos = new Set(encuentros.flatMap((e) => [e.arriba, e.abajo]).filter(esReal).map((p) => clave(p.nombre)));
  const porJornada = Math.max(1, Math.floor(equipos.size / 2));
  const jornadas = [];
  encuentros.forEach((e, i) => {
    const j = Math.floor(i / porJornada);
    (jornadas[j] ??= []).push(e);
  });
  $('#cdr-jornadas').replaceChildren(...jornadas.map((js, j) => el('section', { class: 'cdr-day', 'aria-label': `Jornada ${j + 1}` },
    el('h3', { class: 'cdr-day__title', text: `Jornada ${j + 1}` }),
    el('div', { class: 'cdr-rounds__grid' }, js.map((e) => tarjeta(e, m, { nombre: `Jornada ${j + 1}` }))))));
}

/* ===========================================================================
   Detalle de una eliminatoria por equipos
   =========================================================================== */
function ganadorPartido(p) {
  const abandono = /abandon|retir|w\.?\s?o\b|descalif|no\s+present|lesi/i;
  if (abandono.test(limpio(p.obs1))) return 2;
  if (abandono.test(limpio(p.obs2))) return 1;
  let a = 0;
  let b = 0;
  for (const s of lista(p.sets)) {
    if (Number(s?.e1) > Number(s?.e2)) a++;
    else if (Number(s?.e2) > Number(s?.e1)) b++;
  }
  return a > b ? 1 : b > a ? 2 : 0;
}

async function abrirEliminatoria(e, ronda) {
  const dlg = $('#cdr-dialogo');
  const cuerpo = $('#cdr-dialogo-cuerpo');
  $('#cdr-dialogo-ronda').textContent = ronda?.nombre ?? 'Eliminatoria';
  $('#cdr-dialogo-titulo').textContent = e.arriba && e.abajo ? `${e.arriba.nombre} – ${e.abajo.nombre}` : 'Eliminatoria';
  cuerpo.replaceChildren(el('p', { class: 'cdr-dialog__loading', text: 'Cargando los partidos…' }));
  if (!dlg.open) dlg.showModal();
  try {
    const d = await get(rutaApi(estado.torneo.id, estado.sel, `/enfrentamientos/${e.avance}/${e.posicion}`));
    pintarEliminatoria(d, e);
  } catch (err) {
    console.warn('[FEP] eliminatoria:', err.message);
    const q = new URLSearchParams({
      idtorneo: estado.torneo.id, genero: estado.sel.genero, cat: estado.sel.categoria, Nivel: estado.sel.nivel,
      Cuadro: estado.sel.cuadro, Avance: e.avance, Posicion: e.posicion,
    });
    cuerpo.replaceChildren(el('p', { class: 'empty' }, 'No se han podido cargar los partidos de esta eliminatoria. ',
      el('a', { href: new URL(`EquiposEnfrentamientos.asp?${q}`, LEGACY_BASE).href, text: 'Verla en la web actual' }), '.'));
  }
}

function pintarEliminatoria(d, e) {
  const partidos = lista(d?.partidos);
  const ganados = [0, 0];
  for (const p of partidos) { const g = ganadorPartido(p); if (g) ganados[g - 1]++; }
  const eq1 = limpio(d?.equipo1) || e.arriba?.nombre || 'Equipo 1';
  const eq2 = limpio(d?.equipo2) || e.abajo?.nombre || 'Equipo 2';
  $('#cdr-dialogo-titulo').textContent = `${eq1} – ${eq2}`;

  const marcador = el('div', { class: 'cdr-tie' },
    el('p', { class: `cdr-tie__team${ganados[0] > ganados[1] ? ' is-winner' : ''}`, text: eq1 }),
    el('p', { class: 'cdr-tie__score', 'aria-label': `${ganados[0]} a ${ganados[1]}` },
      el('span', { text: String(ganados[0]) }), el('span', { 'aria-hidden': 'true', text: '–' }), el('span', { text: String(ganados[1]) })),
    el('p', { class: `cdr-tie__team cdr-tie__team--right${ganados[1] > ganados[0] ? ' is-winner' : ''}`, text: eq2 }));

  const filas = partidos.map((p) => {
    const g = ganadorPartido(p);
    const sets = lista(p.sets);
    const lado = (n) => el('div', { class: `cdr-rubber__side${g === n ? ' is-winner' : g ? ' is-loser' : ''}` },
      el('p', { class: 'cdr-rubber__players' }, lista(n === 1 ? p.pareja1 : p.pareja2).map((j) => el('span', { text: limpio(j) }))),
      limpio(n === 1 ? p.obs1 : p.obs2) ? el('span', { class: 'cdr-badge', text: limpio(n === 1 ? p.obs1 : p.obs2) }) : null,
      el('p', { class: 'cdr-rubber__sets' }, sets.map((s) => {
        const a = Number(n === 1 ? s?.e1 : s?.e2);
        const b = Number(n === 1 ? s?.e2 : s?.e1);
        return el('span', { class: a > b ? 'is-won' : null, text: Number.isFinite(a) ? String(a) : '–' });
      })));
    return el('li', { class: 'cdr-rubber' },
      el('p', { class: 'cdr-rubber__num', text: `Partido ${p.partido ?? ''}` }),
      lado(1), lado(2));
  });
  $('#cdr-dialogo-cuerpo').replaceChildren(marcador,
    filas.length ? el('ol', { class: 'cdr-rubbers' }, filas) : el('p', { class: 'empty', text: 'Todavía no hay partidos de esta eliminatoria.' }));
}

function initDialogo() {
  const dlg = $('#cdr-dialogo');
  $('#cdr-dialogo-cerrar').addEventListener('click', () => dlg.close());
  // Clic fuera del contenido (en el fondo) cierra.
  dlg.addEventListener('click', (ev) => { if (ev.target === dlg) dlg.close(); });
}

/* ===========================================================================
   Búsqueda y resaltado del recorrido
   =========================================================================== */
function contenedorActivo() {
  return estado.vista === 'rondas' ? $('#cdr-rondas') : $('#cdr-bracket');
}

function aplicarBusqueda({ desplazar = false } = {}) {
  const q = clave($('#cdr-buscar').value);
  const zonas = [$('#cdr-bracket'), $('#cdr-rondas'), $('#cdr-liguilla')];
  let total = 0;
  const vistos = new Set();
  for (const z of zonas) {
    z.classList.toggle('is-searching', q.length >= 2);
    for (const f of z.querySelectorAll('[data-k]')) {
      const hit = q.length >= 2 && f.dataset.k.includes(q);
      f.classList.toggle('is-hit', hit);
      if (hit && z === contenedorActivo()) vistos.add(f.dataset.k);
    }
  }
  total = vistos.size;
  $('#cdr-buscar-estado').textContent = q.length >= 2
    ? (total ? `${plural(total, 'coincidencia', 'coincidencias')}` : 'Sin coincidencias')
    : '';
  if (desplazar && total) {
    const primero = contenedorActivo().querySelector('.is-hit');
    primero?.scrollIntoView({ block: 'center', inline: 'center', behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' });
  }
}

function initResaltado() {
  const b = $('#cdr-bracket');
  const marcar = (k) => {
    for (const f of b.querySelectorAll('.is-path')) f.classList.remove('is-path');
    b.classList.toggle('is-tracing', Boolean(k));
    marcarRuta(b, k, 'is-trace');
    if (!k) return;
    for (const f of b.querySelectorAll(`[data-k="${CSS.escape(k)}"]`)) f.classList.add('is-path');
  };
  b.addEventListener('pointerover', (ev) => marcar(ev.target.closest('[data-k]')?.dataset.k ?? null));
  b.addEventListener('pointerleave', () => marcar(null));
  b.addEventListener('focusin', (ev) => marcar(ev.target.closest('.cdr-match')?.querySelector('.is-winner[data-k]')?.dataset.k ?? null));
}

/* ===========================================================================
   Vista, selector y cabecera
   =========================================================================== */
function vistaInicial() {
  try {
    const v = localStorage.getItem(VISTA_CLAVE);
    if (v === 'cuadro' || v === 'rondas') return v;
  } catch { /* almacenamiento bloqueado */ }
  return matchMedia('(max-width: 860px)').matches ? 'rondas' : 'cuadro';
}

function ponerVista(v, { guardar = false } = {}) {
  estado.vista = v;
  for (const b of $$('.cdr-seg__btn')) b.setAttribute('aria-pressed', String(b.dataset.vista === v));
  const liguilla = estado.datos?.formato === 'liguilla';
  $('#cdr-bracket-wrap').hidden = liguilla || v !== 'cuadro';
  $('#cdr-rondas').hidden = liguilla || v !== 'rondas';
  if (guardar) { try { localStorage.setItem(VISTA_CLAVE, v); } catch { /* sin almacenamiento */ } }
  if (v === 'cuadro') requestAnimationFrame(actualizarFlechas);
  aplicarBusqueda();
}

function pintarSelector(lista_, sel, torneo) {
  const items = lista_.filter((c) => c && GENEROS[c.genero] && limpio(c.categoria) && limpio(c.cuadro) && Number.isInteger(Number(c.nivel)));
  $('#cdr-picker').hidden = items.length < 2;
  if (items.length < 2) return;

  const chip = (texto, item, activo) => el('a', {
    class: `cdr-chip${activo ? ' is-active' : ''}`, href: urlCuadro(torneo, item),
    'aria-current': activo ? 'true' : null, text: texto,
    onclick: (ev) => {
      if (ev.ctrlKey || ev.metaKey || ev.shiftKey || ev.button !== 0) return;
      ev.preventDefault();
      irA(item);
    },
  });
  const primero = (filtro) => items.filter(filtro).sort(ordenItems)[0];

  const generos = [...new Set(items.map((c) => c.genero))].sort((a, b) => ORDEN_GENERO[a] - ORDEN_GENERO[b]);
  $('#cdr-generos').hidden = generos.length < 2;
  $('#cdr-generos').replaceChildren(el('span', { class: 'cdr-picker__label', text: 'Género' }),
    ...generos.map((g) => chip(generoTexto(g), primero((c) => c.genero === g), g === sel.genero)));

  const delGenero = items.filter((c) => c.genero === sel.genero);
  const cats = [...new Set(delGenero.map((c) => limpio(c.categoria)))];
  $('#cdr-categorias').hidden = cats.length < 2;
  $('#cdr-categorias').replaceChildren(el('span', { class: 'cdr-picker__label', text: 'Categoría' }),
    ...cats.map((c) => chip(categoriaTexto(c), primero((x) => x.genero === sel.genero && limpio(x.categoria) === c), clave(c) === clave(sel.categoria))));

  const tipos = delGenero.filter((c) => clave(c.categoria) === clave(sel.categoria)).sort(ordenItems);
  const variosNiveles = new Set(tipos.map((c) => Number(c.nivel))).size > 1;
  $('#cdr-tipos').replaceChildren(el('span', { class: 'cdr-picker__label', text: 'Cuadro' }),
    ...tipos.map((c) => chip([variosNiveles || NIVELES_ESPECIALES[c.nivel] ? nivelTexto(Number(c.nivel)) : '', cuadroTexto(c.cuadro)].filter(Boolean).join(' · '), c, mismo(c, sel))));
}

function ordenItems(a, b) {
  const peso = (q) => (/^final$/i.test(q) ? 0 : /^consola/i.test(q) ? 1 : 2);
  return (ORDEN_GENERO[a.genero] - ORDEN_GENERO[b.genero])
    || (Number(a.nivel) % 20) - (Number(b.nivel) % 20)
    || peso(a.cuadro) - peso(b.cuadro)
    || String(a.cuadro).localeCompare(String(b.cuadro), 'es');
}

function pintarCabecera(d, m) {
  const t = d.torneo ?? {};
  const titulo = limpio(t.titulo) || 'Cuadros del torneo';
  document.title = `Cuadros · ${titulo} · Federación Española de Pádel`;
  $('#titulo-pagina').textContent = titulo;
  const miga = $('#cdr-miga-torneo');
  miga.textContent = titulo.length > 42 ? `${titulo.slice(0, 40)}…` : titulo;
  miga.href = urlFichaTorneo(t);
  if (/^https?:/i.test(miga.href) && !miga.href.startsWith(location.origin)) miga.title = 'Ficha del torneo en la web actual';

  const sel = estado.sel;
  $('#cdr-eyebrow').textContent = [cuadroTexto(sel.cuadro), `Categoría ${categoriaTexto(sel.categoria)}`,
    Number(sel.nivel) !== 1 ? nivelTexto(Number(sel.nivel)) : '', generoTexto(sel.genero)].filter(Boolean).join(' · ');
  $('#cdr-meta').textContent = [rangoFechas(t), capital(t.lugar)].filter(Boolean).join(' · ');

  const resumen = [];
  if (d.formato === 'liguilla') {
    resumen.push([lista(d.clasificacion).length || new Set([...m.part.values()].filter(esReal).map((p) => clave(p.nombre))).size, m.esEquipos ? 'equipos' : 'parejas']);
  } else if (m.rondas.length) {
    const primera = m.rondas[0];
    const n = primera.encuentros.flatMap((e) => [e.arriba, e.abajo]).filter(esReal).length;
    const todos = m.rondas.flatMap((r) => r.encuentros).filter((e) => !e.exento && (esReal(e.arriba) || esReal(e.abajo)));
    resumen.push([n, m.esEquipos ? 'equipos' : 'parejas'], [m.rondas.length, m.rondas.length === 1 ? 'ronda' : 'rondas']);
    if (todos.length) resumen.push([`${todos.filter((e) => e.resultado).length}/${todos.length}`, 'partidos jugados']);
  }
  if (lista(d.cabezasSerie).length) resumen.push([lista(d.cabezasSerie).length, 'cabezas de serie']);
  $('#cdr-resumen').replaceChildren(...resumen.map(([n, txt]) => el('li', {}, el('strong', { text: typeof n === 'number' ? fmtNum.format(n) : n }), ` ${txt}`)));
}

function pintarCampeon(m) {
  const sec = $('#cdr-campeon');
  sec.hidden = !m.campeon;
  if (!m.campeon) return;
  $('#cdr-campeon-titulo').textContent = m.esEquipos ? 'Campeón' : 'Campeones';
  const js = lista(m.campeon.jugadores).map(limpio).filter(Boolean);
  $('#cdr-campeon-nombre').textContent = js.length ? js.join(' / ') : m.campeon.nombre;
  const final = m.rondas[m.rondas.length - 1]?.encuentros[0];
  const rival = final ? (final.ganador === 1 ? final.abajo : final.ganador === 2 ? final.arriba : null) : null;
  $('#cdr-campeon-final').textContent = rival && esReal(rival)
    ? `Final ante ${rival.nombre}${final.resultado ? ` · ${final.resultado.texto}` : ''}`
    : '';
}

function pintarExtras(d) {
  const notas = $('#cdr-notas');
  const frag = d.notas ? sanearNotas(d.notas) : null;
  notas.hidden = !frag || !limpio(frag.textContent);
  if (!notas.hidden) {
    $('#cdr-notas-texto').replaceChildren(frag);
    notas.open = !matchMedia('(max-width: 860px)').matches;
  }

  const cs = lista(d.cabezasSerie).filter((c) => c && limpio(c.nombre)).sort((a, b) => (a.cabezaSerie ?? 99) - (b.cabezaSerie ?? 99));
  $('#cdr-cabezas').hidden = !cs.length;
  $('#cdr-cabezas-lista').replaceChildren(...cs.map((c) => el('li', { class: 'cdr-seeds__item', dataset: { k: clave(limpio(c.nombre).replace(/\s*\/\s*/g, ' - ')) } },
    el('span', { class: 'cdr-seed', text: String(c.cabezaSerie ?? '') }),
    el('span', { class: 'cdr-seeds__name', text: limpio(c.nombre).replace(/\s*\/\s*/g, ' / ') }),
    c.puntos != null && c.puntos !== '' && Number.isFinite(Number(c.puntos)) ? el('span', { class: 'cdr-seeds__pts', text: `${fmtNum.format(Number(c.puntos))} pts` }) : null)));
}

/* ===========================================================================
   Carga
   =========================================================================== */
function mostrar(parte) {
  $('#cdr-skeleton').hidden = parte !== 'cargando';
  $('#cdr-error').hidden = parte !== 'error';
  $('#cdr-vacio').hidden = parte !== 'vacio';
  $('#cdr-no-publicado').hidden = parte !== 'no-publicado';
  $('#cdr-vista').hidden = parte !== 'vista';
  $('#cdr-contenido').setAttribute('aria-busy', String(parte === 'cargando'));
}

function escribirUrl(sel, { reemplazar = false } = {}) {
  const url = urlCuadro(estado.torneo?.id ?? estado.torneoId, sel);
  try {
    if (reemplazar) history.replaceState({ sel }, '', url);
    else history.pushState({ sel }, '', url);
  } catch { /* file:// */ }
}

async function irA(sel, { historial = true } = {}) {
  estado.sel = sel;
  if (historial) escribirUrl(sel);
  await cargarCuadro();
}

async function cargarCuadro() {
  const id = ++estado.peticion;
  mostrar('cargando');
  const sel = estado.sel;
  try {
    const d = await get(rutaApi(estado.torneoId, sel));
    if (id !== estado.peticion) return;
    estado.datos = d;
    estado.torneo = { ...(estado.torneo ?? {}), ...(d.torneo ?? {}), id: estado.torneoId };
    const otros = lista(d.otrosCuadros).length ? lista(d.otrosCuadros) : estado.indice;
    if (otros.length) estado.indice = otros;
    pintarSelector(estado.indice, sel, estado.torneoId);

    const m = construir(d);
    pintarCabecera(d, m);
    if (d.publicado === false) {
      mostrar('no-publicado');
      return;
    }
    mostrar('vista');
    $('#cdr-liguilla').hidden = d.formato !== 'liguilla';
    $('.cdr-seg').hidden = d.formato === 'liguilla';
    $('#cdr-leyenda').hidden = d.formato === 'liguilla';
    if (d.formato === 'liguilla') pintarLiguilla(d, m);
    else {
      pintarCuadro(m);
      pintarRondas(m);
    }
    pintarCampeon(m);
    pintarExtras(d);
    $('#cdr-buscar').placeholder = m.esEquipos ? 'Buscar equipo…' : 'Buscar jugador o pareja…';
    $('#cdr-buscar-etiqueta').textContent = m.esEquipos ? 'Buscar equipo' : 'Buscar jugador o pareja';
    ponerVista(estado.vista);
    $('#cdr-bracket').scrollLeft = 0;
    actualizarFlechas();

    // Enlace antiguo a EquiposEnfrentamientos.asp: se abre esa eliminatoria.
    const elim = estado.eliminatoriaPendiente;
    estado.eliminatoriaPendiente = null;
    if (elim && d.formato !== 'liguilla') {
      const r = m.rondas.find((x) => x.avance === elim.avance);
      const e = r?.encuentros.find((x) => x.posicion === elim.posicion);
      if (e && esReal(e.arriba) && esReal(e.abajo)) abrirEliminatoria(e, r);
    }
  } catch (err) {
    if (id !== estado.peticion) return;
    console.warn('[FEP] cuadro:', err.message);
    $('#cdr-error-enlace').href = urlWebActual(estado.torneoId, sel, estado.torneo?.tipo);
    mostrar('error');
  }
}

async function iniciarCarga() {
  const { torneo, cuadro, eliminatoria } = leerUrl();
  estado.torneoId = torneo;
  estado.eliminatoriaPendiente = eliminatoria;
  if (!torneo) {
    $('#cdr-error-enlace').href = urlWebActual(null);
    mostrar('error');
    return;
  }
  if (cuadro) {
    estado.sel = cuadro;
    escribirUrl(cuadro, { reemplazar: true });
    await cargarCuadro();
    return;
  }
  // Solo el torneo: índice de cuadros y el primero publicado.
  try {
    const d = await get(`/api/v1/competicion/cuadros/${torneo}`);
    estado.torneo = { ...(d?.torneo ?? {}), id: torneo };
    estado.indice = lista(d?.cuadros);
    const elegido = estado.indice.filter((c) => c.publicado !== false).sort(ordenItems)[0] ?? estado.indice.slice().sort(ordenItems)[0];
    if (!elegido) {
      pintarCabecera({ torneo: d?.torneo }, { part: new Map(), rondas: [] });
      $('#cdr-eyebrow').textContent = 'Cuadros y resultados';
      mostrar('vacio');
      return;
    }
    estado.sel = elegido;
    escribirUrl(elegido, { reemplazar: true });
    await cargarCuadro();
  } catch (err) {
    console.warn('[FEP] cuadros del torneo:', err.message);
    $('#cdr-error-enlace').href = urlWebActual(torneo, null, null);
    mostrar('error');
  }
}

/* ===========================================================================
   Arranque
   =========================================================================== */
function init() {
  estado.vista = vistaInicial();
  for (const b of $$('.cdr-seg__btn')) b.addEventListener('click', () => ponerVista(b.dataset.vista, { guardar: true }));

  const bracket = $('#cdr-bracket');
  bracket.addEventListener('scroll', actualizarFlechas, { passive: true });
  addEventListener('resize', actualizarFlechas, { passive: true });
  for (const b of $$('.cdr-scroll')) {
    b.addEventListener('click', () => bracket.scrollBy({ left: Number(b.dataset.dir) * bracket.clientWidth * 0.8, behavior: 'smooth' }));
  }

  const buscar = $('#cdr-buscar');
  buscar.addEventListener('input', () => aplicarBusqueda());
  buscar.addEventListener('keydown', (ev) => { if (ev.key === 'Enter') { ev.preventDefault(); aplicarBusqueda({ desplazar: true }); } });

  $('#cdr-imprimir').addEventListener('click', () => {
    // Al imprimir se usa la vista de cuadro completa.
    const antes = estado.vista;
    if (estado.datos?.formato !== 'liguilla') ponerVista('cuadro');
    $('#cdr-notas').open = true;
    window.print();
    ponerVista(antes);
  });
  $('#cdr-compartir').addEventListener('click', async () => {
    const url = location.href;
    const estadoTxt = $('#cdr-buscar-estado');
    try {
      if (navigator.share) await navigator.share({ title: document.title, url });
      else { await navigator.clipboard.writeText(url); estadoTxt.textContent = 'Enlace copiado'; }
    } catch { /* cancelado */ }
  });

  initResaltado();
  initTeclasPestanas();
  initDialogo();
  addEventListener('popstate', () => {
    const { cuadro } = leerUrl();
    if (cuadro && !mismo(cuadro, estado.sel)) irA(cuadro, { historial: false });
  });
  iniciarCarga();
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
})();
