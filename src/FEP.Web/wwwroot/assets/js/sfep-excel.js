/**
 * SFEP · Exportar a Excel (.xlsx) sin librerías externas.
 *
 * Genera en el navegador un libro .xlsx real (Office Open XML: una hoja con cabecera en negrita, fila
 * de cabecera fija y autofiltro) empaquetado en un ZIP sin compresión, y lo descarga. No envía nada a
 * ningún servidor. Lo usan los listados de los módulos de SFEP:
 *
 *   SFEPExcel.descargar('fichas.xlsx', {
 *     hoja: 'Fichas',
 *     columnas: [{ titulo: 'Id ficha', valor: (f) => f.idFicha, ancho: 12 }, …],
 *     filas: lista,
 *   });
 *
 * Los valores numéricos (typeof number) se guardan como número; el resto, como texto.
 */
(function () {
'use strict';

/* ---------------- ZIP sin compresión (método "store") ---------------- */
const TABLA_CRC = (() => {
  const t = new Uint32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xEDB88320 ^ (c >>> 1) : c >>> 1;
    t[n] = c >>> 0;
  }
  return t;
})();
function crc32(bytes) {
  let c = 0xFFFFFFFF;
  for (let i = 0; i < bytes.length; i++) c = TABLA_CRC[(c ^ bytes[i]) & 0xFF] ^ (c >>> 8);
  return (c ^ 0xFFFFFFFF) >>> 0;
}
/** archivos: [{ nombre, datos: Uint8Array }] -> Blob .zip */
function zip(archivos, tipo) {
  const enc = new TextEncoder();
  const partes = [];
  const central = [];
  let offset = 0;
  const FECHA = 0x21;   // 01/01/1980 (fecha fija: el contenido no depende de cuándo se genera)
  for (const a of archivos) {
    const nombre = enc.encode(a.nombre);
    const crc = crc32(a.datos);
    const n = a.datos.length;
    const cab = new DataView(new ArrayBuffer(30));
    cab.setUint32(0, 0x04034b50, true);
    cab.setUint16(4, 20, true);
    cab.setUint16(6, 0x0800, true);          // nombres en UTF-8
    cab.setUint16(8, 0, true);               // sin compresión
    cab.setUint16(10, 0, true);
    cab.setUint16(12, FECHA, true);
    cab.setUint32(14, crc, true);
    cab.setUint32(18, n, true);
    cab.setUint32(22, n, true);
    cab.setUint16(26, nombre.length, true);
    cab.setUint16(28, 0, true);
    partes.push(new Uint8Array(cab.buffer), nombre, a.datos);

    const cd = new DataView(new ArrayBuffer(46));
    cd.setUint32(0, 0x02014b50, true);
    cd.setUint16(4, 20, true);
    cd.setUint16(6, 20, true);
    cd.setUint16(8, 0x0800, true);
    cd.setUint16(10, 0, true);
    cd.setUint16(12, 0, true);
    cd.setUint16(14, FECHA, true);
    cd.setUint32(16, crc, true);
    cd.setUint32(20, n, true);
    cd.setUint32(24, n, true);
    cd.setUint16(28, nombre.length, true);
    cd.setUint32(42, offset, true);
    central.push(new Uint8Array(cd.buffer), nombre);
    offset += 30 + nombre.length + n;
  }
  const tamCentral = central.reduce((s, p) => s + p.length, 0);
  const fin = new DataView(new ArrayBuffer(22));
  fin.setUint32(0, 0x06054b50, true);
  fin.setUint16(8, archivos.length, true);
  fin.setUint16(10, archivos.length, true);
  fin.setUint32(12, tamCentral, true);
  fin.setUint32(16, offset, true);
  return new Blob([...partes, ...central, new Uint8Array(fin.buffer)], { type: tipo });
}

/* ---------------- Libro .xlsx ---------------- */
const XML = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>\n';
const NS = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main';
const NS_R = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships';
const esc = (t) => String(t)
  .replace(/[\u0000-\u0008\u000B\u000C\u000E-\u001F]/g, '')
  .replace(/[<>&"]/g, (ch) => ({ '<': '&lt;', '>': '&gt;', '&': '&amp;', '"': '&quot;' }[ch]));
function letra(i) {
  let s = '';
  for (let n = i + 1; n > 0; n = Math.floor((n - 1) / 26)) s = String.fromCharCode(65 + ((n - 1) % 26)) + s;
  return s;
}
function celda(valor, ref, estilo = 0) {
  const s = estilo ? ` s="${estilo}"` : '';
  if (valor == null || valor === '') return '';
  if (typeof valor === 'number' && Number.isFinite(valor)) return `<c r="${ref}"${s}><v>${valor}</v></c>`;
  return `<c r="${ref}"${s} t="inlineStr"><is><t xml:space="preserve">${esc(valor)}</t></is></c>`;
}
/** Nombre de hoja válido en Excel: sin []:*?/\ y como mucho 31 caracteres. */
const nombreHoja = (t) => (String(t || 'Hoja1').replace(/[[\]:*?/\\]/g, ' ').trim().slice(0, 31) || 'Hoja1');

function libro({ hoja = 'Hoja1', columnas, filas }) {
  const nh = nombreHoja(hoja);
  const ultima = letra(columnas.length - 1);
  const nFilas = filas.length + 1;
  const cabecera = `<row r="1">${columnas.map((c, i) => celda(c.titulo, `${letra(i)}1`, 1)).join('')}</row>`;
  const cuerpo = filas.map((f, j) => {
    const r = j + 2;
    return `<row r="${r}">${columnas.map((c, i) => celda(c.valor(f), `${letra(i)}${r}`)).join('')}</row>`;
  }).join('');
  const anchos = columnas.map((c, i) => {
    const largo = Math.max(String(c.titulo).length, ...filas.slice(0, 200).map((f) => String(c.valor(f) ?? '').length));
    const w = c.ancho ?? Math.min(60, Math.max(8, largo + 2));
    return `<col min="${i + 1}" max="${i + 1}" width="${w}" customWidth="1"/>`;
  }).join('');
  const sheet = `${XML}<worksheet xmlns="${NS}" xmlns:r="${NS_R}">`
    + '<sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>'
    + `<cols>${anchos}</cols><sheetData>${cabecera}${cuerpo}</sheetData>`
    + `<autoFilter ref="A1:${ultima}${nFilas}"/></worksheet>`;
  const workbook = `${XML}<workbook xmlns="${NS}" xmlns:r="${NS_R}"><sheets><sheet name="${esc(nh)}" sheetId="1" r:id="rId1"/></sheets>`
    + `<definedNames><definedName name="_xlnm._FilterDatabase" localSheetId="0" hidden="1">'${esc(nh.replace(/'/g, "''"))}'!$A$1:$${ultima}$${nFilas}</definedName></definedNames></workbook>`;
  const styles = `${XML}<styleSheet xmlns="${NS}">`
    + '<fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts>'
    + '<fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>'
    + '<borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>'
    + '<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>'
    + '<cellXfs count="2"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/></cellXfs>'
    + '<cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>';
  const tipos = `${XML}<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">`
    + '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>'
    + '<Default Extension="xml" ContentType="application/xml"/>'
    + '<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>'
    + '<Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>'
    + '<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>';
  const rels = `${XML}<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">`
    + `<Relationship Id="rId1" Type="${NS_R}/officeDocument" Target="xl/workbook.xml"/></Relationships>`;
  const relsLibro = `${XML}<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">`
    + `<Relationship Id="rId1" Type="${NS_R}/worksheet" Target="worksheets/sheet1.xml"/>`
    + `<Relationship Id="rId2" Type="${NS_R}/styles" Target="styles.xml"/></Relationships>`;
  const enc = new TextEncoder();
  return zip([
    { nombre: '[Content_Types].xml', datos: enc.encode(tipos) },
    { nombre: '_rels/.rels', datos: enc.encode(rels) },
    { nombre: 'xl/workbook.xml', datos: enc.encode(workbook) },
    { nombre: 'xl/_rels/workbook.xml.rels', datos: enc.encode(relsLibro) },
    { nombre: 'xl/styles.xml', datos: enc.encode(styles) },
    { nombre: 'xl/worksheets/sheet1.xml', datos: enc.encode(sheet) },
  ], 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');
}

function descargar(nombreArchivo, opciones) {
  const blob = libro(opciones);
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = nombreArchivo.endsWith('.xlsx') ? nombreArchivo : `${nombreArchivo}.xlsx`;
  document.body.append(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 10000);
  return blob;
}

window.SFEPExcel = Object.freeze({ libro, descargar });
})();
