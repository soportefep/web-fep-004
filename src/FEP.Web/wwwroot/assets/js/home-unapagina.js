/* =========================================================================
   FEP · Portada de una sola página (antes prototipo web-fep-003)
   - Enlaces data-destino → páginas de detalle (base configurable en
     <meta name="fep-destino-base">; vacía = misma web).
   - Menú por secciones con resaltado de la sección visible (scrollspy).
   - Filtros por categoría en Competición.
   - Buscador guiado: compone el enlace al calendario/ranking concreto.
   ========================================================================= */
(() => {
  'use strict';

  const BASE = document.querySelector('meta[name="fep-destino-base"]')?.content || '';
  const destino = (ruta) => BASE + ruta;

  /* Federaciones autonómicas: [id BD FEP, nombre corto, web propia] (de web-fep-002/mock) */
  const FEDERACIONES = [
    [6, 'Andalucía', 'https://www.fap.es'], [11, 'Aragón', 'https://www.arapadel.com'],
    [7, 'Asturias', 'https://www.fppastur.com'], [17, 'Illes Balears', 'https://www.fpadelib.es/'],
    [18, 'Canarias', 'https://canariaspadel.es/'], [13, 'Cantabria', 'https://www.federacioncantabradepadel.com'],
    [22, 'Castilla-La Mancha', 'https://fpclm.com'], [21, 'Castilla y León', 'https://www.padelcyl.es'],
    [2, 'Cataluña', 'https://www.fcpadel.cat'], [16, 'Ceuta', 'https://www.fpadelceuta.com'],
    [14, 'C. Valenciana', 'https://padelcv.com'], [12, 'Extremadura', 'https://www.fexpadel.com'],
    [8, 'Galicia', 'https://www.fgpadel.com'], [19, 'La Rioja', null], [1, 'Madrid', 'https://www.fmpadel.com'],
    [9, 'Melilla', 'https://www.padelmelilla.com'], [15, 'Murcia', 'https://www.federacionpadelmurcia.com'],
    [10, 'Navarra', 'https://www.federacionnavarradepadel.com'], [3, 'País Vasco', 'https://www.fvpadel.com'],
  ];
  const banderaFed = (id) => `assets/img/fep/federaciones/bandera-${id}.jpg`;

  /* Idiomas: castellano y lenguas cooficiales + principales internacionales.
     Banderas SVG reutilizadas de web-fep-002/assets/js/i18n.js. */
  const SENYERA = '<rect width="30" height="20" fill="#FCDD09"/><g fill="#DA121A"><rect y="2.22" width="30" height="2.22"/><rect y="6.67" width="30" height="2.22"/><rect y="11.11" width="30" height="2.22"/><rect y="15.56" width="30" height="2.22"/></g>';
  const IDIOMAS = {
    es: [
      ['es', 'Castellano', '<rect width="30" height="20" fill="#AA151B"/><rect y="5" width="30" height="10" fill="#F1BF00"/>'],
      ['ca', 'Català', SENYERA],
      ['va', 'Valencià', `${SENYERA}<rect width="7" height="20" fill="#0047BA"/>`],
      ['eu', 'Euskara', '<rect width="30" height="20" fill="#D52B1E"/><path d="M0 0L30 20M30 0L0 20" stroke="#009B48" stroke-width="3"/><path d="M15 0V20M0 10H30" stroke="#fff" stroke-width="2.6"/>'],
      ['gl', 'Galego', '<rect width="30" height="20" fill="#fff"/><path d="M0 0L30 20" stroke="#0099CC" stroke-width="4.5"/>'],
    ],
    int: [
      ['en', 'English', '<rect width="30" height="20" fill="#012169"/><path d="M0 0L30 20M30 0L0 20" stroke="#fff" stroke-width="4"/><path d="M0 0L30 20M30 0L0 20" stroke="#C8102E" stroke-width="1.4"/><path d="M15 0V20M0 10H30" stroke="#fff" stroke-width="6"/><path d="M15 0V20M0 10H30" stroke="#C8102E" stroke-width="3.6"/>'],
      ['fr', 'Français', '<rect width="10" height="20" fill="#0055A4"/><rect x="10" width="10" height="20" fill="#fff"/><rect x="20" width="10" height="20" fill="#EF4135"/>'],
      ['pt', 'Português', '<rect width="12" height="20" fill="#046A38"/><rect x="12" width="18" height="20" fill="#DA291C"/><circle cx="12" cy="10" r="4.5" fill="#FFE900" stroke="#000" stroke-width=".3"/>'],
      ['it', 'Italiano', '<rect width="10" height="20" fill="#009246"/><rect x="10" width="10" height="20" fill="#fff"/><rect x="20" width="10" height="20" fill="#CE2B37"/>'],
      ['de', 'Deutsch', '<rect width="30" height="6.67" fill="#000"/><rect y="6.67" width="30" height="6.67" fill="#DD0000"/><rect y="13.33" width="30" height="6.67" fill="#FFCE00"/>'],
      ['zh', '中文', '<rect width="30" height="20" fill="#EE1C25"/><polygon fill="#FFFF00" points="5.5,2.5 6.18,4.57 8.35,4.57 6.59,5.86 7.26,7.93 5.5,6.65 3.74,7.93 4.41,5.86 2.65,4.57 4.82,4.57"/>'],
      ['ja', '日本語', '<rect width="30" height="20" fill="#fff"/><circle cx="15" cy="10" r="6" fill="#BC002D"/>'],
    ],
  };

  /* ---------- Enlaces a páginas de detalle ---------- */
  document.querySelectorAll('[data-destino]').forEach((a) => { a.href = destino(a.dataset.destino); });

  /** Crea un elemento con atributos y contenido. */
  function el(tag, attrs = {}, ...hijos) {
    const n = document.createElement(tag);
    for (const [k, v] of Object.entries(attrs)) {
      if (v == null || v === false) continue;
      if (k === 'text') n.textContent = v; else n.setAttribute(k, v === true ? '' : v);
    }
    n.append(...hijos.filter((h) => h != null));
    return n;
  }
  const imgBandera = (id) => el('img', { class: 'bandera', src: banderaFed(id), alt: '', width: 21, height: 15, loading: 'lazy' });

  /* ---------- Federaciones territoriales (sección Federación) ---------- */
  const listaFeds = document.getElementById('feds');
  if (listaFeds) {
    listaFeds.append(...FEDERACIONES.map(([id, nombre]) => {
      const img = el('img', { src: banderaFed(id), alt: '', loading: 'lazy', width: 32, height: 22 });
      return el('li', {}, el('a', { href: destino(`federacion.html?id=${id}`) }, img, nombre));
    }));
  }

  /* ---------- Barra superior: desplegable de federaciones autonómicas ---------- */
  const fedsBoton = document.getElementById('feds-boton');
  const fedsPanel = document.getElementById('feds-panel');
  document.getElementById('feds-panel-lista').append(...FEDERACIONES.map(([id, nombre, web]) => {
    // Sin web propia conocida → su ficha dentro de la web FEP
    const externo = Boolean(web);
    return el('li', {}, el('a', {
      href: web || destino(`federacion.html?id=${id}`),
      target: externo ? '_blank' : null, rel: externo ? 'noopener' : null,
      title: externo ? `${nombre}: abre su web en otra pestaña` : `${nombre}: ficha en la web FEP`,
    }, imgBandera(id), nombre, el('small', { 'aria-hidden': 'true', text: externo ? '↗' : '' })));
  }));

  function abrirFeds(abrir) {
    fedsPanel.hidden = !abrir;
    fedsBoton.setAttribute('aria-expanded', String(abrir));
    if (abrir) fedsPanel.querySelector('a')?.focus();
  }
  fedsBoton.addEventListener('click', () => abrirFeds(fedsPanel.hidden));
  document.addEventListener('click', (e) => { if (!fedsPanel.hidden && !e.target.closest('#feds-menu')) abrirFeds(false); });
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && !fedsPanel.hidden) { abrirFeds(false); fedsBoton.focus(); }
  });

  /* ---------- Barra superior: idiomas ---------- */
  const CLAVE_IDIOMA = 'fep003.idioma';
  let idioma = 'es';
  try { idioma = localStorage.getItem(CLAVE_IDIOMA) || 'es'; } catch { /* almacenamiento no disponible */ }

  function pintarIdiomas() {
    for (const [grupo, lista] of Object.entries(IDIOMAS)) {
      document.getElementById(`idiomas-${grupo}`).replaceChildren(...lista.map(([cod, nombre, svg]) => {
        const b = el('button', {
          type: 'button', class: 'idioma', lang: cod === 'va' ? 'ca' : cod, title: nombre,
          'aria-label': nombre, 'aria-pressed': String(cod === idioma), 'data-idioma': cod,
        });
        b.innerHTML = `<span class="bandera"><svg viewBox="0 0 30 20" preserveAspectRatio="xMidYMid slice" aria-hidden="true" focusable="false">${svg}</svg></span>`;
        return el('li', {}, b);
      }));
    }
    document.documentElement.lang = idioma === 'va' ? 'ca' : idioma;
  }
  document.querySelector('.idiomas').addEventListener('click', (e) => {
    const b = e.target.closest('[data-idioma]');
    if (!b) return;
    idioma = b.dataset.idioma;
    try { localStorage.setItem(CLAVE_IDIOMA, idioma); } catch { /* sin almacenamiento */ }
    pintarIdiomas();
    document.querySelector(`[data-idioma="${idioma}"]`)?.focus();
  });
  pintarIdiomas();

  /* ---------- Datos de competición (mock, mismo formato que la API de web-fep-002) ---------- */
  const leer = (ruta) => fetch(ruta).then((r) => (r.ok ? r.json() : Promise.reject(new Error(r.status))));
  const DIA = 86400000;
  const hoy0 = () => { const d = new Date(); d.setHours(0, 0, 0, 0); return d; };
  const fecha = (iso) => { const [a, m, d] = iso.slice(0, 10).split('-').map(Number); return new Date(a, m - 1, d); };
  const fmtCorta = new Intl.DateTimeFormat('es-ES', { day: '2-digit', month: 'short' });

  /** Enlaces de la web antigua → página equivalente del prototipo 002 (si existe). */
  function enlaceTorneo(url) {
    if (!url) return null;
    const m = url.match(/padelfederacion\.es\/(Camp_Equipos|Camp_Parejas|Camp_Silla)\.asp(\?.*)?$/i);
    if (!m) return url;
    const pagina = { camp_equipos: 'campeonato-equipos.html', camp_parejas: 'campeonato-parejas.html', camp_silla: 'campeonato-silla.html' }[m[1].toLowerCase()];
    return destino(pagina + (m[2] || ''));
  }

  /* ---------- Teletipo de próximas citas ---------- */
  async function pintarTeletipo() {
    let eventos = [], cursos = [];
    try { [eventos, cursos] = await Promise.all([leer('mock/eventos-destacados.json'), leer('mock/formacion-convocatorias.json')]); }
    catch (e) { console.warn('[FEP] próximas citas:', e.message); return; }
    const citas = [
      ...eventos.sort((a, b) => a.orden - b.orden).map((e) => ({ tag: 'Competición', titulo: e.titulo, url: enlaceTorneo(e.enlace) })),
      ...cursos.filter((c) => fecha(c.fechaFin || c.fechaInicio) >= hoy0()).map((c) => ({
        tag: 'Formación', titulo: c.titulo, fecha: fmtCorta.format(fecha(c.fechaInicio)), url: 'https://www.padelfederacion.es/Formacion.asp',
      })),
    ];
    if (!citas.length) return;
    const item = (c, copia) => el('li', copia ? { 'aria-hidden': 'true' } : {},
      el('a', { href: c.url || '#competicion', tabindex: copia ? '-1' : null },
        el('span', { class: 'ticker__tag', text: c.tag }),
        c.fecha ? el('span', { class: 'ticker__fecha', text: c.fecha }) : null,
        c.titulo));
    // La lista se duplica para que el desplazamiento continuo no tenga saltos (la copia no es accesible)
    document.getElementById('ticker-lista').replaceChildren(...citas.map((c) => item(c, false)), ...citas.map((c) => item(c, true)));
    document.getElementById('ticker').hidden = false;
  }
  pintarTeletipo();

  /* ---------- Carrusel de destacados de la portada ---------- */
  const reduceMotion = matchMedia('(prefers-reduced-motion: reduce)');
  (function carrusel() {
    const slides = [...document.querySelectorAll('.carrusel .slide')];
    if (!slides.length) return;
    const n = document.getElementById('slide-n');
    const caption = document.getElementById('slide-caption');
    const pausa = document.getElementById('slide-pause');
    document.getElementById('slide-total').textContent = String(slides.length).padStart(2, '0');
    let actual = 0;
    let pausado = reduceMotion.matches;
    let temporizador = null;

    function mostrar(i) {
      actual = (i + slides.length) % slides.length;
      slides.forEach((s, k) => {
        s.classList.toggle('is-active', k === actual);
        s.setAttribute('aria-hidden', String(k !== actual));
      });
      const s = slides[actual];
      n.textContent = String(actual + 1).padStart(2, '0');
      caption.textContent = s.dataset.titulo;
      caption.href = s.dataset.enlace || '#actualidad';
      s.querySelector('img').loading = 'eager';
    }
    function programar() {
      clearInterval(temporizador);
      if (!pausado && slides.length > 1) temporizador = setInterval(() => mostrar(actual + 1), 6000);
    }
    function pausar(si) {
      pausado = si;
      pausa.setAttribute('aria-pressed', String(si));
      pausa.setAttribute('aria-label', si ? 'Reanudar carrusel' : 'Pausar carrusel');
      programar();
    }
    document.getElementById('slide-prev').addEventListener('click', () => { mostrar(actual - 1); programar(); });
    document.getElementById('slide-next').addEventListener('click', () => { mostrar(actual + 1); programar(); });
    pausa.addEventListener('click', () => pausar(!pausado));
    mostrar(0);
    pausar(pausado);
  })();

  /* ---------- Reels de Instagram (mock con el formato de la API de 002) ---------- */
  async function pintarReels() {
    let datos;
    try { datos = await leer('mock/redes-sociales-instagram-reels.json'); } catch (e) { console.warn('[FEP] reels:', e.message); return; }
    const reels = (datos.reels || []).slice(0, 12);
    if (!reels.length) return;
    const esInstagram = (u) => (/^https:\/\/(www\.)?instagram\.com\//.test(u || '') ? u : null);
    const perfil = esInstagram(datos.perfilUrl) || 'https://www.instagram.com/padelfederacion/';
    document.getElementById('reels-perfil').href = perfil;
    document.getElementById('reels-seguir').href = perfil;
    document.getElementById('reels-aviso').hidden = !datos.datosDePrueba;
    const limiteNuevo = Date.now() - 7 * DIA;
    const fmtReel = new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'short' });
    const track = document.getElementById('reels-lista');
    track.replaceChildren(...reels.map((r, i) => {
      const f = r.fecha ? new Date(r.fecha) : null;
      const nuevo = f && f.getTime() >= limiteNuevo;
      const desc = (r.descripcion || 'Reel de la Federación Española de Pádel').slice(0, 110);
      return el('li', { class: 'reel', 'aria-roledescription': 'reel', 'aria-label': `${i + 1} de ${reels.length}` },
        el('a', {
          class: 'reel__link', href: esInstagram(r.enlace) || perfil, target: '_blank', rel: 'noopener',
          'aria-label': `${nuevo ? 'Nuevo. ' : ''}Ver reel en Instagram${f ? ` del ${fmtReel.format(f)}` : ''}: ${desc} (se abre en una pestaña nueva)`,
        },
          el('img', { class: 'reel__img', src: r.miniaturaUrl, alt: '', width: 360, height: 640, loading: i < 6 ? 'eager' : 'lazy' }),
          el('span', { class: 'reel__top', 'aria-hidden': 'true' },
            nuevo ? el('span', { class: 'reel__nuevo', text: 'Nuevo' }) : el('span'),
            el('span', { class: 'reel__icono' })),
          el('span', { class: 'reel__play', 'aria-hidden': 'true' }),
          el('span', { class: 'reel__pie', 'aria-hidden': 'true' },
            f ? el('span', { class: 'reel__fecha', text: fmtReel.format(f).replace('.', '') }) : null,
            el('span', { class: 'reel__desc', text: desc }))));
    }));
    document.getElementById('reels').hidden = false;

    // Flechas (en la cabecera): avanzan una "pantalla" de tarjetas y se desactivan en los extremos
    const prev = document.getElementById('reels-prev');
    const next = document.getElementById('reels-next');
    const paso = () => {
      const t = track.firstElementChild;
      const gap = parseFloat(getComputedStyle(track).columnGap) || 0;
      const ancho = t ? t.getBoundingClientRect().width + gap : track.clientWidth;
      return Math.max(ancho, Math.floor(track.clientWidth / ancho) * ancho);
    };
    const mover = (dir) => track.scrollBy({ left: dir * paso(), behavior: reduceMotion.matches ? 'auto' : 'smooth' });
    const actualizar = () => {
      const inicio = track.scrollLeft <= 2;
      const fin = track.scrollLeft >= track.scrollWidth - track.clientWidth - 2;
      prev.disabled = inicio;
      next.disabled = fin;
      track.parentElement.classList.toggle('is-inicio', inicio);
      track.parentElement.classList.toggle('is-fin', fin);
    };
    prev.addEventListener('click', () => mover(-1));
    next.addEventListener('click', () => mover(1));
    track.addEventListener('scroll', actualizar, { passive: true });
    addEventListener('resize', actualizar);
    actualizar();
  }
  pintarReels();

  /* ---------- Patrocinadores (3 niveles, igual que web-fep-002/assets/js/comun.js) ----------
     Cada logo es un recorte (x, y, ancho, alto en % de la imagen) de su barra oficial. */
  const BARRAS_PATRO = {
    a: { src: 'assets/img/fep/publicidad/barrapatroa5.jpg', w: 1068, h: 266 },
    b: { src: 'assets/img/fep/publicidad/barrapatrob22.jpg', w: 1068, h: 266 },
    c: { src: 'assets/img/fep/publicidad/barrapatroc8.jpg', w: 1074, h: 98 },
  };
  const NIVELES_PATRO = [
    { clave: 'principales', titulo: 'Patrocinadores principales', barra: 'a', logos: [
      { nombre: 'HEAD, pelota oficial', href: 'https://www.head.com/es_ES/shop-padel', r: [5.6, 13.2, 23.4, 32] },
      { nombre: 'Bullpadel, Selección Española', href: 'https://www.bullpadel.com/es/', r: [63.7, 14.3, 32.3, 30.1] },
      { nombre: 'Babolat, Circuito Nacional de Menores', href: 'https://www.babolat.com/es', r: [62.3, 56.4, 33.7, 33.1] },
      { nombre: 'Padel Pro Shop', href: 'https://padelproshop.com/', r: [8, 55.6, 22, 34.6] },
      { nombre: 'Consejo Superior de Deportes', href: 'https://www.csd.gob.es/', r: [35.9, 35.7, 23.4, 33.1], institucional: true },
    ] },
    { clave: 'secundarios', titulo: 'Patrocinadores', barra: 'b', logos: [
      { nombre: 'CUPRA', href: 'https://www.cupra.com/es-es/', r: [4.7, 30, 11.2, 44] },
      { nombre: 'Iberdrola', href: 'https://www.iberdrola.com/compromiso-social/deporte-femenino', r: [51.8, 63.2, 23.9, 26.3] },
      { nombre: 'Rafa Nadal Academy', href: 'https://www.rafanadalacademy.com/', r: [19.9, 14.3, 13.6, 41.4] },
      { nombre: 'Padel Galis', href: 'https://padelgalis.com/', r: [48.9, 14.3, 27.9, 32] },
      { nombre: 'JOMA', href: 'https://www.joma-sport.com/', r: [78.5, 18, 19.9, 24.4] },
      { nombre: 'SIUX', href: 'https://www.siuxpadel.com/', r: [35.9, 19.5, 10.3, 36.8] },
      { nombre: 'Halcón Viajes', href: 'https://www.halconviajes.com/', r: [81.9, 53.4, 12.6, 37.6] },
      { nombre: 'TacticBo', href: 'https://tacticbo.com/', r: [19.2, 71.4, 12.6, 15] },
      { nombre: 'BVMPRS Brand', href: 'https://www.bumpersbrand.com/', r: [33.5, 71.4, 17.3, 15] },
    ] },
    { clave: 'homologados', titulo: 'Productos homologados', barra: 'c', logos: [
      { nombre: 'Realturf', href: 'https://realturf.com/es/', r: [12.4, 22, 14, 62] },
      { nombre: 'FieldTurf', href: null, r: [34.5, 22, 19.5, 62] },
      { nombre: 'NATURF', href: null, r: [70.3, 18, 17.4, 66] },
    ] },
  ];
  const SVG_NS = 'http://www.w3.org/2000/svg';
  let clipId = 0;
  function logoRecortado(barra, [x, y, w, h], nombre) {
    const caja = [x * barra.w / 100, y * barra.h / 100, w * barra.w / 100, h * barra.h / 100].map((v) => v.toFixed(1));
    const svg = document.createElementNS(SVG_NS, 'svg');
    svg.setAttribute('viewBox', caja.join(' '));
    svg.setAttribute('preserveAspectRatio', 'xMidYMid meet');
    svg.setAttribute('role', 'img');
    svg.setAttribute('aria-label', nombre);
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
  // Como en la home de 002: los principales (titulados "Patrocinadores") en su franja antes de Competición;
  // patrocinadores y productos homologados, en la franja de debajo del ranking.
  const bloquesPatro = NIVELES_PATRO.map((nivel) => {
    const barra = BARRAS_PATRO[nivel.barra];
    const idTitulo = `patro-${nivel.clave}-titulo`;
    const lista = el('ul', { class: 'patro-nivel__list' }, ...nivel.logos.map((p) => {
      const logo = logoRecortado(barra, p.r, p.nombre);
      return el('li', {}, p.href
        ? el('a', { class: 'patro-tile', href: p.href, target: '_blank', rel: p.institucional ? 'noopener' : 'noopener sponsored', title: p.nombre },
          logo, el('span', { class: 'sr-only', text: ' (se abre en una pestaña nueva)' }))
        : el('span', { class: 'patro-tile', title: p.nombre }, logo));
    }));
    const titulo = nivel.clave === 'principales' ? 'Patrocinadores' : nivel.titulo;
    return el('div', { class: `patro-nivel patro-nivel--${nivel.clave}`, role: 'group', 'aria-labelledby': idTitulo },
      el('h2', { class: 'patro-nivel__title', id: idTitulo, text: titulo }), lista);
  });
  document.getElementById('patrocinadores-principales').replaceChildren(bloquesPatro[0]);
  document.getElementById('patrocinadores-resto').replaceChildren(bloquesPatro[1], bloquesPatro[2]);

  /* ---------- Cuenta atrás del próximo torneo por categoría ---------- */
  const CATEGORIAS = [
    { clave: 'absoluto', nombre: 'Absoluto', calendario: 'calendariofep.html', es: (t) => /^(absoluta|absoluta ffaa|sub 23)$/i.test(t.categoria) },
    { clave: 'menores', nombre: 'Menores', calendario: 'calendariofep.html', es: (t) => /^menores/i.test(t.categoria) },
    { clave: 'veteranos', nombre: 'Veteranos', calendario: 'calendariofep.html', es: (t) => /^veteranos/i.test(t.categoria) },
    { clave: 'equipos', nombre: 'Equipos', calendario: 'campeonato-equipos.html', es: (t) => /equipos|ligas/i.test(t.titulo) },
    { clave: 'silla', nombre: 'Silla de ruedas', calendario: 'campeonato-silla.html', es: (t) => /silla/i.test(t.categoria) },
  ];
  const HORA_INICIO = 9; // hora orientativa de comienzo de la primera jornada

  async function pintarCuentas() {
    const caja = document.getElementById('cuentas');
    let items = [];
    try { items = (await leer('mock/competicion-calendario.json')).items || []; }
    catch (e) { console.warn('[FEP] calendario:', e.message); document.getElementById('proximos').hidden = true; return; }

    const hoy = hoy0();
    const vigentes = items
      .filter((t) => t.fechaInicio && !t.fechaPorConfirmar && fecha(t.fechaFin || t.fechaInicio) >= hoy)
      .sort((a, b) => a.fechaInicio.localeCompare(b.fechaInicio));

    const relojes = [];
    caja.replaceChildren(...CATEGORIAS.map((cat) => {
      const t = vigentes.find(cat.es);
      if (!t) {
        return el('a', { class: 'cuenta cuenta--vacia', 'data-cat': cat.clave, href: destino(cat.calendario) },
          el('span', { class: 'cuenta__cat', text: cat.nombre }),
          el('span', { class: 'cuenta__titulo', text: 'Sin fechas confirmadas' }),
          el('span', { class: 'cuenta__ir', text: 'Ver calendario ↗' }));
      }
      const inicio = fecha(t.fechaInicio); inicio.setHours(HORA_INICIO);
      const fin = fecha(t.fechaFin || t.fechaInicio); fin.setHours(23, 59, 59);
      const partes = ['días', 'horas', 'min', 'seg'].map((u) => el('span', {}, el('b', { text: '00' }), el('small', { text: u })));
      const reloj = el('span', { class: 'cuenta__reloj', role: 'timer', 'aria-label': `Cuenta atrás para ${t.titulo}` }, ...partes);
      const rango = t.fechaFin && t.fechaFin !== t.fechaInicio
        ? `${fmtCorta.format(fecha(t.fechaInicio))} – ${fmtCorta.format(fecha(t.fechaFin))}` : fmtCorta.format(fecha(t.fechaInicio));
      const tarjeta = el('a', { class: 'cuenta', 'data-cat': cat.clave, href: enlaceTorneo(t.enlace) || destino(cat.calendario) },
        el('span', { class: 'cuenta__cat', text: cat.nombre }),
        reloj,
        el('span', { class: 'cuenta__ya', text: 'En juego ahora' }),
        el('span', { class: 'cuenta__titulo', text: t.titulo }),
        el('span', { class: 'cuenta__meta', text: `${rango} · ${t.sede}` }),
        el('span', { class: 'cuenta__ir', text: 'Ver torneo ↗' }));
      relojes.push({ tarjeta, partes, inicio, fin });
      return tarjeta;
    }));

    const dos = (n) => String(n).padStart(2, '0');
    function tic() {
      const ahora = Date.now();
      for (const r of relojes) {
        const falta = r.inicio - ahora;
        r.tarjeta.classList.toggle('is-ya', falta <= 0 && ahora <= r.fin);
        const s = Math.max(0, Math.floor(falta / 1000));
        const vals = [Math.floor(s / 86400), Math.floor(s / 3600) % 24, Math.floor(s / 60) % 60, s % 60];
        r.partes.forEach((p, i) => { p.firstChild.textContent = dos(vals[i]); });
      }
    }
    tic();
    setInterval(tic, 1000);
    // Recolocar el filtro activo sobre las tarjetas recién creadas
    filtrar(document.querySelector('#competicion .filtros .is-on')?.dataset.filtro || 'todo');
  }

  /* ---------- Menú por secciones + scrollspy ---------- */
  const navLinks = [...document.querySelectorAll('.nav__link')];
  const navList = document.querySelector('.nav__list');
  const subLinks = [...document.querySelectorAll('.subnav a')];

  function marcar(links, id) {
    links.forEach((l) => {
      const on = l.getAttribute('href') === `#${id}`;
      l.classList.toggle('is-current', on);
      if (on) l.setAttribute('aria-current', 'true'); else l.removeAttribute('aria-current');
    });
  }

  function centrarEnMenu(link) {
    // En móvil el menú se desliza en horizontal: mantener visible la sección activa
    if (!navList || navList.scrollWidth <= navList.clientWidth) return;
    navList.scrollTo({ left: link.offsetLeft - 16, behavior: 'smooth' });
  }

  function observar(ids, links, alCambiar) {
    const secs = ids.map((id) => document.getElementById(id)).filter(Boolean);
    const visibles = new Map();
    const io = new IntersectionObserver((entradas) => {
      entradas.forEach((e) => visibles.set(e.target.id, e.isIntersecting));
      const actual = secs.find((s) => visibles.get(s.id));
      marcar(links, actual ? actual.id : '');
      if (actual && alCambiar) alCambiar(actual.id);
    }, { rootMargin: '-35% 0px -60% 0px' });
    secs.forEach((s) => io.observe(s));
  }

  observar(navLinks.map((l) => l.hash.slice(1)), navLinks, (id) => {
    const l = navLinks.find((x) => x.hash === `#${id}`);
    if (l) centrarEnMenu(l);
  });
  observar(subLinks.map((l) => l.hash.slice(1)), subLinks);

  // Al pulsar en el menú, la sección destino se resalta un momento
  navLinks.forEach((l) => l.addEventListener('click', () => {
    const sec = document.getElementById(l.hash.slice(1));
    if (!sec) return;
    sec.classList.remove('is-flash');
    void sec.offsetWidth;
    sec.classList.add('is-flash');
    setTimeout(() => sec.classList.remove('is-flash'), 1200);
  }));

  /* ---------- Filtros de Competición ---------- */
  const comp = document.getElementById('competicion');
  const chips = [...comp.querySelectorAll('.filtros .chip')];
  const vacio = document.getElementById('vacio');

  function filtrar(cat) {
    chips.forEach((c) => {
      const on = c.dataset.filtro === cat;
      c.classList.toggle('is-on', on);
      c.setAttribute('aria-pressed', String(on));
    });
    // Se consulta cada vez: las cuentas atrás se añaden después de cargar los datos
    comp.querySelectorAll('[data-cat]').forEach((el) => {
      const cats = el.dataset.cat.split(' ');
      el.classList.toggle('is-oculto', cat !== 'todo' && !cats.includes(cat));
    });
    const eventosVisibles = comp.querySelectorAll('.evento:not(.is-oculto)').length;
    vacio.hidden = eventosVisibles > 0;
  }
  comp.querySelectorAll('[data-filtro]').forEach((b) => b.addEventListener('click', () => filtrar(b.dataset.filtro)));
  pintarCuentas();

  /* ---------- Buscador guiado ---------- */
  const f = {
    form: document.getElementById('buscador'),
    que: document.getElementById('f-que'),
    cat: document.getElementById('f-cat'),
    amb: document.getElementById('f-amb'),
    fed: document.getElementById('f-fed'),
    cCat: document.getElementById('c-cat'),
    cAmb: document.getElementById('c-amb'),
    cFed: document.getElementById('c-fed'),
    salida: document.getElementById('f-destino'),
  };
  FEDERACIONES.forEach(([id, nombre]) => f.fed.add(new Option(nombre, id)));

  const RANKINGS = {
    absoluto: 'ranking-absoluto.html', menores: 'ranking-menores.html', veteranos: 'ranking-veteranos.html',
    equipos: 'ranking-equipos-absolutos.html', silla: 'ranking-silla-ruedas.html',
  };
  const CAMPEONATOS = {
    absoluto: 'campeonato-parejas.html', equipos: 'campeonato-equipos.html', silla: 'campeonato-silla.html',
  };
  const CAL_TERRITORIAL = {
    absoluto: 'calendarioft.html', menores: 'calendario-menores-ffaa.html', veteranos: 'calendario-veteranos-ffaa.html',
    equipos: 'campeonato-equipos.html', silla: 'campeonato-silla.html',
  };

  /** Devuelve { ruta, texto } según la selección actual. */
  function resolver() {
    const que = f.que.value, cat = f.cat.value, amb = f.amb.value, fed = f.fed.value;
    const catTxt = f.cat.selectedOptions[0].text.toLowerCase();
    switch (que) {
      case 'ranking': return { ruta: RANKINGS[cat], texto: `Ranking ${catTxt}` };
      case 'campeonato': return { ruta: CAMPEONATOS[cat] || 'campeonatos.html', texto: `Campeonatos · ${catTxt}` };
      case 'licencia': return { ruta: 'buscar-licencia.html', texto: 'Buscador de licencias' };
      case 'club': return { ruta: 'clubes.html', texto: 'Directorio de clubes' };
      default: {
        if (amb === 'fep') return { ruta: 'calendariofep.html', texto: 'Calendario nacional FEP' };
        let ruta = CAL_TERRITORIAL[cat];
        let texto = `Calendario territorial · ${catTxt}`;
        if (cat === 'absoluto' && fed) {
          ruta += `?federacion=${fed}`;
          texto += ` · ${f.fed.selectedOptions[0].text}`;
        }
        return { ruta, texto };
      }
    }
  }

  function actualizarCampos() {
    const que = f.que.value;
    f.cCat.hidden = que === 'licencia' || que === 'club';
    f.cAmb.hidden = que !== 'calendario';
    f.cFed.hidden = !(que === 'calendario' && f.amb.value === 'ffaa' && f.cat.value === 'absoluto');
    const r = resolver();
    f.salida.innerHTML = '';
    f.salida.append('Te llevamos a: ');
    const b = document.createElement('b');
    b.textContent = r.texto;
    f.salida.append(b);
  }

  [f.que, f.cat, f.amb, f.fed].forEach((s) => s.addEventListener('change', actualizarCampos));
  f.form.addEventListener('submit', (e) => {
    e.preventDefault();
    location.href = destino(resolver().ruta);
  });
  actualizarCampos();

  /* ---------- Buscar licencia ---------- */
  const formLic = document.getElementById('form-licencia');
  const licQ = document.getElementById('lic-q');
  const licErr = document.getElementById('lic-error');
  licQ.addEventListener('input', () => { licErr.hidden = true; });
  formLic.addEventListener('submit', (e) => {
    e.preventDefault();
    const q = licQ.value.trim();
    if (q.length < 3) { licErr.hidden = false; licQ.focus(); return; }
    location.href = destino(`buscar-licencia.html?q=${encodeURIComponent(q)}`);
  });

  /* ---------- Volver arriba ---------- */
  const arriba = document.querySelector('.volver-arriba');
  const alScroll = () => arriba.classList.toggle('is-visible', scrollY > 800);
  addEventListener('scroll', alScroll, { passive: true });
  alScroll();
})();
