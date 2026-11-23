/**
 * Prototipo 003 — Área privada: "Mi Club" (mi-club.html) y "Mi Portal" (mi-portal.html).
 *
 * Son dos páginas AISLADAS de la web, con aspecto de dashboard: sin cabecera, menú, pie ni asistente de la web
 * (no cargan comun.js). Menú lateral con las secciones; cada sección es una vista (#resumen, #licencias…) y
 * "Resumen" es el cuadro de mando con cifras y widgets.
 *
 * Una sola lógica para las dos páginas; <body data-area="club|portal"> dice cuál es.
 *   - Mi Club   → GET /api/v1/area-privada/club/{id}    (ID de club)
 *   - Mi Portal → GET /api/v1/area-privada/perfil/{id}  (nº de licencia); la vista depende de "tipo":
 *                 "jugador" o "arbitro".
 * Si el ID es del otro tipo (una licencia en Mi Club o un club en Mi Portal) se avisa y se ofrece la otra página.
 *
 * PROTOTIPO: no hay inicio de sesión real. Se entra con un ID de ejemplo y se cargan datos de prueba
 * (mock/area-privada-*.json, personas y club ficticios). Las acciones (confirmar designaciones, guardar datos,
 * descargar certificados…) se ven en pantalla pero no se envían a ningún sitio. En producción la API exigirá
 * la sesión de la persona o del club (usuario y contraseña FEP) y solo devolverá su propio panel.
 * El ID elegido se recuerda en sessionStorage (solo en esta pestaña) y en la URL (?id=) para poder enlazarlo.
 *
 * Todo texto que llega de la API se pinta con textContent / atributos (el()), nunca con innerHTML.
 */
(function () {
'use strict';

const body = document.body;
const AREA = body.dataset.area;
if (AREA !== 'club' && AREA !== 'portal') return;

/* ---------------------------------------------------------------------------
   Utilidades propias (la página no carga comun.js)
   --------------------------------------------------------------------------- */
const $ = (s, raiz = document) => raiz.querySelector(s);
const $$ = (s, raiz = document) => [...raiz.querySelectorAll(s)];
function el(tag, props = {}, ...hijos) {
  const n = document.createElement(tag);
  for (const [k, v] of Object.entries(props)) {
    if (v == null || v === false) continue;
    if (k === 'class') n.className = v;
    else if (k === 'text') n.textContent = v;
    else if (k.startsWith('on')) n.addEventListener(k.slice(2), v);
    else n.setAttribute(k, v === true ? '' : String(v));
  }
  for (const h of hijos.flat()) {
    if (h == null || h === false) continue;
    n.append(h instanceof Node ? h : document.createTextNode(String(h)));
  }
  return n;
}
/** "2026-11-06" → Date local (sin desfase de zona horaria). */
function aFecha(valor) {
  if (!valor) return null;
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(valor));
  return m ? new Date(+m[1], +m[2] - 1, +m[3]) : null;
}
const get = (ruta) => window.FEPApi.get(ruta);

const CLAVE_SESION = `fep003-area-${AREA}`;
const ID_VALIDO = /^[A-Za-z0-9-]{3,12}$/;
const RUTA = {
  club: (id) => `/api/v1/area-privada/club/${encodeURIComponent(id)}`,
  portal: (id) => `/api/v1/area-privada/perfil/${encodeURIComponent(id)}`,
};
const PAGINA = { club: 'mi-club.html', portal: 'mi-portal.html' };
const NOMBRE_AREA = { club: 'Mi Club', portal: 'Mi Portal' };

const fmtDia = new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'short', year: 'numeric' });
const fmtDiaCorto = new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'short' });
const fmtMes = new Intl.DateTimeFormat('es-ES', { month: 'long', year: 'numeric' });
const fmtMesCorto = new Intl.DateTimeFormat('es-ES', { month: 'short' });
const fmtNum = new Intl.NumberFormat('es-ES');

const fecha = (v) => { const f = aFecha(v); return f ? fmtDia.format(f).replace('.', '') : '—'; };
function rango(ini, fin) {
  const a = aFecha(ini); const b = aFecha(fin) ?? a;
  if (!a) return 'Fecha por confirmar';
  if (a.toDateString() === b.toDateString()) return fmtDia.format(a).replace('.', '');
  return `${fmtDiaCorto.format(a).replace('.', '')} – ${fmtDia.format(b).replace('.', '')}`;
}
const slug = (t) => String(t ?? '').toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
const estado = (t) => el('span', { class: `ap-estado ap-estado--${slug(t)}`, text: t });
const iniciales = (t) => String(t ?? '').split(/\s+/).filter((p) => /^[A-ZÁÉÍÓÚÑ]/i.test(p) && !/^(de|del|la|las|los|y)$/i.test(p)).slice(0, 2).map((p) => p[0]).join('').toUpperCase();

/** Enlace de los datos: solo páginas del prototipo (algo.html?…) o https. Si no, null. */
function enlaceSeguro(v) {
  if (!v) return null;
  if (/^[a-z0-9-]+\.html(\?[a-z0-9=&_-]*)?$/i.test(v)) return { href: v };
  try {
    const u = new URL(v);
    return u.protocol === 'https:' ? { href: u.href, target: '_blank', rel: 'noopener' } : null;
  } catch { return null; }
}
function enlace(v, texto, clase) {
  const e = enlaceSeguro(v);
  if (!e) return el('span', { class: clase, text: texto });
  return el('a', { ...e, class: clase }, texto, e.target ? el('span', { class: 'sr-only', text: ' (se abre en una pestaña nueva)' }) : null);
}

