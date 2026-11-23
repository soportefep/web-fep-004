namespace FEP.Api.Contracts.Navegacion;

/// <summary>Perfil de la FEP en una red social (tabla <c>SFEP_Web_RS</c>).</summary>
/// <param name="Nombre">Nombre de la red (<c>Red</c>), p. ej. "Instagram".</param>
/// <param name="Url">URL del perfil (<c>URLRED</c>).</param>
/// <param name="Icono">Clases CSS del icono heredado (<c>Icono</c>); <c>null</c> si no hay.</param>
/// <param name="Orden">Orden de aparición.</param>
public sealed record RedSocialDto(
    string Nombre,
    string? Url,
    string? Icono,
    int Orden);
