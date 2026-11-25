using System.Net;
using System.Net.Http.Json;
using FEP.Api.Contracts.Federacion;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FEP.Api.Tests;

/// <summary>
/// GET /api/v1/federacion/equipo (migrado desde Equipo_FEP.asp).
/// No usa SQL Server: el contenido sale de src/FEP.Api/Contenidos/equipo-fep.json.
/// </summary>
public sealed class EquipoFepTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Ruta = "/api/v1/federacion/equipo";

    [Fact]
    public async Task Devuelve_las_dos_areas_en_el_orden_de_la_pagina()
    {
        using var cliente = factory.CreateClient();

        var datos = await cliente.GetFromJsonAsync<EquipoFepDto>(Ruta);

        Assert.NotNull(datos);
        Assert.Equal(["organigrama", "menores"], datos.Areas.Select(a => a.Clave));
        Assert.All(datos.Areas, a => Assert.NotEmpty(a.Grupos));
        Assert.All(datos.Areas.SelectMany(a => a.Grupos), g => Assert.NotEmpty(g.Miembros));
    }

    [Fact]
    public async Task La_junta_directiva_del_organigrama_es_la_misma_que_la_de_organos_de_gobierno()
    {
        using var cliente = factory.CreateClient();

        var equipo = await cliente.GetFromJsonAsync<EquipoFepDto>(Ruta);
        var organos = await cliente.GetFromJsonAsync<OrganosGobiernoDto>("/api/v1/federacion/organos-gobierno");

        var esperada = organos!.Organos.Single(o => o.Clave == "junta-directiva")
            .Grupos.SelectMany(g => g.Miembros).Select(m => m.Nombre).ToArray();
        var junta = equipo!.Areas.Single(a => a.Clave == "organigrama")
            .Grupos.Single(g => g.Clave == "junta-directiva").Miembros.Select(m => m.Nombre).ToArray();

        Assert.Equal(18, esperada.Length);
        Assert.Equal(esperada, junta);
    }

    [Fact]
    public async Task Cada_area_devuelve_la_ruta_original_de_su_imagen_y_la_url_absoluta_codificada()
    {
        using var cliente = factory.CreateClient();

        var datos = await cliente.GetFromJsonAsync<EquipoFepDto>(Ruta);

        Assert.Equal(["images/ORGANIGRAMA-FEP.png", "images/MENORES-FEP.png"], datos!.Areas.Select(a => a.Imagen));
        Assert.All(datos.Areas, a =>
        {
            Assert.True(Uri.IsWellFormedUriString(a.ImagenUrl, UriKind.Absolute));
            Assert.EndsWith(a.Imagen!, a.ImagenUrl);
            Assert.False(string.IsNullOrWhiteSpace(a.ImagenAlt));
        });
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
