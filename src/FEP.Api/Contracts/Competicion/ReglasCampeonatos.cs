namespace FEP.Api.Contracts.Competicion;

/// <summary>Criterios ya validados del listado de campeonatos.</summary>
/// <param name="Anio">Temporada pedida o <c>null</c> (la más reciente con torneos).</param>
/// <param name="Tipo">Clave de tipo (<c>parejas</c> | <c>equipos</c>) o <c>null</c>.</param>
/// <param name="Categoria">Clave de categoría (<c>absoluto</c> | <c>menores</c> | <c>veteranos</c>) o <c>null</c>.</param>
public sealed record BusquedaCampeonatos(int? Anio, string? Tipo, string? Categoria);

/// <summary>
/// Reglas heredadas de <c>Campeonatos.asp</c> y <c>Torneos_Previo.asp</c> (no se llama <c>Campeonatos</c>
/// para no chocar con nombres de controladores y repositorios).
/// </summary>
public static class ReglasCampeonatos
{
    /// <summary>Rango admitido para <c>anio</c> (el ASP listaba las temporadas con <c>Año &gt; 1900</c>).</summary>
    public const int AnioMinimo = 1901, AnioMaximo = 2100;

    /// <summary>
    /// Campeonato de España de Menores 2021 (<c>IdPrueba = 19721</c>): el ASP lo añadía siempre a su
    /// temporada (con <c>OR</c>, sin filtros de visibilidad) y excluía los torneos ligados a él
    /// (<c>TorneoLigadoFEP &lt;&gt; 19721</c>). <c>Torneos_Previo.asp</c> lo mandaba a su web propia.
    /// </summary>
    public const int IdCampeonatoMenores2021 = 19721;

    /// <summary>Web del Campeonato de España de Menores 2021 (solo responde por http).</summary>
    public const string UrlCampeonatoMenores2021 = "http://circuitomenores.padelfederacion.es/CEM-2021";

    /// <summary>Ficha de los torneos de menores por parejas en la web del circuito de menores (solo http).</summary>
    public const string UrlCircuitoMenores = "http://circuitomenores.padelfederacion.es/Campeonato?IdTorneo=";

