using FEP.Api.Contracts.Competicion;
using Microsoft.Extensions.Options;

namespace FEP.Api.Infrastructure.Asistente;

/// <summary>
/// Red de seguridad del gasto: cuenta los tokens consumidos en el día (hora de Madrid) y, si se supera
/// <see cref="AsistenteOptions.TokensDiarios"/>, el asistente deja de llamar al modelo hasta medianoche y el chat
/// ofrece el formulario de contacto. En memoria: con varias instancias de la API, cada una cuenta lo suyo.
/// </summary>
public sealed class PresupuestoAsistente(IOptions<AsistenteOptions> opciones, TimeProvider reloj)
{
    private readonly object _cerrojo = new();
    private DateOnly _dia;
    private long _consumidos;

    /// <summary><c>true</c> si aún queda presupuesto hoy.</summary>
    public bool HayPresupuesto()
    {
        var limite = opciones.Value.TokensDiarios;
        if (limite <= 0)
        {
            return true;
        }

        lock (_cerrojo)
        {
            Renovar();
            return _consumidos < limite;
        }
    }

    /// <summary>Suma los tokens de una llamada al modelo.</summary>
    public void Registrar(long tokens)
    {
        lock (_cerrojo)
        {
            Renovar();
            _consumidos += Math.Max(0, tokens);
        }
    }

    /// <summary>Tokens consumidos hoy.</summary>
    public long ConsumidosHoy
    {
        get
        {
            lock (_cerrojo)
            {
                Renovar();
                return _consumidos;
            }
        }
    }

    private void Renovar()
    {
        var hoy = DateOnly.FromDateTime(ReglasOrganizacionTorneos.AhoraEnMadrid(reloj));
        if (hoy != _dia)
        {
            _dia = hoy;
            _consumidos = 0;
        }
    }
}
