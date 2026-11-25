using System.Globalization;
using FEP.Api.Contracts.Clubes;
using FEP.Api.Contracts.Federacion;

namespace FEP.Api.Contracts.Licencias;

/// <summary>Tipo de licencia del buscador (desplegable "Tipo Licencia" de <c>Licencias_Buscar.asp</c>).</summary>
public enum TipoLicencia
{
    /// <summary>Jugador/a (<c>J</c>): <c>SFEP_Fichas.LicenciaJugador</c>, última temporada (<c>JugadorUltimoAno</c>) y federación.</summary>
    Jugador,

    /// <summary>Técnico/a (<c>T</c>): <c>SFEP_Fichas.LicenciaTecnico</c> no nula, sin federación ni temporada.</summary>
    Tecnico,

    /// <summary>Juez-árbitro (<c>A</c>): <c>SFEP_Fichas.LicenciaArbitro</c> no nula, sin federación ni temporada.</summary>
    Arbitro,

    /// <summary>Pádel en silla de ruedas (<c>S</c>): <c>SFEP_Fichas.LicenciaSilla</c>, última temporada (<c>SillaUltimoAno</c>).</summary>
    SillaRuedas,
}

/// <summary>Criterios ya validados del buscador de licencias.</summary>
/// <param name="Tipo">Tipo de licencia.</param>
/// <param name="Federacion">
/// Federación que expidió la licencia (<c>SFEP_Licencia.Federacion</c>, 1–22): obligatoria para jugador/a, opcional en
/// silla de ruedas y <c>null</c> en técnico/a y juez-árbitro (el ASP no la usaba).
/// </param>
/// <param name="Terminos">Términos de nombre y apellidos: <c>NombreCompleto</c> debe contenerlos todos.</param>
/// <param name="FechaNacimiento">Fecha de nacimiento exacta, si se ha indicado.</param>
public sealed record BusquedaLicencias(
    TipoLicencia Tipo,
    int? Federacion,
    IReadOnlyList<string> Terminos,
    DateOnly? FechaNacimiento);

/// <summary>Reglas del buscador de licencias (origen: <c>Licencias_Buscar.asp</c>).</summary>
public static class ReglasLicencias
{
    /// <summary>
    /// Resultados máximos por búsqueda. El ASP no tenía límite (buscar "A" listaba miles de personas con su licencia):
    /// DECISIÓN FEP. Si hay más, la respuesta trae <c>total</c> para pedir que se afine la búsqueda.
    /// </summary>
    public const int ResultadosMaximos = 100;

    /// <summary>Longitud máxima del texto buscado.</summary>
    public const int LongitudMaximaBusqueda = 100;

    /// <summary>Longitud mínima del texto buscado (sin espacios ni guiones), para no listar a todo el mundo con una letra.</summary>
    public const int LongitudMinimaBusqueda = 2;

    /// <summary>Número máximo de términos del texto buscado (como el buscador por nombre de los rankings).</summary>
    public const int TerminosMaximos = 6;

    /// <summary>Primer año de nacimiento admitido (el desplegable del ASP empezaba en 1930).</summary>
    public const int AnioNacimientoMinimo = 1900;

    /// <summary>Rango de federaciones autonómicas (<see cref="ReglasFederaciones"/>, DECIDIDO por la FEP: 1–22).</summary>
    public const int FederacionMinima = ReglasFederaciones.IdMinimo, FederacionMaxima = ReglasFederaciones.IdMaximo;

    /// <summary>Prefijo con el que se muestran las licencias de silla de ruedas (<c>"PS" &amp; lic</c> en el ASP).</summary>
    public const string PrefijoSilla = "PS";

    /// <summary>
    /// Tipo a partir del parámetro <c>tipo</c>: clave (<c>jugador</c>, <c>tecnico</c>, <c>arbitro</c>,
    /// <c>silla-ruedas</c>) o letra del ASP (<c>J</c>, <c>T</c>, <c>A</c>, <c>S</c>), sin distinguir mayúsculas.
    /// Vacío → jugador/a (opción por defecto del ASP).
    /// </summary>
    public static bool TryParseTipo(string? valor, out TipoLicencia tipo)
    {
        tipo = TipoLicencia.Jugador;
        var v = valor?.Trim().ToLowerInvariant();
        switch (v)
        {
            case null or "" or "j" or "jugador":
                return true;
            case "t" or "tecnico" or "técnico":
                tipo = TipoLicencia.Tecnico;
                return true;
            case "a" or "arbitro" or "árbitro" or "juez-arbitro":
                tipo = TipoLicencia.Arbitro;
                return true;
            case "s" or "silla" or "silla-ruedas":
                tipo = TipoLicencia.SillaRuedas;
                return true;
            default:
                return false;
        }
    }

    /// <summary>Clave pública del tipo (la que devuelve la API).</summary>
    public static string Clave(TipoLicencia tipo) => tipo switch
    {
        TipoLicencia.Tecnico => "tecnico",
        TipoLicencia.Arbitro => "arbitro",
        TipoLicencia.SillaRuedas => "silla-ruedas",
        _ => "jugador",
    };

    /// <summary>¿El tipo usa la federación? Jugador/a (obligatoria) y silla de ruedas (opcional).</summary>
    public static bool UsaFederacion(TipoLicencia tipo) => tipo is TipoLicencia.Jugador or TipoLicencia.SillaRuedas;

    /// <summary>¿La federación es obligatoria? Solo para jugador/a (el ASP avisaba "Selecciona a que Federación…").</summary>
    public static bool FederacionObligatoria(TipoLicencia tipo) => tipo == TipoLicencia.Jugador;

    /// <summary>Términos del texto (espacios y guiones, como el ASP), reutilizando la regla del buscador de clubes.</summary>
    public static IReadOnlyList<string> DividirTerminos(string? texto) => BusquedaClubes.DividirTerminos(texto);

    /// <summary>Escapa los comodines de <c>LIKE</c> (<c>%</c>, <c>_</c>, <c>[</c>).</summary>
    public static string EscaparLike(string termino) => BusquedaClubes.EscaparLike(termino);

    /// <summary>Licencia tal como se muestra: con <c>PS</c> delante en silla de ruedas, como el ASP.</summary>
    public static string LicenciaVisible(TipoLicencia tipo, string licencia)
        => tipo == TipoLicencia.SillaRuedas ? PrefijoSilla + licencia : licencia;

    /// <summary>
    /// Ruta de las estadísticas en la web actual: solo para jugador/a, como el ASP (<c>Estadisticas.asp?Id={lic}</c>,
    /// migrada a <c>estadisticas.html?licencia=</c>; el front la traslada). Licencia no numérica → <c>null</c>.
    /// </summary>
    public static string? RutaEstadisticas(TipoLicencia tipo, string licencia)
        => tipo == TipoLicencia.Jugador && int.TryParse(licencia, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0
            ? string.Create(CultureInfo.InvariantCulture, $"Estadisticas?id={n}")
            : null;
}
