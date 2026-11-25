namespace FEP.Api.Contracts.Navegacion;

/// <summary>Opción del menú principal de la web (tabla <c>SFEP_Web_Menu</c>, Site='FEP').</summary>
/// <param name="Id">Identificador de la opción (<c>IdOpcion</c>). La opción 1 es la portada (el ASP la marcaba como activa).</param>
/// <param name="Texto">Texto visible (<c>Opcion</c>).</param>
/// <param name="Enlace">URL o ruta relativa del sitio (<c>Link</c>).</param>
/// <param name="Ventana">Destino del enlace (<c>Ventana</c>, p. ej. <c>_self</c> o <c>_blank</c>); <c>null</c> si no se indica.</param>
/// <param name="Icono">Clases CSS del icono heredado (Font Awesome 4, <c>Icono</c>); <c>null</c> si no hay.</param>
/// <param name="Orden">Orden dentro de su nivel.</param>
/// <param name="Hijos">Opciones de nivel 2 cuyo <c>Origen</c> es esta opción (vacío en el nivel 2).</param>
public sealed record MenuItemDto(
    int Id,
    string Texto,
    string? Enlace,
    string? Ventana,
    string? Icono,
    int Orden,
    IReadOnlyList<MenuItemDto> Hijos);
