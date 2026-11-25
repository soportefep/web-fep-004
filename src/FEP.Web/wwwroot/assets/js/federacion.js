/**
 * Página "Ficha de federación" (migrada desde Datos_Federacion.asp, URL limpia /Federacion/{id}/{nombre}).
 * Requiere assets/js/comun.js (cabecera, menú, pie y utilidades en window.FEP).
 *
 *   URL: federacion.html?id=6 (también acepta Id, como Datos_Federacion.asp; 0 = FEP, total nacional)
 *   Ficha y licencias → GET /api/v1/federacion/federaciones-autonomicas/{id}
 *                       (mock/federacion-federaciones-autonomicas-{id}.json)
 *   Nombre oficial, territorio, bandera y web revisados, y lista de federaciones
 *                     → GET /api/v1/federacion/federaciones-autonomicas (el mismo de federaciones.html)
 *
 * Secciones con ancla: #contacto, #licencias, #evolucion y #perfil (género y edad). Los gráficos de
 * Highcharts del original se sustituyen por barras hechas con HTML/CSS que se leen como texto (cada barra
 * lleva su cifra) y, para la evolución, una tabla con los datos. Todo texto de la API va por textContent.
 */
(function () {
'use strict';

const { $, $$, el, get, urlSegura, aFecha, fmtLargo } = window.FEP;
const { LEGACY_BASE } = window.FEPApi;

const ID = /^(0|[1-9]|1\d|2[0-2])$/;    // 0–22 (FEP + 22 federaciones autonómicas), como la API
const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const SITIO = 'Federación Española de Pádel';

/* ---------------------------------------------------------------------------
   Utilidades
   --------------------------------------------------------------------------- */
/** ?id=6, ?Id=6 o ?ID=6 (Request() de ASP no distingue mayúsculas). */
function leerId(search = location.search) {
  const q = new Map([...new URLSearchParams(search)].map(([k, v]) => [k.toLowerCase(), v.trim()]));
  const id = q.get('id') ?? '';
  return ID.test(id) ? Number(id) : null;
}

const urlWebActual = (id) => new URL(id == null ? 'Federaciones' : `Datos_Federacion.asp?Id=${id}`, LEGACY_BASE).href;

/** 8863 → "8.863" (Intl con es-ES no agrupa los números de 4 cifras; la web actual sí). */
const num = (n) => (Number.isFinite(n) ? String(Math.trunc(n)).replace(/\B(?=(\d{3})+(?!\d))/g, '.') : '—');
const pct = (parte, total) => (total > 0 ? `${((parte / total) * 100).toFixed(1).replace('.', ',')} %` : '—');
const plural = (n, uno, varios) => `${num(n)} ${n === 1 ? uno : varios}`;
const entero = (v) => (Number.isFinite(v) && v >= 0 ? Math.trunc(v) : 0);

/** Imagen: absolutas (API real) validadas con urlSegura; relativas (mocks) tal cual. */
function urlImagen(ruta) {
  if (!ruta || typeof ruta !== 'string') return null;
  const limpia = ruta.trim();
  if (/^https?:\/\//i.test(limpia)) return urlSegura(limpia);
  if (/^[a-z][a-z0-9+.-]*:|^\/\//i.test(limpia)) return null; // javascript:, data:, //host… no
  return limpia;
}

/** "https://www.fap.es/" → "fap.es" */
function dominio(url) {
  try { return new URL(url).hostname.replace(/^www\./, ''); } catch { return url; }
}

/** Teléfono → enlace tel: solo si tiene entre 9 y 15 dígitos. */
function enlaceTelefono(t) {
  const digitos = t.replace(/\D/g, '');
  if (digitos.length < 9 || digitos.length > 15) return el('span', { text: t });
  return el('a', { href: `tel:${digitos.length === 9 ? `+34${digitos}` : `+${digitos}`}`, text: t });
}

const nuevaPestana = () => el('span', { class: 'sr-only', text: ' (se abre en una pestaña nueva)' });

/* ---------------------------------------------------------------------------
   Secciones
   --------------------------------------------------------------------------- */
function cabecera(clave, n, titulo, lead, meta) {
  const idTitulo = `${clave}-titulo`;
  return {
    idTitulo,
    nodo: el('header', { class: 'organo__head' },
      el('p', { class: 'organo__num', 'aria-hidden': 'true', text: String(n).padStart(2, '0') }),
      el('div', {},
        el('h2', { class: 'h2 organo__title', id: idTitulo, text: titulo }),
        lead ? el('p', { class: 'organo__lead', text: lead }) : null,
        meta ? el('p', { class: 'organo__meta', text: meta }) : null)),
  };
}

function seccion(clave, n, titulo, lead, meta, ...hijos) {
  const { idTitulo, nodo } = cabecera(clave, n, titulo, lead, meta);
  return el('section', { class: 'organo', id: clave, 'aria-labelledby': idTitulo }, nodo, ...hijos);
}

function datoContacto(etiqueta, ...valor) {
  return el('div', { class: 'fedf-contact__item' },
    el('dt', { text: etiqueta }),
    el('dd', {}, ...valor));
}

function seccionContacto(f, info, n) {
  const web = urlSegura(info?.web) ?? urlSegura(f.web);
  const email = f.email && EMAIL.test(f.email) ? f.email : null;
  const telefonos = (f.telefonos ?? []).filter(Boolean);
  const lineaPostal = [f.codigoPostal, f.localidad].filter(Boolean).join(' ');

  const datos = [
    f.presidente ? datoContacto('Presidencia', f.presidente) : null,
    f.direccion || lineaPostal ? datoContacto('Dirección',
      f.direccion ? el('span', { class: 'fedf-contact__line', text: f.direccion }) : null,
      lineaPostal ? el('span', { class: 'fedf-contact__line', text: lineaPostal }) : null) : null,
    telefonos.length ? datoContacto(telefonos.length === 1 ? 'Teléfono' : 'Teléfonos',
      ...telefonos.flatMap((t, i) => [i ? el('span', { 'aria-hidden': 'true', text: ' · ' }) : null, enlaceTelefono(t)])) : null,
    email ? datoContacto('Correo electrónico', el('a', { href: `mailto:${email}` }, email.split('@')[0], el('wbr'), `@${email.split('@')[1]}`)) : null,
    web ? datoContacto('Web', el('a', { href: web, target: '_blank', rel: 'noopener' },
      dominio(web), el('span', { 'aria-hidden': 'true', text: ' ↗' }), nuevaPestana())) : null,
  ].filter(Boolean);

  return seccion('contacto', n, 'Datos de contacto', null, null,
    datos.length
      ? el('dl', { class: 'fedf-contact' }, datos)
      : el('p', { class: 'empty', text: 'La federación no tiene datos de contacto publicados.' }));
}

function kpi(etiqueta, valor, ...detalle) {
  return el('li', { class: 'fedf-kpi' },
    el('p', { class: 'fedf-kpi__label', text: etiqueta }),
    el('p', { class: 'fedf-kpi__value', text: num(valor) }),
    detalle.some(Boolean) ? el('p', { class: 'fedf-kpi__detail' }, ...detalle) : null);
}

function seccionLicencias(f, l, n) {
  const clubes = f.nacional ? 'clubes.html' : `clubes.html?federacion=${encodeURIComponent(f.id)}`;
  const puesto = !f.nacional && l.puesto
    ? `Puesto ${l.puesto}.º de ${plural(entero(l.federacionesEnRanking), 'federación autonómica', 'federaciones autonómicas')}`
    : null;
  return seccion('licencias', n, `Licencias ${l.anio}`,
    'Los datos están actualizados en línea con la base de datos de la FEP. Al inicio de cada año empieza el proceso de renovación, así que las cifras van aumentando a lo largo del año.',
    f.nacional ? 'Total nacional: licencias de todas las federaciones autonómicas.' : null,
    el('ul', { class: 'fedf-kpis' },
      kpi('Licencias de jugador/a', l.jugadores, puesto),
      kpi('Licencias de técnico nacional', l.tecnicos),
      kpi('Licencias de juez-árbitro nacional', l.arbitros),
      kpi('Clubes federados', l.clubes, el('a', { class: 'link-red link-red--sm', href: clubes },
        'Ver los clubes', el('span', { 'aria-hidden': 'true', text: '→' })))));
}

/* Evolución: columnas (una por año) y tabla con la variación interanual */
function seccionEvolucion(l, n) {
  const serie = (l.evolucion ?? []).filter((e) => e && Number.isInteger(e.anio)).sort((a, b) => a.anio - b.anio);
  const max = Math.max(1, ...serie.map((e) => e.licencias ?? 0));
  const primero = serie.find((e) => e.licencias != null);
  const ultimo = serie[serie.length - 1];
  const meta = primero && ultimo && primero !== ultimo
    ? `De ${num(primero.licencias)} licencias en ${primero.anio} a ${num(ultimo.licencias)} en ${ultimo.anio}${ultimo.enCurso ? ' (año en curso)' : ''}.`
    : null;

  const columnas = el('ol', { class: 'fedf-chart', 'aria-describedby': 'evolucion-nota' }, serie.map((e) => {
    const sinDato = e.licencias == null;
    const alto = sinDato ? 0 : Math.max(1, Math.round((e.licencias / max) * 100));
    return el('li', { class: `fedf-chart__col${e.enCurso ? ' is-current' : ''}${sinDato ? ' is-empty' : ''}` },
      // Lo visible va oculto al lector de pantalla, que lee una sola frase por año.
      el('span', { class: 'fedf-chart__value', 'aria-hidden': 'true', text: sinDato ? 's. d.' : num(e.licencias) }),
      el('span', { class: 'fedf-chart__bar', style: `--alto: ${alto}%`, 'aria-hidden': 'true' }),
      el('span', { class: 'fedf-chart__year', 'aria-hidden': 'true', text: String(e.anio) }),
      el('span', { class: 'sr-only', text: `${e.anio}${e.enCurso ? ' (año en curso)' : ''}: ${sinDato ? 'sin dato' : `${num(e.licencias)} licencias`}` }));
  }));

  const filas = serie.map((e, i) => {
    const previo = serie[i - 1]?.licencias;
    const variacion = e.licencias != null && previo ? ((e.licencias - previo) / previo) * 100 : null;
    return el('tr', {},
      el('th', { scope: 'row', text: `${e.anio}${e.enCurso ? ' (en curso)' : ''}` }),
      el('td', { class: 'fedf-num', text: e.licencias == null ? 'Sin dato' : num(e.licencias) }),
      el('td', { class: 'fedf-num', text: variacion == null ? '—' : `${variacion > 0 ? '+' : ''}${variacion.toFixed(1).replace('.', ',')} %` }));
  });

  return seccion('evolucion', n, 'Evolución de las licencias de jugador/a', null, meta,
    el('figure', { class: 'fedf-figure' },
      el('div', { class: 'fedf-chart-wrap', tabindex: '0', role: 'region', 'aria-label': 'Licencias de jugador/a por año' }, columnas),
      el('figcaption', { id: 'evolucion-nota' },
        `Fuente: base de datos de la FEP. Los años cerrados salen del histórico de la federación; ${ultimo?.enCurso ? `${ultimo.anio} es el año en curso y su cifra sigue creciendo con las renovaciones.` : 'el último es el año en curso.'}`)),
    el('details', { class: 'org-more' },
      el('summary', { text: 'Ver los datos en una tabla' }),
      el('table', { class: 'org-table transp-amounts fedf-table' },
        el('caption', { text: 'Licencias de jugador/a por año' }),
        el('thead', {}, el('tr', {},
          el('th', { scope: 'col', text: 'Año' }),
          el('th', { scope: 'col', text: 'Licencias' }),
          el('th', { scope: 'col', text: 'Variación' }))),
        el('tbody', {}, filas))));
}

/* Género y edad: barra apilada y barras horizontales con cifra y porcentaje */
function filaBarra(etiqueta, valor, total, modificador) {
  const ancho = total > 0 ? (valor / total) * 100 : 0;
  return el('li', { class: 'fedf-bars__row' },
    el('span', { class: 'fedf-bars__label', text: etiqueta }),
    el('span', { class: 'fedf-bars__track', 'aria-hidden': 'true' },
      el('span', { class: `fedf-bars__fill${modificador ? ` fedf-bars__fill--${modificador}` : ''}`, style: `--ancho: ${ancho.toFixed(2)}%` })),
    el('span', { class: 'fedf-bars__value' }, el('strong', { text: num(valor) }), ` · ${pct(valor, total)}`));
}

function seccionPerfil(l, n) {
  const g = l.genero ?? {};
  const e = l.edades ?? {};
  const [masc, fem, sinGenero] = [entero(g.masculino), entero(g.femenino), entero(g.sinDato)];
  const totalGenero = masc + fem + sinGenero;
  const tramos = [
    ['Menores (hasta 18 años)', entero(e.menores)],
    ['Sub-23 (19 a 23 años)', entero(e.sub23)],
    ['Sénior (24 a 39 años)', entero(e.senior)],
    ['Veteranos (40 años o más)', entero(e.veteranos)],
  ];
  if (entero(e.sinDato)) tramos.push(['Sin fecha de nacimiento', entero(e.sinDato)]);
  const totalEdad = tramos.reduce((s, [, v]) => s + v, 0);

  if (!totalGenero && !totalEdad) {
    return seccion('perfil', n, 'Género y edad', null, null,
      el('p', { class: 'empty', text: `No hay licencias de jugador/a tramitadas en ${l.anio}.` }));
  }

  const generos = [['Masculino', masc, 'masc'], ['Femenino', fem, 'fem']];
  if (sinGenero) generos.push(['Sin dato', sinGenero, 'otro']);

  return seccion('perfil', n, 'Género y edad', `Distribución de las ${plural(l.jugadores, 'licencia', 'licencias')} de jugador/a de ${l.anio}.`, null,
    el('div', { class: 'org-groups' },
      el('section', { class: 'org-group', 'aria-labelledby': 'perfil-genero' },
        el('h3', { class: 'org-group__title', id: 'perfil-genero', text: 'Por género' }),
        el('p', { class: 'fedf-stack', 'aria-hidden': 'true' }, generos.map(([, v, m]) =>
          el('span', { class: `fedf-stack__part fedf-stack__part--${m}`, style: `--ancho: ${(totalGenero ? (v / totalGenero) * 100 : 0).toFixed(2)}%` }))),
        el('ul', { class: 'fedf-bars' }, generos.map(([t, v, m]) => filaBarra(t, v, totalGenero, m)))),
      el('section', { class: 'org-group', 'aria-labelledby': 'perfil-edad' },
        el('h3', { class: 'org-group__title', id: 'perfil-edad', text: 'Por edad' }),
        el('ul', { class: 'fedf-bars' }, tramos.map(([t, v]) => filaBarra(t, v, totalEdad))),
        el('p', { class: 'organo__meta', text: `Edad que se cumple en ${l.anio}.` }))));
}

/* ---------------------------------------------------------------------------
   Cabecera de página, índice y otras federaciones
   --------------------------------------------------------------------------- */
function pintarCabecera(f, info) {
  const nombre = info?.nombre || f.nombre;
  const territorio = info?.territorio || nombre;
  const l = f.licencias;
  $('#titulo-pagina').textContent = `${nombre.replace(/[.\s]+$/, '')}.`;
  $('#fed-miga').textContent = territorio;
  $('#fed-eyebrow').textContent = f.nacional ? 'Federación nacional' : 'Federación autonómica';
  $('#fed-entradilla').textContent = f.nacional
    ? `Datos de contacto de la ${SITIO} y total nacional de licencias de ${l.anio}, actualizado con la base de datos de la FEP.`
    : `Datos de contacto y licencias de ${l.anio} de la federación de ${territorio}, actualizados con la base de datos de la FEP.`;

  const bandera = urlImagen(info?.imagenUrl) ?? urlImagen(f.imagenUrl);
  const img = $('#fed-bandera');
  if (bandera) {
    img.addEventListener('error', () => { img.hidden = true; }, { once: true });
    img.src = bandera;
    img.hidden = false;
  }

  $('#fed-resumen').replaceChildren(...[
    el('li', {}, el('strong', { text: num(l.jugadores) }), ` licencias de jugador/a en ${l.anio}`),
    !f.nacional && l.puesto ? el('li', {}, el('strong', { text: `${l.puesto}.º` }), ` de ${num(entero(l.federacionesEnRanking))} federaciones`) : null,
    el('li', {}, el('strong', { text: num(l.clubes) }), ` ${l.clubes === 1 ? 'club federado' : 'clubes federados'}`),
  ].filter(Boolean));

  document.title = `${nombre} · ${SITIO}`;
}

function pintarIndice(entradas) {
  $('#fed-indice').replaceChildren(...entradas.map(([clave, texto]) => el('li', {},
    el('a', { href: `#${clave}`, dataset: { clave } }, el('span', { text: texto })))));

  if (!('IntersectionObserver' in window)) return;
  const enlaces = new Map($$('#fed-indice a').map((a) => [a.dataset.clave, a]));
  const obs = new IntersectionObserver((vistas) => {
    for (const v of vistas) {
      if (!v.isIntersecting) continue;
      enlaces.forEach((a, clave) => {
        const activo = clave === v.target.id;
        a.classList.toggle('is-active', activo);
        if (activo) a.setAttribute('aria-current', 'location'); else a.removeAttribute('aria-current');
      });
    }
  }, { rootMargin: '-35% 0px -55% 0px' });
  entradas.forEach(([clave]) => { const s = document.getElementById(clave); if (s) obs.observe(s); });
}

function pintarOtras(federaciones, id) {
  const otras = federaciones.filter((f) => f.id !== id);
  if (!otras.length) return;
  $('#fed-otras-lista').replaceChildren(...otras.map((f) => el('li', {},
    el('a', { href: `federacion.html?id=${encodeURIComponent(f.id)}`, text: f.nacional ? 'Federación Española (total nacional)' : f.territorio }))),
  el('li', {}, el('a', { href: 'federaciones.html', text: 'Todas las federaciones →' })));
  $('#fed-otras').hidden = false;
}

function pie(f, l) {
  const consultado = aFecha(f.consultado);
  return el('p', { class: 'org-updated' },
    consultado ? ['Cifras de licencias de ', String(l.anio), ' consultadas el ',
      el('time', { datetime: String(f.consultado).slice(0, 16), text: fmtLargo.format(consultado) }), '. '] : null,
    el('a', { class: 'fedf-legacy', href: urlWebActual(f.id), target: '_blank', rel: 'noopener' },
      'Ver esta ficha en la web actual', el('span', { 'aria-hidden': 'true', text: ' ↗' }), nuevaPestana()));
}

/* ---------------------------------------------------------------------------
   Estados
   --------------------------------------------------------------------------- */
function mostrarError(texto, url) {
  $('#fed-skeleton')?.remove();
  const p = $('#fed-error');
  p.replaceChildren(`${texto} Puedes consultarla en la `, el('a', { href: url }, 'web actual de la FEP'), '.');
  p.hidden = false;
  $('#fed-contenido').setAttribute('aria-busy', 'false');
}

/* ---------------------------------------------------------------------------
   Arranque
   --------------------------------------------------------------------------- */
async function init() {
  const id = leerId();
  if (id == null) {
    mostrarError('No se ha indicado qué federación mostrar.', urlWebActual(null));
    return;
  }

  // La ficha es imprescindible; el listado solo aporta nombre oficial, territorio y la lista de federaciones.
  const [ficha, listado] = await Promise.allSettled([
    get(`/api/v1/federacion/federaciones-autonomicas/${id}`),
    get('/api/v1/federacion/federaciones-autonomicas'),
  ]);

  const f = ficha.status === 'fulfilled' ? ficha.value : null;
  if (!f || f.id !== id || !f.nombre || !f.licencias) {
    console.warn('[FEP] ficha de federación:', ficha.reason?.message ?? 'respuesta sin ficha');
    mostrarError('No se ha podido cargar la ficha de esta federación.', urlWebActual(id));
    return;
  }
  const federaciones = listado.status === 'fulfilled'
    ? (listado.value?.federaciones ?? []).filter((x) => x && Number.isInteger(x.id) && x.territorio)
    : [];
  const info = federaciones.find((x) => x.id === id) ?? null;
  const l = f.licencias;

  pintarCabecera(f, info);

  let n = 0;
  const secciones = [
    seccionContacto(f, info, ++n),
    seccionLicencias(f, l, ++n),
    seccionEvolucion(l, ++n),
    seccionPerfil(l, ++n),
  ];
  $('#fed-skeleton')?.remove();
  $('#fed-error').before(...secciones, pie(f, l));
  pintarIndice([['contacto', 'Contacto'], ['licencias', `Licencias ${l.anio}`], ['evolucion', 'Evolución'], ['perfil', 'Género y edad']]);
  pintarOtras(federaciones, id);
  $('#fed-contenido').setAttribute('aria-busy', 'false');

  if (location.hash) document.getElementById(decodeURIComponent(location.hash.slice(1)))?.scrollIntoView();
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
})();
