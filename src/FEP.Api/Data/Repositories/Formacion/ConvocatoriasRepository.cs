using Dapper;
using FEP.Api.Contracts.Formacion;
using FEP.Api.Infrastructure;

namespace FEP.Api.Data.Repositories.Formacion;

/// <summary>Convocatorias de formación con inscripción abierta.</summary>
public interface IConvocatoriasRepository
{
    /// <summary>
    /// Acciones con inscripción abierta cuyo tope es posterior a <paramref name="ahora"/>, ordenadas por tope.
    /// Si <paramref name="categoria"/> es <c>null</c> devuelve los tres grupos de la portada.
    /// </summary>
    Task<IReadOnlyList<ConvocatoriaDto>> ObtenerAbiertasAsync(CategoriaFormacion? categoria, DateTime ahora, CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>index.asp</c> (sql_monitor, sql_foco, sql_juez). Las tres consultas solo cambiaban
/// la categoría; además comparaban <c>FechaInscripcion &gt; '" &amp; now() &amp; "'</c> como texto
/// (dependiente del formato regional). Aquí: una consulta con <c>@Ahora</c> tipado y <c>IN @Categorias</c>.
/// </summary>
public sealed class ConvocatoriasRepository(IDbConnectionFactory connectionFactory) : IConvocatoriasRepository
{
    private const string Sql = """
        SELECT fc.Titulo, fc.Categoria, fc.FechaIni, fc.FechaFin, fc.Lugar, fc.Localidad, fc.FechaInscripcion
        FROM SFEP_Formacion_Calendario AS fc
        WHERE fc.AbiertaInscripcion = 'S'
          AND fc.FechaInscripcion > @Ahora
          AND fc.Borrado = 'N'
          AND fc.Federacion = 0
          AND fc.Categoria IN @Categorias
        ORDER BY fc.FechaInscripcion;
        """;

    public async Task<IReadOnlyList<ConvocatoriaDto>> ObtenerAbiertasAsync(
        CategoriaFormacion? categoria, DateTime ahora, CancellationToken cancellationToken)
    {
        var categorias = categoria is { } c
            ? CategoriasFormacion.ValoresBd(c)
            : CategoriasFormacion.TodosLosValoresBd();

        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var filas = await conexion.QueryAsync<Fila>(new CommandDefinition(
            Sql,
            new { Ahora = ahora, Categorias = categorias },
            cancellationToken: cancellationToken));

        var resultado = new List<ConvocatoriaDto>();
        foreach (var f in filas)
        {
            // El filtro SQL garantiza que la categoría es de la portada; se comprueba por robustez.
            if (CategoriasFormacion.DesdeValorBd(f.Categoria) is not { } grupo || f.FechaInscripcion is not { } tope)
            {
                continue;
            }

            resultado.Add(new ConvocatoriaDto(
                CategoriasFormacion.Clave(grupo),
                Texto.Requerido(f.Categoria),
                Texto.Requerido(f.Titulo),
                f.FechaIni is { } ini ? DateOnly.FromDateTime(ini) : null,
                f.FechaFin is { } fin ? DateOnly.FromDateTime(fin) : null,
                Texto.Limpio(f.Lugar),
                Texto.Limpio(f.Localidad),
                tope));
        }

        return resultado;
    }

    // SUPUESTO: FechaIni, FechaFin y FechaInscripcion son datetime (el ASP usaba formatdatetime);
    // si fueran varchar habría que convertirlas en SQL. Federacion entero; resto texto.
    private sealed class Fila
    {
        public string? Titulo { get; set; }
        public string? Categoria { get; set; }
        public DateTime? FechaIni { get; set; }
        public DateTime? FechaFin { get; set; }
        public string? Lugar { get; set; }
        public string? Localidad { get; set; }
        public DateTime? FechaInscripcion { get; set; }
    }
}
