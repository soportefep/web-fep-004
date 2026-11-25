/**
 * Página "Organización de torneos" (migrada desde Organizacion_Torneos.asp, URL antigua /Organizacion_Torneos, y
 * Organizacion_FueraPlazo.asp). Requiere assets/js/comun.js (cabecera, menú, pie y utilidades en window.FEP).
 *
 *   Convocatoria, documentos, pasos, pruebas y comunidades → GET /api/v1/competicion/organizacion-torneos
 *                                                            (respaldo: mock/competicion-organizacion-torneos.json)
 *   Clubes de una provincia (desplegable "Club sede")      → GET /api/v1/competicion/organizacion-torneos/clubes?provincia=
 *                                                            (respaldo: mock/competicion-organizacion-torneos-clubes.json,
 *                                                            todos los clubes: se filtran aquí por provincia)
 *
 * El formulario conserva los nombres de campo del ASP (torneo, comunidad, provincia, localidad, club, club2,
 * organizador, responsable, telefono, email, lopd, accion) y se envía con POST a Organizacion_P0.asp de la web actual
 * (envioUrl), que guarda la solicitud y sigue con los pasos 2 a 4. Aquí solo se valida antes de enviar (como validar()
 * del ASP). Fechas y fianza del torneo elegido ya vienen en la respuesta (antes, cargarfechastorneo.asp).
 * Todo texto de la API va por textContent.
 */
(function () {
'use strict';

const { $, $$, el, get, urlSegura, aFecha, bloqueFecha, fmtLargo } = window.FEP;

const fmtNum = new Intl.NumberFormat('es-ES');
const fmtEuros = new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR', minimumFractionDigits: 0, maximumFractionDigits: 2 });
const fmtMes = new Intl.DateTimeFormat('es-ES', { month: 'long', year: 'numeric' });
const fmtDiaMes = new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'long' });
const fmtHora = new Intl.DateTimeFormat('es-ES', { hour: '2-digit', minute: '2-digit' });
const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const TELEFONO = /^[0-9+()\s.-]{6,15}$/;

/** Etiqueta y clase de cada grupo del desplegable (claves de la API). */
const ETIQUETAS_GRUPO = {
  menores: ['Menores', 'org-tag camp-tag camp-tag--menores'],
  absolutos: ['Absoluto y sub-23', 'org-tag camp-tag camp-tag--absoluto'],
  veteranos: ['Veteranos', 'org-tag camp-tag camp-tag--veteranos'],
  silla: ['Pádel en silla', 'org-tag calf-tag--silla'],
};

let torneos = [];
let grupos = [];
let comunidades = [];
let todosLosClubes = null;   // respaldo del mock: todos los clubes (se filtran por provincia)
let peticionClubes = 0;

/* ---------------------------------------------------------------------------
   Utilidades
   --------------------------------------------------------------------------- */
const limpio = (t) => String(t ?? '').replace(/\s+/g, ' ').trim();
const plural = (n, uno, varios) => `${fmtNum.format(n)} ${n === 1 ? uno : varios}`;
const mayuscula = (t) => (t ? t.charAt(0).toLocaleUpperCase('es-ES') + t.slice(1) : t);

/** "del 5 al 7 de febrero de 2027", "del 30 de abril al 2 de mayo de 2027", "el 12 de diciembre de 2027". */
function rangoFechas(ini, fin) {
  const a = aFecha(ini);
  if (!a) return null;
  const b = aFecha(fin) ?? a;
  if (a.toDateString() === b.toDateString()) return `el ${fmtLargo.format(a)}`;
  if (a.getFullYear() !== b.getFullYear()) return `del ${fmtLargo.format(a)} al ${fmtLargo.format(b)}`;
  if (a.getMonth() === b.getMonth()) return `del ${a.getDate()} al ${fmtLargo.format(b)}`;
  return `del ${fmtDiaMes.format(a)} al ${fmtLargo.format(b)}`;
}

const fmtMesCorto = new Intl.DateTimeFormat('es-ES', { month: 'short' });
const mesCorto = (d) => fmtMesCorto.format(d).replace('.', '');

/** Fechas cortas para el desplegable: "12 dic", "5–7 feb", "30 abr – 2 may". */
function fechasCortas(ini, fin) {
  const a = aFecha(ini);
  if (!a) return null;
  const z = aFecha(fin) ?? a;
  if (a.toDateString() === z.toDateString()) return `${a.getDate()} ${mesCorto(a)}`;
  if (a.getMonth() === z.getMonth() && a.getFullYear() === z.getFullYear()) return `${a.getDate()}–${z.getDate()} ${mesCorto(a)}`;
  return `${a.getDate()} ${mesCorto(a)} – ${z.getDate()} ${mesCorto(z)}`;
}

