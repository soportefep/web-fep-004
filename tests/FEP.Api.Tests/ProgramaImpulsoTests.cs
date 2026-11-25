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
/// GET /api/v1/competicion/programa-impulso (migrado desde Impulso.asp). La presentación y los documentos
/// salen de src/FEP.Api/Contenidos/programa-impulso.json (sin SQL Server); los torneos necesitan la BD, así
/// que se sustituyen por un repositorio falso. La validación corta antes de tocar la BD.
/// </summary>
public sealed class ProgramaImpulsoTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Ruta = "/api/v1/competicion/programa-impulso";
    private static readonly DateTime Ahora = new(2026, 11, 13, 10, 0, 0);

    [Theory]
    [InlineData("anio=1900")]
    [InlineData("anio=2101")]
    [InlineData("anio=abc")]
    [InlineData("anio=2026%27%20OR%201%3D1")]
    public async Task Anio_no_valido_devuelve_400_con_ValidationProblemDetails(string query)
    {
        using var cliente = factory.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(problema.GetProperty("errors").EnumerateObject(),
            p => p.Name.Equals("anio", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Devuelve_la_presentacion_y_los_dos_documentos_con_url_absoluta_codificada()
    {
        using var app = ConRepositorio(new RepositorioFalso());
        using var cliente = app.CreateClient();

        var datos = await cliente.GetFromJsonAsync<ProgramaImpulsoDto>(Ruta);

        Assert.NotNull(datos);
        Assert.Equal("Programa Impulso", datos.Titulo);
        Assert.NotEmpty(datos.Introduccion);
        Assert.Equal(["informacion-2026", "calendario-2026"], datos.Documentos.Select(d => d.Clave));
        Assert.All(datos.Documentos, d => Assert.StartsWith("https://", d.Url));
        // "Programa-Impulso-2026 15-01-26.pdf" lleva un espacio: se codifica.
        Assert.DoesNotContain(datos.Documentos, d => d.Url!.Contains(' '));
        Assert.EndsWith("/docs/Programa-Impulso-2026%2015-01-26.pdf", datos.Documentos[0].Url);
        Assert.Equal("docs/Programa-Impulso-2026 15-01-26.pdf", datos.Documentos[0].Archivo);
    }

    [Fact]
    public async Task La_imagen_conserva_la_ruta_original_y_la_url_absoluta()
    {
        using var app = ConRepositorio(new RepositorioFalso());
        using var cliente = app.CreateClient();

        var datos = await cliente.GetFromJsonAsync<ProgramaImpulsoDto>(Ruta);

        Assert.Equal("Noticias/Impulso2017.jpg", datos!.Imagen);
        Assert.True(Uri.IsWellFormedUriString(datos.ImagenUrl, UriKind.Absolute));
        Assert.EndsWith("/Noticias/Impulso2017.jpg", datos.ImagenUrl);
        // El ASP tenía alt="Cto. España Empresas" (copiado de otra página).
        Assert.Contains("Impulso", datos.ImagenAlt);
    }

    [Fact]
    public async Task Pasa_al_repositorio_el_anio_y_la_hora_local_y_sin_anio_no_filtra()
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        var datos = await cliente.GetFromJsonAsync<ProgramaImpulsoDto>($"{Ruta}?anio=2023");
        Assert.Equal(2023, falso.AnioRecibido);
        Assert.Equal(Ahora, falso.AhoraRecibido);
        Assert.Equal(2023, datos!.Anio);

        using var respuesta = await cliente.GetAsync(Ruta);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(respuesta.Headers.CacheControl?.Public);
        Assert.Null(falso.AnioRecibido);
    }

    [Fact]
    public async Task El_json_usa_camelCase_y_la_forma_de_los_torneos_de_campeonatos()
    {
        var falso = new RepositorioFalso
        {
            Items =
            [
                new CampeonatoDto(0, 2026, "FIP BRONZE CASTELLON AUTOMOCION CANO KIA", "parejas", "absoluto", false, false,
                    new DateOnly(2026, 5, 25), new DateOnly(2026, 5, 31), "CASTELLÓN", ["LA PLANA SPORT CASTELLON"],
                    "https://padelcv.com/competicion/torneos/fip-bronze-castellon-automocion-cano-kia/", true, null),
            ],
        };
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        var json = await cliente.GetFromJsonAsync<JsonElement>(Ruta);

        Assert.Equal(2026, json.GetProperty("anio").GetInt32());
        Assert.Equal(2, json.GetProperty("anios").GetArrayLength());
        Assert.Equal("2026-01-15", json.GetProperty("documentos")[0].GetProperty("fecha").GetString());
        var item = json.GetProperty("items")[0];
        Assert.Equal("2026-05-25", item.GetProperty("fechaInicio").GetString());
        Assert.True(item.GetProperty("nuevaVentana").GetBoolean());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("inscripcion").ValueKind);
    }

    private WebApplicationFactory<Program> ConRepositorio(ITorneosProgramaImpulsoRepository repositorio)
        => factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddSingleton(repositorio);
            s.AddSingleton<TimeProvider>(new RelojFijo(new DateTimeOffset(Ahora, TimeSpan.Zero)));
        }));

    private sealed class RepositorioFalso : ITorneosProgramaImpulsoRepository
    {
        public int? AnioRecibido { get; private set; }
        public DateTime AhoraRecibido { get; private set; }
        public IReadOnlyList<CampeonatoDto> Items { get; init; } = [];

        public Task<TorneosProgramaImpulsoDto> BuscarAsync(int? anio, DateTime ahora, CancellationToken cancellationToken)
        {
            AnioRecibido = anio;
            AhoraRecibido = ahora;
            return Task.FromResult(new TorneosProgramaImpulsoDto(anio ?? 2026, [2026, 2023], Items));
        }
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
