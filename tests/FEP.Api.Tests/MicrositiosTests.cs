using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FEP.Api.Contracts.Micrositios;
using FEP.Api.Data.Repositories.Micrositios;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace FEP.Api.Tests;

/// <summary>
/// GET /api/v1/micrositios/{id} y /api/v1/micrositios/{id}/opciones/{idOpcion} (migrados desde
/// Microsite.asp). Las consultas reales necesitan SQL Server: aquí se prueban la validación de ids, el
/// bloqueo de los micrositios privados (cortan antes de tocar la BD), los 404 con un repositorio falso y
/// la normalización de tipos y URLs de archivos.
/// </summary>
public sealed class MicrositiosTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Ruta = "/api/v1/micrositios";

    [Theory]
    [InlineData("0", "id")]
    [InlineData("-7", "id")]
    [InlineData("abc", "id")]
    [InlineData("7/opciones/0", "idOpcion")]
    [InlineData("7/opciones/abc", "idOpcion")]
    [InlineData("0/opciones/19", "id")]
    public async Task Ids_no_validos_devuelven_400_con_ValidationProblemDetails(string ruta, string campo)
    {
        using var cliente = factory.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/{ruta}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problema.GetProperty("status").GetInt32());
        Assert.Contains(problema.GetProperty("errors").EnumerateObject(),
            p => p.Name.Equals(campo, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("25", "https://www.padelfederacion.es/Microsite.asp?Id=25")]
    [InlineData("26/opciones/189", "https://www.padelfederacion.es/Microsite.asp?Id=26&IdOpcion=189")]
    public async Task Los_micrositios_privados_devuelven_403_con_la_url_de_la_web_actual(string ruta, string urlWebActual)
    {
        // Sin repositorio falso ni cadena de conexión: si llegara a la BD respondería 500.
        using var cliente = factory.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/{ruta}");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(403, problema.GetProperty("status").GetInt32());
        Assert.Equal(urlWebActual, problema.GetProperty("urlWebActual").GetString());
    }

    [Theory]
    [InlineData("99")]
    [InlineData("7/opciones/999")]
    public async Task Lo_que_no_existe_o_no_es_visible_devuelve_404_ProblemDetails(string ruta)
    {
        using var app = ConRepositorio(new RepositorioFalso());
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/{ruta}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(404, problema.GetProperty("status").GetInt32());
        Assert.StartsWith("https://www.padelfederacion.es/Microsite.asp?Id=", problema.GetProperty("urlWebActual").GetString());
    }

    [Fact]
    public async Task Devuelve_el_micrositio_con_su_menu_y_cache_publica()
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        using var respuesta = await cliente.GetAsync($"{Ruta}/7");
        var datos = await respuesta.Content.ReadFromJsonAsync<MicrositioDto>();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(respuesta.Headers.CacheControl?.Public);
        Assert.Equal(7, falso.IdRecibido);
        Assert.Equal("Homologaciones", datos!.Titulo);
        Assert.Equal(30, datos.OpcionInicial);
        Assert.Equal([30, 19], datos.Opciones.Select(o => o.Id));
    }

    [Fact]
    public async Task Devuelve_el_contenido_de_la_opcion()
    {
        var falso = new RepositorioFalso();
        using var app = ConRepositorio(falso);
        using var cliente = app.CreateClient();

        var datos = await cliente.GetFromJsonAsync<OpcionMicrositioContenidoDto>($"{Ruta}/7/opciones/19");

        Assert.Equal(7, falso.IdRecibido);
        Assert.Equal(19, falso.IdOpcionRecibida);
        Assert.Equal("Introducción", datos!.Titulo);
        Assert.Equal("normal", datos.Tipo);
        Assert.Empty(datos.Archivos);
    }

    [Theory]
    [InlineData("NORMAL", "normal")]
    [InlineData("NORMAL    ", "normal")]
    [InlineData("FORMULARIO", "formulario")]
    [InlineData(null, "formulario")]
    public void El_tipo_de_opcion_se_normaliza_como_en_el_ASP(string? tipo, string esperado)
        => Assert.Equal(esperado, MicrositiosRepository.TipoOpcion(tipo));

    [Theory]
    [InlineData("pdf", "pdf")]
    [InlineData("PDF ", "pdf")]
    [InlineData("you", "you")]
    [InlineData("--", null)]
    [InlineData("exe", null)]
    [InlineData(null, null)]
    public void El_tipo_de_archivo_solo_admite_los_iconos_de_la_gestion(string? icono, string? esperado)
        => Assert.Equal(esperado, MicrositiosRepository.TipoArchivo(icono));

    [Theory]
    [InlineData("censo inicial.pdf", "https://www.padelfederacion.es/Paginas/Microsites/censo%20inicial.pdf")]
    [InlineData("Acta nº 40 PROCLAMACIÓN PRESIDENTE PÁDEL_.pdf",
        "https://www.padelfederacion.es/Paginas/Microsites/Acta%20n%C2%BA%2040%20PROCLAMACI%C3%93N%20PRESIDENTE%20P%C3%81DEL_.pdf")]
    [InlineData("../web.config", "https://www.padelfederacion.es/Paginas/Microsites/web.config")]
    [InlineData("https://www.youtube.com/watch?v=abc", "https://www.youtube.com/watch?v=abc")]
    [InlineData("www.ibv.org", "https://www.ibv.org/")]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("   ", null)]
    public void La_url_de_los_archivos_es_absoluta_y_codificada(string link, string? esperada)
        => Assert.Equal(esperada, MicrositiosRepository.UrlArchivo(link, new Uri("https://www.padelfederacion.es/")));

    private WebApplicationFactory<Program> ConRepositorio(IMicrositiosRepository repositorio)
        => factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(repositorio)));

    /// <summary>Solo conoce el micrositio 7 (Homologaciones) con las opciones 30 y 19.</summary>
    private sealed class RepositorioFalso : IMicrositiosRepository
    {
        public int? IdRecibido { get; private set; }
        public int? IdOpcionRecibida { get; private set; }

        public Task<MicrositioDto?> ObtenerAsync(int id, CancellationToken cancellationToken)
        {
            IdRecibido = id;
            return Task.FromResult(id != 7 ? null : new MicrositioDto(7, "Homologaciones", null, 30,
            [
                new OpcionMicrositioDto(30, "Relación de Empresas y Materiales Homologados", "normal", 1),
                new OpcionMicrositioDto(19, "Introducción", "normal", 2),
            ]));
        }

        public Task<OpcionMicrositioContenidoDto?> ObtenerOpcionAsync(int id, int idOpcion, CancellationToken cancellationToken)
        {
            (IdRecibido, IdOpcionRecibida) = (id, idOpcion);
            return Task.FromResult(id != 7 || idOpcion is not (30 or 19) ? null : new OpcionMicrositioContenidoDto(
                idOpcion, 7, "Homologaciones", idOpcion == 19 ? "Introducción" : "Relación de Empresas y Materiales Homologados",
                null, "normal", "<p>Texto</p>", []));
        }
    }
}
