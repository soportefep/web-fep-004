namespace FEP.Api.Contracts.Banners;

/// <summary>Tipos de banner (columna <c>SFEP_Banners.Tipo</c>).</summary>
public enum TipoBanner
{
    /// <summary>"Enlaces de interés" del pie (Tipo='Interes').</summary>
    Interes,

    /// <summary>Bloques destacados de la barra lateral (Tipo='Destacado').</summary>
    Destacado,

    /// <summary>"Eventos destacados e inscripciones abiertas" de la portada (Tipo='Evento').</summary>
    Evento,
}

/// <summary>Conversión entre la clave pública (<c>?tipo=</c>), el valor en BD y la carpeta de imágenes.</summary>
public static class TiposBanner
{
    /// <summary>Valores admitidos en <c>?tipo=</c>.</summary>
    public const string ValoresAdmitidos = "interes, destacado, evento";

    /// <summary>Interpreta la clave pública (sin distinguir mayúsculas; admite "interés").</summary>
    public static bool TryParse(string? clave, out TipoBanner tipo)
    {
        TipoBanner? resultado = clave?.Trim().ToLowerInvariant() switch
        {
            "interes" or "interés" => TipoBanner.Interes,
            "destacado" => TipoBanner.Destacado,
            "evento" => TipoBanner.Evento,
            _ => null,
        };
        tipo = resultado.GetValueOrDefault();
        return resultado.HasValue;
    }

    /// <summary>Clave pública en minúsculas (la que se devuelve en <see cref="BannerDto.Tipo"/>).</summary>
    public static string Clave(TipoBanner tipo) => tipo switch
    {
        TipoBanner.Interes => "interes",
        TipoBanner.Destacado => "destacado",
        TipoBanner.Evento => "evento",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };

    /// <summary>Valor literal de la columna <c>Tipo</c> en BD.</summary>
    public static string ValorBd(TipoBanner tipo) => tipo switch
    {
        TipoBanner.Interes => "Interes",
        TipoBanner.Destacado => "Destacado",
        TipoBanner.Evento => "Evento",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };

    /// <summary>Carpeta bajo <c>refs/</c> donde el ASP buscaba la imagen de cada tipo.</summary>
    public static string CarpetaImagen(TipoBanner tipo) => tipo switch
    {
        TipoBanner.Interes => "images/banners",   // PiePagina.inc
        TipoBanner.Destacado => "images",         // BarraLateral.inc
        TipoBanner.Evento => "images/Banners",    // index.asp
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };
}
