/* =========================================================================
   FEP · PoC de páginas interiores con el diseño de la portada nueva (body.ua)
   Barra superior (federaciones autonómicas + idiomas), menú con la sección actual,
   "volver arriba" y fondo adaptable del botón de FEPi: igual que en la portada
   (assets/js/home-unapagina.js). comun.js sigue haciendo el resto de la página.
   ========================================================================= */
(() => {
  'use strict';

  /* Federaciones autonómicas: [id BD FEP, nombre corto, web propia] (de mock/federacion-federaciones-autonomicas.json) */
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

  function el(tag, attrs = {}, ...hijos) {
    const n = document.createElement(tag);
    for (const [k, v] of Object.entries(attrs)) {
      if (v == null || v === false) continue;
      if (k === 'text') n.textContent = v; else n.setAttribute(k, v === true ? '' : v);
    }
    n.append(...hijos.filter((h) => h != null));
    return n;
  }

  /* ---------- Desplegable de federaciones autonómicas ---------- */
  function iniciarFederaciones() {
    const boton = document.getElementById('feds-boton');
    const panel = document.getElementById('feds-panel');
    const lista = document.getElementById('feds-panel-lista');
    if (!boton || !panel || !lista) return;
    lista.append(...FEDERACIONES.map(([id, nombre, web]) => {
      const externo = Boolean(web);
      return el('li', {}, el('a', {
        href: web || `federacion.html?id=${id}`, target: externo ? '_blank' : null, rel: externo ? 'noopener' : null,
        title: externo ? `${nombre}: abre su web en otra pestaña` : `${nombre}: ficha en la web FEP`,
      }, el('img', { class: 'bandera', src: `assets/img/fep/federaciones/bandera-${id}.jpg`, alt: '', width: 21, height: 15, loading: 'lazy' }),
      nombre, el('small', { 'aria-hidden': 'true', text: externo ? '↗' : '' })));
    }));
    const abrir = (si) => {
      panel.hidden = !si;
      boton.setAttribute('aria-expanded', String(si));
      if (si) panel.querySelector('a')?.focus();
    };
    boton.addEventListener('click', () => abrir(panel.hidden));
    document.addEventListener('click', (e) => { if (!panel.hidden && !e.target.closest('#feds-menu')) abrir(false); });
    document.addEventListener('keydown', (e) => { if (e.key === 'Escape' && !panel.hidden) { abrir(false); boton.focus(); } });
  }

  /* ---------- Idiomas (mismo comportamiento que la portada: guarda la elección; aún sin traducción) ---------- */
  function iniciarIdiomas() {
    const caja = document.querySelector('.topbar .idiomas');
    if (!caja) return;
    const CLAVE = 'fep003.idioma';
    let idioma = 'es';
    try { idioma = localStorage.getItem(CLAVE) || 'es'; } catch { /* sin almacenamiento */ }
    const pintar = () => {
      for (const [grupo, lista] of Object.entries(IDIOMAS)) {
        document.getElementById(`idiomas-${grupo}`)?.replaceChildren(...lista.map(([cod, nombre, svg]) => {
          const b = el('button', { type: 'button', class: 'idioma', lang: cod === 'va' ? 'ca' : cod, title: nombre, 'aria-label': nombre, 'aria-pressed': String(cod === idioma), 'data-idioma': cod });
          b.innerHTML = `<span class="bandera"><svg viewBox="0 0 30 20" preserveAspectRatio="xMidYMid slice" aria-hidden="true" focusable="false">${svg}</svg></span>`;
          return el('li', {}, b);
        }));
      }
    };
    caja.addEventListener('click', (e) => {
      const b = e.target.closest('[data-idioma]');
      if (!b) return;
      idioma = b.dataset.idioma;
      try { localStorage.setItem(CLAVE, idioma); } catch { /* sin almacenamiento */ }
      pintar();
      caja.querySelector(`[data-idioma="${idioma}"]`)?.focus();
    });
    pintar();
  }

  /* ---------- Sección actual en el menú (por defecto, Competición) ---------- */
  function marcarSeccion() {
    const seccion = document.body.dataset.seccion || 'competicion';
    document.querySelectorAll('.header .nav__link').forEach((a) => {
      const si = a.dataset.seccion === seccion;
      a.classList.toggle('is-current', si);
      if (si) a.setAttribute('aria-current', 'true'); else a.removeAttribute('aria-current');
    });
  }

  /* ---------- Volver arriba ---------- */
  function iniciarVolverArriba() {
    const a = el('a', { class: 'ua-arriba', href: '#', 'aria-label': 'Volver arriba', text: '↑' });
    a.addEventListener('click', (e) => { e.preventDefault(); window.scrollTo({ top: 0, behavior: 'smooth' }); document.querySelector('.header__logo')?.focus({ preventScroll: true }); });
    document.body.append(a);
    const ver = () => a.classList.toggle('is-visible', window.scrollY > 800);
    window.addEventListener('scroll', ver, { passive: true });
    ver();
  }

  /* ---------- FEPi: el botón cambia de estilo según el fondo que tiene detrás (como en la portada) ---------- */
  function fondoEn(x, y) {
    const pila = document.elementsFromPoint(x, y).filter((n) => !n.closest('.fep-asis, .ua-arriba'));
    for (let n = pila[0]; n && n !== document.documentElement; n = n.parentElement) {
      if (n.tagName === 'IMG' || n.tagName === 'VIDEO') return 'foto';
      const cs = getComputedStyle(n);
      if (cs.backgroundImage !== 'none' && !cs.backgroundImage.includes('gradient')) return 'foto';
      const m = cs.backgroundColor.match(/[\d.]+/g);
      if (!m) continue;
      const [r, g, b, alfa = 1] = m.map(Number);
      if (alfa < .5) continue;
      if (r > 170 && g < 90 && b < 100) return 'rojo';
      return (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255 < .45 ? 'oscuro' : 'claro';
    }
    return 'claro';
  }
  function vigilarAsistente(raiz) {
    const boton = raiz.querySelector('.fep-asis__burbuja');
    let pendiente = false;
    const medir = () => {
      pendiente = false;
      if (!boton || boton.offsetParent === null) return;
      const r = boton.getBoundingClientRect();
      const y = r.top + r.height / 2;
      const votos = [r.left + 6, r.left + r.width / 2, r.right - 6].map((x) => fondoEn(x, y));
      const fondo = votos.find((v, i) => votos.indexOf(v) !== i) || votos[1];
      if (raiz.dataset.fondo !== fondo) raiz.dataset.fondo = fondo;
    };
    const programar = () => { if (!pendiente) { pendiente = true; setTimeout(medir, 50); } };
    window.addEventListener('scroll', programar, { passive: true });
    window.addEventListener('resize', programar);
    setInterval(programar, 1500);
    medir();
  }
  function esperarAsistente() {
    const ya = document.querySelector('.fep-asis');
    if (ya) { vigilarAsistente(ya); return; }
    const obs = new MutationObserver(() => {
      const raiz = document.querySelector('.fep-asis');
      if (raiz) { obs.disconnect(); vigilarAsistente(raiz); }
    });
    obs.observe(document.body, { childList: true });
  }

  function iniciar() {
    iniciarFederaciones();
    iniciarIdiomas();
    marcarSeccion();
    iniciarVolverArriba();
    esperarAsistente();
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', iniciar);
  else iniciar();
})();
