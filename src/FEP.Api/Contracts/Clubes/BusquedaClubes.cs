using FEP.Api.Contracts.Federacion;

namespace FEP.Api.Contracts.Clubes;

/// <summary>Criterios ya validados del buscador de clubes.</summary>
/// <param name="Federacion">Id de federación (1–22) o <c>null</c>.</param>
/// <param name="Provincia">Id de provincia o <c>null</c>.</param>
/// <param name="Terminos">Términos del texto buscado: el nombre del club debe contenerlos todos.</param>
/// <param name="Pagina">Página (desde 1).</param>
/// <param name="Limite">Tamaño de página.</param>
public sealed record BusquedaClubes(int? Federacion, int? Provincia, IReadOnlyList<string> Terminos, int Pagina, int Limite)
{
    /// <summary>Tamaño de página por defecto.</summary>
    public const int LimitePorDefecto = 50;

    /// <summary>Tamaño de página máximo.</summary>
    public const int LimiteMaximo = 100;

    /// <summary>Página máxima admitida (evita OFFSET desorbitados).</summary>
    public const int PaginaMaxima = 1000;

    /// <summary>Longitud máxima del texto buscado.</summary>
    public const int LongitudMaximaBusqueda = 100;

    /// <summary>Número máximo de términos del texto buscado.</summary>
    public const int TerminosMaximos = 8;

    /// <summary>
    /// Rango válido de <c>IdFederacion</c>: las 22 federaciones autonómicas (<see cref="ReglasFederaciones"/>; el ASP
    /// listaba <c>IdFederacion &lt;&gt; 0 AND IdFederacion &lt; 100</c>).
    /// </summary>
    public const int FederacionMinima = ReglasFederaciones.IdMinimo, FederacionMaxima = ReglasFederaciones.IdMaximo;

    /// <summary>Rango válido de <c>IdProvincia</c> (el ASP excluía la 0; existe la 100 "SIN DEFINIR").</summary>
    public const int ProvinciaMinima = 1, ProvinciaMaxima = 999;

    private static readonly char[] Separadores = [' ', '\t', '\r', '\n', '-'];

    /// <summary>
    /// Divide el texto en términos como el ASP (que cambiaba los espacios por "-" y partía por "-"):
    /// el club debe contener todos. Sin términos → lista vacía.
    /// </summary>
    public static IReadOnlyList<string> DividirTerminos(string? texto)
        => string.IsNullOrWhiteSpace(texto)
            ? []
            : texto.Split(Separadores, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Escapa los comodines de <c>LIKE</c> de SQL Server (<c>%</c>, <c>_</c>, <c>[</c>) para que el texto
    /// se busque literalmente.
    /// </summary>
    public static string EscaparLike(string termino)
        => termino.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
}
