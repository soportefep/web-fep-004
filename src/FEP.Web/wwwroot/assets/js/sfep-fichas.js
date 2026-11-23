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
 * Solo "Buscar fichas" está migrado; el resto muestra "pendiente de migrar" y, cuando hay datos, una vista previa.
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
  buscar: { nombre: 'Buscar fichas', icono: 'fichas', legado: 'Fichas.asp', desc: 'Buscador de licencias por nombre, licencia, club, estado o categoría.', migrado: true },
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

/** Tabla de fichas con paginación. `cols` decide si se muestra la federación. */
function tablaFichas(lista, { conFederacion, extra } = {}) {
  let pagina = 0;
  const cuerpo = el('tbody');
  const pie = el('div', { class: 'sfep-paginas' });
  const cabeceras = ['Licencia', 'Apellidos y nombre', 'Categoría', 'Club', ...(conFederacion ? ['Federación'] : []), ...(extra ? [extra.titulo] : []), 'Estado', 'Temp.'];
  const pintar = () => {
    const total = Math.max(1, Math.ceil(lista.length / POR_PAGINA));
    pagina = Math.min(pagina, total - 1);
    const trozo = lista.slice(pagina * POR_PAGINA, (pagina + 1) * POR_PAGINA);
    cuerpo.replaceChildren(...trozo.map((f) => el('tr', {},
      el('td', { class: 'sfep-mono', 'data-label': 'Licencia', text: f.licencia }),
      el('th', { scope: 'row' }, el('button', { type: 'button', class: 'sfep-enlace', onclick: (e) => abrirFicha(f, e.currentTarget) }, nombreCompleto(f))),
      el('td', { 'data-label': 'Categoría', text: f.categoria }),
      el('td', { 'data-label': 'Club', text: f.club }),
      conFederacion ? el('td', { 'data-label': 'Federación', class: 'sfep-pequena', text: f.federacion?.nombre }) : null,
      extra ? el('td', { 'data-label': extra.titulo }, extra.valor(f)) : null,
      el('td', { 'data-label': 'Estado' }, estado(f.estado)),
      el('td', { 'data-label': 'Temporada', text: String(f.temporada) }))));
    pie.replaceChildren(
      el('p', { text: lista.length ? `${fmtNum.format(pagina * POR_PAGINA + 1)}–${fmtNum.format(pagina * POR_PAGINA + trozo.length)} de ${fmtNum.format(lista.length)}` : '' }),
      total > 1 ? el('div', { class: 'sfep-paginas__btns' },
        el('button', { type: 'button', class: 'sfep-btn sfep-btn--sec sfep-btn--sm', disabled: pagina === 0, onclick: () => { pagina--; pintar(); } }, '← Anterior'),
        el('span', { text: `Página ${pagina + 1} de ${total}` }),
        el('button', { type: 'button', class: 'sfep-btn sfep-btn--sec sfep-btn--sm', disabled: pagina >= total - 1, onclick: () => { pagina++; pintar(); } }, 'Siguiente →')) : null);
  };
  pintar();
  const caja = el('div', { class: 'sfep-tabla-caja' },
    lista.length ? el('div', { class: 'sfep-tabla-scroll' }, el('table', { class: 'sfep-tabla' },
      el('caption', { class: 'sr-only', text: 'Fichas' }),
      el('thead', {}, el('tr', {}, ...cabeceras.map((c) => el('th', { scope: 'col', text: c })))), cuerpo))
      : el('p', { class: 'sfep-vacio', text: 'No hay fichas que coincidan.' }),
    lista.length ? pie : null);
  return caja;
}

/* ---------------------------------------------------------------------------
   Ficha (diálogo)
   --------------------------------------------------------------------------- */
