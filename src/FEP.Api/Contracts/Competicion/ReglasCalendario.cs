using FEP.Api.Contracts.Federacion;

namespace FEP.Api.Contracts.Competicion;

/// <summary>
/// Reglas heredadas de <c>CalendarioFEP.asp</c> (calendario oficial de competición de la FEP) y de
/// <c>CalendarioFT.asp</c> (el mismo calendario para las federaciones territoriales).
/// </summary>
public static class ReglasCalendario
{
    /// <summary>
    /// <c>SFEP_Calendario.Federacion</c> del calendario nacional (el ASP usaba <c>fed = 0</c> por defecto). Las demás
    /// federaciones son el calendario territorial (<c>CalendarioFT.asp</c>,
    /// <c>/api/v1/competicion/calendario/federaciones/{id}</c>).
    /// </summary>
    public const int IdFederacionFep = 0;

    /// <summary>
    /// Primer id de federación territorial. El ASP listaba <c>IdFederacion BETWEEN 1 AND 21</c> y dejaba fuera a
    /// Castilla-La Mancha (22). DECIDIDO por la FEP (13/11/2026): hay 22 federaciones, de la 1 a la 22, ambas incluidas.
    /// </summary>
    public const int IdFederacionTerritorialMinimo = ReglasFederaciones.IdMinimo;

    /// <summary>Último id de federación territorial: Castilla-La Mancha (22; <see cref="ReglasFederaciones"/>).</summary>
    public const int IdFederacionTerritorialMaximo = ReglasFederaciones.IdMaximo;

    /// <summary>
    /// Zona horaria de referencia para el cambio de temporada (hora peninsular española), con independencia de la
    /// zona horaria del servidor. Id IANA; .NET lo resuelve también en Windows.
    /// </summary>
    public const string ZonaHorariaFep = "Europe/Madrid";

    /// <summary>El desplegable de temporadas solo mostraba las posteriores a 2014 (<c>año &gt; 2014</c>).</summary>
    public const int AnioPublicadoDesde = 2014;

    /// <summary>
    /// Calendarios territoriales: <c>CalendarioFT.asp</c> no filtraba años; se listan todas las temporadas que se
    /// pueden pedir con <c>anio</c> (≥ <see cref="ReglasCampeonatos.AnioMinimo"/>).
    /// </summary>
    public const int AnioPublicadoDesdeTerritorial = ReglasCampeonatos.AnioMinimo - 1;

    /// <summary>
    /// Año en curso en hora peninsular española (<see cref="ZonaHorariaFep"/>): la temporada por defecto cambia
    /// automáticamente a las 00:00 del 1 de enero de Madrid, aunque el servidor esté en otra zona horaria (en UTC,
    /// <c>GetLocalNow()</c> cambiaría de año a la 01:00 o las 02:00 de Madrid).
    /// </summary>
    /// <param name="reloj">Reloj de la aplicación (inyectable en las pruebas).</param>
    public static int AnioEnCurso(TimeProvider reloj)
        => TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), ZonaMadrid.Value).Year;

    private static readonly Lazy<TimeZoneInfo> ZonaMadrid = new(() =>
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ZonaHorariaFep);
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Windows sin ICU: el id de Windows de la hora peninsular.
            return TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
        }
    });

    /// <summary>
    /// Temporada que se muestra sin parámetro <c>anio</c>. El ASP la tenía escrita a mano (<c>ano = 2026</c>, con
    /// el código que elegía la más reciente comentado) para no abrir la temporada siguiente, que se publica en
    /// licitación meses antes. DECIDIDO por la FEP (13/11/2026): cambio automático el 1 de enero de cada año. Aquí:
    /// la del año en curso (<see cref="AnioEnCurso"/>) si está publicada; si no, la más reciente anterior; si
    /// tampoco hay, la más próxima. <c>null</c> si no hay ninguna.
    /// </summary>
    /// <param name="anios">Temporadas publicadas, en cualquier orden.</param>
    /// <param name="anioActual">Año en curso (hora local del servidor).</param>
    public static int? TemporadaPorDefecto(IReadOnlyCollection<int> anios, int anioActual)
    {
        if (anios.Count == 0)
        {
            return null;
        }

        if (anios.Contains(anioActual))
        {
            return anioActual;
        }

        var anteriores = anios.Where(a => a < anioActual).ToArray();
        return anteriores.Length > 0 ? anteriores.Max() : anios.Min();
    }

    /// <summary>
    /// Prueba sin fecha real: sin <c>FechaIni</c> (bloque "SIN FECHA ASIGNADA" del ASP) o con la fecha comodín de
    /// todo el año (1 de enero – 31 de diciembre del mismo año), que la gestión usa para las pruebas en licitación
    /// o por designar (el ASP pintaba "1 Ene – 31 Dic" en enero).
    /// </summary>
    public static bool FechaPorConfirmar(DateTime? inicio, DateTime? fin)
        => inicio is not { } ini
           || (ini.Month == 1 && ini.Day == 1
               && fin is { } f && f.Year == ini.Year && f.Month == 12 && f.Day == 31);

    /// <summary>
    /// El ASP solo enlazaba la prueba si tenía torneo (<c>IdPrueba &lt;&gt; 0</c>) y su <c>Link</c> no era <c>"#"</c>
    /// (con <c>Link</c> vacío o <c>NULL</c> sí enlazaba).
    /// </summary>
    public static bool Enlazable(int? idPrueba, string? link)
        => idPrueba is > 0 && link?.Trim() != "#";
}