    /// <summary>Clave pública del tipo → valor de <c>SFEP_Torneos.TipoTorneo</c>.</summary>
    public static readonly IReadOnlyDictionary<string, string> Tipos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["parejas"] = "PAREJAS",
        ["equipos"] = "EQUIPOS",
    };

    /// <summary>
    /// Clave pública de la categoría → valor de <c>SFEP_Torneos.ModalidadTorneo</c> con el que filtraba el
    /// ASP (igualdad exacta: <c>absoluto</c> no incluye <c>SEL_ABS</c>, como en la web actual).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Categorias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["absoluto"] = "ABSOLUTO",
        ["menores"] = "MENORES",
        ["veteranos"] = "VETERANOS",
    };

    /// <summary>Clave pública normalizada (minúsculas) de un tipo admitido, o <c>null</c> si no lo es.</summary>
    public static string? ClaveTipo(string? valor)
        => valor is not null && Tipos.ContainsKey(valor.Trim()) ? valor.Trim().ToLowerInvariant() : null;

    /// <summary>Clave pública normalizada (minúsculas) de una categoría admitida, o <c>null</c> si no lo es.</summary>
    public static string? ClaveCategoria(string? valor)
        => valor is not null && Categorias.ContainsKey(valor.Trim()) ? valor.Trim().ToLowerInvariant() : null;

    /// <summary><c>TipoTorneo</c> de la BD → <c>parejas</c> | <c>equipos</c> (otro valor: en minúsculas tal cual).</summary>
    public static string TipoDesdeBd(string? tipoTorneo)
        => (tipoTorneo?.Trim() ?? string.Empty).ToUpperInvariant() switch
        {
            "PAREJAS" => "parejas",
            "EQUIPOS" => "equipos",
            var otro => otro.ToLowerInvariant(),
        };

    /// <summary>
    /// <c>ModalidadTorneo</c> de la BD → categoría pública y si es de selecciones autonómicas
    /// (mismos colores que el <c>select case</c> del ASP).
    /// </summary>
    public static (string? Categoria, bool Seleccion) CategoriaDesdeBd(string? modalidad)
        => (modalidad?.Trim() ?? string.Empty).ToUpperInvariant() switch
        {
            "ABSOLUTO" => ("absoluto", false),
            "SEL_ABS" => ("absoluto", true),
            "MENORES" => ("menores", false),
            "SEL_MEN" => ("menores", true),
            "VETERANOS" => ("veteranos", false),
            "SEL_VET" => ("veteranos", true),
            _ => (null, false),
        };

    /// <summary>Pádel en silla de ruedas: el ASP solo lo marcaba en <c>ABSOLUTO</c> con <c>Circuito = 'SILLA'</c>.</summary>
    public static bool EsSillaRuedas(string? modalidad, string? circuito)
        => string.Equals(modalidad?.Trim(), "ABSOLUTO", StringComparison.OrdinalIgnoreCase)
           && string.Equals(circuito?.Trim(), "SILLA", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Plazo de inscripción abierto respecto a <paramref name="ahora"/> (bloque "Fecha Tope…" del ASP):
    /// <list type="bullet">
    /// <item>Equipos con la preinscripción abierta: preinscripción (o inscripción de equipos si ambas fechas coinciden).</item>
    /// <item>Con la inscripción abierta: inscripción (de equipos, si lo es).</item>
    /// <item>Corrección: si solo sigue abierta la preinscripción de un torneo por parejas, el ASP pintaba la
    /// fecha de inscripción ya pasada; aquí se devuelve la de preinscripción.</item>
    /// </list>
    /// </summary>
    public static PlazoInscripcionDto? Plazo(string tipo, DateTime? inscripcion, DateTime? preinscripcion, DateTime ahora)
    {
        var equipos = tipo == "equipos";
        var preAbierta = preinscripcion is { } pre && pre > ahora;
        var inscAbierta = inscripcion is { } insc && insc > ahora;

        if (preAbierta && equipos)
        {
            return new PlazoInscripcionDto(preinscripcion == inscripcion ? "inscripcion-equipos" : "preinscripcion", preinscripcion!.Value);
        }

        if (inscAbierta)
        {
            return new PlazoInscripcionDto(equipos ? "inscripcion-equipos" : "inscripcion", inscripcion!.Value);
        }

        return preAbierta ? new PlazoInscripcionDto("preinscripcion", preinscripcion!.Value) : null;
    }

    /// <summary>
    /// Enlace a los cuadros (<c>LinkCuadros</c>, guardado sin esquema): el ASP le anteponía <c>https:\\</c>
    /// (con barras invertidas). Devuelve la URL absoluta http(s) o <c>null</c> si no hay o no es válida.
    /// </summary>
    public static string? EnlaceCuadros(string? linkCuadros)
    {
        var limpio = linkCuadros?.Trim();
        if (string.IsNullOrEmpty(limpio) || limpio == "#")
        {
            return null;
        }

        limpio = limpio.Replace('\\', '/');
        if (!limpio.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !limpio.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            limpio = "https://" + limpio.TrimStart('/');
        }

        return Uri.TryCreate(limpio, UriKind.Absolute, out var url)
               && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
               && !string.IsNullOrEmpty(url.Host)
            ? url.AbsoluteUri
            : null;
    }

    /// <summary>
    /// Ficha del torneo según <c>Torneos_Previo.asp</c> (al que el ASP enviaba con un formulario POST):
    /// ruta relativa a la web actual (<c>Camp_*.asp?IdTorneo=</c>) o URL absoluta del circuito de menores.
    /// </summary>
    public static string RutaFicha(int id, string? modalidad, string? tipoTorneo, int? torneoPadre, string? liga, string? empresas)
    {
        var equipos = string.Equals(tipoTorneo?.Trim(), "EQUIPOS", StringComparison.OrdinalIgnoreCase);
        if (string.Equals(modalidad?.Trim(), "MENORES", StringComparison.OrdinalIgnoreCase))
        {
            if (torneoPadre is { } padre && padre != 0)
            {
                return id == IdCampeonatoMenores2021 ? UrlCampeonatoMenores2021 : $"Camp_Clasificatorio.asp?IdTorneo={id}";
            }

            return equipos ? $"Camp_Equipos.asp?IdTorneo={id}" : UrlCircuitoMenores + id;
        }

        if (equipos)
        {
            return EsSi(empresas) ? $"Empr_Equipos.asp?IdTorneo={id}" : $"Camp_Equipos.asp?IdTorneo={id}";
        }

        return EsSi(liga) ? $"Camp_Liga.asp?IdTorneo={id}" : $"Camp_Parejas.asp?IdTorneo={id}";
    }

    private static bool EsSi(string? valor) => string.Equals(valor?.Trim(), "S", StringComparison.OrdinalIgnoreCase);
}