/** Aviso de acción simulada (no se guarda nada). */
function demo(texto) {
  const caja = $('#ap-aviso');
  caja.textContent = `${texto} En este prototipo la acción no se envía ni se guarda.`;
  caja.hidden = false;
  clearTimeout(demo.t);
  demo.t = setTimeout(() => { caja.hidden = true; }, 6000);
}
const botonDemo = (texto, accion, clase = 'btn btn--outline btn--sm') =>
  el('button', { type: 'button', class: clase, onclick: () => demo(accion) }, texto);

/* ---------------------------------------------------------------------------
   Piezas del dashboard
   --------------------------------------------------------------------------- */
/** Vista del dashboard. `insignia`: número que se muestra junto a la sección en el menú (p. ej. pendientes). */
function seccion(id, titulo, opciones, ...contenido) {
  const s = el('section', { class: 'ap-section', id: `vista-${id}`, 'aria-labelledby': 'titulo-pagina', hidden: true },
    ...contenido.filter(Boolean));
  s.dataset.vista = id;
  s.dataset.indice = titulo;
  if (opciones?.insignia) s.dataset.insignia = String(opciones.insignia);
  if (opciones?.desc) s.dataset.desc = opciones.desc;      // texto de la card en el inicio
  if (opciones?.dato) s.dataset.dato = opciones.dato;      // dato destacado de la card
  return s;
}
/** Hueco del inicio donde se pintan las cards de todas las secciones (lo rellena cargar()). */
const huecoCards = () => el('nav', { class: 'dash-cards', 'aria-label': 'Secciones', 'data-cards': '' });
const irA = (vista, texto) => el('a', { class: 'dash-widget__link', href: `#${vista}` }, texto);

/** Fila de cifras clave. Devuelve también los <strong> para poder actualizarlos. */
function kpis(lista_) {
  const valores = [];
  const ul = el('ul', { class: 'dash-kpis', 'aria-label': 'Cifras clave' }, ...lista_.map(([valor, etiqueta, nota]) => {
    const fuerte = el('strong', { class: 'dash-kpi__valor', text: valor });
    valores.push(fuerte);
    return el('li', { class: 'dash-kpi' }, el('span', { class: 'dash-kpi__etiqueta', text: etiqueta }), fuerte,
      nota ? el('span', { class: 'dash-kpi__nota' }, nota) : null);
  }));
  return { ul, valores };
}

function widget(titulo, { ancho = false, enlace: link = null } = {}, ...cuerpo) {
  return el('article', { class: `dash-widget${ancho ? ' dash-widget--ancho' : ''}` },
    el('header', { class: 'dash-widget__cab' }, el('h2', { class: 'dash-widget__titulo', text: titulo }), link),
    ...cuerpo.filter(Boolean));
}
const vacio = (t) => el('p', { class: 'dash-vacio', text: t });

const dato = (etiqueta, valor) => [el('dt', { text: etiqueta }), el('dd', {}, valor ?? '—')];
const lista = (pares) => el('dl', { class: 'ap-dl' }, ...pares.flat());

function tabla(caption, cabeceras, filas) {
  return el('div', { class: 'ap-table-wrap' },
    el('table', { class: 'ap-table' },
      el('caption', { class: 'sr-only', text: caption }),
      el('thead', {}, el('tr', {}, ...cabeceras.map((c) => el('th', { scope: 'col', text: c })))),
      el('tbody', {}, ...filas)));
}

function documentos(docs, contexto) {
  return el('ul', { class: 'ap-docs' }, ...(docs ?? []).map((d) => {
    const e = enlaceSeguro(d.enlace);
    return el('li', { class: 'ap-doc' },
      el('span', { class: 'ap-doc__icon', 'aria-hidden': 'true', text: d.tipo || 'DOC' }),
      el('span', { class: 'ap-doc__title', text: d.titulo }),
      e ? el('a', { ...e, class: 'btn btn--outline btn--sm' }, 'Abrir', e.target ? el('span', { class: 'sr-only', text: ' (se abre en una pestaña nueva)' }) : null)
        : botonDemo('Descargar', `Descarga de "${d.titulo}" (${contexto}).`));
  }));
}

function avisos(lista_) {
  if (!lista_?.length) return vacio('No tienes avisos nuevos.');
  return el('ul', { class: 'ap-avisos' }, ...lista_.map((a) => el('li', {},
    el('time', { datetime: a.fecha, text: fecha(a.fecha) }), el('span', { text: a.texto }))));
}

/** Lista compacta para widgets: fecha + título + meta + estado. */
function mini(items) {
  if (!items.length) return vacio('Nada pendiente.');
  return el('ul', { class: 'dash-mini' }, ...items.map((i) => el('li', {},
    i.fecha ? el('span', { class: 'dash-mini__fecha', text: i.fecha }) : null,
    el('span', { class: 'dash-mini__main' }, el('strong', { text: i.titulo }), i.meta ? el('span', { text: i.meta }) : null),
    i.estado ? estado(i.estado) : null)));
}

/** Barras horizontales (reparto por estado). */
function barras(items, total) {
  return el('ul', { class: 'dash-barras' }, ...items.map(([etiqueta, n, clase]) => el('li', {},
    el('span', { class: 'dash-barras__etiqueta', text: etiqueta }),
    el('span', { class: 'dash-barras__pista', 'aria-hidden': 'true' },
      el('span', { class: `dash-barras__relleno dash-barras__relleno--${clase}`, style: `width: ${total ? Math.round((n / total) * 100) : 0}%` })),
    el('strong', { class: 'dash-barras__n', text: fmtNum.format(n) }))));
}

