using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FEP.Api.Tests;

/// <summary>
/// Pruebas de humo: la API arranca en memoria (TestServer) sin base de datos.
/// WebApplicationFactory usa el entorno "Development", donde se publica /openapi/v1.json.
/// No necesitan cadena de conexión: ningún test de esta clase toca SQL Server.
/// </summary>
public sealed class HumoTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task La_api_arranca_y_publica_el_documento_openapi()
    {
        using var cliente = factory.CreateClient();

        using var respuesta = await cliente.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("\"openapi\"", cuerpo);
    }

    [Fact]
    public async Task Una_ruta_inexistente_devuelve_404()
    {
        using var cliente = factory.CreateClient();

        using var respuesta = await cliente.GetAsync("/api/v1/ruta-que-no-existe");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
