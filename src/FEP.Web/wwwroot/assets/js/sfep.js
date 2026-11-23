/**
 * SFEP — extranet de gestión interna de la FEP (sfep.html). Sustituye a /Gestion/Index.asp.
 *
 * Página AISLADA de la web (no carga comun.js). PROTOTIPO con datos de prueba, SIN CONEXIÓN A BASE DE DATOS:
 * los usuarios, permisos, avisos y notas salen de mock/sfep-usuarios.json (GET /api/v1/sfep/usuarios).
 * No hay contraseña: se elige un usuario de ejemplo. Las acciones (marcar nota leída…) no se guardan.
 *
 * Estructura nueva: los 17 accesos del "Menú Gestión" se agrupan por áreas (AREAS) en un menú lateral.
 * Reglas de visibilidad heredadas de Index.asp:
 *   - Cada módulo depende de su permiso (SFEP_Usuarios_Permisos: 1 Fichas, 2 Torneos, 3 Ranking, 4 Sanciones,
 *     5 Banco, 6 Comunicaciones, 7 Portal, 8 Configuración, 9 Informes, 8-2 Clubes, 8-8 Calendario).
 *   - "Portal web" solo para la FEP y las federaciones con portal (FED_CON_PORTAL); las demás ven "Calendario".
 *   - Eventos, Voto por correo y Control horario: personal FEP o usuarios con eventos asignados.
 *   - Servidor FEP, Calendario laboral, Notas y Certificados: solo personal FEP. Servidor, además, solo con
 *     acceso concedido (si no, aparece bloqueado, como el antiguo icono "ServidorNO").
 *   - Usuarios restringidos (antes el usuario 766): solo Fichas, Torneos y Clubes.
 * Los módulos son, por ahora, fichas "pendiente de migrar" con lo que incluirán y la página ASP que sustituyen.
 *
 * Inicio: el mismo para todo el personal (cifras, avisos, notas, todos los módulos). Cada usuario puede
 * personalizarlo con accesos favoritos (☆) y secciones del catálogo común mock/sfep-panel.json
 * (GET /api/v1/sfep/panel); prototipo: preferencias en localStorage por usuario.
 *
 * Rutas (hash): #inicio · #avisos (inicio, notas) · #m/<módulo>. Texto de datos siempre con textContent.
 */
(function () {
'use strict';

/* ---------------------------------------------------------------------------
   Utilidades
   --------------------------------------------------------------------------- */
const $ = (s, r = document) => r.querySelector(s);
const $$ = (s, r = document) => [...r.querySelectorAll(s)];
function el(tag, props = {}, ...hijos) {
  const n = document.createElement(tag);
  for (const [k, v] of Object.entries(props)) {
    if (v == null || v === false) continue;
    if (k === 'class') n.className = v;
    else if (k === 'text') n.textContent = v;
    else if (k === 'html') n.innerHTML = v;          // solo para iconos SVG propios (constantes de este archivo)
    else if (k.startsWith('on')) n.addEventListener(k.slice(2), v);
    else n.setAttribute(k, v === true ? '' : String(v));
  }
  for (const h of hijos.flat()) {
    if (h == null || h === false) continue;
    n.append(h instanceof Node ? h : document.createTextNode(String(h)));
  }
  return n;
}
function aFecha(v) {
  const m = /^(\d{4})-(\d{2})-(\d{2})(?:T(\d{2}):(\d{2}))?/.exec(String(v ?? ''));
  return m ? new Date(+m[1], +m[2] - 1, +m[3], +(m[4] ?? 0), +(m[5] ?? 0)) : null;
}
const fmtDia = new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'short' });
const fmtHora = new Intl.DateTimeFormat('es-ES', { hour: '2-digit', minute: '2-digit' });
const fmtHoy = new Intl.DateTimeFormat('es-ES', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });
const normaliza = (t) => String(t ?? '').toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '');

function toast(texto, { demo = true } = {}) {
  const t = $('#sfep-toast');
  t.textContent = demo ? `${texto} (Prototipo: no se guarda.)` : texto;
  t.hidden = false;
  clearTimeout(toast.t);
  toast.t = setTimeout(() => { t.hidden = true; }, 5000);
}

/* ---------------------------------------------------------------------------
   Iconos (trazo, 24×24)
   --------------------------------------------------------------------------- */
const ICONOS = {
  inicio: '<path d="M3 11l9-7 9 7M5 10v10h14V10M10 20v-6h4v6"/>',
  fichas: '<circle cx="9" cy="8" r="3.2"/><path d="M3 20c0-3.3 2.7-6 6-6s6 2.7 6 6"/><circle cx="17" cy="9" r="2.5"/><path d="M15.5 14.2c3 0 5.5 2.4 5.5 5.8"/>',
  clubes: '<path d="M12 3l8 3v6c0 4.5-3.4 8-8 9-4.6-1-8-4.5-8-9V6z"/><path d="M8.5 12h7"/>',
  certificados: '<rect x="3" y="4" width="18" height="13"/><path d="M7 8h10M7 11h6"/><circle cx="16" cy="16" r="2.5"/><path d="M14.5 18l-1 3 2.5-1 2.5 1-1-3"/>',
  torneos: '<circle cx="12" cy="12" r="9"/><path d="M5.5 5.5c3 2.5 3 10.5 0 13M18.5 5.5c-3 2.5-3 10.5 0 13"/>',
  ranking: '<path d="M4 20V13h5v7M9.5 20V7h5v13M15 20v-9h5v9M3 20h18"/>',
  calendario: '<rect x="3" y="5" width="18" height="16"/><path d="M3 10h18M8 3v4M16 3v4"/>',
  sanciones: '<rect x="7" y="3" width="10" height="14" transform="rotate(12 12 10)"/><path d="M5 21h14"/>',
  comunicaciones: '<rect x="9" y="3" width="6" height="11" rx="3"/><path d="M6 11a6 6 0 0 0 12 0M12 17v4M9 21h6"/>',
  portal: '<circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3c2.5 3 2.5 15 0 18M12 3c-2.5 3-2.5 15 0 18"/>',
  notas: '<path d="M4 4h16v11l-5 5H4z"/><path d="M15 20v-5h5M8 9h8M8 12h5"/>',
  banco: '<path d="M17 6.5A6.5 6.5 0 1 0 17 17.5M4 10h9M4 14h9"/>',
  informes: '<path d="M6 3h9l4 4v14H6z"/><path d="M14 3v5h5M9 17v-3M12 17v-6M15 17v-4"/>',
  voto: '<rect x="3" y="6" width="18" height="13"/><path d="M3 7l9 6 9-6"/>',
  eventos: '<path d="M4 6l2 2 3-3M4 12l2 2 3-3M4 18l2 2 3-3M12 7h8M12 13h8M12 19h8"/>',
  laboral: '<rect x="3" y="5" width="18" height="16"/><path d="M3 10h18M8 3v4M16 3v4M7 14h2M11 14h2M15 14h2M7 17h2M11 17h2"/>',
  horario: '<circle cx="12" cy="13" r="8"/><path d="M12 9v4l3 2M9 2h6"/>',
  configuracion: '<circle cx="12" cy="12" r="3"/><path d="M12 2v3M12 19v3M2 12h3M19 12h3M4.9 4.9l2.1 2.1M17 17l2.1 2.1M4.9 19.1L7 17M17 7l2.1-2.1"/>',
  servidor: '<rect x="4" y="3" width="16" height="7"/><rect x="4" y="14" width="16" height="7"/><path d="M8 6.5h.01M8 17.5h.01M12 6.5h5M12 17.5h5"/>',
  candado: '<rect x="5" y="11" width="14" height="10"/><path d="M8 11V7a4 4 0 0 1 8 0v4"/>',
};
const icono = (nombre, clase = 'sfep-ico') =>
  el('span', { class: clase, 'aria-hidden': 'true', html: `<svg viewBox="0 0 24 24">${ICONOS[nombre] ?? ''}</svg>` });

