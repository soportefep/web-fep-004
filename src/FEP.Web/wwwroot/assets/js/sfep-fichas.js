/**
 * SFEP · Módulo "Fichas jugadores/as" (sustituye a Gestion/FichasFEP.asp y su submenú).
 *
 * PROTOTIPO, SIN CONEXIÓN A BASE DE DATOS: las fichas salen de mock/sfep-fichas.json (GET /api/v1/sfep/fichas),
 * 320 fichas ficticias generadas por tools/generar-mock-sfep-fichas.ps1. Las acciones no se guardan.
 *
 * Rutas: #m/fichas (portada del módulo) · #m/fichas/buscar[?q=texto] · #m/fichas/<apartado>.
 * Reglas de FichasFEP.asp:
 *   - Buscar fichas: siempre.
 *   - Nueva ficha e Importar Excel: permiso 1-1 (fichasNueva). Importar no para usuarios "solo consulta".
 *   - Renovación masiva, Seguro, Renovados sin pagar, Transferencias y Domiciliaciones: permiso 1-2
 *     (fichasModificar) y no "solo consulta" (antes los usuarios 1350 y 1290).
 *   - Censo electoral: lo mismo y además no "sinCenso" (antes el usuario 276).
 *   - Licencias extranjeros: solo la FEP (federación 0). Validaciones: solo la federación 6.
 *   - Cada usuario ve las fichas de su federación; la FEP (0) ve todas.
 * Solo "Buscar fichas" (Gestion/Fichas.asp) está migrado; el resto muestra "pendiente de migrar" y, cuando hay datos,
 * una vista previa. Una ficha es una persona y puede tener 1, 2 o 3 licencias: jugador/a, técnico/a y juez-árbitro
 * (columnas Lic.Jug, Lic.Tec y Lic.Arb del SFEP actual), cada una con su número y su estado.
 */
