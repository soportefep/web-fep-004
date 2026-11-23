// FEP.Web: sirve el front estático de wwwroot (index.html, assets/, mock/).
// No contiene lógica de negocio ni acceso a datos: eso vive en FEP.Api.

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// "/" -> wwwroot/index.html; el resto de ficheros estáticos tal cual.
app.UseDefaultFiles();
app.UseStaticFiles();

// Rutas sin fichero (p. ej. /noticias/123 cuando haya enrutado en cliente) -> index.html.
// Las peticiones a ficheros inexistentes con extensión (.js, .css, .json...) siguen dando 404.
app.MapFallbackToFile("index.html");

app.Run();
