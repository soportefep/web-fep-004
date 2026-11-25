using System.Text.RegularExpressions;
using FEP.Api.Contracts.Clubes;

namespace FEP.Api.Contracts.Competicion;

/// <summary>
/// Reglas heredadas de <c>Organizacion_Torneos.asp</c> (convocatoria para organizar las pruebas del calendario FEP),
/// <c>cargarfechastorneo.asp</c> (fechas y fianza del torneo elegido), <c>comboProvincias.asp</c> y
/// <c>comboclubesLicitacion.asp</c> (desplegables encadenados del formulario).
/// </summary>
public static partial class ReglasOrganizacionTorneos
{
    /// <summary>Solo se licitan pruebas del calendario nacional (<c>SFEP_Calendario.Federacion = 0</c>).</summary>
    public const int IdFederacionFep = ReglasCalendario.IdFederacionFep;

    /// <summary>Comunidades del desplegable: <c>IdComunidad &lt; 20</c> (las 19 autonómicas, Ceuta y Melilla incluidas).</summary>
    public const int IdComunidadLimite = 20;

    /// <summary>Rango admitido para <c>provincia</c> en el desplegable de clubes (el mismo que el buscador de clubes).</summary>
    public const int ProvinciaMinima = BusquedaClubes.ProvinciaMinima, ProvinciaMaxima = BusquedaClubes.ProvinciaMaxima;

    /// <summary>
    /// Grupos del desplegable "Selecciona el torneo", en el orden del ASP, con las categorías
    /// (<c>SFEP_Calendario.Categoria</c>) de cada una de sus cuatro consultas (<c>sql_menores</c>, <c>sql_abs</c>,
    /// <c>sql_vet</c>, <c>sql_silla</c>). Las pruebas licitables de otras categorías salían en el calendario lateral
    /// (<c>BarraLateralLicitacion.inc</c>) pero no se podían elegir; se mantiene.
    /// </summary>
    public static IReadOnlyList<(GrupoTorneosLicitablesDto Grupo, int[] Categorias)> Grupos { get; } =
    [
        (new GrupoTorneosLicitablesDto("menores", "Torneos de menores"), new[] { 3, 4 }),
        (new GrupoTorneosLicitablesDto("absolutos", "Torneos absolutos y sub-23"), new[] { 1, 2, 5 }),
        (new GrupoTorneosLicitablesDto("veteranos", "Torneos de veteranos"), new[] { 6, 7 }),
        (new GrupoTorneosLicitablesDto("silla", "Torneos de pádel en silla"), new[] { 10 }),
    ];

    /// <summary>Clave del grupo de una categoría; <c>null</c> si no está en ninguno.</summary>
    public static string? GrupoDe(int? categoria)
        => categoria is { } c ? Grupos.FirstOrDefault(g => g.Categorias.Contains(c)).Grupo?.Clave : null;

    /// <summary>
    /// Fianza publicada: <c>cargarfechastorneo.asp</c> mostraba "A Negociar directamente con la FEP" con <c>Fianza = 0</c>
    /// y " euros" (sin cifra) con <c>Fianza</c> vacía. Las dos se tratan como "a negociar" (<c>null</c>).
    /// </summary>
    public static decimal? Fianza(decimal? fianza) => fianza is > 0 ? fianza : null;

    /// <summary>
    /// Título de la prueba sin espacios sobrantes: en la BD hay nombres con espacio inicial y con tabuladores
    /// ("*Circuito Nacional de Menores 7\t— acceso…").
    /// </summary>
    public static string Titulo(string? campeonato) => Espacios().Replace(campeonato ?? string.Empty, " ").Trim();

    /// <summary>
    /// La convocatoria está abierta hasta <paramref name="fechaLimite"/> (hora local de España). El ASP comparaba
    /// <c>CDate(now()) &gt; CDate("20/11/26 14:00:00")</c> con la hora del servidor y el formato regional; aquí con la
    /// hora de Madrid, sea cual sea la zona horaria del servidor. Sin fecha límite, abierta.
    /// </summary>
    public static bool Abierta(DateTime? fechaLimite, TimeProvider reloj)
        => fechaLimite is not { } limite || AhoraEnMadrid(reloj) < limite;

    /// <summary>Hora local de Madrid (<see cref="ReglasCalendario.ZonaHorariaFep"/>) sin zona.</summary>
    public static DateTime AhoraEnMadrid(TimeProvider reloj)
        => TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), ZonaMadrid.Value).DateTime;

    private static readonly Lazy<TimeZoneInfo> ZonaMadrid = new(() =>
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ReglasCalendario.ZonaHorariaFep);
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Windows sin ICU: el id de Windows de la hora peninsular.
            return TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
        }
    });

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacios();
}
