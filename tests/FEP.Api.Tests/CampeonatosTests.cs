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
/// GET /api/v1/competicion/campeonatos (migrado desde Campeonatos.asp + Torneos_Previo.asp). La consulta
/// real necesita SQL Server: aquí se prueban la validación (corta antes de tocar la BD), los criterios
/// normalizados que llegan al repositorio (con un repositorio falso) y las reglas heredadas del ASP
/// (plazo de inscripción, enlace a la ficha, categorías).
/// </summary>
public sealed class CampeonatosTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Ruta = "/api/v1/competicion/campeonatos";
    private static readonly DateTime Ahora = new(2026, 11, 13, 10, 0, 0);

    [Theory]
    [InlineData("anio=1900", "anio")]
    [InlineData("anio=2101", "anio")]
    [InlineData("anio=abc", "anio")]
    [InlineData("tipo=individual", "tipo")]
    [InlineData("tipo=--", "tipo")]
    [InlineData("categoria=SEL_ABS", "categoria")]
    [InlineData("categoria=1%27%20OR%201%3D1", "categoria")]
    public async Task Parametros_no_validos_devuelven_400_con_ValidationProblemDetails(string query, string campo)
    {
        using var cliente = factory.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problema.GetProperty("status").GetInt32());
        Assert.Contains(problema.GetProperty("errors").EnumerateObject(),
            p => p.Name.Equals(campo, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Pasa_al_repositorio_los_criterios_normalizados_y_la_hora_local()
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        var datos = await cliente.GetFromJsonAsync<CampeonatosDto>($"{Ruta}?anio=2025&tipo=EQUIPOS&categoria=Veteranos");

        Assert.NotNull(falso.Recibido);
        Assert.Equal(2025, falso.Recibido.Anio);
        Assert.Equal("equipos", falso.Recibido.Tipo);
        Assert.Equal("veteranos", falso.Recibido.Categoria);
        Assert.Equal(Ahora, falso.AhoraRecibido);
        Assert.Equal("equipos", datos!.Tipo);
    }

    [Fact]
    public async Task Sin_parametros_no_filtra_y_deja_el_anio_al_repositorio()
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync(Ruta);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(respuesta.Headers.CacheControl?.Public);
        Assert.NotNull(falso.Recibido);
        Assert.Null(falso.Recibido.Anio);
        Assert.Null(falso.Recibido.Tipo);
        Assert.Null(falso.Recibido.Categoria);
    }

    [Fact]
    public async Task El_json_usa_camelCase_y_fechas_sin_hora()
    {
        var falso = new RepositorioFalso
        {
            Items =
            [
                new CampeonatoDto(31277, 2026, "X OPEN NACIONAL", "parejas", "absoluto", false, true,
                    new DateOnly(2026, 11, 27), new DateOnly(2026, 11, 29), "ZAMUDIO", [],
                    "https://www.padelfederacion.es/Camp_Parejas.asp?IdTorneo=31277", false,
                    new PlazoInscripcionDto("inscripcion", new DateTime(2026, 11, 20, 12, 0, 0))),
            ],
        };
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        var json = await cliente.GetFromJsonAsync<JsonElement>(Ruta);

        var item = json.GetProperty("items")[0];
        Assert.Equal("2026-11-27", item.GetProperty("fechaInicio").GetString());
        Assert.True(item.GetProperty("sillaRuedas").GetBoolean());
        Assert.Equal(0, item.GetProperty("clubes").GetArrayLength());
        Assert.Equal("2026-11-20T12:00:00", item.GetProperty("inscripcion").GetProperty("fechaLimite").GetString());
    }

    // --- Reglas heredadas -------------------------------------------------------------------------

    [Fact]
    public void Plazo_cerrado_si_ninguna_fecha_es_posterior_a_ahora()
        => Assert.Null(ReglasCampeonatos.Plazo("parejas", Ahora.AddDays(-1), Ahora.AddDays(-2), Ahora));

    [Fact]
    public void Plazo_de_parejas_es_la_inscripcion()
    {
        var plazo = ReglasCampeonatos.Plazo("parejas", Ahora.AddDays(7), Ahora.AddDays(3), Ahora);
        Assert.Equal(new PlazoInscripcionDto("inscripcion", Ahora.AddDays(7)), plazo);
    }

    [Fact]
    public void Equipos_con_preinscripcion_abierta_muestran_la_preinscripcion()
    {
        var plazo = ReglasCampeonatos.Plazo("equipos", Ahora.AddDays(20), Ahora.AddDays(5), Ahora);
        Assert.Equal(new PlazoInscripcionDto("preinscripcion", Ahora.AddDays(5)), plazo);
    }

    [Fact]
    public void Equipos_con_preinscripcion_igual_a_la_inscripcion_es_inscripcion_de_equipos()
    {
        var plazo = ReglasCampeonatos.Plazo("equipos", Ahora.AddDays(5), Ahora.AddDays(5), Ahora);
        Assert.Equal(new PlazoInscripcionDto("inscripcion-equipos", Ahora.AddDays(5)), plazo);
    }

    [Fact]
    public void Equipos_con_preinscripcion_cerrada_muestran_la_inscripcion_de_equipos()
    {
        var plazo = ReglasCampeonatos.Plazo("equipos", Ahora.AddDays(5), Ahora.AddDays(-5), Ahora);
        Assert.Equal(new PlazoInscripcionDto("inscripcion-equipos", Ahora.AddDays(5)), plazo);
    }

    [Fact]
    public void Parejas_con_solo_la_preinscripcion_abierta_no_muestran_una_fecha_pasada()
    {
        // El ASP pintaba aquí la fecha de inscripción ya vencida.
        var plazo = ReglasCampeonatos.Plazo("parejas", Ahora.AddDays(-1), Ahora.AddDays(2), Ahora);
        Assert.Equal(new PlazoInscripcionDto("preinscripcion", Ahora.AddDays(2)), plazo);
    }

    [Theory]
    [InlineData("padelcv.com/competicion/torneos/x/", "https://padelcv.com/competicion/torneos/x/")]
    [InlineData("  https://ejemplo.es/cuadros  ", "https://ejemplo.es/cuadros")]
    [InlineData("http://ejemplo.es/a b", "http://ejemplo.es/a%20b")]
    [InlineData("\\\\ejemplo.es\\cuadros", "https://ejemplo.es/cuadros")]
    [InlineData("", null)]
    [InlineData("#", null)]
    [InlineData(null, null)]
    public void El_enlace_a_los_cuadros_es_una_url_https_absoluta(string? link, string? esperado)
        => Assert.Equal(esperado, ReglasCampeonatos.EnlaceCuadros(link));

    [Theory]
    [InlineData(100, "MENORES   ", "PAREJAS", 0, "N", "N", "http://circuitomenores.padelfederacion.es/Campeonato?IdTorneo=100")]
    [InlineData(100, "MENORES", "EQUIPOS", 0, "N", "N", "Camp_Equipos.asp?IdTorneo=100")]
    [InlineData(100, "MENORES", "PAREJAS", 55, "N", "N", "Camp_Clasificatorio.asp?IdTorneo=100")]
    [InlineData(19721, "MENORES", "PAREJAS", 55, "N", "N", "http://circuitomenores.padelfederacion.es/CEM-2021")]
    [InlineData(100, "SEL_MEN", "EQUIPOS", 0, "N", "N", "Camp_Equipos.asp?IdTorneo=100")]
    [InlineData(100, "ABSOLUTO", "EQUIPOS", 0, "N", "S", "Empr_Equipos.asp?IdTorneo=100")]
    [InlineData(100, "VETERANOS", "PAREJAS", 0, "S", "N", "Camp_Liga.asp?IdTorneo=100")]
    [InlineData(100, "ABSOLUTO", "PAREJAS", null, null, null, "Camp_Parejas.asp?IdTorneo=100")]
    public void La_ficha_sigue_las_reglas_de_Torneos_Previo(
        int id, string modalidad, string tipo, int? padre, string? liga, string? empresas, string esperado)
        => Assert.Equal(esperado, ReglasCampeonatos.RutaFicha(id, modalidad, tipo, padre, liga, empresas));

    [Theory]
    [InlineData("ABSOLUTO", "absoluto", false)]
    [InlineData("SEL_ABS", "absoluto", true)]
    [InlineData("MENORES   ", "menores", false)]
    [InlineData("SEL_MEN", "menores", true)]
    [InlineData("SEL_VET", "veteranos", true)]
    [InlineData("OTRA", null, false)]
    [InlineData(null, null, false)]
    public void Categorias_de_ModalidadTorneo(string? modalidad, string? categoria, bool seleccion)
        => Assert.Equal((categoria, seleccion), ReglasCampeonatos.CategoriaDesdeBd(modalidad));

    [Fact]
    public void Silla_de_ruedas_solo_en_absoluto_con_circuito_SILLA()
    {
        Assert.True(ReglasCampeonatos.EsSillaRuedas("ABSOLUTO  ", "SILLA "));
        Assert.False(ReglasCampeonatos.EsSillaRuedas("VETERANOS", "SILLA"));
        Assert.False(ReglasCampeonatos.EsSillaRuedas("ABSOLUTO", null));
    }

    private WebApplicationFactory<Program> ConRepositorio(ICampeonatosRepository repositorio)
        => factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddSingleton(repositorio);
            s.AddSingleton<TimeProvider>(new RelojFijo(new DateTimeOffset(Ahora, TimeSpan.Zero)));
        }));

    private sealed class RepositorioFalso : ICampeonatosRepository
    {
        public BusquedaCampeonatos? Recibido { get; private set; }
        public DateTime AhoraRecibido { get; private set; }
        public IReadOnlyList<CampeonatoDto> Items { get; init; } = [];

        public Task<CampeonatosDto> BuscarAsync(BusquedaCampeonatos busqueda, DateTime ahora, CancellationToken cancellationToken)
        {
            Recibido = busqueda;
            AhoraRecibido = ahora;
            return Task.FromResult(new CampeonatosDto(busqueda.Anio ?? 2026, [2026, 2025], busqueda.Tipo, busqueda.Categoria, Items));
        }
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