function pintarUsuario({ nombre, tipoTexto, id, sub, variante }) {
  body.classList.remove('ap-003--club', 'ap-003--jugador', 'ap-003--arbitro');
  body.classList.add(`ap-003--${variante}`);
  const ini = iniciales(nombre);
  $('#ap-usuario').replaceChildren(
    el('span', { class: 'dash-user__avatar', 'aria-hidden': 'true', text: ini }),
    el('span', { class: 'dash-user__datos' },
      el('span', { class: 'dash-user__tipo', text: tipoTexto }),
      el('strong', { class: 'dash-user__nombre', text: nombre }),
      el('span', { class: 'dash-user__id', text: `ID ${id}${sub ? ` · ${sub}` : ''}` })));
  $('#ap-avatar').textContent = ini;
  $('#ap-miga').textContent = `${NOMBRE_AREA[AREA]} · ${nombre}`;
  document.title = `${NOMBRE_AREA[AREA]} · ${nombre} · Federación Española de Pádel`;
}

/* ---------------------------------------------------------------------------
   MI CLUB
   --------------------------------------------------------------------------- */
function panelClub(d) {
  const lic = d.licencias ?? [];
  const cuenta = (e) => lic.filter((l) => l.estado === e).length;
  const activas = cuenta('activa');
  const tramitesPend = (d.tramites ?? []).filter((t) => t.estado === 'pendiente');
  const hoy = new Date();
  const torneosTemp = (d.torneos ?? []).filter((t) => String(aFecha(t.fechaFin)?.getFullYear()) === String(d.temporada)).length;
  pintarUsuario({ nombre: d.nombre, tipoTexto: 'Club', id: d.id, sub: d.localidad, variante: 'club' });

  // Licencias: filtro por texto y estado
  const filtroTexto = el('input', { type: 'search', id: 'ap-lic-q', placeholder: 'Nombre o nº de licencia', autocomplete: 'off' });
  const filtroEstado = el('select', { id: 'ap-lic-estado' },
    el('option', { value: '', text: 'Todos los estados' }), ...['activa', 'pendiente', 'caducada'].map((e) => el('option', { value: e, text: e[0].toUpperCase() + e.slice(1) })));
  const cuerpo = el('tbody');
  const cuentaLic = el('p', { class: 'ap-cuenta', role: 'status', 'aria-live': 'polite' });
  const pintarLicencias = () => {
    const q = filtroTexto.value.trim().toLowerCase();
    const e = filtroEstado.value;
    const filas = lic.filter((l) => (!e || l.estado === e) && (!q || l.jugador.toLowerCase().includes(q) || l.licencia.includes(q)));
    cuerpo.replaceChildren(...filas.map((l) => el('tr', {},
      el('td', { 'data-label': 'Licencia', text: l.licencia }),
      el('th', { scope: 'row', text: l.jugador }),
      el('td', { 'data-label': 'Categoría', text: l.categoria ?? '—' }),
      el('td', { 'data-label': 'Género', text: l.genero === 'F' ? 'Femenino' : l.genero === 'M' ? 'Masculino' : '—' }),
      el('td', { 'data-label': 'Estado' }, estado(l.estado)),
      el('td', { 'data-label': 'Vence', text: l.vigenteHasta ? fecha(l.vigenteHasta) : '—' }))));
    cuentaLic.textContent = `${filas.length} de ${lic.length} licencias`;
  };
  filtroTexto.addEventListener('input', pintarLicencias);
  filtroEstado.addEventListener('change', pintarLicencias);
  pintarLicencias();

  const c = d.contacto ?? {};
  const campo = (id, etiqueta, valor, tipo = 'text') => el('div', { class: 'ap-field' },
    el('label', { for: id, text: etiqueta }), el('input', { id, type: tipo, value: valor ?? '' }));

  const proximos = [
    ...(d.equipos ?? []).map((e) => ({ ini: e.fechaInicio, fin: e.fechaFin, titulo: e.competicion, meta: `Equipo · ${e.nombre}`, estado: e.estado })),
    ...(d.torneos ?? []).map((t) => ({ ini: t.fechaInicio, fin: t.fechaFin, titulo: t.titulo, meta: `Torneo organizado · ${t.categoria}`, estado: t.estado })),
  ].filter((x) => (aFecha(x.fin) ?? hoy) >= hoy).sort((a, b) => aFecha(a.ini) - aFecha(b.ini))
    .map((x) => ({ ...x, fecha: rango(x.ini, x.fin) }));

  const { ul: cifras } = kpis([
    [`${activas}/${lic.length}`, 'Licencias activas'],
    [fmtNum.format((d.equipos ?? []).length), 'Equipos inscritos'],
    [fmtNum.format(torneosTemp), `Torneos ${d.temporada}`],
    [String(tramitesPend.length), 'Trámites pendientes'],
  ]);

  const secciones = [
    seccion('resumen', 'Inicio', null,
      cifras,
      huecoCards(),
      el('div', { class: 'dash-grid' },
        widget('Avisos', {}, avisos(d.avisos)),
        widget('Próximas competiciones', { enlace: irA('equipos', 'Ver equipos →') }, mini(proximos)))),
    seccion('tramites', 'Trámites', { insignia: tramitesPend.length, desc: 'Altas, renovaciones y gestiones del club.', dato: `${tramitesPend.length} pendientes` },
      el('ul', { class: 'ap-rows' }, ...(d.tramites ?? []).map((t) => el('li', { class: 'ap-row' },
        el('div', { class: 'ap-row__main' }, el('strong', { text: t.titulo }),
          el('span', { class: 'ap-row__meta', text: t.detalle || (t.fechaLimite ? `Hasta el ${fecha(t.fechaLimite)}` : '') })),
        estado(t.estado),
        t.estado === 'pendiente' ? botonDemo('Iniciar trámite', `Inicio del trámite "${t.titulo}".`) : null)))),
    seccion('licencias', 'Licencias del club', { desc: 'Licencias de los jugadores y jugadoras del club.', dato: `${activas} de ${lic.length} activas` },
      el('div', { class: 'ap-toolbar' },
        el('div', { class: 'ap-field' }, el('label', { for: 'ap-lic-q', text: 'Buscar' }), filtroTexto),
        el('div', { class: 'ap-field' }, el('label', { for: 'ap-lic-estado', text: 'Estado' }), filtroEstado),
        botonDemo('Dar de alta una licencia', 'Alta de una licencia nueva en el club.', 'btn btn--red btn--sm')),
      cuentaLic,
      el('div', { class: 'ap-table-wrap' }, el('table', { class: 'ap-table' },
        el('caption', { class: 'sr-only', text: 'Licencias del club' }),
        el('thead', {}, el('tr', {}, ...['Licencia', 'Jugador/a', 'Categoría', 'Género', 'Estado', 'Vence'].map((t) => el('th', { scope: 'col', text: t })))),
        cuerpo))),
    seccion('equipos', 'Equipos inscritos', { desc: 'Equipos del club en competiciones por equipos.', dato: `${(d.equipos ?? []).length} equipos` },
      el('ul', { class: 'ap-rows' }, ...(d.equipos ?? []).map((e) => el('li', { class: 'ap-row' },
        el('p', { class: 'ap-fecha', text: rango(e.fechaInicio, e.fechaFin) }),
        el('div', { class: 'ap-row__main' }, enlace(e.enlace, e.competicion, 'ap-row__link'),
          el('span', { class: 'ap-row__meta', text: `${e.nombre} · ${e.categoria}` })),
        estado(e.estado))))),
    seccion('torneos', 'Torneos organizados', { desc: 'Torneos que organiza el club.', dato: `${torneosTemp} en ${d.temporada}` },
      el('ul', { class: 'ap-rows' }, ...(d.torneos ?? []).map((t) => el('li', { class: 'ap-row' },
        el('p', { class: 'ap-fecha', text: rango(t.fechaInicio, t.fechaFin) }),
        el('div', { class: 'ap-row__main' }, el('strong', { text: t.titulo }),
          el('span', { class: 'ap-row__meta', text: `${t.categoria}${t.puntuable ? ' · puntuable para el ranking' : ''}` })),
        estado(t.estado)))),
      el('p', { class: 'ap-acciones' }, el('a', { class: 'btn btn--red btn--sm', href: 'organizacion-torneos.html' }, 'Organización de torneos FEP →'))),
    seccion('datos', 'Datos del club', { desc: 'Contacto, dirección e instalaciones.', dato: `${d.instalaciones?.pistas ?? '—'} pistas` },
      el('form', { class: 'ap-form', onsubmit: (e) => { e.preventDefault(); demo('Cambios en los datos del club.'); } },
        el('div', { class: 'ap-form__grid' },
          campo('ap-presidente', 'Presidente/a', c.presidente), campo('ap-email', 'Correo electrónico', c.email, 'email'),
          campo('ap-telefono', 'Teléfono', c.telefono, 'tel'), campo('ap-web', 'Web', c.web, 'url'),
          campo('ap-direccion', 'Dirección', c.direccion), campo('ap-cp', 'Código postal', c.codigoPostal)),
        el('p', { class: 'ap-form__nota', text: `Instalaciones: ${d.instalaciones?.pistas ?? '—'} pistas (${d.instalaciones?.cubiertas ?? 0} cubiertas).` }),
        el('button', { type: 'submit', class: 'btn btn--red btn--sm' }, 'Guardar cambios'))),
    seccion('documentos', 'Documentos', { desc: 'Certificados y documentación del club.', dato: `${(d.documentos ?? []).length} documentos` }, documentos(d.documentos, 'Mi Club')),
  ];
  return secciones;
}