/* ---------------------------------------------------------------------------
   Estructura de SFEP: áreas y módulos
   --------------------------------------------------------------------------- */
// Federaciones con "Portal web" en Index.asp (0 = FEP). El resto ve "Calendario".
const FED_CON_PORTAL = new Set([0, 6, 8, 12, 13, 17, 18, 21]);

const MODULOS = {
  fichas: { nombre: 'Fichas jugadores/as', icono: 'fichas', legado: 'FichasFEP.asp', permiso: 'fichas',
    desc: 'Licencias y fichas federativas: alta, renovación, cambios de club y búsqueda.',
    funciones: ['Buscador de fichas por nombre, licencia, club o federación', 'Alta y renovación de licencias', 'Cambios de club y transferencias', 'Validación de fichas pendientes'] },
  clubes: { nombre: 'Clubes', icono: 'clubes', legado: 'Configuracion_Clubes.asp', permiso: 'clubes', restringidoVe: true,
    desc: 'Censo de clubes: datos, altas, cuotas e instalaciones.',
    funciones: ['Listado y ficha de cada club', 'Alta de club y renovación anual', 'Contacto, instalaciones y pistas', 'Licencias del club'] },
  certificados: { nombre: 'Certificados', icono: 'certificados', legado: 'Certificados_Listado.asp', soloFep: true,
    desc: 'Emisión y registro de certificados federativos.',
    funciones: ['Solicitudes de certificados', 'Generación del certificado en PDF', 'Registro de certificados emitidos'] },
  torneos: { nombre: 'Torneos', icono: 'torneos', legado: 'Torneos.asp', permiso: 'torneos', restringidoVe: true,
    desc: 'Alta de torneos, inscripciones, cuadros y resultados.',
    funciones: ['Alta y aprobación de torneos', 'Inscripciones y listas de parejas', 'Cuadros (4 a 128), previas y liguillas', 'Resultados y actas'] },
  ranking: { nombre: 'Ranking', icono: 'ranking', legado: 'Ranking.asp', permiso: 'ranking',
    desc: 'Cálculo y publicación de los rankings oficiales.',
    funciones: ['Carga de puntos por torneo', 'Cálculo del ranking (absoluto, menores, veteranos, silla)', 'Revisión y publicación'] },
  calendario: { nombre: 'Calendario', icono: 'calendario', legado: 'Configuracion_Calendario.asp', permiso: 'calendario', sinPortal: true,
    desc: 'Calendario de competiciones de la federación autonómica.',
    funciones: ['Alta de pruebas en el calendario', 'Fechas, sedes y categorías', 'Publicación en la web'] },
  sanciones: { nombre: 'Sanciones', icono: 'sanciones', legado: 'Sanciones.asp', permiso: 'sanciones',
    desc: 'Expedientes disciplinarios e inhabilitaciones.',
    funciones: ['Registro de sanciones', 'Inhabilitaciones nacionales y provinciales con fecha de fin', 'Fin automático de inhabilitaciones vencidas'] },
  comunicaciones: { nombre: 'Comunicaciones', icono: 'comunicaciones', legado: 'Comunicaciones.asp', permiso: 'comunicaciones',
    desc: 'Comunicados y envíos a federados, clubes y federaciones.',
    funciones: ['Redacción de comunicados', 'Selección de destinatarios', 'Histórico de envíos'] },
  portal: { nombre: 'Portal web', icono: 'portal', legado: 'Portal.asp', permiso: 'portal', conPortal: true,
    desc: 'Contenidos de la web: noticias, banners, documentos y páginas.',
    funciones: ['Noticias y destacados', 'Banners y patrocinadores', 'Documentos y normativa', 'Menús y páginas'] },
  notas: { nombre: 'Notas internas', icono: 'notas', legado: 'Notas.asp', soloFep: true,
    desc: 'Notas entre usuarios de SFEP, con aviso de lectura.',
    funciones: ['Publicar una nota a uno o varios usuarios', 'Notas recibidas y leídas', 'Borrado de notas'] },
  banco: { nombre: 'Banco', icono: 'banco', legado: 'Pagos_remesas.asp', permiso: 'banco',
    desc: 'Pagos, remesas bancarias y cobros.',
    funciones: ['Generación de remesas', 'Pagos recibidos y pendientes', 'Conciliación'] },
  informes: { nombre: 'Informes', icono: 'informes', legado: 'Informes.asp', permiso: 'informes',
    desc: 'Informes y estadísticas de federados, clubes y competición.',
    funciones: ['Informes de licencias por federación y categoría', 'Estadísticas de torneos', 'Exportación a Excel'] },
  voto: { nombre: 'Voto por correo', icono: 'voto', legado: 'VotoPorCorreo.asp', fepOEventos: true,
    desc: 'Gestión del voto por correo en los procesos electorales.',
    funciones: ['Solicitudes de voto por correo', 'Comprobación del censo', 'Registro de votos recibidos'] },
  eventos: { nombre: 'Eventos', icono: 'eventos', legado: 'Eventos.asp', fepOEventos: true,
    desc: 'Organización de eventos con checklist de tareas y fechas tope.',
    funciones: ['Eventos y responsables', 'Checklist por apartados con fecha tope', 'Avisos 3 días antes del vencimiento'] },
  laboral: { nombre: 'Calendario laboral', icono: 'laboral', legado: 'CalendarioLaboral.asp', soloFep: true,
    desc: 'Festivos, vacaciones y ausencias del personal.',
    funciones: ['Festivos nacionales y locales', 'Vacaciones y ausencias', 'Vista mensual del equipo'] },
  horario: { nombre: 'Control horario', icono: 'horario', legado: 'ConntrolHorario.asp', fepOEventos: true,
    desc: 'Registro de jornada del personal.',
    funciones: ['Fichaje de entrada y salida', 'Resumen semanal y mensual', 'Exportación del registro'] },
  configuracion: { nombre: 'Configuración', icono: 'configuracion', legado: 'Configuracion.asp', permiso: 'configuracion',
    desc: 'Usuarios de SFEP, permisos y tablas auxiliares.',
    funciones: ['Usuarios y permisos por módulo', 'Federaciones, categorías y temporadas', 'Tablas auxiliares'] },
  servidor: { nombre: 'Servidor FEP', icono: 'servidor', legado: 'Servidor.asp', soloFep: true, accesoEspecial: true,
    desc: 'Archivos compartidos del servidor de la FEP.',
    funciones: ['Carpetas y documentos compartidos', 'Subida y descarga de archivos'] },
};

