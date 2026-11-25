using System.Globalization;

namespace FEP.Api.Contracts.Competicion;

/// <summary>
/// Un partido de <c>SFEP_Torneos_Resultados</c> visto desde la pareja del jugador (<paramref name="NumPareja"/>).
/// Los juegos de cada set son los de la pareja 1 (<c>Set{n}Jug1</c>) y la pareja 2 (<c>Set{n}Jug2</c>).
/// </summary>
public sealed record ResultadoPartido(
    int NumPareja,
    int? IdPareja1,
    int? IdPareja2,
    string? GanaPareja1,
    string? GanaPareja2,
    int? Set1Jug1,
    int? Set1Jug2,
    int? Set2Jug1,
    int? Set2Jug2,
    int? Set3Jug1,
    int? Set3Jug2);

/// <summary>
/// Reglas heredadas de <c>Estadisticas.asp</c> (ficha, balance, pareja, mejor clasificación, evolución y equipos) y de
/// <c>CargarPruebas.asp</c> (desglose de las pruebas de cada ranking).
/// </summary>
public static class ReglasEstadisticas
{
    /// <summary>Rankings nacionales: <c>IdFederacion = 0</c> / <c>Federacion = 0</c> (filtro fijo del ASP).</summary>
    public const int IdFederacionFep = ReglasClasificaciones.IdFederacionFep;

    /// <summary>
    /// Ranking Único (10000 masculino, 10001 femenino): el balance, la pareja, la mejor clasificación y la evolución
    /// solo miran estas pruebas (<c>Ranking=10001 or Ranking=10000</c> en el ASP).
    /// </summary>
    public static readonly IReadOnlyList<int> TiposRankingUnico = [10000, 10001];

    /// <summary>
    /// Tipo cuya fecha de actualización marca el periodo del balance (<c>sql_fecharnk</c>: <c>TipoRanking=10000</c>, también
    /// para las jugadoras).
    /// </summary>
    public const int TipoRankingPeriodo = 10000;

    /// <summary>
    /// <c>CargarPruebas.asp</c> fijaba a mano el inicio del periodo de los rankings de menores
    /// (<c>fechaini="04/11/25"</c> si <c>SFEP_Ranking_Tipos.Modalidad = 'MENORES'</c>; 4 de noviembre de 2025 con el formato
    /// regional español del servidor). Se mantiene (DECISIÓN FEP: calcularla como en el resto).
    /// </summary>
    public static readonly DateOnly InicioPeriodoMenores = new(2025, 11, 4);

    /// <summary>Valor de <c>SFEP_Ranking_Tipos.Modalidad</c> de los rankings de menores.</summary>
    public const string ModalidadMenores = "MENORES";

    /// <summary>
    /// Las pruebas de la categoría de menores anterior cuentan el 40 % (<c>puntos*0.4</c> y "(40%)" en
    /// <c>CargarPruebas.asp</c>).
    /// </summary>
    public const int PorcentajeCategoriaAnterior = 40;

    /// <summary>Campeonatos por equipos: el ASP filtraba <c>SFEP_Torneos.Año &gt; '2012'</c>.</summary>
    public const int AnioEquiposDesde = 2012;

    /// <summary>Publicaciones del histórico que se muestran (<c>TOP 12</c>).</summary>
    public const int PublicacionesEvolucion = 12;

    /// <summary>Carpeta de las fotos de las fichas en la web actual (<c>https://padelfederacion.es/gESTION/Fotos/</c>).</summary>
    public const string CarpetaFotos = "gESTION/Fotos";

    /// <summary>
    /// Rankings de menores con categoría anterior (<c>CargarPruebas.asp</c>): <c>TipoRanking</c> → abreviatura de su
    /// categoría en <c>SFEP_CategoriasMenores</c> y <c>TipoRanking</c> de la categoría anterior (mismo género).
    /// Benjamín (8000/8001), absoluto y veteranos no tienen (<c>RnkAnt=0</c>).
    /// </summary>
    public static readonly IReadOnlyDictionary<int, (string Abreviatura, int TipoAnterior)> CategoriasAnteriores =
        new Dictionary<int, (string, int)>
        {
            [4000] = ("JUN", 5000),
            [4001] = ("JUN", 5001),
            [5000] = ("CAD", 6000),
            [5001] = ("CAD", 6001),
            [6000] = ("INF", 7000),
            [6001] = ("INF", 7001),
            [7000] = ("ALE", 8000),
            [7001] = ("ALE", 8001),
        };

    /// <summary>
    /// Las pruebas de la categoría anterior solo se suman si el año de nacimiento es el primero o el último de la
    /// categoría actual (<c>AnoInicial</c>/<c>AnoFinal</c> de <c>SFEP_CategoriasMenores</c>), como <c>CargarPruebas.asp</c>
    /// (<c>ver="S"</c>). Sin año de nacimiento o sin datos de la categoría, no se suman.
    /// </summary>
    public static bool SumaCategoriaAnterior(int? anioNacimiento, int? anoInicial, int? anoFinal)
        => anioNacimiento is { } anio && (anio == anoInicial || anio == anoFinal);

