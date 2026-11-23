namespace FEP.Api.Contracts.Formacion;

/// <summary>
/// Acción formativa con inscripción abierta (tabla <c>SFEP_Formacion_Calendario</c>:
/// AbiertaInscripcion='S', FechaInscripcion posterior a ahora, Borrado='N', Federacion=0).
/// </summary>
/// <param name="Categoria">Grupo de la portada: <c>monitor</c>, <c>foco</c> o <c>juez</c>.</param>
/// <param name="TipoCurso">Valor original de <c>Categoria</c> (p. ej. <c>EntrenadorN2</c>).</param>
/// <param name="Titulo">Nombre del curso (<c>Titulo</c>).</param>
/// <param name="FechaInicio">Primer día (<c>FechaIni</c>).</param>
/// <param name="FechaFin">Último día (<c>FechaFin</c>).</param>
/// <param name="Lugar">Instalación o sede (<c>Lugar</c>).</param>
/// <param name="Localidad">Localidad (<c>Localidad</c>).</param>
/// <param name="FechaLimiteInscripcion">Tope de inscripción (<c>FechaInscripcion</c>, fecha y hora locales de España).</param>
public sealed record ConvocatoriaDto(
    string Categoria,
    string TipoCurso,
    string Titulo,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    string? Lugar,
    string? Localidad,
    DateTime FechaLimiteInscripcion);
