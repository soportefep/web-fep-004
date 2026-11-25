namespace FEP.Api.Contracts.Competicion;

/// <summary>
/// Ranking de equipos absolutos (<c>GET /api/v1/competicion/rankings/equipos-absolutos</c>). Sustituye a
/// <c>Ranking_Equipos_Absolutos.asp</c>, que pintaba con 32 consultas casi idénticas el resultado de los Campeonatos
/// Absolutos por equipos de 2017 a 2024 (1.ª y 2.ª categoría, masculina y femenina). Estructuralmente distinto de las
/// clasificaciones de jugadores (<see cref="ClasificacionRankingDto"/>): cada puesto es un equipo de club, por temporada.
/// </summary>
/// <param name="Clave">Clave del ranking en el índice (<c>equipos-absolutos</c>).</param>
/// <param name="Titulo">Título del ranking ("Ranking de Equipos Absolutos").</param>
/// <param name="Anios">Temporadas con clasificación publicada, de la más reciente a la más antigua.</param>
/// <param name="Anio">Filtro de temporada aplicado o <c>null</c> (todas).</param>
/// <param name="Temporadas">Clasificación de cada temporada pedida, de la más reciente a la más antigua.</param>
public sealed record RankingEquiposDto(
    string Clave,
    string Titulo,
    IReadOnlyList<int> Anios,
    int? Anio,
    IReadOnlyList<TemporadaRankingEquiposDto> Temporadas);

/// <summary>Clasificación de los Campeonatos Absolutos por equipos de una temporada.</summary>
/// <param name="Anio">Temporada (<c>SFEP_Ranking_Equipos.ano</c>).</param>
/// <param name="Listas">
/// Una lista por categoría y género con algún equipo, en el orden del ASP: 1.ª masculina, 1.ª femenina, 2.ª masculina,
/// 2.ª femenina.
/// </param>
public sealed record TemporadaRankingEquiposDto(int Anio, IReadOnlyList<ListaRankingEquiposDto> Listas);

/// <summary>Clasificación de una categoría y género.</summary>
/// <param name="Categoria">Categoría (<c>1</c> primera, <c>2</c> segunda).</param>
/// <param name="Genero"><c>M</c> (masculina) o <c>F</c> (femenina).</param>
/// <param name="Titulo">Nombre de la lista ("1.ª categoría masculina").</param>
/// <param name="Puestos">Equipos por puesto (puede haber puestos compartidos: dos 5.º y dos 7.º).</param>
public sealed record ListaRankingEquiposDto(int Categoria, string Genero, string Titulo, IReadOnlyList<PuestoEquipoDto> Puestos);

/// <summary>Un equipo clasificado.</summary>
/// <param name="Puesto">Puesto final (<c>Puesto</c>).</param>
/// <param name="Equipo">Nombre del equipo (<c>Equipo</c>, sin espacios repetidos; en mayúsculas, como en la BD).</param>
/// <param name="Club">Código del club (<c>Idclub</c>, como texto: tiene ceros a la izquierda, p. ej. <c>0908009</c>).</param>
/// <param name="ClubUrl">Ficha del club en la web actual (<c>Club.asp?id={club}</c>; aún no migrada).</param>
/// <param name="Escudo">Ruta original del escudo del club bajo <c>refs/</c> (<c>images/{club}.jpg</c>, como el ASP). Muchos clubes no lo tienen (404).</param>
/// <param name="EscudoUrl">URL absoluta del escudo (<c>Media:BaseUrl</c>).</param>
/// <param name="Federacion">Federación autonómica del club (bandera del ASP) o <c>null</c> si el club no tiene.</param>
/// <param name="Provincia">Provincia del club (<c>SFEP_Provincias.Provincia</c>, en mayúsculas como en la BD) o <c>null</c>.</param>
public sealed record PuestoEquipoDto(
    int Puesto,
    string Equipo,
    string? Club,
    string? ClubUrl,
    string? Escudo,
    string? EscudoUrl,
    FederacionEquipoDto? Federacion,
    string? Provincia);

/// <summary>Federación autonómica del club de un equipo.</summary>
/// <param name="Id"><c>SFEP_Federaciones.IdFederacion</c> (1–22; ficha en <c>federacion.html?id=</c>).</param>
/// <param name="Territorio">Comunidad o ciudad autónoma ("Cataluña"; de <c>Contenidos/federaciones-autonomicas.json</c>, o la abreviatura de la BD si no está).</param>
/// <param name="Nombre">Nombre oficial ("Federació Catalana de Pàdel") o <c>null</c> si no se conoce.</param>
/// <param name="Abreviatura"><c>SFEP_Federaciones.Abreviatura</c> tal cual ("CATALANA") o <c>null</c>.</param>
/// <param name="Bandera">Ruta original de la bandera bajo <c>refs/</c> (<c>images/{id}.jpg</c>, como el ASP).</param>
/// <param name="BanderaUrl">URL absoluta de la bandera.</param>
public sealed record FederacionEquipoDto(int Id, string Territorio, string? Nombre, string? Abreviatura, string Bandera, string? BanderaUrl);