const AREAS = [
  { id: 'federados', nombre: 'Federados', modulos: ['fichas', 'clubes', 'certificados'] },
  { id: 'competicion', nombre: 'Competición', modulos: ['torneos', 'ranking', 'calendario', 'sanciones'] },
  { id: 'comunicacion', nombre: 'Comunicación', modulos: ['comunicaciones', 'portal', 'notas'] },
  { id: 'administracion', nombre: 'Administración', modulos: ['banco', 'informes', 'voto'] },
  { id: 'oficina', nombre: 'Oficina FEP', modulos: ['eventos', 'laboral', 'horario'] },
  { id: 'sistema', nombre: 'Sistema', modulos: ['configuracion', 'servidor'] },
];
const areaDe = (id) => AREAS.find((a) => a.modulos.includes(id));

/** 'si' (visible y usable), 'bloqueado' (visible sin acceso) o null (no se muestra). */
function acceso(u, id) {
  const m = MODULOS[id];
  const p = u.permisos ?? {};
  const fep = !!u.personalFep;
  if (u.restringido) return m.restringidoVe || id === 'fichas' ? 'si' : null;
  if (m.permiso && !p[m.permiso]) return null;
  if (m.conPortal && !FED_CON_PORTAL.has(u.federacion?.id)) return null;
  if (m.sinPortal && FED_CON_PORTAL.has(u.federacion?.id)) return null;
  if (m.soloFep && !fep) return null;
  if (m.fepOEventos && !(fep || u.eventosAsignados)) return null;
  if (m.accesoEspecial && !u.accesoServidor) return 'bloqueado';
  return 'si';
}

/* ---------------------------------------------------------------------------
   Estado
   --------------------------------------------------------------------------- */
const CLAVE = 'sfep-usuario-demo';
let usuarios = [];
let usuario = null;
let notasLeidas = new Set();

/* ---------------------------------------------------------------------------
   Menú lateral y cabecera
   --------------------------------------------------------------------------- */
function pintarMenu() {
  const nav = $('#sfep-nav');
  const bloques = [el('ul', { class: 'sfep-nav__lista' },
    el('li', {}, el('a', { class: 'sfep-nav__link', href: '#inicio', 'data-ruta': 'inicio' }, icono('inicio'), el('span', { text: 'Inicio' }))))];
  for (const area of AREAS) {
    const items = area.modulos.map((id) => [id, acceso(usuario, id)]).filter(([, a]) => a);
    if (!items.length) continue;
    bloques.push(el('div', { class: 'sfep-nav__grupo' },
      el('p', { class: 'sfep-nav__area', id: `nav-${area.id}`, text: area.nombre }),
      el('ul', { class: 'sfep-nav__lista', 'aria-labelledby': `nav-${area.id}` }, ...items.map(([id, a]) => el('li', {},
        a === 'si'
          ? el('a', { class: 'sfep-nav__link', href: `#m/${id}`, 'data-ruta': `m/${id}` }, icono(MODULOS[id].icono), el('span', { text: MODULOS[id].nombre }))
          : el('span', { class: 'sfep-nav__link is-bloqueado', title: 'Sin acceso para tu usuario' }, icono(MODULOS[id].icono), el('span', { text: MODULOS[id].nombre }),
            icono('candado', 'sfep-ico sfep-ico--candado'), el('span', { class: 'sr-only', text: ' (sin acceso)' })))))));
  }
  nav.replaceChildren(...bloques);
}

function pintarUsuario() {
  $('#sfep-usuario').replaceChildren(
    el('span', { class: 'sfep-top__nombre', text: usuario.nombre }),
    el('span', { class: 'sfep-top__fed', text: usuario.federacion?.nombre ?? '' }));
  actualizarCampana();
}
function notasPendientes() { return (usuario.notas ?? []).filter((n) => !notasLeidas.has(n.id)); }
function actualizarCampana() {
  const n = notasPendientes().length;
  const b = $('#sfep-campana-n');
  b.hidden = !n;
  b.textContent = String(n);
  $('#sfep-campana').title = n ? `${n} notas sin leer` : 'Sin notas nuevas';
}

/* ---------------------------------------------------------------------------
   Personalización del inicio (igual para todo el personal; cada uno elige sus favoritos)
   - Accesos favoritos: módulos marcados con ★ (en el inicio, en cada módulo o en "Personalizar").
   - Secciones: widgets del catálogo común (mock/sfep-panel.json) que el usuario añade y ordena.
   PROTOTIPO: se guarda en este navegador (localStorage, por usuario). En producción, en el usuario de SFEP.
   --------------------------------------------------------------------------- */
