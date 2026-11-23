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
/// GET /api/v1/competicion/rankings (migrado desde Rankings.asp). La consulta real necesita SQL Server: aquí se
/// prueban la forma del JSON y la caché (con un repositorio falso) y las reglas de <see cref="ReglasRankings"/>
/// (clave, erratas, enlaces).
/// </summary>
public sealed class RankingsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Ruta = "/api/v1/competicion/rankings";

    [Fact]
    public async Task Devuelve_la_lista_en_camelCase_y_cacheable()
    {
        using var app = ConRepositorio(new RepositorioFalso(
        [
            new RankingWebDto("absoluto", "Ranking Único FEP", "Ranking_Absoluto",
                "https://www.padelfederacion.es/Ranking_Absoluto", false,
                "images/RnkAbs.jpg", "https://www.padelfederacion.es/refs/images/RnkAbs.jpg", 1),
        ]));
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync(Ruta);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(respuesta.Headers.CacheControl?.Public);
        var json = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, json.ValueKind);
        var item = json[0];
        Assert.Equal("absoluto", item.GetProperty("clave").GetString());
        Assert.Equal("Ranking Único FEP", item.GetProperty("titulo").GetString());
        Assert.Equal("Ranking_Absoluto", item.GetProperty("enlace").GetString());
        Assert.Equal("https://www.padelfederacion.es/Ranking_Absoluto", item.GetProperty("enlaceUrl").GetString());
        Assert.False(item.GetProperty("nuevaVentana").GetBoolean());
        Assert.Equal("images/RnkAbs.jpg", item.GetProperty("imagen").GetString());
        Assert.Equal("https://www.padelfederacion.es/refs/images/RnkAbs.jpg", item.GetProperty("imagenUrl").GetString());
        Assert.Equal(1, item.GetProperty("orden").GetInt32());
    }

    [Fact]
    public async Task Sin_rankings_devuelve_lista_vacia()
    {
        using var app = ConRepositorio(new RepositorioFalso([]));
        using var cliente = app.CreateClient();

        var json = await cliente.GetFromJsonAsync<JsonElement>(Ruta);

        Assert.Equal(0, json.GetArrayLength());
    }

    // --- Reglas ------------------------------------------------------------------------------------

    [Theory]
    [InlineData("Ranking_Absoluto", "absoluto")]
    [InlineData("Ranking_Menores", "menores")]
    [InlineData("Ranking_Equipos_Absolutos", "equipos-absolutos")]
    [InlineData("Ranking_Silla_Ruedas", "silla-ruedas")]
    [InlineData("Ranking_Veteranos.asp", "veteranos")]
    [InlineData("/Ranking_Veteranos?x=1#a", "veteranos")]
    [InlineData("https://www.padelfederacion.es/Ranking_Menores", "menores")]
    [InlineData("Estadisticas.asp", "estadisticas")]
    public void La_clave_sale_del_enlace(string link, string esperada)
        => Assert.Equal(esperada, ReglasRankings.Clave(link, "Otro título", 3));

    [Theory]
    [InlineData(null, "Ranking Único FEP", "ranking-unico-fep")]
    [InlineData("#", "Ranking Silla Ruedas", "ranking-silla-ruedas")]
    [InlineData("  ", null, "ranking-7")]
    public void Sin_enlace_la_clave_sale_del_titulo_o_del_orden(string? link, string? titulo, string esperada)
        => Assert.Equal(esperada, ReglasRankings.Clave(link, titulo, 7));

    [Theory]
    [InlineData("Ranking Equipos Asbolutos", "Ranking Equipos Absolutos")]
    [InlineData("  Ranking   Menores ", "Ranking Menores")]
    [InlineData("Ranking Único FEP", "Ranking Único FEP")]
    [InlineData(null, "")]
    public void El_titulo_se_limpia_y_corrige_erratas(string? texto, string esperado)
        => Assert.Equal(esperado, ReglasRankings.Titulo(texto));

    [Theory]
    [InlineData(" Ranking_Absoluto ", "Ranking_Absoluto")]
    [InlineData("#", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void El_enlace_vacio_o_almohadilla_es_null(string? link, string? esperado)
        => Assert.Equal(esperado, ReglasRankings.Enlace(link));

    [Theory]
    [InlineData("Ranking_Absoluto", false)]
    [InlineData("https://www.padelfederacion.es/Ranking_Absoluto", false)]
    [InlineData("http://circuitomenores.padelfederacion.es/Ranking", false)]
    [InlineData("https://www.ejemplo.com/ranking", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData(null, false)]
    public void Solo_los_enlaces_de_otro_dominio_abren_ventana_nueva(string? link, bool esperado)
        => Assert.Equal(esperado, ReglasRankings.EsExterno(link));

    private WebApplicationFactory<Program> ConRepositorio(IRankingsWebRepository repositorio)
        => factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(repositorio)));

    private sealed class RepositorioFalso(IReadOnlyList<RankingWebDto> items) : IRankingsWebRepository
    {
        public Task<IReadOnlyList<RankingWebDto>> ObtenerAsync(CancellationToken cancellationToken)
            => Task.FromResult(items);
    }
}
