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
/// GET /api/v1/competicion/organizacion-torneos y /clubes (migrados desde Organizacion_Torneos.asp,
/// Organizacion_FueraPlazo.asp, cargarfechastorneo.asp, comboProvincias.asp y comboclubesLicitacion.asp).
/// Las pruebas, comunidades, ficheros y clubes necesitan SQL Server, así que se sustituyen por un repositorio falso;
/// temporada, plazo y textos salen de src/FEP.Api/Contenidos/organizacion-torneos.json (sin BD). El reloj se fija para
/// probar el cierre del plazo en hora de Madrid. La validación corta antes de tocar la BD.
/// </summary>
public sealed class OrganizacionTorneosTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Ruta = "/api/v1/competicion/organizacion-torneos";

    // 16/11/2026 10:00 UTC: plazo abierto (cierra el 20/11/2026 a las 14:00, hora de Madrid).
    private static readonly DateTimeOffset Abierto = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Cerrado = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("")]
    [InlineData("?provincia=0")]
    [InlineData("?provincia=1000")]
    [InlineData("?provincia=abc")]
    [InlineData("?provincia=28%20OR%201%3D1")]
    public async Task Clubes_sin_provincia_valida_devuelve_400_con_ValidationProblemDetails(string query)
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso, Abierto);
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/clubes{query}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problema.GetProperty("status").GetInt32());
        Assert.Contains(problema.GetProperty("errors").EnumerateObject(),
            p => p.Name.Equals("provincia", StringComparison.OrdinalIgnoreCase));
        Assert.Null(falso.ProvinciaRecibida);
    }

    [Fact]
    public async Task Clubes_pasa_la_provincia_al_repositorio_y_devuelve_los_ids_como_texto()
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso, Abierto);
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/clubes?provincia=33");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(respuesta.Headers.CacheControl?.Public);
        Assert.Equal(33, falso.ProvinciaRecibida);

        var json = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var club = json[0];
        Assert.Equal("0203371", club.GetProperty("id").GetString());
        Assert.Equal("CLUB ALTAMIRA PADEL & FITNESS", club.GetProperty("nombre").GetString());
        Assert.Equal(33, club.GetProperty("provinciaId").GetInt32());
    }

    [Fact]
    public async Task Con_el_plazo_abierto_devuelve_pruebas_comunidades_documentos_y_envio()
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso, Abierto);
        using var cliente = app.CreateClient();

        var datos = await cliente.GetFromJsonAsync<OrganizacionTorneosDto>(Ruta);

        Assert.NotNull(datos);
        Assert.Equal(2027, falso.TemporadaRecibida);
        Assert.Equal(2027, datos.Temporada);
        Assert.True(datos.Abierta);
        Assert.Equal(new DateTime(2026, 11, 20, 14, 0, 0), datos.FechaLimite);
        Assert.False(string.IsNullOrWhiteSpace(datos.Titulo));
        Assert.Equal(new[] { "menores", "absolutos", "veteranos", "silla" }, datos.Grupos.Select(g => g.Clave));
        Assert.Equal(2, datos.Torneos.Count);
        Assert.Single(datos.Comunidades);
        Assert.NotEmpty(datos.ProteccionDatos);
        Assert.Equal(new[] { 1, 2, 3, 4 }, datos.Pasos.Select(p => p.Orden));

        // Documentos: solo los que tienen fichero en la BD, con la URL absoluta y codificada.
        var documento = Assert.Single(datos.Documentos);
        Assert.Equal("condiciones", documento.Clave);
        Assert.Equal("docs/Bases de Licitacion 2027.pdf", documento.Archivo);
        Assert.Equal("https://www.padelfederacion.es/refs/docs/Bases%20de%20Licitacion%202027.pdf", documento.Url);

        // La solicitud sigue en la web actual.
        Assert.Equal("https://www.padelfederacion.es/Organizacion_P0.asp", datos.EnvioUrl);
    }

    [Fact]
    public async Task Con_el_plazo_cerrado_no_hay_pruebas_ni_comunidades_pero_si_documentos()
    {
        using var app = ConRepositorio(new RepositorioFalso(), Cerrado);
        using var cliente = app.CreateClient();

        var datos = await cliente.GetFromJsonAsync<OrganizacionTorneosDto>(Ruta);

        Assert.NotNull(datos);
        Assert.False(datos.Abierta);
        Assert.Empty(datos.Torneos);
        Assert.Empty(datos.Comunidades);
        Assert.Single(datos.Documentos);
        Assert.False(string.IsNullOrWhiteSpace(datos.AvisoCerrado));
    }

    [Fact]
    public async Task El_json_usa_camelCase_fechas_sin_hora_y_fianza_null_a_negociar()
    {
        using var app = ConRepositorio(new RepositorioFalso(), Abierto);
        using var cliente = app.CreateClient();

        var json = await cliente.GetFromJsonAsync<JsonElement>(Ruta);

        Assert.Equal("2026-11-20T14:00:00", json.GetProperty("fechaLimite").GetString());
        var torneo = json.GetProperty("torneos")[0];
        Assert.Equal(5663, torneo.GetProperty("id").GetInt32());
        Assert.Equal("menores", torneo.GetProperty("grupo").GetString());
        Assert.Equal("2027-02-05", torneo.GetProperty("fechaInicio").GetString());
        Assert.Equal(JsonValueKind.Null, torneo.GetProperty("fianza").ValueKind);
        Assert.Equal(1500m, json.GetProperty("torneos")[1].GetProperty("fianza").GetDecimal());
        var provincia = json.GetProperty("comunidades")[0].GetProperty("provincias")[0];
        Assert.Equal(33, provincia.GetProperty("id").GetInt32());
    }

    // --- Reglas heredadas -------------------------------------------------------------------------

    // El ASP comparaba CDate(now()) > CDate("20/11/26 14:00:00") con la hora del servidor; aquí, hora de Madrid.
    [Theory]
    [InlineData("2026-11-20T12:59:59Z", true)]   // 13:59:59 en Madrid (UTC+1 en noviembre)
    [InlineData("2026-11-20T13:00:00Z", false)]  // 14:00 en Madrid: cerrada
    [InlineData("2026-11-16T10:00:00Z", true)]
    [InlineData("2026-12-01T00:00:00Z", false)]
    public void El_plazo_cierra_a_la_hora_de_Madrid(string utc, bool abierta)
    {
        var reloj = new RelojFijo(DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(abierta, ReglasOrganizacionTorneos.Abierta(new DateTime(2026, 11, 20, 14, 0, 0), reloj));
    }

    [Fact]
    public void Sin_fecha_limite_la_convocatoria_esta_abierta()
        => Assert.True(ReglasOrganizacionTorneos.Abierta(null, new RelojFijo(Cerrado)));

    // cargarfechastorneo.asp: 0 → "A Negociar directamente con la FEP"; vacía → " euros" (sin cifra).
    [Theory]
    [InlineData(null, null)]
    [InlineData(0.0, null)]
    [InlineData(-1.0, null)]
    [InlineData(1500.0, 1500.0)]
    public void Fianza_cero_o_vacia_es_a_negociar(double? fianza, double? esperado)
        => Assert.Equal((decimal?)esperado, ReglasOrganizacionTorneos.Fianza((decimal?)fianza));

    [Theory]
    [InlineData(3, "menores")]
    [InlineData(4, "menores")]
    [InlineData(1, "absolutos")]
    [InlineData(2, "absolutos")]
    [InlineData(5, "absolutos")]
    [InlineData(6, "veteranos")]
    [InlineData(7, "veteranos")]
    [InlineData(10, "silla")]
    [InlineData(8, null)]
    [InlineData(null, null)]
    public void Grupo_del_desplegable_segun_la_categoria(int? categoria, string? esperado)
        => Assert.Equal(esperado, ReglasOrganizacionTorneos.GrupoDe(categoria));

    [Theory]
    [InlineData(" Circuito Nacional de Menores 1– Acceso", "Circuito Nacional de Menores 1– Acceso")]
    [InlineData("*Circuito Nacional de Menores 7\t— acceso", "*Circuito Nacional de Menores 7 — acceso")]
    [InlineData("Master final  de pádel en silla", "Master final de pádel en silla")]
    [InlineData(null, "")]
    public void Titulo_sin_espacios_sobrantes(string? campeonato, string esperado)
        => Assert.Equal(esperado, ReglasOrganizacionTorneos.Titulo(campeonato));

    private WebApplicationFactory<Program> ConRepositorio(IOrganizacionTorneosRepository repositorio, DateTimeOffset ahora)
        => factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddSingleton(repositorio);
            s.AddSingleton<TimeProvider>(new RelojFijo(ahora));
        }));

    private sealed class RepositorioFalso : IOrganizacionTorneosRepository
    {
        public int? TemporadaRecibida { get; private set; }
        public int? ProvinciaRecibida { get; private set; }

        public Task<DatosOrganizacionTorneosDto> ObtenerAsync(int temporada, CancellationToken cancellationToken)
        {
            TemporadaRecibida = temporada;
            TorneoLicitableDto[] torneos =
            [
                new(5663, "Circuito Nacional de Menores 1– Acceso mediante clasificatorio territorial + ranking nacional",
                    "menores", new DateOnly(2027, 2, 5), new DateOnly(2027, 2, 7), null),
                new(5665, "XLIII Cto. España Equipos 1ª", "absolutos", new DateOnly(2027, 2, 12), new DateOnly(2027, 2, 14), 1500m),
            ];
            ComunidadSolicitudDto[] comunidades = [new(3, "ASTURIAS", [new ProvinciaSolicitudDto(33, "ASTURIAS")])];
            var archivos = new Dictionary<string, string> { ["condiciones"] = "Bases de Licitacion 2027.pdf" };
            return Task.FromResult(new DatosOrganizacionTorneosDto(torneos, comunidades, archivos));
        }

        public Task<IReadOnlyList<ClubSedeDto>> ListarClubesAsync(int provincia, CancellationToken cancellationToken)
        {
            ProvinciaRecibida = provincia;
            return Task.FromResult<IReadOnlyList<ClubSedeDto>>([new ClubSedeDto("0203371", "CLUB ALTAMIRA PADEL & FITNESS", provincia)]);
        }
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