const textoFianza = (t) => (Number(t.fianza) > 0 ? fmtEuros.format(Number(t.fianza)) : 'a negociar directamente con la FEP');

/**
 * Plazo: la API decide si la convocatoria está abierta; además se comprueba la fecha límite con el reloj del
 * navegador (hora local de España sin zona) para que el mock y las respuestas cacheadas no la den por abierta
 * pasado el plazo.
 */
function convocatoriaAbierta(datos) {
  if (datos.abierta !== true) return false;
  const limite = aFecha(datos.fechaLimite);
  return !limite || new Date() < limite;
}

/* ---------------------------------------------------------------------------
   Estado de la convocatoria, documentos y pasos
   --------------------------------------------------------------------------- */
function pintarEstado(datos, abierta) {
  $('#convocatoria-titulo').textContent = limpio(datos.titulo) || 'Organización de torneos';
  $('#orgt-estado-etiqueta').textContent = abierta ? 'Convocatoria abierta' : 'Convocatoria cerrada';
  $('#convocatoria').classList.toggle('orgt-estado--cerrada', !abierta);

  const parrafos = abierta
    ? (Array.isArray(datos.introduccion) ? datos.introduccion : []).map(limpio).filter(Boolean)
    : [limpio(datos.avisoCerrado) || 'El plazo para solicitar la organización de torneos ha finalizado.'];
  $('#orgt-intro').replaceChildren(...parrafos.map((p) => el('p', { text: p })));

  const limite = aFecha(datos.fechaLimite);
  const plazo = $('#orgt-plazo');
  if (limite) {
    plazo.replaceChildren(
      abierta ? 'Plazo para presentar solicitudes: hasta el ' : 'El plazo terminó el ',
      el('time', { datetime: String(datos.fechaLimite).slice(0, 16), text: `${fmtLargo.format(limite)} a las ${fmtHora.format(limite)}` }),
      ' (hora peninsular).');
    plazo.hidden = false;
  }
  $('#convocatoria').hidden = false;
}

function pintarDocumentos(documentos) {
  const docs = (Array.isArray(documentos) ? documentos : []).filter((d) => d && limpio(d.titulo) && urlSegura(d.url));
  $('#orgt-docs-total').textContent = String(docs.length);
  $('#orgt-docs').replaceChildren(...docs.map((d) => {
    const url = urlSegura(d.url);
    const titulo = limpio(d.titulo);
    return el('li', { class: 'doc' },
      el('span', { class: 'doc__icon', 'aria-hidden': 'true', text: 'PDF' }),
      el('div', { class: 'doc__body' },
        el('a', { class: 'doc__title', href: url, target: '_blank', rel: 'noopener' }, titulo,
          el('span', { class: 'sr-only', text: ' (PDF, se abre en una pestaña nueva)' })),
        el('p', { class: 'doc__meta' },
          el('span', { class: 'org-tag', text: 'PDF' }),
          limpio(d.descripcion) ? el('span', { text: limpio(d.descripcion) }) : null)),
      el('a', { class: 'doc__download', href: url, target: '_blank', rel: 'noopener', 'aria-label': `Descargar en PDF: ${titulo}` },
        'Descargar ', el('span', { 'aria-hidden': 'true', text: '↓' })));
  }));
  $('#documentos').hidden = !docs.length;
}

function pintarPasos(pasos) {
  const lista = (Array.isArray(pasos) ? pasos : []).filter((p) => p && limpio(p.titulo))
    .sort((a, b) => (a.orden ?? 0) - (b.orden ?? 0));
  $('#orgt-pasos').replaceChildren(...lista.map((p, i) => el('li', { class: `orgt-paso${i === 0 ? ' orgt-paso--aqui' : ''}` },
    el('span', { class: 'orgt-paso__num', 'aria-hidden': 'true', text: String(i + 1) }),
    el('div', {},
      el('p', { class: 'orgt-paso__title' }, limpio(p.titulo),
        i === 0 ? el('span', { class: 'org-tag org-tag--red', text: 'En esta página' }) : null),
      limpio(p.texto) ? el('p', { class: 'orgt-paso__text', text: limpio(p.texto) }) : null))));
  $('#proceso').hidden = !lista.length;
}