    /// <summary>
    /// Categoría por edad (<c>edad = year(now()) - year(fechanac)</c>), como el ASP salvo una corrección: de 24 a 34 años
    /// el ASP no asignaba ninguna (se veía "Categoría:" vacío) y aquí es <c>SENIOR</c>. El ASP también llamaba
    /// <c>SENIOR</c> a la edad 0 (fecha de nacimiento en el año en curso, dato no válido): se mantiene. Sin año de
    /// nacimiento → <c>null</c>.
    /// </summary>
    public static string? CategoriaPorEdad(int? anioNacimiento, int anioActual)
    {
        if (anioNacimiento is not { } nacimiento)
        {
            return null;
        }

        var edad = anioActual - nacimiento;
        return edad switch
        {
            0 => "SENIOR",
            < 11 => "BENJAMIN",
            <= 12 => "ALEVIN",
            <= 14 => "INFANTIL",
            <= 16 => "CADETE",
            <= 18 => "JUNIOR",
            <= 23 => "SUB-23",
            <= 34 => "SENIOR",
            <= 39 => "CAT-35",
            <= 44 => "CAT-40",
            <= 49 => "CAT-45",
            <= 54 => "CAT-50",
            _ => "CAT-55",
        };
    }

    /// <summary>
    /// Inicio del periodo de un ranking: un año antes de su fecha de actualización (<c>day/month/(year-1)</c> en el ASP,
    /// que fallaba el 29 de febrero) o, en menores, <see cref="InicioPeriodoMenores"/>. Sin fecha → <c>null</c>.
    /// </summary>
    public static DateOnly? InicioPeriodo(DateOnly? actualizado, string? modalidad)
    {
        if (string.Equals(modalidad?.Trim(), ModalidadMenores, StringComparison.OrdinalIgnoreCase))
        {
            return InicioPeriodoMenores;
        }

        return actualizado?.AddYears(-1);
    }

    /// <summary>
    /// Ronda alcanzada según <c>SFEP_Torneos_Cuadros.Avance</c>. El ASP escribía "Cuartos", "Octavos", "16's", "32's" y
    /// "64's"; aquí, con su nombre completo. Otro valor → <c>null</c> (el ASP lo dejaba en blanco).
    /// </summary>
    public static string? Ronda(int avance) => avance switch
    {
        0 => "Campeón",
        1 => "Finalista",
        2 => "Semifinalista",
        3 => "Cuartos de final",
        4 => "Octavos de final",
        5 => "Dieciseisavos de final",
        6 => "Treintaidosavos de final",
        7 => "Sesentaicuatroavos de final",
        _ => null,
    };

    /// <summary>
    /// Categoría del cuadro según <c>SFEP_Torneos_Cuadros.Nivel</c> ("Primera Categoría"… en el ASP; aquí en estilo de
    /// frase). Otro valor (previas 21 y 31…) → <c>null</c> (el ASP lo dejaba en blanco).
    /// </summary>
    public static string? CategoriaCuadro(int nivel) => nivel switch
    {
        1 => "Primera categoría",
        2 => "Segunda categoría",
        3 => "Tercera categoría",
        4 => "Cuarta categoría",
        5 => "Quinta categoría",
        6 => "Sexta categoría",
        7 => "Séptima categoría",
        8 => "Octava categoría",
        9 => "Novena categoría",
        10 => "Décima categoría",
        _ => null,
    };

    /// <summary>
    /// Nombre del ranking cuando el <c>TipoRanking</c> no está declarado en <see cref="ReglasClasificaciones"/>: como el ASP,
    /// por <c>SFEP_Ranking_Tipos.Categoria</c> (<c>ABSOLUTO</c> → Ranking Único; categorías de menores → Ranking de
    /// Menores; <c>CAT35</c>…<c>CAT60</c> → Ranking de Veteranos). Corrección: el ASP no reiniciaba la variable y una
    /// categoría desconocida heredaba el nombre de la fila anterior; aquí es "Otro ranking".
    /// </summary>
    public static string NombreRankingPorCategoria(string? categoria)
        => (categoria?.Trim() ?? string.Empty).ToUpperInvariant() switch
        {
            "ABSOLUTO" => "Ranking Único Absoluto",
            "BENJAMIN" or "ALEVIN" or "INFANTIL" or "CADETE" or "JUNIOR" => "Ranking de Menores",
            "CAT35" or "CAT40" or "CAT45" or "CAT50" or "CAT55" or "CAT60" => "Ranking de Veteranos",
            _ => "Otro ranking",
        };

