namespace FEP.Api.Contracts.Federacion;

/// <summary>
/// Composición de los órganos de gobierno de la FEP (página <c>Organos_Gobierno.asp</c>).
/// </summary>
/// <param name="Actualizado">Fecha de la última revisión del contenido.</param>
/// <param name="Organos">Órganos en el orden de la página: Junta Directiva, Asamblea General, Comisión Delegada y CTAP.</param>
public sealed record OrganosGobiernoDto(DateOnly? Actualizado, IReadOnlyList<OrganoDto> Organos);

/// <summary>Un órgano de gobierno.</summary>
/// <param name="Clave">Identificador estable (ancla en la página): <c>junta-directiva</c>, <c>asamblea</c>…</param>
/// <param name="Titulo">Nombre del órgano.</param>
/// <param name="Descripcion">Texto introductorio opcional.</param>
/// <param name="ImagenUrl">URL absoluta de la imagen asociada (organigrama), si la hay.</param>
/// <param name="ImagenAlt">Texto alternativo de la imagen.</param>
/// <param name="Grupos">Grupos de miembros (presidencia, vocales, estamentos…).</param>
/// <param name="Actas">Actas publicadas, de la más reciente a la más antigua.</param>
public sealed record OrganoDto(
    string Clave,
    string Titulo,
    string? Descripcion,
    string? ImagenUrl,
    string? ImagenAlt,
    IReadOnlyList<GrupoOrganoDto> Grupos,
    IReadOnlyList<ActaDto> Actas);

/// <summary>Grupo de miembros dentro de un órgano.</summary>
/// <param name="Clave">Identificador del grupo dentro del órgano.</param>
/// <param name="Titulo">Título del grupo (p. ej. "Por el estamento de clubes").</param>
/// <param name="EtiquetaNombre">Cabecera de la columna de nombre (Miembro, Federación, Club…).</param>
/// <param name="EtiquetaDetalle">Cabecera de la columna de detalle (Cargo, Email, Tipo…), si la hay.</param>
/// <param name="Presentacion"><c>tarjetas</c> (personas destacadas) o <c>lista</c> (tablas largas).</param>
/// <param name="Miembros">Miembros del grupo en el orden oficial.</param>
public sealed record GrupoOrganoDto(
    string Clave,
    string Titulo,
    string EtiquetaNombre,
    string? EtiquetaDetalle,
    string Presentacion,
    IReadOnlyList<MiembroOrganoDto> Miembros);

/// <summary>Miembro de un órgano.</summary>
/// <param name="Nombre">Nombre de la persona o entidad.</param>
/// <param name="Cargo">Cargo (Presidente, Vocal de…).</param>
/// <param name="Detalle">Dato complementario (área, categoría, tipo, estamento).</param>
/// <param name="Email">Correo de contacto público.</param>
/// <param name="Web">Web de contacto (federaciones y clubes).</param>
/// <param name="Depende">Cargo del que depende en el organigrama (p. ej. los vocales de la Junta
/// cuelgan de "Vicepresidente 1/2/3"). Debe coincidir con el <c>Cargo</c> de otro miembro del órgano.</param>
public sealed record MiembroOrganoDto(string Nombre, string? Cargo, string? Detalle, string? Email, string? Web, string? Depende = null);

/// <summary>Acta publicada.</summary>
/// <param name="Titulo">Título del documento.</param>
/// <param name="Fecha">Fecha de la sesión.</param>
/// <param name="Tipo"><c>Ordinaria</c> o <c>Extraordinaria</c>.</param>
/// <param name="Url">URL absoluta del PDF.</param>
public sealed record ActaDto(string Titulo, DateOnly? Fecha, string? Tipo, string? Url);