/* ---------------------------------------------------------------------------
   Formulario: desplegables
   --------------------------------------------------------------------------- */
function pintarTorneos() {
  const select = $('#orgt-torneo');
  const opciones = [el('option', { value: '0', text: 'Selecciona una prueba' })];
  for (const g of grupos) {
    const delGrupo = torneos.filter((t) => t.grupo === g.clave);
    if (!delGrupo.length) continue;
    opciones.push(el('optgroup', { label: limpio(g.titulo) }, delGrupo.map((t) => {
      const fechas = fechasCortas(t.fechaInicio, t.fechaFin);
      return el('option', { value: String(t.id), text: fechas ? `${t.titulo} · ${fechas}` : t.titulo });
    })));
  }
  select.replaceChildren(...opciones);
}

function mostrarInfoTorneo() {
  const info = $('#orgt-torneo-info');
  const t = torneos.find((x) => String(x.id) === $('#orgt-torneo').value);
  if (!t) { info.hidden = true; info.replaceChildren(); return; }
  const fechas = rangoFechas(t.fechaInicio, t.fechaFin);
  info.replaceChildren(
    fechas ? el('span', {}, el('strong', { text: 'Fechas: ' }), mayuscula(fechas)) : null,
    el('span', {}, el('strong', { text: 'Fianza: ' }), textoFianza(t)));
  info.hidden = false;
}

function pintarComunidades() {
  $('#orgt-comunidad').replaceChildren(
    el('option', { value: '0', text: 'Selecciona una comunidad' }),
    ...comunidades.map((c) => el('option', { value: String(c.id), text: limpio(c.nombre) })));
}

function cambiarComunidad() {
  const c = comunidades.find((x) => String(x.id) === $('#orgt-comunidad').value);
  const select = $('#orgt-provincia');
  const provincias = (c?.provincias ?? []).filter((p) => p && Number.isInteger(p.id) && limpio(p.nombre));
  select.replaceChildren(
    el('option', { value: '0', text: c ? 'Selecciona una provincia' : 'Elige antes la comunidad' }),
    ...provincias.map((p) => el('option', { value: String(p.id), text: limpio(p.nombre) })));
  select.disabled = !c;
  // Con una sola provincia (Asturias, Madrid…) se elige sola.
  if (provincias.length === 1) select.value = String(provincias[0].id);
  cambiarProvincia();
}

async function cambiarProvincia() {
  const id = Number($('#orgt-provincia').value);
  const select = $('#orgt-club');
  const pet = ++peticionClubes;
  if (!(id > 0)) {
    select.replaceChildren(el('option', { value: '0', text: 'Elige antes la provincia' }));
    select.disabled = true;
    return;
  }
  select.replaceChildren(el('option', { value: '0', text: 'Cargando clubes…' }));
  select.disabled = true;
  try {
    let clubes = await get(`/api/v1/competicion/organizacion-torneos/clubes?provincia=${id}`);
    if (pet !== peticionClubes) return;
    if (!Array.isArray(clubes)) throw new Error('Respuesta sin clubes');
    // En modo prototipo el mock trae todos los clubes: se quedan los de la provincia (con la API no cambia nada).
    if (clubes.some((c) => c && c.provinciaId !== id)) todosLosClubes = clubes;
    clubes = (todosLosClubes ?? clubes).filter((c) => c && c.provinciaId === id && limpio(c.id) && limpio(c.nombre));
    select.replaceChildren(
      el('option', { value: '0', text: clubes.length ? 'Selecciona un club' : 'No hay clubes en esta provincia: escríbelo abajo' }),
      ...clubes.map((c) => el('option', { value: limpio(c.id), text: limpio(c.nombre) })));
    select.disabled = !clubes.length;
  } catch (e) {
    if (pet !== peticionClubes) return;
    console.warn('[FEP] clubes de la provincia:', e.message);
    select.replaceChildren(el('option', { value: '0', text: 'No se ha podido cargar la lista: escribe el club abajo' }));
    select.disabled = true;
  }
}

function pintarProteccionDatos(parrafos) {
  const textos = (Array.isArray(parrafos) ? parrafos : []).map(limpio).filter(Boolean);
  const caja = $('#orgt-lopd');
  if (!textos.length) { caja.hidden = true; return; }
  caja.replaceChildren(el('details', { class: 'org-more orgt-lopd__details' },
    el('summary', { text: 'Leer la información sobre protección de datos' }),
    ...textos.map((t) => el('p', { text: t }))));
}

