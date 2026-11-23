namespace FEP.Api.Contracts.Federacion;

/// <summary>
/// Rango de ids de las federaciones autonómicas (<c>SFEP_Federaciones.IdFederacion</c>), común a toda la API
/// (calendario territorial, buscador de clubes, ficha de federación y ranking de equipos).
/// <para>
/// DECIDIDO por la FEP (13/11/2026): hay 22 federaciones autonómicas, de la 1 a la 22, ambas incluidas
/// (Castilla-La Mancha es la 22). La 0 es la FEP. El ASP usaba rangos distintos según la página
/// (<c>BETWEEN 1 AND 21</c> en <c>CalendarioFT.asp</c>, <c>IdFederacion &lt; 100</c> en clubes y rankings); las
/// federaciones con id ≥ 100 (p. ej. 1001, 1002) no son autonómicas y duplicaban equipos en los <c>JOIN</c>.
/// </para>
/// </summary>
public static class ReglasFederaciones
{
    /// <summary>Primer id de federación autonómica.</summary>
    public const int IdMinimo = 1;

    /// <summary>Último id de federación autonómica: Castilla-La Mancha (22).</summary>
    public const int IdMaximo = 22;
}
