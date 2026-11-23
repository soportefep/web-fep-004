using System.Text.RegularExpressions;

namespace FEP.Api.Contracts.Clubes;

/// <summary>
/// Ficha de un club federado (sustituye a <c>Club.asp?id={id}</c>): datos de contacto, licencias de jugador/a del
/// año en curso (autonómicas y de competición nacional) y torneos federados organizados por el club.
/// </summary>
/// <param name="Id"><c>SFEP_Clubes.IdClub</c> como texto (hay códigos con ceros a la izquierda).</param>
/// <param name="Nombre">Nombre del club (<c>Club</c>).</param>
/// <param name="ProvinciaId">Provincia del club, si existe en <c>SFEP_Provincias</c>.</param>
/// <param name="Provincia">Nombre de la provincia (en mayúsculas, como en la BD).</param>
/// <param name="FederacionId">Federación autonómica de la comunidad del club (1–22), si la tiene.</param>
/// <param name="Federacion">Abreviatura de esa federación (<c>ANDALUZA</c>, <c>VASCA</c>…).</param>
/// <param name="Presidente">Presidente/a (<c>Presidente</c>).</param>
/// <param name="Direccion">Calle y número (<c>Direccion</c>), sin código postal ni localidad.</param>
/// <param name="CodigoPostal"><c>CP</c>.</param>
/// <param name="Localidad"><c>Localidad</c>.</param>
/// <param name="Telefonos"><c>Telefono</c> y <c>Telefono2</c> no vacíos y sin repetir.</param>
/// <param name="Email"><c>Email</c>.</param>
/// <param name="Web">Web del club con esquema (el ASP anteponía <c>http://</c>; aquí <c>https://</c> si no lo trae).</param>
/// <param name="AnioLicencias">Año de las licencias listadas (el año en curso, como <c>year(now())</c>).</param>
/// <param name="LicenciasAutonomicas">Licencias de jugador/a tramitadas con este club como club autonómico.</param>
/// <param name="LicenciasNacionales">Licencias de jugador/a tramitadas con este club como club de competición nacional.</param>
/// <param name="Torneos">Torneos visibles organizados por el club, del más reciente al más antiguo.</param>
public sealed record ClubDetalleDto(
    string Id,
    string Nombre,
    int? ProvinciaId,
    string? Provincia,
    int? FederacionId,
    string? Federacion,
    string? Presidente,
    string? Direccion,
    string? CodigoPostal,
    string? Localidad,
    IReadOnlyList<string> Telefonos,
    string? Email,
    string? Web,
    int AnioLicencias,
    IReadOnlyList<LicenciaClubDto> LicenciasAutonomicas,
    IReadOnlyList<LicenciaClubDto> LicenciasNacionales,
    IReadOnlyList<TorneoClubDto> Torneos);

/// <summary>Licencia de jugador/a en la ficha del club.</summary>
/// <param name="Licencia">Número de licencia con ceros a la izquierda hasta 6 cifras (como el ASP: <c>095625</c>).</param>
/// <param name="Jugador">"APELLIDO1 APELLIDO2, NOMBRE", como en la web actual.</param>
/// <param name="Genero"><c>M</c>, <c>F</c> o <c>null</c> si no consta.</param>
/// <param name="Categoria">
/// Categoría por edad abreviada (<c>BEN</c>, <c>ALE</c>, <c>INF</c>, <c>CAD</c>, <c>JUN</c>, <c>SUB-23</c>, <c>SEN</c>,
/// <c>CAT-35</c>…<c>CAT-55</c>); <c>null</c> sin fecha de nacimiento. La fecha de nacimiento nunca se publica.
/// </param>
public sealed record LicenciaClubDto(string Licencia, string Jugador, string? Genero, string? Categoria);

