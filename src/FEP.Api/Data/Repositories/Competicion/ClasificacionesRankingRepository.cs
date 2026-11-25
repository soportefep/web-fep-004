using System.Globalization;
using System.Text;
using Dapper;
using FEP.Api.Contracts.Competicion;
using FEP.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace FEP.Api.Data.Repositories.Competicion;

/// <summary>Clasificaciones de los rankings nacionales (<c>SFEP_RankingFinal</c>).</summary>
public interface IClasificacionesRankingRepository
{
    /// <summary>
    /// Listas del ranking que cumplen el género/categoría pedidos, con sus datos (<c>SFEP_Ranking_Tipos</c>) y como
    /// mucho <see cref="CriteriosClasificacion.Limite"/> puestos cada una que cumplan la licencia y el nombre buscados.
    /// </summary>
    Task<ClasificacionRankingDto> ObtenerAsync(DefinicionClasificacion ranking, CriteriosClasificacion criterios, CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>CargarRnkAbs.asp</c> y <c>CargarRnkAbsNombre.asp</c> (cargados por AJAX desde <c>Ranking_Absoluto.asp</c>)
/// y <c>CargarRnkMen.asp</c> (desde <c>Ranking_Menores.asp</c>: mismas tablas, un <c>TipoRanking</c> por categoría y
/// género, sin <c>TOP</c>, con la misma concatenación de la licencia y el mismo error 500 si no existía) y
/// <c>CargarRnkVet.asp</c> (desde <c>Ranking_Veteranos.asp</c>: copia de <c>CargarRnkMen.asp</c> con los
/// <c>TipoRanking</c> 10035–11060).
/// <c>Ranking_Silla_Ruedas.asp</c> consultaba directamente <c>SFEP_RankingFinal</c> (<c>TipoRanking = 15</c>, sin
/// <c>TOP</c> ni JOIN: licencias de silla y nombre de la propia tabla; ver <see cref="LicenciasRanking.Silla"/>).
/// El ASP concatenaba la licencia (<c>NumLicencia=" &amp; id</c>, sin comillas: inyección SQL) y el nombre (solo
/// duplicaba las comillas) en el SQL, pintaba el <c>TOP 100</c> del género elegido y consultaba
/// <c>SFEP_Ranking_Tipos</c> solo para el tipo de la primera fila. Si la licencia no existía, <c>rs_datos</c> quedaba sin
/// asignar y la página daba error 500.
/// Aquí: una sola consulta con todos los valores como parámetros (<c>@Tipos</c>, <c>@Licencia</c>, <c>@T0…</c>),
/// <c>ROW_NUMBER</c> para limitar por lista, <c>COUNT(*) OVER</c> para las coincidencias y los datos de cada lista.
/// <para>El SQL se compone solo con fragmentos fijos de esta clase.</para>
/// </summary>
public sealed class ClasificacionesRankingRepository(
    IDbConnectionFactory connectionFactory,
    IMediaUrlBuilder mediaUrls,
    IOptions<MediaOptions> media) : IClasificacionesRankingRepository
{
    // Mismas tablas, JOIN y filtro fijo (IdFederacion = 0) que el ASP. Orden del ASP: Posicion, Puntos DESC; la
    // licencia desempata para que el corte por @Limite sea estable.
    // SUPUESTO: columnas numéricas (el ASP las comparaba y pintaba como números); se convierten a int, los puntos
    // redondeados (en la web pública siempre son enteros).
    private const string SqlPuestosInicio = """
        WITH Clasificacion AS (
            SELECT r.TipoRanking,
                   CAST(r.Posicion AS int) AS Posicion,
                   CAST(r.NumLicencia AS int) AS Licencia,
        """;

    // Rankings de jugadores: nombre de la ficha (JOIN por LicenciaJugador, como CargarRnkAbs.asp y CargarRnkMen.asp).
    private const string NombresFicha = "\n               f.Nombre, f.Apellido1, f.Apellido2,";

    // Silla de ruedas: NumLicencia es la licencia de silla (SFEP_Fichas.LicenciaSilla, otra numeración), así que no
    // se cruza con la ficha; Ranking_Silla_Ruedas.asp pintaba Nombre/Apellido1/Apellido2 de SFEP_RankingFinal.*.
    // SUPUESTO: esas columnas existen en SFEP_RankingFinal (el ASP las leía de su SELECT SFEP_RankingFinal.*).
    private const string NombresRanking = "\n               r.Nombre, r.Apellido1, r.Apellido2,";

    private const string SqlPuestos = """

                   CAST(r.TorneosJugador AS int) AS TorneosTotales,
                   CAST(ROUND(r.PuntosTotal, 0) AS int) AS PuntosTotales,
                   CAST(r.Torneos AS int) AS TorneosRanking,
                   CAST(ROUND(r.Puntos, 0) AS int) AS PuntosRanking,
                   CAST(r.Sube AS int) AS Evolucion,
                   CAST(r.JornadasPuesto AS int) AS JornadasPuesto,
                   ROW_NUMBER() OVER (PARTITION BY r.TipoRanking ORDER BY r.Posicion, r.Puntos DESC, r.NumLicencia) AS Fila,
                   COUNT(*) OVER (PARTITION BY r.TipoRanking) AS Coincidencias
            FROM SFEP_RankingFinal AS r
        """;

    private const string JoinFicha = "\n        INNER JOIN SFEP_Fichas AS f ON f.LicenciaJugador = r.NumLicencia";

    private const string SqlPuestosFiltro = """

            WHERE r.IdFederacion = @IdFederacion
              AND r.TipoRanking IN @Tipos
        """;

    private const string FiltroLicencia = """

              AND r.NumLicencia = @Licencia
        """;

    // CargarRnkAbsNombre.asp buscaba en SFEP_Fichas.NombreCompleto con un LIKE '%término%' por palabra.
    // SUPUESTO: la intercalación de la BD distingue acentos; se compara sin distinguir mayúsculas ni acentos.
    private const string FiltroTermino = "\n      AND f.NombreCompleto COLLATE Latin1_General_CI_AI LIKE @T{0}";

    // Silla de ruedas (sin ficha): el nombre completo se compone con las columnas de SFEP_RankingFinal (CONCAT trata
    // NULL como cadena vacía).
    private const string FiltroTerminoRanking =
        "\n      AND CONCAT(r.Nombre, ' ', r.Apellido1, ' ', r.Apellido2) COLLATE Latin1_General_CI_AI LIKE @T{0}";

    private const string SqlPuestosFin = """

        )
        SELECT TipoRanking, Posicion, Licencia, Nombre, Apellido1, Apellido2, TorneosTotales, PuntosTotales,
               TorneosRanking, PuntosRanking, Evolucion, JornadasPuesto, Coincidencias
        FROM Clasificacion
        WHERE Fila <= @Limite
        ORDER BY TipoRanking, Fila;
        """;

    // Datos de cada lista (el ASP: "El ranking único tiene un total de … jugadores y están sumando … pruebas",
    // "Fecha de Actualización" y el PDF). SUPUESTO: FechaAct puede ser fecha o texto dd/mm/aaaa (el ASP la pintaba tal
    // cual): TRY_CONVERT con estilo 103 admite ambas; Jugadores/Pruebas numéricos.
    // En veteranos Jugadores, Pruebas y ArchivoPDF llegan vacíos (el ASP pintaba "un total de  jugadores" y un enlace a
    // refs/docs/): NULLIF evita que '' se convierta en 0 (o en 1900-01-01 la fecha); un 0 tampoco es un dato útil.
    private const string SqlTipos = """
        SELECT t.TipoRanking, t.ArchivoPDF,
               TRY_CONVERT(int, NULLIF(t.Jugadores, '')) AS Jugadores,
               TRY_CONVERT(int, NULLIF(t.Pruebas, '')) AS Pruebas,
               TRY_CONVERT(date, NULLIF(t.FechaAct, ''), 103) AS FechaAct
        FROM SFEP_Ranking_Tipos AS t
        WHERE t.IdFederacion = @IdFederacion
          AND t.TipoRanking IN @Tipos;
        """;

    public async Task<ClasificacionRankingDto> ObtenerAsync(
        DefinicionClasificacion ranking, CriteriosClasificacion criterios, CancellationToken cancellationToken)
    {
        var tipos = ranking.Tipos
            .Where(t => (criterios.Genero is null || t.Genero == criterios.Genero)
                        && (criterios.Categoria is null || t.Categoria == criterios.Categoria))
            .ToArray();

        var puestosPorTipo = new Dictionary<int, List<FilaPuesto>>();
        var datosPorTipo = new Dictionary<int, FilaTipo>();
        if (tipos.Length > 0)
        {
            var parametros = new DynamicParameters();
            parametros.Add("IdFederacion", ReglasClasificaciones.IdFederacionFep);
            parametros.Add("Tipos", tipos.Select(t => t.TipoRanking).ToArray());
            parametros.Add("Limite", criterios.Limite);

            var conFicha = ranking.Licencias == LicenciasRanking.Jugador;
            var sql = new StringBuilder(SqlPuestosInicio)
                .Append(conFicha ? NombresFicha : NombresRanking)
                .Append(SqlPuestos)
                .Append(conFicha ? JoinFicha : string.Empty)
                .Append(SqlPuestosFiltro);
            if (criterios.Licencia is { } licencia)
            {
                sql.Append(FiltroLicencia);
                parametros.Add("Licencia", licencia);
            }

            for (var i = 0; i < criterios.Terminos.Count; i++)
            {
                sql.AppendFormat(CultureInfo.InvariantCulture, conFicha ? FiltroTermino : FiltroTerminoRanking, i);
                parametros.Add($"T{i}", $"%{ReglasClasificaciones.EscaparLike(criterios.Terminos[i])}%");
            }

            sql.Append(SqlPuestosFin).Append("\n\n").Append(SqlTipos);

            await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            using var lector = await conexion.QueryMultipleAsync(
                new CommandDefinition(sql.ToString(), parametros, cancellationToken: cancellationToken));

            foreach (var fila in await lector.ReadAsync<FilaPuesto>())
            {
                if (!puestosPorTipo.TryGetValue(fila.TipoRanking, out var lista))
                {
                    puestosPorTipo[fila.TipoRanking] = lista = [];
                }

                lista.Add(fila);
            }

            foreach (var fila in await lector.ReadAsync<FilaTipo>())
            {
                datosPorTipo.TryAdd(fila.TipoRanking, fila);
            }
        }

        var listas = tipos.Select(t =>
        {
            var puestos = puestosPorTipo.GetValueOrDefault(t.TipoRanking) ?? [];
            var datos = datosPorTipo.GetValueOrDefault(t.TipoRanking);
            var pdf = Texto.Limpio(datos?.ArchivoPDF);
            var categoria = ReglasClasificaciones.NombreCategoria(t.Categoria);
            var genero = ReglasClasificaciones.NombreGenero(t.Genero);
            return new ListaRankingDto(
                t.TipoRanking,
                t.Genero,
                t.Categoria,
                t.Titulo ?? (categoria is null ? genero : $"{categoria} {genero.ToLowerInvariant()}"),
                datos?.Jugadores,
                datos?.Pruebas,
                datos?.FechaAct is { } fecha ? DateOnly.FromDateTime(fecha) : null,
                pdf is null ? null : $"{ReglasClasificaciones.CarpetaDocumentos}/{pdf}",
                mediaUrls.Construir(ReglasClasificaciones.CarpetaDocumentos, pdf),
                puestos.Count > 0 ? puestos[0].Coincidencias : 0,
                puestos.Select(p => new PuestoRankingDto(
                        p.Posicion,
                        p.Licencia,
                        ReglasClasificaciones.NombreJugador(p.Nombre, p.Apellido1, p.Apellido2),
                        p.TorneosTotales,
                        p.PuntosTotales,
                        p.TorneosRanking,
                        p.PuntosRanking,
                        p.Evolucion,
                        p.JornadasPuesto,
                        WebActual.Url(media.Value, ReglasClasificaciones.RutaEstadisticas(p.Licencia, ranking.Licencias))))
                    .ToArray());
        }).ToArray();

        return new ClasificacionRankingDto(
            ranking.Clave,
            ranking.Titulo,
            ranking.Generos.Select(g => new OpcionRankingDto(g, ReglasClasificaciones.NombreGenero(g))).ToArray(),
            ranking.Categorias.Select(c => new OpcionRankingDto(c, ReglasClasificaciones.NombreCategoria(c) ?? c)).ToArray(),
            criterios.Genero,
            criterios.Categoria,
            criterios.Licencia,
            criterios.Terminos.Count > 0 ? string.Join(' ', criterios.Terminos) : null,
            criterios.Limite,
            listas,
            ranking.PrefijoLicencia);
    }

    // SUPUESTO: TipoRanking entero (el ASP lo comparaba sin comillas).
    private sealed class FilaPuesto
    {
        public int TipoRanking { get; set; }
        public int Posicion { get; set; }
        public int Licencia { get; set; }
        public string? Nombre { get; set; }
        public string? Apellido1 { get; set; }
        public string? Apellido2 { get; set; }
        public int? TorneosTotales { get; set; }
        public int? PuntosTotales { get; set; }
        public int? TorneosRanking { get; set; }
        public int? PuntosRanking { get; set; }
        public int? Evolucion { get; set; }
        public int? JornadasPuesto { get; set; }
        public int Coincidencias { get; set; }
    }

    private sealed class FilaTipo
    {
        public int TipoRanking { get; set; }
        public string? ArchivoPDF { get; set; }
        public int? Jugadores { get; set; }
        public int? Pruebas { get; set; }
        public DateTime? FechaAct { get; set; }
    }
}
