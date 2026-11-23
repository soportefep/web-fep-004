namespace FEP.Api.Contracts.Federacion;

/// <summary>
/// Equipo técnico y administrativo de la FEP (página <c>Equipo_FEP.asp</c>).
/// </summary>
/// <param name="Actualizado">Fecha de la última revisión del contenido.</param>
/// <param name="Areas">Áreas en el orden de la página: organigrama (equipo técnico y administrativo) y equipo de menores.</param>
public sealed record EquipoFepDto(DateOnly? Actualizado, IReadOnlyList<AreaEquipoDto> Areas);

/// <summary>Área del equipo de la FEP (cada una tenía su propia imagen en el ASP).</summary>
/// <param name="Clave">Identificador estable (ancla en la página): <c>organigrama</c>, <c>menores</c>.</param>
/// <param name="Titulo">Nombre del área.</param>
/// <param name="Descripcion">Texto introductorio opcional.</param>
/// <param name="Imagen">Ruta original de la imagen bajo <c>refs/</c> (p. ej. <c>images/ORGANIGRAMA-FEP.png</c>).</param>
/// <param name="ImagenUrl">URL absoluta de la imagen, calculada con <c>Media:BaseUrl</c>.</param>
/// <param name="ImagenAlt">Texto alternativo de la imagen.</param>
/// <param name="Grupos">Grupos de personas del área.</param>
public sealed record AreaEquipoDto(
    string Clave,
    string Titulo,
    string? Descripcion,
    string? Imagen,
    string? ImagenUrl,
    string? ImagenAlt,
    IReadOnlyList<GrupoEquipoDto> Grupos);

/// <summary>Grupo de personas dentro de un área (dirección, equipo técnico…).</summary>
/// <param name="Clave">Identificador del grupo dentro del área.</param>
/// <param name="Titulo">Título del grupo.</param>
/// <param name="Miembros">Personas del grupo en el orden del organigrama.</param>
public sealed record GrupoEquipoDto(string Clave, string Titulo, IReadOnlyList<MiembroEquipoDto> Miembros);

/// <summary>Persona del equipo de la FEP.</summary>
/// <param name="Nombre">Nombre y apellidos.</param>
/// <param name="Cargo">Puesto, si el organigrama lo indica.</param>
public sealed record MiembroEquipoDto(string Nombre, string? Cargo);
