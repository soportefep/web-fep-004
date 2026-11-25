using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FEP.Api.Contracts.Competicion;
using FEP.Api.Data.Repositories.Competicion;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace FEP.Api.Tests;

/// <summary>
/// GET /api/v1/competicion/jugadores/{licencia}/estadisticas (migrado desde Estadisticas.asp y CargarPruebas.asp). Las
/// consultas necesitan SQL Server: aquí se prueban la validación de la licencia (corta antes de tocar la BD), los 404
/// y la forma del JSON con un repositorio falso, y las reglas de <see cref="ReglasEstadisticas"/> (categoría por edad,
/// balance de partidos, porcentajes, periodo, rondas y puntos de la categoría anterior).
/// </summary>
public sealed class EstadisticasJugadorTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Ruta = "/api/v1/competicion/jugadores";

    // --- Validación y estados ------------------------------------------------------------------------------------

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("12762%20OR%201%3D1")]   // el ASP concatenaba la licencia en el SQL
    [InlineData("99999999999")]          // fuera de int
    public async Task Licencias_no_validas_devuelven_400_con_ValidationProblemDetails(string licencia)
    {
        // Sin repositorio falso ni cadena de conexión: si llegara a la BD respondería 500.
        using var cliente = factory.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/{licencia}/estadisticas");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(problema.GetProperty("errors").EnumerateObject(),
            p => p.Name.Equals("licencia", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(EstadoEstadisticas.LicenciaNoEncontrada, "licencia-no-encontrada")]
    [InlineData(EstadoEstadisticas.SinRanking, "sin-ranking")]
    public async Task Sin_estadisticas_devuelve_404_con_motivo_y_la_web_actual(EstadoEstadisticas estado, string motivo)
    {
        using var app = ConRepositorio(new RepositorioFalso(estado));
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/1/estadisticas");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(motivo, problema.GetProperty("motivo").GetString());
        Assert.Equal("https://www.padelfederacion.es/Estadisticas?id=1", problema.GetProperty("urlWebActual").GetString());
    }

    [Fact]
    public async Task Devuelve_las_estadisticas_en_camelCase_con_cache_publica()
    {
        var falso = new RepositorioFalso(EstadoEstadisticas.Encontradas);
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/12762/estadisticas");
        var datos = await respuesta.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(respuesta.Headers.CacheControl?.Public);
        Assert.Equal(12762, falso.LicenciaRecibida);
        Assert.Equal(12762, datos.GetProperty("licencia").GetInt32());
        Assert.Equal("CAT-40", datos.GetProperty("categoria").GetString());
        var partidos = datos.GetProperty("balance").GetProperty("partidos");
        Assert.Equal(1, partidos.GetProperty("ganados").GetInt32());
        Assert.Equal(7, partidos.GetProperty("total").GetInt32());
        Assert.Equal(14.3m, partidos.GetProperty("porcentajeGanados").GetDecimal());
        var ranking = datos.GetProperty("rankings")[0];
        Assert.Equal("absoluto", ranking.GetProperty("clave").GetString());
        Assert.Equal(40, ranking.GetProperty("pruebas")[0].GetProperty("porcentaje").GetInt32());
        Assert.Equal("2026-11-04", datos.GetProperty("evolucion").GetProperty("puntos")[0].GetProperty("fecha").GetString());
    }

    // --- Reglas ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1986, "CAT-40")]
    [InlineData(1971, "CAT-55")]
    [InlineData(1976, "CAT-50")]
    [InlineData(1991, "CAT-35")]
    [InlineData(1995, "SENIOR")]     // 31 años: el ASP no asignaba ninguna (corrección)
    [InlineData(2003, "SUB-23")]
    [InlineData(2008, "JUNIOR")]
    [InlineData(2010, "CADETE")]
    [InlineData(2012, "INFANTIL")]
    [InlineData(2014, "ALEVIN")]
    [InlineData(2016, "BENJAMIN")]
    [InlineData(2026, "SENIOR")]     // edad 0: como el ASP
    public void La_categoria_por_edad_es_la_del_ASP(int anioNacimiento, string esperada)
        => Assert.Equal(esperada, ReglasEstadisticas.CategoriaPorEdad(anioNacimiento, 2026));

    [Fact]
    public void Sin_fecha_de_nacimiento_no_hay_categoria()
        => Assert.Null(ReglasEstadisticas.CategoriaPorEdad(null, 2026));

    [Fact]
    public void El_balance_cuenta_partidos_sets_y_juegos_desde_la_pareja_del_jugador()
    {
        ResultadoPartido[] partidos =
        [
            // Pareja 1 (la nuestra) gana 6-4 3-6 6-2.
            new(5, 5, 9, "S", "N", 6, 4, 3, 6, 6, 2),
            // Somos la pareja 2 y perdemos 6-1 6-0 (el tercer set 0-0 no cuenta).
            new(5, 7, 5, "S", "N", 6, 1, 6, 0, 0, 0),
        ];

        var balance = ReglasEstadisticas.Balance(partidos);

        Assert.Equal((1, 1), (balance.Partidos.Ganados, balance.Partidos.Perdidos));
        Assert.Equal((2, 3), (balance.Sets.Ganados, balance.Sets.Perdidos));
        Assert.Equal((6 + 3 + 6 + 1 + 0, 4 + 6 + 2 + 6 + 6), (balance.Juegos.Ganados, balance.Juegos.Perdidos));
    }

    [Fact]
    public void Un_set_sin_dato_vale_cero()
    {
        var balance = ReglasEstadisticas.Balance([new(3, 3, 4, "N", "S", 4, 6, null, null, null, null)]);

        Assert.Equal((0, 1), (balance.Partidos.Ganados, balance.Partidos.Perdidos));
        Assert.Equal((0, 1), (balance.Sets.Ganados, balance.Sets.Perdidos));
    }

    [Theory]
    [InlineData(1, 6, 14.3, 85.7)]     // web pública, licencia 12762
    [InlineData(3, 8, 27.3, 72.7)]
    [InlineData(47, 56, 45.6, 54.4)]
    public void Los_porcentajes_son_los_del_ASP(int ganados, int perdidos, double pctGanados, double pctPerdidos)
    {
        var recuento = ReglasEstadisticas.Recuento(ganados, perdidos);

        Assert.Equal((decimal)pctGanados, recuento.PorcentajeGanados);
        Assert.Equal((decimal)pctPerdidos, recuento.PorcentajePerdidos);
        Assert.Equal(ganados + perdidos, recuento.Total);
    }

    [Fact]
    public void Sin_partidos_no_hay_porcentaje()
    {
        var recuento = ReglasEstadisticas.Recuento(0, 0);
        Assert.Null(recuento.PorcentajeGanados);
        Assert.Null(recuento.PorcentajePerdidos);
    }

    [Fact]
    public void El_periodo_es_un_anio_antes_de_la_actualizacion_y_fijo_en_menores()
    {
        Assert.Equal(new DateOnly(2025, 11, 4), ReglasEstadisticas.InicioPeriodo(new DateOnly(2026, 11, 4), "ABSOLUTO"));
        Assert.Equal(new DateOnly(2023, 2, 28), ReglasEstadisticas.InicioPeriodo(new DateOnly(2024, 2, 29), null)); // el ASP fallaba
        Assert.Equal(ReglasEstadisticas.InicioPeriodoMenores, ReglasEstadisticas.InicioPeriodo(new DateOnly(2026, 11, 4), "MENORES  "));
        Assert.Null(ReglasEstadisticas.InicioPeriodo(null, "ABSOLUTO"));
    }

    [Theory]
    [InlineData(0, "Campeón")]
    [InlineData(5, "Dieciseisavos de final")]
    [InlineData(8, null)]
    public void Las_rondas_tienen_nombre_completo(int avance, string? esperada)
        => Assert.Equal(esperada, ReglasEstadisticas.Ronda(avance));

    [Theory]
    [InlineData(1480.0, 40, 592)]      // web pública: MASTER MENORES BABOLAT 2025, "592 (40%)"
    [InlineData(1044.0, 100, 1044)]
    [InlineData(500.5, 40, 200)]       // cint(500.5) = 500 (redondeo al par) × 0,4
    public void Los_puntos_de_la_categoria_anterior_cuentan_el_40_por_ciento(double puntos, int porcentaje, int esperados)
        => Assert.Equal(esperados, ReglasEstadisticas.PuntosComputados(puntos, porcentaje));

    [Theory]
    [InlineData(2015, 2015, 2016, true)]
    [InlineData(2016, 2015, 2016, true)]
    [InlineData(2017, 2015, 2016, false)]   // juega en una categoría superior a la suya
    [InlineData(null, 2015, 2016, false)]
    public void La_categoria_anterior_solo_suma_a_los_nacidos_en_la_categoria(int? nacimiento, int? inicial, int? final, bool suma)
        => Assert.Equal(suma, ReglasEstadisticas.SumaCategoriaAnterior(nacimiento, inicial, final));

    [Fact]
    public void Los_tipos_declarados_dan_el_ranking_y_la_lista()
    {
        var alevinF = ReglasEstadisticas.BuscarTipo(7001);
        Assert.NotNull(alevinF);
        Assert.Equal("menores", alevinF.Value.Ranking.Clave);
        Assert.Equal("Alevín femenino", ReglasEstadisticas.NombreLista(alevinF.Value.Tipo));
        Assert.Null(ReglasEstadisticas.BuscarTipo(10100));   // "CAT" con posición 0 en la web pública
        Assert.Equal("Otro ranking", ReglasEstadisticas.NombreRankingPorCategoria("CAT"));
        Assert.Equal("Ranking de Veteranos", ReglasEstadisticas.NombreRankingPorCategoria("CAT35     "));
    }

    // --- Repositorio falso ----------------------------------------------------------------------------------------

    private WebApplicationFactory<Program> ConRepositorio(IEstadisticasJugadorRepository repositorio)
        => factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(repositorio)));

    private sealed class RepositorioFalso(EstadoEstadisticas estado) : IEstadisticasJugadorRepository
    {
        public int? LicenciaRecibida { get; private set; }

        public Task<ConsultaEstadisticas> ObtenerAsync(int licencia, DateTime ahora, CancellationToken cancellationToken)
        {
            LicenciaRecibida = licencia;
            if (estado != EstadoEstadisticas.Encontradas)
            {
                return Task.FromResult(new ConsultaEstadisticas(estado, null));
            }

            var dto = new EstadisticasJugadorDto(
                licencia, "ALVARO MATADOR GARCIA DE VINUESA", null, null,
                ReglasEstadisticas.CategoriaPorEdad(1986, 2026), 2026, "SNOBISSIMO PADEL", "CLUB R.E.P.",
                new DateOnly(2025, 11, 4),
                new BalancePartidosDto(ReglasEstadisticas.Recuento(1, 6), ReglasEstadisticas.Recuento(3, 8), ReglasEstadisticas.Recuento(47, 56)),
                new ParejaJugadorDto(1, "SERGIO AREVALILLO ASENJO", "https://www.padelfederacion.es/Estadisticas?id=1"),
                new MejorClasificacionDto(1, 5, "Dieciseisavos de final", "Primera categoría", 1),
                [
                    new RankingJugadorDto(10000, "absoluto", "Ranking Único Absoluto", "Masculino", "M", "ABSOLUTO", 1748, 5, 3289, 5, 3289,
                        new DateOnly(2026, 11, 4), new DateOnly(2025, 11, 4),
                        [new PruebaRankingJugadorDto(1, "MASTER", new DateOnly(2025, 11, 20), null, "Campeón", null, 1480, 40, 592, true)]),
                ],
                new EvolucionRankingDto([10000], [new PuntoEvolucionDto(new DateOnly(2026, 11, 4), 1748, 3289)]),
                [],
                $"https://www.padelfederacion.es/Estadisticas?id={licencia}",
                ahora);
            return Task.FromResult(new ConsultaEstadisticas(estado, dto));
        }
    }
}
