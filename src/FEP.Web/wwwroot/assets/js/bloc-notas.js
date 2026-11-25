/**
 * Bloc de notas personal al principio del inicio de Mi Portal, Mi Club y SFEP.
 *
 * - Cada persona (o club) tiene el suyo: se guarda por área y por usuario.
 * - Se guarda solo mientras se escribe (sin botón), con fecha de la última edición.
 * - PROTOTIPO: se guarda en este navegador (localStorage). En producción irá a la API,
 *   para que cada usuario vea sus notas en cualquier dispositivo.
 * - Además, en el inicio, el menú de tarjetas/módulos pasa al final de la página.
 *
 * No toca la lógica de area-privada-003.js ni de sfep.js: espera a que pinten el inicio
 * y entonces coloca el bloc y recoloca el menú.
 */
(function () {
  'use strict';

  const PREFIJO = 'fep-bloc-notas';
  const MAX = 5000;
  const fmt = new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });

  const leer = (k) => { try { return JSON.parse(localStorage.getItem(k) || 'null'); } catch { return null; } };
  const guardar = (k, v) => { try { localStorage.setItem(k, JSON.stringify(v)); return true; } catch { return false; } };
  const borrar = (k) => { try { localStorage.removeItem(k); } catch { /* sin almacenamiento */ } };

  function el(tag, attrs = {}, ...hijos) {
    const n = document.createElement(tag);
    for (const [k, v] of Object.entries(attrs)) {
      if (v == null || v === false) continue;
      if (k === 'text') n.textContent = v; else n.setAttribute(k, v === true ? '' : v);
    }
    n.append(...hijos.filter((h) => h != null));
    return n;
  }

  /** Crea el bloc para un usuario concreto. */
  function crearBloc(area, usuario) {
    const clave = `${PREFIJO}:${area}:${usuario}`;
    const id = `bloc-${area}`;
    const guardado = leer(clave);

    const estado = el('p', { class: 'bloc__estado', 'aria-live': 'polite' });
    const texto = el('textarea', {
      class: 'bloc__texto', id: `${id}-texto`, maxlength: String(MAX), spellcheck: 'true',
      placeholder: 'Escribe aquí lo que no quieras olvidar: un plazo, un teléfono, una tarea pendiente…',
    });
    const cuenta = el('span', { class: 'bloc__cuenta', 'aria-hidden': 'true' });
    const btnBorrar = el('button', { type: 'button', class: 'bloc__borrar', text: 'Borrar notas' });

    const pintarEstado = (fecha, txt) => {
      estado.classList.toggle('is-guardando', txt === 'guardando');
      estado.textContent = txt === 'guardando' ? 'Guardando…'
        : txt === 'error' ? 'No se ha podido guardar en este navegador'
          : fecha ? `Guardado · ${fmt.format(new Date(fecha))}` : 'Sin notas todavía';
    };
    const pintarCuenta = () => { cuenta.textContent = `${texto.value.length} / ${MAX}`; };
    // El área de texto crece con el contenido (hasta un máximo; después, scroll)
    const ajustarAlto = () => { texto.style.height = 'auto'; texto.style.height = `${Math.min(texto.scrollHeight + 2, 480)}px`; };

    texto.value = guardado?.texto ?? '';
    pintarEstado(guardado?.fecha);
    pintarCuenta();

    let espera = null;
    texto.addEventListener('input', () => {
      pintarCuenta();
      ajustarAlto();
      pintarEstado(null, 'guardando');
      clearTimeout(espera);
      espera = setTimeout(() => {
        const fecha = new Date().toISOString();
        if (!texto.value.trim()) { borrar(clave); pintarEstado(null); return; }
        pintarEstado(fecha, guardar(clave, { texto: texto.value, fecha }) ? null : 'error');
      }, 600);
    });
    btnBorrar.addEventListener('click', () => {
      if (!texto.value) return;
      if (!window.confirm('¿Borrar todas las notas de tu bloc? No se pueden recuperar.')) return;
      clearTimeout(espera);
      texto.value = '';
      borrar(clave);
      pintarCuenta();
      ajustarAlto();
      pintarEstado(null);
      texto.focus();
    });

    const bloc = el('section', { class: 'bloc', 'aria-labelledby': `${id}-titulo`, 'data-usuario': usuario },
      el('header', { class: 'bloc__cab' },
        el('div', {},
          el('p', { class: 'bloc__eyebrow', text: 'Solo para ti' }),
          el('h2', { class: 'bloc__titulo', id: `${id}-titulo` }, el('label', { for: `${id}-texto`, text: 'Mi bloc de notas' }))),
        estado),
      el('div', { class: 'bloc__hoja' }, texto),
      el('footer', { class: 'bloc__pie' },
        el('span', { class: 'bloc__nota', text: 'Prototipo: tus notas se guardan solo en este navegador.' }),
        cuenta, btnBorrar));

    setTimeout(ajustarAlto, 0);
    return bloc;
  }

  /* ------------------------------------------------------------------ Mi Portal / Mi Club */
  function usuarioDash() {
    const id = new URLSearchParams(location.search).get('id');
    if (id) return id;
    const m = document.querySelector('.dash-user__id')?.textContent.match(/ID\s+(\S+)/);
    return m ? m[1] : null;
  }

  function colocarDash() {
    const vista = document.getElementById('vista-resumen');
    if (!vista) return;
    const area = document.body.dataset.area || 'portal';
    const usuario = usuarioDash();
    if (!usuario) return;
    const actual = vista.querySelector(':scope > .bloc');
    if (!actual || actual.dataset.usuario !== usuario) {
      actual?.remove();
      vista.prepend(crearBloc(area, usuario));
    }
    // El menú de tarjetas, al final del inicio
    const tarjetas = vista.querySelector(':scope > .dash-cards');
    if (tarjetas && tarjetas !== vista.lastElementChild) vista.append(tarjetas);
  }

  /* ------------------------------------------------------------------ SFEP */
  function usuarioSfep() {
    try { const g = sessionStorage.getItem('sfep-usuario-demo'); if (g) return g; } catch { /* nada */ }
    return document.querySelector('.sfep-top__nombre')?.textContent.trim() || null;
  }

  function colocarSfep() {
    if (!document.body.classList.contains('sfep--inicio')) return;
    const main = document.getElementById('sfep-contenido');
    const menu = main?.querySelector(':scope > .sfep-menu');
    const saludo = main?.querySelector(':scope > .sfep-saludo');
    const usuario = usuarioSfep();
    if (!menu || !saludo || !usuario) return;

    const actual = main.querySelector(':scope > .bloc');
    if (!actual || actual.dataset.usuario !== usuario) {
      actual?.remove();
      saludo.after(crearBloc('sfep', usuario));
    }
    // El menú de módulos, al final, con sus filtros por área encima
    if (menu !== main.lastElementChild) {
      const chips = saludo.querySelector('.sfep-chips');
      let cab = main.querySelector(':scope > .bloc-menucab');
      if (!cab) cab = el('div', { class: 'bloc-menucab' }, el('h2', { class: 'bloc-menucab__titulo', text: 'Módulos' }));
      if (chips) cab.append(chips);
      main.append(cab, menu);
    }
  }

  /* ------------------------------------------------------------------ Arranque */
  function vigilar(contenedor, colocar) {
    let pendiente = false;
    const programar = () => {
      if (pendiente) return;
      pendiente = true;
      // setTimeout y no requestAnimationFrame: este último se detiene si la pestaña no está a la vista
      setTimeout(() => { pendiente = false; colocar(); }, 0);
    };
    new MutationObserver(programar).observe(contenedor, { childList: true, subtree: true });
    programar();
  }

  function iniciar() {
    const panel = document.getElementById('ap-panel');
    if (panel) vigilar(panel, colocarDash);
    const sfep = document.getElementById('sfep-contenido');
    if (sfep) vigilar(sfep, colocarSfep);
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', iniciar);
  else iniciar();
})();
