using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FEP.Api.Contracts.Federacion;
using FEP.Api.Data.Repositories.Federacion;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace FEP.Api.Tests;

/// <summary>
/// GET /api/v1/federacion/federaciones-autonomicas/{id} (migrado desde Datos_Federacion.asp). Las consultas
/// reales necesitan SQL Server: aquí se prueban la validación del id (corta antes de tocar la BD), el 404 y
/// el año de las licencias con un repositorio falso, y las funciones de normalización del repositorio.
/// </summary>
public sealed class FichaFederacionTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Ruta = "/api/v1/federacion/federaciones-autonomicas";

    [Theory]
    [InlineData("-1")]
    [InlineData("23")]     // DECISIÓN FEP: 0 = FEP y 22 federaciones autonómicas (1–22)
    [InlineData("99")]
    [InlineData("100")]
    [InlineData("abc")]
    [InlineData("6%27%20OR%201%3D1")]
    public async Task Ids_no_validos_devuelven_400_con_ValidationProblemDetails(string id)
    {
        // Sin repositorio falso ni cadena de conexión: si llegara a la BD respondería 500.
        using var cliente = factory.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/{id}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problema.GetProperty("status").GetInt32());
        Assert.Contains(problema.GetProperty("errors").EnumerateObject(),
            p => p.Name.Equals("id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Una_federacion_que_no_existe_devuelve_404_ProblemDetails()
    {
        using var app = ConRepositorio(new RepositorioFalso());
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/20");   // dentro de 0–22, pero no existe

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(404, problema.GetProperty("status").GetInt32());
        Assert.Equal("https://www.padelfederacion.es/Federaciones", problema.GetProperty("urlWebActual").GetString());
    }

    [Theory]
    [InlineData(6)]
    [InlineData(0)]
    public async Task Devuelve_la_ficha_con_el_anio_en_curso_y_cache_publica(int id)
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/{id}");
        var datos = await respuesta.Content.ReadFromJsonAsync<FichaFederacionDto>();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(respuesta.Headers.CacheControl?.Public);
        Assert.Equal(id, falso.IdRecibido);
        Assert.Equal(2026, falso.AhoraRecibido?.Year);
        Assert.Equal(id, datos!.Id);
        Assert.Equal(id == 0, datos.Nacional);
        Assert.Equal(2026, datos.Licencias.Anio);
    }

    [Fact]
    public async Task El_listado_de_federaciones_sigue_respondiendo_en_la_ruta_base()
    {
        // El listado (Federaciones.asp) y la ficha comparten prefijo de ruta en controladores distintos.
        using var cliente = factory.CreateClient();

        var datos = await cliente.GetFromJsonAsync<FederacionesAutonomicasDto>(Ruta);

        Assert.NotNull(datos);
        Assert.Contains(datos.Federaciones, f => f.Id == 6);
    }

    [Fact]
    public void La_evolucion_va_de_2012_al_anio_en_curso_con_huecos_a_null()
    {
        var serie = FichaFederacionRepository.Evolucion(
            [(2012, 6053), (2013, 6853), (2015, null), (2015, 9246), (2026, 1)], 2026, 8863);

        Assert.Equal(15, serie.Count);
        Assert.Equal(Enumerable.Range(2012, 15), serie.Select(e => e.Anio));
        Assert.Equal(6053, serie[0].Licencias);
        Assert.Null(serie[2].Licencias);                 // 2014: no está en el histórico
        Assert.Equal(9246, serie[3].Licencias);          // 2015 repetido: gana la última fila
        Assert.Equal(8863, serie[^1].Licencias);         // año en curso: cifra en directo, no el histórico
        Assert.True(serie[^1].EnCurso);
        Assert.All(serie.SkipLast(1), e => Assert.False(e.EnCurso));
    }

    [Theory]
    [InlineData("981 231 537     ", "981231537", new[] { "981 231 537" })]
    [InlineData("954270884       ", "                ", new[] { "954270884" })]
    [InlineData("654216003  ", "603811363", new[] { "654216003", "603811363" })]
    [InlineData(null, "-", new string[0])]
    public void Los_telefonos_se_limpian_y_no_se_repiten(string? t1, string? t2, string[] esperados)
        => Assert.Equal(esperados, FichaFederacionRepository.Telefonos(t1, t2));

    [Theory]
    [InlineData("FEDERACION DE PADEL  CASTILLA-LA MANCHA ", "FEDERACION DE PADEL CASTILLA-LA MANCHA")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Los_espacios_repetidos_se_colapsan(string? valor, string? esperado)
        => Assert.Equal(esperado, FichaFederacionRepository.Espacios(valor));

    private WebApplicationFactory<Program> ConRepositorio(IFichaFederacionRepository repositorio)
        => factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddSingleton(repositorio);
            s.AddSingleton<TimeProvider>(new RelojFijo(new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero)));
        }));

    /// <summary>Solo conoce la FEP (0) y Andalucía (6).</summary>
    private sealed class RepositorioFalso : IFichaFederacionRepository
    {
        public int? IdRecibido { get; private set; }
        public DateTime? AhoraRecibido { get; private set; }

        public Task<FichaFederacionDto?> ObtenerAsync(int id, DateTime ahora, CancellationToken cancellationToken)
        {
            (IdRecibido, AhoraRecibido) = (id, ahora);
            if (id is not (0 or 6))
            {
                return Task.FromResult<FichaFederacionDto?>(null);
            }

            var licencias = new LicenciasFederacionDto(ahora.Year, 10, id == 0 ? null : 4, 18, 1, 1, 2,
                FichaFederacionRepository.Evolucion([], ahora.Year, 10),
                new GeneroLicenciasDto(6, 4, 0), new EdadesLicenciasDto(1, 2, 3, 4, 0));
            return Task.FromResult<FichaFederacionDto?>(new FichaFederacionDto(
                id, id == 0 ? "FEDERACION ESPAÑOLA DE PADEL" : "FEDERACION ANDALUZA DE PADEL", id == 0,
                $"images/{id}.jpg", null, null, null, null, null, [], null, null, licencias, ahora));
        }
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
