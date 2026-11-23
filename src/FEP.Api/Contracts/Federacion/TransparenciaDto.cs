namespace FEP.Api.Contracts.Federacion;

/// <summary>
/// Portal de transparencia de la FEP (página <c>Ley_De_Transparencia.asp</c>).
/// </summary>
/// <param name="Actualizado">Fecha de la última revisión del contenido.</param>
/// <param name="Categorias">Apartados agrupados por categoría: institucional, contratos y convenios, económica y buen gobierno.</param>
public sealed record TransparenciaDto(DateOnly? Actualizado, IReadOnlyList<CategoriaTransparenciaDto> Categorias);

/// <summary>Categoría de la información de transparencia.</summary>
/// <param name="Clave">Identificador estable (ancla en la página): <c>institucional</c>, <c>economica</c>…</param>
/// <param name="Titulo">Nombre de la categoría.</param>
/// <param name="Descripcion">Texto introductorio opcional.</param>
/// <param name="Apartados">Apartados de la categoría (cada desplegable del ASP).</param>
public sealed record CategoriaTransparenciaDto(
    string Clave,
    string Titulo,
    string? Descripcion,
    IReadOnlyList<ApartadoTransparenciaDto> Apartados);

/// <summary>
/// Apartado de transparencia. Solo vienen con contenido las listas que el apartado usa; el resto,
/// vacías (nunca <c>null</c>).
/// </summary>
/// <param name="Clave">Identificador estable (ancla en la página): <c>presupuestos</c>, <c>cuentas-anuales</c>…</param>
/// <param name="Titulo">Título del apartado.</param>
/// <param name="Pendiente"><c>true</c> si la FEP indica que la información está pendiente de actualización.</param>
/// <param name="Actualizacion">Texto de la última actualización declarada por la FEP (p. ej. "enero de 2026").</param>
/// <param name="Parrafos">Párrafos de texto plano.</param>
/// <param name="Lista">Puntos de una lista (funciones, normas, órganos…).</param>
/// <param name="Enlaces">Enlaces a otras páginas relacionadas.</param>
/// <param name="Personas">Personas (estructura organizativa, retribuciones de altos cargos).</param>
/// <param name="Importes">Importes en euros (subvenciones, ingresos propios).</param>
/// <param name="Documentos">Documentos descargables, en el orden publicado por la FEP.</param>
public sealed record ApartadoTransparenciaDto(
    string Clave,
    string Titulo,
    bool Pendiente,
    string? Actualizacion,
    IReadOnlyList<string> Parrafos,
    IReadOnlyList<string> Lista,
    IReadOnlyList<EnlaceTransparenciaDto> Enlaces,
    IReadOnlyList<PersonaTransparenciaDto> Personas,
    IReadOnlyList<ImporteTransparenciaDto> Importes,
    IReadOnlyList<DocumentoTransparenciaDto> Documentos);

/// <summary>Enlace a una página relacionada.</summary>
/// <param name="Texto">Texto del enlace.</param>
/// <param name="Url">URL absoluta (de la web actual; el front la redirige si la página ya está migrada).</param>
public sealed record EnlaceTransparenciaDto(string Texto, string Url);

/// <summary>Persona citada en un apartado.</summary>
/// <param name="Nombre">Nombre y apellidos.</param>
/// <param name="Cargo">Cargo o área.</param>
/// <param name="Grupo">Grupo al que pertenece (Equipo administrativo, Equipo técnico), si aplica.</param>
/// <param name="Funciones">Funciones descritas por la FEP.</param>
/// <param name="RetribucionAnualBruta">Retribución anual bruta en euros, si se publica.</param>
/// <param name="Nota">Observación (p. ej. "Aprobado en Asamblea General").</param>
public sealed record PersonaTransparenciaDto(
    string Nombre,
    string? Cargo,
    string? Grupo,
    IReadOnlyList<string> Funciones,
    decimal? RetribucionAnualBruta,
    string? Nota);

/// <summary>Importe económico.</summary>
/// <param name="Concepto">Concepto (subvención, partida…).</param>
/// <param name="Importe">Importe en euros.</param>
public sealed record ImporteTransparenciaDto(string Concepto, decimal Importe);

/// <summary>Documento publicado.</summary>
/// <param name="Titulo">Título del documento.</param>
/// <param name="Descripcion">Aclaración opcional.</param>
/// <param name="Periodo">Ejercicio o periodo al que se refiere (<c>2024</c>, <c>2025-2026</c>), si se conoce.</param>
/// <param name="Archivo">Ruta original bajo <c>refs/</c> (p. ej. <c>docs/Presupuesto 2023.pdf</c>).</param>
/// <param name="Url">URL absoluta del documento (espacios codificados), calculada con <c>Media:BaseUrl</c>.</param>
public sealed record DocumentoTransparenciaDto(string Titulo, string? Descripcion, string? Periodo, string Archivo, string? Url);
