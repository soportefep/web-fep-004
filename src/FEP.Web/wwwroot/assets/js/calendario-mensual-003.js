/**
 * Calendario FEP · Prototipo 003 — Vista mensual (cuadrícula de días).
 * Complemento de calendario-fep.js: NO toca sus datos ni su lógica. Lee las fichas que pinta en
 * #calf-lista (una <section class="camp-month" id="mes-AAAA-MM"> por mes) y añade dentro de cada mes
 * una cuadrícula de lunes a domingo con cada prueba como una barra a lo largo de sus días.
 * La lista original se queda en el DOM: en móvil se muestra ella (la cuadrícula se oculta por CSS),
 * y el índice, los filtros y las anclas de calendario-fep.js siguen funcionando igual.
 * Se vuelve a pintar cada vez que calendario-fep.js cambia la lista (filtros, temporada).
 */
(function () {
'use strict';

const { el } = window.FEP;
const DIAS_SEMANA = ['L', 'M', 'X', 'J', 'V', 'S', 'D'];
const DIAS_SEMANA_LARGO = ['lunes', 'martes', 'miércoles', 'jueves', 'viernes', 'sábado', 'domingo'];
const fmtDia = new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'long' });

/** Listas de pruebas por mes: calendario FEP, territoriales, autonómicos (menores / veteranos) y campeonatos. */
const LISTAS = '#calf-lista, #calt-lista, #cal-lista, #camp-lista';
const MESES = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'];

/**
 * Año y mes (0–11) de una sección: por su id ("mes-2026-03", calendarios) o por su título
 * ("Noviembre de 2026", campeonatos, cuyas secciones no llevan id). null si no es un mes (p. ej. "Fecha por confirmar").
 */
function mesDeSeccion(seccion) {
  const porId = /^mes-(\d{4})-(\d{2})$/.exec(seccion.id);
  if (porId) return { anio: Number(porId[1]), mes: Number(porId[2]) - 1 };
  const texto = (seccion.getAttribute('aria-label') || seccion.querySelector('.camp-month__title')?.textContent || '').toLowerCase();
  const m = /([a-záéíóúñ]+)\s+de\s+(\d{4})/.exec(texto);
  const mes = m ? MESES.indexOf(m[1]) : -1;
  return mes >= 0 ? { anio: Number(m[2]), mes } : null;
}

/** Categoría de la ficha (por la clase de su etiqueta) → clave de color. */
function categoria(ficha) {
  // Pádel en silla: en el calendario FEP lleva clase propia; en campeonatos es una etiqueta de texto
  const etiquetas = [...ficha.querySelectorAll('.camp-item__tags .org-tag')].map((t) => t.textContent.trim().toLowerCase());
  if (ficha.querySelector('.calf-tag--silla') || etiquetas.includes('silla de ruedas')) return 'silla';
  if (ficha.querySelector('.camp-tag--absoluto')) return 'absoluto';
  if (ficha.querySelector('.camp-tag--menores')) return 'menores';
  if (ficha.querySelector('.camp-tag--veteranos')) return 'veteranos';
  // Calendarios autonómicos de menores y veteranos: campeonato autonómico o prueba puntuable
  if (ficha.querySelector('.cal-tag--campeonato')) return 'campeonato';
  if (ficha.querySelector('.cal-tag--ranking')) return 'ranking';
  return 'otra';
}

/**
 * Tipo de torneo (campeonatos): silla de ruedas, selecciones autonómicas, equipos o parejas.
 * Sale de las etiquetas de texto de la ficha; '' si no hay (calendarios).
 */
function tipo(ficha) {
  const etiquetas = [...ficha.querySelectorAll('.camp-item__tags .org-tag')].map((t) => t.textContent.trim().toLowerCase());
  if (ficha.querySelector('.calf-tag--silla') || etiquetas.includes('silla de ruedas')) return 'silla';
  if (etiquetas.includes('selecciones autonómicas')) return 'selecciones';
  if (etiquetas.includes('equipos')) return 'equipos';
  if (etiquetas.includes('parejas')) return 'parejas';
  return '';
}

