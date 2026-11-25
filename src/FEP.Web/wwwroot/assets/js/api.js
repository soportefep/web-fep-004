/**
 * Cliente de la API FEP con respaldo en mocks.
 *
 * - API_BASE se lee de <meta name="fep-api-base" content="https://api..."> (vacío = mismo origen).
 * - get('/api/v1/noticias/destacadas') intenta la API; si falla (red, timeout, estado != 2xx o
 *   respuesta que no es JSON) carga mock/noticias-destacadas.json (ruta sin /api/v1/, "/" → "-").
 * - Si la página se abre directamente desde disco (file://), el navegador bloquea fetch():
 *   se usan los mocks embebidos en mock/mocks.js (window.FEP_MOCKS), generado con
 *   tools/generar-mocks-js.ps1 a partir de los .json.
 * - LEGACY_BASE (<meta name="fep-legacy-base">) es la web actual, destino temporal de los enlaces
 *   relativos que guarda la BD (p. ej. "Noticia.asp?id=123") hasta que se migren esas páginas.
 *
 * Script clásico (no módulo) para que funcione también con file://. Expone window.FEPApi.
 */
(function () {
  'use strict';

  const leerMeta = (nombre) => document.querySelector(`meta[name="${nombre}"]`)?.getAttribute('content')?.trim() ?? '';

  const API_BASE = leerMeta('fep-api-base').replace(/\/+$/, '');
  const LEGACY_BASE = leerMeta('fep-legacy-base') || 'https://www.padelfederacion.es/';
  const DESDE_DISCO = location.protocol === 'file:';

  const TIMEOUT_MS = 6000;

  /** Nombre del mock para una ruta: /api/v1/noticias/destacadas?x=1 → noticias-destacadas */
  function nombreMock(ruta) {
    return ruta
      .split('?')[0]
      .replace(/^\/?api\/v\d+\//i, '')
      .replace(/^\/+|\/+$/g, '')
      .replace(/\//g, '-');
  }

  async function fetchJson(url, timeoutMs) {
    const ctrl = new AbortController();
    const t = setTimeout(() => ctrl.abort(), timeoutMs);
    try {
      const res = await fetch(url, { headers: { Accept: 'application/json' }, signal: ctrl.signal });
      if (!res.ok) throw new Error(`HTTP ${res.status} en ${url}`);
      const tipo = res.headers.get('content-type') ?? '';
      if (!/json/i.test(tipo)) throw new Error(`Respuesta no JSON en ${url}`);
      return await res.json();
    } finally {
      clearTimeout(t);
    }
  }

  /** Copia del mock embebido (para que nadie modifique el original). */
  function mockEmbebido(nombre) {
    const m = window.FEP_MOCKS && window.FEP_MOCKS[nombre];
    return m === undefined ? undefined : JSON.parse(JSON.stringify(m));
  }

  /**
   * GET de la API con respaldo en mock/<nombre>.json y, si tampoco se puede, en window.FEP_MOCKS.
   * @param {string} ruta Ruta absoluta de la API, p. ej. '/api/v1/menu'.
   * @returns {Promise<any>} JSON de la API o del mock. Lanza si todo falla.
   */
  async function get(ruta, { timeout = TIMEOUT_MS } = {}) {
    const nombre = nombreMock(ruta);
    // Desde disco sin API configurada no hay nada que pedir: mocks embebidos directamente.
    if (DESDE_DISCO && !API_BASE) {
      const m = mockEmbebido(nombre);
      if (m !== undefined) return m;
      throw new Error(`Sin API ni mock embebido para ${ruta} (página abierta desde disco)`);
    }
    try {
      return await fetchJson(API_BASE + ruta, timeout);
    } catch (errApi) {
      const mock = new URL(`mock/${nombre}.json`, document.baseURI).href;
      try {
        const datos = await fetchJson(mock, timeout);
        console.info(`[FEP] API no disponible para ${ruta}; usando ${mock}`, errApi?.message ?? errApi);
        return datos;
      } catch (errMock) {
        const m = mockEmbebido(nombre);
        if (m !== undefined) {
          console.info(`[FEP] API y ${mock} no disponibles; usando mock embebido`);
          return m;
        }
        throw new Error(`No se pudo cargar ${ruta} ni su mock: ${errMock?.message ?? errMock}`);
      }
    }
  }

  /** Normaliza la respuesta a array (acepta [..] o envoltorios { items | datos | data: [..] }). */
  function comoLista(datos) {
    if (Array.isArray(datos)) return datos;
    if (datos && typeof datos === 'object') {
      for (const k of ['items', 'datos', 'data', 'resultados', 'value']) {
        if (Array.isArray(datos[k])) return datos[k];
      }
    }
    return [];
  }

  /**
   * Convierte un enlace de la BD en URL utilizable: absolutas se respetan; relativas se resuelven
   * contra la web actual. Solo admite http(s) y mailto (descarta javascript:, data:…).
   */
  function urlSegura(enlace, base = LEGACY_BASE) {
    if (!enlace || typeof enlace !== 'string') return null;
    const limpio = enlace.trim();
    if (!limpio || limpio === '#') return null;
    try {
      const url = new URL(limpio.startsWith('www.') ? `https://${limpio}` : limpio, base);
      return ['http:', 'https:', 'mailto:'].includes(url.protocol) ? url.href : null;
    } catch {
      return null;
    }
  }

  window.FEPApi = Object.freeze({ API_BASE, LEGACY_BASE, nombreMock, get, comoLista, urlSegura });
})();
