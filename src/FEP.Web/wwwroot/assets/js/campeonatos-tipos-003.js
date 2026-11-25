/**
 * Campeonatos · Prototipo 003 — Barra de la vista: tipo de torneo y color del calendario.
 * Complemento de campeonatos.js y calendario-mensual-003.js (no cambia su lógica):
 *   - "Parejas" / "Equipos" / "Todos" cambian el filtro "Tipo de torneo" del formulario (#camp-tipo) y lanzan
 *     su evento change, así que la API / el mock, la URL y el resto de filtros siguen funcionando igual.
 *   - "Silla de ruedas" no es un tipo de la API (son torneos por parejas con esa etiqueta): se filtra en la
 *     página con la clase .camp-solo-silla; calendario-mensual-003.js marca cada ficha y barra con data-tipo.
 *   - "Color por": categoría (por defecto) o tipo, con data-color en <main>. Se recuerda en este navegador.
 */
(function () {
'use strict';

const main = document.getElementById('contenido');
const barra = document.getElementById('camp-vista');
const select = document.getElementById('camp-tipo');
if (!main || !barra || !select) return;

const botonesTipo = [...barra.querySelectorAll('[data-tipo]')];
const botonesColor = [...barra.querySelectorAll('[data-color]')];
const estado = document.getElementById('camp-vista-estado');
const CLAVE_COLOR = 'fep003-campeonatos-color';

function marcar(botones, activo) {
  botones.forEach((b) => {
    const si = b === activo;
    b.classList.toggle('is-active', si);
    b.setAttribute('aria-pressed', String(si));
  });
}

/** Botón de tipo activo según el formulario y el filtro de silla. */
function sincronizarTipo() {
  const valor = main.classList.contains('camp-solo-silla') ? 'silla' : select.value;
  marcar(botonesTipo, botonesTipo.find((b) => b.dataset.tipo === valor) ?? botonesTipo[0]);
}

/** Anuncia cuántos torneos se ven tras cambiar de tipo (la lista la repinta campeonatos.js). */
function anunciar() {
  setTimeout(() => {
    const fichas = [...document.querySelectorAll('#camp-lista .camp-item')];
    const visibles = main.classList.contains('camp-solo-silla') ? fichas.filter((f) => f.dataset.tipo === 'silla') : fichas;
    const n = visibles.length;
    estado.textContent = `${n} ${n === 1 ? 'torneo' : 'torneos'}${main.classList.contains('camp-solo-silla') ? ' de pádel en silla de ruedas' : ''}.`;
  }, 400);
}

function elegirTipo(valor) {
  const silla = valor === 'silla';
  main.classList.toggle('camp-solo-silla', silla);
  const destino = silla ? '' : valor;
  if (select.value !== destino) {
    select.value = destino;
    select.dispatchEvent(new Event('change', { bubbles: true }));
  }
  sincronizarTipo();
  anunciar();
}

function elegirColor(modo, guardar = true) {
  main.dataset.color = modo;
  marcar(botonesColor, botonesColor.find((b) => b.dataset.color === modo) ?? botonesColor[0]);
  if (guardar) { try { localStorage.setItem(CLAVE_COLOR, modo); } catch { /* sin almacenamiento: no pasa nada */ } }
}

botonesTipo.forEach((b) => b.addEventListener('click', () => elegirTipo(b.dataset.tipo)));
botonesColor.forEach((b) => b.addEventListener('click', () => elegirColor(b.dataset.color)));

// Si se cambia el tipo desde el formulario o se limpian los filtros, la barra se pone al día
select.addEventListener('change', () => {
  if (select.value) main.classList.remove('camp-solo-silla');
  sincronizarTipo();
});
select.form?.addEventListener('reset', () => {
  main.classList.remove('camp-solo-silla');
  setTimeout(sincronizarTipo);
});

let guardado = 'categoria';
try { guardado = localStorage.getItem(CLAVE_COLOR) || 'categoria'; } catch { /* ignorar */ }
elegirColor(guardado === 'tipo' ? 'tipo' : 'categoria', false);
// El tipo puede venir en la URL (?tipo=equipos) o de una temporada del índice: campeonatos.js rellena el
// formulario y repinta la lista; cada vez que la repinta, la barra se sincroniza.
const lista = document.getElementById('camp-lista');
if (lista) new MutationObserver(sincronizarTipo).observe(lista, { childList: true });
sincronizarTipo();
})();
