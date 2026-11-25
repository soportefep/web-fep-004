using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace FEP.Api.Data;

/// <summary>
/// Crea conexiones abiertas a la base de datos FEP. Sustituye a <c>include/conexionSQL.inc</c>
/// (apertura) y <c>include/finconexion.inc</c> (cierre: aquí lo hace el <c>await using</c>).
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>Devuelve una conexión ya abierta; el llamador es responsable de liberarla.</summary>
    Task<DbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}

/// <summary>Implementación para SQL Server con <c>ConnectionStrings:FEP</c>.</summary>
public sealed class SqlConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    /// <summary>Nombre de la cadena de conexión en configuración.</summary>
    public const string NombreCadena = "FEP";

    private readonly string? _cadena = configuration.GetConnectionString(NombreCadena);

    public async Task<DbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_cadena))
        {
            throw new InvalidOperationException(
                $"Falta la cadena de conexión 'ConnectionStrings:{NombreCadena}'. " +
                "Configúrala con 'dotnet user-secrets' o con la variable de entorno 'ConnectionStrings__FEP'.");
        }

        var conexion = new SqlConnection(_cadena);
        try
        {
            await conexion.OpenAsync(cancellationToken);
            return conexion;
        }
        catch
        {
            await conexion.DisposeAsync();
            throw;
        }
    }
}
