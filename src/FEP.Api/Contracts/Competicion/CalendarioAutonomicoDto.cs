namespace FEP.Api.Contracts.Competicion;

/// <summary>
/// Calendario de pruebas de las federaciones autonómicas (territoriales) puntuables para un ranking FEP
/// de una categoría (páginas <c>Calendario_Menores_FFAA.asp</c> y <c>Calendario_Veteranos_FFAA.asp</c>).
/// </summary>
/// <param name="Categoria">Categoría del calendario: <c>menores</c> o <c>veteranos</c> (ver <see cref="ReglasCalendariosAutonomicos"/>).</param>
/// <param name="Titulo">Título del calendario.</param>
/// <param name="Temporada">Año de la temporada.</param>
/// <param name="Actualizado">Fecha de la última revisión del contenido.</param>
/// <param name="Ranking">Ranking FEP para el que puntúan las pruebas (p. ej. "Ranking de Menores FEP").</param>
/// <param name="MaximoPruebasPorFederacion">Pruebas puntuables que el RTG permite a cada federación autonómica.</param>
/// <param name="Introduccion">Párrafos de presentación (texto plano).</param>
/// <param name="ActualizacionesRanking">Momentos de la temporada en que se incorporan las pruebas al ranking, si el reglamento los fija.</param>
/// <param name="Participacion">Condiciones de participación de los deportistas de otras federaciones.</param>
/// <param name="Leyenda">Significado de la marca <c>enRanking</c> (en la web antigua, las pruebas "en verde").</param>
/// <param name="Federaciones">Federaciones con pruebas, en el orden de la página original.</param>
public sealed record CalendarioAutonomicoDto(
    string Categoria,
    string Titulo,
    int Temporada,
    DateOnly? Actualizado,
    string? Ranking,
    int? MaximoPruebasPorFederacion,
    IReadOnlyList<string> Introduccion,
    ActualizacionesRankingDto? ActualizacionesRanking,
    string? Participacion,
    string? Leyenda,
    IReadOnlyList<FederacionCalendarioDto> Federaciones);

/// <summary>Momentos en que se incorporan al ranking las pruebas autonómicas.</summary>
/// <param name="Texto">Texto que introduce la lista.</param>
/// <param name="Momentos">Momentos, en orden cronológico.</param>
public sealed record ActualizacionesRankingDto(string? Texto, IReadOnlyList<MomentoActualizacionRankingDto> Momentos);

/// <summary>Una actualización del ranking en la que entran pruebas autonómicas.</summary>
/// <param name="Orden">Número del momento (1, 2, 3…).</param>
/// <param name="Titulo">Resumen corto (p. ej. "Tras el TyC 2").</param>
/// <param name="Descripcion">Texto completo del reglamento o de la FEP.</param>
/// <param name="MaximoTorneos">Torneos autonómicos que se pueden incorporar en ese momento.</param>
/// <param name="Fecha">Fecha de la actualización, si está fijada.</param>
public sealed record MomentoActualizacionRankingDto(int Orden, string Titulo, string? Descripcion, int? MaximoTorneos, DateOnly? Fecha);

/// <summary>Federación autonómica y sus pruebas en el calendario.</summary>
/// <param name="Clave">Identificador estable (ancla en la página): <c>andalucia</c>, <c>pais-vasco</c>…</param>
/// <param name="IdFederacion"><c>SFEP_Federaciones.IdFederacion</c> (ficha <c>/api/v1/federacion/federaciones-autonomicas/{id}</c>).</param>
/// <param name="Territorio">Comunidad o ciudad autónoma (texto corto para índices).</param>
/// <param name="Nombre">Nombre oficial de la federación.</param>
/// <param name="Pruebas">Pruebas en el orden de la federación (cronológico). Vacía si no ha comunicado ninguna.</param>
public sealed record FederacionCalendarioDto(
    string Clave,
    int? IdFederacion,
    string Territorio,
    string Nombre,
    IReadOnlyList<PruebaCalendarioDto> Pruebas);

/// <summary>Prueba autonómica puntuable.</summary>
/// <param name="Orden">Número de la prueba dentro de su federación.</param>
/// <param name="Fechas">Fechas tal como las comunica la federación (p. ej. "13, 14, 15, 20, 21 y 22 de febrero").</param>
/// <param name="FechaInicio">Primer día de la prueba.</param>
/// <param name="FechaFin">Último día de la prueba (igual a <c>FechaInicio</c> si es de un día).</param>
/// <param name="Nombre">Nombre de la prueba o del circuito, si se conoce.</param>
/// <param name="Sede">Club o lugar de celebración, si se conoce.</param>
/// <param name="CampeonatoAutonomico">Otorga el título de campeón autonómico (la participación puede estar restringida).</param>
/// <param name="EnRanking">Incluida actualmente en el ranking FEP de la categoría.</param>
/// <param name="Fases">Fases con fechas y sedes propias (p. ej. un campeonato por categorías). Vacía si no tiene.</param>
public sealed record PruebaCalendarioDto(
    int Orden,
    string Fechas,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    string? Nombre,
    string? Sede,
    bool CampeonatoAutonomico,
    bool EnRanking,
    IReadOnlyList<FasePruebaCalendarioDto> Fases);

/// <summary>Fase de una prueba con fechas o sede distintas.</summary>
/// <param name="Fechas">Fechas tal como las comunica la federación.</param>
/// <param name="FechaInicio">Primer día de la fase.</param>
/// <param name="FechaFin">Último día de la fase.</param>
/// <param name="Descripcion">Qué se juega en la fase (categorías…).</param>
/// <param name="Sede">Club o lugar de celebración.</param>
public sealed record FasePruebaCalendarioDto(
    string Fechas,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    string? Descripcion,
    string? Sede);

/// <summary>Categorías de calendario autonómico publicadas y su página en la web actual.</summary>
public static class ReglasCalendariosAutonomicos
{
    /// <summary>
    /// Categorías admitidas en la ruta y página equivalente en la web actual (ASP clásico). El contenido de cada
    /// una está en <c>Contenidos/calendarios-autonomicos/{categoria}.json</c>; si aún no existe, la API responde 404.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> PaginasWebActual = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["menores"] = "Calendario_Menores_FFAA",
        ["veteranos"] = "Calendario_Veteranos_FFAA",
    };

    /// <summary>Normaliza la categoría (minúsculas, sin espacios) o devuelve <c>null</c> si no es una de las admitidas.</summary>
    public static string? Normalizar(string? categoria)
    {
        var valor = categoria?.Trim().ToLowerInvariant();
        return valor is not null && PaginasWebActual.ContainsKey(valor) ? valor : null;
    }
}
