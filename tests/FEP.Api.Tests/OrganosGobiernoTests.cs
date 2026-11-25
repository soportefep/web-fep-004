using System.Net;
using System.Net.Http.Json;
using FEP.Api.Contracts.Federacion;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FEP.Api.Tests;

/// <summary>
/// GET /api/v1/federacion/organos-gobierno (migrado desde Organos_Gobierno.asp).
/// No usa SQL Server: el contenido sale de src/FEP.Api/Contenidos/organos-gobierno.json.
/// </summary>
public sealed class OrganosGobiernoTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Ruta = "/api/v1/federacion/organos-gobierno";

    [Fact]
    public async Task Devuelve_los_cuatro_organos_en_el_orden_de_la_pagina()
    {
        using var cliente = factory.CreateClient();

        var datos = await cliente.GetFromJsonAsync<OrganosGobiernoDto>(Ruta);

        Assert.NotNull(datos);
        Assert.Equal(
            ["junta-directiva", "asamblea", "comision-delegada", "ctap"],
            datos.Organos.Select(o => o.Clave));
        Assert.All(datos.Organos, o => Assert.NotEmpty(o.Grupos));
    }

    [Fact]
    public async Task Las_actas_tienen_url_absoluta_y_van_de_la_mas_reciente_a_la_mas_antigua()
    {
        using var cliente = factory.CreateClient();

        var datos = await cliente.GetFromJsonAsync<OrganosGobiernoDto>(Ruta);
        var actas = datos!.Organos.Single(o => o.Clave == "asamblea").Actas;

        Assert.Equal(10, actas.Count);
        Assert.All(actas, a => Assert.StartsWith("https://", a.Url));
        Assert.Equal(actas.OrderByDescending(a => a.Fecha).Select(a => a.Fecha), actas.Select(a => a.Fecha));
        // Los nombres con espacios se codifican (refs/docs/2023 Acta… → 2023%20Acta…)
        Assert.DoesNotContain(actas, a => a.Url!.Contains(' '));
    }

    [Fact]
    public async Task Las_imagenes_de_organigrama_se_devuelven_como_url_absoluta()
    {
        using var cliente = factory.CreateClient();

        var datos = await cliente.GetFromJsonAsync<OrganosGobiernoDto>(Ruta);

        var conImagen = datos!.Organos.Where(o => o.ImagenUrl is not null).ToArray();
        Assert.Equal(["junta-directiva", "ctap"], conImagen.Select(o => o.Clave));
        Assert.All(conImagen, o => Assert.True(Uri.IsWellFormedUriString(o.ImagenUrl, UriKind.Absolute)));
    }

    [Fact]
    public async Task Los_vocales_de_la_Junta_dependen_de_una_vicepresidencia_existente()
    {
        using var cliente = factory.CreateClient();

        var datos = await cliente.GetFromJsonAsync<OrganosGobiernoDto>(Ruta);
        var junta = datos!.Organos.Single(o => o.Clave == "junta-directiva");
        var cargos = junta.Grupos.Single(g => g.Clave == "presidencia").Miembros.Select(m => m.Cargo).ToHashSet();
        var vocales = junta.Grupos.Single(g => g.Clave == "vocales").Miembros;

        // El organigrama de la web cuelga cada vocal de la rama cuyo cargo coincide con "depende"
        Assert.All(vocales, v => Assert.Contains(v.Depende, cargos));
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
