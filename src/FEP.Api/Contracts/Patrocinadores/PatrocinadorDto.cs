namespace FEP.Api.Contracts.Patrocinadores;

/// <summary>Patrocinador de la FEP (tabla <c>SFEP_Patrocinadores</c>, Ver='S', Federacion=0).</summary>
/// <param name="Logo">Nombre de fichero original del logotipo (<c>Logo</c>), relativo a <c>refs/images/banners/</c>.</param>
/// <param name="LogoUrl">URL absoluta del logotipo.</param>
/// <param name="Enlace">Web del patrocinador (<c>Link</c>, con <c>https://</c> antepuesto si no trae esquema). Se abre en ventana nueva.</param>
/// <param name="Orden">Orden de aparición (<c>orden</c>).</param>
public sealed record PatrocinadorDto(
    string? Logo,
    string? LogoUrl,
    string? Enlace,
    int Orden);
