namespace FEP.Api.Contracts.Federacion;

/// <summary>
/// Ficha de una federación autonómica (o de la propia FEP, <c>Id = 0</c>) con sus datos de contacto y las
/// estadísticas de licencias del año en curso (página <c>Datos_Federacion.asp?id={id}</c>, URL limpia
/// <c>/Federacion/{id}/{nombre}</c>). Datos de <c>SFEP_Federaciones</c>, <c>SFEP_Licencia</c>,
/// <c>SFEP_Fichas</c>, <c>SFEP_Clubes</c>/<c>SFEP_Clubes_Licencia</c> y <c>SFEP_Web_Historial</c>.
/// </summary>
/// <param name="Id"><c>SFEP_Federaciones.IdFederacion</c> (0 = Federación Española de Pádel).</param>
/// <param name="Nombre">
/// Nombre tal como está en la BD (<c>Federacion</c>, normalmente en mayúsculas y sin tildes, p. ej.
/// "FEDERACION ANDALUZA DE PADEL"). El nombre oficial legible está en
/// <c>/api/v1/federacion/federaciones-autonomicas</c>.
/// </param>
/// <param name="Nacional"><c>true</c> solo para la FEP (<c>Id = 0</c>): sus cifras son el total nacional.</param>
/// <param name="Imagen">Ruta original de la bandera bajo <c>refs/</c> (<c>images/{id}.jpg</c>, como el ASP).</param>
/// <param name="ImagenUrl">URL absoluta de la bandera, calculada con <c>Media:BaseUrl</c>.</param>
/// <param name="Presidente">Presidente/a (<c>Presidente</c>), si consta.</param>
/// <param name="Direccion">Dirección postal (<c>Direccion</c>), si consta.</param>
/// <param name="CodigoPostal">Código postal (<c>CP</c>), tal como está en la BD.</param>
/// <param name="Localidad">Localidad (<c>Localidad</c>).</param>
/// <param name="Telefonos">Teléfonos (<c>Telefono1</c>, <c>Telefono2</c>) no vacíos y sin repetir. Lista vacía si no hay.</param>
/// <param name="Email">Correo electrónico de contacto (<c>Email</c>).</param>
/// <param name="Web">Web oficial como URL absoluta (<c>Web</c>; se antepone <c>https://</c> solo si falta el esquema).</param>
/// <param name="Licencias">Estadísticas de licencias del año en curso y evolución histórica.</param>
/// <param name="Consultado">Momento en que se calcularon las cifras (hora local del servidor, sin zona).</param>
public sealed record FichaFederacionDto(
    int Id,
    string Nombre,
    bool Nacional,
    string? Imagen,
    string? ImagenUrl,
    string? Presidente,
    string? Direccion,
    string? CodigoPostal,
    string? Localidad,
    IReadOnlyList<string> Telefonos,
    string? Email,
    string? Web,
    LicenciasFederacionDto Licencias,
    DateTime Consultado);

/// <summary>
/// Licencias de la federación en el año en curso (tramitadas, <c>Tramitado = 'S'</c>). En la FEP
/// (<c>Id = 0</c>) son el total nacional.
/// </summary>
/// <param name="Anio">Año de las licencias (el año en curso, como <c>year(now())</c> del ASP).</param>
/// <param name="Jugadores">Licencias de jugador/a (<c>Tipo = 'J'</c>).</param>
/// <param name="Puesto">
/// Puesto de la federación entre las autonómicas por número de licencias de jugador/a (1 = la que más
/// tiene; los empates comparten puesto). <c>null</c> en la FEP o si la federación no tiene licencias.
/// </param>
/// <param name="FederacionesEnRanking">Federaciones autonómicas con alguna licencia de jugador/a en el año (base del puesto).</param>
/// <param name="Tecnicos">Licencias de técnico nacional (<c>Tipo = 'T'</c>).</param>
/// <param name="Arbitros">Licencias de juez-árbitro nacional (<c>Tipo = 'A'</c> y ficha con <c>ArbitroHabilitadoFEP = 'S'</c>).</param>
/// <param name="Clubes">Clubes federados: licencia de club del año renovada (<c>SFEP_Clubes_Licencia.Renovado = 'S'</c>).</param>
/// <param name="Evolucion">
/// Licencias de jugador/a por año, de 2012 en adelante: los años cerrados salen de <c>SFEP_Web_Historial</c> y
/// el año en curso (último elemento, <see cref="EvolucionLicenciasDto.EnCurso"/>) es <see cref="Jugadores"/>.
/// </param>
/// <param name="Genero">Licencias de jugador/a por género de la ficha.</param>
/// <param name="Edades">Licencias de jugador/a por tramo de edad (edad que se cumple en el año).</param>
public sealed record LicenciasFederacionDto(
    int Anio,
    int Jugadores,
    int? Puesto,
    int FederacionesEnRanking,
    int Tecnicos,
    int Arbitros,
    int Clubes,
    IReadOnlyList<EvolucionLicenciasDto> Evolucion,
    GeneroLicenciasDto Genero,
    EdadesLicenciasDto Edades);

/// <summary>Licencias de jugador/a de un año.</summary>
/// <param name="Anio">Año.</param>
/// <param name="Licencias">Número de licencias (<c>null</c> si el histórico no tiene el dato).</param>
/// <param name="EnCurso"><c>true</c> para el año en curso (cifra en directo, aún en renovación).</param>
public sealed record EvolucionLicenciasDto(int Anio, int? Licencias, bool EnCurso);

/// <summary>Licencias de jugador/a por género (<c>SFEP_Fichas.Genero</c>).</summary>
/// <param name="Masculino">Género <c>M</c>.</param>
/// <param name="Femenino">Género <c>F</c>.</param>
/// <param name="SinDato">Sin género o con otro valor (el ASP no las mostraba).</param>
public sealed record GeneroLicenciasDto(int Masculino, int Femenino, int SinDato);

/// <summary>
/// Licencias de jugador/a por tramo de edad. La edad es <c>año en curso − año de nacimiento</c>
/// (la que se cumple en el año, como el ASP).
/// </summary>
/// <param name="Menores">Hasta 18 años ("Menores (&lt;19)").</param>
/// <param name="Sub23">De 19 a 23 años.</param>
/// <param name="Senior">De 24 a 39 años.</param>
/// <param name="Veteranos">40 años o más ("Veteranos (&gt;39)").</param>
/// <param name="SinDato">Sin fecha de nacimiento (el ASP no las contaba en ningún tramo).</param>
public sealed record EdadesLicenciasDto(int Menores, int Sub23, int Senior, int Veteranos, int SinDato);

/// <summary>Reglas fijas de la ficha de federación heredadas de <c>Datos_Federacion.asp</c>.</summary>
public static class ReglasFichaFederacion
{
    /// <summary>Id mínimo (0 = la FEP, que muestra el total nacional).</summary>
    public const int IdMinimo = 0;

    /// <summary>Id máximo: la última federación autonómica, 22 (<see cref="ReglasFederaciones"/>).</summary>
    public const int IdMaximo = ReglasFederaciones.IdMaximo;

    /// <summary>Id de la Federación Española de Pádel en <c>SFEP_Federaciones</c>.</summary>
    public const int IdNacional = 0;

    /// <summary>Primer año del gráfico de evolución del ASP (los años anteriores del histórico no se mostraban).</summary>
    public const int PrimerAnioEvolucion = 2012;
}
