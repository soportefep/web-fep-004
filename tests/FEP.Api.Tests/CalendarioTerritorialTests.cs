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
/// GET /api/v1/competicion/calendario/federaciones[/{id}] (migrado desde CalendarioFT.asp). Las pruebas y temporadas
/// necesitan SQL Server, así que se sustituyen por un repositorio falso; el territorio y el nombre oficial salen de
/// src/FEP.Api/Contenidos/federaciones-autonomicas.json (sin BD). La validación corta antes de tocar la BD.
/// </summary>
public sealed class CalendarioTerritorialTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Ruta = "/api/v1/competicion/calendario/federaciones";
    private static readonly DateTime Ahora = new(2026, 11, 13, 10, 0, 0);

    [Theory]
    [InlineData("0")]      // la FEP tiene su propio endpoint (/competicion/calendario)
    [InlineData("23")]     // DECISIÓN FEP: hay 22 federaciones (1–22)
    [InlineData("100")]
    [InlineData("1000")]   // "-----" del desplegable del ASP
    [InlineData("-1")]
    [InlineData("abc")]
    public async Task Id_no_valido_devuelve_400_con_ValidationProblemDetails(string id)
    {
        using var cliente = factory.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/{id}");

        await AssertErrorDeValidacion(respuesta, "id");
    }

    [Theory]
    [InlineData("anio=1900")]
    [InlineData("anio=2101")]
    [InlineData("anio=abc")]
    [InlineData("anio=2026%27%20and%20SFEP_Calendario.Federacion%3D6%20--")]
    public async Task Anio_no_valido_devuelve_400_con_ValidationProblemDetails(string query)
    {
        using var cliente = factory.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/6?{query}");

        await AssertErrorDeValidacion(respuesta, "anio");
    }

    [Fact]
    public async Task El_listado_completa_territorio_y_nombre_oficial_desde_Contenidos()
    {
        using var app = ConRepositorio(new RepositorioFalso());
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync(Ruta);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(respuesta.Headers.CacheControl?.Public);
        var lista = await respuesta.Content.ReadFromJsonAsync<FederacionCalendarioTerritorialDto[]>();

        Assert.NotNull(lista);
        Assert.Equal(new[] { 6, 22, 50 }, lista.Select(f => f.Id));
        var andalucia = lista[0];
        Assert.Equal("Andalucía", andalucia.Territorio);
        Assert.Equal("Federación Andaluza de Pádel", andalucia.Nombre);
        Assert.Equal("ANDALUZA", andalucia.Abreviatura);
        Assert.Equal(new[] { 2026, 2025 }, andalucia.Temporadas);
        // Castilla-La Mancha (22): el ASP la dejaba fuera del desplegable (BETWEEN 1 AND 21).
        Assert.Equal("Castilla-La Mancha", lista[1].Territorio);
        Assert.Empty(lista[1].Temporadas);
        // Sin entrada en Contenidos: la abreviatura de la BD.
        Assert.Equal("OTRA", lista[2].Territorio);
        Assert.Null(lista[2].Nombre);
    }

    [Fact]
    public async Task Pasa_al_repositorio_la_federacion_el_anio_y_el_anio_en_curso()
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        // Castilla-La Mancha (22), la última federación: pasa la validación (el falso no la tiene → 404, no 400).
        using (var castillaLaMancha = await cliente.GetAsync($"{Ruta}/22"))
        {
            Assert.Equal(HttpStatusCode.NotFound, castillaLaMancha.StatusCode);
            Assert.Equal(22, falso.FederacionRecibida);
        }

        var datos = await cliente.GetFromJsonAsync<CalendarioTerritorialDto>($"{Ruta}/6?anio=2025");
        Assert.Equal(6, falso.FederacionRecibida);
        Assert.Equal(2025, falso.AnioRecibido);
        Assert.Equal(2026, falso.AnioActualRecibido);
        Assert.Equal(2025, datos!.Anio);
        Assert.Equal("Andalucía", datos.Federacion.Territorio);

        using var respuesta = await cliente.GetAsync($"{Ruta}/6");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Null(falso.AnioRecibido);
    }

    [Fact]
    public async Task Federacion_inexistente_devuelve_404_con_la_url_de_la_web_actual()
    {
        using var app = ConRepositorio(new RepositorioFalso());
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/4");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.EndsWith("/CalendarioFT", problema.GetProperty("urlWebActual").GetString());
    }

    [Fact]
    public async Task El_json_usa_camelCase_y_la_misma_forma_de_prueba_que_el_calendario_FEP()
    {
        using var app = ConRepositorio(new RepositorioFalso());
        using var cliente = app.CreateClient();

        var json = await cliente.GetFromJsonAsync<JsonElement>($"{Ruta}/6");

        Assert.Equal(6, json.GetProperty("federacion").GetProperty("id").GetInt32());
        Assert.Equal(2026, json.GetProperty("anio").GetInt32());
        var temporada = json.GetProperty("temporadas")[0];
        Assert.Equal(2026, temporada.GetProperty("anio").GetInt32());
        Assert.Equal(JsonValueKind.Null, temporada.GetProperty("url").ValueKind);
        var prueba = json.GetProperty("items")[0];
        Assert.Equal("2026-03-06", prueba.GetProperty("fechaInicio").GetString());
        Assert.False(prueba.GetProperty("fechaPorConfirmar").GetBoolean());
        Assert.Equal("Menores", prueba.GetProperty("categoria").GetString());
        Assert.Equal(JsonValueKind.Null, prueba.GetProperty("enlace").ValueKind);
    }

    private static async Task AssertErrorDeValidacion(HttpResponseMessage respuesta, string parametro)
    {
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problema.GetProperty("status").GetInt32());
        Assert.Contains(problema.GetProperty("errors").EnumerateObject(),
            p => p.Name.Equals(parametro, StringComparison.OrdinalIgnoreCase));
    }

    private WebApplicationFactory<Program> ConRepositorio(ICalendarioCompeticionRepository repositorio)
        => factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddSingleton(repositorio);
            s.AddSingleton<TimeProvider>(new RelojFijo(new DateTimeOffset(Ahora, TimeSpan.Zero)));
        }));

    private sealed class RepositorioFalso : ICalendarioCompeticionRepository
    {
        public int? FederacionRecibida { get; private set; }
        public int? AnioRecibido { get; private set; }
        public int AnioActualRecibido { get; private set; }

        public Task<DatosCalendarioCompeticionDto> BuscarAsync(int? anio, int anioActual, CancellationToken cancellationToken)
            => throw new NotSupportedException("Calendario FEP: ver CalendarioTests.");

        public Task<IReadOnlyList<DatosFederacionCalendarioDto>> ListarFederacionesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DatosFederacionCalendarioDto>>(
            [
                new DatosFederacionCalendarioDto(6, "ANDALUZA", [2026, 2025]),
                new DatosFederacionCalendarioDto(22, "CASTILLA LA MANCHA", []),
                new DatosFederacionCalendarioDto(50, "OTRA", []),
            ]);

        public Task<DatosCalendarioTerritorialDto?> BuscarFederacionAsync(int federacion, int? anio, int anioActual, CancellationToken cancellationToken)
        {
            FederacionRecibida = federacion;
            AnioRecibido = anio;
            AnioActualRecibido = anioActual;
            if (federacion != 6)
            {
                return Task.FromResult<DatosCalendarioTerritorialDto?>(null);
            }

            var temporada = anio ?? 2026;
            return Task.FromResult<DatosCalendarioTerritorialDto?>(new DatosCalendarioTerritorialDto(
                new DatosFederacionCalendarioDto(6, "ANDALUZA", [2026, 2025]),
                temporada,
                [new TemporadaCalendarioDto(2026, null, null, null), new TemporadaCalendarioDto(2025, null, null, null)],
                [
                    new PruebaCalendarioCompeticionDto(temporada, new DateOnly(2026, 3, 6), new DateOnly(2026, 3, 8), false,
                        "TORNEO DE MENORES TYC 1", "Menores", "CLUB DE PRUEBA (SEVILLA)", null, null, false),
                ]));
        }
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
