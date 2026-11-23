/**
 * Prototipo 003 — Índice lateral ("En esta página") que se para al acabar el contenido.
 * En las páginas con main.cal-003 (calendarios, campeonatos, fichas y rankings) el índice es fijo
 * (position: sticky). Un elemento sticky solo se mueve dentro de su contenedor; el de <nav class="page-toc">
 * es toda la rejilla de la página, que incluye la franja de patrocinadores, así que el panel seguía bajando
 * sobre ella. Este script mete el contenido del índice en una caja (.page-toc__caja): el <nav> ocupa la
 * altura de la zona de contenido y la caja es la que se queda fija, así que se para al llegar a su final.
 * No cambia ids ni enlaces: los scripts de cada página siguen rellenando #…-indice igual.
 */
(function () {
'use strict';

function envolver() {
  document.querySelectorAll('main.cal-003 .page-toc').forEach((nav) => {
    if (nav.querySelector(':scope > .page-toc__caja')) return;
    const caja = document.createElement('div');
    caja.className = 'page-toc__caja';
    while (nav.firstChild) caja.append(nav.firstChild);
    nav.append(caja);
  });
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', envolver);
else envolver();
})();
