namespace FEP.Api.Contracts.Micrositios;

/// <summary>
/// Micrositio de la web FEP (mini-CMS de <c>Microsite.asp</c>): cabecera y menú de opciones
/// (<c>SFEP_Microsites</c> + <c>SFEP_Microsites_Menus</c>). Solo micrositios con
/// <c>Visible = 'S'</c> y <c>Borrado = 'N'</c>.
/// </summary>
/// <param name="Id"><c>SFEP_Microsites.IdMicrosite</c> (parámetro <c>Id</c> de <c>Microsite.asp</c>).</param>
/// <param name="Titulo">Nombre del micrositio (<c>Microsite</c>), p. ej. "Homologaciones".</param>
/// <param name="Actualizado">Última modificación de su contenido (<c>FechaUlt</c>, hora local de España, sin zona).</param>
/// <param name="OpcionInicial">
/// Opción que se muestra si no se indica ninguna: la primera del menú por <c>Orden</c> (como hacía el ASP
/// sin <c>IdOpcion</c>). <c>null</c> si el micrositio no tiene opciones visibles.
/// </param>
/// <param name="Opciones">Opciones visibles del menú, por <c>Orden</c>. El menú es de un solo nivel (la tabla no tiene jerarquía).</param>
public sealed record MicrositioDto(
    int Id,
    string Titulo,
    DateTime? Actualizado,
    int? OpcionInicial,
    IReadOnlyList<OpcionMicrositioDto> Opciones);

/// <summary>Opción (apartado) del menú de un micrositio.</summary>
/// <param name="Id"><c>SFEP_Microsites_Menus.IdMenu</c> (parámetro <c>IdOpcion</c> de <c>Microsite.asp</c>).</param>
/// <param name="Titulo">Texto del menú (<c>Menu</c>).</param>
/// <param name="Tipo">
/// <c>normal</c> (texto + archivos) o <c>formulario</c> (la web actual pinta un formulario de inscripción
/// escrito a mano en el ASP; no se migra: el front enlaza a la web actual).
/// </param>
/// <param name="Orden">Posición en el menú (<c>Orden</c>).</param>
public sealed record OpcionMicrositioDto(int Id, string Titulo, string Tipo, int Orden);

/// <summary>Contenido de una opción de un micrositio (<c>SFEP_Microsites_Posts</c> + <c>SFEP_Microsites_Archivos</c>).</summary>
/// <param name="Id"><c>IdMenu</c> de la opción.</param>
/// <param name="MicrositioId"><c>IdMicrosite</c> al que pertenece.</param>
/// <param name="Micrositio">Nombre del micrositio.</param>
/// <param name="Titulo">Texto de la opción en el menú (<c>Menu</c>).</param>
/// <param name="Subtitulo">Título del contenido (<c>Subtitulo</c> del post; el ASP lo pintaba como <c>H3</c>).</param>
/// <param name="Tipo"><c>normal</c> o <c>formulario</c> (ver <see cref="OpcionMicrositioDto.Tipo"/>).</param>
/// <param name="Html">
/// Cuerpo tal como está en la BD (<c>Post</c>, HTML del editor CKEditor de la gestión, a veces con
/// documentos completos <c>&lt;html&gt;&lt;body&gt;</c>). <b>No está saneado</b>: el cliente debe sanearlo
/// con una lista blanca antes de pintarlo. Las rutas relativas son de la web actual.
/// </param>
/// <param name="Archivos">Archivos y enlaces adjuntos, en orden de alta. Lista vacía si no hay.</param>
public sealed record OpcionMicrositioContenidoDto(
    int Id,
    int MicrositioId,
    string Micrositio,
    string Titulo,
    string? Subtitulo,
    string Tipo,
    string? Html,
    IReadOnlyList<ArchivoMicrositioDto> Archivos);

/// <summary>Archivo o enlace adjunto a una opción.</summary>
/// <param name="Titulo">Texto del enlace (columna <c>Archivo</c>); si está vacío, el nombre del fichero.</param>
/// <param name="Tipo">
/// Tipo declarado en la gestión (columna <c>Icono</c>): <c>pdf</c>, <c>xls</c>, <c>doc</c>, <c>ppt</c>,
/// <c>avi</c> (vídeo), <c>web</c>, <c>fac</c> (Facebook), <c>pic</c> (Picasa), <c>fli</c> (Flickr),
/// <c>you</c> (YouTube); <c>null</c> si no se indicó (<c>--</c>) o es desconocido.
/// </param>
/// <param name="Archivo">Valor original de la columna <c>Link</c>: nombre del fichero bajo <c>Paginas/Microsites/</c> o URL externa.</param>
/// <param name="Url">
/// URL absoluta y codificada: <c>{web actual}/Paginas/Microsites/{Link}</c>, o la propia URL si <c>Link</c>
/// ya es http(s). <c>null</c> si no es válida.
/// </param>
public sealed record ArchivoMicrositioDto(string Titulo, string? Tipo, string? Archivo, string? Url);

/// <summary>
/// Reglas fijas de los micrositios heredadas de <c>Microsite.asp</c>. (No se llama <c>Micrositios</c>
/// para no chocar con los espacios de nombres <c>…Micrositios</c> de controladores y repositorios.)
/// </summary>
public static class ReglasMicrositios
{
    /// <summary>Valor mínimo de <c>id</c> e <c>idOpcion</c> (identidades de SQL Server).</summary>
    public const int IdMinimo = 1;

    /// <summary>
    /// Micrositios de acceso restringido: <c>Microsite.asp</c> redirigía a su opción de acceso (25 → 176,
    /// 26 → 189) si no existía la cookie <c>ControlAsambleaSFEP</c> (asambleas con documentación para los
    /// asambleístas). La API pública no los sirve (403) y el front enlaza a la web actual.
    /// </summary>
    public static readonly IReadOnlySet<int> IdsPrivados = new HashSet<int> { 25, 26 };

    /// <summary>Tipos de archivo admitidos en <c>SFEP_Microsites_Archivos.Icono</c>.</summary>
    public static readonly IReadOnlySet<string> TiposArchivo =
        new HashSet<string>(StringComparer.Ordinal) { "pdf", "xls", "doc", "ppt", "avi", "web", "fac", "pic", "fli", "you" };

    /// <summary>Indica si el micrositio es de acceso restringido.</summary>
    public static bool EsPrivado(int id) => IdsPrivados.Contains(id);

    /// <summary>Ruta de la página en la web actual (relativa a su raíz), p. ej. <c>Microsite.asp?Id=7&amp;IdOpcion=19</c>.</summary>
    public static string RutaWebActual(int id, int? idOpcion = null)
        => idOpcion is { } opcion ? $"Microsite.asp?Id={id}&IdOpcion={opcion}" : $"Microsite.asp?Id={id}";
}