let panelComun = null;
let pref = { favoritos: [], secciones: [] };
const clavePref = () => `sfep-preferencias-${usuario?.id}`;
function leerPref() {
  try {
    const p = JSON.parse(localStorage.getItem(clavePref()) ?? 'null');
    return { favoritos: Array.isArray(p?.favoritos) ? p.favoritos : [], secciones: Array.isArray(p?.secciones) ? p.secciones : [] };
  } catch { return { favoritos: [], secciones: [] }; }
}
function guardarPref() {
  try { localStorage.setItem(clavePref(), JSON.stringify(pref)); return true; } catch { return false; }
}
/** Secciones que este usuario puede añadir: las de módulos a los que tiene acceso; las internas, solo personal FEP. */
const catalogo = () => (panelComun?.widgets ?? []).filter((w) => (w.modulo ? acceso(usuario, w.modulo) === 'si' : !!usuario.personalFep));
const favoritosValidos = () => pref.favoritos.filter((id) => MODULOS[id] && acceso(usuario, id) === 'si');
const seccionesValidas = () => { const c = catalogo(); return pref.secciones.map((id) => c.find((w) => w.id === id)).filter(Boolean); };

/** Botón ★ para añadir o quitar un módulo de favoritos. */
function botonFavorito(id, clase = '') {
  const b = el('button', { type: 'button', class: `sfep-estrella ${clase}`.trim(), 'data-fav': id });
  const pintar = () => {
    const si = pref.favoritos.includes(id);
    b.setAttribute('aria-pressed', String(si));
    b.setAttribute('aria-label', `${si ? 'Quitar' : 'Añadir'} ${MODULOS[id].nombre} ${si ? 'de' : 'a'} favoritos`);
    b.title = si ? 'Quitar de favoritos' : 'Añadir a favoritos';
    b.textContent = si ? '★' : '☆';
  };
  b.addEventListener('click', (e) => {
    e.preventDefault();
    const si = pref.favoritos.includes(id);
    pref.favoritos = si ? pref.favoritos.filter((x) => x !== id) : [...pref.favoritos, id];
    guardarPref();
    $$(`[data-fav="${id}"]`).forEach((x) => x.dispatchEvent(new CustomEvent('sfep:repintar')));
    pintarBloquePersonal();
    toast(si ? `${MODULOS[id].nombre} quitado de favoritos.` : `${MODULOS[id].nombre} añadido a favoritos.`, { demo: false });
  });
  b.addEventListener('sfep:repintar', pintar);
  pintar();
  return b;
}

const fmtFechaCorta = (ini, fin) => {
  const a = aFecha(ini); const b = aFecha(fin);
  if (!a) return '';
  return b && b.toDateString() !== a.toDateString() ? `${fmtDia.format(a)} – ${fmtDia.format(b)}` : fmtDia.format(a);
};

/** Sección del catálogo: { id, titulo, grupo, tipo: 'lista'|'barras', modulo?, enlace?, items }. */
function widgetPanel(w) {
  const hid = `sfep-w-${w.id}`;
  const link = w.modulo && acceso(usuario, w.modulo) === 'si'
    ? el('a', { class: 'sfep-panel__link', href: `#m/${w.modulo}` }, `${w.enlace ?? 'Abrir'} →`) : null;
  let cuerpo;
  if (w.tipo === 'barras') {
    cuerpo = el('ul', { class: 'sfep-wbarras' }, ...(w.items ?? []).map((b) => el('li', {},
      el('span', { class: 'sfep-wbarras__etiqueta', text: b.etiqueta }),
      el('span', { class: 'sfep-wbarras__pista', 'aria-hidden': 'true' },
        el('span', { class: 'sfep-wbarras__relleno', style: `width: ${Math.min(100, Math.round((b.valor / (b.max || 100)) * 100))}%` })),
      el('strong', { class: 'sfep-wbarras__n', text: b.texto ?? String(b.valor) }))));
  } else {
    cuerpo = (w.items ?? []).length ? el('ul', { class: 'sfep-wlista' }, ...w.items.map((it) => el('li', {},
      el('span', { class: 'sfep-wlista__fecha' }, it.fecha ? el('time', { datetime: it.fecha, text: fmtFechaCorta(it.fecha, it.fechaFin) }) : '—'),
      el('span', { class: 'sfep-wlista__main' }, el('strong', { text: it.titulo }), it.meta ? el('span', { text: it.meta }) : null),
      el('span', { class: 'sfep-wlista__fin' },
        it.importe ? el('strong', { class: 'sfep-wlista__importe', text: it.importe }) : null,
        it.estado ? el('span', { class: `sfep-tono sfep-tono--${it.tono ?? 'neutro'}`, text: it.estado }) : null)))) : el('p', { class: 'sfep-vacio', text: 'Nada pendiente.' });
  }
  return el('section', { class: 'sfep-panel', 'aria-labelledby': hid },
    el('header', { class: 'sfep-panel__cab' },
      el('div', {}, el('p', { class: 'sfep-panel__grupo', text: w.grupo }), el('h2', { id: hid, text: w.titulo })), link),
    cuerpo);
}

/** Accesos favoritos (encima de las cards). */
function bloqueFavoritos() {
  const favs = favoritosValidos();
  if (!favs.length) return [];
  return [el('section', { class: 'sfep-atajos', 'aria-labelledby': 'sfep-favs-t' },
    el('h2', { class: 'sfep-area__titulo', id: 'sfep-favs-t', text: '★ Mis accesos favoritos' }),
    el('ul', { class: 'sfep-atajos__lista' }, ...favs.map((id) => el('li', {},
      el('a', { class: 'sfep-atajo', href: `#m/${id}` }, icono(MODULOS[id].icono, 'sfep-atajo__ico'), el('span', { text: MODULOS[id].nombre }))))))];
}
/** Secciones elegidas por el usuario (debajo de las cards) o una invitación a personalizar. */
function bloqueSecciones() {
  const secs = seccionesValidas();
  if (secs.length) return [el('div', { class: 'sfep-puesto-grid' }, ...secs.map(widgetPanel))];
  if (favoritosValidos().length) return [];
  return [el('div', { class: 'sfep-invita' },
    el('p', { class: 'sfep-invita__titulo', text: 'Haz tuyo este inicio' }),
    el('p', { text: catalogo().length
      ? 'Marca con ☆ los módulos que más uses y añade las secciones que quieras tener a mano: remesas, campeonatos, firmas, envíos…'
      : 'Marca con ☆ los módulos que más uses para tenerlos aquí como accesos directos.' }),
    el('button', { type: 'button', class: 'sfep-btn', onclick: (e) => abrirPersonalizar(e.currentTarget) }, 'Personalizar inicio'))];
}
function pintarBloquePersonal() {
  $('#sfep-favs')?.replaceChildren(...bloqueFavoritos());
  $('#sfep-secs')?.replaceChildren(...bloqueSecciones());
}