/* ---------------------------------------------------------------------------
   MI PORTAL · JUGADOR/A
   --------------------------------------------------------------------------- */
function graficoRanking(evol, compacto = false) {
  if (!evol?.length) return vacio('Sin datos de ranking todavía.');
  const peor = Math.max(...evol.map((e) => e.posicion), 1);
  const mejor = Math.min(...evol.map((e) => e.posicion), peor);
  return el('figure', { class: `ap-chart${compacto ? ' ap-chart--compacto' : ''}` },
    el('ol', { class: 'ap-chart__barras' }, ...evol.map((e) => {
      const alto = 30 + Math.round(((peor - e.posicion) / Math.max(1, peor - mejor)) * 70);
      return el('li', { style: `--h: ${alto}%` },
        el('span', { class: 'ap-chart__valor', text: `${e.posicion}.º` }),
        el('span', { class: 'ap-chart__barra', 'aria-hidden': 'true' }),
        el('span', { class: 'ap-chart__mes', text: e.mes }));
    })),
    el('figcaption', { text: 'Puesto en el ranking los últimos meses (más alta, mejor puesto).' }));
}

function panelJugador(d) {
  const l = d.licencia ?? {};
  const r = d.ranking ?? {};
  const p = d.balance?.partidos ?? {};
  const total = (p.ganados ?? 0) + (p.perdidos ?? 0);
  const pct = total ? Math.round((p.ganados / total) * 100) : null;
  const nombre = `${d.nombre} ${d.apellidos}`;
  pintarUsuario({ nombre, tipoTexto: 'Jugador/a', id: d.id, sub: d.categoria, variante: 'jugador' });
  const variacion = r.variacion > 0 ? el('span', { class: 'ap-sube', text: `▲ ${r.variacion} puestos` })
    : r.variacion < 0 ? el('span', { class: 'ap-baja', text: `▼ ${-r.variacion} puestos` }) : 'Sin cambios';
  const abiertas = (d.inscripciones ?? []).filter((i) => i.estado === 'plazo abierto').length;

  const { ul: cifras } = kpis([
    [r.posicion != null ? `${r.posicion}.º` : '—', 'Ranking', variacion],
    [fmtNum.format(r.puntos ?? 0), 'Puntos'],
    [pct != null ? `${pct} %` : '—', 'Partidos ganados', `${p.ganados ?? 0} G · ${p.perdidos ?? 0} P`],
    [String(d.balance?.torneos ?? '—'), 'Torneos jugados'],
  ]);

  const secciones = [
    seccion('resumen', 'Inicio', null,
      cifras,
      huecoCards(),
      el('div', { class: 'dash-grid' },
        widget('Avisos', {}, avisos(d.avisos)),
        widget('Próximas inscripciones', { enlace: irA('inscripciones', 'Ver inscripciones →') },
          mini((d.inscripciones ?? []).map((i) => ({ fecha: rango(i.fechaInicio, i.fechaFin), titulo: i.titulo, meta: i.categoria, estado: i.estado })))))),
    seccion('licencia', 'Mi licencia', { desc: 'Tu licencia federativa, renovación y cambio de club.', dato: `${l.estado ?? '—'} · hasta ${fecha(l.vigenteHasta)}` },
      lista([dato('Número', l.numero), dato('Tipo', l.tipo), dato('Estado', estado(l.estado)), dato('Vigente hasta', fecha(l.vigenteHasta)),
        dato('Club', l.club), dato('Federación', l.federacion), dato('Categoría', d.categoria), dato('Seguro', l.seguro)]),
      el('p', { class: 'ap-acciones' },
        botonDemo(`Renovar licencia ${Number(l.temporada) + 1}`, 'Renovación de la licencia.', 'btn btn--red btn--sm'),
        botonDemo('Cambiar de club', 'Solicitud de cambio de club.'))),
    seccion('ranking', 'Mi ranking', { desc: 'Tu posición y su evolución en los últimos meses.', dato: r.posicion != null ? `${r.posicion}.º · ${fmtNum.format(r.puntos ?? 0)} puntos` : '' },
      el('p', { class: 'ap-ranking' }, el('strong', { text: `${r.posicion}.º` }), ` en el ${r.nombre} · ${fmtNum.format(r.puntos)} puntos en ${r.torneosRanking} torneos`),
      graficoRanking(r.evolucion),
      el('p', { class: 'ap-acciones' }, enlace(r.enlace, 'Ver el ranking completo →', 'btn btn--outline btn--sm'))),
    seccion('inscripciones', 'Mis inscripciones', { insignia: abiertas, desc: 'Torneos en los que estás inscrito y plazos abiertos.', dato: `${(d.inscripciones ?? []).length} inscripciones` },
      el('ul', { class: 'ap-rows' }, ...(d.inscripciones ?? []).map((i) => el('li', { class: 'ap-row' },
        el('p', { class: 'ap-fecha', text: rango(i.fechaInicio, i.fechaFin) }),
        el('div', { class: 'ap-row__main' }, enlace(i.enlace, i.titulo, 'ap-row__link'),
          el('span', { class: 'ap-row__meta', text: [i.categoria, i.sede, i.pareja ? `con ${i.pareja}` : null].filter(Boolean).join(' · ') })),
        estado(i.estado),
        i.estado === 'plazo abierto' ? botonDemo('Inscribirme', `Inscripción en "${i.titulo}".`, 'btn btn--red btn--sm') : null))),
      el('p', { class: 'ap-acciones' }, el('a', { class: 'btn btn--outline btn--sm', href: 'campeonatos.html' }, 'Buscar campeonatos y torneos →'))),
    seccion('historial', 'Mis resultados', { desc: 'Resultados y puntos de la temporada.', dato: `${(d.historial ?? []).length} torneos` },
      tabla('Resultados de la temporada', ['Torneo', 'Fecha', 'Ronda', 'Puntos'], (d.historial ?? []).map((h) => el('tr', {},
        el('th', { scope: 'row', text: h.titulo }), el('td', { 'data-label': 'Fecha', text: fecha(h.fecha) }),
        el('td', { 'data-label': 'Ronda', text: h.ronda }), el('td', { 'data-label': 'Puntos', class: 'ap-num', text: fmtNum.format(h.puntos) }))))),
    seccion('certificados', 'Certificados', { desc: 'Descarga tus certificados federativos.', dato: `${(d.certificados ?? []).length} disponibles` }, documentos(d.certificados, 'Mi Portal')),
  ];
  return secciones;
}