/// <summary>Torneo federado organizado por el club.</summary>
/// <param name="IdPrueba"><c>SFEP_Torneos.IdPrueba</c>.</param>
/// <param name="FederacionId">Federación organizadora (0 = FEP).</param>
/// <param name="Anio">Año de la fecha de fin.</param>
/// <param name="Mes">Mes de la fecha de fin (1–12).</param>
/// <param name="Torneo">Nombre del torneo.</param>
/// <param name="Modalidad"><c>absoluto</c>, <c>menores</c>, <c>veteranos</c> o <c>null</c> si no consta.</param>
/// <param name="Url">
/// Enlace del torneo: la ficha en la web actual (<c>torneo.asp?Idtorneo=</c>) si es de la FEP, o la web de la
/// federación autonómica. <c>null</c> si no hay a dónde enlazar.
/// </param>
public sealed record TorneoClubDto(int IdPrueba, int FederacionId, int Anio, int Mes, string Torneo, string? Modalidad, string? Url);

/// <summary>Reglas de <c>Club.asp</c> trasladadas a la API.</summary>
public static partial class ReglasFichaClub
{
    /// <summary>Longitud máxima del id de club en la ruta.</summary>
    public const int LongitudMaximaId = 10;

    /// <summary>Los ids de club son solo dígitos (con ceros a la izquierda): <c>1501001</c>, <c>0118003</c>.</summary>
    public static bool IdValido(string? id) => id is not null && IdClub().IsMatch(id);

    /// <summary>Ruta de la ficha en la web actual.</summary>
    public static string RutaWebActual(string id) => $"Club.asp?id={Uri.EscapeDataString(id)}";

    /// <summary>
    /// Categoría abreviada por edad (<c>year(now()) - year(FechaNac)</c>), igual que <c>Club.asp</c>. A diferencia de
    /// <c>Estadisticas.asp</c>, aquí una edad de 0 o negativa (fecha de nacimiento no válida) cae en <c>BEN</c>, como en
    /// el ASP. Sin año de nacimiento → <c>null</c> (el ASP fallaba).
    /// </summary>
    public static string? CategoriaAbreviada(int? anioNacimiento, int anioActual)
    {
        if (anioNacimiento is not { } nacimiento)
        {
            return null;
        }

        return (anioActual - nacimiento) switch
        {
            < 11 => "BEN",
            <= 12 => "ALE",
            <= 14 => "INF",
            <= 16 => "CAD",
            <= 18 => "JUN",
            <= 23 => "SUB-23",
            <= 34 => "SEN",
            <= 39 => "CAT-35",
            <= 44 => "CAT-40",
            <= 49 => "CAT-45",
            <= 54 => "CAT-50",
            _ => "CAT-55",
        };
    }

    /// <summary>Número de licencia rellenado con ceros hasta 6 cifras (el <c>select case len()</c> del ASP).</summary>
    public static string NumeroLicencia(string? licencia)
    {
        var limpio = licencia?.Trim() ?? string.Empty;
        return limpio.Length < 6 ? limpio.PadLeft(6, '0') : limpio;
    }

    /// <summary>"APELLIDO1 APELLIDO2, NOMBRE" sin espacios sobrantes si falta el segundo apellido o el nombre.</summary>
    public static string Jugador(string? apellido1, string? apellido2, string? nombre)
    {
        var apellidos = string.Join(' ', new[] { apellido1, apellido2 }
            .Select(a => a?.Trim())
            .Where(a => !string.IsNullOrEmpty(a)));
        var n = nombre?.Trim();
        return string.IsNullOrEmpty(n) ? apellidos : string.IsNullOrEmpty(apellidos) ? n : $"{apellidos}, {n}";
    }

    /// <summary><c>ModalidadTorneo</c> (<c>ABSOLUTO</c>, <c>MENORES</c>, <c>VETERANOS</c>) en minúsculas; otra → <c>null</c>.</summary>
    public static string? Modalidad(string? modalidadTorneo) => modalidadTorneo?.Trim().ToUpperInvariant() switch
    {
        "ABSOLUTO" => "absoluto",
        "MENORES" => "menores",
        "VETERANOS" => "veteranos",
        _ => null,
    };

    /// <summary><c>M</c> o <c>F</c>; cualquier otro valor → <c>null</c>.</summary>
    public static string? Genero(string? genero) => genero?.Trim().ToUpperInvariant() switch
    {
        "M" => "M",
        "F" => "F",
        _ => null,
    };

    [GeneratedRegex(@"^\d{1,10}$")]
    private static partial Regex IdClub();
}
