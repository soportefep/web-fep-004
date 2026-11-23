using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FEP.Api.Contracts.Licencias;
using FEP.Api.Data.Repositories.Licencias;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace FEP.Api.Tests;

/// <summary>
/// GET /api/v1/licencias (migrado desde Licencias_Buscar.asp, "Busca tu licencia"). La consulta real necesita SQL
/// Server: aquí se prueban la validación (que corta antes de tocar la BD), los criterios que llegan al repositorio, que la
/// respuesta no se cachea y el límite de búsquedas por IP, con un repositorio falso.
/// </summary>
public sealed class LicenciasTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Ruta = "/api/v1/licencias";

    [Theory]
    [InlineData("tipo=X&busqueda=garcia", "tipo")]
    [InlineData("busqueda=garcia", "federacion")]                               // jugador/a sin federación (el ASP avisaba)
    [InlineData("federacion=0&busqueda=garcia", "federacion")]
    [InlineData("federacion=23&busqueda=garcia", "federacion")]                 // 22 federaciones (DECISIÓN FEP ya tomada)
    [InlineData("federacion=abc&busqueda=garcia", "federacion")]                // el ASP: error 500
    [InlineData("tipo=tecnico&federacion=1&busqueda=garcia", "federacion")]
    [InlineData("tipo=arbitro&federacion=1&busqueda=garcia", "federacion")]
    [InlineData("federacion=1", "busqueda")]                                    // ni nombre ni fecha
    [InlineData("federacion=1&busqueda=a", "busqueda")]                         // una sola letra
    [InlineData("federacion=1&busqueda=a+b+c+d+e+f+g", "busqueda")]
    [InlineData("federacion=1&fechaNacimiento=15/03/1980", "fechaNacimiento")]  // solo aaaa-mm-dd
    [InlineData("federacion=1&fechaNacimiento=1980-02-30", "fechaNacimiento")]
    [InlineData("federacion=1&fechaNacimiento=1899-12-31", "fechaNacimiento")]
    [InlineData("federacion=1&fechaNacimiento=2030-01-01", "fechaNacimiento")]  // posterior a hoy
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
    public async Task Pasa_al_repositorio_los_criterios_normalizados_y_no_se_cachea()
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync(
            $"{Ruta}?tipo=J&federacion=1&busqueda={Uri.EscapeDataString("  matador  garcía-de ")}&fechaNacimiento=1975-03-15");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(respuesta.Headers.CacheControl?.NoStore);
        Assert.NotNull(falso.Recibido);
        Assert.Equal(TipoLicencia.Jugador, falso.Recibido.Tipo);
        Assert.Equal(1, falso.Recibido.Federacion);
        Assert.Equal(new[] { "matador", "garcía", "de" }, falso.Recibido.Terminos);
        Assert.Equal(new DateOnly(1975, 3, 15), falso.Recibido.FechaNacimiento);

        // La respuesta no repite la fecha de nacimiento: solo dice que se ha usado.
        var json = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("jugador", json.GetProperty("tipo").GetString());
        Assert.True(json.GetProperty("conFechaNacimiento").GetBoolean());
        Assert.DoesNotContain("1975", json.GetRawText());
    }

    [Theory]
    [InlineData("tipo=tecnico&busqueda=garcia", TipoLicencia.Tecnico, null)]
    [InlineData("tipo=A&fechaNacimiento=1980-01-31", TipoLicencia.Arbitro, null)]
    [InlineData("tipo=silla-ruedas&busqueda=perez", TipoLicencia.SillaRuedas, null)]   // federación opcional
    [InlineData("tipo=S&federacion=14&busqueda=perez", TipoLicencia.SillaRuedas, 14)]
    public async Task Tecnicos_arbitros_y_silla_no_exigen_federacion(string query, TipoLicencia tipo, int? federacion)
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}?{query}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(tipo, falso.Recibido!.Tipo);
        Assert.Equal(federacion, falso.Recibido.Federacion);
    }

    [Fact]
    public async Task Demasiadas_busquedas_desde_la_misma_IP_devuelven_429()
    {
        using var app = ConRepositorio(new RepositorioFalso());
        using var cliente = app.CreateClient();

        HttpResponseMessage? ultima = null;
        for (var i = 0; i <= LimiteBusquedaLicencias.BusquedasPorMinuto; i++)
        {
            ultima?.Dispose();
            ultima = await cliente.GetAsync($"{Ruta}?federacion=1&busqueda=garcia");
        }

        using (ultima)
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, ultima!.StatusCode);
            var problema = await ultima.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Demasiadas búsquedas", problema.GetProperty("title").GetString());
        }
    }

    [Theory]
    [InlineData(TipoLicencia.Jugador, "12762", "12762", "Estadisticas?id=12762")]
    [InlineData(TipoLicencia.SillaRuedas, "1008", "PS1008", null)]
    [InlineData(TipoLicencia.Tecnico, "5021", "5021", null)]
    [InlineData(TipoLicencia.Jugador, "A12", "A12", null)]
    public void Licencia_visible_y_enlace_a_estadisticas_como_el_ASP(TipoLicencia tipo, string licencia, string visible, string? ruta)
    {
        Assert.Equal(visible, ReglasLicencias.LicenciaVisible(tipo, licencia));
        Assert.Equal(ruta, ReglasLicencias.RutaEstadisticas(tipo, licencia));
    }

    private WebApplicationFactory<Program> ConRepositorio(ILicenciasRepository repositorio)
        => factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddSingleton(repositorio);
            s.AddSingleton<TimeProvider>(new RelojFijo(new DateTimeOffset(2026, 11, 19, 10, 0, 0, TimeSpan.Zero)));
        }));

    private sealed class RepositorioFalso : ILicenciasRepository
    {
        public BusquedaLicencias? Recibido { get; private set; }

        public Task<LicenciasBusquedaDto> BuscarAsync(BusquedaLicencias busqueda, CancellationToken cancellationToken)
        {
            Recibido = busqueda;
            return Task.FromResult(new LicenciasBusquedaDto(
                ReglasLicencias.Clave(busqueda.Tipo),
                busqueda.Federacion,
                busqueda.Terminos.Count > 0 ? string.Join(' ', busqueda.Terminos) : null,
                busqueda.FechaNacimiento is not null,
                1,
                ReglasLicencias.ResultadosMaximos,
                [new LicenciaEncontradaDto("12762", "MATADOR", "GARCIA DE VINUESA", "ALVARO", null)]));
        }
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