/** Lee una ficha pintada por calendario-fep.js. Devuelve null si no tiene fecha real. */
function leerFicha(ficha) {
  const time = ficha.querySelector('.camp-item__date time[datetime]');
  if (!time) return null;
  const [a, m, d] = time.getAttribute('datetime').split('-').map(Number);
  if (!a || !m || !d) return null;
  const inicio = new Date(a, m - 1, d);
  // "06–08" → termina el 8; "27–01" → termina el 1 del mes siguiente
  const partes = time.textContent.split(/[–-]/).map((t) => parseInt(t, 10));
  let fin = new Date(inicio);
  if (partes.length === 2 && Number.isFinite(partes[1])) {
    fin = new Date(a, m - 1, partes[1]);
    if (fin < inicio) fin = new Date(a, m, partes[1]);
  }
  const enlace = ficha.querySelector('.camp-item__title a');
  // El tipo también se deja en la ficha de la lista (filtro rápido de campeonatos, también en móvil)
  const t = tipo(ficha);
  if (t) ficha.dataset.tipo = t;
  return {
    tipo: t,
    inicio, fin,
    titulo: ficha.querySelector('.camp-item__title')?.textContent.replace('↗', '').trim() ?? '',
    sede: ficha.querySelector('.camp-item__place')?.textContent.replace(/^Sede:\s*/, '').trim() ?? '',
    etiqueta: ficha.querySelector('.camp-item__tags')?.textContent.trim() ?? '',
    cat: categoria(ficha),
    href: enlace?.getAttribute('href') ?? null,
    target: enlace?.getAttribute('target') ?? null,
  };
}

const diaSemana = (f) => (f.getDay() + 6) % 7;          // lunes = 0
const mismoDia = (x, y) => x.toDateString() === y.toDateString();

/** Cuadrícula de un mes: semanas de lunes a domingo; cada prueba, un tramo por semana. */
function cuadricula(anio, mes, pruebas) {
  const primero = new Date(anio, mes, 1);
  const ultimo = new Date(anio, mes + 1, 0);
  const hoy = new Date();
  const semanas = [];
  for (let ini = new Date(anio, mes, 1 - diaSemana(primero)); ini <= ultimo; ini = new Date(ini.getFullYear(), ini.getMonth(), ini.getDate() + 7)) {
    semanas.push(ini);
  }

  const filas = semanas.map((lunes) => {
    const domingo = new Date(lunes.getFullYear(), lunes.getMonth(), lunes.getDate() + 6);
    // Cabecera de días de la semana
    const dias = Array.from({ length: 7 }, (_, i) => {
      const f = new Date(lunes.getFullYear(), lunes.getMonth(), lunes.getDate() + i);
      const fuera = f.getMonth() !== mes;
      return el('span', {
        class: `calm-dia${fuera ? ' is-fuera' : ''}${i >= 5 ? ' is-finde' : ''}${mismoDia(f, hoy) ? ' is-hoy' : ''}`,
        style: `grid-column: ${i + 1}; grid-row: 1`,   // fila de números; las barras se colocan debajo
        'aria-hidden': 'true',
      }, el('span', { class: 'calm-dia__n', text: fuera ? '' : String(f.getDate()) }));
    });
    // Tramos de las pruebas que caen en esta semana (recortados al mes)
    const tramos = [];
    pruebas.forEach((p) => {
      const desde = new Date(Math.max(p.inicio, lunes, primero));
      const hasta = new Date(Math.min(p.fin, domingo, ultimo));
      if (desde > hasta) return;
      const col = diaSemana(desde) + 1;
      const span = Math.round((hasta - desde) / 864e5) + 1;
      // Primer tramo de la prueba dentro de este mes (las que vienen del mes anterior empiezan el día 1)
      const esPrimero = mismoDia(desde, p.inicio < primero ? primero : p.inicio);
      const sigue = p.fin > hasta;
      const viene = p.inicio < desde;
      const rango = mismoDia(p.inicio, p.fin) ? fmtDia.format(p.inicio) : `del ${fmtDia.format(p.inicio)} al ${fmtDia.format(p.fin)}`;
      const etiqueta = `${p.titulo}, ${rango}${p.sede ? `, ${p.sede}` : ''}${p.etiqueta ? ` (${p.etiqueta})` : ''}`;
      const visible = esPrimero || mismoDia(desde, lunes);   // el texto se repite al empezar cada semana
      const props = {
        class: `calm-barra calm-barra--${p.cat}${p.tipo ? ` calm-tipo--${p.tipo}` : ''}${viene ? ' is-viene' : ''}${sigue ? ' is-sigue' : ''}`,
        'data-tipo': p.tipo || null,
        style: `grid-column: ${col} / span ${span}`,
        title: etiqueta,
      };
      const contenido = [
        el('span', { class: 'calm-barra__txt', text: visible ? p.titulo : '' }),
      ];
      // Solo el primer tramo de cada prueba es enlace accesible; el resto, decorativo.
      const accesible = esPrimero;
      tramos.push(p.href
        ? el('a', { ...props, href: p.href, target: p.target, rel: p.target ? 'noopener' : null,
          'aria-label': accesible ? `${etiqueta}${p.target ? ' (se abre en una pestaña nueva)' : ''}` : null,
          'aria-hidden': accesible ? null : 'true', tabindex: accesible ? null : '-1' }, ...contenido)
        : el('span', { ...props, role: accesible ? 'note' : null, 'aria-label': accesible ? etiqueta : null, 'aria-hidden': accesible ? null : 'true' }, ...contenido));
    });
    return el('div', { class: 'calm-semana', role: 'group', 'aria-label': `Semana del ${fmtDia.format(lunes)}` }, ...dias, ...tramos);
  });

  return el('div', { class: 'calm' },
    el('div', { class: 'calm-cab', 'aria-hidden': 'true' },
      ...DIAS_SEMANA.map((d, i) => el('span', { class: i >= 5 ? 'is-finde' : null, title: DIAS_SEMANA_LARGO[i], text: d }))),
    ...filas);
}