/**
 * Abre un <dialog> modal y devuelve la función que lo cierra. No depende del evento "close" (en algunos
 * navegadores embebidos no llega): cierre con botones, Escape o clic fuera; quita el diálogo y devuelve el foco.
 */
function dialogoModal(dlg, origen) {
  const cerrar = () => {
    if (!dlg.isConnected) return;
    if (dlg.open) dlg.close();
    dlg.remove();
    (origen?.isConnected ? origen : $('#sfep-titulo'))?.focus({ preventScroll: true });
  };
  dlg.addEventListener('cancel', (e) => { e.preventDefault(); cerrar(); });
  dlg.addEventListener('keydown', (e) => { if (e.key === 'Escape') { e.preventDefault(); cerrar(); } });
  dlg.addEventListener('close', cerrar);
  dlg.addEventListener('click', (e) => { if (e.target === dlg) cerrar(); });
  document.body.append(dlg);
  dlg.showModal();
  return cerrar;
}

/** Diálogo "Personalizar inicio": secciones (con orden) y accesos favoritos. */
function abrirPersonalizar(origen) {
  let cerrar = () => {};
  const cat = catalogo();
  let sel = seccionesValidas().map((w) => w.id);
  const favs = new Set(favoritosValidos());
  const orden = el('ol', { class: 'sfep-orden' });
  const pintarOrden = () => {
    orden.replaceChildren(...(sel.length ? sel.map((id, i) => {
      const w = cat.find((x) => x.id === id);
      const mover = (d) => { const j = i + d; [sel[i], sel[j]] = [sel[j], sel[i]]; pintarOrden(); orden.querySelectorAll('li')[j]?.querySelector(d < 0 ? '[data-sube]' : '[data-baja]')?.focus(); };
      return el('li', {}, el('span', { text: w.titulo }),
        el('span', { class: 'sfep-orden__btns' },
          el('button', { type: 'button', class: 'sfep-btn sfep-btn--sec sfep-btn--sm', 'data-sube': '', 'aria-label': `Subir ${w.titulo}`, disabled: i === 0, onclick: () => mover(-1) }, '↑'),
          el('button', { type: 'button', class: 'sfep-btn sfep-btn--sec sfep-btn--sm', 'data-baja': '', 'aria-label': `Bajar ${w.titulo}`, disabled: i === sel.length - 1, onclick: () => mover(1) }, '↓')));
    }) : [el('li', { class: 'sfep-vacio', text: 'Ninguna sección elegida.' })]));
  };
  pintarOrden();

  const grupos = [...new Set(cat.map((w) => w.grupo))];
  const casillas = grupos.map((g) => el('fieldset', { class: 'sfep-casillas' }, el('legend', { text: g }),
    ...cat.filter((w) => w.grupo === g).map((w) => {
      const cb = el('input', { type: 'checkbox', id: `p-${w.id}`, checked: sel.includes(w.id) });
      cb.addEventListener('change', () => { sel = cb.checked ? [...sel, w.id] : sel.filter((x) => x !== w.id); pintarOrden(); });
      return el('label', { class: 'sfep-casilla', for: `p-${w.id}` }, cb, el('span', { text: w.titulo }));
    })));

  const modulos = AREAS.map((a) => ({ a, ids: a.modulos.filter((id) => acceso(usuario, id) === 'si') })).filter((x) => x.ids.length);
  const casFav = modulos.map(({ a, ids }) => el('fieldset', { class: 'sfep-casillas' }, el('legend', { text: a.nombre }),
    ...ids.map((id) => {
      const cb = el('input', { type: 'checkbox', id: `f-${id}`, checked: favs.has(id) });
      cb.addEventListener('change', () => { if (cb.checked) favs.add(id); else favs.delete(id); });
      return el('label', { class: 'sfep-casilla', for: `f-${id}` }, cb, el('span', { text: MODULOS[id].nombre }));
    })));

  const dlg = el('dialog', { class: 'sfep-ficha sfep-perso', 'aria-labelledby': 'perso-t' },
    el('header', { class: 'sfep-ficha__cab' },
      el('div', {}, el('p', { class: 'sfep-ficha__lic', text: 'Tu inicio' }), el('h2', { id: 'perso-t', text: 'Personalizar inicio' })),
      el('button', { type: 'button', class: 'sfep-icono sfep-ficha__cerrar', 'aria-label': 'Cerrar sin guardar', onclick: () => cerrar() }, '×')),
    el('div', { class: 'sfep-ficha__cuerpo' },
      cat.length ? el('section', { class: 'sfep-perso__bloque', 'aria-labelledby': 'perso-sec' },
        el('h3', { id: 'perso-sec', text: 'Secciones del inicio' }),
        el('p', { class: 'sfep-perso__ayuda', text: 'Elige qué secciones quieres ver en tu inicio.' }),
        el('div', { class: 'sfep-perso__grupos' }, ...casillas),
        el('h3', { text: 'Orden' }), orden) : null,
      el('section', { class: 'sfep-perso__bloque', 'aria-labelledby': 'perso-fav' },
        el('h3', { id: 'perso-fav', text: 'Accesos favoritos' }),
        el('p', { class: 'sfep-perso__ayuda', text: 'Los módulos marcados aparecen como accesos directos. También puedes usar la ☆ de cada módulo.' }),
        el('div', { class: 'sfep-perso__grupos' }, ...casFav)),
      el('p', { class: 'sfep-perso__nota', text: 'Prototipo: se guarda en este navegador. En la versión real se guardará en tu usuario de SFEP.' })),
    el('footer', { class: 'sfep-ficha__pie' },
      el('button', { type: 'button', class: 'sfep-btn', onclick: () => {
        // Conserva el orden en que estaban los favoritos y añade los nuevos al final
        pref = { favoritos: [...pref.favoritos.filter((id) => favs.has(id)), ...[...favs].filter((id) => !pref.favoritos.includes(id))], secciones: sel };
        guardarPref();
        cerrar();
        if (rutaActual().tipo !== 'modulo') { mostrar({ enfocar: false }); $('#sfep-personalizar')?.focus(); }
        $$('[data-fav]').forEach((x) => x.dispatchEvent(new CustomEvent('sfep:repintar')));
        toast('Inicio personalizado.', { demo: false });
      } }, 'Guardar'),
      el('button', { type: 'button', class: 'sfep-btn sfep-btn--sec', onclick: () => cerrar() }, 'Cancelar'),
      el('button', { type: 'button', class: 'sfep-btn sfep-btn--sec sfep-btn--peligro sfep-perso__reset', onclick: () => {
        pref = { favoritos: [], secciones: [] };
        guardarPref();
        cerrar();
        if (rutaActual().tipo !== 'modulo') { mostrar({ enfocar: false }); $('#sfep-personalizar')?.focus(); }
        $$('[data-fav]').forEach((x) => x.dispatchEvent(new CustomEvent('sfep:repintar')));
        toast('Inicio restablecido.', { demo: false });
      } }, 'Restablecer')));
  cerrar = dialogoModal(dlg, origen);
}

