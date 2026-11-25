using System.Text.Json;
using FEP.Api.Contracts.Federacion;
using FEP.Api.Infrastructure;

namespace FEP.Api.Data.Repositories.Federacion;

/// <summary>Equipo técnico y administrativo de la FEP.</summary>
public interface IEquipoFepRepository
{
    /// <summary>Composición actual del equipo de la FEP.</summary>
    Task<EquipoFepDto> ObtenerAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>Equipo_FEP.asp</c>. En el ASP la página solo mostraba dos imágenes escritas a mano en el
/// HTML (<c>refs/images/ORGANIGRAMA-FEP.png</c> y <c>refs/images/MENORES-FEP.png</c>); los nombres y
/// puestos solo existían dentro de esas imágenes. Aquí se transcriben a
/// <c>Contenidos/equipo-fep.json</c> y la ruta original de cada imagen (<c>imagen</c>) se completa
/// con su URL absoluta (<c>imagenUrl</c>) usando <c>Media:BaseUrl</c>.
/// <para>
/// El organigrama incluye la Junta Directiva, pero la imagen no da sus nombres. El grupo
/// <c>junta-directiva</c> viene vacío en el JSON y se rellena aquí con la Junta Directiva de
/// <see cref="IOrganosGobiernoRepository"/>: una sola fuente para las dos páginas.
/// </para>
/// Cuando exista modelo en SQL Server (ver docs/api/analisis/Equipo_FEP.md), basta con sustituir
/// esta implementación por una con Dapper: el contrato no cambia.
/// </summary>
public sealed class EquipoFepRepository(
    IWebHostEnvironment entorno,
    IMediaUrlBuilder media,
    IOrganosGobiernoRepository organos) : IEquipoFepRepository
{
    /// <summary>Ruta del fichero de contenido, relativa a la raíz del proyecto.</summary>
    public const string RutaContenido = "Contenidos/equipo-fep.json";

    /// <summary>Clave del grupo (y del órgano en Órganos de gobierno) de la Junta Directiva.</summary>
    public const string ClaveJunta = "junta-directiva";

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public async Task<EquipoFepDto> ObtenerAsync(CancellationToken cancellationToken)
    {
        var fichero = entorno.ContentRootFileProvider.GetFileInfo(RutaContenido);
        if (!fichero.Exists)
        {
            throw new FileNotFoundException($"No se encuentra el contenido del equipo de la FEP ({RutaContenido}).");
        }

        EquipoFepDto contenido;
        await using (var flujo = fichero.CreateReadStream())
        {
            contenido = await JsonSerializer.DeserializeAsync<EquipoFepDto>(flujo, OpcionesJson, cancellationToken)
                ?? throw new InvalidDataException($"{RutaContenido} está vacío o no es válido.");
        }

        var junta = await MiembrosJuntaAsync(cancellationToken);

        return contenido with
        {
            Areas = contenido.Areas
                .Select(a => a with
                {
                    ImagenUrl = media.Construir(a.Imagen),
                    Grupos = a.Grupos
                        .Select(g => g.Clave == ClaveJunta && g.Miembros.Count == 0 ? g with { Miembros = junta } : g)
                        .ToArray(),
                })
                .ToArray(),
        };
    }

    /// <summary>Todos los miembros de la Junta Directiva (presidencia, vicepresidencias, secretaría y vocales).</summary>
    private async Task<IReadOnlyList<MiembroEquipoDto>> MiembrosJuntaAsync(CancellationToken cancellationToken)
    {
        var datos = await organos.ObtenerAsync(cancellationToken);
        var organo = datos.Organos.FirstOrDefault(o => o.Clave == ClaveJunta);
        return organo is null
            ? []
            : organo.Grupos.SelectMany(g => g.Miembros).Select(m => new MiembroEquipoDto(m.Nombre, m.Cargo)).ToArray();
    }
}
