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
/// GET /api/v1/competicion/campeonatos-silla/{id} (migrado desde Camp_Silla.asp). Las consultas necesitan SQL
/// Server: aquí se prueban la validación del id (corta antes de tocar la BD), los 404 y la hora pasada al
/// repositorio con un repositorio falso, y las reglas heredadas del ASP (redirecciones, cuadros por género,
/// inscripción individual).
/// </summary>
public sealed class CampeonatosSillaTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Ruta = "/api/v1/competicion/campeonatos-silla";
    private static readonly DateTime Ahora = new(2026, 11, 16, 10, 0, 0);

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("31277%27%20OR%201%3D1")]
    public async Task Ids_no_validos_devuelven_400_con_ValidationProblemDetails(string id)
    {
        // Sin repositorio falso ni cadena de conexión: si llegara a la BD respondería 500.
        using var cliente = factory.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/{id}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(problema.GetProperty("errors").EnumerateObject(),
            p => p.Name.Equals("id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Un_torneo_que_no_existe_devuelve_404_con_el_listado_de_la_web_actual()
    {
        using var app = ConRepositorio(new RepositorioFalso());
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/99999999");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("https://www.padelfederacion.es/Campeonatos", problema.GetProperty("urlWebActual").GetString());
    }

    [Fact]
    public async Task Un_torneo_que_no_es_de_silla_devuelve_404_con_su_ficha()
    {
        using var app = ConRepositorio(new RepositorioFalso());
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/28676");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("https://www.padelfederacion.es/Camp_Equipos.asp?IdTorneo=28676", problema.GetProperty("urlWebActual").GetString());
    }

    [Fact]
    public async Task Devuelve_la_ficha_con_la_hora_local_y_cache_publica()
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/30180");
        var datos = await respuesta.Content.ReadFromJsonAsync<CampeonatoSillaDto>();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(respuesta.Headers.CacheControl?.Public);
        Assert.Equal(30180, falso.IdRecibido);
        Assert.Equal(Ahora, falso.AhoraRecibido);
        Assert.Equal(30180, datos!.Id);
        Assert.Equal("mixto", datos.Cuadros[0].Genero);
        Assert.Equal(1008, datos.CuadroHonor[0].Licencia);
    }

    [Theory]
    [InlineData(20255, "SILLA", "ABSOLUTO", "PAREJAS", "Camp_Parejas_Copa.asp?IdTorneo=20255")]
    [InlineData(500, "INCLUSIVO", "ABSOLUTO", "PAREJAS", "Camp_Parejas_Inclusivo.asp?IdTorneo=500")]
    [InlineData(501, "SILLA", "MENORES", "PAREJAS", "Prueba_CuadrosMenores.asp?Idtorneo=501")]
    [InlineData(502, "SILLA", "MENORES", "EQUIPOS", "PruebaEquipos_Cuadros.asp?Idtorneo=502")]
    [InlineData(503, "SILLA", "VETERANOS", "PAREJAS", "Camp_Parejas_Veteranos.asp?Idtorneo=503")]
    [InlineData(28927, "SILLA ", "ABSOLUTO", "EQUIPOS", "Camp_Equipos.asp?IdTorneo=28927")]
    [InlineData(504, "", "ABSOLUTO", "PAREJAS", "Camp_Parejas.asp?IdTorneo=504")]
    [InlineData(505, null, "ABSOLUTO", "EQUIPOS", "Camp_Equipos.asp?IdTorneo=505")]
    public void Las_redirecciones_del_asp_llevan_a_la_ficha_que_corresponde(int id, string? circuito, string modalidad, string tipo, string esperada)
        => Assert.Equal(esperada, ReglasCampeonatoSilla.RutaOtraFicha(id, circuito, modalidad, tipo, null, "N", "N"));

    [Theory]
    [InlineData("SILLA", "ABSOLUTO")]
    [InlineData(" silla ", "ABSOLUTO ")]
    [InlineData("SILLA", "SEL_ABS")]
    public void Los_torneos_de_silla_por_parejas_se_muestran_aqui(string circuito, string modalidad)
        => Assert.Null(ReglasCampeonatoSilla.RutaOtraFicha(31277, circuito, modalidad, "PAREJAS", null, "N", "N"));

    [Fact]
    public void El_bloque_mixto_busca_nombres_con_categoria_absoluto_y_usa_los_textos_por_defecto()
    {
        var fila = Fila(("Cat2", "8"), ("VerPrimera", "S"), ("VerSegunda", "S "), ("VerSegundaC", "S"), ("VerPrePrevia", "S"), ("VerQuinta", "N"));
        var pedidas = new List<ClaveNombreCuadro>();

        var cuadros = ReglasCampeonatoSilla.Cuadros("X", "ABSOLUTO", fila,
            k => { pedidas.Add(k); return k.Nivel == 2 && k.Cuadro == "Final" ? "9º al 12º Puesto" : null; },
            (nivel, cuadro, cat) => $"Previo.asp?Nivel={nivel}&Cat={cat}&Cuadro={cuadro}");

        // Primera: "Cuadro final" aunque haya segunda categoría (el ASP sobrescribía el texto con la búsqueda).
        Assert.Equal(["Cuadro final", "9º al 12º Puesto", "Puestos 7.º y 8.º", "Cuadro de pre-previa"], cuadros.Select(c => c.Titulo));
        Assert.Equal(["final", "final", "consolacion", "previa"], cuadros.Select(c => c.Tipo));
        Assert.Equal("Previo.asp?Nivel=2&Cat=ABSOLUTO&Cuadro=Consola", cuadros[2].Url);
        // La pre-previa no buscaba nombre (el ASP reutilizaba el del cuadro anterior).
        Assert.Equal(
        [
            new ClaveNombreCuadro(1, "ABSOLUTO", "X", "Final"),
            new ClaveNombreCuadro(2, "ABSOLUTO", "X", "Final"),
            new ClaveNombreCuadro(2, "ABSOLUTO", "X", "Consola"),
        ], pedidas);
    }

    [Fact]
    public void Masculino_y_femenino_no_buscan_nombres_y_la_consolacion_lleva_la_categoria_del_desglose()
    {
        var pedidas = 0;
        var masculino = ReglasCampeonatoSilla.Cuadros("M", "ABSOLUTO B", Fila(("Cat2", "0"), ("VerPrimera", "S"), ("VerPrimeraC", "S"), ("VerDecima", "S")),
            _ => { pedidas++; return "x"; }, (nivel, cuadro, cat) => $"{nivel}|{cuadro}|{cat}");
        var femenino = ReglasCampeonatoSilla.Cuadros("F", "ABSOLUTO", Fila(("Cat2", "2"), ("VerPrimera", "S"), ("VerGrupoB", "S"), ("VerSexta", "S")),
            _ => { pedidas++; return "x"; }, (nivel, cuadro, cat) => $"{nivel}|{cuadro}|{cat}");

        Assert.Equal(0, pedidas);
        Assert.Equal(["Cuadro final", "Cuadro de consolación", "Décima categoría"], masculino.Select(c => c.Titulo));
        Assert.Equal(["1|Final|ABSOLUTO", "1|Consola|ABSOLUTO B", "10|Final|ABSOLUTO"], masculino.Select(c => c.Url));
        Assert.Equal(["Primera categoría", "Grupo B", "Sexta categoría"], femenino.Select(c => c.Titulo));
        Assert.Equal("grupo", femenino[1].Tipo);
    }

    [Fact]
    public void Las_columnas_de_visibilidad_no_se_repiten_y_son_columnas_ver()
    {
        Assert.Equal(ReglasCampeonatoSilla.ColumnasVisibilidad.Count,
            ReglasCampeonatoSilla.ColumnasVisibilidad.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains("VerDecima", ReglasCampeonatoSilla.ColumnasVisibilidad);
        Assert.Contains("VerGrupoB", ReglasCampeonatoSilla.ColumnasVisibilidad);
        Assert.Contains("VerTerceraC", ReglasCampeonatoSilla.ColumnasVisibilidad);
        Assert.All(ReglasCampeonatoSilla.ColumnasVisibilidad, c => Assert.Matches("^Ver[A-Za-z0-9]+$", c));
    }

    [Theory]
    [InlineData("S", "ABSOLUTO", true)]
    [InlineData("S ", "ABSOLUTO   ", true)]
    [InlineData("N", "ABSOLUTO", false)]
    [InlineData("S", "SEL_ABS", false)]
    [InlineData(null, "ABSOLUTO", false)]
    public void Los_cuadros_solo_se_publican_en_absoluto_con_VerCuadros(string? verCuadros, string modalidad, bool publicados)
        => Assert.Equal(publicados, ReglasCampeonatoSilla.CuadrosPublicados(verCuadros, modalidad));

    [Theory]
    [InlineData("N", false)]
    [InlineData("n ", false)]
    [InlineData("S", true)]
    [InlineData(null, true)]
    public void Solo_InscripcionSolo_N_pide_pareja(string? valor, bool individual)
        => Assert.Equal(individual, ReglasCampeonatoSilla.InscripcionIndividual(valor));

    private static IReadOnlyDictionary<string, object?> Fila(params (string Columna, object? Valor)[] valores)
        => valores.ToDictionary(v => v.Columna, v => v.Valor, StringComparer.OrdinalIgnoreCase);

    private WebApplicationFactory<Program> ConRepositorio(ICampeonatoSillaRepository repositorio)
        => factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddSingleton(repositorio);
            s.AddSingleton<TimeProvider>(new RelojFijo(new DateTimeOffset(Ahora, TimeSpan.Zero)));
        }));

    /// <summary>Conoce el 30180 (silla de ruedas por parejas) y el 28676 (por equipos).</summary>
    private sealed class RepositorioFalso : ICampeonatoSillaRepository
    {
        public int? IdRecibido { get; private set; }
        public DateTime? AhoraRecibido { get; private set; }

        public Task<ConsultaCampeonatoSilla> ObtenerAsync(int id, DateTime ahora, CancellationToken cancellationToken)
        {
            (IdRecibido, AhoraRecibido) = (id, ahora);
            return Task.FromResult(id switch
            {
                30180 => new ConsultaCampeonatoSilla(Ficha(id, ahora), null),
                28676 => new ConsultaCampeonatoSilla(null, "https://www.padelfederacion.es/Camp_Equipos.asp?IdTorneo=28676"),
                _ => new ConsultaCampeonatoSilla(null, null),
            });
        }

        private static CampeonatoSillaDto Ficha(int id, DateTime ahora) => new(
            id, 2026, "II OPEN NACIONAL PSR CIUDAD DE MÁLAGA", "absoluto", false,
            new DateOnly(2026, 11, 6), new DateOnly(2026, 11, 8), "Málaga",
            [new SedeCampeonatoDto("POLIDEPORTIVO MUNICIPAL CIUDAD JARDÍN", null, null, null)], [0m],
            [new GeneroCampeonatoDto("mixto", [])],
            null, [], true,
            [new GrupoCuadrosDto("mixto", "ABSOLUTO", [new CuadroEquiposDto("Cuadro final", 1, "Final", "final", "u")])],
            [new PuestoHonorDto("Campeón", 1008, "FERNANDO PEREZ SOUTO", 150m, "https://www.padelfederacion.es/Silla-Ruedas-Est?id=PS1008")],
            null, [], [new InscritosGeneroDto("mixto", 15, "u")], null,
            new InscripcionSillaDto(false, null, false, null, null, "https://www.padelfederacion.es/Camp_Silla.asp?IdTorneo=" + id),
            null, null, null, [], "https://www.padelfederacion.es/Camp_Silla.asp?IdTorneo=" + id, ahora);
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