/* ---------------------------------------------------------------------------
   MI PORTAL · JUEZ-ÁRBITRO
   --------------------------------------------------------------------------- */
function panelArbitro(d) {
  const l = d.licencia ?? {};
  const desig = (d.designaciones ?? []).map((x) => ({ ...x }));   // copia: se cambia el estado en pantalla
  const hoy = new Date();
  const proxima = desig.filter((x) => (aFecha(x.fechaFin) ?? hoy) >= hoy).sort((a, b) => aFecha(a.fechaInicio) - aFecha(b.fechaInicio))[0];
  const nombre = `${d.nombre} ${d.apellidos}`;
  pintarUsuario({ nombre, tipoTexto: 'Juez-árbitro', id: d.id, sub: l.nivel, variante: 'arbitro' });
  const pendientes = () => desig.filter((x) => x.estado === 'pendiente');

  const { ul: cifras, valores } = kpis([
    [String(d.resumen?.designaciones ?? '—'), 'Designaciones'],
    [String(d.resumen?.jornadas ?? '—'), 'Jornadas arbitradas'],
    [String(pendientes().length), 'Por confirmar'],
    [`${d.resumen?.formacionHoras ?? 0} h`, 'Formación'],
  ]);

  // Designaciones: lista completa (vista) y pendientes (widget del resumen); las dos se repintan al responder
  const listaDesig = el('ul', { class: 'ap-rows' });
  const widgetPend = el('div');
  const botones = (x) => el('span', { class: 'ap-row__btns' },
    el('button', { type: 'button', class: 'btn btn--red btn--sm', onclick: () => responder(x, 'confirmada') }, 'Confirmar'),
    el('button', { type: 'button', class: 'btn btn--outline btn--sm', onclick: () => responder(x, 'rechazada') }, 'No puedo'));
  const pintarDesig = () => {
    listaDesig.replaceChildren(...desig.map((x) => el('li', { class: 'ap-row' },
      el('p', { class: 'ap-fecha', text: rango(x.fechaInicio, x.fechaFin) }),
      el('div', { class: 'ap-row__main' }, enlace(x.enlace, x.torneo, 'ap-row__link'),
        el('span', { class: 'ap-row__meta', text: `${x.rol} · ${x.sede}` })),
      estado(x.estado),
      x.estado === 'pendiente' ? botones(x) : null)));
    const pend = pendientes();
    widgetPend.replaceChildren(pend.length
      ? el('ul', { class: 'dash-mini' }, ...pend.map((x) => el('li', {},
        el('span', { class: 'dash-mini__fecha', text: rango(x.fechaInicio, x.fechaFin) }),
        el('span', { class: 'dash-mini__main' }, el('strong', { text: x.torneo }), el('span', { text: x.rol })),
        botones(x))))
      : vacio('No tienes designaciones por confirmar.'));
    valores[2].textContent = String(pend.length);
    // Contador en el menú lateral y en la card del inicio
    for (const insignia of $$('[data-insignia-de="designaciones"]')) { insignia.textContent = String(pend.length); insignia.hidden = !pend.length; }
    const datoCard = $('[data-dato-de="designaciones"]');
    if (datoCard) datoCard.textContent = `${pend.length} por confirmar`;
  };
  const responder = (x, nuevo) => {
    x.estado = nuevo;
    pintarDesig();
    demo(`Designación "${x.torneo}" marcada como ${nuevo}.`);
    $('#contenido').focus({ preventScroll: true });
  };
  pintarDesig();

  const meses = (d.disponibilidad ?? []).map((m) => ({ ...m }));
  const chips = el('ul', { class: 'dash-chips' });
  const pintarChips = () => chips.replaceChildren(...meses.map((m) => {
    const f = aFecha(`${m.mes}-01`);
    return el('li', { class: m.disponible ? 'is-si' : 'is-no' }, f ? fmtMesCorto.format(f).replace('.', '') : m.mes,
      el('span', { class: 'sr-only', text: m.disponible ? ': disponible' : ': no disponible' }));
  }));
  pintarChips();
  const disp = el('ul', { class: 'ap-meses' }, ...meses.map((m) => {
    const f = aFecha(`${m.mes}-01`);
    const b = el('button', { type: 'button', class: 'ap-mes', 'aria-pressed': String(m.disponible) },
      el('span', { class: 'ap-mes__nombre', text: f ? fmtMes.format(f) : m.mes }),
      el('span', { class: 'ap-mes__estado', text: m.disponible ? 'Disponible' : 'No disponible' }));
    b.addEventListener('click', () => {
      m.disponible = !m.disponible;
      b.setAttribute('aria-pressed', String(m.disponible));
      b.lastChild.textContent = m.disponible ? 'Disponible' : 'No disponible';
      pintarChips();
    });
    return el('li', {}, b);
  }));

  const secciones = [
    seccion('resumen', 'Inicio', null,
      cifras,
      huecoCards(),
      el('div', { class: 'dash-grid' },
        widget('Próxima designación', { enlace: irA('designaciones', 'Ver todas →') },
          proxima ? el('div', { class: 'dash-proxima' },
            el('p', { class: 'dash-proxima__fecha', text: rango(proxima.fechaInicio, proxima.fechaFin) }),
            enlace(proxima.enlace, proxima.torneo, 'dash-proxima__titulo'),
            el('p', { class: 'dash-proxima__meta', text: `${proxima.rol} · ${proxima.sede}` }),
            estado(proxima.estado)) : vacio('Sin designaciones próximas.')),
        widget('Avisos', {}, avisos(d.avisos)))),
    seccion('designaciones', 'Mis designaciones', { insignia: pendientes().length, desc: 'Torneos asignados: confírmalos o indica que no puedes.', dato: `${pendientes().length} por confirmar` }, listaDesig),
    seccion('disponibilidad', 'Disponibilidad', { desc: 'Meses en los que puedes arbitrar.', dato: `${meses.filter((m) => m.disponible).length} de ${meses.length} meses` },
      el('p', { class: 'ap-intro', text: 'Marca los meses en los que puedes arbitrar. El comité tiene en cuenta tu disponibilidad al hacer las designaciones.' }),
      disp,
      el('p', { class: 'ap-acciones' }, el('button', { type: 'button', class: 'btn btn--red btn--sm', onclick: () => demo('Disponibilidad actualizada.') }, 'Guardar disponibilidad'))),
    seccion('historial', 'Torneos arbitrados', { desc: 'Torneos arbitrados esta temporada y sus actas.', dato: `${(d.historial ?? []).length} torneos` },
      tabla('Torneos arbitrados esta temporada', ['Torneo', 'Fecha', 'Función', 'Acta'], (d.historial ?? []).map((h) => el('tr', {},
        el('th', { scope: 'row', text: h.torneo }), el('td', { 'data-label': 'Fecha', text: fecha(h.fecha) }),
        el('td', { 'data-label': 'Función', text: h.rol }), el('td', { 'data-label': 'Acta' }, estado(h.acta)))))),
    seccion('formacion', 'Formación', { desc: 'Cursos y reciclaje de jueces-árbitro.', dato: `${d.resumen?.formacionHoras ?? 0} h esta temporada` },
      el('ul', { class: 'ap-rows' }, ...(d.formacion ?? []).map((f) => el('li', { class: 'ap-row' },
        el('p', { class: 'ap-fecha', text: fecha(f.fecha) }),
        el('div', { class: 'ap-row__main' }, enlace(f.enlace, f.titulo, 'ap-row__link'), el('span', { class: 'ap-row__meta', text: f.modalidad })),
        estado(f.estado))))),
    seccion('licencia', 'Mi licencia', { desc: 'Tu licencia de juez-árbitro y su renovación.', dato: `${l.estado ?? '—'} · hasta ${fecha(l.vigenteHasta)}` },
      lista([dato('Número', l.numero), dato('Tipo', l.tipo), dato('Nivel', l.nivel), dato('Estado', estado(l.estado)),
        dato('Vigente hasta', fecha(l.vigenteHasta)), dato('Federación', l.federacion), dato('Comité', l.comite)]),
      el('p', { class: 'ap-acciones' }, botonDemo(`Renovar licencia ${Number(l.temporada) + 1}`, 'Renovación de la licencia de juez-árbitro.', 'btn btn--red btn--sm'))),
    seccion('documentos', 'Documentos', { desc: 'Reglamentos, plantillas y certificados.', dato: `${(d.documentos ?? []).length} documentos` }, documentos(d.documentos, 'Mi Portal')),
  ];
  return secciones;
}

