using System.Net;
using System.Net.Http.Json;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

/// <summary>El mapa de eventos (4.3), del gerente general.</summary>
[Collection(ColeccionApi.Nombre)]
public class MapaEventosTests(ApiDePrueba api)
{
    [Fact]
    public async Task El_mapa_trae_los_puntos_de_los_filtros_con_su_ubicacion_y_su_estado()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var pendiente = await operador.CrearEventoAsync(clienteId, tipoEventoId: 4);
        var resuelto = await operador.CrearEventoAsync(clienteId, tipoEventoId: 5);
        (await operador.PostAsJsonAsync($"/api/eventos/{resuelto}/resolver", new { solucion = "Listo" })).EnsureSuccessStatusCode();
        var gerente = await api.CrearUsuarioAsync(Roles.GerenteGeneral);
        using var cliente = api.ClienteCon(gerente.Token);

        var mapa = await (await cliente.GetAsync($"/api/eventos/mapa?clienteId={clienteId}")).LeerJsonAsync();

        Assert.Equal(2, mapa.GetProperty("total").GetInt32());
        var puntos = mapa.GetProperty("puntos").EnumerateArray().ToList();
        Assert.Equal([resuelto, pendiente], puntos.Select(p => p.GetProperty("id").GetInt32()));
        Assert.Equal(-17.7833m, puntos[0].GetProperty("latitud").GetDecimal());
        Assert.Equal(1, puntos[0].GetProperty("estado").GetInt32());
        Assert.Equal(clienteId, puntos[0].GetProperty("cliente").GetProperty("id").GetInt32());

        var soloPendientes = await (await cliente.GetAsync($"/api/eventos/mapa?clienteId={clienteId}&estado=0&tipoEventoId=4"))
            .LeerJsonAsync();
        Assert.Equal(pendiente, Assert.Single(soloPendientes.GetProperty("puntos").EnumerateArray()).GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task El_gerente_ve_el_mapa_y_el_detalle_pero_no_la_lista_y_la_secretaria_no_ve_el_mapa()
    {
        using var operador = await api.OperadorAsync();
        var eventoId = await operador.CrearEventoAsync(await operador.CrearClienteAsync(await operador.CrearZonaAsync()));
        var gerente = await api.CrearUsuarioAsync(Roles.GerenteGeneral);
        var secretaria = await api.CrearUsuarioAsync(Roles.Secretaria);
        using var clienteGerente = api.ClienteCon(gerente.Token);
        using var clienteSecretaria = api.ClienteCon(secretaria.Token);

        // El clic en un punto abre el detalle, y el filtro de promotor necesita la lista de promotores.
        Assert.Equal(HttpStatusCode.OK, (await clienteGerente.GetAsync($"/api/eventos/{eventoId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await clienteGerente.GetAsync("/api/usuarios/promotores")).StatusCode);
        await (await clienteGerente.GetAsync("/api/eventos")).ProblemaAsync(403, "sin_permiso");
        await (await clienteSecretaria.GetAsync("/api/eventos/mapa")).ProblemaAsync(403, "sin_permiso");
    }
}
