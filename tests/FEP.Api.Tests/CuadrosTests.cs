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
/// GET /api/v1/competicion/cuadros/… (migrado desde Previo.asp, PrevioEquipos.asp, Cuadro*.asp, CuadroPrevia*.asp,
/// CuadroPrePrevia*.asp, CuadroEquipos*.asp, Liguilla*.asp y EquiposEnfrentamientos.asp). Las consultas necesitan SQL
/// Server: aquí se prueban la validación (corta antes de tocar la BD), los 404 y la decodificación de la ruta con un
/// repositorio falso, y las reglas heredadas (plantilla por número de filas, publicación por nivel, huecos sin pareja)
/// y la composición de la respuesta a partir de filas en memoria.
/// </summary>
public sealed class CuadrosTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Ruta = "/api/v1/competicion/cuadros";

    private static readonly TorneoCuadroDto Torneo28714 = new(28714, "CAMPEONATO DE ESPAÑA DE VETERANOS 2026", "parejas",
        new DateOnly(2026, 6, 26), new DateOnly(2026, 6, 28), "San Pedro de Alcántara");

    private static readonly TorneoCuadroDto Torneo28676 = new(28676, "VI COPA DE ESPAÑA DE LIGAS AUTONÓMICAS - HEAD", "equipos",
        new DateOnly(2026, 11, 13), new DateOnly(2026, 11, 15), "Barcelona");

    [Theory]
    [InlineData("0/M/ABSOLUTO/1/Final", "idTorneo")]
    [InlineData("abc/M/ABSOLUTO/1/Final", "idTorneo")]
    [InlineData("28714/Z/ABSOLUTO/1/Final", "genero")]
    [InlineData("28714/M/ABSOLUTO'%20OR%201%3D1--/1/Final", "categoria")]
    [InlineData("28714/M/ABSOLUTO/0/Final", "nivel")]
    [InlineData("28714/M/ABSOLUTO/100/Final", "nivel")]
    [InlineData("28714/M/ABSOLUTO/1/Final%3Bdrop", "cuadro")]
    [InlineData("28714/M/ABSOLUTO/1/UnCuadroConUnNombreDemasiadoLargo", "cuadro")]
    [InlineData("28676/M/ABSOLUTO/1/Final/enfrentamientos/-1/1", "avance")]
    [InlineData("28676/M/ABSOLUTO/1/Final/enfrentamientos/1/0", "posicion")]
    [InlineData("0", "idTorneo")]
    public async Task Parametros_no_validos_devuelven_400_con_ValidationProblemDetails(string ruta, string campo)
    {
        // Sin repositorio falso ni cadena de conexión: si llegara a la BD respondería 500.
        using var cliente = factory.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/{ruta}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(problema.GetProperty("errors").EnumerateObject(), p => p.Name.Equals(campo, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Un_torneo_que_no_existe_devuelve_404_con_el_listado_de_la_web_actual()
    {
        using var app = ConRepositorio(new RepositorioFalso());
        using var cliente = app.CreateClient();

        foreach (var ruta in new[] { "99999999", "99999999/M/ABSOLUTO/1/Final", "99999999/M/ABSOLUTO/1/Final/enfrentamientos/1/1" })
        {
            using var respuesta = await cliente.GetAsync($"{Ruta}/{ruta}");

            Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
            var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("https://www.padelfederacion.es/Campeonatos", problema.GetProperty("urlWebActual").GetString());
        }
    }

    [Fact]
    public async Task Un_cuadro_sin_filas_devuelve_404_con_el_cuadro_de_la_web_actual()
    {
        using var app = ConRepositorio(new RepositorioFalso());
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/28676/F/ABSOLUTO/1/Grupo%20B");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("https://www.padelfederacion.es/PrevioEquipos.asp?IdTorneo=28676&Genero=F&Cat=ABSOLUTO&Nivel=1&Cuadro=Grupo%20B",
            problema.GetProperty("urlWebActual").GetString());
    }

    [Fact]
    public async Task Devuelve_el_cuadro_con_la_ruta_decodificada_y_normalizada_y_cache_publica()
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/28714/m/Cat50/1/Grupo%20A");
        var json = await respuesta.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(respuesta.Headers.CacheControl?.Public);
        Assert.Equal(new ClaveCuadro(28714, "M", "Cat50", 1, "Grupo A"), falso.ClaveRecibida);
        // Contrato en camelCase, fechas sin hora.
        Assert.Equal("2026-06-26", json.GetProperty("torneo").GetProperty("fechaInicio").GetString());
        Assert.Equal("Grupo A", json.GetProperty("cuadro").GetString());
        Assert.True(json.TryGetProperty("otrosCuadros", out _));
    }

    [Fact]
    public async Task Lista_los_cuadros_del_torneo_y_el_detalle_de_un_enfrentamiento()
    {
        using var app = ConRepositorio(new RepositorioFalso());
        using var cliente = app.CreateClient();

        var lista = await cliente.GetFromJsonAsync<JsonElement>($"{Ruta}/28676");
        var enfrentamiento = await cliente.GetFromJsonAsync<JsonElement>($"{Ruta}/28676/M/ABSOLUTO/1/Final/enfrentamientos/1/1");

        Assert.Equal("equipos", lista.GetProperty("torneo").GetProperty("tipo").GetString());
        Assert.True(lista.GetProperty("cuadros")[0].GetProperty("publicado").GetBoolean());
        Assert.Equal("CENTRAL PADEL A", enfrentamiento.GetProperty("equipo1").GetString());
        Assert.Equal(7, enfrentamiento.GetProperty("partidos")[0].GetProperty("sets")[0].GetProperty("e1").GetInt32());
    }

    // ------------------------------------------------------------------------------------------- Reglas

    [Theory]
    [InlineData(false, 63, 1, null, "eliminatoria", "Cuadro32")]
    [InlineData(false, 7, 1, null, "eliminatoria", "Cuadro4")]
    [InlineData(false, 255, 1, null, "eliminatoria", "Cuadro128")]
    [InlineData(false, 60, 2, null, "eliminatoria", "CuadroPrevia32-4")]
    [InlineData(false, 6, 1, null, "liguilla", "Liguilla3")]
    [InlineData(false, 12, 1, null, "liguilla", "Liguilla4")]
    [InlineData(false, 12, 1, 2, "liguilla", "Liguilla3")]
    [InlineData(false, 12, 2, null, "eliminatoria", "CuadroPrevia8-4")]
    [InlineData(false, 24, 1, null, "liguilla", "Liguilla4")]
    [InlineData(false, 30, 1, null, "liguilla", "Liguilla6")]
    [InlineData(true, 31, 1, null, "eliminatoria", "CuadroEquipos16")]
    [InlineData(true, 6, 1, null, "liguilla", "Liguilla3Equipos")]
    [InlineData(true, 12, 1, null, "liguilla", "Liguilla4Equipos")]
    public void La_plantilla_sale_del_numero_de_filas_como_en_Previo_asp(
        bool equipos, int filas, int avanceMinimo, int? vueltas, string formato, string plantilla)
    {
        // Filas con avance mínimo (distinto de 0) = avanceMinimo; el resto, avances mayores.
        var avances = Enumerable.Range(0, filas).Select(i => i == 0 ? avanceMinimo : avanceMinimo + 1 + (i % 3)).ToArray();

        var resultado = ReglasCuadros.Formato(equipos, avances, vueltas);

        Assert.Equal((formato, plantilla), resultado);
    }

    [Fact]
    public void Con_un_numero_de_filas_desconocido_el_formato_se_deduce_de_la_forma()
    {
        // 4 partidos de liguilla (avance 1–4, huecos 1 y 2), sin campeón.
        var liguilla = new[] { 1, 1, 2, 2, 3, 3, 4, 4 };
        // Cuadro incompleto con campeón (avance 0).
        var eliminatoria = new[] { 2, 2, 1, 1, 0 };

        Assert.Equal(("liguilla", (string?)null), ReglasCuadros.Formato(false, liguilla, null));
        Assert.Equal(("eliminatoria", (string?)null), ReglasCuadros.Formato(false, eliminatoria, null));
    }

    [Theory]
    [InlineData(1, "VerPrimera", "N", false)]
    [InlineData(1, "VerPrimera", "S", true)]
    [InlineData(1, "VerPrimera", null, true)]
    [InlineData(21, "VerPrevia", "N ", false)]
    [InlineData(31, "VerPrePrevia", "n", false)]
    [InlineData(10, "VerDecima", "S", true)]
    [InlineData(15, "VerPrimera", "N", true)]
    public void Solo_oculta_el_cuadro_la_columna_Ver_del_nivel_a_N(int nivel, string columna, string? valor, bool publicado)
        => Assert.Equal(publicado, ReglasCuadros.Publicado(nivel, true, c => c == columna ? valor : "N"));

    [Fact]
    public void Sin_fila_de_desglose_el_cuadro_no_esta_publicado()
        => Assert.False(ReglasCuadros.Publicado(1, false, _ => "S"));

    [Theory]
    [InlineData(1, "E", null, null, "exento", "EXENTO", null)]
    [InlineData(1, "E", "A ", null, "previa", "PREVIA - A", "A")]
    [InlineData(21, null, "B", null, "previa", "PRE-PREVIA - B", "B")]
    [InlineData(1, "E", "C", "S", "lucky", "LUCKY LOSER", null)]
    [InlineData(1, null, " ", "N", null, null, null)]
    public void Los_huecos_sin_pareja_siguen_el_orden_del_asp(
        int nivel, string? exento, string? letra, string? lucky, string? tipo, string? nombre, string? letraEsperada)
    {
        var hueco = ReglasCuadros.Hueco(nivel, exento, letra, lucky);

        Assert.Equal(tipo, hueco?.Tipo);
        Assert.Equal(nombre, hueco?.Nombre);
        Assert.Equal(letraEsperada, hueco?.Letra);
    }

    [Theory]
    [InlineData("M", "M")]
    [InlineData(" x ", "X")]
    [InlineData("Masculino", null)]
    [InlineData("", null)]
    public void El_genero_se_normaliza(string valor, string? esperado) => Assert.Equal(esperado, ReglasCuadros.Genero(valor));

    [Theory]
    [InlineData("Grupo A", "Grupo A")]
    [InlineData(" Cat50 ", "Cat50")]
    [InlineData("SUB-23", "SUB-23")]
    [InlineData("3y4", "3y4")]
    [InlineData("A'B", null)]
    [InlineData("-Final", null)]
    [InlineData("<script>", null)]
    public void Categoria_y_cuadro_solo_admiten_texto_seguro(string valor, string? esperado)
        => Assert.Equal(esperado, ReglasCuadros.TextoRuta(valor));

    [Fact]
    public void Los_cuadros_se_ordenan_por_genero_categoria_nivel_y_final_primero()
    {
        var ordenados = ReglasCuadros.Ordenar(
        [
            new("X", "ABSOLUTO", 1, "Final", true),
            new("M", "Cat50", 1, "Grupo A", true),
            new("M", "Cat50", 1, "Consola", true),
            new("F", "ABSOLUTO", 1, "Final", true),
            new("M", "Cat50", 21, "Final", true),
            new("M", "Cat50", 1, "Final", true),
            new("M", "Cat45", 1, "Final", true),
        ]);

        Assert.Equal(
            ["M Cat45 1 Final", "M Cat50 1 Final", "M Cat50 1 Consola", "M Cat50 1 Grupo A", "M Cat50 21 Final", "F ABSOLUTO 1 Final", "X ABSOLUTO 1 Final"],
            ordenados.Select(c => $"{c.Genero} {c.Categoria} {c.Nivel} {c.Cuadro}"));
    }

    // ------------------------------------------------------------------------------------------- Composición

    [Fact]
    public void Compone_un_cuadro_de_parejas_con_nombres_cabezas_de_serie_resultados_y_horarios()
    {
        var clave = new ClaveCuadro(28714, "M", "Cat50", 1, "Final");
        var filas = new[]
        {
            Hueco(2, 1, 10, cds: "1"), Hueco(2, 2, 0, exento: "E"), Hueco(2, 3, 0, letra: "A"), Hueco(2, 4, 11, cds: "2", lucky: "S"),
            Hueco(1, 1, 10), Hueco(1, 2, 0), Hueco(0, 1, 10),
        };
        var jugadores = new[]
        {
            Jugador(10, 2, "B. SEGUNDO", 100), Jugador(10, 1, "A.  PRIMERO", 200, "S"),
            Jugador(11, 1, "C. TERCERO", 50), Jugador(11, 2, "D. CUARTO", 25),
        };
        var resultados = new[] { new CuadrosRepository.FilaResultado { Avance = 2, Posicion = 1, Resultado = " 6/4-6/2 " } };
        var horarios = new[]
        {
            new CuadrosRepository.FilaHorario { Avance = 2, Posicion = 2, Dia = new DateTime(2026, 6, 26, 10, 0, 0), Lugar = "Pista 3 ", Vs = "S" },
            new CuadrosRepository.FilaHorario { Avance = 1, Posicion = 1, Dia = new DateTime(2026, 6, 27, 18, 0, 0), Visible = "N" },
        };

        var dto = CuadrosRepository.Componer(Torneo28714, clave, false, Desglose("S"), [], filas, jugadores, [], resultados, horarios, [], []);

        Assert.True(dto.Publicado);
        Assert.Equal(("eliminatoria", "Cuadro4"), (dto.Formato, dto.Plantilla));
        Assert.Equal("<p>Nota</p>", dto.Notas);
        Assert.Equal(6, dto.Participantes.Count); // los huecos vacíos no se devuelven
        var primero = dto.Participantes[0];
        Assert.Equal(("pareja", 10, "A. PRIMERO - B. SEGUNDO", 1), (primero.Tipo, primero.Id!.Value, primero.Nombre, primero.CabezaSerie!.Value));
        Assert.Equal(["A. PRIMERO", "B. SEGUNDO"], primero.Jugadores!);
        Assert.Equal(("exento", "EXENTO"), (dto.Participantes[1].Tipo, dto.Participantes[1].Nombre));
        Assert.Equal(("previa", "PREVIA - A", "A"), (dto.Participantes[2].Tipo, dto.Participantes[2].Nombre, dto.Participantes[2].Letra));
        Assert.True(dto.Participantes[3].LuckyLoser);
        // La cabeza de serie solo se pinta en la primera ronda.
        Assert.Null(dto.Participantes[4].CabezaSerie);
        Assert.Equal((0, 1), (dto.Participantes[5].Avance, dto.Participantes[5].Posicion));

        Assert.Equal(2, dto.Partidos.Count);
        Assert.Equal("6/4-6/2", dto.Partidos[0].Resultado);
        Assert.Equal(new HorarioPartidoCuadroDto(new DateTime(2026, 6, 26, 10, 0, 0), "Pista 3", null, true), dto.Partidos[1].Horario);
        Assert.False(dto.Partidos[1].Enfrentamiento);

        Assert.Equal(
            [new CabezaSerieCuadroDto(1, "A. PRIMERO/B. SEGUNDO", 300, true), new CabezaSerieCuadroDto(2, "C. TERCERO/D. CUARTO", 75, false)],
            dto.CabezasSerie);
        Assert.Empty(dto.Clasificacion);
    }

    [Fact]
    public void Un_cuadro_no_publicado_llega_con_las_listas_vacias()
    {
        var clave = new ClaveCuadro(28714, "M", "Cat50", 1, "Final");
        var otros = new[] { new CuadroDisponibleDto("M", "Cat50", 1, "Final", false) };

        var dto = CuadrosRepository.Componer(Torneo28714, clave, false, Desglose("N"), otros,
            [Hueco(1, 1, 10), Hueco(1, 2, 11), Hueco(0, 1, 10)], [Jugador(10, 1, "A", 1)], [], [], [], [], []);

        Assert.False(dto.Publicado);
        Assert.Empty(dto.Participantes);
        Assert.Empty(dto.Partidos);
        Assert.Empty(dto.CabezasSerie);
        Assert.Single(dto.OtrosCuadros);
    }

    [Fact]
    public void Compone_una_liguilla_de_equipos_con_clasificacion_y_enfrentamientos()
    {
        var clave = new ClaveCuadro(28676, "M", "ABSOLUTO", 1, "Grupo A");
        var filas = new[] { Hueco(1, 1, 3), Hueco(1, 2, 1), Hueco(2, 1, 2), Hueco(2, 2, 3), Hueco(3, 1, 1), Hueco(3, 2, 2) };
        var equipos = new[]
        {
            new CuadrosRepository.FilaEquipo { NumEquipo = 1, Equipo = "UD BAGATAZO 2008", Puntos = 101587m },
            new CuadrosRepository.FilaEquipo { NumEquipo = 2, Equipo = "CENTRAL  PADEL A", Puntos = 90000m },
            new CuadrosRepository.FilaEquipo { NumEquipo = 3, Equipo = "BAHIA MAR A", Puntos = 80000.5m },
        };
        var liguilla = new[]
        {
            new CuadrosRepository.FilaLiguilla { Posicion = 1, NumPareja = 1, Pareja = null, Puntos = 4, Eg = 2, Ep = 0 },
            new CuadrosRepository.FilaLiguilla { Posicion = 2, NumPareja = 2, Pareja = "CENTRAL PADEL A", Puntos = 2 },
        };

        var dto = CuadrosRepository.Componer(Torneo28676, clave, true, Desglose("S"), [], filas, [], equipos,
            [new CuadrosRepository.FilaResultado { Avance = 1, Posicion = 1, Resultado = "2/1" }], [],
            [new CuadrosRepository.FilaClave { Avance = 1, Posicion = 1 }], liguilla);

        Assert.Equal(("liguilla", "Liguilla3Equipos"), (dto.Formato, dto.Plantilla));
        Assert.All(dto.Participantes, p => Assert.Equal("equipo", p.Tipo));
        Assert.All(dto.Participantes, p => Assert.Null(p.Jugadores));
        Assert.Equal("CENTRAL PADEL A", dto.Participantes.Single(p => p.Id == 2 && p.Avance == 3).Nombre);
        Assert.True(dto.Partidos.Single().Enfrentamiento);
        // En las liguillas, "C.S." = número de equipo (como el ASP), con sus puntos.
        Assert.Equal([1, 2, 3], dto.CabezasSerie.Select(c => c.CabezaSerie));
        Assert.Equal(80001, dto.CabezasSerie[2].Puntos);
        Assert.Equal("UD BAGATAZO 2008", dto.Clasificacion[0].Nombre); // Pareja vacía → nombre del participante
        Assert.Equal((2, 0), (dto.Clasificacion[0].EnfrentamientosGanados!.Value, dto.Clasificacion[0].EnfrentamientosPerdidos!.Value));
    }

    [Fact]
    public void Compone_el_enfrentamiento_sin_el_tercer_set_a_cero()
    {
        var partidos = new[]
        {
            new CuadrosRepository.FilaPartidoEquipos
            {
                Partido = 1, Equipo1 = "CENTRAL PADEL A ", Equipo2 = "UD BAGATAZO 2008", Jugador11 = "MARCO REMOLINA PABLO",
                Jugador12 = "ALEX BLANCO SOLLET", Jugador21 = "CARLOS EUGENIO BARRERA", Jugador22 = null,
                Set1E1 = 7, Set1E2 = 6, Set2E1 = 6, Set2E2 = 2, Set3E1 = 0, Set3E2 = 0, Obs1 = " ",
            },
            new CuadrosRepository.FilaPartidoEquipos { Partido = 2, Set1E1 = 6, Set1E2 = 3, Set2E1 = 2, Set2E2 = 6, Set3E1 = 1, Set3E2 = 0, Obs1 = "Abandono" },
        };

        var dto = CuadrosRepository.Enfrentamiento(partidos)!;

        Assert.Equal(("CENTRAL PADEL A", "UD BAGATAZO 2008"), (dto.Equipo1, dto.Equipo2));
        Assert.Equal(2, dto.Partidos[0].Sets.Count);
        Assert.Equal(["CARLOS EUGENIO BARRERA"], dto.Partidos[0].Pareja2);
        Assert.Null(dto.Partidos[0].Obs1);
        Assert.Equal(3, dto.Partidos[1].Sets.Count);
        Assert.Equal("Abandono", dto.Partidos[1].Obs1);
        Assert.Null(CuadrosRepository.Enfrentamiento([]));
    }

    [Fact]
    public void Los_cuadros_disponibles_se_recortan_sin_repetir_y_con_su_publicacion()
    {
        var cuadros = new[]
        {
            new CuadrosRepository.FilaCuadroTorneo { Genero = "M", Categoria = "Cat50   ", Nivel = 1, Cuadro = "Consola" },
            new CuadrosRepository.FilaCuadroTorneo { Genero = "M", Categoria = "Cat50", Nivel = 1, Cuadro = "Final" },
            new CuadrosRepository.FilaCuadroTorneo { Genero = "m", Categoria = "CAT50", Nivel = 1, Cuadro = "Final " },
            new CuadrosRepository.FilaCuadroTorneo { Genero = "F", Categoria = "Cat50", Nivel = 2, Cuadro = "Final" },
        };
        var desglose = new[]
        {
            new CuadrosRepository.FilaDesglose { Genero = "M", Tipo = "CAT50 ", VerPrimera = "S" },
            new CuadrosRepository.FilaDesglose { Genero = "F", Tipo = "Cat50", VerSegunda = "N" },
        };

        var lista = CuadrosRepository.Disponibles(cuadros, desglose);

        Assert.Equal(
        [
            new CuadroDisponibleDto("M", "Cat50", 1, "Final", true),
            new CuadroDisponibleDto("M", "Cat50", 1, "Consola", true),
            new CuadroDisponibleDto("F", "Cat50", 2, "Final", false),
        ], lista);
    }

    // ------------------------------------------------------------------------------------------- Utilidades

    private static CuadrosRepository.FilaHueco Hueco(int avance, int posicion, int pareja, string? cds = null,
        string? exento = null, string? letra = null, string? lucky = null)
        => new() { Avance = avance, Posicion = posicion, Pareja = pareja, Cds = cds, Exento = exento, Letra = letra, Lucky = lucky };

    private static CuadrosRepository.FilaJugador Jugador(int pareja, int orden, string nombre, decimal puntos, string? wildcard = null)
        => new() { NumPareja = pareja, OrdenPareja = orden, Jugador = nombre, Puntos = puntos, Wildcard = wildcard };

    private static CuadrosRepository.FilaDesglose Desglose(string verPrimera)
        => new() { Genero = "M", Tipo = "Cat50", TextoCuadros = " <p>Nota</p> ", VerPrimera = verPrimera };

    private WebApplicationFactory<Program> ConRepositorio(ICuadrosRepository repositorio)
        => factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(repositorio)));

    /// <summary>Conoce el 28714 (parejas) y el 28676 (equipos; sin el grupo B femenino).</summary>
    private sealed class RepositorioFalso : ICuadrosRepository
    {
        public ClaveCuadro? ClaveRecibida { get; private set; }

        private static TorneoCuadroDto? TorneoDe(int id) => id switch
        {
            28714 => Torneo28714,
            28676 => Torneo28676,
            _ => null,
        };

        public Task<ConsultaCuadros<CuadrosTorneoDto>> ListarAsync(int idTorneo, CancellationToken cancellationToken)
        {
            var torneo = TorneoDe(idTorneo);
            return Task.FromResult(new ConsultaCuadros<CuadrosTorneoDto>(torneo,
                torneo is null ? null : new CuadrosTorneoDto(torneo, [new CuadroDisponibleDto("M", "ABSOLUTO", 1, "Final", true)])));
        }

        public Task<ConsultaCuadros<CuadroTorneoDto>> ObtenerAsync(ClaveCuadro clave, CancellationToken cancellationToken)
        {
            ClaveRecibida = clave;
            var torneo = TorneoDe(clave.IdTorneo);
            var existe = torneo is not null && !(clave.Genero == "F" && clave.Cuadro == "Grupo B");
            return Task.FromResult(new ConsultaCuadros<CuadroTorneoDto>(torneo, existe
                ? new CuadroTorneoDto(torneo!, clave.Genero, clave.Categoria, clave.Nivel, clave.Cuadro, true, null, "eliminatoria",
                    "Cuadro32", [], [], [], [], [new CuadroDisponibleDto(clave.Genero, clave.Categoria, clave.Nivel, clave.Cuadro, true)])
                : null));
        }

        public Task<ConsultaCuadros<EnfrentamientoEquiposDto>> ObtenerEnfrentamientoAsync(
            ClaveCuadro clave, int avance, int posicion, CancellationToken cancellationToken)
        {
            var torneo = TorneoDe(clave.IdTorneo);
            return Task.FromResult(new ConsultaCuadros<EnfrentamientoEquiposDto>(torneo, torneo?.Tipo == "equipos"
                ? new EnfrentamientoEquiposDto("CENTRAL PADEL A", "UD BAGATAZO 2008",
                    [new PartidoEnfrentamientoDto(1, ["MARCO REMOLINA PABLO", "ALEX BLANCO SOLLET"], ["CARLOS EUGENIO BARRERA", "JOEL JESUS FERNANDEZ MUÑOZ"],
                        [new SetEnfrentamientoDto(7, 6), new SetEnfrentamientoDto(6, 2)], null, null)])
                : null));
        }
    }
}
