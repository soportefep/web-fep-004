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
/// GET /api/v1/competicion/campeonatos-equipos/{id} (migrado desde Camp_Equipos.asp). Las consultas necesitan
/// SQL Server: aquí se prueban la validación del id (corta antes de tocar la BD), los 404 y la hora pasada al
/// repositorio con un repositorio falso, el contenido escrito a mano (Contenidos/campeonatos-equipos.json) y las
/// reglas heredadas del ASP (cuadros, pasos de la inscripción, normalizaciones).
/// </summary>
public sealed class CampeonatosEquiposTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Ruta = "/api/v1/competicion/campeonatos-equipos";
    private static readonly DateTime Ahora = new(2026, 11, 13, 10, 0, 0);

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("28676%27%20OR%201%3D1")]
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
    public async Task Un_campeonato_que_no_existe_devuelve_404_con_el_listado_de_la_web_actual()
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
    public async Task Un_torneo_por_parejas_devuelve_404_con_su_ficha_en_la_web_actual()
    {
        using var app = ConRepositorio(new RepositorioFalso());
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/31277");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("https://www.padelfederacion.es/Camp_Parejas.asp?IdTorneo=31277", problema.GetProperty("urlWebActual").GetString());
    }

    [Fact]
    public async Task Devuelve_la_ficha_con_la_hora_local_y_cache_publica()
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/28676");
        var datos = await respuesta.Content.ReadFromJsonAsync<CampeonatoEquiposDto>();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(respuesta.Headers.CacheControl?.Public);
        Assert.Equal(28676, falso.IdRecibido);
        Assert.Equal(Ahora, falso.AhoraRecibido);
        Assert.Equal(28676, datos!.Id);
        Assert.Equal("masculino", datos.Cuadros[0].Genero);
    }

    [Fact]
    public async Task El_contenido_escrito_a_mano_trae_los_tres_videos_del_28133_y_las_excepciones()
    {
        var repositorio = factory.Services.GetRequiredService<IContenidoCampeonatosEquiposRepository>();

        var contenido = await repositorio.ObtenerAsync(CancellationToken.None);

        var videos = contenido.Videos.Where(v => v.Torneo == 28133).ToArray();
        Assert.Equal(3, videos.Length);
        Assert.All(videos, v => Assert.StartsWith("https://www.youtube.com/embed/", v.Url));
        // Sin el parámetro de seguimiento ?si= que traían los iframes del ASP.
        Assert.All(videos, v => Assert.DoesNotContain("?", v.Url));
        Assert.Equal([25071, 28676, 28677, 28854], contenido.SinListadoClubes);
        Assert.Equal([23069], contenido.SinPagoEquipos);
        Assert.Contains(contenido.NombresCuadros, n => n is { Torneo: 10666, Genero: "F", Nivel: 1, Cuadro: "Consola" });
    }

    [Theory]
    [InlineData("https://www.youtube.com/embed/LC3hm8qtWRQ", true)]
    [InlineData("https://www.youtube.com/watch?v=LC3hm8qtWRQ", false)]
    [InlineData("http://www.youtube.com/embed/LC3hm8qtWRQ", false)]
    [InlineData("https://evil.example/embed/x", false)]
    [InlineData("javascript:alert(1)", false)]
    public void Solo_se_admiten_videos_insertables_de_youtube(string url, bool valido)
        => Assert.Equal(valido, ContenidoCampeonatosEquiposRepository.EsVideoYoutube(url));

    [Fact]
    public void Los_cuadros_siguen_el_orden_del_asp_con_nombres_personalizados_y_categoria_recortada()
    {
        var fila = Fila(("Cat2", "0"), ("VerPrimera", "S"), ("VerPrimeraC", "S "), ("VerSegunda", "S"), ("VerGrupoD", "S"), ("VerTercera", "N"));
        var pedidas = new List<ClaveNombreCuadro>();

        var cuadros = ReglasCampeonatoEquipos.Cuadros("M", "ABSOLUTO", fila,
            k => { pedidas.Add(k); return k.Nivel == 2 ? "Plata" : null; },
            (nivel, cuadro) => $"PrevioEquipos.asp?Nivel={nivel}&Cuadro={cuadro}");

        Assert.Equal(["Cuadro final", "Cuadro de consolación", "Plata", "Grupo D"], cuadros.Select(c => c.Titulo));
        Assert.Equal(["final", "consolacion", "final", "grupo"], cuadros.Select(c => c.Tipo));
        Assert.Equal("PrevioEquipos.asp?Nivel=1&Cuadro=GrupoD", cuadros[3].Url);
        // Solo se buscan nombres donde el ASP los buscaba (primera y segunda; no consolación ni grupo D).
        Assert.Equal([new ClaveNombreCuadro(1, "ABSOLUTO", "M", "Final"), new ClaveNombreCuadro(2, "ABSOLUTO", "M", "Final")], pedidas);
    }

    [Fact]
    public void Con_segunda_categoria_el_primer_cuadro_es_primera_categoria()
    {
        var cuadros = ReglasCampeonatoEquipos.Cuadros("F", "ABSOLUTO", Fila(("Cat2", "2"), ("VerPrimera", "S")), _ => null, (_, _) => "u");

        Assert.Equal("Primera categoría", Assert.Single(cuadros).Titulo);
    }

    [Fact]
    public void Las_busquedas_de_nombre_respetan_las_rarezas_del_asp()
    {
        var pedidas = new List<ClaveNombreCuadro>();
        string? Anotar(ClaveNombreCuadro k) { pedidas.Add(k); return null; }

        // Femenino: los grupos A y B se buscaban con Nivel = 2 aunque el enlace es de nivel 1.
        var femenino = ReglasCampeonatoEquipos.Cuadros("F", "ABSOLUTO", Fila(("VerGrupoA", "S")), Anotar, (_, _) => "u");
        // Mixto: la primera categoría se buscaba siempre con Categoria = 'ABSOLUTO'.
        ReglasCampeonatoEquipos.Cuadros("X", "JUNIOR", Fila(("VerPrimera", "S"), ("VerTercera", "S")), Anotar, (_, _) => "u");

        Assert.Equal(1, Assert.Single(femenino).Nivel);
        Assert.Equal(
        [
            new ClaveNombreCuadro(2, "ABSOLUTO", "F", "GrupoA"),
            new ClaveNombreCuadro(1, "ABSOLUTO", "X", "Final"),
            new ClaveNombreCuadro(3, "JUNIOR", "X", "Final"),
        ], pedidas);
    }

    [Fact]
    public void Las_columnas_de_visibilidad_no_se_repiten_e_incluyen_todos_los_grupos()
    {
        Assert.Equal(ReglasCampeonatoEquipos.ColumnasVisibilidad.Count,
            ReglasCampeonatoEquipos.ColumnasVisibilidad.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains("VerGrupoZ", ReglasCampeonatoEquipos.ColumnasVisibilidad);
        Assert.Contains("VerDecima", ReglasCampeonatoEquipos.ColumnasVisibilidad);
        Assert.All(ReglasCampeonatoEquipos.ColumnasVisibilidad, c => Assert.Matches("^Ver[A-Za-z0-9]+$", c));
    }

    [Fact]
    public void Con_preinscripcion_de_clubes_abierta_el_primer_paso_es_la_preinscripcion()
    {
        var acciones = ReglasCampeonatoEquipos.Acciones(30000, preinscripcion: true, fechaPreinscripcion: Ahora.AddDays(3),
            porClubes: true, circuito: null, Ahora, ContenidoCampeonatosEquipos.Vacio, r => $"https://web/{r}");

        Assert.Equal(["preinscripcion-club", "clubes-preinscritos", "nuevo-equipo", "modificar-equipo"], acciones.Select(a => a.Clave));
        Assert.Equal("https://web/Camp_Equipos_InscPrevia.asp?idtorneo=30000", acciones[0].Url);
        Assert.Equal(Ahora.AddDays(3), acciones[0].Hasta);
        Assert.Contains("del club", acciones[^1].Nota);
    }

    [Fact]
    public void Las_excepciones_por_torneo_y_la_copa_cambian_los_pasos()
    {
        var contenido = new ContenidoCampeonatosEquipos(null, [], [28676], [23069], []);

        var copa = ReglasCampeonatoEquipos.Acciones(28676, true, Ahora.AddDays(-1), true, "COPA1 ", Ahora, contenido, r => r);
        var sinPago = ReglasCampeonatoEquipos.Acciones(23069, false, null, false, null, Ahora, contenido, r => r);

        // 28676: sin listado de clubes y con la preinscripción ya cerrada; COPA1 añade el envío del Excel.
        Assert.Equal(["excel-clasificados", "nuevo-equipo", "modificar-equipo"], copa.Select(a => a.Clave));
        // 23069: sin el pago de equipos; inscripción por federaciones.
        Assert.Equal(["clubes-pagados", "nuevo-equipo", "modificar-equipo"], sinPago.Select(a => a.Clave));
        Assert.Contains("de la federación", sinPago[1].Nota);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("2026-11-13T10:00:00", false)]
    [InlineData("2026-11-13T10:00:01", true)]
    public void La_inscripcion_solo_esta_abierta_antes_de_la_fecha_limite(string? limite, bool abierta)
        => Assert.Equal(abierta, ReglasCampeonatoEquipos.InscripcionAbierta(limite is null ? null : DateTime.Parse(limite), Ahora));

    [Theory]
    [InlineData("https://http://padelbarcelonaprat.com/", "https://padelbarcelonaprat.com/")]
    [InlineData("www.regalpadelclub.com", "https://www.regalpadelclub.com/")]
    [InlineData("http://www.usport.es", "https://www.usport.es/")]
    [InlineData("#", null)]
    [InlineData("   ", null)]
    [InlineData("sin-dominio", null)]
    public void La_web_del_club_se_normaliza(string? web, string? esperada)
        => Assert.Equal(esperada, CampeonatoEquiposRepository.WebClub(web));

    [Theory]
    [InlineData("pdf", "pdf")]
    [InlineData(" XLS ", "xls")]
    [InlineData("-- ", null)]
    [InlineData("", null)]
    [InlineData("<img>", null)]
    public void El_tipo_de_adjunto_sale_del_icono(string? icono, string? tipo)
        => Assert.Equal(tipo, ReglasCampeonatoEquipos.TipoAdjunto(icono));

    [Theory]
    [InlineData("41,6488", 90, 41.6488)]
    [InlineData("-0.8891", 180, -0.8891)]
    [InlineData("200", 180, null)]
    [InlineData("N", 90, null)]
    [InlineData(null, 90, null)]
    public void Las_coordenadas_de_texto_se_validan(string? valor, double limite, double? esperada)
        => Assert.Equal(esperada, ReglasCampeonatoEquipos.Coordenada(valor, limite));

    [Theory]
    [InlineData(null, true)]
    [InlineData("0", true)]
    [InlineData("0.00", true)]
    [InlineData("8", false)]
    public void Cat2_vacio_o_cero_es_sin_segunda_categoria(string? valor, bool cero)
        => Assert.Equal(cero, ReglasCampeonatoEquipos.EsCero(valor));

    private static IReadOnlyDictionary<string, object?> Fila(params (string Columna, object? Valor)[] valores)
        => valores.ToDictionary(v => v.Columna, v => v.Valor, StringComparer.OrdinalIgnoreCase);

    private WebApplicationFactory<Program> ConRepositorio(ICampeonatoEquiposRepository repositorio)
        => factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddSingleton(repositorio);
            s.AddSingleton<TimeProvider>(new RelojFijo(new DateTimeOffset(Ahora, TimeSpan.Zero)));
        }));

    /// <summary>Conoce el 28676 (por equipos) y el 31277 (por parejas, silla de ruedas).</summary>
    private sealed class RepositorioFalso : ICampeonatoEquiposRepository
    {
        public int? IdRecibido { get; private set; }
        public DateTime? AhoraRecibido { get; private set; }

        public Task<ConsultaCampeonatoEquipos> ObtenerAsync(int id, DateTime ahora, CancellationToken cancellationToken)
        {
            (IdRecibido, AhoraRecibido) = (id, ahora);
            return Task.FromResult(id switch
            {
                28676 => new ConsultaCampeonatoEquipos(Ficha(id, ahora), null),
                31277 => new ConsultaCampeonatoEquipos(null, "https://www.padelfederacion.es/Camp_Parejas.asp?IdTorneo=31277"),
                _ => new ConsultaCampeonatoEquipos(null, null),
            });
        }

        private static CampeonatoEquiposDto Ficha(int id, DateTime ahora) => new(
            id, 2026, "VI COPA DE ESPAÑA DE LIGAS AUTONÓMICAS - HEAD", "absoluto", false, false,
            new DateOnly(2026, 11, 13), new DateOnly(2026, 11, 15), "barcelona",
            [new SedeCampeonatoDto("PADEL BARCELONA-EL PRAT", null, null, null)], [160m],
            [new GeneroCampeonatoDto("masculino", []), new GeneroCampeonatoDto("femenino", [])],
            null, [], [], true,
            [new GrupoCuadrosDto("masculino", "ABSOLUTO", [new CuadroEquiposDto("Cuadro final", 1, "Final", "final", "u")])],
            null, [], [],
            new InscripcionEquiposDto(false, null, "federaciones", null, [], "u", null),
            null, null, null, [], "https://www.padelfederacion.es/Camp_Equipos.asp?IdTorneo=" + id, ahora);
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