/* ---------------------------------------------------------------------------
   Vistas
   --------------------------------------------------------------------------- */
function vistaInicio() {
  const hoy = new Date(); hoy.setHours(0, 0, 0, 0);
  const visibles = AREAS.map((a) => ({ ...a, items: a.modulos.map((id) => [id, acceso(usuario, id)]).filter(([, x]) => x) })).filter((a) => a.items.length);

  // Cifras: las comunes de la FEP, o las propias del usuario (federaciones autonómicas)
  const resumen = usuario.resumen ?? panelComun?.resumen ?? [];
  const cifrasVisibles = resumen.filter((r) => !r.modulo || acceso(usuario, r.modulo));
  const cifras = cifrasVisibles.length ? el('ul', { class: 'sfep-kpis', 'aria-label': 'Resumen' }, ...cifrasVisibles.map((r) => {
    const dentro = [el('span', { class: 'sfep-kpi__etiqueta', text: r.etiqueta }), el('strong', { class: 'sfep-kpi__valor', text: r.valor })];
    return el('li', {}, r.modulo && acceso(usuario, r.modulo) === 'si' ? el('a', { class: 'sfep-kpi', href: `#${r.ruta ?? `m/${r.modulo}`}` }, ...dentro) : el('div', { class: 'sfep-kpi' }, ...dentro));
  })) : null;

  // Avisos del checklist de eventos
  const avisos = (usuario.avisos ?? []).slice().sort((a, b) => aFecha(a.fechaTope) - aFecha(b.fechaTope));
  const panelAvisos = el('section', { class: 'sfep-panel', 'aria-labelledby': 'sfep-avisos-t' },
    el('header', { class: 'sfep-panel__cab' }, el('h2', { id: 'sfep-avisos-t', text: 'Avisos del checklist' }),
      acceso(usuario, 'eventos') === 'si' ? el('a', { class: 'sfep-panel__link', href: '#m/eventos' }, 'Eventos →') : null),
    avisos.length ? el('ul', { class: 'sfep-avisos' }, ...avisos.map((a) => {
      const f = aFecha(a.fechaTope);
      const dias = Math.round((f - hoy) / 864e5);
      const estado = dias < 0 ? ['vencido', `Vencido hace ${-dias} ${-dias === 1 ? 'día' : 'días'}`] : dias === 0 ? ['hoy', 'Vence hoy'] : dias <= 3 ? ['pronto', `Vence en ${dias} ${dias === 1 ? 'día' : 'días'}`] : ['ok', `Vence el ${fmtDia.format(f)}`];
      return el('li', { class: `sfep-aviso sfep-aviso--${estado[0]}` },
        el('span', { class: 'sfep-aviso__estado', text: estado[1] }),
        el('strong', { class: 'sfep-aviso__tarea', text: a.tarea }),
        el('span', { class: 'sfep-aviso__evento', text: a.evento }));
    })) : el('p', { class: 'sfep-vacio', text: 'No tienes tareas del checklist pendientes.' }));

  // Notas sin leer
  const listaNotas = el('ul', { class: 'sfep-notas' });
  const pintarNotas = () => {
    const pend = notasPendientes();
    listaNotas.replaceChildren(...(pend.length ? pend.map((n) => {
      const f = aFecha(n.fecha);
      return el('li', { class: 'sfep-nota' },
        el('p', { class: 'sfep-nota__meta' }, el('time', { datetime: n.fecha, text: f ? `${fmtDia.format(f)} · ${fmtHora.format(f)}` : '' }), ` · ${n.autor}`),
        el('p', { class: 'sfep-nota__texto', text: n.texto }),
        el('button', { type: 'button', class: 'sfep-btn sfep-btn--sec sfep-btn--sm', onclick: () => {
          notasLeidas.add(n.id); pintarNotas(); actualizarCampana(); toast('Nota marcada como leída.');
          $('#sfep-notas-t').focus();
        } }, 'Marcar como leída'));
    }) : [el('li', { class: 'sfep-vacio', text: 'No tienes notas sin leer.' })]));
  };
  pintarNotas();
  const panelNotas = el('section', { class: 'sfep-panel', id: 'sfep-panel-notas', 'aria-labelledby': 'sfep-notas-t' },
    el('header', { class: 'sfep-panel__cab' }, el('h2', { id: 'sfep-notas-t', tabindex: '-1', text: 'Notas sin leer' }),
      acceso(usuario, 'notas') === 'si' ? el('a', { class: 'sfep-panel__link', href: '#m/notas' }, 'Notas internas →') : null),
    listaNotas);

  // Todos los módulos por áreas, con buscador y ☆ para favoritos
  const buscador = el('input', { type: 'search', id: 'sfep-buscar', placeholder: 'Buscar un módulo…', autocomplete: 'off' });
  const sinRes = el('p', { class: 'sfep-vacio', hidden: true, text: 'Ningún módulo coincide con la búsqueda.' });
  // Una sola cuadrícula de cards; cada card lleva el nombre de su área
  const grupos = [el('div', { class: 'sfep-area' },
    el('ul', { class: 'sfep-tiles' }, ...visibles.flatMap((a) => a.items.map(([id, x]) => [a, id, x])).map(([a, id, x]) => {
      const m = MODULOS[id];
      const cuerpo = [icono(m.icono, 'sfep-tile__ico'), el('span', { class: 'sfep-tile__area', text: a.nombre }), el('span', { class: 'sfep-tile__nombre', text: m.nombre }), el('span', { class: 'sfep-tile__desc', text: x === 'si' ? m.desc : 'Sin acceso para tu usuario' })];
      const li = el('li', { class: 'sfep-tiles__item' }, x === 'si' ? el('a', { class: 'sfep-tile', href: `#m/${id}` }, ...cuerpo) : el('div', { class: 'sfep-tile is-bloqueado' }, ...cuerpo, icono('candado', 'sfep-tile__candado')),
        x === 'si' ? botonFavorito(id, 'sfep-estrella--tile') : null);
      li.dataset.buscar = normaliza(`${m.nombre} ${m.desc} ${a.nombre}`);
      return li;
    })))];
  buscador.addEventListener('input', () => {
    const q = normaliza(buscador.value.trim());
    let total = 0;
    for (const g of grupos) {
      let n = 0;
      for (const li of $$('.sfep-tiles__item', g)) { const si = !q || li.dataset.buscar.includes(q); li.hidden = !si; if (si) n++; }
      g.hidden = !n; total += n;
    }
    sinRes.hidden = total > 0;
  });

  return [
    el('div', { class: 'sfep-saludo sfep-saludo--fila' },
      el('div', {},
        el('p', { class: 'sfep-saludo__fecha', text: fmtHoy.format(new Date()) }),
        el('h1', { class: 'sfep-h1', id: 'sfep-titulo', tabindex: '-1', text: `Hola, ${usuario.nombre}` }),
        el('p', { class: 'sfep-saludo__perfil', text: `${usuario.perfil} · ${usuario.federacion?.nombre ?? ''}` })),
      el('button', { type: 'button', class: 'sfep-btn sfep-btn--sec', id: 'sfep-personalizar', onclick: (e) => abrirPersonalizar(e.currentTarget) }, '⚙ Personalizar inicio')),
    cifras,
    el('div', { class: 'sfep-personal', id: 'sfep-favs' }, ...bloqueFavoritos()),
    // Inicio = todas las opciones en cards (el menú lateral solo aparece dentro de un módulo)
    el('section', { class: 'sfep-menu sfep-menu--cards', 'aria-labelledby': 'sfep-menu-t' },
      el('header', { class: 'sfep-menu__cab' },
        el('h2', { id: 'sfep-menu-t', text: 'Módulos de gestión' }),
        el('div', { class: 'sfep-buscar' }, el('label', { class: 'sr-only', for: 'sfep-buscar', text: 'Buscar un módulo' }), buscador)),
      ...grupos, sinRes),
    el('div', { class: 'sfep-personal', id: 'sfep-secs' }, ...bloqueSecciones()),
    el('div', { class: 'sfep-dos' }, panelAvisos, panelNotas),
  ];
}