/* ---------------------------------------------------------------------------
   Formulario: validación (validar() del ASP) y envío a la web actual
   --------------------------------------------------------------------------- */
const valor = (id) => limpio($(id).value);

function errores() {
  const lista = [];
  const exigir = (cond, id, texto) => { if (!cond) lista.push([id, texto]); };
  exigir(valor('#orgt-torneo') !== '0' && valor('#orgt-torneo') !== '', '#orgt-torneo', 'Selecciona la prueba que quieres organizar.');
  exigir(valor('#orgt-comunidad') !== '0', '#orgt-comunidad', 'Selecciona la comunidad autónoma donde se disputaría la prueba.');
  exigir(Number(valor('#orgt-provincia')) > 0, '#orgt-provincia', 'Selecciona la provincia donde se disputaría la prueba.');
  exigir(valor('#orgt-localidad'), '#orgt-localidad', 'Indica la localidad donde se disputaría la prueba.');
  const club = valor('#orgt-club');
  exigir((club && club !== '0') || valor('#orgt-club2'), $('#orgt-club').disabled ? '#orgt-club2' : '#orgt-club',
    'Selecciona el club sede o escribe su nombre.');
  exigir(valor('#orgt-organizador'), '#orgt-organizador', 'Indica la entidad organizadora que solicita la prueba.');
  exigir(valor('#orgt-responsable'), '#orgt-responsable', 'Indica el nombre y apellidos de la persona responsable.');
  const tel = valor('#orgt-telefono');
  exigir(tel, '#orgt-telefono', 'Indica un teléfono de contacto.');
  if (tel) exigir(TELEFONO.test(tel), '#orgt-telefono', 'El teléfono solo puede tener números, espacios y los signos + ( ) . - (máximo 15).');
  const email = valor('#orgt-email');
  exigir(email, '#orgt-email', 'Indica un email de contacto.');
  if (email) exigir(EMAIL.test(email), '#orgt-email', 'El email no parece válido.');
  exigir($('#orgt-acepto').checked, '#orgt-acepto', 'Debes aceptar la política de protección de datos.');
  return lista;
}

function marcarErrores(lista) {
  const conError = new Set(lista.map(([id]) => id));
  for (const campo of $$('#orgt-form select, #orgt-form input:not([type="hidden"])')) {
    const id = `#${campo.id}`;
    if (conError.has(id)) campo.setAttribute('aria-invalid', 'true');
    else campo.removeAttribute('aria-invalid');
  }
  const caja = $('#orgt-errores');
  $('#orgt-errores-lista').replaceChildren(...lista.map(([id, texto]) => el('li', {},
    el('a', { href: id, onclick: (e) => { e.preventDefault(); $(id).focus(); } }, texto))));
  caja.hidden = !lista.length;
}

function initFormulario(envioUrl) {
  const form = $('#orgt-form');
  const destino = urlSegura(envioUrl);
  if (destino) form.action = destino;

  $('#orgt-torneo').addEventListener('change', mostrarInfoTorneo);
  $('#orgt-comunidad').addEventListener('change', cambiarComunidad);
  $('#orgt-provincia').addEventListener('change', cambiarProvincia);

  // Tras un error, cada campo se revalida al cambiarlo.
  form.addEventListener('change', () => { if (!$('#orgt-errores').hidden) marcarErrores(errores()); });

  form.addEventListener('submit', (e) => {
    const lista = errores();
    marcarErrores(lista);
    if (lista.length) {
      e.preventDefault();
      $('#orgt-errores').focus();
      return;
    }
    // Se quitan los espacios sobrantes; el resto de la limpieza la sigue haciendo Organizacion_P0.asp.
    for (const campo of $$('#orgt-form input[type="text"], #orgt-form input[type="tel"], #orgt-form input[type="email"]')) {
      campo.value = limpio(campo.value);
    }
    // Un <select> deshabilitado no se envía: Organizacion_P0.asp necesita club = "0" para usar club2 (con club
    // vacío buscaría IdClub = '' y guardaría "No designado").
    const club = $('#orgt-club');
    if (club.disabled || !club.value) {
      club.disabled = false;
      club.value = '0';
    }
    // Evita envíos dobles (el ASP ocultaba el botón y mostraba "Guardando…").
    const boton = $('#orgt-enviar');
    setTimeout(() => {
      boton.disabled = true;
      boton.replaceChildren('Enviando la solicitud…');
      $('#orgt-envio-ayuda').textContent = 'Se están guardando los datos. Puede tardar unos segundos según tu conexión.';
    });
  });

  // Al volver con el botón "Atrás" del navegador, el botón tiene que volver a estar disponible.
  window.addEventListener('pageshow', () => {
    const boton = $('#orgt-enviar');
    if (!boton.disabled) return;
    boton.disabled = false;
    boton.replaceChildren('Continuar con la solicitud ', el('span', { 'aria-hidden': 'true', text: '→' }));
  });
}

