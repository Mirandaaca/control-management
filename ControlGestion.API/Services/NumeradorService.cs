using System.Data;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ControlGestion.API.Services;

/// <summary>
/// Entrega números correlativos sin repetirlos, aunque dos altas lleguen a la vez. Reemplaza al
/// "CountAsync() + 1", que daba el mismo número a dos altas simultáneas y una terminaba en 500 (H4).
/// </summary>
public class NumeradorService(AppDbContext db, TimeProvider reloj)
{
    public const string HojaPedido = "HP";
    public const string Cobranza = "COB";
    public const string Pedido = "PED";
    public const string Evento = "EVT";
    public const string Ruta = "RUT";
    public const string Cliente = "CLI";

    /// <summary>"HP-2026-00001". La secuencia vuelve a 1 cada año, contado en la hora de Bolivia.</summary>
    public async Task<string> SiguienteDelAnioAsync(string prefijo)
    {
        var clave = $"{prefijo}-{HoraBolivia.Hoy(reloj).Year}";
        return $"{clave}-{await IncrementarAsync(clave):D5}";
    }

    /// <summary>"CLI-00001". El código no lleva año, así que su secuencia nunca vuelve a empezar.</summary>
    public async Task<string> SiguienteAsync(string prefijo) => $"{prefijo}-{await IncrementarAsync(prefijo):D5}";

    private async Task<int> IncrementarAsync(string clave)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var comando = db.Database.GetDbConnection().CreateCommand();
            comando.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            // Una sola sentencia atómica: crea la secuencia en 1 o le suma 1, y LAST_INSERT_ID(x) deja ese valor
            // guardado para esta conexión. Dos conexiones a la vez nunca leen el mismo número.
            comando.CommandText = """
                INSERT INTO Secuencias (Clave, Ultimo) VALUES (@clave, LAST_INSERT_ID(1))
                ON DUPLICATE KEY UPDATE Ultimo = LAST_INSERT_ID(Ultimo + 1);
                SELECT LAST_INSERT_ID();
                """;
            var parametro = comando.CreateParameter();
            parametro.ParameterName = "@clave";
            parametro.DbType = DbType.String;
            parametro.Value = clave;
            comando.Parameters.Add(parametro);

            return Convert.ToInt32(await comando.ExecuteScalarAsync());
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