/** Módulos ya migrados: se registran desde su propio archivo (assets/js/sfep-<módulo>.js) con SFEP.registrar(). */
const VISTAS = {};

function vistaModulo(id, sub, params) {
  const m = MODULOS[id];
  const a = m ? acceso(usuario, id) : null;
  if (m && a === 'si' && VISTAS[id]) return VISTAS[id](sub, params);
  if (!m || a !== 'si') {
    return [el('div', { class: 'sfep-saludo' },
      el('h1', { class: 'sfep-h1', id: 'sfep-titulo', tabindex: '-1', text: m ? m.nombre : 'Módulo no encontrado' })),
    el('div', { class: 'sfep-panel sfep-sinacceso' }, icono('candado', 'sfep-sinacceso__ico'),
      el('p', { text: m ? 'Tu usuario no tiene permiso para este módulo. Pide acceso a la administración de SFEP.' : 'Este módulo no existe.' }),
      el('a', { class: 'sfep-btn', href: '#inicio' }, 'Volver al inicio'))];
  }
  const area = areaDe(id);
  return [
    el('nav', { class: 'sfep-migas', 'aria-label': 'Estás en' }, el('a', { href: '#inicio', text: 'Inicio' }), el('span', { 'aria-hidden': 'true', text: '/' }),
      el('span', { text: area.nombre }), el('span', { 'aria-hidden': 'true', text: '/' }), el('span', { 'aria-current': 'page', text: m.nombre })),
    el('div', { class: 'sfep-modcab' }, icono(m.icono, 'sfep-modcab__ico'),
      el('div', {}, el('h1', { class: 'sfep-h1', id: 'sfep-titulo', tabindex: '-1', text: m.nombre }), el('p', { class: 'sfep-modcab__desc', text: m.desc })),
      botonFavorito(id, 'sfep-estrella--cab')),
    el('div', { class: 'sfep-dos' },
      el('section', { class: 'sfep-panel', 'aria-labelledby': 'mod-incluye' },
        el('header', { class: 'sfep-panel__cab' }, el('h2', { id: 'mod-incluye', text: 'Qué incluirá' })),
        el('ul', { class: 'sfep-check' }, ...m.funciones.map((f) => el('li', { text: f })))),
      el('section', { class: 'sfep-panel sfep-pendiente', 'aria-labelledby': 'mod-estado' },
        el('header', { class: 'sfep-panel__cab' }, el('h2', { id: 'mod-estado', text: 'Estado' })),
        el('p', { class: 'sfep-pendiente__chip', text: 'Pendiente de migrar' }),
        el('p', {}, 'Sustituirá a ', el('code', { text: `Gestion/${m.legado}` }), '. Se construirá con datos de prueba, sin conexión a la base de datos, cuando lleguen las páginas de este módulo.'))),
  ];
}

/** #m/<módulo>[/<subapartado>][?clave=valor…] */
function rutaActual() {
  const [camino, consulta = ''] = location.hash.slice(1).split('?');
  const h = decodeURIComponent(camino);
  if (h.startsWith('m/')) {
    const [id, sub = ''] = h.slice(2).split('/');
    return { tipo: 'modulo', id, sub, params: new URLSearchParams(consulta) };
  }
  return { tipo: h === 'avisos' ? 'avisos' : 'inicio' };
}