/* ---------------------------------------------------------------------------
   Vistas y menú lateral
   --------------------------------------------------------------------------- */
function vistaActual() {
  const h = decodeURIComponent(location.hash.slice(1));
  return $(`.ap-section[data-vista="${CSS.escape(h)}"]`) ? h : 'resumen';
}

function mostrarVista(id, { enfocar = false } = {}) {
  const vistas = $$('.ap-section');
  if (!vistas.length) return;
  let actual = null;
  for (const v of vistas) {
    const si = v.dataset.vista === id;
    v.hidden = !si;
    if (si) actual = v;
  }
  for (const a of $$('.dash-nav a')) {
    if (a.getAttribute('href') === `#${id}`) a.setAttribute('aria-current', 'page');
    else a.removeAttribute('aria-current');
  }
  $('#titulo-pagina').textContent = actual?.dataset.indice ?? 'Inicio';
  // En el inicio no hay menú lateral (todas las secciones están en cards); dentro de una sección, sí.
  body.classList.toggle('dash--inicio', id === 'resumen');
  cerrarMenu();
  window.scrollTo(0, 0);
  if (enfocar) $('#titulo-pagina').focus({ preventScroll: true });
}

function pintarMenu(secciones) {
  $('#ap-indice').replaceChildren(...secciones.map((s) => {
    const n = Number(s.dataset.insignia || 0);
    return el('li', {}, el('a', { href: `#${s.dataset.vista}` },
      el('span', { class: 'dash-nav__texto', text: s.dataset.indice }),
      el('span', { class: 'dash-nav__insignia', 'data-insignia-de': s.dataset.vista, 'aria-label': `${n} pendientes`, text: String(n), hidden: !n })));
  }));
}

