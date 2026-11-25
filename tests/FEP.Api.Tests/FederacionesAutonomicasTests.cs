using System.Net;
using System.Net.Http.Json;
using FEP.Api.Contracts.Federacion;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FEP.Api.Tests;

/// <summary>
/// GET /api/v1/federacion/federaciones-autonomicas (migrado desde Federaciones.asp).
/// No usa SQL Server: el contenido sale de src/FEP.Api/Contenidos/federaciones-autonomicas.json.
/// </summary>
public sealed class FederacionesAutonomicasTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Ruta = "/api/v1/federacion/federaciones-autonomicas";

    [Fact]
    public async Task Devuelve_las_19_autonomicas_y_la_FEP_al_final()
    {
        using var cliente = factory.CreateClient();

        var datos = await cliente.GetFromJsonAsync<FederacionesAutonomicasDto>(Ruta);

        Assert.NotNull(datos);
        Assert.Equal(20, datos.Federaciones.Count);
        Assert.Equal(19, datos.Federaciones.Count(f => !f.Nacional));
        var ultima = datos.Federaciones[^1];
        Assert.True(ultima.Nacional);
        Assert.Equal(0, ultima.Id);
        // Los Id son los de SFEP_Federaciones (enlace a la ficha y filtro de clubes): sin repetidos.
        Assert.Equal(datos.Federaciones.Count, datos.Federaciones.Select(f => f.Id).Distinct().Count());
        Assert.Equal(6, datos.Federaciones[0].Id); // Andalucía, primera como en el ASP
    }

    [Fact]
    public async Task Las_banderas_y_las_webs_son_urls_absolutas()
    {
        using var cliente = factory.CreateClient();

        var datos = await cliente.GetFromJsonAsync<FederacionesAutonomicasDto>(Ruta);

        Assert.All(datos!.Federaciones, f =>
        {
            Assert.NotNull(f.ImagenUrl);
            Assert.StartsWith("https://", f.ImagenUrl);
            Assert.EndsWith(f.Imagen!, f.ImagenUrl);
            if (f.Web is not null)
            {
                Assert.True(Uri.IsWellFormedUriString(f.Web, UriKind.Absolute));
                Assert.DoesNotContain("https://https://", f.Web);
            }
        });
    }

    [Fact]
    public async Task La_reserva_de_zoom_incluye_el_calendario_de_google()
    {
        using var cliente = factory.CreateClient();

        var datos = await cliente.GetFromJsonAsync<FederacionesAutonomicasDto>(Ruta);
        var zoom = datos!.ReservaZoom;

        Assert.NotNull(zoom);
        Assert.Equal("zoom@padelfederacion.es", zoom.Email);
        Assert.NotEmpty(zoom.Parrafos);
        Assert.StartsWith("https://", zoom.ImagenUrl);
        Assert.NotNull(zoom.Calendario);
        Assert.StartsWith("https://calendar.google.com/calendar/embed?", zoom.Calendario.Url);
        Assert.False(string.IsNullOrWhiteSpace(zoom.Calendario.Titulo));
    }

    [Fact]
    public async Task Responde_con_cache_publica()
    {
        using var cliente = factory.CreateClient();

        using var respuesta = await cliente.GetAsync(Ruta);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(respuesta.Headers.CacheControl?.Public);
    }
}
