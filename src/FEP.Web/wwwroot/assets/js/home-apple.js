/**
 * Portada "escaparate" (home-apple.html).
 * Requiere assets/js/comun.js (cabecera, menú, pie, patrocinadores y utilidades en window.FEP).
 * Todo texto de la API se pinta con textContent / atributos, nunca con innerHTML.
 *
 *   Héroe claro, héroe oscuro, 2 mosaicos y carrusel "Más actualidad" → GET /api/v1/noticias/destacadas
 *   Carrusel de competición                                          → GET /api/v1/eventos/destacados
 *                                                                      + /api/v1/formacion/convocatorias
 *   Franja de aviso y mosaico de formación                            → GET /api/v1/formacion/convocatorias
 */
(function () {
'use strict';

const { $, $$, el, attrsEnlace, img, textoPlano, aFecha, bloqueFecha, fmtLargo, cargarLista, reduceMotion } = window.FEP;
const INSCRIPCIONES = 'https://www.padelfederacion.es/Formacion_Inscripciones';
const IMG_FORMACION = 'assets/img/fep/destacados/formacionfep.jpg';

/** Más reciente primero (IdNoticia más alto); sin id se respeta el orden de portada. */
function idNoticia(n) {
  if (Number.isFinite(n.idNoticia)) return n.idNoticia;
  const m = /[?&]idnoticia=(\d+)/i.exec(n.enlace ?? '');
  return m ? Number(m[1]) : null;
}
const porRecientes = (a, b) => {
  const ia = idNoticia(a); const ib = idNoticia(b);
  if (ia != null && ib != null && ia !== ib) return ib - ia;
  return (a.orden ?? 0) - (b.orden ?? 0);
};

/* ---------- Héroes ---------- */
function pintarHeroe(seccion, n, oscuro) {
  const attrs = attrsEnlace(n.enlace, n.ventana);
  seccion.setAttribute('aria-busy', 'false');
  $('.ap-hero__title', seccion).replaceChildren(el('a', attrs, n.titulo));
  $('.ap-ctas', seccion).replaceChildren(
    el('a', { ...attrs, class: 'ap-btn', text: 'Leer más' }),
    el('a', { class: `ap-btn ap-btn--ghost${oscuro ? ' ap-btn--on-dark' : ''}`, href: 'https://www.padelfederacion.es/Noticias.asp', text: 'Todas las noticias' }));
  const media = $('.ap-hero__media', seccion);
  media.classList.remove('ap-sk');
  media.replaceChildren(img(n.imagenUrl, '', { width: 1600, height: 900, loading: oscuro ? 'lazy' : 'eager', fetchpriority: oscuro ? 'auto' : 'high' }));
  seccion.hidden = false;
}

/* ---------- Mosaico de noticia ---------- */
function mosaicoNoticia(n, oscuro) {
  const attrs = attrsEnlace(n.enlace, n.ventana);
  return el('section', { class: `ap-tile ap-tile--news ${oscuro ? 'ap-tile--dark' : 'ap-tile--light'}` },
    el('div', { class: 'ap-tile__copy' },
      el('p', { class: 'ap-eyebrow', text: 'Actualidad' }),
      el('h2', { class: 'ap-tile__title' }, el('a', attrs, n.titulo)),
      el('p', { class: 'ap-ctas' },
        el('a', { ...attrs, class: 'ap-btn', text: 'Leer más' }))),
    img(n.imagenUrl, '', { class: 'ap-tile__img', width: 800, height: 500, loading: 'lazy' }));
}

/* ---------- Tarjeta de carrusel ---------- */
function tarjeta({ enlace, imagen, etiqueta, titulo, detalle }) {
  return el('li', { class: 'ap-card' },
    el('a', { ...enlace, class: 'ap-card__link' },
      img(imagen, '', { class: 'ap-card__img', width: 744, height: 556, loading: 'lazy' }),
      el('span', { class: 'ap-card__body' },
        etiqueta ? el('span', { class: 'ap-card__tag', text: etiqueta }) : null,
        el('strong', { class: 'ap-card__title', text: titulo }),
        detalle ? el('span', { class: 'ap-card__text', text: detalle }) : null)));
}

function pintarNoticias(noticias) {
  const h1 = $('#ap-hero-1');
  if (!noticias.length) { h1.hidden = true; return; }
  const [a, b, c, d, ...resto] = noticias;
  pintarHeroe(h1, a, false);
  if (b) pintarHeroe($('#ap-hero-2'), b, true);

  const ancla = $('#ap-mosaico-ranking');
  [c, d].filter(Boolean).forEach((n, i) => ancla.before(mosaicoNoticia(n, i === 1)));

  if (resto.length) {
    $('#ap-mas').replaceChildren(...resto.map((n) => tarjeta({
      enlace: attrsEnlace(n.enlace, n.ventana), imagen: n.imagenUrl, etiqueta: 'Noticia', titulo: n.titulo,
    })));
    $('#ap-mas-bloque').hidden = false;
  }
}

/* ---------- Competición + formación ---------- */
function pintarCompeticion(eventos, convocatorias) {
  const track = $('#ap-eventos');
  track.setAttribute('aria-busy', 'false');
  const items = [
    ...eventos.slice().sort((x, y) => (x.orden ?? 0) - (y.orden ?? 0)).map((e) => {
      const f = bloqueFecha(e.fechaInicio, e.fechaFin);
      return {
        enlace: attrsEnlace(e.enlace, e.ventana), imagen: e.imagenUrl, etiqueta: 'Competición', titulo: e.titulo,
        detalle: f ? [`${f.dias} ${f.mes}`, e.localidad, e.lugar].filter(Boolean).join(' · ') : textoPlano(e.texto),
      };
    }),
    ...convocatorias.map((c) => {
      const limite = aFecha(c.fechaLimiteInscripcion);
      return {
        enlace: { href: INSCRIPCIONES }, imagen: IMG_FORMACION, etiqueta: 'Inscripción abierta', titulo: c.titulo,
        detalle: [c.localidad, limite ? `Hasta el ${fmtLargo.format(limite)}` : null].filter(Boolean).join(' · '),
      };
    }),
  ];
  if (!items.length) { track.closest('section').hidden = true; return; }
  track.replaceChildren(...items.map(tarjeta));
  actualizarFlechas(track);
}

function pintarFormacion(convocatorias) {
  if (!convocatorias.length) return;
  const [c] = convocatorias;
  $('#ap-formacion-sub').textContent = `Próxima convocatoria: ${c.titulo}.`;
  const aviso = $('#ap-aviso a');
  aviso.replaceChildren(`Inscripciones abiertas: ${c.titulo}. `, el('span', { class: 'ap-more', text: 'Inscríbete' }));
  $('#ap-aviso').hidden = false;
}

/* ---------- Flechas de los carruseles ---------- */
function actualizarFlechas(track) {
  const max = track.scrollWidth - track.clientWidth - 2;
  $$(`.ap-arrow[aria-controls="${track.id}"]`).forEach((b) => {
    b.disabled = Number(b.dataset.dir) < 0 ? track.scrollLeft <= 2 : track.scrollLeft >= max;
  });
}
function initFlechas() {
  $$('.ap-arrow').forEach((b) => {
    const track = document.getElementById(b.getAttribute('aria-controls'));
    if (!track) return;
    b.addEventListener('click', () => track.scrollBy({
      left: Number(b.dataset.dir) * track.clientWidth * 0.8, behavior: reduceMotion.matches ? 'auto' : 'smooth',
    }));
    if (!track.dataset.flechas) {
      track.dataset.flechas = '1';
      track.addEventListener('scroll', () => actualizarFlechas(track), { passive: true });
      window.addEventListener('resize', () => actualizarFlechas(track), { passive: true });
      actualizarFlechas(track);
    }
  });
}

function init() {
  initFlechas();
  cargarLista('/api/v1/noticias/destacadas').then((n) => pintarNoticias((n ?? []).slice().sort(porRecientes)));
  Promise.all([cargarLista('/api/v1/eventos/destacados'), cargarLista('/api/v1/formacion/convocatorias')])
    .then(([ev, conv]) => {
      pintarCompeticion(ev ?? [], conv ?? []);
      pintarFormacion(conv ?? []);
    });
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
})();
