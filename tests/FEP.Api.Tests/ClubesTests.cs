using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FEP.Api.Contracts.Clubes;
using FEP.Api.Data.Repositories.Clubes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace FEP.Api.Tests;

/// <summary>
/// GET /api/v1/clubes (migrado desde Clubes.asp). La consulta real necesita SQL Server: aquí se prueban
/// la validación de parámetros (que corta antes de tocar la BD) y, con un repositorio falso, cómo se
/// normalizan los criterios y el año de la licencia que se pasan al repositorio.
/// </summary>
public sealed class ClubesTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Ruta = "/api/v1/clubes";

    [Theory]
    [InlineData("federacion=0", "federacion")]
    [InlineData("federacion=23", "federacion")]   // DECISIÓN FEP: 22 federaciones (1–22)
    [InlineData("federacion=100", "federacion")]
    [InlineData("federacion=abc", "federacion")]
    [InlineData("provincia=0", "provincia")]
    [InlineData("pagina=0", "pagina")]
    [InlineData("limite=0", "limite")]
    [InlineData("limite=101", "limite")]
    [InlineData("busqueda=a+b+c+d+e+f+g+h+i", "busqueda")]
    public async Task Parametros_no_validos_devuelven_400_con_ValidationProblemDetails(string query, string campo)
    {
        using var cliente = factory.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        // Cuerpo ValidationProblemDetails: { "title", "status": 400, "errors": { "<campo>": [ ... ] } }
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problema.GetProperty("status").GetInt32());
        Assert.Contains(problema.GetProperty("errors").EnumerateObject(),
            p => p.Name.Equals(campo, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Una_busqueda_demasiado_larga_devuelve_400()
    {
        using var cliente = factory.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}?busqueda={new string('a', 101)}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Pasa_al_repositorio_los_criterios_normalizados_y_el_anio_en_curso()
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        var datos = await cliente.GetFromJsonAsync<ClubesPaginaDto>(
            $"{Ruta}?federacion=1&provincia=28&busqueda={Uri.EscapeDataString("  club  pádel-indoor ")}&pagina=2&limite=10");

        Assert.NotNull(falso.Recibido);
        Assert.Equal(1, falso.Recibido.Federacion);
        Assert.Equal(28, falso.Recibido.Provincia);
        Assert.Equal(new[] { "club", "pádel", "indoor" }, falso.Recibido.Terminos);
        Assert.Equal(2, falso.Recibido.Pagina);
        Assert.Equal(10, falso.Recibido.Limite);
        Assert.Equal(2026, falso.AnioRecibido);
        Assert.Equal("club pádel indoor", datos!.Busqueda);
    }

    [Fact]
    public async Task Sin_parametros_usa_la_primera_pagina_de_50_sin_filtros()
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync(Ruta);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(respuesta.Headers.CacheControl?.Public);
        Assert.NotNull(falso.Recibido);
        Assert.Null(falso.Recibido.Federacion);
        Assert.Null(falso.Recibido.Provincia);
        Assert.Empty(falso.Recibido.Terminos);
        Assert.Equal(1, falso.Recibido.Pagina);
        Assert.Equal(BusquedaClubes.LimitePorDefecto, falso.Recibido.Limite);
    }

    [Theory]
    [InlineData("100%", "100[%]")]
    [InlineData("a_b", "a[_]b")]
    [InlineData("[x]", "[[]x]")]
    public void Los_comodines_de_LIKE_se_buscan_literalmente(string termino, string esperado)
        => Assert.Equal(esperado, BusquedaClubes.EscaparLike(termino));

    private WebApplicationFactory<Program> ConRepositorio(IClubesRepository repositorio)
        => factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddSingleton(repositorio);
            s.AddSingleton<TimeProvider>(new RelojFijo(new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero)));
        }));

    private sealed class RepositorioFalso : IClubesRepository
    {
        public BusquedaClubes? Recibido { get; private set; }
        public int AnioRecibido { get; private set; }

        public Task<ClubesPaginaDto> BuscarAsync(BusquedaClubes busqueda, int anio, CancellationToken cancellationToken)
        {
            Recibido = busqueda;
            AnioRecibido = anio;
            return Task.FromResult(new ClubesPaginaDto(
                anio, busqueda.Federacion, busqueda.Provincia,
                busqueda.Terminos.Count > 0 ? string.Join(' ', busqueda.Terminos) : null,
                busqueda.Pagina, busqueda.Limite, 0, []));
        }

        public Task<ClubesFiltrosDto> ObtenerFiltrosAsync(CancellationToken cancellationToken)
            => Task.FromResult(new ClubesFiltrosDto([], []));
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
