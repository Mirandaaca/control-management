using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class ConfiguracionTests(ApiDePrueba api)
{
    [Fact]
    public async Task La_configuracion_general_viene_sembrada_con_los_dias_del_alcance()
    {
        using var operador = await api.OperadorAsync();

        var configuracion = (await (await operador.GetAsync("/api/configuraciones")).LeerJsonAsync())
            .EnumerateArray().ToDictionary(c => c.GetProperty("clave").GetString()!, c => c.GetProperty("valor").GetString());

        Assert.Equal("15", configuracion["DiasRevisita"]);
        Assert.Equal("30", configuracion["DiasVencimientoCredito"]);
    }
}