function mostrar({ enfocar = true } = {}) {
  if (!usuario) return;
  const r = rutaActual();
  const main = $('#sfep-contenido');
  main.replaceChildren(...(r.tipo === 'modulo' ? vistaModulo(r.id, r.sub, r.params) : vistaInicio()).filter(Boolean));
  // En el inicio no hay menú lateral (todo está en cards); dentro de un módulo, sí.
  document.body.classList.toggle('sfep--inicio', r.tipo !== 'modulo');
  const ruta = r.tipo === 'modulo' ? `m/${r.id}` : 'inicio';
  for (const a of $$('.sfep-nav__link[data-ruta]')) {
    if (a.dataset.ruta === ruta) a.setAttribute('aria-current', 'page'); else a.removeAttribute('aria-current');
  }
  const m = r.tipo === 'modulo' ? MODULOS[r.id] : null;
  document.title = `${m ? `${m.nombre} · ` : ''}SFEP · Gestión interna FEP`;
  cerrarMenu();
  if (r.tipo === 'avisos') {
    const p = $('#sfep-panel-notas');
    p?.scrollIntoView({ block: 'start' });
    $('#sfep-notas-t')?.focus({ preventScroll: true });
  } else {
    window.scrollTo(0, 0);
    if (enfocar) $('#sfep-titulo')?.focus({ preventScroll: true });
  }
}

/* ---------------------------------------------------------------------------
   Menú móvil, ayuda, sesión
   --------------------------------------------------------------------------- */
function abrirMenu() { document.body.classList.add('sfep--menu'); $('#sfep-menu').setAttribute('aria-expanded', 'true'); $('#sfep-velo').hidden = false; $('.sfep-nav__link')?.focus(); }
function cerrarMenu() {
  if (!document.body.classList.contains('sfep--menu')) return;
  document.body.classList.remove('sfep--menu'); $('#sfep-menu').setAttribute('aria-expanded', 'false'); $('#sfep-velo').hidden = true;
}

function entrar(u, { enfocar = true } = {}) {
  usuario = u;
  notasLeidas = new Set();
  pref = leerPref();
  try { sessionStorage.setItem(CLAVE, String(u.id)); } catch { /* sin almacenamiento */ }
  document.body.classList.remove('sfep--sin-sesion');
  $('#sfep-acceso').hidden = true;
  $('#sfep-app').hidden = false;
  pintarUsuario();
  pintarMenu();
  mostrar({ enfocar });
}

function salir() {
  usuario = null;
  try { sessionStorage.removeItem(CLAVE); } catch { /* ignorar */ }
  history.replaceState(null, '', location.pathname);
  cerrarMenu();
  $('#sfep-app').hidden = true;
  $('#sfep-contenido').replaceChildren();
  document.body.classList.add('sfep--sin-sesion');
  $('#sfep-acceso').hidden = false;
  document.title = 'SFEP · Gestión interna · Federación Española de Pádel';
  $('#sfep-usuarios button')?.focus();
}

async function init() {
  $('#sfep-salir').addEventListener('click', salir);
  $('#sfep-menu').addEventListener('click', () => (document.body.classList.contains('sfep--menu') ? cerrarMenu() : abrirMenu()));
  $('#sfep-velo').addEventListener('click', cerrarMenu);
  const ayuda = $('#sfep-panel-ayuda');
  $('#sfep-ayuda').addEventListener('click', () => { ayuda.hidden = !ayuda.hidden; if (!ayuda.hidden) $('#sfep-ayuda-cerrar').focus(); });
  $('#sfep-ayuda-cerrar').addEventListener('click', () => { ayuda.hidden = true; $('#sfep-ayuda').focus(); });
  document.addEventListener('keydown', (e) => {
    if (e.key !== 'Escape') return;
    if (!ayuda.hidden) { ayuda.hidden = true; $('#sfep-ayuda').focus(); }
    else if (document.body.classList.contains('sfep--menu')) { cerrarMenu(); $('#sfep-menu').focus(); }
  });
  window.addEventListener('hashchange', () => mostrar());

  try {
    const [datos, panel] = await Promise.all([
      window.FEPApi.get('/api/v1/sfep/usuarios'),
      window.FEPApi.get('/api/v1/sfep/panel').catch((e) => { console.warn('[SFEP] panel', e); return null; }),
    ]);
    usuarios = window.FEPApi.comoLista(datos);
    panelComun = panel;
  } catch (e) {
    const err = $('#sfep-error');
    err.textContent = 'No se han podido cargar los usuarios de ejemplo.';
    err.hidden = false;
    console.warn('[SFEP]', e);
    return;
  }
  $('#sfep-usuarios').replaceChildren(...usuarios.map((u) => el('li', {},
    el('button', { type: 'button', class: 'sfep-usu', onclick: () => entrar(u) },
      el('span', { class: 'sfep-usu__nombre', text: u.nombre }),
      el('span', { class: 'sfep-usu__perfil', text: u.perfil }),
      el('span', { class: 'sfep-usu__fed', text: u.federacion?.nombre ?? '' })))));

  // Al llegar desde la web (enlace "Extranet") se ve siempre la pantalla de acceso; solo se mantiene
  // la sesión si se recarga la página estando dentro.
  let guardado = null;
  const recarga = performance.getEntriesByType?.('navigation')?.[0]?.type === 'reload';
  if (recarga) { try { guardado = sessionStorage.getItem(CLAVE); } catch { /* ignorar */ } }
  const u = usuarios.find((x) => String(x.id) === guardado);
  if (u) entrar(u, { enfocar: false });
}

/* ---------------------------------------------------------------------------
   API para los módulos migrados (sfep-<módulo>.js, cargados después de este archivo)
   --------------------------------------------------------------------------- */
window.SFEP = Object.freeze({
  registrar(id, vista) { VISTAS[id] = vista; },
  usuario: () => usuario,
  modulo: (id) => MODULOS[id],
  area: areaDe,
  acceso: (id) => (usuario ? acceso(usuario, id) : null),
  get: (ruta) => window.FEPApi.get(ruta),
  comoLista: (d) => window.FEPApi.comoLista(d),
  rutaActual,
  botonFavorito: (id) => botonFavorito(id, 'sfep-estrella--cab'),
  dialogoModal,
  el, $, $$, icono, toast, aFecha, normaliza, ICONOS,
});

// Con scripts "defer" el DOMContentLoaded llega después de que se hayan cargado todos los módulos.
if (document.readyState === 'complete') init();
else document.addEventListener('DOMContentLoaded', init);
})();