/* ---------------------------------------------------------------------------
   Pruebas por mes (calendario lateral del ASP: por mes de la fecha de fin)
   --------------------------------------------------------------------------- */
function claveMes(t) {
  const f = aFecha(t.fechaFin) ?? aFecha(t.fechaInicio);
  return f ? `${f.getFullYear()}-${String(f.getMonth() + 1).padStart(2, '0')}` : 'sin-fecha';
}

function elegirPrueba(t) {
  const select = $('#orgt-torneo');
  select.value = String(t.id);
  mostrarInfoTorneo();
  if (!$('#orgt-errores').hidden) marcarErrores(errores());
  $('#solicitud').scrollIntoView({ block: 'start' });
  select.focus({ preventScroll: true });
}

function itemPrueba(t, abierta) {
  const b = bloqueFecha(t.fechaInicio, t.fechaFin);
  const [etiqueta, clase] = ETIQUETAS_GRUPO[t.grupo] ?? [null, null];
  const fechas = rangoFechas(t.fechaInicio, t.fechaFin);
  return el('article', { class: 'camp-item orgt-item' },
    el('div', { class: 'camp-item__date' }, b
      ? el('time', { datetime: b.iso },
        el('span', { class: 'event__days', text: b.dias }),
        el('span', { class: 'event__month', text: b.mes }))
      : el('span', { class: 'calf-date-tbd', text: 'Fecha por confirmar' })),
    el('div', { class: 'camp-item__body' },
      etiqueta ? el('p', { class: 'camp-item__tags' }, el('span', { class: clase, text: etiqueta })) : null,
      el('h3', { class: 'camp-item__title', text: t.titulo }),
      el('p', { class: 'camp-item__place' },
        fechas ? `${mayuscula(fechas)} · ` : '', 'Fianza: ', textoFianza(t)),
      abierta && t.grupo
        ? el('button', { type: 'button', class: 'orgt-elegir', onclick: () => elegirPrueba(t), 'aria-label': `Solicitar: ${t.titulo}` },
          'Solicitar ', el('span', { 'aria-hidden': 'true', text: '→' }))
        : null));
}

function pintarPruebas(abierta) {
  const grupo = $('#orgt-grupo').value;
  const lista = torneos.filter((t) => !grupo || t.grupo === grupo);
  const meses = new Map();
  for (const t of lista) {
    const k = claveMes(t);
    if (!meses.has(k)) meses.set(k, []);
    meses.get(k).push(t);
  }
  const claves = [...meses.keys()].sort((a, b) => (a === 'sin-fecha') - (b === 'sin-fecha') || a.localeCompare(b));
  $('#orgt-lista').replaceChildren(...claves.map((k) => {
    const items = meses.get(k);
    const titulo = k === 'sin-fecha' ? 'Fecha por confirmar' : mayuscula(fmtMes.format(aFecha(`${k}-01`)));
    const id = `mes-${k}`;
    return el('section', { class: 'camp-month', 'aria-labelledby': id },
      el('h3', { class: 'camp-month__title', id }, titulo, el('span', { class: 'org-count', text: String(items.length) })),
      items.map((t) => itemPrueba(t, abierta)));
  }));
  $('#orgt-total').textContent = String(lista.length);
  $('#orgt-estado').textContent = grupo
    ? `${plural(lista.length, 'prueba', 'pruebas')} de ${plural(torneos.length, 'prueba', 'pruebas')}`
    : plural(lista.length, 'prueba', 'pruebas');
}

function initFiltro(abierta) {
  const presentes = new Set(torneos.map((t) => t.grupo).filter(Boolean));
  $('#orgt-grupo').replaceChildren(
    el('option', { value: '', text: 'Todas' }),
    ...grupos.filter((g) => presentes.has(g.clave)).map((g) => el('option', { value: g.clave, text: limpio(g.titulo) })));
  $('#orgt-grupo').addEventListener('change', () => pintarPruebas(abierta));
  $('#orgt-filtro').addEventListener('submit', (e) => e.preventDefault());
}

