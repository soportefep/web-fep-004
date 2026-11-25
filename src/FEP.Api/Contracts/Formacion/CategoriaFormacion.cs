namespace FEP.Api.Contracts.Formacion;

/// <summary>Grupos de convocatorias de formación tal y como los muestra la portada.</summary>
public enum CategoriaFormacion
{
    /// <summary>Técnicos: ParcialN1, Monitor, EntrenadorN2, EntrenadorN3, FC.</summary>
    Monitor,

    /// <summary>Cursos FOCO (Categoria='Foco').</summary>
    Foco,

    /// <summary>Jueces árbitros (Categoria='Juez').</summary>
    Juez,
}

/// <summary>Conversión entre la clave pública (<c>?categoria=</c>) y los valores de <c>SFEP_Formacion_Calendario.Categoria</c>.</summary>
public static class CategoriasFormacion
{
    /// <summary>Valores admitidos en <c>?categoria=</c>.</summary>
    public const string ValoresAdmitidos = "monitor, foco, juez";

    private static readonly Dictionary<CategoriaFormacion, string[]> ValoresBdPorCategoria = new()
    {
        [CategoriaFormacion.Monitor] = ["ParcialN1", "Monitor", "EntrenadorN2", "EntrenadorN3", "FC"],
        [CategoriaFormacion.Foco] = ["Foco"],
        [CategoriaFormacion.Juez] = ["Juez"],
    };

    /// <summary>Interpreta la clave pública (sin distinguir mayúsculas).</summary>
    public static bool TryParse(string? clave, out CategoriaFormacion categoria)
    {
        CategoriaFormacion? resultado = clave?.Trim().ToLowerInvariant() switch
        {
            "monitor" => CategoriaFormacion.Monitor,
            "foco" => CategoriaFormacion.Foco,
            "juez" => CategoriaFormacion.Juez,
            _ => null,
        };
        categoria = resultado.GetValueOrDefault();
        return resultado.HasValue;
    }

    /// <summary>Clave pública en minúsculas.</summary>
    public static string Clave(CategoriaFormacion categoria) => categoria switch
    {
        CategoriaFormacion.Monitor => "monitor",
        CategoriaFormacion.Foco => "foco",
        CategoriaFormacion.Juez => "juez",
        _ => throw new ArgumentOutOfRangeException(nameof(categoria), categoria, null),
    };

    /// <summary>Valores de la columna <c>Categoria</c> que componen el grupo.</summary>
    public static IReadOnlyList<string> ValoresBd(CategoriaFormacion categoria) => ValoresBdPorCategoria[categoria];

    /// <summary>Todos los valores de <c>Categoria</c> que muestra la portada.</summary>
    public static IReadOnlyList<string> TodosLosValoresBd() => ValoresBdPorCategoria.Values.SelectMany(v => v).ToArray();

    /// <summary>Grupo al que pertenece un valor de la columna <c>Categoria</c> (<c>null</c> si no es de la portada).</summary>
    public static CategoriaFormacion? DesdeValorBd(string? valorBd)
    {
        var valor = valorBd?.Trim();
        if (string.IsNullOrEmpty(valor))
        {
            return null;
        }

        foreach (var (categoria, valores) in ValoresBdPorCategoria)
        {
            if (valores.Contains(valor, StringComparer.OrdinalIgnoreCase))
            {
                return categoria;
            }
        }

        return null;
    }
}
