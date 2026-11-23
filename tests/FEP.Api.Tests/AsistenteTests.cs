using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FEP.Api.Contracts.Asistente;
using FEP.Api.Contracts.Competicion;
using FEP.Api.Data.Repositories.Asistente;
using FEP.Api.Data.Repositories.Competicion;
using FEP.Api.Infrastructure.Asistente;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FEP.Api.Tests;

/// <summary>
/// POST /api/v1/asistente/chat y sus piezas sin red: el corpus (Contenidos/asistente, generado con
/// tools/generar-corpus-asistente.ps1), las herramientas con repositorios falsos y la validación, los errores y el límite
/// de peticiones del endpoint con un asistente falso. La llamada real a Claude no se prueba aquí (necesita clave y red).
/// </summary>
public sealed class AsistenteTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Ruta = "/api/v1/asistente/chat";

    // --- Corpus ----------------------------------------------------------------------------------------

    [Fact]
    public void El_corpus_carga_los_documentos_y_la_normativa_de_rankings()
    {
        var corpus = factory.Services.GetRequiredService<ICorpusNormativa>();

        Assert.True(corpus.Documentos.Count(d => !d.SinTexto) >= 60);
        Assert.Contains("reglamentos", corpus.Categorias);
        Assert.Contains(CorpusNormativa.CategoriaRankings, corpus.Categorias);
        Assert.Contains(corpus.Documentos, d => d.Id == "rankings-normativa-absoluto" && d.Url == "ranking-absoluto.html");
    }

    [Theory]
    [InlineData("reglamento disciplina sanciones", "reglamentos-reglamentodisciplina2009")]
    [InlineData("puntuación circuito nacional veteranos", "reglamentos-modificacion-puntuacion-ranking-de-veteranos")]
    [InlineData("tarifas jueces árbitro", "reglamentos-tarifas-jueces-arbitro-fep-2026")]
    [InlineData("estatutos asamblea general miembros", "estatutos-10-g61-estatutos-padel-cd-18-06-2026")]
    public void La_busqueda_encuentra_el_documento_esperado_entre_los_primeros(string consulta, string documento)
    {
        var corpus = factory.Services.GetRequiredService<ICorpusNormativa>();

        var resultados = corpus.Buscar(consulta, null, 6);

        Assert.Contains(resultados, r => r.Documento.Id == documento);
    }

    [Fact]
    public void La_busqueda_filtra_por_categoria_y_no_devuelve_nada_sin_terminos()
    {
        var corpus = factory.Services.GetRequiredService<ICorpusNormativa>();

        Assert.All(corpus.Buscar("campeonato de españa", "circulares", 6), r => Assert.Equal("circulares", r.Documento.Categoria));
        Assert.Empty(corpus.Buscar("de la y el", null, 6));
        Assert.Empty(corpus.Buscar("   ", null, 6));
    }

    [Fact]
    public void Los_terminos_ignoran_acentos_mayusculas_plurales_y_palabras_vacias()
    {
        Assert.Equal(["sancion", "jugador", "categoria", "nacional"], CorpusNormativa.Terminos("¿Las SANCIONES de los jugadores y categorías nacionales?"));
        Assert.Equal(CorpusNormativa.Terminos("jugadora"), CorpusNormativa.Terminos("jugadoras"));
        Assert.Equal(["2026", "rtg"], CorpusNormativa.Terminos("RTG 2026").Order().ToList());
    }

    // --- Herramientas ----------------------------------------------------------------------------------

    [Fact]
    public async Task Buscar_documentos_devuelve_fragmentos_con_documento_paginas_y_fuentes()
    {
        var h = Herramientas();

        var r = await h.EjecutarAsync(HerramientasAsistente.BuscarDocumentos, Entrada(new { consulta = "reglamento disciplina sanciones" }), default);

        Assert.False(r.EsError);
        Assert.Contains("<fragmento documento=\"Reglamento de Disciplina Deportiva FEP\"", r.Contenido);
        Assert.Contains(r.Fuentes, f => f.Tipo == "documento" && f.Titulo == "Reglamento de Disciplina Deportiva FEP" && f.Detalle!.StartsWith("pág. "));
        Assert.True(r.Contenido.Length <= HerramientasAsistente.LongitudMaximaResultado + 30);
    }

    [Fact]
    public async Task Buscar_documentos_con_categoria_no_valida_es_un_error_para_el_modelo()
    {
        var r = await Herramientas().EjecutarAsync(HerramientasAsistente.BuscarDocumentos,
            Entrada(new { consulta = "ranking", categoria = "inventada" }), default);

        Assert.True(r.EsError);
        Assert.Contains("Categoría no válida", r.Contenido);
    }

    [Fact]
    public async Task Listar_documentos_filtra_por_categoria()
    {
        var r = await Herramientas().EjecutarAsync(HerramientasAsistente.ListarDocumentos, Entrada(new { categoria = "circulares" }), default);

        var lista = JsonDocument.Parse(r.Contenido).RootElement;
        Assert.True(lista.GetArrayLength() >= 10);
        Assert.All(lista.EnumerateArray(), d => Assert.Equal("circulares", d.GetProperty("categoria").GetString()));
    }

    [Theory]
    [InlineData("{\"ranking\":\"inventado\"}", "Ranking no válido")]
    [InlineData("{\"ranking\":\"absoluto\",\"genero\":\"X\"}", "Género no válido")]
    [InlineData("{\"ranking\":\"absoluto\",\"categoria\":\"cadete\"}", "no tiene categorías")]
    [InlineData("{\"ranking\":\"menores\",\"categoria\":\"senior\"}", "Categoría no válida")]
    [InlineData("{\"ranking\":\"absoluto\",\"licencia\":0}", "licencia")]
    public async Task Consultar_ranking_valida_los_filtros_antes_de_ir_a_la_bd(string json, string mensaje)
    {
        var repo = new RankingFalso();
        var r = await Herramientas(repo).EjecutarAsync(HerramientasAsistente.ConsultarRanking, Entrada(json), default);

        Assert.True(r.EsError);
        Assert.Contains(mensaje, r.Contenido);
        Assert.Null(repo.Recibido);
    }

    [Fact]
    public async Task Consultar_ranking_pasa_los_criterios_y_devuelve_datos_sin_urls_ni_documentos()
    {
        var repo = new RankingFalso();

        var r = await Herramientas(repo).EjecutarAsync(HerramientasAsistente.ConsultarRanking,
            Entrada("{\"ranking\":\"veteranos\",\"genero\":\"F\",\"categoria\":\"+40\",\"nombre\":\"Pérez Gómez\",\"limite\":500}"), default);

        Assert.False(r.EsError);
        Assert.Equal(("F", "40", HerramientasAsistente.PuestosMaximos), (repo.Recibido!.Genero, repo.Recibido.Categoria, repo.Recibido.Limite));
        Assert.Equal(2, repo.Recibido.Terminos.Count);
        Assert.DoesNotContain("Url", r.Contenido);
        Assert.DoesNotContain(".pdf", r.Contenido);
        Assert.Contains("\"paginaRanking\":\"ranking-veteranos.html\"", r.Contenido);
        var fuente = Assert.Single(r.Fuentes);
        Assert.Equal(("ranking", "ranking-veteranos.html"), (fuente.Tipo, fuente.Url));
    }

    [Fact]
    public async Task Si_la_bd_falla_la_herramienta_devuelve_un_error_legible_y_no_una_excepcion()
    {
        var r = await Herramientas(new RankingFalso { Fallar = true }).EjecutarAsync(HerramientasAsistente.ConsultarRanking,
            Entrada(new { ranking = "absoluto" }), default);

        Assert.True(r.EsError);
        Assert.Contains("no están disponibles", r.Contenido);
    }

    [Fact]
    public async Task Estadisticas_de_una_licencia_sin_ranking_no_es_un_error()
    {
        var r = await Herramientas().EjecutarAsync(HerramientasAsistente.EstadisticasJugador, Entrada(new { licencia = 123 }), default);

        Assert.False(r.EsError);
        Assert.Contains("No hay estadísticas", r.Contenido);
        Assert.Empty(r.Fuentes);
    }

    [Fact]
    public void Las_definiciones_de_herramientas_tienen_esquema_de_objeto()
    {
        Assert.All(Herramientas().Definiciones, d =>
        {
            Assert.Equal("object", d.Esquema.GetProperty("type").GetString());
            Assert.Equal(JsonValueKind.Object, d.Esquema.GetProperty("properties").ValueKind);
        });
    }

    [Fact]
    public void Las_instrucciones_incluyen_el_catalogo_y_la_marca_de_derivacion()
    {
        var corpus = factory.Services.GetRequiredService<ICorpusNormativa>();

        var texto = InstruccionesAsistente.Construir(corpus.Documentos);

        Assert.Contains(InstruccionesAsistente.MarcaDerivar, texto);
        Assert.Contains("Reglamento Técnico General FEP 2027", texto);
        Assert.DoesNotContain("{{", texto);
    }

    // --- Endpoint --------------------------------------------------------------------------------------

    [Theory]
    [InlineData("{}", "mensajes")]
    [InlineData("{\"mensajes\":[]}", "mensajes")]
    [InlineData("{\"mensajes\":[{\"rol\":\"asistente\",\"texto\":\"hola\"}]}", "mensajes[0].rol")]
    [InlineData("{\"mensajes\":[{\"rol\":\"usuario\",\"texto\":\"a\"},{\"rol\":\"usuario\",\"texto\":\"b\"}]}", "mensajes[0].rol")]
    [InlineData("{\"mensajes\":[{\"rol\":\"usuario\",\"texto\":\"  \"}]}", "mensajes[0].texto")]
    [InlineData("{\"mensajes\":[{\"rol\":\"usuario\",\"texto\":\"hola\"}],\"idioma\":\"fr\"}", "idioma")]
    public async Task Conversaciones_no_validas_devuelven_400(string json, string campo)
    {
        using var app = ConAsistente(new AsistenteFalso());
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.PostAsync(Ruta, new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(problema.GetProperty("errors").EnumerateObject(), p => p.Name.Equals(campo, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Una_pregunta_demasiado_larga_devuelve_400()
    {
        using var app = ConAsistente(new AsistenteFalso());
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.PostAsJsonAsync(Ruta,
            new PeticionChatDto([new MensajeChatDto("usuario", new string('a', ReglasChat.LongitudMaximaPregunta + 1))], "es"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Una_conversacion_valida_llega_al_asistente_y_devuelve_su_respuesta()
    {
        var asistente = new AsistenteFalso();
        using var app = ConAsistente(asistente);
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.PostAsJsonAsync(Ruta, new PeticionChatDto(
        [
            new MensajeChatDto("usuario", "¿Cuántas pruebas suman en el ranking?"),
            new MensajeChatDto("asistente", "Las 13 mejores."),
            new MensajeChatDto("usuario", "¿Y en veteranos?"),
        ], "es"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("respuesta", cuerpo.GetProperty("texto").GetString());
        Assert.False(cuerpo.GetProperty("derivar").GetBoolean());
        Assert.Equal(3, asistente.Recibida!.Mensajes!.Count);
    }

    [Fact]
    public async Task Si_el_asistente_no_esta_disponible_devuelve_503_con_el_formulario_de_contacto()
    {
        using var app = ConAsistente(new AsistenteFalso { NoDisponible = true });
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.PostAsJsonAsync(Ruta, new PeticionChatDto([new MensajeChatDto("usuario", "hola")], null));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.StartsWith("https://", problema.GetProperty("urlContacto").GetString());
    }

    [Fact]
    public async Task Sin_clave_de_api_el_asistente_real_devuelve_503()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
        {
            return; // en una máquina con la clave en el entorno esta prueba no aplica
        }

        using var app = factory.WithWebHostBuilder(b => b.UseSetting("Asistente:ApiKey", ""));
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.PostAsJsonAsync(Ruta, new PeticionChatDto([new MensajeChatDto("usuario", "hola")], "es"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
    }

    [Fact]
    public async Task Superado_el_limite_por_minuto_devuelve_429_con_retry_after()
    {
        using var app = ConAsistente(new AsistenteFalso()).WithWebHostBuilder(b => b.UseSetting("Asistente:PreguntasPorMinuto", "2"));
        using var cliente = app.CreateClient();
        var peticion = new PeticionChatDto([new MensajeChatDto("usuario", "hola")], "es");

        for (var i = 0; i < 2; i++)
        {
            using var ok = await cliente.PostAsJsonAsync(Ruta, peticion);
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        using var respuesta = await cliente.PostAsJsonAsync(Ruta, peticion);

        Assert.Equal(HttpStatusCode.TooManyRequests, respuesta.StatusCode);
        Assert.True(respuesta.Headers.Contains("Retry-After"));
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problema.TryGetProperty("urlContacto", out _));
    }

    // --- Presupuesto -----------------------------------------------------------------------------------

    [Fact]
    public void El_presupuesto_se_agota_y_se_renueva_al_dia_siguiente()
    {
        var reloj = new RelojFijo(new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero));
        var presupuesto = new PresupuestoAsistente(Microsoft.Extensions.Options.Options.Create(new AsistenteOptions { TokensDiarios = 1000 }), reloj);

        presupuesto.Registrar(999);
        Assert.True(presupuesto.HayPresupuesto());
        presupuesto.Registrar(1);
        Assert.False(presupuesto.HayPresupuesto());

        reloj.Ahora = reloj.Ahora.AddDays(1);
        Assert.True(presupuesto.HayPresupuesto());
        Assert.Equal(0, presupuesto.ConsumidosHoy);
    }

    // --- Utilidades ------------------------------------------------------------------------------------

    private HerramientasAsistente Herramientas(RankingFalso? ranking = null)
        => new(factory.Services.GetRequiredService<ICorpusNormativa>(), ranking ?? new RankingFalso(), new EstadisticasFalsas(),
            TimeProvider.System, NullLogger<HerramientasAsistente>.Instance);

    private WebApplicationFactory<Program> ConAsistente(IAsistente asistente)
        => factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped(_ => asistente)));

    private static Dictionary<string, JsonElement> Entrada(object valor)
        => Entrada(JsonSerializer.Serialize(valor));

    private static Dictionary<string, JsonElement> Entrada(string json)
        => JsonDocument.Parse(json).RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());

    private sealed class AsistenteFalso : IAsistente
    {
        public bool NoDisponible { get; init; }
        public PeticionChatDto? Recibida { get; private set; }

        public Task<RespuestaChatDto> ResponderAsync(PeticionChatDto peticion, CancellationToken cancellationToken)
        {
            Recibida = peticion;
            if (NoDisponible)
            {
                throw new AsistenteNoDisponibleException("prueba");
            }

            return Task.FromResult(new RespuestaChatDto("respuesta", [], false, "https://www.padelfederacion.es/Contacto"));
        }
    }

    private sealed class RankingFalso : IClasificacionesRankingRepository
    {
        public bool Fallar { get; init; }
        public CriteriosClasificacion? Recibido { get; private set; }

        public Task<ClasificacionRankingDto> ObtenerAsync(DefinicionClasificacion ranking, CriteriosClasificacion criterios, CancellationToken cancellationToken)
        {
            if (Fallar)
            {
                throw new InvalidOperationException("Falta la cadena de conexión");
            }

            Recibido = criterios;
            var lista = new ListaRankingDto(11040, "F", "40", "+40 femenino", 300, 12, new DateOnly(2026, 11, 11),
                "docs/ranking.pdf", "https://www.padelfederacion.es/refs/docs/ranking.pdf", 1,
                [new PuestoRankingDto(4, 900001, "JUGADORA DE PRUEBA", 10, 1200, 8, 1100, 2, 3, "https://www.padelfederacion.es/Estadisticas?id=900001")]);
            return Task.FromResult(new ClasificacionRankingDto(ranking.Clave, ranking.Titulo,
                [new OpcionRankingDto("M", "Masculino"), new OpcionRankingDto("F", "Femenino")], [],
                criterios.Genero, criterios.Categoria, criterios.Licencia, null, criterios.Limite, [lista], ranking.PrefijoLicencia));
        }
    }

    private sealed class EstadisticasFalsas : IEstadisticasJugadorRepository
    {
        public Task<ConsultaEstadisticas> ObtenerAsync(int licencia, DateTime ahora, CancellationToken cancellationToken)
            => Task.FromResult(new ConsultaEstadisticas(EstadoEstadisticas.LicenciaNoEncontrada, null));
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = ahora;

        public override DateTimeOffset GetUtcNow() => Ahora;
    }
}
