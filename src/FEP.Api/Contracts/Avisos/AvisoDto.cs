namespace FEP.Api.Contracts.Avisos;

/// <summary>Aviso de la barra lateral (tabla <c>SFEP_Avisos</c>, Portal 'FEP' o 'AMB').</summary>
/// <param name="Fecha">Fecha y hora del aviso (<c>Fecha</c>).</param>
/// <param name="Texto">Texto del aviso (<c>aviso</c>).</param>
/// <param name="Enlace">Destino (<c>Link</c>).</param>
/// <param name="Ventana">Destino del enlace (<c>ventana</c>).</param>
public sealed record AvisoDto(
    DateTime? Fecha,
    string Texto,
    string? Enlace,
    string? Ventana);
