namespace FEP.Api.Contracts.Clubes;

/// <summary>
/// Página de resultados del buscador de clubes federados (página <c>Clubes.asp</c>): clubes con la
/// licencia de club renovada en el año en curso (<c>SFEP_Clubes_Licencia.Año</c> = año actual y
/// <c>Renovado = 'S'</c>), ordenados por provincia, localidad y nombre.
/// </summary>
/// <param name="Anio">Año de la licencia de club consultado (el año en curso).</param>
/// <param name="Federacion">Filtro de federación aplicado (<c>SFEP_Federaciones.IdFederacion</c>), si lo hay.</param>
/// <param name="Provincia">Filtro de provincia aplicado (<c>SFEP_Provincias.IdProvincia</c>), si lo hay.</param>
/// <param name="Busqueda">Texto buscado ya normalizado (términos separados por un espacio), si lo hay.</param>
/// <param name="Pagina">Página devuelta (empieza en 1).</param>
/// <param name="Limite">Tamaño de página.</param>
/// <param name="Total">Número total de clubes que cumplen los filtros (todas las páginas).</param>
/// <param name="Items">Clubes de esta página.</param>
public sealed record ClubesPaginaDto(
    int Anio,
    int? Federacion,
    int? Provincia,
    string? Busqueda,
    int Pagina,
    int Limite,
    int Total,
    IReadOnlyList<ClubResumenDto> Items);

/// <summary>Club federado en el listado.</summary>
/// <param name="Id">
/// <c>SFEP_Clubes.IdClub</c> como texto (hay códigos con ceros a la izquierda, p. ej. <c>0118003</c>).
/// Identifica la ficha de la web actual (<c>Club.asp?id={id}</c>).
/// </param>
/// <param name="Nombre">Nombre del club (<c>Club</c>).</param>
/// <param name="ProvinciaId">Provincia del club (<c>SFEP_Provincias.IdProvincia</c>).</param>
/// <param name="Provincia">Nombre de la provincia (en mayúsculas, como en la BD).</param>
/// <param name="Localidad">Localidad (<c>Localidad</c>); muchos clubes no la tienen.</param>
public sealed record ClubResumenDto(string Id, string Nombre, int ProvinciaId, string Provincia, string? Localidad);

/// <summary>Opciones de los desplegables del buscador de clubes.</summary>
/// <param name="Federaciones">Federaciones autonómicas (sin la FEP), por abreviatura.</param>
/// <param name="Provincias">Todas las provincias, por nombre.</param>
public sealed record ClubesFiltrosDto(
    IReadOnlyList<FederacionFiltroDto> Federaciones,
    IReadOnlyList<ProvinciaFiltroDto> Provincias);

/// <summary>Federación del desplegable.</summary>
/// <param name="Id"><c>SFEP_Federaciones.IdFederacion</c> (1–22).</param>
/// <param name="Nombre">Abreviatura (<c>ANDALUZA</c>, <c>COM.VALENCIANA</c>…).</param>
/// <param name="Provincias">Ids de las provincias de su comunidad (para acotar el desplegable de provincias).</param>
public sealed record FederacionFiltroDto(int Id, string Nombre, IReadOnlyList<int> Provincias);

/// <summary>Provincia del desplegable.</summary>
/// <param name="Id"><c>SFEP_Provincias.IdProvincia</c>.</param>
/// <param name="Nombre">Nombre (en mayúsculas, como en la BD).</param>
public sealed record ProvinciaFiltroDto(int Id, string Nombre);
