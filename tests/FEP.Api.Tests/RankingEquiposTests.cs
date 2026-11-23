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
/// GET /api/v1/competicion/rankings/equipos-absolutos (migrado desde Ranking_Equipos_Absolutos.asp). Los datos salen
/// de SQL Server: aquí se prueban la validación, la ruta (que no la capture /rankings/{clave}), la composición de la
/// respuesta con un repositorio falso (URLs, federación con el territorio de Contenidos/federaciones-autonomicas.json)
/// y las reglas de <see cref="ReglasRankingEquipos"/>.
/// </summary>
public sealed class RankingEquiposTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Ruta = "/api/v1/competicion/rankings/equipos-absolutos";

    // Filas reales de la web pública (2024, 1.ª categoría masculina) y una de 2.ª femenina, desordenadas.
    private static readonly FilaRankingEquipo[] Filas =
    [
        new(2024, 1, "M", 2, "REIAL CLUB DE TENNIS BARCELONA 1899", "0908013", "BARCELONA", 2, "CATALANA"),
        new(2024, 1, "M ", 1, "REAL CLUB DE POLO BARCELONA", "0908009", "BARCELONA", 2, "CATALANA"),
        new(2024, 1, "M", 7, "\tC.D. LA RED XXI PADEL CENTER", "0141038", "SEVILLA", 6, "ANDALUZA"),
        new(2024, 2, "F", 1, "CLUB SIN FEDERACION", "0000001", null, null, null),
        new(2023, 1, "F", 1, "CLUB DE TENIS CHAMARTIN", "1228015", "MADRID", 1, "MADRILEÑA"),
    ];

    // --- Validación y ruta -----------------------------------------------------------------------------

    [Theory]
    [InlineData("anio=1900")]
    [InlineData("anio=2101")]
    [InlineData("anio=abc")]
    [InlineData("anio=2024%20OR%201%3D1")]
    public async Task Un_anio_no_valido_devuelve_400_con_ValidationProblemDetails(string query)
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(problema.GetProperty("errors").EnumerateObject(),
            p => p.Name.Equals("anio", StringComparison.OrdinalIgnoreCase));
        Assert.False(falso.Llamado);
    }

    [Fact]
    public async Task La_ruta_literal_tiene_prioridad_sobre_rankings_clave()
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync(Ruta);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(respuesta.Headers.CacheControl?.Public);
        Assert.True(falso.Llamado);
        Assert.Null(falso.Anio);
    }

    [Fact]
    public async Task El_anio_llega_al_repositorio_y_se_devuelve_como_filtro_aplicado()
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        var datos = await cliente.GetFromJsonAsync<JsonElement>($"{Ruta}?anio=2023");

        Assert.Equal(2023, falso.Anio);
        Assert.Equal(2023, datos.GetProperty("anio").GetInt32());
    }

    // --- Forma del JSON ----------------------------------------------------------------------------------

    [Fact]
    public async Task Agrupa_por_temporada_y_lista_con_urls_y_federacion()
    {
        using var app = ConRepositorio(new RepositorioFalso());
        using var cliente = app.CreateClient();

        var datos = await cliente.GetFromJsonAsync<JsonElement>(Ruta);

        Assert.Equal("equipos-absolutos", datos.GetProperty("clave").GetString());
        Assert.Equal(JsonValueKind.Null, datos.GetProperty("anio").ValueKind);
        Assert.Equal([2024, 2023], datos.GetProperty("anios").EnumerateArray().Select(a => a.GetInt32()));

        var temporadas = datos.GetProperty("temporadas");
        Assert.Equal(2024, temporadas[0].GetProperty("anio").GetInt32());
        var listas = temporadas[0].GetProperty("listas");
        Assert.Equal(["1.ª categoría masculina", "2.ª categoría femenina"],
            listas.EnumerateArray().Select(l => l.GetProperty("titulo").GetString()));

        var primeros = listas[0].GetProperty("puestos");
        Assert.Equal([1, 2, 7], primeros.EnumerateArray().Select(p => p.GetProperty("puesto").GetInt32()));
        var campeon = primeros[0];
        Assert.Equal("REAL CLUB DE POLO BARCELONA", campeon.GetProperty("equipo").GetString());
        Assert.Equal("0908009", campeon.GetProperty("club").GetString());
        Assert.Equal("https://www.padelfederacion.es/Club.asp?id=0908009", campeon.GetProperty("clubUrl").GetString());
        Assert.Equal("images/0908009.jpg", campeon.GetProperty("escudo").GetString());
        Assert.Equal("https://www.padelfederacion.es/refs/images/0908009.jpg", campeon.GetProperty("escudoUrl").GetString());
        Assert.Equal("BARCELONA", campeon.GetProperty("provincia").GetString());

        var federacion = campeon.GetProperty("federacion");
        Assert.Equal(2, federacion.GetProperty("id").GetInt32());
        Assert.Equal("Cataluña", federacion.GetProperty("territorio").GetString());
        Assert.Equal("CATALANA", federacion.GetProperty("abreviatura").GetString());
        Assert.Equal("images/2.jpg", federacion.GetProperty("bandera").GetString());
        Assert.Equal("https://www.padelfederacion.es/refs/images/2.jpg", federacion.GetProperty("banderaUrl").GetString());

        // El tabulador inicial del nombre (dato real de 2024) se elimina.
        Assert.Equal("C.D. LA RED XXI PADEL CENTER", primeros[2].GetProperty("equipo").GetString());

        // Club sin provincia ni federación: el ASP lo perdía con el INNER JOIN; ahora sale sin bandera.
        var sinFederacion = listas[1].GetProperty("puestos")[0];
        Assert.Equal(JsonValueKind.Null, sinFederacion.GetProperty("federacion").ValueKind);
        Assert.Equal(JsonValueKind.Null, sinFederacion.GetProperty("provincia").ValueKind);
    }

    // --- Reglas ------------------------------------------------------------------------------------------

    [Fact]
    public void Las_listas_siguen_el_orden_del_ASP_y_descartan_lo_que_no_pintaba()
    {
        FilaRankingEquipo[] filas =
        [
            new(2020, 2, "F", 1, "D", "4", null, null, null),
            new(2020, 2, "M", 1, "C", "3", null, null, null),
            new(2020, 1, "F", 1, "B", "2", null, null, null),
            new(2020, 1, "M", 1, "A", "1", null, null, null),
            new(2020, 3, "M", 1, "Tercera", "5", null, null, null),  // el ASP solo pintaba 1.ª y 2.ª
            new(2020, 1, "X", 1, "Mixto", "6", null, null, null),    // ni mixtos
        ];

        var temporadas = ReglasRankingEquipos.Temporadas(filas, f => new PuestoEquipoDto(f.Puesto, f.Equipo!, f.Club, null, null, null, null, null));

        var temporada = Assert.Single(temporadas);
        Assert.Equal(
            ["1.ª categoría masculina", "1.ª categoría femenina", "2.ª categoría masculina", "2.ª categoría femenina"],
            temporada.Listas.Select(l => l.Titulo));
        Assert.Equal(["A", "B", "C", "D"], temporada.Listas.Select(l => l.Puestos.Single().Equipo));
    }

    [Fact]
    public void Los_puestos_compartidos_se_ordenan_por_nombre()
    {
        FilaRankingEquipo[] filas =
        [
            new(2024, 1, "M", 5, "SECCION DEPORTIVA CLUB RIO GRANDE SIPADEL", "0141008", null, null, null),
            new(2024, 1, "M", 5, "CLUB DE TENIS CHAMARTIN", "1228015", null, null, null),
        ];

        var lista = ReglasRankingEquipos.Temporadas(filas, f => new PuestoEquipoDto(f.Puesto, f.Equipo!, f.Club, null, null, null, null, null))
            .Single().Listas.Single();

        Assert.Equal(["CLUB DE TENIS CHAMARTIN", "SECCION DEPORTIVA CLUB RIO GRANDE SIPADEL"], lista.Puestos.Select(p => p.Equipo));
    }

    [Theory]
    [InlineData("\tC.D. LA RED XXI  PADEL CENTER ", "C.D. LA RED XXI PADEL CENTER")]
    [InlineData(null, "")]
    public void El_nombre_del_equipo_se_normaliza(string? equipo, string esperado)
        => Assert.Equal(esperado, ReglasRankingEquipos.NombreEquipo(equipo));

    [Fact]
    public void La_ficha_del_club_conserva_los_ceros_y_se_codifica()
    {
        Assert.Equal("Club.asp?id=0908009", ReglasRankingEquipos.RutaClub("0908009"));
        Assert.Equal("Club.asp?id=1%262", ReglasRankingEquipos.RutaClub("1&2"));
    }

    private WebApplicationFactory<Program> ConRepositorio(IRankingEquiposRepository repositorio)
        => factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(repositorio)));

    /// <summary>Guarda el año recibido y devuelve <see cref="Filas"/> (filtradas por año, como la BD).</summary>
    private sealed class RepositorioFalso : IRankingEquiposRepository
    {
        public bool Llamado { get; private set; }
        public int? Anio { get; private set; }

        public Task<DatosRankingEquipos> ObtenerAsync(int? anio, CancellationToken cancellationToken)
        {
            Llamado = true;
            Anio = anio;
            return Task.FromResult(new DatosRankingEquipos(
                [2024, 2023],
                Filas.Where(f => anio is null || f.Anio == anio).ToArray()));
        }
    }
}