function abrirFicha(f, origen) {
  const u = S.usuario();
  const mod = !!u.permisos?.fichasModificar && !u.fichasSoloConsulta;
  const dato = (k, v) => [el('dt', { text: k }), el('dd', {}, v ?? '—')];
  let cerrar = () => {};
  const dlg =el('dialog', { class: 'sfep-ficha', 'aria-labelledby': 'ficha-titulo' },
    el('header', { class: 'sfep-ficha__cab' },
      el('div', {},
        el('p', { class: 'sfep-ficha__lic', text: `Licencia ${f.licencia} · ${f.tipo}` }),
        el('h2', { id: 'ficha-titulo', text: `${f.nombre} ${f.apellidos}` })),
      el('button', { type: 'button', class: 'sfep-icono sfep-ficha__cerrar', 'aria-label': 'Cerrar ficha', onclick: () => cerrar() }, '×')),
    el('div', { class: 'sfep-ficha__cuerpo' },
      el('p', {}, estado(f.estado), f.extranjero ? el('span', { class: 'sfep-etiqueta', text: 'Extranjero/a' }) : null),
      f.transferenciaPendiente ? el('p', { class: 'sfep-ficha__aviso' },
        el('strong', { text: 'Transferencia pendiente: ' }), `${f.transferenciaPendiente.deClub} → ${f.transferenciaPendiente.aClub}`) : null,
      el('dl', { class: 'sfep-dl' },
        dato('Categoría', f.categoria), dato('Género', f.genero === 'F' ? 'Femenino' : 'Masculino'),
        dato('Año de nacimiento', String(f.anioNacimiento)), dato('Club', f.club), dato('Federación', f.federacion?.nombre),
        dato('Temporada', String(f.temporada)), dato('Vigente hasta', f.vigenteHasta?.split('-').reverse().join('/')),
        dato('Seguro', f.seguro), dato('Forma de pago', f.pago)),
      el('div', {}, el('p', { class: 'sfep-ficha__sub', text: 'Temporadas con licencia' }),
        el('ul', { class: 'sfep-temporadas' }, ...(f.temporadas ?? []).map((t) => el('li', { text: String(t) }))))),
    el('footer', { class: 'sfep-ficha__pie' },
      mod ? el('button', { type: 'button', class: 'sfep-btn', onclick: () => toast(`Modificación de la ficha ${f.licencia}.`) }, 'Modificar') : null,
      mod && ['activa', 'caducada'].includes(f.estado) ? el('button', { type: 'button', class: 'sfep-btn sfep-btn--sec', onclick: () => toast(`Renovación de la licencia ${f.licencia} para 2027.`) }, 'Renovar 2027') : null,
      el('button', { type: 'button', class: 'sfep-btn sfep-btn--sec', onclick: () => toast(`Impresión de la licencia ${f.licencia}.`) }, 'Imprimir licencia'),
      mod && f.estado !== 'baja' ? el('button', { type: 'button', class: 'sfep-btn sfep-btn--sec sfep-btn--peligro', onclick: () => toast(`Baja de la ficha ${f.licencia}.`) }, 'Dar de baja') : null));
  cerrar = S.dialogoModal(dlg, origen);
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

function buscar(params) {
  const u = S.usuario();
  const esFep = u.federacion?.id === 0;
  const campo = (id, etiqueta, control) => el('div', { class: 'sfep-campo' }, el('label', { for: id, text: etiqueta }), control);
  const opciones = (vals, todos) => [el('option', { value: '', text: todos }), ...vals.map((v) => el('option', { value: v, text: v[0].toUpperCase() + v.slice(1) }))];

  const fTexto = el('input', { type: 'search', id: 'fb-texto', placeholder: 'Nombre, apellidos o licencia', autocomplete: 'off', value: params?.get('q') ?? '' });
  const fFed = el('select', { id: 'fb-fed' });
  const fClub = el('select', { id: 'fb-club' });
  const fEstado = el('select', { id: 'fb-estado' }, ...opciones(['activa', 'renovada sin pagar', 'pendiente de validar', 'caducada', 'baja'], 'Todos'));
  const fCat = el('select', { id: 'fb-cat' });
  const fTipo = el('select', { id: 'fb-tipo' }, ...opciones(['Jugador/a', 'Técnico/a', 'Juez-árbitro'], 'Todos'));
  const fGen = el('select', { id: 'fb-gen' }, el('option', { value: '', text: 'Todos' }), el('option', { value: 'F', text: 'Femenino' }), el('option', { value: 'M', text: 'Masculino' }));
  const resultados = el('div', { class: 'sfep-resultados', 'aria-live': 'polite' }, cargando());
  const cuenta = el('p', { class: 'sfep-cuenta', role: 'status' });
  let lista = [];

  const filtrar = () => {
    const q = normaliza(fTexto.value.trim());
    const filtradas = lista.filter((f) => (!q || normaliza(`${f.nombre} ${f.apellidos} ${f.licencia}`).includes(q))
      && (!fFed.value || String(f.federacion?.id) === fFed.value) && (!fClub.value || f.club === fClub.value)
      && (!fEstado.value || f.estado === fEstado.value) && (!fCat.value || f.categoria === fCat.value)
      && (!fTipo.value || f.tipo === fTipo.value) && (!fGen.value || f.genero === fGen.value))
      .sort((a, b) => nombreCompleto(a).localeCompare(nombreCompleto(b), 'es'));
    cuenta.textContent = `${fmtNum.format(filtradas.length)} ${filtradas.length === 1 ? 'ficha' : 'fichas'}`;
    resultados.replaceChildren(tablaFichas(filtradas, { conFederacion: esFep && !fFed.value }));
  };
  const pintarClubes = () => {
    const clubes = [...new Set(lista.filter((f) => !fFed.value || String(f.federacion?.id) === fFed.value).map((f) => f.club))].sort((a, b) => a.localeCompare(b, 'es'));
    const actual = fClub.value;
    fClub.replaceChildren(el('option', { value: '', text: 'Todos' }), ...clubes.map((c) => el('option', { value: c, text: c })));
    fClub.value = clubes.includes(actual) ? actual : '';
  };

  const form = el('form', { class: 'sfep-filtros', role: 'search', 'aria-label': 'Filtros de búsqueda', onsubmit: (e) => { e.preventDefault(); filtrar(); } },
    el('div', { class: 'sfep-filtros__grid' },
      el('div', { class: 'sfep-campo sfep-campo--ancho' }, el('label', { for: 'fb-texto', text: 'Buscar' }), fTexto),
      esFep ? campo('fb-fed', 'Federación', fFed) : null,
      campo('fb-club', 'Club', fClub), campo('fb-estado', 'Estado', fEstado), campo('fb-cat', 'Categoría', fCat),
      campo('fb-tipo', 'Tipo', fTipo), campo('fb-gen', 'Género', fGen)),
    el('div', { class: 'sfep-filtros__acciones' },
      el('button', { type: 'submit', class: 'sfep-btn' }, 'Buscar'),
      el('button', { type: 'reset', class: 'sfep-btn sfep-btn--sec', onclick: () => setTimeout(() => { pintarClubes(); filtrar(); fTexto.focus(); }) }, 'Limpiar'),
      puede('nueva') ? el('a', { class: 'sfep-btn sfep-btn--rojo', href: '#m/fichas/nueva' }, '+ Nueva ficha') : null));

  let espera;
  fTexto.addEventListener('input', () => { clearTimeout(espera); espera = setTimeout(filtrar, 200); });
  [fEstado, fCat, fTipo, fGen, fClub].forEach((s) => s.addEventListener('change', filtrar));
  fFed.addEventListener('change', () => { pintarClubes(); filtrar(); });

  datos().then((d) => {
    lista = delUsuario(d);
    const feds = S.comoLista(d.federaciones ?? []);
    fFed.replaceChildren(el('option', { value: '', text: 'Todas' }), ...feds.map((f) => el('option', { value: String(f.id), text: f.nombre })));
    const ORDEN = ['Benjamín', 'Alevín', 'Infantil', 'Cadete', 'Júnior', 'Absoluto', 'Veteranos +35', 'Veteranos +45', 'Veteranos +55'];
    const cats = [...new Set(lista.map((f) => f.categoria))].sort((a, b) => ORDEN.indexOf(a) - ORDEN.indexOf(b));
    fCat.replaceChildren(el('option', { value: '', text: 'Todas' }), ...cats.map((c) => el('option', { value: c, text: c })));
    pintarClubes();
    filtrar();
  }).catch(() => resultados.replaceChildren(errorCarga()));

  return [migas('Buscar fichas'),
    cabecera('Buscar fichas', esFep ? 'Todas las federaciones · datos de prueba.' : `${u.federacion?.nombre} · datos de prueba.`),
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
    datos().then((d) => {
      const lista = delUsuario(d).filter(a.filtro).sort((x, y) => nombreCompleto(x).localeCompare(nombreCompleto(y), 'es'));
      titulo.textContent = `Vista previa con datos de prueba · ${lista.length}`;
      const extra = id === 'transferencias' ? { titulo: 'Club de destino', valor: (f) => f.transferenciaPendiente?.aClub } : null;
      caja.replaceChildren(tablaFichas(lista, { conFederacion: S.usuario().federacion?.id === 0, extra }));
    }).catch(() => caja.replaceChildren(errorCarga()));
  }
  return vista;
}

S.registrar('fichas', (sub, params) => {
  if (!sub) return portada();
  if (sub === 'buscar') return buscar(params);
  return apartado(sub);
});
})();