function pintar() {
  document.querySelectorAll(LISTAS).forEach(pintarLista);
}

function pintarLista(lista) {
  // Todas las pruebas con fecha: una que empieza el 27 de febrero y acaba el 1 de marzo
  // (agrupada en febrero por calendario-fep.js) también debe verse el 1 en la cuadrícula de marzo.
  const todas = [...lista.querySelectorAll('section.camp-month .camp-item')].map(leerFicha).filter(Boolean);
  for (const seccion of lista.querySelectorAll('section.camp-month')) {
    seccion.querySelector(':scope > .calm')?.remove();
    const fecha = mesDeSeccion(seccion);
    if (!fecha) continue;                                     // "Fecha por confirmar": se queda como lista
    const { anio, mes } = fecha;
    const desde = new Date(anio, mes, 1);
    const hasta = new Date(anio, mes + 1, 0);
    const pruebas = todas.filter((p) => p.inicio <= hasta && p.fin >= desde);
    if (!pruebas.length) continue;
    seccion.classList.add('has-calm');
    seccion.querySelector('.camp-month__title').after(cuadricula(anio, mes, pruebas));
  }
}

function init() {
  // Los scripts de cada calendario sustituyen el contenido de la lista al filtrar, cambiar de temporada,
  // de federación o de vista: se vuelve a dibujar la cuadrícula cada vez.
  document.querySelectorAll(LISTAS).forEach((lista) => {
    pintarLista(lista);
    new MutationObserver((cambios) => {
      if (cambios.some((c) => [...c.addedNodes].some((n) => n.nodeType === 1 && n.matches('section.camp-month')))) pintarLista(lista);
    }).observe(lista, { childList: true });
  });
  // Autonómicos: "Por federación" se guarda en la URL para que al recargar no vuelva a "Por fecha"
  document.getElementById('vista-federacion')?.addEventListener('click', () => setTimeout(() => {
    const url = new URL(location.href);
    url.searchParams.set('vista', 'federacion');
    history.replaceState(null, '', url);
  }));
}

/*
 * Calendarios autonómicos (menores / veteranos FFAA): la vista por defecto pasa a ser "Por fecha" (la que
 * tiene meses y, por tanto, cuadrícula). Se hace aquí, antes de que calendario-autonomico.js lea ?vista=
 * (este script se carga antes que él), sin cambiar su lógica.
 */
if (document.getElementById('cal-lista')) {
  const params = new URLSearchParams(location.search);
  if (!params.has('vista')) {
    params.set('vista', 'fecha');
    history.replaceState(null, '', `${location.pathname}?${params}${location.hash}`);
  }
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
})();
