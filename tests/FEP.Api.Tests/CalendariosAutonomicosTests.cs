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
/// GET /api/v1/competicion/calendarios-autonomicos/{categoria} (migrado desde Calendario_Menores_FFAA.asp y
/// Calendario_Veteranos_FFAA.asp). No usa SQL Server: el contenido sale de
/// src/FEP.Api/Contenidos/calendarios-autonomicos/{categoria}.json.
/// </summary>
public sealed class CalendariosAutonomicosTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Ruta = "/api/v1/competicion/calendarios-autonomicos";

    private async Task<CalendarioAutonomicoDto> ObtenerAsync(string categoria)
    {
        using var cliente = factory.CreateClient();
        var datos = await cliente.GetFromJsonAsync<CalendarioAutonomicoDto>($"{Ruta}/{categoria}");
        Assert.NotNull(datos);
        return datos;
    }

    private Task<CalendarioAutonomicoDto> ObtenerMenoresAsync() => ObtenerAsync("menores");

    private Task<CalendarioAutonomicoDto> ObtenerVeteranosAsync() => ObtenerAsync("veteranos");

    [Fact]
    public async Task Menores_devuelve_las_17_federaciones_y_75_pruebas_del_original()
    {
        var datos = await ObtenerMenoresAsync();

        Assert.Equal("menores", datos.Categoria);
        Assert.Equal(2026, datos.Temporada);
        Assert.Equal(17, datos.Federaciones.Count);
        Assert.Equal(75, datos.Federaciones.Sum(f => f.Pruebas.Count));
        Assert.Equal("andalucia", datos.Federaciones[0].Clave);
        Assert.Equal("pais-vasco", datos.Federaciones[^1].Clave);
        // Las claves sirven de ancla en la página: no pueden repetirse.
        Assert.Equal(datos.Federaciones.Count, datos.Federaciones.Select(f => f.Clave).Distinct().Count());
        // Ninguna federación supera el máximo del RTG.
        Assert.All(datos.Federaciones, f => Assert.True(f.Pruebas.Count <= datos.MaximoPruebasPorFederacion));
    }

    [Fact]
    public async Task Las_pruebas_tienen_fechas_coherentes_de_la_temporada()
    {
        var datos = await ObtenerMenoresAsync();

        var pruebas = datos.Federaciones.SelectMany(f => f.Pruebas).ToArray();
        Assert.All(pruebas, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Fechas));
            Assert.NotNull(p.FechaInicio);
            Assert.NotNull(p.FechaFin);
            Assert.True(p.FechaInicio <= p.FechaFin);
            Assert.Equal(datos.Temporada, p.FechaFin!.Value.Year);
            Assert.NotNull(p.Fases);
        });
        // "Del 31 al 6 de septiembre" (Federación Vasca) se interpreta como del 31 de agosto al 6 de septiembre.
        var vasca = datos.Federaciones.Single(f => f.Clave == "pais-vasco").Pruebas.Single(p => p.Orden == 3);
        Assert.Equal(new DateOnly(2026, 8, 31), vasca.FechaInicio);
    }

    [Fact]
    public async Task Solo_la_prueba_de_Navarra_no_esta_en_el_ranking_y_el_campeonato_catalan_tiene_dos_fases()
    {
        var datos = await ObtenerMenoresAsync();

        var fuera = datos.Federaciones.SelectMany(f => f.Pruebas.Where(p => !p.EnRanking).Select(_ => f.Clave)).ToArray();
        Assert.Equal(["navarra"], fuera);
        var catalan = datos.Federaciones.Single(f => f.Clave == "cataluna").Pruebas.Single(p => p.Fases.Count > 0);
        Assert.True(catalan.CampeonatoAutonomico);
        Assert.Equal(2, catalan.Fases.Count);
    }

    [Fact]
    public async Task Incluye_los_tres_momentos_de_incorporacion_al_ranking()
    {
        var datos = await ObtenerMenoresAsync();

        Assert.NotNull(datos.ActualizacionesRanking);
        Assert.Equal([1, 2, 3], datos.ActualizacionesRanking.Momentos.Select(m => m.Orden));
        Assert.Equal(new DateOnly(2026, 7, 8), datos.ActualizacionesRanking.Momentos[1].Fecha);
        Assert.False(string.IsNullOrWhiteSpace(datos.Participacion));
    }

    [Fact]
    public async Task La_categoria_no_distingue_mayusculas()
    {
        using var cliente = factory.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/MENORES");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Categoria_no_admitida_responde_400()
    {
        using var cliente = factory.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/absoluto");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Veteranos_devuelve_las_17_federaciones_y_19_pruebas_del_original()
    {
        var datos = await ObtenerVeteranosAsync();

        Assert.Equal("veteranos", datos.Categoria);
        Assert.Equal(2026, datos.Temporada);
        Assert.Equal(4, datos.MaximoPruebasPorFederacion);
        Assert.Equal(17, datos.Federaciones.Count);
        Assert.Equal(19, datos.Federaciones.Sum(f => f.Pruebas.Count));
        Assert.Equal("andalucia", datos.Federaciones[0].Clave);
        Assert.Equal("pais-vasco", datos.Federaciones[^1].Clave);
        Assert.Equal(datos.Federaciones.Count, datos.Federaciones.Select(f => f.Clave).Distinct().Count());
        Assert.All(datos.Federaciones, f => Assert.True(f.Pruebas.Count <= datos.MaximoPruebasPorFederacion));
        // Veteranos no tiene "momentos" de incorporación al ranking.
        Assert.Null(datos.ActualizacionesRanking);
        Assert.False(string.IsNullOrWhiteSpace(datos.Participacion));
    }

    [Fact]
    public async Task Veteranos_las_federaciones_sin_pruebas_llegan_con_lista_vacia()
    {
        var datos = await ObtenerVeteranosAsync();

        var sinPruebas = datos.Federaciones.Where(f => f.Pruebas.Count == 0).Select(f => f.Clave).ToArray();
        Assert.Equal(
            new[] { "cantabria", "castilla-la-mancha", "murcia", "baleares", "asturias", "galicia", "melilla", "pais-vasco" },
            sinPruebas);
    }

    [Fact]
    public async Task Veteranos_marca_en_el_ranking_las_11_pruebas_en_verde_del_original()
    {
        var datos = await ObtenerVeteranosAsync();

        var pruebas = datos.Federaciones.SelectMany(f => f.Pruebas).ToArray();
        Assert.Equal(11, pruebas.Count(p => p.EnRanking));
        Assert.Equal(9, pruebas.Count(p => p.CampeonatoAutonomico));
        Assert.All(pruebas, p =>
        {
            Assert.NotNull(p.FechaInicio);
            Assert.NotNull(p.FechaFin);
            Assert.True(p.FechaInicio <= p.FechaFin);
            Assert.Equal(datos.Temporada, p.FechaFin!.Value.Year);
            Assert.Empty(p.Fases);
        });
        // Pruebas de un solo día: fechaFin = fechaInicio.
        var extremadura = datos.Federaciones.Single(f => f.Clave == "extremadura").Pruebas.Single(p => p.Orden == 1);
        Assert.Equal(extremadura.FechaInicio, extremadura.FechaFin);
        // Ninguna prueba de Navarra estaba en verde.
        Assert.DoesNotContain(datos.Federaciones.Single(f => f.Clave == "navarra").Pruebas, p => p.EnRanking);
    }

    [Fact]
    public async Task Categoria_sin_contenido_publicado_responde_404_con_la_pagina_de_la_web_actual()
    {
        // Las dos categorías ya tienen contenido: se simula uno no publicado con un repositorio que no encuentra nada.
        using var cliente = ConRepositorio(new RepositorioVacio()).CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/veteranos");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.EndsWith("/Calendario_Veteranos_FFAA", problema.RootElement.GetProperty("urlWebActual").GetString());
    }

    [Theory]
    [InlineData("menores")]
    [InlineData("veteranos")]
    public async Task Responde_con_cache_publica(string categoria)
    {
        using var cliente = factory.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/{categoria}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(respuesta.Headers.CacheControl?.Public);
    }

    private WebApplicationFactory<Program> ConRepositorio(ICalendariosAutonomicosRepository repositorio)
        => factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(repositorio)));

    /// <summary>Ninguna categoría publicada.</summary>
    private sealed class RepositorioVacio : ICalendariosAutonomicosRepository
    {
        public Task<CalendarioAutonomicoDto?> ObtenerAsync(string categoria, CancellationToken cancellationToken)
            => Task.FromResult<CalendarioAutonomicoDto?>(null);
    }
}
