/**
 * Portada ordenada por prioridad (home-prioridad.html).
 * Requiere assets/js/comun.js (cabecera, menú, pie, patrocinadores y utilidades en window.FEP).
 * Todo texto de la API se pinta con textContent / atributos, nunca con innerHTML.
 *
 *   1. Actualidad destacada (3 primeras) + 4. Más actualidad → GET /api/v1/noticias/destacadas
 *   3. Eventos destacados e inscripciones abiertas            → GET /api/v1/eventos/destacados
 *                                                              + /api/v1/formacion/convocatorias
 *   5. Convocatorias de formación                             → GET /api/v1/formacion/convocatorias
 *   8. Últimas fotos                                          → GET /api/v1/multimedia/fotos/ultimas
 */
(function () {
'use strict';

const { $, el, attrsEnlace, img, textoPlano, aFecha, bloqueFecha, fmtLargo, cargarLista } = window.FEP;
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

/* 1. Actualidad destacada: 1 grande + 2 laterales */
function tarjetaPortada(n, clase, etiqueta) {
  const attrs = attrsEnlace(n.enlace, n.ventana);
  return el('article', { class: clase },
    img(n.imagenUrl, '', { class: 'hp-card__img', loading: clase === 'hp-lead' ? 'eager' : 'lazy' }),
    el('div', { class: 'hp-card__body' },
      etiqueta ? el('p', { class: 'tag-red tag-red--new', text: etiqueta }) : null,
      el(clase === 'hp-lead' ? 'h2' : 'h3', { class: 'hp-card__title' }, el('a', attrs, n.titulo))));
}

function pintarPortada(noticias) {
  const cont = $('#hp-top');
  cont.setAttribute('aria-busy', 'false');
  if (!noticias.length) { cont.closest('section').hidden = true; return; }
  const [lead, ...resto] = noticias;
  cont.replaceChildren(
    tarjetaPortada(lead, 'hp-lead', 'Última hora'),
    ...resto.slice(0, 2).map((n) => tarjetaPortada(n, 'hp-side')));

  // 4. Más actualidad: el resto
  const mas = resto.slice(2);
  if (mas.length) {
    $('#hp-mas').replaceChildren(...mas.map((n) => {
      const attrs = attrsEnlace(n.enlace, n.ventana);
      return el('article', { class: 'hp-more__item' },
        img(n.imagenUrl, '', { class: 'hp-more__img', width: 400, height: 225 }),
        el('h3', { class: 'hp-more__title' }, el('a', attrs, n.titulo)));
    }));
    $('#hp-mas-bloque').hidden = false;
  }
}

/* 3. Eventos destacados e inscripciones abiertas */
function pintarEventos(eventos, convocatorias) {
  const cont = $('#hp-eventos');
  cont.setAttribute('aria-busy', 'false');
  const items = [
    ...eventos.slice().sort((a, b) => (a.orden ?? 0) - (b.orden ?? 0)).map((e) => {
      const f = bloqueFecha(e.fechaInicio, e.fechaFin);
      return {
        titulo: e.titulo,
        detalle: f ? [`${f.dias} ${f.mes}`, e.localidad, e.lugar].filter(Boolean).join(' · ') : textoPlano(e.texto),
        imagen: e.imagenUrl, enlace: attrsEnlace(e.enlace, e.ventana), etiqueta: 'Competición',
      };
    }),
    ...convocatorias.map((c) => {
      const limite = aFecha(c.fechaLimiteInscripcion);
      return {
        titulo: c.titulo,
        detalle: [c.localidad, limite ? `Inscripción hasta el ${fmtLargo.format(limite)}` : null].filter(Boolean).join(' · '),
        imagen: IMG_FORMACION, enlace: { href: INSCRIPCIONES }, etiqueta: 'Inscripción abierta',
      };
    }),
  ];
  $('#hp-eventos-vacio').hidden = items.length > 0;
  cont.replaceChildren(...items.map((it) => el('li', { class: 'hp-event' },
    el('a', { ...it.enlace, class: 'hp-event__link' },
      img(it.imagen, '', { class: 'hp-event__img', width: 300, height: 300 }),
      el('span', { class: 'hp-event__body' },
        el('span', { class: 'event__tag', text: it.etiqueta }),
        el('strong', { class: 'hp-event__title', text: it.titulo }),
        it.detalle ? el('span', { class: 'hp-event__text', text: it.detalle }) : null)))));
}

/* 5. Convocatorias de formación */
function pintarConvocatorias(convocatorias) {
  const ul = $('#hp-convocatorias');
  if (!convocatorias.length) {
    ul.replaceChildren(el('li', { class: 'hp-form__none', text: 'No hay convocatorias abiertas en este momento.' }));
    return;
  }
  ul.replaceChildren(...convocatorias.map((c) => {
    const f = bloqueFecha(c.fechaInicio, c.fechaFin);
    const limite = aFecha(c.fechaLimiteInscripcion);
    return el('li', {},
      f ? el('time', { class: 'hp-form__date', datetime: f.iso }, el('strong', { text: f.dias }), el('span', { text: f.mes })) : null,
      el('span', {},
        el('a', { href: INSCRIPCIONES, text: c.titulo }),
        el('span', { class: 'hp-form__meta', text: [c.localidad, limite ? `Inscripción hasta el ${fmtLargo.format(limite)}` : null].filter(Boolean).join(' · ') })));
  }));
}

/* 8. Últimas fotos */
function pintarFotos(fotos) {
  if (!fotos.length) return;
  $('#hp-fotos').replaceChildren(...fotos.slice(0, 6).map((f) => el('li', {},
    el('a', { href: `https://www.padelfederacion.es/Documentos_Multimedia_Previo.asp?id=${encodeURIComponent(f.idGaleria)}` },
      img(f.imagenUrl, f.subtitulo || 'Foto de la galería FEP', { width: 400, height: 267 })))));
  $('#hp-fotos-bloque').hidden = false;
}

function init() {
  cargarLista('/api/v1/noticias/destacadas').then((n) => pintarPortada((n ?? []).slice().sort(porRecientes)));
  Promise.all([cargarLista('/api/v1/eventos/destacados'), cargarLista('/api/v1/formacion/convocatorias')])
    .then(([ev, conv]) => {
      pintarEventos(ev ?? [], conv ?? []);
      pintarConvocatorias(conv ?? []);
    });
  // Endpoint real: /api/v1/multimedia/fotos/ultimas. Su mock se llama fotos-ultimas.json, así que si
  // la API y el mock "multimedia-fotos-ultimas" fallan se pide la ruta que resuelve a ese mock.
  cargarLista('/api/v1/multimedia/fotos/ultimas?limite=6')
    .then((f) => f ?? cargarLista('/api/v1/fotos/ultimas'))
    .then((f) => f && pintarFotos(f));
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
})();
