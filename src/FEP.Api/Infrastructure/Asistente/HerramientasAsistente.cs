using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FEP.Api.Contracts.Asistente;
using FEP.Api.Contracts.Competicion;
using FEP.Api.Data.Repositories.Asistente;
using FEP.Api.Data.Repositories.Competicion;

namespace FEP.Api.Infrastructure.Asistente;

/// <summary>Definición de una herramienta que el modelo puede usar (nombre, descripción y JSON Schema de la entrada).</summary>
public sealed record DefinicionHerramienta(string Nombre, string Descripcion, JsonElement Esquema);

/// <summary>Resultado de ejecutar una herramienta: texto para el modelo y fuentes para el usuario.</summary>
public sealed record ResultadoHerramienta(string Contenido, bool EsError, IReadOnlyList<FuenteChatDto> Fuentes)
{
    /// <summary>Error que el modelo debe leer (p. ej. un filtro no válido) para corregir la llamada o explicarlo.</summary>
    public static ResultadoHerramienta Error(string mensaje) => new(mensaje, true, []);
}

/// <summary>
/// Herramientas del asistente: buscar en la normativa, listar documentos, consultar un ranking y las estadísticas de un
/// jugador. Se ejecutan dentro de la API con los mismos repositorios (solo lectura) que los endpoints públicos, así que
/// el asistente solo ve lo que ya publica la web. No depende del SDK de Anthropic: se prueba sin red.
/// </summary>
public sealed class HerramientasAsistente(
    ICorpusNormativa corpus,
    IClasificacionesRankingRepository clasificaciones,
    IEstadisticasJugadorRepository estadisticas,
    TimeProvider reloj,
    ILogger<HerramientasAsistente> logger)
{
    /// <summary>Nombre de la herramienta de búsqueda en documentos.</summary>
    public const string BuscarDocumentos = "buscar_documentos";

    /// <summary>Nombre de la herramienta que lista el catálogo.</summary>
    public const string ListarDocumentos = "listar_documentos";

    /// <summary>Nombre de la herramienta de rankings.</summary>
    public const string ConsultarRanking = "consultar_ranking";

    /// <summary>Nombre de la herramienta de estadísticas de jugador.</summary>
    public const string EstadisticasJugador = "estadisticas_jugador";

    /// <summary>Fragmentos por búsqueda.</summary>
    public const int FragmentosPorBusqueda = 6;

    /// <summary>Puestos por defecto y máximos que devuelve <see cref="ConsultarRanking"/>.</summary>
    public const int PuestosPorDefecto = 10;

    /// <summary>Puestos máximos por lista en <see cref="ConsultarRanking"/> (el ranking completo está en la web).</summary>
    public const int PuestosMaximos = 50;

    /// <summary>Caracteres máximos de un resultado (lo que sobra se corta, para no inflar el contexto).</summary>
    public const int LongitudMaximaResultado = 14_000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    /// <summary>Herramientas, en orden estable (el orden forma parte del prefijo que se cachea).</summary>
    public IReadOnlyList<DefinicionHerramienta> Definiciones { get; } =
    [
        new(BuscarDocumentos,
            "Busca en los documentos oficiales de la FEP (reglamentos, circulares, estatutos, normativa de cálculo de los " +
            "rankings, manual de pistas y memorias deportivas históricas) y devuelve los fragmentos más relevantes con " +
            "su documento y páginas. Búsqueda por palabras (no semántica): usa los términos que aparecerían en un " +
            "reglamento en español (\"sanción incomparecencia\", \"puntuación ranking veteranos\", \"categoría cadete edad\"), " +
            "aunque la pregunta sea en inglés. Si no encuentras lo que buscas, repite con sinónimos u otra categoría.",
            Esquema("""
            {
              "type": "object",
              "properties": {
                "consulta": { "type": "string", "description": "Palabras clave en español." },
                "categoria": {
                  "type": "string",
                  "enum": ["reglamentos", "circulares", "estatutos", "rankings", "instalaciones", "memorias"],
                  "description": "Opcional. memorias = resultados y campeones históricos por año (1992-2025)."
                }
              },
              "required": ["consulta"],
              "additionalProperties": false
            }
            """)),
        new(ListarDocumentos,
            "Lista los documentos disponibles (título, año, si está vigente y descripción). Útil para preguntas como " +
            "\"¿qué circulares se han publicado?\" o para saber qué reglamento consultar.",
            Esquema("""
            {
              "type": "object",
              "properties": {
                "categoria": {
                  "type": "string",
                  "enum": ["reglamentos", "circulares", "estatutos", "rankings", "instalaciones", "memorias"]
                }
              },
              "additionalProperties": false
            }
            """)),
        new(ConsultarRanking,
            "Consulta la clasificación ACTUAL de un ranking nacional FEP (datos en directo de la web): puestos, puntos y " +
            "torneos. Busca por nombre y/o apellidos o por número de licencia, o pide los primeros puestos. " +
            "Rankings: absoluto (Ranking Único, géneros M/F), menores (categorías benjamin, alevin, infantil, cadete, " +
            "junior; M/F), veteranos (categorías 35, 40, 45, 50, 55, 60 = +35…+60; M/F), silla-ruedas (lista única mixta).",
            Esquema("""
            {
              "type": "object",
              "properties": {
                "ranking": { "type": "string", "enum": ["absoluto", "menores", "veteranos", "silla-ruedas"] },
                "genero": { "type": "string", "enum": ["M", "F", "X"], "description": "M masculino, F femenino, X mixto (solo silla-ruedas). Omítelo para todos." },
                "categoria": { "type": "string", "description": "Solo menores (benjamin, alevin, infantil, cadete, junior) y veteranos (35, 40, 45, 50, 55, 60)." },
                "nombre": { "type": "string", "description": "Nombre y/o apellidos (todas las palabras deben aparecer)." },
                "licencia": { "type": "integer", "description": "Número de licencia sin prefijo." },
                "limite": { "type": "integer", "description": "Puestos por lista (1-50, por defecto 10)." }
              },
              "required": ["ranking"],
              "additionalProperties": false
            }
            """)),
        new(EstadisticasJugador,
            "Estadísticas públicas de un jugador o jugadora por su número de licencia: rankings en los que aparece con " +
            "puesto y puntos, pruebas que puntúan, mejor clasificación, balance de partidos del último año y última pareja. " +
            "Si solo tienes el nombre, busca antes la licencia con consultar_ranking.",
            Esquema("""
            {
              "type": "object",
              "properties": {
                "licencia": { "type": "integer", "description": "Número de licencia de jugador/a (sin prefijo)." }
              },
              "required": ["licencia"],
              "additionalProperties": false
            }
            """)),
    ];

    /// <summary>Ejecuta la herramienta <paramref name="nombre"/> con la entrada que ha generado el modelo.</summary>
    public async Task<ResultadoHerramienta> EjecutarAsync(
        string nombre, IReadOnlyDictionary<string, JsonElement> entrada, CancellationToken cancellationToken)
    {
        try
        {
            return nombre switch
            {
                BuscarDocumentos => Buscar(entrada),
                ListarDocumentos => Listar(entrada),
                ConsultarRanking => await RankingAsync(entrada, cancellationToken),
                EstadisticasJugador => await EstadisticasAsync(entrada, cancellationToken),
                _ => ResultadoHerramienta.Error($"Herramienta desconocida: {nombre}."),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Sin BD (o caída), los rankings no están disponibles: el modelo lo explica y enlaza la página del ranking.
            logger.LogWarning(ex, "La herramienta {Herramienta} del asistente ha fallado", nombre);
            return ResultadoHerramienta.Error(
                "Los datos no están disponibles en este momento. Indica al usuario que puede consultarlos en la página " +
                "de rankings de la web o volver a intentarlo más tarde.");
        }
    }

    private ResultadoHerramienta Buscar(IReadOnlyDictionary<string, JsonElement> entrada)
    {
        var consulta = Texto(entrada, "consulta");
        if (string.IsNullOrWhiteSpace(consulta))
        {
            return ResultadoHerramienta.Error("Falta 'consulta'.");
        }

        var categoria = Texto(entrada, "categoria");
        if (categoria is not null && !corpus.Categorias.Contains(categoria, StringComparer.OrdinalIgnoreCase))
        {
            return ResultadoHerramienta.Error($"Categoría no válida. Usa una de: {string.Join(", ", corpus.Categorias)}.");
        }

        var resultados = corpus.Buscar(consulta, categoria, FragmentosPorBusqueda);
        if (resultados.Count == 0)
        {
            return new ResultadoHerramienta(
                "Sin resultados. Prueba con otras palabras clave (sinónimos, términos de reglamento) o sin categoría.", false, []);
        }

        var sb = new StringBuilder();
        foreach (var r in resultados)
        {
            var d = r.Documento;
            sb.Append("<fragmento documento=\"").Append(d.Titulo).Append('"');
            sb.Append(" categoria=\"").Append(d.Categoria).Append('"');
            if (d.Anio is { } anio) sb.Append(" anio=\"").Append(anio).Append('"');
            sb.Append(" vigente=\"").Append(d.Vigente ? "sí" : "no (histórico o sustituido)").Append('"');
            if (r.Fragmento.PaginaDesde > 0) sb.Append(" paginas=\"").Append(Paginas(r.Fragmento)).Append('"');
            sb.AppendLine(">");
            sb.AppendLine(r.Fragmento.Texto);
            sb.AppendLine("</fragmento>");
        }

        var fuentes = resultados
            .GroupBy(r => r.Documento.Id)
            .Select(g => new FuenteChatDto("documento", g.First().Documento.Titulo,
                g.All(r => r.Fragmento.PaginaDesde == 0) ? null : "pág. " + string.Join(", ", g
                    .Where(r => r.Fragmento.PaginaDesde > 0)
                    .OrderBy(r => r.Fragmento.PaginaDesde)
                    .Select(r => Paginas(r.Fragmento)).Distinct()),
                g.First().Documento.Url))
            .ToArray();
        return new ResultadoHerramienta(Recortar(sb.ToString()), false, fuentes);
    }

    private ResultadoHerramienta Listar(IReadOnlyDictionary<string, JsonElement> entrada)
    {
        var categoria = Texto(entrada, "categoria");
        var documentos = corpus.Documentos
            .Where(d => categoria is null || string.Equals(d.Categoria, categoria, StringComparison.OrdinalIgnoreCase))
            .Select(d => new
            {
                d.Titulo,
                d.Categoria,
                d.Anio,
                d.Vigente,
                d.Descripcion,
                consultable = !d.SinTexto,
            });
        return new ResultadoHerramienta(Recortar(JsonSerializer.Serialize(documentos, Json)), false, []);
    }

    private async Task<ResultadoHerramienta> RankingAsync(IReadOnlyDictionary<string, JsonElement> entrada, CancellationToken ct)
    {
        var ranking = ReglasClasificaciones.Buscar(Texto(entrada, "ranking"));
        if (ranking is null)
        {
            return ResultadoHerramienta.Error($"Ranking no válido. Usa uno de: {string.Join(", ", ReglasClasificaciones.Rankings.Keys)}.");
        }

        if (!ReglasClasificaciones.TryNormalizarGenero(Texto(entrada, "genero"), out var genero)
            || (genero is not null && !ranking.Generos.Contains(genero)))
        {
            return ResultadoHerramienta.Error($"Género no válido para este ranking. Usa: {string.Join(", ", ranking.Generos)} u omítelo.");
        }

        var categoria = Texto(entrada, "categoria")?.Trim().ToLowerInvariant().TrimStart('+');
        if (categoria is not null && !ranking.Categorias.Contains(categoria))
        {
            return ResultadoHerramienta.Error(ranking.Categorias.Count == 0
                ? "Este ranking no tiene categorías: omite 'categoria'."
                : $"Categoría no válida. Usa una de: {string.Join(", ", ranking.Categorias)}.");
        }

        var nombre = Texto(entrada, "nombre");
        var terminos = ReglasClasificaciones.Terminos(nombre);
        if (nombre?.Length > ReglasClasificaciones.LongitudMaximaBusqueda || terminos.Count > ReglasClasificaciones.TerminosMaximos)
        {
            return ResultadoHerramienta.Error("El nombre es demasiado largo: usa nombre y apellidos.");
        }

        var licencia = Entero(entrada, "licencia");
        if (licencia is < 1)
        {
            return ResultadoHerramienta.Error("La licencia debe ser un número positivo.");
        }

        var limite = Math.Clamp(Entero(entrada, "limite") ?? PuestosPorDefecto, 1, PuestosMaximos);
        var clasificacion = await clasificaciones.ObtenerAsync(ranking,
            new CriteriosClasificacion(genero, categoria, licencia, terminos, limite), ct);

        var nodo = JsonSerializer.SerializeToNode(clasificacion, Json)!;
        Limpiar(nodo);
        nodo["paginaRanking"] = PaginaRanking(ranking.Clave);
        var detalle = string.Join(" · ", new[] { genero, categoria, nombre, licencia?.ToString() }.Where(x => x is not null));
        var fuente = new FuenteChatDto("ranking", ranking.Titulo, detalle.Length == 0 ? null : detalle, PaginaRanking(ranking.Clave));
        return new ResultadoHerramienta(Recortar(nodo.ToJsonString(Json)), false, [fuente]);
    }

    private async Task<ResultadoHerramienta> EstadisticasAsync(IReadOnlyDictionary<string, JsonElement> entrada, CancellationToken ct)
    {
        var licencia = Entero(entrada, "licencia");
        if (licencia is null or < 1)
        {
            return ResultadoHerramienta.Error("Falta un número de licencia válido.");
        }

        var consulta = await estadisticas.ObtenerAsync(licencia.Value, ReglasOrganizacionTorneos.AhoraEnMadrid(reloj), ct);
        if (consulta.Estado != EstadoEstadisticas.Encontradas || consulta.Estadisticas is null)
        {
            return new ResultadoHerramienta(
                "No hay estadísticas públicas para esa licencia (no existe o no aparece en ningún ranking nacional).", false, []);
        }

        var nodo = JsonSerializer.SerializeToNode(consulta.Estadisticas, Json)!;
        Limpiar(nodo);
        // Las pruebas de cada ranking pueden ser decenas: bastan las más recientes.
        if (nodo["rankings"] is JsonArray rankings)
        {
            foreach (var r in rankings.OfType<JsonObject>())
            {
                if (r["pruebas"] is JsonArray pruebas)
                {
                    while (pruebas.Count > 8) pruebas.RemoveAt(pruebas.Count - 1);
                }
            }
        }

        var pagina = $"estadisticas.html?id={licencia}";
        nodo["paginaEstadisticas"] = pagina;
        var titulo = nodo["nombre"]?.GetValue<string>() ?? $"Licencia {licencia}";
        return new ResultadoHerramienta(Recortar(nodo.ToJsonString(Json)), false,
            [new FuenteChatDto("estadisticas", $"Estadísticas de {titulo}", null, pagina)]);
    }

    /// <summary>Página del ranking en la web nueva (ranking-absoluto.html…).</summary>
    public static string PaginaRanking(string clave) => $"ranking-{clave}.html";

    /// <summary>
    /// Quita lo que no aporta al modelo y ocupa contexto: URLs de la web antigua, fotos, imágenes, documentos y nulos.
    /// </summary>
    private static void Limpiar(JsonNode? nodo)
    {
        switch (nodo)
        {
            case JsonObject obj:
                foreach (var clave in obj.Select(p => p.Key).ToArray())
                {
                    var valor = obj[clave];
                    if (valor is null
                        || clave.EndsWith("Url", StringComparison.OrdinalIgnoreCase)
                        || clave is "foto" or "imagen" or "documento" or "bandera" or "escudo")
                    {
                        obj.Remove(clave);
                    }
                    else
                    {
                        Limpiar(valor);
                    }
                }

                break;
            case JsonArray arr:
                foreach (var item in arr)
                {
                    Limpiar(item);
                }

                break;
        }
    }

    private static string Paginas(FragmentoCorpus f)
        => f.PaginaDesde == f.PaginaHasta ? f.PaginaDesde.ToString() : $"{f.PaginaDesde}-{f.PaginaHasta}";

    private static string Recortar(string texto)
        => texto.Length <= LongitudMaximaResultado ? texto : texto[..LongitudMaximaResultado] + "\n[…resultado recortado]";

    private static string? Texto(IReadOnlyDictionary<string, JsonElement> entrada, string clave)
        => entrada.TryGetValue(clave, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!.Trim()
            : null;

    private static int? Entero(IReadOnlyDictionary<string, JsonElement> entrada, string clave)
    {
        if (!entrada.TryGetValue(clave, out var v))
        {
            return null;
        }

        return v.ValueKind switch
        {
            JsonValueKind.Number when v.TryGetInt32(out var n) => n,
            JsonValueKind.String when int.TryParse(v.GetString(), out var n) => n,
            _ => null,
        };
    }

    private static JsonElement Esquema(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