/** Inicio: una card por cada sección (todas menos el propio inicio). */
function pintarCards(secciones) {
  const hueco = $('[data-cards]');
  if (!hueco) return;
  hueco.replaceChildren(el('ul', { class: 'dash-cards__lista' }, ...secciones.filter((s) => s.dataset.vista !== 'resumen').map((s) => {
    const n = Number(s.dataset.insignia || 0);
    return el('li', {}, el('a', { class: 'dash-card', href: `#${s.dataset.vista}` },
      el('span', { class: 'dash-card__cab' },
        el('span', { class: 'dash-card__titulo', text: s.dataset.indice }),
        el('span', { class: 'dash-nav__insignia dash-card__insignia', 'data-insignia-de': s.dataset.vista, 'aria-label': `${n} pendientes`, text: String(n), hidden: !n })),
      s.dataset.desc ? el('span', { class: 'dash-card__desc', text: s.dataset.desc }) : null,
      el('span', { class: 'dash-card__pie' },
        s.dataset.dato ? el('strong', { class: 'dash-card__dato', 'data-dato-de': s.dataset.vista, text: s.dataset.dato }) : el('span'),
        el('span', { class: 'dash-card__ir', 'aria-hidden': 'true', text: '→' }))));
  })));
}

function abrirMenu() {
  body.classList.add('dash--menu-abierto');
  $('#ap-menu').setAttribute('aria-expanded', 'true');
  $('#ap-velo').hidden = false;
  $('.dash-nav a[aria-current]')?.focus();
}
function cerrarMenu() {
  if (!body.classList.contains('dash--menu-abierto')) return;
  body.classList.remove('dash--menu-abierto');
  $('#ap-menu').setAttribute('aria-expanded', 'false');
  $('#ap-velo').hidden = true;
}