(function () {
'use strict';
const S = window.SFEP;
if (!S) return;
const { el, $, $$, icono, toast, normaliza } = S;

const POR_PAGINA = 20;
const fmtNum = new Intl.NumberFormat('es-ES');
const slug = (t) => normaliza(t).replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
const estado = (t) => el('span', { class: `sfep-estado sfep-estado--${slug(t)}`, text: t });
const nombreCompleto = (f) => `${f.apellidos}, ${f.nombre}`;

/* ---------------------------------------------------------------------------
   Datos (una sola carga por sesión de página)
   --------------------------------------------------------------------------- */
let carga = null;
function datos() {
  carga ??= S.get('/api/v1/sfep/fichas').catch((e) => { carga = null; throw e; });
  return carga;
}
/** Fichas que puede ver el usuario: las de su federación (la FEP, todas). */
function delUsuario(d) {
  const u = S.usuario();
  const todas = S.comoLista(d);
  return u.federacion?.id === 0 ? todas : todas.filter((f) => f.federacion?.id === u.federacion?.id);
}

/* ---------------------------------------------------------------------------
   Apartados del módulo (submenú de FichasFEP.asp reorganizado)
   --------------------------------------------------------------------------- */
const APARTADOS = {
  buscar: { nombre: 'Buscar fichas', icono: 'fichas', legado: 'Fichas.asp', desc: 'Por DNI, licencia, nombre, club, categoría, residencia, contacto… y tipo de licencia.', migrado: true },
  nueva: { nombre: 'Nueva ficha', icono: 'fichas', legado: 'Ficha_Nuevo.asp', desc: 'Alta de una licencia nueva.',
    funciones: ['Datos personales y de contacto', 'Club, categoría y tipo de licencia', 'Seguro deportivo y forma de pago'] },
  importar: { nombre: 'Importar Excel', icono: 'informes', legado: 'Fichas_Importar.asp', desc: 'Alta de fichas en bloque desde una hoja de cálculo.',
    funciones: ['Plantilla de importación', 'Comprobación de errores antes de importar', 'Resumen de fichas creadas'] },
  extranjeros: { nombre: 'Licencias extranjeros', icono: 'portal', legado: 'Fichas_Extranjeros.asp', desc: 'Licencias de jugadores/as con nacionalidad extranjera.',
    funciones: ['Listado de licencias de extranjeros', 'Documentación requerida', 'Alta y renovación'], filtro: (f) => f.extranjero },
  masiva: { nombre: 'Renovación masiva', icono: 'eventos', legado: 'Fichas_RenovacionMasivaBusqueda.asp', desc: 'Renovar de una vez las fichas de la temporada anterior.',
    funciones: ['Búsqueda de fichas a renovar por club o federación', 'Selección múltiple', 'Renovación y cobro en bloque'] },
  seguro: { nombre: 'Tramitar / renovar seguro', icono: 'certificados', legado: 'Fichas_Mutua.asp', desc: 'Alta y renovación del seguro deportivo de la licencia.',
    funciones: ['Alta del seguro en la mutua', 'Renovación anual', 'Partes y certificados del seguro'] },
  sinpagar: { nombre: 'Renovados sin pagar', icono: 'banco', legado: 'Fichas_SinPagar.asp', desc: 'Fichas renovadas pendientes de pago.',
    funciones: ['Listado de renovaciones sin pagar', 'Reclamación del pago', 'Anulación de la renovación'], filtro: (f) => f.estado === 'renovada sin pagar' },
  transferencias: { nombre: 'Transferencias recibidas', icono: 'clubes', legado: 'Fichas_Transferencias.asp', desc: 'Cambios de club pendientes de aceptar.',
    funciones: ['Solicitudes de cambio de club recibidas', 'Aceptar o rechazar', 'Histórico de transferencias'], filtro: (f) => !!f.transferenciaPendiente },
  domiciliaciones: { nombre: 'Domiciliaciones recibidas', icono: 'banco', legado: 'Fichas_Domiciliaciones.asp', desc: 'Pagos por domiciliación bancaria recibidos.',
    funciones: ['Domiciliaciones recibidas', 'Conciliación con el banco', 'Activación de la ficha al cobrar'], filtro: (f) => f.domiciliacionRecibida },
  validaciones: { nombre: 'Validaciones', icono: 'eventos', legado: 'Fichas_Validaciones.asp', desc: 'Fichas pendientes de validar por la federación.',
    funciones: ['Fichas pendientes de validar', 'Validar o devolver con motivo'], filtro: (f) => f.estado === 'pendiente de validar' },
  censo: { nombre: 'Censo electoral', icono: 'voto', legado: 'Fichas_Censo.asp', desc: 'Censo de federados para los procesos electorales.',
    funciones: ['Censo por estamento y federación', 'Exportación del censo', 'Reclamaciones al censo'] },
};

const GRUPOS = [
  { nombre: 'Consulta', ids: ['buscar'] },
  { nombre: 'Altas', ids: ['nueva', 'importar', 'extranjeros'] },
  { nombre: 'Bandeja de entrada', ids: ['transferencias', 'domiciliaciones', 'validaciones'] },
  { nombre: 'Renovaciones y seguro', ids: ['masiva', 'seguro', 'sinpagar'] },
  { nombre: 'Censo', ids: ['censo'] },
];

function puede(id) {
  const u = S.usuario();
  const p = u.permisos ?? {};
  const consulta = !!u.fichasSoloConsulta;
  const fed = u.federacion?.id;
  switch (id) {
    case 'buscar': return true;
    case 'nueva': return !!p.fichasNueva;
    case 'importar': return !!p.fichasNueva && !consulta;
    case 'extranjeros': return fed === 0;
    case 'validaciones': return fed === 6 && !!p.fichasModificar && !consulta;
    case 'censo': return !!p.fichasModificar && !consulta && !u.sinCenso;
    default: return !!p.fichasModificar && !consulta;   // masiva, seguro, sinpagar, transferencias, domiciliaciones
  }
}

/* ---------------------------------------------------------------------------
   Piezas comunes
   --------------------------------------------------------------------------- */
function migas(...tramos) {
  const area = S.area('fichas');
  const partes = [el('a', { href: '#inicio', text: 'Inicio' }), el('span', { text: area.nombre })];
  partes.push(tramos.length ? el('a', { href: '#m/fichas', text: 'Fichas jugadores/as' }) : el('span', { 'aria-current': 'page', text: 'Fichas jugadores/as' }));
  tramos.forEach((t, i) => partes.push(i === tramos.length - 1 ? el('span', { 'aria-current': 'page', text: t }) : el('span', { text: t })));
  const nav = el('nav', { class: 'sfep-migas', 'aria-label': 'Estás en' });
  partes.forEach((p, i) => { if (i) nav.append(el('span', { 'aria-hidden': 'true', text: '/' })); nav.append(p); });
  return nav;
}
function cabecera(titulo, desc, ico = 'fichas', favorito = false) {
  return el('div', { class: 'sfep-modcab' }, icono(ico, 'sfep-modcab__ico'),
    el('div', {}, el('h1', { class: 'sfep-h1', id: 'sfep-titulo', tabindex: '-1', text: titulo }), el('p', { class: 'sfep-modcab__desc', text: desc })),
    favorito ? S.botonFavorito('fichas') : null);
}
const cargando = () => el('p', { class: 'sfep-vacio', role: 'status', text: 'Cargando fichas…' });
const errorCarga = () => el('p', { class: 'sfep-acceso__error', role: 'alert', text: 'No se han podido cargar las fichas de prueba.' });

/* Licencias de una persona: jugador/a, técnico/a y juez-árbitro (columnas Lic.Jug, Lic.Tec y Lic.Arb del SFEP actual) */
const TIPOS_LIC = [
  { id: 'jugador', corto: 'Lic. Jug.', nombre: 'Jugador/a' },
  { id: 'tecnico', corto: 'Lic. Tec.', nombre: 'Técnico/a' },
  { id: 'arbitro', corto: 'Lic. Arb.', nombre: 'Juez-árbitro' },
];
const licenciasDe = (f) => f.licencias ?? [{ tipo: 'jugador', numero: f.licencia, estado: f.estado, temporada: f.temporada, vigenteHasta: f.vigenteHasta }];
const licDe = (f, tipo) => licenciasDe(f).find((l) => l.tipo === tipo);
/** Celda de licencia: número coloreado por estado (verde en vigor, ámbar pendiente, rojo sin renovar/baja). */
function celdaLic(l, tipo) {
  if (!l) return el('span', { class: 'sfep-lic sfep-lic--no' }, el('span', { 'aria-hidden': 'true', text: '—' }), el('span', { class: 'sr-only', text: `Sin licencia de ${tipo.nombre.toLowerCase()}` }));
  return el('span', { class: `sfep-lic sfep-lic--${slug(l.estado)}`, title: `${tipo.nombre} · ${l.estado} · temporada ${l.temporada}` },
    el('span', { text: l.numero }), el('span', { class: 'sr-only', text: ` (${l.estado})` }));
}
const leyendaLic = () => el('p', { class: 'sfep-leyenda' },
  el('span', { class: 'sfep-lic sfep-lic--activa', text: 'En vigor' }),
  el('span', { class: 'sfep-lic sfep-lic--renovada-sin-pagar', text: 'Pendiente de pago o validación' }),
  el('span', { class: 'sfep-lic sfep-lic--caducada', text: 'Caducada o baja' }));

/* ---------------------------------------------------------------------------
   Permisos de modificar y exportar.
   De momento, TODOS los usuarios (FEP y federaciones autonómicas). Más adelante se restringirá a las
   federaciones autonómicas lo que corresponda: basta con cambiar estas dos funciones.
   --------------------------------------------------------------------------- */
const puedeModificar = () => true;
const puedeExportar = () => true;
/** La vista abierta (buscador o vista previa) define cómo repintar su listado después de modificar una ficha. */
let refrescarListado = null;

/* ---------------------------------------------------------------------------
   Exportar a Excel: el listado que se está viendo (todas sus fichas, no solo la página), con las columnas elegidas
   --------------------------------------------------------------------------- */
const siNoTxt = (v) => (v ? 'Sí' : 'No');
const GRUPOS_EXPORT = [
  { id: 'ident', nombre: 'Identificación', fijo: true, cols: [
    { titulo: 'Id ficha', valor: (f) => f.idFicha ?? null },
    { titulo: 'DNI / NIE', valor: (f) => f.dni ?? '' }] },
  { id: 'persona', nombre: 'Persona', porDefecto: true, cols: [
    { titulo: 'Nombre', valor: (f) => f.nombre },
    { titulo: 'Primer apellido', valor: (f) => f.apellido1 ?? f.apellidos },
    { titulo: 'Segundo apellido', valor: (f) => f.apellido2 ?? '' },
    { titulo: 'Género', valor: (f) => (f.genero === 'F' ? 'Femenino' : 'Masculino') },
    { titulo: 'Año de nacimiento', valor: (f) => f.anioNacimiento ?? null },
    { titulo: 'Categoría', valor: (f) => f.categoria },
    { titulo: 'Menor', valor: (f) => siNoTxt(f.menor) }] },
  { id: 'licencias', nombre: 'Licencias (nº, estado y temporada)', porDefecto: true, cols: TIPOS_LIC.flatMap((t) => [
    { titulo: t.corto.replace(/\.$/, ''), valor: (f) => licDe(f, t.id)?.numero ?? '' },
    { titulo: `Estado ${t.corto.replace(/\.$/, '').toLowerCase()}`, valor: (f) => licDe(f, t.id)?.estado ?? '' },
    { titulo: `Temporada ${t.corto.replace(/\.$/, '').toLowerCase()}`, valor: (f) => licDe(f, t.id)?.temporada ?? null }]) },
  { id: 'club', nombre: 'Federación y club', porDefecto: true, cols: [
    { titulo: 'Federación', valor: (f) => f.federacion?.nombre ?? '' },
    { titulo: 'Club autonómico', valor: (f) => f.club ?? '' },
    { titulo: 'Tramitado jug.', valor: (f) => siNoTxt(f.tramitadoJug) }] },
  { id: 'residencia', nombre: 'Residencia', cols: [
    { titulo: 'Comunidad', valor: (f) => f.residencia?.comunidad?.nombre ?? '' },
    { titulo: 'Provincia', valor: (f) => f.residencia?.provincia?.nombre ?? '' },
    { titulo: 'Localidad', valor: (f) => f.residencia?.localidad ?? '' }] },
  { id: 'contacto', nombre: 'Contacto', cols: [
    { titulo: 'Teléfono / móvil', valor: (f) => f.telefono ?? '' },
    { titulo: 'Email', valor: (f) => f.email ?? '' }] },
  { id: 'condicion', nombre: 'Condición', cols: [
    { titulo: 'Español/a', valor: (f) => siNoTxt(f.espanol) },
    { titulo: 'Comunitario/a', valor: (f) => siNoTxt(f.comunitario) },
    { titulo: 'Residente', valor: (f) => siNoTxt(f.residente) },
    { titulo: 'Afiliado/a FIP', valor: (f) => siNoTxt(f.afiliadoFip) }] },
  { id: 'pago', nombre: 'Pago y seguro', cols: [
    { titulo: 'Forma de pago', valor: (f) => f.pago ?? '' },
    { titulo: 'Id TPV', valor: (f) => f.idTpv ?? '' },
    { titulo: 'Seguro', valor: (f) => f.seguro ?? '' }] },
];

function abrirExportar(lista, nombreListado, origen) {
  let cerrar = () => {};
  const casillas = GRUPOS_EXPORT.map((g) => {
    const cb = el('input', { type: 'checkbox', id: `ex-${g.id}`, checked: !!(g.fijo || g.porDefecto), disabled: !!g.fijo });
    return { g, cb, nodo: el('label', { class: 'sfep-casilla', for: cb.id }, cb,
      el('span', {}, g.nombre, el('span', { class: 'sfep-pequena', text: ` · ${g.cols.map((c) => c.titulo).join(', ')}` }))) };
  });
  const aviso = el('p', { class: 'sfep-acceso__error', role: 'alert', hidden: true });
  const hoy = new Date().toISOString().slice(0, 10);
  const archivo = `fichas-${slug(nombreListado) || 'listado'}-${hoy}.xlsx`;
  const dlg = el('dialog', { class: 'sfep-ficha sfep-exportar', 'aria-labelledby': 'ex-titulo' },
    el('header', { class: 'sfep-ficha__cab' },
      el('div', {}, el('p', { class: 'sfep-ficha__lic', text: `${nombreListado} · ${fmtNum.format(lista.length)} ${lista.length === 1 ? 'ficha' : 'fichas'}` }),
        el('h2', { id: 'ex-titulo', text: 'Exportar a Excel' })),
      el('button', { type: 'button', class: 'sfep-icono sfep-ficha__cerrar', 'aria-label': 'Cerrar sin exportar', onclick: () => cerrar() }, '×')),
    el('div', { class: 'sfep-ficha__cuerpo' },
      el('p', { text: `Se exportan las ${fmtNum.format(lista.length)} fichas del listado (todas las páginas), en el mismo orden. Elige qué columnas incluir:` }),
      el('fieldset', { class: 'sfep-casillas' }, el('legend', { text: 'Columnas' }), ...casillas.map((x) => x.nodo)),
      aviso,
      el('p', { class: 'sfep-perso__nota', text: `Archivo: ${archivo}. Se genera en tu navegador; no se envía a ningún sitio. Prototipo: datos de prueba.` })),
    el('footer', { class: 'sfep-ficha__pie' },
      el('button', { type: 'button', class: 'sfep-btn', onclick: () => {
        const cols = casillas.filter((x) => x.cb.checked).flatMap((x) => x.g.cols);
        if (!window.SFEPExcel) { aviso.textContent = 'No se ha podido cargar el exportador de Excel.'; aviso.hidden = false; return; }
        window.SFEPExcel.descargar(archivo, { hoja: 'Fichas', columnas: cols, filas: lista });
        cerrar();
        toast(`Descargado ${archivo} (${fmtNum.format(lista.length)} fichas, ${cols.length} columnas).`, { demo: false });
      } }, 'Descargar Excel'),
      el('button', { type: 'button', class: 'sfep-btn sfep-btn--sec', onclick: () => cerrar() }, 'Cancelar')));
  cerrar = S.dialogoModal(dlg, origen);
}

/** Tabla de fichas (una fila por persona, con sus licencias) y paginación. */
function tablaFichas(lista, { conFederacion, extra, nombre = 'Listado de fichas', pagina: paginaInicial = 0, alCambiarPagina } = {}) {
  let pagina = paginaInicial;
  const cuerpo = el('tbody');
  const pie = el('div', { class: 'sfep-paginas' });
  const cabeceras = ['Id ficha', 'Apellidos y nombre', 'Categoría', 'Club', ...(conFederacion ? ['Federación'] : []), ...(extra ? [extra.titulo] : []), ...TIPOS_LIC.map((t) => t.corto)];
  const pintar = () => {
    const total = Math.max(1, Math.ceil(lista.length / POR_PAGINA));
    pagina = Math.min(pagina, total - 1);
    const trozo = lista.slice(pagina * POR_PAGINA, (pagina + 1) * POR_PAGINA);
    cuerpo.replaceChildren(...trozo.map((f) => el('tr', {},
      el('td', { class: 'sfep-mono', 'data-label': 'Id ficha', text: String(f.idFicha ?? '') }),
      el('th', { scope: 'row' }, el('button', { type: 'button', class: 'sfep-enlace', onclick: (e) => abrirFicha(f, e.currentTarget) }, nombreCompleto(f))),
      el('td', { 'data-label': 'Categoría', text: f.categoria }),
      el('td', { 'data-label': 'Club', text: f.club }),
      conFederacion ? el('td', { 'data-label': 'Federación', class: 'sfep-pequena', text: f.federacion?.abreviatura ?? f.federacion?.nombre }) : null,
      extra ? el('td', { 'data-label': extra.titulo }, extra.valor(f)) : null,
      ...TIPOS_LIC.map((t) => el('td', { class: 'sfep-tabla__lic', 'data-label': t.corto }, celdaLic(licDe(f, t.id), t))))));
    pie.replaceChildren(
      el('p', { text: lista.length ? `${fmtNum.format(pagina * POR_PAGINA + 1)}–${fmtNum.format(pagina * POR_PAGINA + trozo.length)} de ${fmtNum.format(lista.length)}` : '' }),
      total > 1 ? el('div', { class: 'sfep-paginas__btns' },
        el('button', { type: 'button', class: 'sfep-btn sfep-btn--sec sfep-btn--sm', disabled: pagina === 0, onclick: () => { pagina--; alCambiarPagina?.(pagina); pintar(); } }, '← Anterior'),
        el('span', { text: `Página ${pagina + 1} de ${total}` }),
        el('button', { type: 'button', class: 'sfep-btn sfep-btn--sec sfep-btn--sm', disabled: pagina >= total - 1, onclick: () => { pagina++; alCambiarPagina?.(pagina); pintar(); } }, 'Siguiente →')) : null);
  };
  pintar();
  const barra = lista.length && puedeExportar() ? el('div', { class: 'sfep-tabla-barra' },
    el('button', { type: 'button', class: 'sfep-btn sfep-btn--sec sfep-btn--sm', onclick: (e) => abrirExportar(lista, nombre, e.currentTarget) },
      icono('informes'), `Exportar a Excel (${fmtNum.format(lista.length)})`)) : null;
  const caja = el('div', { class: 'sfep-tabla-caja' }, barra,
    lista.length ? el('div', { class: 'sfep-tabla-scroll' }, el('table', { class: 'sfep-tabla' },
      el('caption', { class: 'sr-only', text: 'Fichas' }),
      el('thead', {}, el('tr', {}, ...cabeceras.map((c) => el('th', { scope: 'col', text: c })))), cuerpo))
      : el('p', { class: 'sfep-vacio', text: 'No hay fichas que coincidan.' }),
    lista.length ? leyendaLic() : null,
    lista.length ? pie : null);
  return caja;
}

/* ---------------------------------------------------------------------------
   Ficha (diálogo)
   --------------------------------------------------------------------------- */
function abrirFicha(f, origen) {
  const mod = puedeModificar();
  const dato = (k, v) => [el('dt', { text: k }), el('dd', {}, v == null || v === '' ? '—' : v)];
  const siNo = (v) => (v ? 'Sí' : 'No');
  const fecha = (iso) => iso?.split('-').reverse().join('/');
  const bloque = (titulo, ...hijos) => el('section', { class: 'sfep-ficha__bloque' }, el('h3', { class: 'sfep-ficha__sub', text: titulo }), ...hijos);
  const id = f.idFicha ?? f.licencia;

  // Licencias: una fila por tipo (jugador/a, técnico/a, juez-árbitro), tenga o no esa licencia
  const licencias = el('ul', { class: 'sfep-licencias' }, ...TIPOS_LIC.map((t) => {
    const l = licDe(f, t.id);
    return el('li', { class: `sfep-licencias__item${l ? '' : ' is-vacia'}` },
      el('span', { class: 'sfep-licencias__tipo', text: t.nombre }),
      l ? el('span', { class: 'sfep-licencias__datos' },
        el('strong', { class: 'sfep-mono', text: `Nº ${l.numero}` }), estado(l.estado),
        el('span', { class: 'sfep-pequena', text: `Temporada ${l.temporada} · vigente hasta ${fecha(l.vigenteHasta)}` }))
        : el('span', { class: 'sfep-licencias__datos sfep-pequena', text: 'Sin licencia' }),
      mod ? (l
        ? (['activa', 'caducada'].includes(l.estado) ? el('button', { type: 'button', class: 'sfep-btn sfep-btn--sec sfep-btn--sm', onclick: () => toast(`Renovación para 2027 de la licencia de ${t.nombre.toLowerCase()} ${l.numero}.`) }, 'Renovar 2027') : null)
        : (t.id !== 'jugador' && f.menor ? null : el('button', { type: 'button', class: 'sfep-btn sfep-btn--sec sfep-btn--sm', onclick: () => toast(`Alta de licencia de ${t.nombre.toLowerCase()} para la ficha ${id}.`) }, '+ Añadir'))) : null);
  }));

  let cerrar = () => {};
  const dlg = el('dialog', { class: 'sfep-ficha', 'aria-labelledby': 'ficha-titulo' },
    el('header', { class: 'sfep-ficha__cab' },
      el('div', {},
        el('p', { class: 'sfep-ficha__lic', text: `Ficha ${id}${f.dni ? ` · DNI ${f.dni}` : ''}` }),
        el('h2', { id: 'ficha-titulo', text: `${f.nombre} ${f.apellidos}` })),
      el('button', { type: 'button', class: 'sfep-icono sfep-ficha__cerrar', 'aria-label': 'Cerrar ficha', onclick: () => cerrar() }, '×')),
    el('div', { class: 'sfep-ficha__cuerpo' },
      el('p', { class: 'sfep-ficha__etiquetas' },
        f.menor ? el('span', { class: 'sfep-etiqueta', text: 'Menor' }) : null,
        f.extranjero ? el('span', { class: 'sfep-etiqueta', text: 'Extranjero/a' }) : null,
        f.afiliadoFip ? el('span', { class: 'sfep-etiqueta', text: 'Afiliado/a FIP' }) : null,
        f.tramitadoJug ? el('span', { class: 'sfep-etiqueta sfep-etiqueta--ok', text: 'Tramitado jug.' }) : null,
        f.modificadaEn ? el('span', { class: 'sfep-etiqueta sfep-etiqueta--aviso', text: `Modificada hoy a las ${f.modificadaEn}` }) : null),
      f.transferenciaPendiente ? el('p', { class: 'sfep-ficha__aviso' },
        el('strong', { text: 'Transferencia pendiente: ' }), `${f.transferenciaPendiente.deClub} → ${f.transferenciaPendiente.aClub}`) : null,
      bloque(`Licencias (${licenciasDe(f).length})`, licencias),
      bloque('Datos personales', el('dl', { class: 'sfep-dl' },
        dato('Nombre', f.nombre), dato('Apellidos', f.apellidos), dato('DNI / NIE', f.dni),
        dato('Género', f.genero === 'F' ? 'Femenino' : 'Masculino'), dato('Año de nacimiento', f.anioNacimiento ? String(f.anioNacimiento) : null),
        dato('Categoría', f.categoria), dato('Menor', siNo(f.menor)), dato('Español/a', siNo(f.espanol)),
        dato('Comunitario/a', siNo(f.comunitario)), dato('Residente', siNo(f.residente)), dato('Afiliado/a FIP', siNo(f.afiliadoFip)))),
      bloque('Residencia y contacto', el('dl', { class: 'sfep-dl' },
        dato('Comunidad', f.residencia?.comunidad?.nombre), dato('Provincia', f.residencia?.provincia?.nombre),
        dato('Localidad', f.residencia?.localidad), dato('Teléfono / móvil', f.telefono), dato('Email', f.email))),
      bloque('Federación y pago', el('dl', { class: 'sfep-dl' },
        dato('Federación', f.federacion?.nombre), dato('Club autonómico', f.club),
        dato('Tramitado jug.', siNo(f.tramitadoJug)), dato('Seguro', f.seguro),
        dato('Forma de pago', f.pago), dato('Id TPV', f.idTpv))),
      bloque('Temporadas con licencia',
        el('ul', { class: 'sfep-temporadas' }, ...(f.temporadas ?? []).map((t) => el('li', { text: String(t) }))))),
    el('footer', { class: 'sfep-ficha__pie' },
      mod ? el('button', { type: 'button', class: 'sfep-btn', onclick: () => { cerrar(); editarFicha(f, origen); } }, icono('ajustes'), 'Modificar datos') : null,
      el('button', { type: 'button', class: 'sfep-btn sfep-btn--sec', onclick: () => toast(`Impresión de las licencias de la ficha ${id}.`) }, 'Imprimir licencias'),
      mod && f.estado !== 'baja' ? el('button', { type: 'button', class: 'sfep-btn sfep-btn--sec sfep-btn--peligro', onclick: () => toast(`Baja de la ficha ${id}.`) }, 'Dar de baja') : null));
  cerrar = S.dialogoModal(dlg, origen);
}

/* ---------------------------------------------------------------------------
   Modificar datos de una ficha
   PROTOTIPO: el cambio se aplica a los datos de prueba cargados en esta página (se ve en listados, ficha y
   exportación) y se pierde al recargar. No se guarda en ninguna base de datos.
   --------------------------------------------------------------------------- */
const ESTADOS_LIC = ['activa', 'renovada sin pagar', 'pendiente de validar', 'caducada', 'baja'];
const mayus = (t) => String(t ?? '').trim().replace(/\s+/g, ' ').toLocaleUpperCase('es');
const capital = (t) => t[0].toUpperCase() + t.slice(1);
/** Categoría por edad al cierre de la temporada (como el generador de datos y el buscador). */
function categoriaDe(anio, temporada) {
  const e = temporada - anio;
  if (e < 12) return 'Benjamín'; if (e < 14) return 'Alevín'; if (e < 16) return 'Infantil';
  if (e < 18) return 'Cadete'; if (e < 19) return 'Júnior'; if (e < 35) return 'Absoluta';
  if (e < 40) return '+35'; if (e < 45) return '+40'; if (e < 50) return '+45'; if (e < 55) return '+50';
  return '+55';
}

async function editarFicha(f, origen) {
  let d;
  try { d = await datos(); } catch { toast('No se han podido cargar los datos de prueba.', { demo: false }); return; }
  const todas = S.comoLista(d);
  const feds = S.comoLista(d.federaciones ?? []);
  const coms = S.comoLista(d.comunidades ?? []);
  const temporadaActual = Number(d.temporada) || 2026;
  const id = f.idFicha ?? f.licencia;

  const campo = (ctl, etiqueta, { ancho = false, ayuda } = {}) => el('div', { class: `sfep-campo${ancho ? ' sfep-campo--ancho' : ''}` },
    el('label', { for: ctl.id, text: etiqueta }), ctl, ayuda ? el('p', { class: 'sfep-campo__ayuda', id: `${ctl.id}-ayuda`, text: ayuda }) : null);
  const input = (cid, valor, extra = {}) => el('input', { type: 'text', id: cid, value: valor ?? '', autocomplete: 'off', ...extra });
  const select = (cid, opciones, valor) => {
    const s = el('select', { id: cid }, ...opciones.map(([v, t]) => el('option', { value: v, text: t })));
    s.value = valor ?? '';
    return s;
  };
  const SN = [['S', 'Sí'], ['N', 'No']];
  const sn = (v) => (v ? 'S' : 'N');
  const [ap1Ini, ...ap2Ini] = String(f.apellidos ?? '').split(' ');

  const c = {
    nombre: input('ed-nombre', f.nombre, { maxlength: 100, required: true }),
    apellido1: input('ed-ap1', f.apellido1 ?? ap1Ini, { maxlength: 100, required: true }),
    apellido2: input('ed-ap2', f.apellido2 ?? ap2Ini.join(' '), { maxlength: 100 }),
    dni: input('ed-dni', f.dni, { maxlength: 12, 'aria-describedby': 'ed-dni-ayuda' }),
    genero: select('ed-gen', [['M', 'Masculino'], ['F', 'Femenino']], f.genero),
    anio: input('ed-anio', f.anioNacimiento, { inputmode: 'numeric', maxlength: 4, required: true, 'aria-describedby': 'ed-anio-ayuda' }),
    espanol: select('ed-esp', SN, sn(f.espanol)), comunitario: select('ed-ue', SN, sn(f.comunitario)),
    residente: select('ed-res', SN, sn(f.residente)), afiliadoFip: select('ed-fip', SN, sn(f.afiliadoFip)),
    comunidad: select('ed-com', [['', '—'], ...coms.map((x) => [String(x.id), x.nombre])], String(f.residencia?.comunidad?.id ?? '')),
    provincia: el('select', { id: 'ed-prov' }),
    localidad: input('ed-loc', f.residencia?.localidad, { maxlength: 100 }),
    telefono: input('ed-tel', f.telefono, { type: 'tel', inputmode: 'tel', maxlength: 15 }),
    email: input('ed-email', f.email, { type: 'email', inputmode: 'email', maxlength: 200 }),
    federacion: select('ed-fed', feds.map((x) => [String(x.id), x.abreviatura ? `${x.abreviatura} · ${x.nombre}` : x.nombre]), String(f.federacion?.id ?? '')),
    club: el('select', { id: 'ed-club' }),
    tramitado: select('ed-tram', SN, sn(f.tramitadoJug)),
    pago: select('ed-pago', [['tarjeta', 'Tarjeta'], ['domiciliación', 'Domiciliación'], ['transferencia', 'Transferencia']], f.pago),
    idTpv: input('ed-tpv', f.idTpv, { inputmode: 'numeric', maxlength: 5 }),
    seguro: select('ed-seguro', [['Seguro deportivo incluido', 'Seguro deportivo incluido'], ['Sin seguro', 'Sin seguro']], f.seguro),
  };
  const anioAyuda = el('p', { class: 'sfep-campo__ayuda', id: 'ed-anio-ayuda' });
  const pintarCategoria = () => {
    const a = Number(c.anio.value);
    anioAyuda.textContent = /^\d{4}$/.test(c.anio.value) ? `Categoría: ${categoriaDe(a, temporadaActual)}${temporadaActual - a < 18 ? ' · menor' : ''}` : 'Año con 4 cifras.';
  };
  c.anio.addEventListener('input', pintarCategoria);
  pintarCategoria();

  // Combos dependientes: comunidad -> provincia; federación -> club
  const pintarProvincias = (valor) => {
    const com = coms.find((x) => String(x.id) === c.comunidad.value);
    const provs = com ? S.comoLista(com.provincias) : [];
    c.provincia.replaceChildren(el('option', { value: '', text: com ? '—' : 'Elige antes la comunidad' }), ...provs.map((p) => el('option', { value: String(p.id), text: p.nombre })));
    c.provincia.value = provs.some((p) => String(p.id) === String(valor)) ? String(valor) : '';
  };
  const pintarClubes = (valor) => {
    const clubes = [...new Set(todas.filter((x) => String(x.federacion?.id) === c.federacion.value).map((x) => x.club))].sort((a, b) => a.localeCompare(b, 'es'));
    if (valor && !clubes.includes(valor)) clubes.unshift(valor);
    c.club.replaceChildren(...clubes.map((x) => el('option', { value: x, text: x })));
    c.club.value = clubes.includes(valor) ? valor : (clubes[0] ?? '');
  };
  c.comunidad.addEventListener('change', () => pintarProvincias(''));
  c.federacion.addEventListener('change', () => pintarClubes(''));
  pintarProvincias(f.residencia?.provincia?.id);
  pintarClubes(f.club);

  // Licencias: jugador/a, técnico/a y juez-árbitro (cada una opcional; al menos una)
  const lic = TIPOS_LIC.map((t) => {
    const l = licDe(f, t.id);
    const tiene = el('input', { type: 'checkbox', id: `ed-l-${t.id}`, checked: !!l });
    const numero = input(`ed-ln-${t.id}`, l?.numero, { inputmode: 'numeric', maxlength: 10 });
    const estadoL = select(`ed-le-${t.id}`, ESTADOS_LIC.map((x) => [x, capital(x)]), l?.estado ?? 'pendiente de validar');
    const temporada = input(`ed-lt-${t.id}`, l?.temporada ?? temporadaActual, { inputmode: 'numeric', maxlength: 4 });
    const sincronizar = () => { for (const x of [numero, estadoL, temporada]) x.disabled = !tiene.checked; };
    tiene.addEventListener('change', () => { sincronizar(); if (tiene.checked) numero.focus(); });
    sincronizar();
    return { t, tiene, numero, estadoL, temporada,
      nodo: el('div', { class: 'sfep-edlic' },
        el('label', { class: 'sfep-casilla sfep-edlic__tipo', for: tiene.id }, tiene, el('span', { text: t.nombre })),
        campo(numero, 'Nº de licencia'), campo(estadoL, 'Estado'), campo(temporada, 'Temporada')) };
  });

  const errores = el('div', { class: 'sfep-ficha__aviso sfep-errores', role: 'alert', tabindex: '-1', hidden: true });
  const grupo = (titulo, ...hijos) => el('fieldset', { class: 'sfep-filtros__grupo' }, el('legend', { text: titulo }), el('div', { class: 'sfep-filtros__grid' }, ...hijos));

  /** Devuelve [{ ctl, msg }] con lo que falta o está mal. */
  function validar() {
    const e = [];
    const req = (ctl, msg) => { if (!ctl.value.trim()) e.push({ ctl, msg }); };
    req(c.nombre, 'Escribe el nombre.');
    req(c.apellido1, 'Escribe el primer apellido.');
    const dni = c.dni.value.trim().toUpperCase().replace(/[\s-]/g, '');
    if (dni && !/^(\d{8}[A-Z]|[XYZ]\d{7}[A-Z])$/.test(dni)) e.push({ ctl: c.dni, msg: 'El DNI/NIE no tiene un formato válido (8 cifras y letra, o X/Y/Z, 7 cifras y letra).' });
    const anio = Number(c.anio.value);
    if (!/^\d{4}$/.test(c.anio.value.trim()) || anio < 1900 || anio > temporadaActual) e.push({ ctl: c.anio, msg: `El año de nacimiento debe estar entre 1900 y ${temporadaActual}.` });
    const tel = c.telefono.value.replace(/\D/g, '');
    if (c.telefono.value.trim() && (tel.length < 9 || tel.length > 15)) e.push({ ctl: c.telefono, msg: 'El teléfono debe tener entre 9 y 15 cifras.' });
    if (c.email.value.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(c.email.value.trim())) e.push({ ctl: c.email, msg: 'El email no tiene un formato válido.' });
    if (c.idTpv.value.trim() && !/^\d{1,5}$/.test(c.idTpv.value.trim())) e.push({ ctl: c.idTpv, msg: 'El Id TPV son hasta 5 cifras.' });
    if (!c.club.value) e.push({ ctl: c.club, msg: 'Elige el club autonómico.' });
    const conLic = lic.filter((x) => x.tiene.checked);
    if (!conLic.length) e.push({ ctl: lic[0].tiene, msg: 'La ficha debe tener al menos una licencia (jugador/a, técnico/a o juez-árbitro).' });
    for (const x of conLic) {
      const num = x.numero.value.trim();
      if (!/^\d{1,10}$/.test(num)) e.push({ ctl: x.numero, msg: `Nº de licencia de ${x.t.nombre.toLowerCase()}: solo cifras.` });
      else if (todas.some((o) => o !== f && licDe(o, x.t.id)?.numero === num)) e.push({ ctl: x.numero, msg: `El nº ${num} ya es la licencia de ${x.t.nombre.toLowerCase()} de otra ficha.` });
      const tp = Number(x.temporada.value);
      if (!/^\d{4}$/.test(x.temporada.value.trim()) || tp < 2000 || tp > temporadaActual + 1) e.push({ ctl: x.temporada, msg: `Temporada de la licencia de ${x.t.nombre.toLowerCase()}: entre 2000 y ${temporadaActual + 1}.` });
    }
    return e;
  }

  function guardar() {
    for (const x of dlg.querySelectorAll('[aria-invalid]')) x.removeAttribute('aria-invalid');
    const e = validar();
    if (e.length) {
      e.forEach(({ ctl }) => ctl.setAttribute('aria-invalid', 'true'));
      errores.replaceChildren(el('strong', { text: `Revisa ${e.length === 1 ? 'este dato' : `estos ${e.length} datos`}:` }),
        el('ul', {}, ...e.map(({ ctl, msg }) => el('li', {}, el('a', { href: `#${ctl.id}`, onclick: (ev) => { ev.preventDefault(); ctl.focus(); } }, msg)))));
      errores.hidden = false;
      errores.focus();
      return;
    }
    const anio = Number(c.anio.value);
    const com = coms.find((x) => String(x.id) === c.comunidad.value);
    const prov = com ? S.comoLista(com.provincias).find((p) => String(p.id) === c.provincia.value) : null;
    const fed = feds.find((x) => String(x.id) === c.federacion.value);
    const ap1 = mayus(c.apellido1.value); const ap2 = mayus(c.apellido2.value);
    const espanol = c.espanol.value === 'S';
    Object.assign(f, {
      nombre: mayus(c.nombre.value), apellido1: ap1, apellido2: ap2, apellidos: `${ap1} ${ap2}`.trim(),
      dni: c.dni.value.trim().toUpperCase().replace(/[\s-]/g, '') || null,
      genero: c.genero.value, anioNacimiento: anio, categoria: categoriaDe(anio, temporadaActual), menor: temporadaActual - anio < 18,
      espanol, extranjero: !espanol, comunitario: c.comunitario.value === 'S', residente: c.residente.value === 'S', afiliadoFip: c.afiliadoFip.value === 'S',
      residencia: { comunidad: com ? { id: com.id, nombre: com.nombre } : null, provincia: prov ? { id: prov.id, nombre: prov.nombre } : null, localidad: c.localidad.value.trim() },
      telefono: c.telefono.value.trim(), email: c.email.value.trim().toLowerCase(),
      federacion: fed ? { id: fed.id, nombre: fed.nombre, abreviatura: fed.abreviatura } : f.federacion,
      club: c.club.value, tramitadoJug: c.tramitado.value === 'S',
      pago: c.pago.value, idTpv: c.idTpv.value.trim() || null, seguro: c.seguro.value,
      licencias: lic.filter((x) => x.tiene.checked).map((x) => ({
        tipo: x.t.id, numero: x.numero.value.trim(), estado: x.estadoL.value,
        temporada: Number(x.temporada.value), vigenteHasta: `${x.temporada.value.trim()}-12-31` })),
      modificadaEn: new Intl.DateTimeFormat('es-ES', { hour: '2-digit', minute: '2-digit' }).format(new Date()),
    });
    // Datos "principales" (primera licencia), que usan los resúmenes y apartados del módulo
    const p = f.licencias[0];
    Object.assign(f, { licencia: p.numero, tipo: p.tipo, estado: p.estado, temporada: p.temporada, vigenteHasta: p.vigenteHasta });
    if (Array.isArray(f.temporadas) && !f.temporadas.includes(p.temporada) && p.temporada > Math.max(...f.temporadas)) f.temporadas = [...f.temporadas, p.temporada];
    cerrar();
    refrescarListado?.();
    toast(`Ficha ${id} modificada. Prototipo: el cambio se ve en los listados y en la exportación, pero no se guarda en la base de datos y se pierde al recargar.`, { demo: false });
    abrirFicha(f, origen);
  }

  let cerrar = () => {};
  const dlg = el('dialog', { class: 'sfep-ficha sfep-editar', 'aria-labelledby': 'ed-titulo' },
    el('header', { class: 'sfep-ficha__cab' },
      el('div', {}, el('p', { class: 'sfep-ficha__lic', text: `Ficha ${id}` }), el('h2', { id: 'ed-titulo', text: `Modificar: ${f.nombre} ${f.apellidos}` })),
      el('button', { type: 'button', class: 'sfep-icono sfep-ficha__cerrar', 'aria-label': 'Cerrar sin guardar', onclick: () => cerrar() }, '×')),
    el('form', { class: 'sfep-ficha__cuerpo sfep-editar__form', novalidate: true, onsubmit: (ev) => { ev.preventDefault(); guardar(); } },
      errores,
      grupo('Datos personales', campo(c.nombre, 'Nombre *'), campo(c.apellido1, 'Primer apellido *'), campo(c.apellido2, 'Segundo apellido'),
        campo(c.dni, 'DNI / NIE', { ayuda: 'Ej.: 99123456A' }), campo(c.genero, 'Género'),
        el('div', { class: 'sfep-campo' }, el('label', { for: 'ed-anio', text: 'Año de nacimiento *' }), c.anio, anioAyuda)),
      el('fieldset', { class: 'sfep-filtros__grupo' }, el('legend', { text: 'Licencias (al menos una)' }), el('div', { class: 'sfep-edlic__lista' }, ...lic.map((x) => x.nodo))),
      grupo('Federación y club', campo(c.federacion, 'Federación de la licencia'), campo(c.club, 'Club autonómico', { ancho: true }), campo(c.tramitado, 'Tramitado jug.')),
      grupo('Residencia', campo(c.comunidad, 'Comunidad'), campo(c.provincia, 'Provincia'), campo(c.localidad, 'Localidad')),
      grupo('Contacto', campo(c.telefono, 'Teléfono / móvil'), campo(c.email, 'Email', { ancho: true })),
      grupo('Condición', campo(c.espanol, 'Español/a'), campo(c.comunitario, 'Comunitario/a'), campo(c.residente, 'Residente'), campo(c.afiliadoFip, 'Afiliado/a FIP')),
      grupo('Pago y seguro', campo(c.pago, 'Forma de pago'), campo(c.idTpv, 'Id TPV'), campo(c.seguro, 'Seguro')),
      el('p', { class: 'sfep-perso__nota', text: 'Prototipo con datos de prueba: el cambio se aplica en esta página y se pierde al recargar. No se guarda en la base de datos.' }),
      el('button', { type: 'submit', hidden: true, tabindex: '-1', 'aria-hidden': 'true' })),
    el('footer', { class: 'sfep-ficha__pie' },
      el('button', { type: 'button', class: 'sfep-btn', onclick: guardar }, 'Guardar cambios'),
      el('button', { type: 'button', class: 'sfep-btn sfep-btn--sec', onclick: () => cerrar() }, 'Cancelar')));
  cerrar = S.dialogoModal(dlg, origen);
  c.nombre.focus();
}

/* ---------------------------------------------------------------------------
   Vistas
   --------------------------------------------------------------------------- */
function portada() {
  const resumen = el('ul', { class: 'sfep-kpis', 'aria-label': 'Resumen de fichas' }, el('li', {}, cargando()));
  datos().then((d) => {
    const lista = delUsuario(d);
    const n = (fn) => lista.filter(fn).length;
    const cifras = [
      [fmtNum.format(n((f) => f.estado === 'activa')), 'Fichas activas', 'buscar'],
      [String(n(APARTADOS.transferencias.filtro)), 'Transferencias recibidas', 'transferencias'],
      [String(n(APARTADOS.domiciliaciones.filtro)), 'Domiciliaciones recibidas', 'domiciliaciones'],
      [String(n(APARTADOS.sinpagar.filtro)), 'Renovados sin pagar', 'sinpagar'],
    ];
    resumen.replaceChildren(...cifras.map(([v, t, id]) => {
      const dentro = [el('span', { class: 'sfep-kpi__etiqueta', text: t }), el('strong', { class: 'sfep-kpi__valor', text: v })];
      return el('li', {}, puede(id) ? el('a', { class: 'sfep-kpi', href: `#m/fichas/${id}` }, ...dentro) : el('div', { class: 'sfep-kpi' }, ...dentro));
    }));
  }).catch(() => resumen.replaceChildren(el('li', {}, errorCarga())));

  const q = el('input', { type: 'search', id: 'fichas-rapida', placeholder: 'Nombre, apellidos o nº de licencia', autocomplete: 'off' });
  const rapida = el('form', { class: 'sfep-rapida', role: 'search', onsubmit: (e) => {
    e.preventDefault();
    location.hash = `m/fichas/buscar${q.value.trim() ? `?q=${encodeURIComponent(q.value.trim())}` : ''}`;
  } },
  el('label', { for: 'fichas-rapida', text: 'Búsqueda rápida' }),
  el('div', { class: 'sfep-rapida__fila' }, q, el('button', { type: 'submit', class: 'sfep-btn' }, 'Buscar')));

  const grupos = GRUPOS.map((g) => ({ ...g, ids: g.ids.filter(puede) })).filter((g) => g.ids.length).map((g) => el('section', { class: 'sfep-area' },
    el('h2', { class: 'sfep-area__titulo', text: g.nombre }),
    el('ul', { class: 'sfep-tiles' }, ...g.ids.map((id) => {
      const a = APARTADOS[id];
      return el('li', {}, el('a', { class: 'sfep-tile', href: `#m/fichas/${id}` }, icono(a.icono, 'sfep-tile__ico'),
        el('span', { class: 'sfep-tile__nombre', text: a.nombre }),
        el('span', { class: 'sfep-tile__desc', text: a.desc }),
        a.migrado ? null : el('span', { class: 'sfep-tile__chip', text: 'Pendiente' })));
    }))));

  return [migas(), cabecera('Fichas jugadores/as', 'Gestión de fichas de la base de datos FEP: licencias, altas, renovaciones, transferencias y censo.', 'fichas', true),
    resumen, rapida, el('div', { class: 'sfep-menu' }, ...grupos)];
}

/**
 * Buscar fichas (Gestion/Fichas.asp): los mismos criterios que el SFEP actual, combinables entre sí.
 * Como en Fichas.asp, hace falta al menos un criterio; los textos se buscan por coincidencia parcial (sin *),
 * sin distinguir mayúsculas ni tildes. El nº de licencia busca en las licencias de jugador/a, técnico/a y juez-árbitro.
 */
function buscar(params) {
  const u = S.usuario();
  const esFep = u.federacion?.id === 0;
  const campo = (id, etiqueta, control, ancho = false) => el('div', { class: `sfep-campo${ancho ? ' sfep-campo--ancho' : ''}` }, el('label', { for: id, text: etiqueta }), control);
  const texto = (id, extra = {}) => el('input', { type: 'text', id, autocomplete: 'off', ...extra });
  const sel = (id, opts, todos = 'Todos') => el('select', { id }, el('option', { value: '', text: todos }), ...opts.map(([v, t]) => el('option', { value: v, text: t })));
  const SI_NO = [['S', 'Sí'], ['N', 'No']];
  const CATEGORIAS = ['Benjamín', 'Alevín', 'Infantil', 'Cadete', 'Júnior', 'Absoluta', '+35', '+40', '+45', '+50', '+55'];
  const ESTADOS = ['activa', 'renovada sin pagar', 'pendiente de validar', 'caducada', 'baja'];

  // Controles, en los bloques de Fichas.asp
  const c = {
    texto: el('input', { type: 'search', id: 'fb-texto', placeholder: 'Nombre, apellidos, DNI o nº de licencia', autocomplete: 'off', value: params?.get('q') ?? '' }),
    dni: texto('fb-dni', { maxlength: 12 }), numlicencia: texto('fb-lic', { inputmode: 'numeric', maxlength: 10 }),
    idficha: texto('fb-idficha', { inputmode: 'numeric', maxlength: 10 }), tpv: texto('fb-tpv', { inputmode: 'numeric', maxlength: 5 }),
    nombre: texto('fb-nombre'), apellido1: texto('fb-ap1'), apellido2: texto('fb-ap2'),
    genero: sel('fb-gen', [['M', 'Masculino'], ['F', 'Femenino']]),
    categoria: sel('fb-cat', CATEGORIAS.map((x) => [x, x]), 'Todas'),
    menor: sel('fb-menor', SI_NO),
    tipoLic: sel('fb-tipolic', TIPOS_LIC.map((t) => [t.id, t.nombre])),
    estado: sel('fb-estado', ESTADOS.map((x) => [x, x[0].toUpperCase() + x.slice(1)])),
    tramitado: sel('fb-tram', [['S', 'Sí']], '--'),
    federacion: el('select', { id: 'fb-fed' }),
    club: el('select', { id: 'fb-club' }),
    clubFiltro: el('input', { type: 'search', id: 'fb-clubfiltro', placeholder: 'Filtrar clubes…', autocomplete: 'off' }),
    comunidad: el('select', { id: 'fb-com' }), provincia: el('select', { id: 'fb-prov' }), localidad: texto('fb-loc'),
    telefono: texto('fb-tel', { inputmode: 'tel', maxlength: 15 }), email: texto('fb-email', { inputmode: 'email', maxlength: 200 }),
    espanol: sel('fb-esp', SI_NO), comunitario: sel('fb-com-ue', SI_NO), residente: sel('fb-res', SI_NO), afiliadoFip: sel('fb-fip', SI_NO),
  };
  const resultados = el('div', { class: 'sfep-resultados' });
  const cuenta = el('p', { class: 'sfep-cuenta', role: 'status' });
  const aviso = el('p', { class: 'sfep-acceso__error', role: 'alert', hidden: true });
  let lista = [];
  let datosCarga = null;

  // Combos dependientes: federación -> club (con filtro de texto) y comunidad -> provincia
  const pintarClubes = () => {
    const q = normaliza(c.clubFiltro.value.trim());
    const clubes = [...new Set(lista.filter((f) => !c.federacion.value || String(f.federacion?.id) === c.federacion.value).map((f) => f.club))]
      .filter((x) => !q || normaliza(x).includes(q)).sort((a, b) => a.localeCompare(b, 'es'));
    const actual = c.club.value;
    c.club.replaceChildren(el('option', { value: '', text: clubes.length ? 'Todos los clubes' : 'Ningún club coincide' }), ...clubes.map((x) => el('option', { value: x, text: x })));
    c.club.value = clubes.includes(actual) ? actual : '';
  };
  const pintarProvincias = () => {
    const com = S.comoLista(datosCarga?.comunidades ?? []).find((x) => String(x.id) === c.comunidad.value);
    const provs = com ? S.comoLista(com.provincias) : S.comoLista(datosCarga?.comunidades ?? []).flatMap((x) => S.comoLista(x.provincias));
    c.provincia.replaceChildren(el('option', { value: '', text: 'Todas' }), ...provs.map((p) => el('option', { value: String(p.id), text: p.nombre })));
  };

  /** Criterios rellenos: [clave, valor]. */
  const criterios = () => Object.entries(c).filter(([k, ctl]) => k !== 'clubFiltro' && ctl.value.trim() !== '').map(([k, ctl]) => [k, ctl.value.trim()]);
  const contiene = (valor, q) => normaliza(valor ?? '').includes(normaliza(q));
  const digitos = (t) => String(t ?? '').replace(/\D/g, '');
  const cumple = (f, [k, v]) => {
    switch (k) {
      case 'texto': return contiene(`${f.nombre} ${f.apellidos} ${f.dni ?? ''} ${licenciasDe(f).map((l) => l.numero).join(' ')} ${f.idFicha ?? ''}`, v);
      case 'dni': return contiene(f.dni, v);
      case 'numlicencia': return licenciasDe(f).some((l) => l.numero.includes(digitos(v)));
      case 'idficha': return String(f.idFicha ?? '').includes(digitos(v));
      case 'tpv': return String(f.idTpv ?? '').includes(digitos(v));
      case 'nombre': return contiene(f.nombre, v);
      case 'apellido1': return contiene(f.apellido1 ?? f.apellidos, v);
      case 'apellido2': return contiene(f.apellido2 ?? f.apellidos, v);
      case 'genero': return f.genero === v;
      case 'categoria': return f.categoria === v;
      case 'menor': return !!f.menor === (v === 'S');
      case 'tipoLic': return !!licDe(f, v);
      case 'estado': return c.tipoLic.value ? licDe(f, c.tipoLic.value)?.estado === v : licenciasDe(f).some((l) => l.estado === v);
      case 'tramitado': return !!f.tramitadoJug;
      case 'federacion': return String(f.federacion?.id) === v;
      case 'club': return f.club === v;
      case 'comunidad': return String(f.residencia?.comunidad?.id) === v;
      case 'provincia': return String(f.residencia?.provincia?.id) === v;
      case 'localidad': return contiene(f.residencia?.localidad, v);
      case 'telefono': return digitos(f.telefono).includes(digitos(v));
      case 'email': return contiene(f.email, v);
      case 'espanol': case 'comunitario': case 'residente': case 'afiliadoFip': return !!f[k] === (v === 'S');
      default: return true;
    }
  };

  let paginaActual = 0;
  const filtrar = ({ desdeBoton = false, mantenerPagina = false } = {}) => {
    const crit = criterios();
    aviso.hidden = true;
    if (!mantenerPagina) paginaActual = 0;
    if (!crit.length) {
      if (desdeBoton) { aviso.textContent = 'Indica al menos un criterio de búsqueda.'; aviso.hidden = false; c.texto.focus(); }
      cuenta.textContent = '';
      resultados.replaceChildren(el('p', { class: 'sfep-vacio', text: 'Rellena uno o varios criterios y pulsa Buscar. Se pueden combinar y no hace falta poner *.' }));
      return;
    }
    const filtradas = lista.filter((f) => crit.every((cr) => cumple(f, cr))).sort((a, b) => nombreCompleto(a).localeCompare(nombreCompleto(b), 'es'));
    cuenta.textContent = `${fmtNum.format(filtradas.length)} ${filtradas.length === 1 ? 'ficha' : 'fichas'} · ${crit.length} ${crit.length === 1 ? 'criterio' : 'criterios'}`;
    resultados.replaceChildren(tablaFichas(filtradas, {
      conFederacion: esFep && !c.federacion.value, nombre: 'Búsqueda de fichas',
      pagina: paginaActual, alCambiarPagina: (p) => { paginaActual = p; } }));
  };
  // Tras modificar una ficha, se repite la búsqueda (con los datos nuevos) sin cambiar de página
  refrescarListado = () => { if (resultados.isConnected) filtrar({ mantenerPagina: true }); };

  const grupo = (titulo, clase, ...campos) => el('fieldset', { class: `sfep-filtros__grupo ${clase}` }, el('legend', { text: titulo }), el('div', { class: 'sfep-filtros__grid' }, ...campos));
  const masCriterios = el('details', { class: 'sfep-filtros__mas' },
    el('summary', { text: 'Más criterios: residencia, contacto y condición' }),
    grupo('Residencia', '', campo('fb-com', 'Comunidad', c.comunidad), campo('fb-prov', 'Provincia', c.provincia), campo('fb-loc', 'Localidad', c.localidad, true)),
    grupo('Contacto', '', campo('fb-tel', 'Teléfono / móvil', c.telefono), campo('fb-email', 'Email', c.email, true)),
    grupo('Condición', '', campo('fb-esp', 'Español/a', c.espanol), campo('fb-com-ue', 'Comunitario/a', c.comunitario), campo('fb-res', 'Residente', c.residente), campo('fb-fip', 'Afiliado/a FIP', c.afiliadoFip)));

  const form = el('form', { class: 'sfep-filtros', role: 'search', 'aria-label': 'Criterios de búsqueda', onsubmit: (e) => { e.preventDefault(); filtrar({ desdeBoton: true }); } },
    el('div', { class: 'sfep-campo sfep-filtros__rapida' }, el('label', { for: 'fb-texto', text: 'Búsqueda rápida' }), c.texto),
    grupo('Identificación', '', campo('fb-dni', 'DNI / NIE', c.dni), campo('fb-lic', 'Nº de licencia', c.numlicencia), campo('fb-idficha', 'Id ficha', c.idficha), campo('fb-tpv', 'Id TPV', c.tpv)),
    grupo('Persona', '', campo('fb-nombre', 'Nombre', c.nombre), campo('fb-ap1', 'Primer apellido', c.apellido1), campo('fb-ap2', 'Segundo apellido', c.apellido2),
      campo('fb-gen', 'Género', c.genero), campo('fb-cat', 'Categoría', c.categoria), campo('fb-menor', 'Menor', c.menor)),
    grupo('Licencia y club', '',
      campo('fb-tipolic', 'Tipo de licencia', c.tipoLic), campo('fb-estado', 'Estado de la licencia', c.estado), campo('fb-tram', 'Tramitado jug.', c.tramitado),
      esFep ? campo('fb-fed', 'Federación de la licencia', c.federacion) : null,
      el('div', { class: 'sfep-campo sfep-campo--ancho' }, el('label', { for: 'fb-club', text: 'Club autonómico' }),
        el('div', { class: 'sfep-club-filtro' }, c.club, el('label', { class: 'sr-only', for: 'fb-clubfiltro', text: 'Filtrar la lista de clubes' }), c.clubFiltro))),
    masCriterios,
    aviso,
    el('div', { class: 'sfep-filtros__acciones' },
      el('button', { type: 'submit', class: 'sfep-btn' }, 'Buscar fichas'),
      el('button', { type: 'reset', class: 'sfep-btn sfep-btn--sec', onclick: () => setTimeout(() => { pintarClubes(); pintarProvincias(); filtrar(); c.texto.focus(); }) }, 'Limpiar')));

  c.federacion.addEventListener('change', pintarClubes);
  c.clubFiltro.addEventListener('input', pintarClubes);
  c.comunidad.addEventListener('change', pintarProvincias);

  resultados.append(cargando());
  datos().then((d) => {
    datosCarga = d;
    lista = delUsuario(d);
    c.federacion.replaceChildren(el('option', { value: '', text: 'Todas' }), ...S.comoLista(d.federaciones ?? []).map((f) => el('option', { value: String(f.id), text: f.abreviatura ? `${f.abreviatura} · ${f.nombre}` : f.nombre })));
    c.comunidad.replaceChildren(el('option', { value: '', text: 'Todas' }), ...S.comoLista(d.comunidades ?? []).map((x) => el('option', { value: String(x.id), text: x.nombre })));
    pintarClubes();
    pintarProvincias();
    filtrar();
  }).catch(() => resultados.replaceChildren(errorCarga()));

  return [migas('Buscar fichas'),
    el('div', { class: 'sfep-modcab' }, icono('fichas', 'sfep-modcab__ico'),
      el('div', {}, el('h1', { class: 'sfep-h1', id: 'sfep-titulo', tabindex: '-1', text: 'Búsqueda de fichas de jugadores/as' }),
        el('p', { class: 'sfep-modcab__desc', text: `${esFep ? 'Todas las federaciones' : u.federacion?.nombre} · datos de prueba. Cada ficha es una persona, con su licencia de jugador/a, técnico/a y/o juez-árbitro.` })),
      puede('nueva') ? el('a', { class: 'sfep-btn sfep-btn--rojo sfep-modcab__accion', href: '#m/fichas/nueva' }, '+ Nueva ficha') : null),
    form, el('div', { class: 'sfep-panel' }, cuenta, resultados)];
}

function apartado(id) {
  const a = APARTADOS[id];
  if (!a || !puede(id)) {
    return [migas(a?.nombre ?? 'No encontrado'), cabecera(a?.nombre ?? 'Apartado no encontrado', a ? 'Tu usuario no tiene permiso para este apartado.' : 'Este apartado no existe.', 'candado'),
      el('p', {}, el('a', { class: 'sfep-btn', href: '#m/fichas' }, 'Volver a Fichas'))];
  }
  const vista = [migas(a.nombre), cabecera(a.nombre, a.desc, a.icono),
    el('div', { class: 'sfep-dos' },
      el('section', { class: 'sfep-panel', 'aria-labelledby': 'ap-incluye' },
        el('header', { class: 'sfep-panel__cab' }, el('h2', { id: 'ap-incluye', text: 'Qué incluirá' })),
        el('ul', { class: 'sfep-check' }, ...a.funciones.map((t) => el('li', { text: t })))),
      el('section', { class: 'sfep-panel sfep-pendiente', 'aria-labelledby': 'ap-estado' },
        el('header', { class: 'sfep-panel__cab' }, el('h2', { id: 'ap-estado', text: 'Estado' })),
        el('p', { class: 'sfep-pendiente__chip', text: 'Pendiente de migrar' }),
        el('p', {}, 'Sustituirá a ', el('code', { text: `Gestion/${a.legado}` }), '. Envía esa página para construirla con datos de prueba.')))];
  if (a.filtro) {
    const caja = el('div', {}, cargando());
    const titulo = el('h2', { id: 'ap-previa', text: 'Vista previa con datos de prueba' });
    vista.push(el('section', { class: 'sfep-panel', 'aria-labelledby': 'ap-previa' }, el('header', { class: 'sfep-panel__cab' }, titulo), caja));
    let paginaActual = 0;
    const pintarPrevia = () => datos().then((d) => {
      const lista = delUsuario(d).filter(a.filtro).sort((x, y) => nombreCompleto(x).localeCompare(nombreCompleto(y), 'es'));
      titulo.textContent = `Vista previa con datos de prueba · ${lista.length}`;
      const extra = id === 'transferencias' ? { titulo: 'Club de destino', valor: (f) => f.transferenciaPendiente?.aClub } : null;
      caja.replaceChildren(tablaFichas(lista, { conFederacion: S.usuario().federacion?.id === 0, extra, nombre: a.nombre,
        pagina: paginaActual, alCambiarPagina: (p) => { paginaActual = p; } }));
    }).catch(() => caja.replaceChildren(errorCarga()));
    pintarPrevia();
    refrescarListado = () => { if (caja.isConnected) pintarPrevia(); };
  }
  return vista;
}

S.registrar('fichas', (sub, params) => {
  if (!sub) return portada();
  if (sub === 'buscar') return buscar(params);
  return apartado(sub);
});
})();
