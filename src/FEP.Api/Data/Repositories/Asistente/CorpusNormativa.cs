using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FEP.Api.Data.Repositories.Asistente;

/// <summary>Documento del catálogo del asistente (<c>Contenidos/asistente/documentos.json</c>).</summary>
public sealed record DocumentoCorpus(
    string Id,
    string Fichero,
    string Categoria,
    string Titulo,
    string? Descripcion,
    int? Anio,
    bool Vigente,
    string? Url,
    int Paginas,
    bool SinTexto);

/// <summary>Fragmento de texto de un documento (<c>Contenidos/asistente/corpus.json</c>).</summary>
public sealed record FragmentoCorpus(string Id, string Documento, int PaginaDesde, int PaginaHasta, string Texto);

/// <summary>Fragmento encontrado, con su documento y su puntuación (BM25).</summary>
public sealed record ResultadoBusquedaCorpus(FragmentoCorpus Fragmento, DocumentoCorpus Documento, double Puntuacion);

/// <summary>
/// Normativa, circulares, estatutos y memorias que consulta el asistente. Búsqueda léxica BM25 en memoria: el corpus
/// (≈1.400 fragmentos, 2 MB) cabe de sobra y no necesita base de datos vectorial ni otro proveedor de embeddings.
/// El modelo compensa la falta de búsqueda semántica reformulando y repitiendo la búsqueda con otros términos.
/// </summary>
public interface ICorpusNormativa
{
    /// <summary>Categorías con documentos (reglamentos, circulares, estatutos, instalaciones, memorias, rankings…).</summary>
    IReadOnlyList<string> Categorias { get; }

    /// <summary>Catálogo completo, en el orden del fichero.</summary>
    IReadOnlyList<DocumentoCorpus> Documentos { get; }

    /// <summary>Los <paramref name="maximo"/> fragmentos más relevantes para <paramref name="consulta"/>.</summary>
    /// <param name="consulta">Texto libre.</param>
    /// <param name="categoria">Limita a una categoría (o <c>null</c>: todas).</param>
    /// <param name="maximo">Fragmentos máximos.</param>
    IReadOnlyList<ResultadoBusquedaCorpus> Buscar(string consulta, string? categoria, int maximo);
}

/// <summary>
/// Carga el corpus generado con <c>tools/generar-corpus-asistente.ps1</c> y la normativa de los rankings
/// (<c>Contenidos/rankings/*.json</c>). Singleton: se indexa una vez al arrancar.
/// </summary>
public sealed class CorpusNormativa : ICorpusNormativa
{
    /// <summary>Categoría de los textos de normativa de los rankings que ya sirve la API.</summary>
    public const string CategoriaRankings = "rankings";

    private const double K1 = 1.2;
    private const double B = 0.75;

    /// <summary>Peso de los documentos históricos (memorias, versiones sustituidas) frente a los vigentes.</summary>
    private const double PesoNoVigente = 0.75;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly HashSet<string> Vacias = new(StringComparer.Ordinal)
    {
        // Español
        "a", "al", "algo", "ante", "antes", "como", "con", "cual", "cuales", "cuando", "de", "del", "desde", "donde",
        "dos", "el", "ella", "ellas", "ellos", "en", "entre", "era", "es", "esa", "ese", "eso", "esta", "este", "esto",
        "estos", "estas", "fue", "ha", "han", "hay", "la", "las", "le", "les", "lo", "los", "mas", "me", "mi", "mis",
        "muy", "ni", "no", "nos", "o", "os", "para", "pero", "por", "porque", "que", "quien", "se", "segun", "ser",
        "si", "sin", "sobre", "son", "su", "sus", "tambien", "te", "tiene", "tienen", "toda", "todas", "todo", "todos",
        "tu", "tus", "un", "una", "uno", "unos", "unas", "y", "ya", "yo", "puedo", "puede", "pueden", "cuanto", "cuantos",
        "cuanta", "cuantas", "hace", "hacer", "debo", "debe",
        // Inglés (preguntas en inglés sobre textos en español: al menos no puntúan las palabras vacías)
        "the", "of", "and", "to", "in", "is", "for", "on", "what", "how", "which", "who", "can", "do", "does", "are",
        "an", "it", "my", "with", "be", "at", "by", "or", "from", "this", "that", "many", "much",
    };

    private readonly List<DocumentoCorpus> _documentos;
    private readonly Dictionary<string, DocumentoCorpus> _porId;
    private readonly List<FragmentoIndexado> _fragmentos = [];
    private readonly Dictionary<string, List<(int Fragmento, int Frecuencia)>> _invertido = new(StringComparer.Ordinal);
    private readonly double _longitudMedia;

