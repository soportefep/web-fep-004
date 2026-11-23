using System.Net;
using System.Net.Http.Json;
using FEP.Api.Contracts.Federacion;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FEP.Api.Tests;

/// <summary>
/// GET /api/v1/federacion/transparencia (migrado desde Ley_De_Transparencia.asp).
/// No usa SQL Server: el contenido sale de src/FEP.Api/Contenidos/transparencia.json.
/// </summary>
public sealed class TransparenciaTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Ruta = "/api/v1/federacion/transparencia";

    private async Task<TransparenciaDto> ObtenerAsync()
    {
        using var cliente = factory.CreateClient();
        var datos = await cliente.GetFromJsonAsync<TransparenciaDto>(Ruta);
        Assert.NotNull(datos);
        return datos;
    }

    [Fact]
    public async Task Devuelve_las_cuatro_categorias_con_los_22_apartados_del_original()
    {
        var datos = await ObtenerAsync();

        Assert.Equal(
            ["institucional", "contratos-convenios", "economica", "buen-gobierno"],
            datos.Categorias.Select(c => c.Clave));
        var apartados = datos.Categorias.SelectMany(c => c.Apartados).ToArray();
        Assert.Equal(22, apartados.Length);
        // Las claves sirven de ancla en la página: no pueden repetirse (en el ASP todos eran id="acc3").
        Assert.Equal(apartados.Length, apartados.Select(a => a.Clave).Distinct().Count());
    }

    [Fact]
    public async Task Las_listas_no_usadas_llegan_vacias_y_nunca_null()
    {
        var datos = await ObtenerAsync();

        Assert.All(datos.Categorias.SelectMany(c => c.Apartados), a =>
        {
            Assert.NotNull(a.Parrafos);
            Assert.NotNull(a.Lista);
            Assert.NotNull(a.Enlaces);
            Assert.NotNull(a.Personas);
            Assert.NotNull(a.Importes);
            Assert.NotNull(a.Documentos);
            Assert.All(a.Personas, p => Assert.NotNull(p.Funciones));
        });
    }

    [Fact]
    public async Task Los_documentos_tienen_ruta_original_y_url_absoluta_codificada()
    {
        var datos = await ObtenerAsync();

        var documentos = datos.Categorias.SelectMany(c => c.Apartados).SelectMany(a => a.Documentos).ToArray();
        Assert.Equal(39, documentos.Length);
        Assert.All(documentos, d =>
        {
            Assert.StartsWith("docs/", d.Archivo);
            Assert.True(Uri.IsWellFormedUriString(d.Url, UriKind.Absolute));
            Assert.DoesNotContain(" ", d.Url);
        });
        // refs/docs/Presupuesto 2023.pdf → Presupuesto%202023.pdf
        Assert.Contains(documentos, d => d.Url!.EndsWith("/docs/Presupuesto%202023.pdf", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Los_importes_se_devuelven_como_numeros()
    {
        var datos = await ObtenerAsync();

        var subvenciones = datos.Categorias.SelectMany(c => c.Apartados).Single(a => a.Clave == "subvenciones");
        Assert.Equal(5, subvenciones.Importes.Count);
        Assert.Equal(248256.18m, subvenciones.Importes[0].Importe);
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
