namespace FEP.Api.Contracts.Competicion;

/// <summary>
/// Normativa de cálculo de un ranking (<c>GET /api/v1/competicion/rankings/{clave}/normativa</c>). En
/// <c>Ranking_Absoluto.asp</c> era el desplegable "Información Cálculo del Ranking", escrito a mano en el HTML; aquí
/// sale de <c>Contenidos/rankings/{clave}.json</c>.
/// </summary>
/// <param name="Clave">Clave del ranking (<c>absoluto</c>).</param>
/// <param name="Titulo">Título del bloque ("Información sobre el cálculo del ranking").</param>
/// <param name="Resumen">Entradilla breve de la página (texto plano) o <c>null</c>.</param>
/// <param name="Secciones">Apartados en el orden de la página original.</param>
public sealed record NormativaRankingDto(
    string Clave,
    string Titulo,
    string? Resumen,
    IReadOnlyList<SeccionNormativaDto> Secciones);

/// <summary>Apartado de la normativa (cada <c>h5</c> del ASP).</summary>
/// <param name="Id">Identificador estable (ancla en la página).</param>
/// <param name="Titulo">Título del apartado.</param>
/// <param name="Bloques">Contenido, en orden.</param>
public sealed record SeccionNormativaDto(string Id, string Titulo, IReadOnlyList<BloqueNormativaDto> Bloques);

/// <summary>
/// Bloque de contenido. Según <see cref="Tipo"/> se usa <see cref="Texto"/> (<c>parrafo</c>, <c>nota</c>),
/// <see cref="Elementos"/> (<c>lista</c>) o <see cref="Tabla"/> (<c>tabla</c>). Todo es texto plano (sin HTML).
/// </summary>
/// <param name="Tipo"><c>parrafo</c>, <c>nota</c> (aclaración en cursiva del original), <c>lista</c> o <c>tabla</c>.</param>
/// <param name="Texto">Texto del párrafo o de la nota.</param>
/// <param name="Elementos">Elementos de la lista (vacía si no es una lista).</param>
/// <param name="Tabla">Tabla o <c>null</c>.</param>
public sealed record BloqueNormativaDto(
    string Tipo,
    string? Texto,
    IReadOnlyList<ElementoNormativaDto> Elementos,
    TablaNormativaDto? Tabla);

/// <summary>Elemento de lista: un inicio destacado opcional (en negrita en el original) y el resto del texto.</summary>
/// <param name="Destacado">Inicio destacado ("Ranking de los jugadores participantes") o <c>null</c>.</param>
/// <param name="Texto">Resto del texto.</param>
public sealed record ElementoNormativaDto(string? Destacado, string Texto);

/// <summary>Tabla de dos o más columnas (todas las celdas como texto, tal como se publican).</summary>
/// <param name="Titulo">Descripción de la tabla (para el <c>caption</c>).</param>
/// <param name="Columnas">Encabezados de columna.</param>
/// <param name="Filas">Filas; cada una con tantas celdas como columnas.</param>
public sealed record TablaNormativaDto(string Titulo, IReadOnlyList<string> Columnas, IReadOnlyList<IReadOnlyList<string>> Filas);