/* ---------------------------------------------------------------------------
   Índice lateral y resumen
   --------------------------------------------------------------------------- */
const SECCIONES = [
  ['convocatoria', 'Convocatoria'],
  ['documentos', 'Documentos'],
  ['proceso', 'Cómo se solicita'],
  ['solicitud', 'Formulario'],
  ['pruebas', 'Pruebas'],
];

function pintarIndice() {
  const visibles = SECCIONES.filter(([id]) => !document.getElementById(id).hidden);
  $('#orgt-indice').replaceChildren(...visibles.map(([id, texto]) => el('li', {},
    el('a', { href: `#${id}`, dataset: { seccion: id } }, el('span', { text: texto })))));

  if (!('IntersectionObserver' in window)) return;
  const enlaces = new Map($$('#orgt-indice a').map((a) => [a.dataset.seccion, a]));
  const obs = new IntersectionObserver((entradas) => {
    for (const e of entradas) {
      if (!e.isIntersecting) continue;
      enlaces.forEach((a, id) => {
        const activo = id === e.target.id;
        a.classList.toggle('is-active', activo);
        if (activo) a.setAttribute('aria-current', 'location'); else a.removeAttribute('aria-current');
      });
    }
  }, { rootMargin: '-35% 0px -55% 0px' });
  visibles.forEach(([id]) => obs.observe(document.getElementById(id)));
}

function pintarResumen(datos, abierta) {
  const limite = aFecha(datos.fechaLimite);
  const items = [
    Number.isInteger(datos.temporada) ? [String(datos.temporada), 'temporada en licitación'] : null,
    abierta && torneos.length ? [fmtNum.format(torneos.length), torneos.length === 1 ? 'prueba' : 'pruebas'] : null,
    abierta && limite ? [fmtDiaMes.format(limite), `fin del plazo, a las ${fmtHora.format(limite)} h`] : null,
    !abierta ? ['Cerrada', 'convocatoria'] : null,
  ].filter(Boolean);
  $('#orgt-resumen').replaceChildren(...items.map(([n, t]) => el('li', {}, el('strong', { text: n }), ` ${t}`)));
}

/* ---------------------------------------------------------------------------
   Arranque
   --------------------------------------------------------------------------- */
async function init() {
  const cont = $('#orgt-contenido');
  try {
    const datos = await get('/api/v1/competicion/organizacion-torneos');
    if (!datos || typeof datos !== 'object' || !limpio(datos.titulo)) throw new Error('Respuesta sin convocatoria');
    const abierta = convocatoriaAbierta(datos);

    grupos = (Array.isArray(datos.grupos) ? datos.grupos : []).filter((g) => g && limpio(g.clave) && limpio(g.titulo));
    torneos = abierta
      ? (Array.isArray(datos.torneos) ? datos.torneos : [])
        .filter((t) => t && Number.isInteger(t.id) && limpio(t.titulo))
        .map((t) => ({ ...t, titulo: limpio(t.titulo) }))
      : [];
    comunidades = abierta ? (Array.isArray(datos.comunidades) ? datos.comunidades : []).filter((c) => c && Number.isInteger(c.id)) : [];

    $('#orgt-skeleton').remove();
    pintarEstado(datos, abierta);
    pintarDocumentos(datos.documentos);

    if (abierta && torneos.length) {
      pintarPasos(datos.pasos);
      pintarTorneos();
      pintarComunidades();
      pintarProteccionDatos(datos.proteccionDatos);
      initFormulario(datos.envioUrl);
      $('#solicitud').hidden = false;
      initFiltro(abierta);
      pintarPruebas(abierta);
      $('#pruebas').hidden = false;
    } else if (abierta) {
      $('#orgt-intro').append(el('p', { class: 'orgt-sin-pruebas', text: 'Todavía no hay pruebas publicadas para solicitar. Vuelve a consultar esta página más adelante.' }));
    }

    pintarResumen(datos, abierta);
    pintarIndice();
    if (location.hash) document.getElementById(decodeURIComponent(location.hash.slice(1)))?.scrollIntoView();
  } catch (e) {
    console.warn('[FEP] organización de torneos:', e.message);
    $('#orgt-skeleton')?.remove();
    $('#orgt-error').hidden = false;
  } finally {
    cont.setAttribute('aria-busy', 'false');
  }
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
})();