    /// <summary>Lee los ficheros de <paramref name="carpetaContenidos"/> (la carpeta <c>Contenidos</c> de la API).</summary>
    public CorpusNormativa(string carpetaContenidos)
    {
        var carpeta = Path.Combine(carpetaContenidos, "asistente");
        var catalogo = Leer<CatalogoJson>(Path.Combine(carpeta, "documentos.json"));
        var corpus = Leer<CorpusJson>(Path.Combine(carpeta, "corpus.json"));

        _documentos = [.. catalogo?.Documentos ?? []];
        var fragmentos = new List<FragmentoCorpus>(corpus?.Fragmentos ?? []);
        AnadirNormativasRankings(Path.Combine(carpetaContenidos, "rankings"), _documentos, fragmentos);

        _porId = _documentos.ToDictionary(d => d.Id, StringComparer.Ordinal);
        foreach (var f in fragmentos)
        {
            if (!_porId.TryGetValue(f.Documento, out var doc))
            {
                continue;
            }

            // El título cuenta como parte del fragmento: "reglamento disciplina sanciones" encuentra el reglamento.
            var terminos = Terminos(doc.Titulo + " " + f.Texto);
            var indice = _fragmentos.Count;
            _fragmentos.Add(new FragmentoIndexado(f, doc, terminos.Count));
            foreach (var grupo in terminos.GroupBy(t => t, StringComparer.Ordinal))
            {
                if (!_invertido.TryGetValue(grupo.Key, out var lista))
                {
                    _invertido[grupo.Key] = lista = [];
                }

                lista.Add((indice, grupo.Count()));
            }
        }

        _longitudMedia = _fragmentos.Count == 0 ? 1 : _fragmentos.Average(f => (double)f.Longitud);
        Categorias = _documentos.Select(d => d.Categoria).Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Categorias { get; }

    /// <inheritdoc />
    public IReadOnlyList<DocumentoCorpus> Documentos => _documentos;

    /// <inheritdoc />
    public IReadOnlyList<ResultadoBusquedaCorpus> Buscar(string consulta, string? categoria, int maximo)
    {
        var terminos = Terminos(consulta).Distinct(StringComparer.Ordinal).ToArray();
        if (terminos.Length == 0 || maximo < 1)
        {
            return [];
        }

        var n = _fragmentos.Count;
        var puntuaciones = new Dictionary<int, double>();
        foreach (var termino in terminos)
        {
            if (!_invertido.TryGetValue(termino, out var apariciones))
            {
                continue;
            }

            var idf = Math.Log(1 + (n - apariciones.Count + 0.5) / (apariciones.Count + 0.5));
            foreach (var (indice, frecuencia) in apariciones)
            {
                var f = _fragmentos[indice];
                if (categoria is not null && !string.Equals(f.Documento.Categoria, categoria, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var tf = frecuencia * (K1 + 1) / (frecuencia + K1 * (1 - B + B * f.Longitud / _longitudMedia));
                puntuaciones[indice] = puntuaciones.GetValueOrDefault(indice) + idf * tf;
            }
        }

        return puntuaciones
            .Select(p =>
            {
                var f = _fragmentos[p.Key];
                // Lo histórico pesa menos salvo que se pida esa categoría (las memorias, para preguntas de historia).
                var peso = f.Documento.Vigente || categoria is not null ? 1 : PesoNoVigente;
                return new ResultadoBusquedaCorpus(f.Fragmento, f.Documento, p.Value * peso);
            })
            .OrderByDescending(r => r.Puntuacion)
            .ThenBy(r => r.Fragmento.Id, StringComparer.Ordinal)
            .Take(maximo)
            .ToArray();
    }

    /// <summary>
    /// Términos normalizados: minúsculas, sin acentos, sin palabras vacías y con un recorte de plurales y género sencillo
    /// ("sanciones" → "sancion", "jugadoras" → "jugador"), suficiente para el vocabulario de los reglamentos.
    /// </summary>
    internal static List<string> Terminos(string texto)
    {
        var resultado = new List<string>();
        var normalizado = texto.Normalize(NormalizationForm.FormD);
        var actual = new StringBuilder();
        foreach (var c in normalizado)
        {
            var categoria = CharUnicodeInfo.GetUnicodeCategory(c);
            if (categoria == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                actual.Append(char.ToLowerInvariant(c));
            }
            else
            {
                Cerrar();
            }
        }

        Cerrar();
        return resultado;

        void Cerrar()
        {
            if (actual.Length == 0)
            {
                return;
            }

            var t = actual.ToString();
            actual.Clear();
            if (Vacias.Contains(t) || (t.Length == 1 && !char.IsDigit(t[0])))
            {
                return;
            }

            resultado.Add(Raiz(t));
        }
    }

    private static string Raiz(string t)
    {
        if (t.Length <= 4 || t.Any(char.IsDigit))
        {
            return t;
        }

        if (t.EndsWith("ciones", StringComparison.Ordinal)) return t[..^2];      // sanciones → sancion
        if (t.EndsWith("ores", StringComparison.Ordinal)) return t[..^2];        // jugadores → jugador
        if (t.EndsWith("oras", StringComparison.Ordinal)) return t[..^2];        // jugadoras → jugador
        if (t.EndsWith("ora", StringComparison.Ordinal)) return t[..^1];         // jugadora → jugador
        if (t.EndsWith("es", StringComparison.Ordinal) && t.Length > 5 && "lnrdz".Contains(t[^3]))
        {
            return t[..^2];                                                       // nacionales → nacional, mujeres → mujer
        }

        if (t.EndsWith('s')) return t[..^1];                                      // torneos → torneo, clases → clase
        return t;
    }

    /// <summary>
    /// La normativa de cálculo de cada ranking (la misma que sirve <c>/competicion/rankings/{clave}/normativa</c>) entra
    /// en el corpus con un fragmento por apartado.
    /// </summary>
    private static void AnadirNormativasRankings(string carpeta, List<DocumentoCorpus> documentos, List<FragmentoCorpus> fragmentos)
    {
        if (!Directory.Exists(carpeta))
        {
            return;
        }

        foreach (var fichero in Directory.EnumerateFiles(carpeta, "*.json").Order(StringComparer.Ordinal))
        {
            var normativa = Leer<NormativaJson>(fichero);
            if (normativa?.Secciones is not { Count: > 0 })
            {
                continue;
            }

            var clave = Path.GetFileNameWithoutExtension(fichero).ToLowerInvariant();
            var id = $"rankings-normativa-{clave}";
            documentos.Add(new DocumentoCorpus(id, $"Contenidos/rankings/{clave}.json", CategoriaRankings,
                $"Ranking {clave}: {normativa.Titulo ?? "normativa de cálculo"}", normativa.Resumen, null, true,
                $"ranking-{clave}.html", 0, false));

            var n = 0;
            foreach (var seccion in normativa.Secciones)
            {
                var sb = new StringBuilder();
                sb.AppendLine(seccion.Titulo);
                foreach (var bloque in seccion.Bloques ?? [])
                {
                    if (!string.IsNullOrWhiteSpace(bloque.Texto)) sb.AppendLine(bloque.Texto);
                    foreach (var e in bloque.Elementos ?? [])
                    {
                        sb.AppendLine(string.IsNullOrWhiteSpace(e.Destacado) ? $"• {e.Texto}" : $"• {e.Destacado} {e.Texto}");
                    }

                    if (bloque.Tabla is { } tabla)
                    {
                        if (!string.IsNullOrWhiteSpace(tabla.Titulo)) sb.AppendLine(tabla.Titulo);
                        sb.AppendLine(string.Join(" | ", tabla.Columnas ?? []));
                        foreach (var fila in tabla.Filas ?? []) sb.AppendLine(string.Join(" | ", fila));
                    }
                }

                fragmentos.Add(new FragmentoCorpus($"{id}#{++n}", id, 0, 0, sb.ToString().Trim()));
            }
        }
    }

    private static T? Leer<T>(string ruta) where T : class
        => File.Exists(ruta) ? JsonSerializer.Deserialize<T>(File.ReadAllText(ruta, Encoding.UTF8), Json) : null;

    private sealed record FragmentoIndexado(FragmentoCorpus Fragmento, DocumentoCorpus Documento, int Longitud);

    private sealed record CatalogoJson(List<DocumentoCorpus>? Documentos);

    private sealed record CorpusJson(List<FragmentoCorpus>? Fragmentos);

    private sealed record NormativaJson(string? Titulo, string? Resumen, List<SeccionJson>? Secciones);

    private sealed record SeccionJson(string Titulo, List<BloqueJson>? Bloques);

    private sealed record BloqueJson(string? Texto, List<ElementoJson>? Elementos, TablaJson? Tabla);

    private sealed record ElementoJson(string? Destacado, string Texto);

    private sealed record TablaJson(string? Titulo, List<string>? Columnas, List<List<string>>? Filas);
}
