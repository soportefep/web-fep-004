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
/// GET /api/v1/competicion/calendario (migrado desde CalendarioFEP.asp). Las pruebas y temporadas necesitan
/// SQL Server, así que se sustituyen por un repositorio falso; el aviso de la convocatoria sale de
/// src/FEP.Api/Contenidos/calendario-fep.json (sin BD). La validación corta antes de tocar la BD. También se
/// prueban las reglas heredadas del ASP (temporada por defecto, fecha comodín y cuándo se enlaza la prueba).
/// </summary>
public sealed class CalendarioTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Ruta = "/api/v1/competicion/calendario";
    private static readonly DateTime Ahora = new(2026, 11, 13, 10, 0, 0);

    [Theory]
    [InlineData("anio=1900")]
    [InlineData("anio=2101")]
    [InlineData("anio=abc")]
    [InlineData("anio=2026%27%20and%20SFEP_Calendario.Federacion%3D0%20--")]
    public async Task Anio_no_valido_devuelve_400_con_ValidationProblemDetails(string query)
    {
        using var cliente = factory.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problema.GetProperty("status").GetInt32());
        Assert.Contains(problema.GetProperty("errors").EnumerateObject(),
            p => p.Name.Equals("anio", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Pasa_al_repositorio_el_anio_y_el_anio_en_curso_y_sin_anio_no_lo_fija()
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        var datos = await cliente.GetFromJsonAsync<CalendarioCompeticionDto>($"{Ruta}?anio=2025");
        Assert.Equal(2025, falso.AnioRecibido);
        Assert.Equal(2026, falso.AnioActualRecibido);
        Assert.Equal(2025, datos!.Anio);

        using var respuesta = await cliente.GetAsync(Ruta);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(respuesta.Headers.CacheControl?.Public);
        Assert.Null(falso.AnioRecibido);
    }

    [Fact]
    public async Task Con_la_convocatoria_abierta_devuelve_el_aviso_con_urls_absolutas()
    {
        using var app = ConRepositorio(new RepositorioFalso { LicitacionAbierta = true });
        using var cliente = app.CreateClient();

        var datos = await cliente.GetFromJsonAsync<CalendarioCompeticionDto>(Ruta);

        var licitacion = datos!.Licitacion;
        Assert.NotNull(licitacion);
        Assert.False(string.IsNullOrWhiteSpace(licitacion.Titulo));
        Assert.Equal("images/Torneos2027.jpg", licitacion.Imagen);
        Assert.EndsWith("/images/Torneos2027.jpg", licitacion.ImagenUrl);
        Assert.False(string.IsNullOrWhiteSpace(licitacion.ImagenAlt));
        Assert.Equal("Organizacion_Torneos", licitacion.Enlace);
        Assert.True(Uri.IsWellFormedUriString(licitacion.EnlaceUrl, UriKind.Absolute));
        Assert.EndsWith("/Organizacion_Torneos", licitacion.EnlaceUrl);
    }

    [Fact]
    public async Task Con_la_convocatoria_cerrada_no_hay_aviso()
    {
        using var app = ConRepositorio(new RepositorioFalso { LicitacionAbierta = false });
        using var cliente = app.CreateClient();

        var json = await cliente.GetFromJsonAsync<JsonElement>(Ruta);

        Assert.Equal(JsonValueKind.Null, json.GetProperty("licitacion").ValueKind);
    }

    [Fact]
    public async Task El_json_usa_camelCase_y_fechas_sin_hora()
    {
        var falso = new RepositorioFalso
        {
            Items =
            [
                new PruebaCalendarioCompeticionDto(2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), true,
                    "Cto. España Sub-23", "Sub 23", "En Licitación", null, null, false),
                new PruebaCalendarioCompeticionDto(2026, new DateOnly(2026, 5, 29), new DateOnly(2026, 5, 31), false,
                    "CIRCUITO NACIONAL DE VETERANOS 1", "Veteranos", "C.D. PADELMERIDA (BADAJOZ)", 28544,
                    "https://www.padelfederacion.es/Camp_Parejas.asp?IdTorneo=28544", false),
            ],
        };
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        var json = await cliente.GetFromJsonAsync<JsonElement>(Ruta);

        Assert.Equal(2026, json.GetProperty("anio").GetInt32());
        var temporada = json.GetProperty("temporadas")[0];
        Assert.Equal("2026-10-13", temporada.GetProperty("actualizado").GetString());
        Assert.Equal("https://www.padelfederacion.es/refs/docs/CALENDARIO%20NACIONAL%202026%20A%2013.10.26.pdf",
            temporada.GetProperty("url").GetString());
        var comodin = json.GetProperty("items")[0];
        Assert.True(comodin.GetProperty("fechaPorConfirmar").GetBoolean());
        Assert.Equal(JsonValueKind.Null, comodin.GetProperty("idTorneo").ValueKind);
        var enlazada = json.GetProperty("items")[1];
        Assert.Equal("2026-05-29", enlazada.GetProperty("fechaInicio").GetString());
        Assert.Equal(28544, enlazada.GetProperty("idTorneo").GetInt32());
        Assert.False(enlazada.GetProperty("nuevaVentana").GetBoolean());
    }

    // --- Reglas heredadas -------------------------------------------------------------------------

    [Theory]
    [InlineData(new[] { 2027, 2026, 2025 }, 2026, 2026)] // el ASP tenía ano = 2026 escrito a mano
    [InlineData(new[] { 2026, 2025 }, 2027, 2026)]       // año en curso aún sin publicar: la más reciente anterior
    [InlineData(new[] { 2027, 2028 }, 2026, 2027)]       // solo temporadas futuras: la más próxima
    [InlineData(new[] { 2025, 2027 }, 2026, 2025)]
    public void Temporada_por_defecto(int[] anios, int anioActual, int esperado)
        => Assert.Equal(esperado, ReglasCalendario.TemporadaPorDefecto(anios, anioActual));

    [Fact]
    public void Sin_temporadas_no_hay_temporada_por_defecto()
        => Assert.Null(ReglasCalendario.TemporadaPorDefecto([], 2026));

    // DECISIÓN FEP (13/11/2026): la temporada cambia sola el 1 de enero, en hora de Madrid (no la del servidor).
    [Theory]
    [InlineData("2026-12-31T22:59:59Z", 2026)] // 23:59:59 del 31 dic en Madrid (invierno, UTC+1)
    [InlineData("2026-12-31T23:00:00Z", 2027)] // 00:00 del 1 ene en Madrid: aún 31 dic en UTC
    [InlineData("2027-06-15T10:00:00Z", 2027)]
    public void El_anio_en_curso_cambia_el_1_de_enero_en_hora_de_Madrid(string utc, int esperado)
    {
        var reloj = new RelojFijo(DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(esperado, ReglasCalendario.AnioEnCurso(reloj));
        Assert.Equal(esperado, ReglasCalendario.TemporadaPorDefecto([2027, 2026, 2025], ReglasCalendario.AnioEnCurso(reloj)));
    }

    // DECISIÓN FEP (13/11/2026): sedes en formato "Nombre Propio".
    [Theory]
    [InlineData("REGAL PADEL CLUB (ZARAGOZA)", "Regal Padel Club (Zaragoza)")]
    [InlineData("Club de Pádel Arruzafa - Córdoba", "Club de Pádel Arruzafa - Córdoba")] // ya escrito a mano: se respeta
    [InlineData("CLUB DE PADEL L´ALFAS DEL PI", "Club de Padel L´Alfas del Pi")]
    [InlineData("REIAL CLUB DE TENNIS BARCELONA 1899", "Reial Club de Tennis Barcelona 1899")]
    [InlineData("CDP LA MOTA Y EL PINAR", "CDP La Mota y El Pinar")]  // artículos de topónimo: mayúscula
    [InlineData("PADELPOINT LA NUCIA (ALICANTE)", "Padelpoint La Nucia (Alicante)")]
    [InlineData("ALHAURIN DE LA TORRE", "Alhaurin de la Torre")]       // artículo tras preposición: minúscula
    [InlineData("LA RIOJA", "La Rioja")]                       // enlace al principio: mayúscula
    [InlineData("SEDE (LA NUCIA)", "Sede (La Nucia)")]       // tras paréntesis: mayúscula
    [InlineData("XXIII CTO. ESPAÑA - FEP", "XXIII Cto. España - FEP")]
    [InlineData("SIN DESIGNAR", "Sin Designar")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Sede_en_formato_nombre_propio(string? sede, string? esperado)
        => Assert.Equal(esperado, FEP.Api.Infrastructure.Texto.NombrePropio(sede));

    [Theory]
    [InlineData("2026-01-01", "2026-12-31", true)]   // fecha comodín: "1 Ene – 31 Dic"
    [InlineData(null, null, true)]                   // "SIN FECHA ASIGNADA"
    [InlineData(null, "2026-05-03", true)]
    [InlineData("2026-01-01", "2026-01-03", false)]
    [InlineData("2026-12-11", "2026-12-13", false)]
    [InlineData("2026-01-01", "2027-12-31", false)]
    [InlineData("2026-02-27", "2026-03-01", false)]
    public void Fecha_por_confirmar(string? inicio, string? fin, bool esperado)
        => Assert.Equal(esperado, ReglasCalendario.FechaPorConfirmar(
            inicio is null ? null : DateTime.Parse(inicio), fin is null ? null : DateTime.Parse(fin)));

    [Theory]
    [InlineData(28544, null, true)]
    [InlineData(28544, "", true)]
    [InlineData(28544, "Camp_Parejas.asp", true)]
    [InlineData(28544, "#", false)]
    [InlineData(28544, " # ", false)]
    [InlineData(0, null, false)]
    [InlineData(null, null, false)]
    public void Solo_se_enlazan_las_pruebas_con_torneo_y_sin_Link_almohadilla(int? idPrueba, string? link, bool esperado)
        => Assert.Equal(esperado, ReglasCalendario.Enlazable(idPrueba, link));

    private WebApplicationFactory<Program> ConRepositorio(ICalendarioCompeticionRepository repositorio)
        => factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddSingleton(repositorio);
            s.AddSingleton<TimeProvider>(new RelojFijo(new DateTimeOffset(Ahora, TimeSpan.Zero)));
        }));

    private sealed class RepositorioFalso : ICalendarioCompeticionRepository
    {
        public int? AnioRecibido { get; private set; }
        public int AnioActualRecibido { get; private set; }
        public bool LicitacionAbierta { get; init; } = true;
        public IReadOnlyList<PruebaCalendarioCompeticionDto> Items { get; init; } = [];

        public Task<DatosCalendarioCompeticionDto> BuscarAsync(int? anio, int anioActual, CancellationToken cancellationToken)
        {
            AnioRecibido = anio;
            AnioActualRecibido = anioActual;
            TemporadaCalendarioDto[] temporadas =
            [
                new(2026, new DateOnly(2026, 10, 13), "docs/CALENDARIO NACIONAL 2026 A 13.10.26.pdf",
                    "https://www.padelfederacion.es/refs/docs/CALENDARIO%20NACIONAL%202026%20A%2013.10.26.pdf"),
                new(2025, null, null, null),
            ];
            return Task.FromResult(new DatosCalendarioCompeticionDto(anio ?? 2026, temporadas, LicitacionAbierta, Items));
        }

        // Calendarios territoriales (CalendarioFT.asp): se prueban en CalendarioTerritorialTests.
        public Task<IReadOnlyList<DatosFederacionCalendarioDto>> ListarFederacionesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DatosFederacionCalendarioDto>>([]);

        public Task<DatosCalendarioTerritorialDto?> BuscarFederacionAsync(int federacion, int? anio, int anioActual, CancellationToken cancellationToken)
            => Task.FromResult<DatosCalendarioTerritorialDto?>(null);
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