/* ---------------------------------------------------------------------------
   Acceso, carga y salida
   --------------------------------------------------------------------------- */
function guardarSesion(id) { try { sessionStorage.setItem(CLAVE_SESION, id); } catch { /* sin almacenamiento */ } }
function leerSesion() { try { return sessionStorage.getItem(CLAVE_SESION); } catch { return null; } }
function borrarSesion() { try { sessionStorage.removeItem(CLAVE_SESION); } catch { /* ignorar */ } }

function ponerIdEnUrl(id) {
  const url = new URL(location.href);
  if (id) url.searchParams.set('id', id); else { url.searchParams.delete('id'); url.hash = ''; }
  history.replaceState(null, '', url);
}

function mostrarAcceso(mensaje) {
  body.classList.add('ap-sin-sesion');
  body.classList.remove('dash--inicio');
  body.classList.remove('ap-003--club', 'ap-003--jugador', 'ap-003--arbitro');
  cerrarMenu();
  $('#ap-app').hidden = true;
  $('#ap-panel').replaceChildren();
  $('#ap-indice').replaceChildren();
  $('#ap-acceso').hidden = false;
  const err = $('#ap-error');
  err.hidden = !mensaje;
  err.replaceChildren(...(mensaje ? [].concat(mensaje) : []));
  document.title = `${NOMBRE_AREA[AREA]} · Federación Española de Pádel`;
}

async function cargar(id, { enfocar = true } = {}) {
  const err = $('#ap-error');
  const boton = $('#ap-entrar');
  boton.disabled = true;
  boton.textContent = 'Entrando…';
  try {
    let datos;
    try {
      datos = await get(RUTA[AREA](id));
    } catch {
      // ¿Es un ID del otro tipo? (una licencia en Mi Club o un club en Mi Portal)
      const otra = AREA === 'club' ? 'portal' : 'club';
      let delOtro = null;
      try { delOtro = await get(RUTA[otra](id)); } catch { /* tampoco */ }
      if (delOtro) {
        mostrarAcceso([`El ID ${id} es de ${otra === 'club' ? 'un club' : 'una licencia'}: su panel está en `,
          el('a', { href: `${PAGINA[otra]}?id=${encodeURIComponent(id)}` }, `${NOMBRE_AREA[otra]} →`)]);
      } else {
        mostrarAcceso(`No hay ningún ${AREA === 'club' ? 'club' : 'federado/a'} con el ID ${id}. Revisa el número o prueba con uno de los ID de ejemplo.`);
      }
      err.focus();
      return;
    }

    let secciones;
    if (AREA === 'club') secciones = panelClub(datos);
    else if (datos.tipo === 'jugador') secciones = panelJugador(datos);
    else if (datos.tipo === 'arbitro') secciones = panelArbitro(datos);
    else { mostrarAcceso(`El ID ${id} no tiene un panel de ${NOMBRE_AREA[AREA]}.`); return; }

    guardarSesion(id);
    ponerIdEnUrl(id);
    body.classList.remove('ap-sin-sesion');
    $('#ap-acceso').hidden = true;
    err.hidden = true;
    $('#ap-panel').replaceChildren(...secciones);
    pintarMenu(secciones);
    pintarCards(secciones);
    $('#ap-app').hidden = false;
    mostrarVista(vistaActual(), { enfocar });
  } finally {
    boton.disabled = false;
    boton.textContent = 'Entrar';
  }
}

function salir() {
  borrarSesion();
  ponerIdEnUrl(null);
  mostrarAcceso();
  $('#ap-id').value = '';
  $('#ap-id').focus();
}

function init() {
  $('#titulo-pagina').setAttribute('tabindex', '-1');
  $('#ap-error').setAttribute('tabindex', '-1');
  $('#ap-form').addEventListener('submit', (e) => {
    e.preventDefault();
    const id = $('#ap-id').value.trim();
    if (!ID_VALIDO.test(id)) {
      mostrarAcceso('Escribe un ID válido: entre 3 y 12 letras, números o guiones.');
      $('#ap-id').focus();
      return;
    }
    cargar(id);
  });
  // IDs de ejemplo: rellenan el campo y entran
  $$('[data-id-demo]').forEach((b) => b.addEventListener('click', () => { $('#ap-id').value = b.dataset.idDemo; cargar(b.dataset.idDemo); }));
  $('#ap-salir').addEventListener('click', salir);
  $('#ap-salir-top')?.addEventListener('click', salir);

  // Vistas: el menú y los enlaces internos (#licencias…) cambian el hash
  window.addEventListener('hashchange', () => { if (!$('#ap-app').hidden) mostrarVista(vistaActual(), { enfocar: true }); });

  // Menú lateral en móvil
  $('#ap-menu').addEventListener('click', () => (body.classList.contains('dash--menu-abierto') ? cerrarMenu() : abrirMenu()));
  $('#ap-velo').addEventListener('click', cerrarMenu);
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && body.classList.contains('dash--menu-abierto')) { cerrarMenu(); $('#ap-menu').focus(); }
  });

  // Al llegar desde la web (botón Mi Portal / Mi Club) se ve siempre la pantalla de entrada.
  // Se entra directamente solo si el enlace trae ?id= o si se recarga la página estando dentro.
  const recarga = performance.getEntriesByType?.('navigation')?.[0]?.type === 'reload';
  const inicial = new URLSearchParams(location.search).get('id') || (recarga ? leerSesion() : null);
  if (inicial && ID_VALIDO.test(inicial)) cargar(inicial, { enfocar: false });
  else mostrarAcceso();
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
})();
