/**
 * Asistente virtual FEP: burbuja de chat (abajo a la derecha) en todas las páginas.
 *
 * - comun.js lo carga en todas las páginas (junto con assets/css/asistente.css); no hay que tocar cada HTML.
 * - Habla con POST /api/v1/asistente/chat (FEP.Api), que responde con la normativa de docs/info_fep y los datos públicos
 *   de rankings y estadísticas. La API no guarda conversaciones: aquí se guarda el historial en sessionStorage
 *   (solo esta pestaña) y se envían los últimos mensajes en cada pregunta.
 * - Si la API no responde, no hay modo mock: el chat ofrece el formulario de contacto.
 * - Español / inglés: el usuario elige en la cabecera del chat (por defecto, el idioma del navegador).
 *
 * Script clásico (no módulo), como api.js, para que funcione también con file:// (allí solo muestra el aviso).
 */
(function () {
  'use strict';

  if (window.__FEP_ASISTENTE__) return;
  window.__FEP_ASISTENTE__ = true;

  const API_BASE = (document.querySelector('meta[name="fep-api-base"]')?.getAttribute('content') ?? '').trim().replace(/\/+$/, '');
  const RUTA = '/api/v1/asistente/chat';
  const URL_CONTACTO_DEFECTO = 'https://www.padelfederacion.es/Contacto';
  const MAX_MENSAJES = 20;          // ReglasChat.MensajesMaximos
  const MAX_PREGUNTA = 1500;        // ReglasChat.LongitudMaximaPregunta
  const TIMEOUT_MS = 90000;         // razonamiento + varias búsquedas pueden tardar
  const CLAVE_HISTORIAL = 'fep-asistente-historial';
  const CLAVE_IDIOMA = 'fep-asistente-idioma';

  const TEXTOS = {
    es: {
      abrir: 'Abrir FEPi, el asistente de la FEP',
      cerrar: 'Cerrar FEPi',
      titulo: 'FEPi',
      subtitulo: 'Asistente virtual de la FEP',
      bienvenida: '¡Hola! Soy FEPi, el asistente virtual de la Federación Española de Pádel. Puedo resolver dudas sobre reglamentos, circulares, estatutos y rankings. ¿En qué te ayudo?',
      sugerencias: ['¿Cómo se calcula el Ranking Único?', '¿Qué edades tiene cada categoría de menores?', '¿Qué sanción tiene no presentarse a un partido?'],
      placeholder: 'Escribe tu pregunta…',
      enviar: 'Enviar',
      pensando: 'Buscando en la normativa…',
      aviso: 'Respuestas generadas con IA a partir de la documentación oficial. Pueden contener errores: comprueba siempre el documento citado.',
      fuentes: 'Fuentes',
      derivar: '¿Necesitas que te ayude una persona?',
      contacto: 'Escribir a la FEP',
      nueva: 'Nueva conversación',
      errorRed: 'No he podido conectar con FEPi. Inténtalo de nuevo en un momento o escríbenos desde el formulario de contacto.',
      errorLimite: 'Has hecho muchas preguntas seguidas. Espera un poco antes de volver a preguntar.',
      errorNoDisponible: 'FEPi no está disponible en este momento. Puedes escribirnos desde el formulario de contacto.',
      demasiadoLargo: `La pregunta es demasiado larga (máximo ${MAX_PREGUNTA} caracteres).`,
      idioma: 'Idioma',
      tu: 'Tú',
    },
    en: {
      abrir: 'Open FEPi, the FEP assistant',
      cerrar: 'Close FEPi',
      titulo: 'FEPi',
      subtitulo: 'FEP virtual assistant',
      bienvenida: 'Hi! I am FEPi, the virtual assistant of the Spanish Padel Federation. I can answer questions about regulations, circulars, statutes and rankings. How can I help you?',
      sugerencias: ['How is the national ranking calculated?', 'What are the age groups for juniors?', 'What is the penalty for not showing up to a match?'],
      placeholder: 'Type your question…',
      enviar: 'Send',
      pensando: 'Searching the regulations…',
      aviso: 'AI-generated answers based on the official documents. They may contain mistakes: always check the cited document.',
      fuentes: 'Sources',
      derivar: 'Would you like help from a person?',
      contacto: 'Contact the FEP',
      nueva: 'New conversation',
      errorRed: "I couldn't reach FEPi. Please try again in a moment or use the contact form.",
      errorLimite: "You've asked a lot of questions in a row. Please wait a little before asking again.",
      errorNoDisponible: 'FEPi is not available right now. You can contact us through the contact form.',
      demasiadoLargo: `Your question is too long (maximum ${MAX_PREGUNTA} characters).`,
      idioma: 'Language',
      tu: 'You',
    },
  };

  /* --------------------------------------------------------------------------------------------------
     Estado (historial en sessionStorage; si el navegador lo bloquea, solo en memoria)
     -------------------------------------------------------------------------------------------------- */
  const leer = (clave, almacen) => { try { return almacen.getItem(clave); } catch { return null; } };
  const guardar = (clave, valor, almacen) => { try { almacen.setItem(clave, valor); } catch { /* sin almacenamiento */ } };

  let idioma = leer(CLAVE_IDIOMA, localStorage) || ((navigator.language || 'es').toLowerCase().startsWith('en') ? 'en' : 'es');
  if (!TEXTOS[idioma]) idioma = 'es';
  const t = () => TEXTOS[idioma];

  /** @type {{rol:'usuario'|'asistente', texto:string, fuentes?:any[], derivar?:boolean, urlContacto?:string, error?:boolean}[]} */
  let historial = [];
  try { historial = JSON.parse(leer(CLAVE_HISTORIAL, sessionStorage) || '[]'); } catch { historial = []; }
  if (!Array.isArray(historial)) historial = [];
  const guardarHistorial = () => guardar(CLAVE_HISTORIAL, JSON.stringify(historial.slice(-40)), sessionStorage);

  let enviando = false;

  /* --------------------------------------------------------------------------------------------------
     Utilidades DOM
     -------------------------------------------------------------------------------------------------- */
  function el(tag, props = {}, ...hijos) {
    const n = document.createElement(tag);
    for (const [k, v] of Object.entries(props)) {
      if (v == null || v === false) continue;
      if (k === 'class') n.className = v;
      else if (k === 'text') n.textContent = v;
      else if (k.startsWith('on')) n.addEventListener(k.slice(2), v);
      else n.setAttribute(k, v === true ? '' : v);
    }
    for (const h of hijos.flat()) if (h != null) n.append(h);
    return n;
  }

  const svg = (d, tam = 24) => {
    const s = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    s.setAttribute('viewBox', '0 0 24 24');
    s.setAttribute('width', tam);
    s.setAttribute('height', tam);
    s.setAttribute('aria-hidden', 'true');
    s.setAttribute('focusable', 'false');
    const p = document.createElementNS('http://www.w3.org/2000/svg', 'path');
    p.setAttribute('d', d);
    p.setAttribute('fill', 'currentColor');
    s.append(p);
    return s;
  };
  /**
   * Logo del asistente: pala de pádel en diagonal (cabeza perforada, puente y empuñadura) con su pelota.
   * La cabeza es un único trazado con fill-rule evenodd para que los agujeros queden transparentes.
   */
  function iconoPadel(tam = 24) {
    const NS = 'http://www.w3.org/2000/svg';
    const nodo = (tag, attrs) => {
      const n = document.createElementNS(NS, tag);
      for (const [k, v] of Object.entries(attrs)) n.setAttribute(k, v);
      return n;
    };
    const elipse = (x, y, rx, ry) => `M${x - rx} ${y}a${rx} ${ry} 0 1 0 ${2 * rx} 0a${rx} ${ry} 0 1 0 ${-2 * rx} 0Z`;
    const agujeros = [[10, 5.6], [14, 5.6], [8, 8.8], [12, 8.8], [16, 8.8], [10, 12], [14, 12]];
    const cabeza = elipse(12, 9, 6.4, 7.2) + agujeros.map(([x, y]) => elipse(x, y, 0.95, 0.95)).join('');
    const s = nodo('svg', { viewBox: '0 0 24 24', width: tam, height: tam, 'aria-hidden': 'true', focusable: 'false', fill: 'currentColor' });
    const pala = nodo('g', { transform: 'rotate(-45 12 12)' });
    pala.append(
      nodo('path', { d: cabeza, 'fill-rule': 'evenodd' }),
      nodo('path', { d: 'M10.4 15.6h3.2l-.5 2.6h-2.2z' }),          // puente
      nodo('rect', { x: '10.5', y: '17.8', width: '3', height: '5.4', rx: '1.3' })); // empuñadura
    s.append(pala, nodo('circle', { cx: '19.6', cy: '4.4', r: '2.4' }));                // pelota
    return s;
  }
  const ICONO_CERRAR = 'M6.4 5 5 6.4 10.6 12 5 17.6 6.4 19l5.6-5.6 5.6 5.6 1.4-1.4-5.6-5.6L19 6.4 17.6 5 12 10.6z';
  const ICONO_ENVIAR = 'M3.4 20.4 21 12 3.4 3.6 3.4 10l12.6 2-12.6 2z';
  const ICONO_NUEVA = 'M12 5V1L7 6l5 5V7a5 5 0 1 1-5 5H5a7 7 0 1 0 7-7z';

  /** Enlace permitido en las respuestas: http(s) o página .html del propio sitio. Nunca javascript:, data:… */
  function urlPermitida(href) {
    if (!href) return null;
    try {
      const u = new URL(href, document.baseURI);
      return ['http:', 'https:'].includes(u.protocol) ? u.href : null;
    } catch { return null; }
  }

  /**
   * Markdown mínimo y seguro (se construye con nodos, nunca innerHTML): párrafos, listas con "-", "*" o "1.",
   * **negrita**, *cursiva* y [enlaces](url).
   */
  function markdown(texto) {
    const cont = el('div', { class: 'fep-asis__md' });
    const lineas = String(texto ?? '').replace(/\r/g, '').split('\n');
    let lista = null;
    let parrafo = [];
    const cerrarParrafo = () => {
      if (parrafo.length) { cont.append(el('p', {}, enLinea(parrafo.join(' ')))); parrafo = []; }
    };
    for (const bruta of lineas) {
      const linea = bruta.trim();
      const item = linea.match(/^(?:[-*•]|\d+[.)])\s+(.*)$/);
      if (item) {
        cerrarParrafo();
        const ordenada = /^\d/.test(linea);
        if (!lista || (lista.tagName === 'OL') !== ordenada) { lista = el(ordenada ? 'ol' : 'ul'); cont.append(lista); }
        lista.append(el('li', {}, enLinea(item[1])));
        continue;
      }
      if (!linea) { cerrarParrafo(); lista = null; continue; }
      lista = null;
      parrafo.push(linea.replace(/^#{1,6}\s+/, ''));
    }
    cerrarParrafo();
    return cont;
  }

  function enLinea(texto) {
    const frag = document.createDocumentFragment();
    const re = /\[([^\]]+)\]\(([^)\s]+)\)|\*\*([^*]+)\*\*|\*([^*\s][^*]*)\*/g;
    let ultimo = 0;
    let m;
    while ((m = re.exec(texto))) {
      if (m.index > ultimo) frag.append(texto.slice(ultimo, m.index));
      if (m[1] !== undefined) {
        const url = urlPermitida(m[2]);
        frag.append(url ? enlace(url, m[1]) : m[1]);
      } else if (m[3] !== undefined) frag.append(el('strong', { text: m[3] }));
      else frag.append(el('em', { text: m[4] }));
      ultimo = re.lastIndex;
    }
    if (ultimo < texto.length) frag.append(texto.slice(ultimo));
    return frag;
  }

  function enlace(url, texto) {
    const externo = new URL(url).origin !== location.origin;
    return el('a', { href: url, text: texto, target: externo ? '_blank' : null, rel: externo ? 'noopener' : null });
  }

  /* --------------------------------------------------------------------------------------------------
     Interfaz
     -------------------------------------------------------------------------------------------------- */
  let raiz, boton, panel, lista, campo, botonEnviar, contador;

  function construir() {
    boton = el('button', {
      type: 'button', class: 'fep-asis__burbuja', 'aria-expanded': 'false', 'aria-controls': 'fep-asis-panel',
      onclick: () => alternar(),
    }, iconoPadel(32), el('span', { class: 'sr-only fep-asis__sr' }));

    panel = el('section', { id: 'fep-asis-panel', class: 'fep-asis__panel', role: 'dialog', 'aria-modal': 'false', hidden: true });
    raiz = el('div', { class: 'fep-asis', lang: idioma }, panel, boton);
    document.body.append(raiz);
    pintarPanel();

    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape' && !panel.hidden) { alternar(false); boton.focus(); }
    });
  }

  function pintarPanel() {
    raiz.lang = idioma;
    boton.querySelector('.fep-asis__sr').textContent = t().abrir;
    boton.title = t().titulo;
    panel.setAttribute('aria-label', t().titulo);
    panel.replaceChildren();

    const selector = el('div', { class: 'fep-asis__idioma', role: 'group', 'aria-label': t().idioma },
      ...['es', 'en'].map((codigo) => el('button', {
        type: 'button', text: codigo.toUpperCase(), 'aria-pressed': String(codigo === idioma), lang: codigo,
        'aria-label': codigo === 'es' ? 'Español' : 'English',
        onclick: () => { if (codigo !== idioma) { idioma = codigo; guardar(CLAVE_IDIOMA, codigo, localStorage); pintarPanel(); campo.focus(); } },
      })));

    const cabecera = el('header', { class: 'fep-asis__cabecera' },
      el('div', { class: 'fep-asis__marca' },
        el('span', { class: 'fep-asis__avatar', 'aria-hidden': 'true' }, iconoPadel(22)),
        el('div', {},
          el('h2', { class: 'fep-asis__titulo', text: t().titulo }),
          el('p', { class: 'fep-asis__subtitulo', text: t().subtitulo }))),
      el('div', { class: 'fep-asis__acciones' },
        selector,
        el('button', { type: 'button', class: 'fep-asis__icono', title: t().nueva, 'aria-label': t().nueva, onclick: nuevaConversacion }, svg(ICONO_NUEVA, 20)),
        el('button', { type: 'button', class: 'fep-asis__icono', title: t().cerrar, 'aria-label': t().cerrar, onclick: () => { alternar(false); boton.focus(); } }, svg(ICONO_CERRAR, 20))));

    lista = el('div', { class: 'fep-asis__mensajes', role: 'log', 'aria-live': 'polite', 'aria-relevant': 'additions', tabindex: '0' });

    campo = el('textarea', {
      class: 'fep-asis__campo', rows: '1', placeholder: t().placeholder, 'aria-label': t().placeholder, maxlength: String(MAX_PREGUNTA),
      oninput: () => { ajustarAltura(); actualizarContador(); },
      onkeydown: (e) => { if (e.key === 'Enter' && !e.shiftKey && !e.isComposing) { e.preventDefault(); enviar(); } },
    });
    botonEnviar = el('button', { type: 'submit', class: 'fep-asis__enviar', 'aria-label': t().enviar, title: t().enviar }, svg(ICONO_ENVIAR, 20));
    contador = el('span', { class: 'fep-asis__contador', 'aria-hidden': 'true' });
    const formulario = el('form', { class: 'fep-asis__form', onsubmit: (e) => { e.preventDefault(); enviar(); } }, campo, botonEnviar);

    panel.append(cabecera, lista, el('div', { class: 'fep-asis__pie' }, formulario, contador, el('p', { class: 'fep-asis__aviso', text: t().aviso })));
    pintarMensajes();
    actualizarContador();
  }

  function pintarMensajes() {
    lista.replaceChildren();
    lista.append(burbujaAsistente({ texto: t().bienvenida }));
    if (!historial.length) {
      lista.append(el('div', { class: 'fep-asis__sugerencias' },
        ...t().sugerencias.map((s) => el('button', { type: 'button', class: 'fep-asis__sugerencia', text: s, onclick: () => { campo.value = s; enviar(); } }))));
    }
    for (const m of historial) lista.append(m.rol === 'usuario' ? burbujaUsuario(m.texto) : burbujaAsistente(m));
    bajar();
  }

  const burbujaUsuario = (texto) => el('div', { class: 'fep-asis__msg fep-asis__msg--usuario' },
    el('span', { class: 'sr-only', text: `${t().tu}: ` }), el('p', { text: texto }));

  function burbujaAsistente(m) {
    const caja = el('div', { class: `fep-asis__msg fep-asis__msg--asistente${m.error ? ' fep-asis__msg--error' : ''}` },
      el('span', { class: 'sr-only', text: `${t().titulo}: ` }), markdown(m.texto));
    if (m.fuentes?.length) {
      caja.append(el('div', { class: 'fep-asis__fuentes' },
        el('span', { class: 'fep-asis__fuentes-titulo', text: `${t().fuentes}:` }),
        el('ul', {}, ...m.fuentes.map((f) => {
          const texto = f.detalle ? `${f.titulo} (${f.detalle})` : f.titulo;
          const url = urlPermitida(f.url);
          return el('li', {}, url ? enlace(url, texto) : texto);
        }))));
    }
    if (m.derivar) {
      const url = urlPermitida(m.urlContacto) || URL_CONTACTO_DEFECTO;
      caja.append(el('div', { class: 'fep-asis__derivar' },
        el('span', { text: t().derivar }),
        el('a', { class: 'fep-asis__boton-contacto', href: url, target: '_blank', rel: 'noopener', text: t().contacto })));
    }
    return caja;
  }

  const bajar = () => { lista.scrollTop = lista.scrollHeight; };
  function ajustarAltura() { campo.style.height = 'auto'; campo.style.height = `${Math.min(campo.scrollHeight, 140)}px`; }
  function actualizarContador() {
    const n = campo.value.length;
    contador.textContent = n > MAX_PREGUNTA * 0.8 ? `${n}/${MAX_PREGUNTA}` : '';
  }

  function alternar(abrir = panel.hidden) {
    panel.hidden = !abrir;
    boton.setAttribute('aria-expanded', String(abrir));
    raiz.classList.toggle('is-abierto', abrir);
    if (abrir) { bajar(); setTimeout(() => campo.focus(), 50); }
  }

  function nuevaConversacion() {
    if (enviando) return;
    historial = [];
    guardarHistorial();
    pintarMensajes();
    campo.focus();
  }

  /* --------------------------------------------------------------------------------------------------
     Envío
     -------------------------------------------------------------------------------------------------- */
  /** Últimos mensajes válidos para la API: alternando usuario/asistente y acabando en el usuario. */
  function conversacionParaApi() {
    const validos = historial.filter((m) => !m.error && m.texto);
    const salida = [];
    for (let i = validos.length - 1; i >= 0 && salida.length < MAX_MENSAJES; i--) {
      const esperado = salida.length % 2 === 0 ? 'usuario' : 'asistente';
      if (validos[i].rol !== esperado) break;
      salida.unshift({ rol: validos[i].rol, texto: validos[i].texto });
    }
    return salida;
  }

  async function enviar() {
    const texto = campo.value.trim();
    if (!texto || enviando) return;
    if (texto.length > MAX_PREGUNTA) { añadir({ rol: 'asistente', texto: t().demasiadoLargo, error: true }); return; }

    // Si la última pregunta quedó sin respuesta (error), se sustituye: la API exige alternancia.
    while (historial.length && historial[historial.length - 1].rol === 'usuario') historial.pop();
    historial = historial.filter((m) => !m.error);

    campo.value = '';
    ajustarAltura();
    actualizarContador();
    añadir({ rol: 'usuario', texto });
    lista.querySelector('.fep-asis__sugerencias')?.remove();

    enviando = true;
    botonEnviar.disabled = true;
    const pensando = el('div', { class: 'fep-asis__msg fep-asis__msg--asistente fep-asis__pensando', role: 'status' },
      el('span', { class: 'fep-asis__puntos', 'aria-hidden': 'true' }, el('i'), el('i'), el('i')), el('span', { text: t().pensando }));
    lista.append(pensando);
    bajar();

    try {
      const r = await preguntar(conversacionParaApi());
      añadir({ rol: 'asistente', texto: r.texto, fuentes: r.fuentes ?? [], derivar: !!r.derivar, urlContacto: r.urlContacto });
    } catch (e) {
      // La pregunta queda en pantalla pero se marca para no reenviarla como contexto.
      const ultima = historial[historial.length - 1];
      if (ultima?.rol === 'usuario') ultima.error = true;
      añadir({ rol: 'asistente', texto: e.mensaje || t().errorRed, error: true, derivar: e.derivar !== false, urlContacto: e.urlContacto });
    } finally {
      pensando.remove();
      enviando = false;
      botonEnviar.disabled = false;
      campo.focus();
    }
  }

  function añadir(m) {
    historial.push(m);
    guardarHistorial();
    lista.append(m.rol === 'usuario' ? burbujaUsuario(m.texto) : burbujaAsistente(m));
    bajar();
  }

  async function preguntar(mensajes) {
    if (location.protocol === 'file:' && !API_BASE) throw { mensaje: t().errorNoDisponible };
    const ctrl = new AbortController();
    const temporizador = setTimeout(() => ctrl.abort(), TIMEOUT_MS);
    let res;
    try {
      res = await fetch(API_BASE + RUTA, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
        body: JSON.stringify({ mensajes, idioma }),
        signal: ctrl.signal,
      });
    } catch {
      throw { mensaje: t().errorRed };
    } finally {
      clearTimeout(temporizador);
    }
    let cuerpo = null;
    try { cuerpo = await res.json(); } catch { /* respuesta sin JSON */ }
    if (res.ok && cuerpo && typeof cuerpo.texto === 'string') return cuerpo;
    if (res.status === 429) throw { mensaje: t().errorLimite, derivar: false };
    if (res.status === 503) throw { mensaje: t().errorNoDisponible, urlContacto: cuerpo?.urlContacto };
    throw { mensaje: t().errorRed, urlContacto: cuerpo?.urlContacto };
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', construir);
  else construir();
})();