    /// <summary>
    /// Ranking publicado (<see cref="ReglasClasificaciones.Rankings"/>) y lista a los que pertenece un <c>TipoRanking</c>;
    /// <c>null</c> si no está declarado.
    /// </summary>
    public static (DefinicionClasificacion Ranking, TipoClasificacion Tipo)? BuscarTipo(int tipoRanking)
    {
        foreach (var ranking in ReglasClasificaciones.Rankings.Values)
        {
            foreach (var tipo in ranking.Tipos)
            {
                if (tipo.TipoRanking == tipoRanking)
                {
                    return (ranking, tipo);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Nombre de la lista como en las páginas de ranking ("Masculino", "Alevín femenino", "+35 masculino"; en silla de
    /// ruedas, "General").
    /// </summary>
    public static string NombreLista(TipoClasificacion tipo)
    {
        if (tipo.Titulo is not null)
        {
            return tipo.Titulo;
        }

        var genero = ReglasClasificaciones.NombreGenero(tipo.Genero);
        var categoria = ReglasClasificaciones.NombreCategoria(tipo.Categoria);
        return categoria is null ? genero : $"{categoria} {genero.ToLowerInvariant()}";
    }

    /// <summary>Ruta de la foto de la ficha en la web actual (si <c>Foto = 'S'</c>).</summary>
    public static string RutaFoto(int licencia)
        => string.Create(CultureInfo.InvariantCulture, $"{CarpetaFotos}/{licencia}.jpg");

    /// <summary>Ruta de la bandera de una federación bajo <c>refs/</c>, como <c>CargarPruebas.asp</c> (<c>refs/images/{id}.jpg</c>).</summary>
    public static string RutaBandera(int idFederacion)
        => string.Create(CultureInfo.InvariantCulture, $"images/{idFederacion}.jpg");

    /// <summary>
    /// Ganados y perdidos con su porcentaje: el de ganados con un decimal (<c>formatnumber(x, 1)</c>) y el de perdidos,
    /// 100 menos el anterior, como el ASP (que además los mezclaba: "14,3%" y "85.7%"). Sin ninguno → porcentajes <c>null</c>.
    /// </summary>
    public static RecuentoDto Recuento(int ganados, int perdidos)
    {
        var total = ganados + perdidos;
        if (total <= 0)
        {
            return new RecuentoDto(ganados, perdidos, null, null);
        }

        var porcentaje = Math.Round(ganados * 100m / total, 1, MidpointRounding.AwayFromZero);
        return new RecuentoDto(ganados, perdidos, porcentaje, 100m - porcentaje);
    }

    /// <summary>
    /// Balance de partidos, sets y juegos (bucle de <c>sql_part</c> del ASP). Por partido: ganado si la pareja del
    /// jugador tiene <c>GanaPareja{n} = 'S'</c>; por set: ganado o perdido según quién hizo más juegos (un set empatado,
    /// p. ej. 0-0 sin jugar, no cuenta) y los juegos de ese set se suman a ambos lados.
    /// SUPUESTO: un set sin dato (<c>NULL</c>) vale 0 (el ASP daba error en <c>cint(Null)</c>).
    /// </summary>
    public static BalancePartidosDto Balance(IEnumerable<ResultadoPartido> partidos)
    {
        int ganados = 0, perdidos = 0, setsGanados = 0, setsPerdidos = 0, juegosGanados = 0, juegosPerdidos = 0;
        foreach (var p in partidos)
        {
            var esPareja1 = p.IdPareja1 == p.NumPareja;
            var gana = esPareja1 ? p.GanaPareja1 : p.GanaPareja2;
            if (string.Equals(gana?.Trim(), "S", StringComparison.OrdinalIgnoreCase))
            {
                ganados++;
            }
            else
            {
                perdidos++;
            }

            foreach (var (j1, j2) in new[] { (p.Set1Jug1, p.Set1Jug2), (p.Set2Jug1, p.Set2Jug2), (p.Set3Jug1, p.Set3Jug2) })
            {
                var propios = esPareja1 ? j1 ?? 0 : j2 ?? 0;
                var rivales = esPareja1 ? j2 ?? 0 : j1 ?? 0;
                if (propios == rivales)
                {
                    continue;
                }

                if (propios > rivales)
                {
                    setsGanados++;
                }
                else
                {
                    setsPerdidos++;
                }

                juegosGanados += propios;
                juegosPerdidos += rivales;
            }
        }

        return new BalancePartidosDto(
            Recuento(ganados, perdidos),
            Recuento(setsGanados, setsPerdidos),
            Recuento(juegosGanados, juegosPerdidos));
    }

    /// <summary>
    /// Puntos que cuentan de una prueba: los de la categoría anterior al 40 % (<c>cint(Puntos)*0.4</c>, redondeado sin
    /// decimales como <c>formatnumber(…, 0)</c>); el resto, enteros.
    /// </summary>
    public static int? PuntosComputados(double? puntos, int porcentaje)
    {
        if (puntos is not { } p)
        {
            return null;
        }

        if (porcentaje == 100)
        {
            return (int)Math.Round(p, MidpointRounding.AwayFromZero);
        }

        // cint() de VBScript redondea al par; después se aplica el porcentaje y formatnumber redondea sin decimales.
        var entero = Math.Round(p, MidpointRounding.ToEven);
        return (int)Math.Round(entero * porcentaje / 100d, MidpointRounding.AwayFromZero);
    }
}
