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
/// GET /api/v1/competicion/rankings/{clave} y /{clave}/normativa (migrados desde Ranking_Absoluto.asp,
/// CargarRnkAbs.asp y CargarRnkAbsNombre.asp, desde Ranking_Menores.asp con CargarRnkMen.asp, desde
/// Ranking_Silla_Ruedas.asp y desde Ranking_Veteranos.asp con CargarRnkVet.asp). La clasificación necesita SQL Server: aquí se prueban la validación
/// (que corta antes de tocar la BD), los criterios que llegan al repositorio (con un repositorio falso), las reglas de
/// <see cref="ReglasClasificaciones"/> y la normativa, que sale de src/FEP.Api/Contenidos/rankings/absoluto.json.
/// </summary>
public sealed class ClasificacionesRankingTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Ruta = "/api/v1/competicion/rankings/absoluto";

    // --- Validación ----------------------------------------------------------------------------------

    [Theory]
    [InlineData("genero=Z", "genero")]
    [InlineData("genero=0", "genero")]       // el "----" del desplegable del ASP (que acababa mostrando el femenino)
    [InlineData("genero=X", "genero")]       // el absoluto no tiene ranking mixto
    [InlineData("categoria=cadete", "categoria")]
    [InlineData("licencia=0", "licencia")]
    [InlineData("licencia=abc", "licencia")]
    [InlineData("licencia=1%20OR%201%3D1", "licencia")]
    [InlineData("limite=0", "limite")]
    [InlineData("limite=501", "limite")]
    [InlineData("busqueda=a+b+c+d+e+f+g", "busqueda")]
    public async Task Parametros_no_validos_devuelven_400_con_ValidationProblemDetails(string query, string campo)
    {
        using var app = ConRepositorio(new RepositorioFalso());
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problema.GetProperty("status").GetInt32());
        Assert.Contains(problema.GetProperty("errors").EnumerateObject(),
            p => p.Name.Equals(campo, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Una_busqueda_demasiado_larga_devuelve_400()
    {
        using var app = ConRepositorio(new RepositorioFalso());
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}?busqueda={new string('a', 101)}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/competicion/rankings/no-existe")]
    [InlineData("/api/v1/competicion/rankings/no-existe/normativa")]
    public async Task Un_ranking_sin_clasificacion_migrada_devuelve_404_ProblemDetails(string ruta)
    {
        using var app = ConRepositorio(new RepositorioFalso());
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync(ruta);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
    }

    // --- Criterios y forma del JSON --------------------------------------------------------------------

    [Fact]
    public async Task Sin_parametros_pide_todos_los_generos_con_el_top_100()
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync(Ruta);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(respuesta.Headers.CacheControl?.Public);
        Assert.NotNull(falso.Recibido);
        Assert.Equal("absoluto", falso.Ranking?.Clave);
        Assert.Null(falso.Recibido.Genero);
        Assert.Null(falso.Recibido.Categoria);
        Assert.Null(falso.Recibido.Licencia);
        Assert.Empty(falso.Recibido.Terminos);
        Assert.Equal(ReglasClasificaciones.LimitePorDefecto, falso.Recibido.Limite);
    }

    [Fact]
    public async Task Normaliza_genero_busqueda_y_limite_y_acepta_la_clave_en_mayusculas()
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        var datos = await cliente.GetFromJsonAsync<JsonElement>(
            $"/api/v1/competicion/rankings/ABSOLUTO?genero=femenino&licencia=3889&busqueda={Uri.EscapeDataString("  sanmartí-beleta ")}&limite=200");

        Assert.NotNull(falso.Recibido);
        Assert.Equal("F", falso.Recibido.Genero);
        Assert.Equal(3889, falso.Recibido.Licencia);
        Assert.Equal(new[] { "sanmartí", "beleta" }, falso.Recibido.Terminos);
        Assert.Equal(200, falso.Recibido.Limite);
        // JSON en camelCase con los criterios aplicados.
        Assert.Equal("absoluto", datos.GetProperty("clave").GetString());
        Assert.Equal("F", datos.GetProperty("genero").GetString());
        Assert.Equal(3889, datos.GetProperty("licencia").GetInt32());
        Assert.Equal("sanmartí beleta", datos.GetProperty("busqueda").GetString());
        Assert.Equal(200, datos.GetProperty("limite").GetInt32());
        var lista = datos.GetProperty("listas")[0];
        Assert.Equal(10001, lista.GetProperty("tipoRanking").GetInt32());
        Assert.Equal("2026-11-04", lista.GetProperty("actualizado").GetString());
        Assert.Equal("https://www.padelfederacion.es/refs/docs/Rnk_Unico_Fem_24_09_25.pdf", lista.GetProperty("documentoUrl").GetString());
        var puesto = lista.GetProperty("puestos")[0];
        Assert.Equal(1, puesto.GetProperty("posicion").GetInt32());
        Assert.Equal(65207, puesto.GetProperty("puntosRanking").GetInt32());
        Assert.Equal("https://www.padelfederacion.es/Estadisticas?id=3889", puesto.GetProperty("estadisticasUrl").GetString());
    }

    [Theory]
    [InlineData("M", "M")]
    [InlineData("m", "M")]
    [InlineData(" Masculino ", "M")]
    [InlineData("FEMENINO", "F")]
    [InlineData("x", "X")]
    public void El_genero_se_normaliza(string genero, string esperado)
    {
        Assert.True(ReglasClasificaciones.TryNormalizarGenero(genero, out var normalizado));
        Assert.Equal(esperado, normalizado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Sin_genero_no_se_filtra(string? genero)
    {
        Assert.True(ReglasClasificaciones.TryNormalizarGenero(genero, out var normalizado));
        Assert.Null(normalizado);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("hombre")]
    public void Un_genero_desconocido_no_es_valido(string genero)
        => Assert.False(ReglasClasificaciones.TryNormalizarGenero(genero, out _));

    // --- Reglas ------------------------------------------------------------------------------------------

    [Fact]
    public void El_absoluto_es_10000_masculino_y_10001_femenino()
    {
        var absoluto = ReglasClasificaciones.Buscar("Absoluto");

        Assert.NotNull(absoluto);
        Assert.Equal([10000, 10001], absoluto.Tipos.Select(t => t.TipoRanking));
        Assert.Equal(["M", "F"], absoluto.Generos);
        Assert.Empty(absoluto.Categorias);
        Assert.Equal("Ranking_Absoluto", absoluto.PaginaWebActual);
    }

    // --- Menores (Ranking_Menores.asp + CargarRnkMen.asp) ------------------------------------------------

    [Fact]
    public void Menores_tiene_un_TipoRanking_por_categoria_y_genero_como_CargarRnkMen()
    {
        var menores = ReglasClasificaciones.Buscar("MENORES");

        Assert.NotNull(menores);
        Assert.Equal("Ranking_Menores", menores.PaginaWebActual);
        Assert.Equal(["M", "F"], menores.Generos);
        Assert.Equal(["benjamin", "alevin", "infantil", "cadete", "junior"], menores.Categorias);
        Assert.Equal([8000, 8001, 7000, 7001, 6000, 6001, 5000, 5001, 4000, 4001], menores.Tipos.Select(t => t.TipoRanking));
        // Los mixtos (x002) no se publican (DECISIÓN FEP).
        Assert.DoesNotContain(menores.Tipos, t => t.Genero == "X" || t.TipoRanking % 1000 == 2);
        Assert.Contains(menores.Tipos, t => t is { TipoRanking: 6001, Genero: "F", Categoria: "infantil" });
    }

    [Theory]
    [InlineData("benjamin", "Benjamín")]
    [InlineData("alevin", "Alevín")]
    [InlineData("infantil", "Infantil")]
    [InlineData("cadete", "Cadete")]
    [InlineData("junior", "Júnior")]
    [InlineData("cat-35", "Cat 35")]
    public void Las_categorias_se_muestran_con_tilde(string categoria, string esperado)
        => Assert.Equal(esperado, ReglasClasificaciones.NombreCategoria(categoria));

    [Theory]
    [InlineData("categoria=senior", "categoria")]
    [InlineData("categoria=0", "categoria")]   // el "----" del desplegable del ASP
    [InlineData("genero=X", "genero")]         // mixto: no publicado (CargarRnkMen.asp daba 500 en benjamín y alevín)
    [InlineData("genero=0", "genero")]         // CargarRnkMen.asp componía "TipoRanking=" vacío → error 500
    [InlineData("licencia=1%20OR%201%3D1", "licencia")]
    public async Task Menores_rechaza_filtros_no_validos_con_400(string query, string campo)
    {
        using var app = ConRepositorio(new RepositorioFalso());
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync($"/api/v1/competicion/rankings/menores?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(problema.GetProperty("errors").EnumerateObject(),
            p => p.Name.Equals(campo, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Menores_normaliza_la_categoria_y_el_genero()
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync("/api/v1/competicion/rankings/menores?categoria=%20CADETE%20&genero=femenino&limite=50");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.NotNull(falso.Recibido);
        Assert.Equal("menores", falso.Ranking?.Clave);
        Assert.Equal("cadete", falso.Recibido.Categoria);
        Assert.Equal("F", falso.Recibido.Genero);
        Assert.Equal(50, falso.Recibido.Limite);
    }

    [Fact]
    public async Task Menores_sin_normativa_devuelve_404_con_la_pagina_de_la_web_actual()
    {
        using var cliente = factory.CreateClient();

        using var respuesta = await cliente.GetAsync("/api/v1/competicion/rankings/menores/normativa");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.EndsWith("/Ranking_Menores", problema.GetProperty("urlWebActual").GetString());
    }

    // --- Silla de ruedas (Ranking_Silla_Ruedas.asp) ----------------------------------------------------------

    [Fact]
    public void Silla_de_ruedas_es_el_TipoRanking_15_en_una_lista_mixta_con_licencias_PS()
    {
        var silla = ReglasClasificaciones.Buscar("Silla-Ruedas");

        Assert.NotNull(silla);
        Assert.Equal("Ranking_Silla_Ruedas", silla.PaginaWebActual);
        var tipo = Assert.Single(silla.Tipos);
        Assert.Equal(15, tipo.TipoRanking);
        Assert.Equal("X", tipo.Genero);
        Assert.Null(tipo.Categoria);
        Assert.Equal("General", tipo.Titulo);
        Assert.Equal(["X"], silla.Generos);
        Assert.Empty(silla.Categorias);
        Assert.Equal(LicenciasRanking.Silla, silla.Licencias);
        Assert.Equal("PS", silla.PrefijoLicencia);
        // Los rankings de jugadores no llevan prefijo.
        Assert.Null(ReglasClasificaciones.Buscar("absoluto")?.PrefijoLicencia);
    }

    [Fact]
    public void Las_estadisticas_de_silla_enlazan_a_Silla_Ruedas_Est_con_el_prefijo_PS()
        => Assert.Equal("Silla-Ruedas-Est?id=PS1008", ReglasClasificaciones.RutaEstadisticas(1008, LicenciasRanking.Silla));

    [Theory]
    [InlineData("genero=M", "genero")]          // una sola lista, mixta
    [InlineData("genero=F", "genero")]
    [InlineData("categoria=general", "categoria")]
    [InlineData("licencia=PS1008", "licencia")] // el prefijo no forma parte del número (lo quita el front)
    public async Task Silla_rechaza_filtros_no_validos_con_400(string query, string campo)
    {
        using var app = ConRepositorio(new RepositorioFalso());
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync($"/api/v1/competicion/rankings/silla-ruedas?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(problema.GetProperty("errors").EnumerateObject(),
            p => p.Name.Equals(campo, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Silla_devuelve_el_prefijo_de_licencia_y_acepta_la_licencia_sin_prefijo()
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        var datos = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/competicion/rankings/silla-ruedas?licencia=1008&genero=x");

        Assert.NotNull(falso.Recibido);
        Assert.Equal("silla-ruedas", falso.Ranking?.Clave);
        Assert.Equal("X", falso.Recibido.Genero);
        Assert.Equal(1008, falso.Recibido.Licencia);
        Assert.Equal("PS", datos.GetProperty("prefijoLicencia").GetString());
    }

    [Fact]
    public async Task El_absoluto_devuelve_prefijoLicencia_null()
    {
        using var app = ConRepositorio(new RepositorioFalso());
        using var cliente = app.CreateClient();

        var datos = await cliente.GetFromJsonAsync<JsonElement>(Ruta);

        Assert.Equal(JsonValueKind.Null, datos.GetProperty("prefijoLicencia").ValueKind);
    }

    [Fact]
    public async Task Silla_sin_normativa_devuelve_404_con_la_pagina_de_la_web_actual()
    {
        using var cliente = factory.CreateClient();

        using var respuesta = await cliente.GetAsync("/api/v1/competicion/rankings/silla-ruedas/normativa");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.EndsWith("/Ranking_Silla_Ruedas", problema.GetProperty("urlWebActual").GetString());
    }

    // --- Veteranos (Ranking_Veteranos.asp + CargarRnkVet.asp) -------------------------------------------------

    [Fact]
    public void Veteranos_tiene_un_TipoRanking_por_categoria_de_edad_y_genero_como_CargarRnkVet()
    {
        var veteranos = ReglasClasificaciones.Buscar("Veteranos");

        Assert.NotNull(veteranos);
        Assert.Equal("Ranking_Veteranos", veteranos.PaginaWebActual);
        Assert.Equal(LicenciasRanking.Jugador, veteranos.Licencias);
        Assert.Null(veteranos.PrefijoLicencia);
        Assert.Equal(["M", "F"], veteranos.Generos);
        Assert.Equal(["35", "40", "45", "50", "55", "60"], veteranos.Categorias);
        Assert.Equal(
            [10035, 11035, 10040, 11040, 10045, 11045, 10050, 11050, 10055, 11055, 10060, 11060],
            veteranos.Tipos.Select(t => t.TipoRanking));
        // Masculino = 10000 + edad, femenino = 11000 + edad (CargarRnkVet.asp).
        Assert.All(veteranos.Tipos, t =>
            Assert.Equal((t.Genero == "M" ? 10000 : 11000) + int.Parse(t.Categoria!, System.Globalization.CultureInfo.InvariantCulture),
            t.TipoRanking));
        // Los "X" de CargarRnkVet.asp eran los mixtos de menores (8002…4002): no se publican.
        Assert.DoesNotContain(veteranos.Tipos, t => t.Genero == "X" || t.TipoRanking < 10000);
    }

    [Theory]
    [InlineData("35", "+35")]
    [InlineData("60", "+60")]
    [InlineData(" 45 ", "+45")]
    public void Las_categorias_de_veteranos_se_muestran_con_el_signo_mas(string categoria, string esperado)
        => Assert.Equal(esperado, ReglasClasificaciones.NombreCategoria(categoria));

    [Theory]
    [InlineData("categoria=CAT35", "categoria")]  // valor del desplegable del ASP: la API usa la edad ("35")
    [InlineData("categoria=30", "categoria")]
    [InlineData("categoria=cadete", "categoria")]  // categorías de menores (CargarRnkMen.asp, al que llamaba por error)
    [InlineData("categoria=0", "categoria")]       // el "----" del desplegable: CargarRnkVet.asp daba 500
    [InlineData("genero=X", "genero")]             // CargarRnkVet.asp servía con "X" los mixtos de menores
    [InlineData("licencia=1%20OR%201%3D1", "licencia")]
    public async Task Veteranos_rechaza_filtros_no_validos_con_400(string query, string campo)
    {
        using var app = ConRepositorio(new RepositorioFalso());
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync($"/api/v1/competicion/rankings/veteranos?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(problema.GetProperty("errors").EnumerateObject(),
            p => p.Name.Equals(campo, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Veteranos_normaliza_la_categoria_y_el_genero()
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        var datos = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/competicion/rankings/VETERANOS?categoria=%2045%20&genero=f");

        Assert.NotNull(falso.Recibido);
        Assert.Equal("veteranos", falso.Ranking?.Clave);
        Assert.Equal("45", falso.Recibido.Categoria);
        Assert.Equal("F", falso.Recibido.Genero);
        Assert.Equal(ReglasClasificaciones.LimitePorDefecto, falso.Recibido.Limite);
        Assert.Equal("45", datos.GetProperty("categoria").GetString());
        Assert.Equal(JsonValueKind.Null, datos.GetProperty("prefijoLicencia").ValueKind);
    }

    [Fact]
    public async Task Veteranos_busca_por_licencia_en_todas_las_categorias()
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync("/api/v1/competicion/rankings/veteranos?licencia=12762");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.NotNull(falso.Recibido);
        Assert.Equal(12762, falso.Recibido.Licencia);
        Assert.Null(falso.Recibido.Genero);
        Assert.Null(falso.Recibido.Categoria);
    }

    [Fact]
    public async Task Veteranos_sin_normativa_devuelve_404_con_la_pagina_de_la_web_actual()
    {
        using var cliente = factory.CreateClient();

        using var respuesta = await cliente.GetAsync("/api/v1/competicion/rankings/veteranos/normativa");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.EndsWith("/Ranking_Veteranos", problema.GetProperty("urlWebActual").GetString());
    }

    [Theory]
    [InlineData("ENRIC", "SANMARTI", "BELETA", "ENRIC SANMARTI BELETA")]
    [InlineData("JAVIER ", "NAVARRO ", "FERNANDEZ ", "JAVIER NAVARRO FERNANDEZ")] // relleno de SFEP_RankingFinal (silla)
    [InlineData("ANA  SOLEDAD ", "MORGADO", null, "ANA SOLEDAD MORGADO")]
    [InlineData(null, "  ", "RUIZ", "RUIZ")]
    public void El_nombre_se_compone_sin_espacios_de_relleno(string? nombre, string? apellido1, string? apellido2, string esperado)
        => Assert.Equal(esperado, ReglasClasificaciones.NombreJugador(nombre, apellido1, apellido2));

    [Fact]
    public void Las_estadisticas_enlazan_a_Estadisticas_y_no_a_Silla_Ruedas_Est()
        => Assert.Equal("Estadisticas?id=3889", ReglasClasificaciones.RutaEstadisticas(3889));

    [Theory]
    [InlineData("100%", "100[%]")]
    [InlineData("a_b", "a[_]b")]
    [InlineData("[x]", "[[]x]")]
    public void Los_comodines_de_LIKE_se_buscan_literalmente(string termino, string esperado)
        => Assert.Equal(esperado, ReglasClasificaciones.EscaparLike(termino));

    [Fact]
    public void La_busqueda_se_parte_por_espacios_y_guiones_como_en_el_ASP()
        => Assert.Equal(["maria", "jose", "lopez"], ReglasClasificaciones.Terminos(" maria-jose  lopez "));

    // --- Normativa (contenido real) ----------------------------------------------------------------------

    [Fact]
    public async Task La_normativa_tiene_los_nueve_apartados_del_original_en_orden()
    {
        using var cliente = factory.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/normativa");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(respuesta.Headers.CacheControl?.Public);
        var datos = await respuesta.Content.ReadFromJsonAsync<NormativaRankingDto>();

        Assert.NotNull(datos);
        Assert.Equal("absoluto", datos.Clave);
        Assert.Equal(
            ["introduccion", "sistema-de-puntuacion", "puntos", "valoracion-economica", "ranking-de-jugadores",
             "numero-de-parejas", "numero-de-licencias", "campeonatos-absolutos", "pruebas-world-padel-tour"],
            datos.Secciones.Select(s => s.Id));
        Assert.All(datos.Secciones, s => Assert.NotEmpty(s.Bloques));
    }

    [Fact]
    public async Task Los_bloques_de_la_normativa_son_coherentes_y_sin_listas_nulas()
    {
        using var cliente = factory.CreateClient();
        var datos = await cliente.GetFromJsonAsync<NormativaRankingDto>($"{Ruta}/normativa");
        Assert.NotNull(datos);

        var bloques = datos.Secciones.SelectMany(s => s.Bloques).ToArray();
        Assert.All(bloques, b =>
        {
            Assert.Contains(b.Tipo, new[] { "parrafo", "nota", "lista", "tabla" });
            Assert.NotNull(b.Elementos);
            switch (b.Tipo)
            {
                case "parrafo" or "nota":
                    Assert.False(string.IsNullOrWhiteSpace(b.Texto));
                    break;
                case "lista":
                    Assert.NotEmpty(b.Elementos);
                    break;
                case "tabla":
                    Assert.NotNull(b.Tabla);
                    Assert.NotEmpty(b.Tabla.Filas);
                    Assert.All(b.Tabla.Filas, f => Assert.Equal(b.Tabla.Columnas.Count, f.Count));
                    break;
            }
        });

        // Cinco tablas: dotación, créditos por puesto, puntos por créditos, parejas y licencias.
        var tablas = bloques.Where(b => b.Tabla is not null).Select(b => b.Tabla!).ToArray();
        Assert.Equal(5, tablas.Length);
        Assert.Equal(14, tablas[0].Filas.Count);
        // Errata del original corregida ("Entre 4.000 € y 4.99 9€").
        Assert.Contains(tablas[0].Filas, f => f[0] == "Entre 4.000 € y 4.999 €" && f[1] == "800");
        // Fila duplicada del original ("10.000º → 1" dos veces) eliminada.
        Assert.Single(tablas[1].Filas, f => f[0] == "10.000º");
    }

    private WebApplicationFactory<Program> ConRepositorio(IClasificacionesRankingRepository repositorio)
        => factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(repositorio)));

    /// <summary>Guarda los criterios recibidos y devuelve una lista con un puesto de ejemplo.</summary>
    private sealed class RepositorioFalso : IClasificacionesRankingRepository
    {
        public DefinicionClasificacion? Ranking { get; private set; }
        public CriteriosClasificacion? Recibido { get; private set; }

        public Task<ClasificacionRankingDto> ObtenerAsync(DefinicionClasificacion ranking, CriteriosClasificacion criterios, CancellationToken cancellationToken)
        {
            Ranking = ranking;
            Recibido = criterios;
            var lista = new ListaRankingDto(10001, "F", null, "Femenino", 7293, 190, new DateOnly(2026, 11, 4),
                "docs/Rnk_Unico_Fem_24_09_25.pdf", "https://www.padelfederacion.es/refs/docs/Rnk_Unico_Fem_24_09_25.pdf", 1,
                [
                    new PuestoRankingDto(1, 3889, "ENRIC SANMARTI BELETA", 14, 65404, 13, 65207, 0, 10,
                        "https://www.padelfederacion.es/Estadisticas?id=3889"),
                ]);
            return Task.FromResult(new ClasificacionRankingDto(
                ranking.Clave, ranking.Titulo,
                [new OpcionRankingDto("M", "Masculino"), new OpcionRankingDto("F", "Femenino")], [],
                criterios.Genero, criterios.Categoria, criterios.Licencia,
                criterios.Terminos.Count > 0 ? string.Join(' ', criterios.Terminos) : null,
                criterios.Limite, [lista], ranking.PrefijoLicencia));
        }
    }
}
