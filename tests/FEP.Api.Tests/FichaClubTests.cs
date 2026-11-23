using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FEP.Api.Contracts.Clubes;
using FEP.Api.Data.Repositories.Clubes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace FEP.Api.Tests;

/// <summary>
/// GET /api/v1/clubes/{id} (migrado desde Club.asp). Nunca toca SQL Server: la validación corta antes de llegar al
/// repositorio y el resto usa un repositorio falso. Las reglas del ASP (categoría, licencia, nombre) se prueban aparte.
/// </summary>
public sealed class FichaClubTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("abc")]
    [InlineData("12a4")]
    [InlineData("12345678901")] // 11 cifras
    [InlineData("1%27%20OR%20%271%27%3D%271")] // 1' OR '1'='1 (el ASP lo concatenaba en el SQL)
    public async Task Un_id_no_numerico_devuelve_400_sin_llegar_al_repositorio(string id)
    {
        var falso = new RepositorioFalso(null);
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync($"/api/v1/clubes/{id}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Null(falso.IdRecibido);
    }

    [Fact]
    public async Task Un_club_que_no_existe_devuelve_404_con_enlace_a_la_web_actual()
    {
        using var app = ConRepositorio(new RepositorioFalso(null));
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync("/api/v1/clubes/9999999");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("club-no-encontrado", problema.GetProperty("motivo").GetString());
        Assert.EndsWith("/Club.asp?id=9999999", problema.GetProperty("urlWebActual").GetString());
    }

    [Fact]
    public async Task Pasa_el_id_con_sus_ceros_y_el_anio_en_curso_y_devuelve_la_ficha()
    {
        var ficha = new ClubDetalleDto("0118003", "Club de Prueba", 18, "GRANADA", 6, "ANDALUZA", "PRESIDENTA",
            "C/ Mayor, 1", "18001", "Granada", ["958-000000"], "club@ejemplo.es", "https://club.ejemplo.es", 2026,
            [new LicenciaClubDto("000123", "APELLIDO UNO, NOMBRE", "F", "SEN")], [],
            [new TorneoClubDto(30120, 6, 2025, 7, "TORNEO", "menores", null)]);
        var falso = new RepositorioFalso(ficha);
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        var datos = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/clubes/0118003");

        Assert.Equal("0118003", falso.IdRecibido);
        Assert.Equal(2026, falso.AnioRecibido);
        Assert.Equal("0118003", datos.GetProperty("id").GetString());
        Assert.Equal("ANDALUZA", datos.GetProperty("federacion").GetString());
        var licencia = datos.GetProperty("licenciasAutonomicas")[0];
        Assert.Equal("000123", licencia.GetProperty("licencia").GetString());
        Assert.False(licencia.TryGetProperty("fechaNac", out _));
        Assert.Equal("menores", datos.GetProperty("torneos")[0].GetProperty("modalidad").GetString());
    }

    [Fact]
    public async Task La_ruta_de_filtros_no_se_confunde_con_la_ficha()
    {
        var falso = new RepositorioFalso(null);
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync("/api/v1/clubes/filtros");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Null(falso.IdRecibido);
    }

    [Theory]
    [InlineData(2026, "BEN")]   // edad 0: dato no válido, BEN como en Club.asp
    [InlineData(2016, "BEN")]   // 10
    [InlineData(2015, "ALE")]   // 11
    [InlineData(2013, "INF")]   // 13
    [InlineData(2011, "CAD")]   // 15
    [InlineData(2008, "JUN")]   // 18
    [InlineData(2003, "SUB-23")] // 23
    [InlineData(2002, "SEN")]   // 24
    [InlineData(1991, "CAT-35")] // 35
    [InlineData(1982, "CAT-40")] // 44
    [InlineData(1977, "CAT-45")] // 49
    [InlineData(1976, "CAT-50")] // 50
    [InlineData(1960, "CAT-55")] // 66
    public void Categoria_por_edad_como_Club_asp(int nacimiento, string esperada)
        => Assert.Equal(esperada, ReglasFichaClub.CategoriaAbreviada(nacimiento, 2026));

    [Fact]
    public void Sin_fecha_de_nacimiento_no_hay_categoria()
        => Assert.Null(ReglasFichaClub.CategoriaAbreviada(null, 2026));

    [Theory]
    [InlineData("95625", "095625")]
    [InlineData("7", "000007")]
    [InlineData(" 135899 ", "135899")]
    [InlineData("1234567", "1234567")]
    public void Licencia_con_ceros_hasta_6_cifras(string licencia, string esperada)
        => Assert.Equal(esperada, ReglasFichaClub.NumeroLicencia(licencia));

    [Theory]
    [InlineData("ABAITUA", "AMEZAGA", "SERAPIO", "ABAITUA AMEZAGA, SERAPIO")]
    [InlineData("GARCÍA ", null, " ANA", "GARCÍA, ANA")]
    [InlineData("GARCÍA", "", null, "GARCÍA")]
    public void Nombre_del_jugador(string? a1, string? a2, string? nombre, string esperado)
        => Assert.Equal(esperado, ReglasFichaClub.Jugador(a1, a2, nombre));

    [Theory]
    [InlineData("ABSOLUTO ", "absoluto")]
    [InlineData("menores", "menores")]
    [InlineData("VETERANOS", "veteranos")]
    [InlineData("OTRA", null)]
    [InlineData(null, null)]
    public void Modalidad_del_torneo(string? modalidad, string? esperada)
        => Assert.Equal(esperada, ReglasFichaClub.Modalidad(modalidad));

    private WebApplicationFactory<Program> ConRepositorio(IFichaClubRepository repositorio)
        => factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddSingleton(repositorio);
            s.AddSingleton<IClubesRepository>(new ClubesFalso());
            s.AddSingleton<TimeProvider>(new RelojFijo(new DateTimeOffset(2026, 11, 18, 10, 0, 0, TimeSpan.Zero)));
        }));

    private sealed class RepositorioFalso(ClubDetalleDto? ficha) : IFichaClubRepository
    {
        public string? IdRecibido { get; private set; }
        public int AnioRecibido { get; private set; }

        public Task<ClubDetalleDto?> ObtenerAsync(string id, int anio, CancellationToken cancellationToken)
        {
            IdRecibido = id;
            AnioRecibido = anio;
            return Task.FromResult(ficha);
        }
    }

    private sealed class ClubesFalso : IClubesRepository
    {
        public Task<ClubesPaginaDto> BuscarAsync(BusquedaClubes busqueda, int anio, CancellationToken cancellationToken)
            => Task.FromResult(new ClubesPaginaDto(anio, null, null, null, 1, 50, 0, []));

        public Task<ClubesFiltrosDto> ObtenerFiltrosAsync(CancellationToken cancellationToken)
            => Task.FromResult(new ClubesFiltrosDto([], []));
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
