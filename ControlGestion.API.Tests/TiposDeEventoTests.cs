using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Data;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

/// <summary>
/// Los tipos de evento (7.3.13, extensión nueva de la tarea 32), que administra el administrador del sistema con tres
/// permisos de Parámetros. Cada prueba crea sus tipos ("T32 …") y deja inactivos los que tienen eventos, para no
/// sumarle opciones a los formularios de las demás.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class TiposDeEventoTests(ApiDePrueba api)
{
    private const int Media = 1;
    private const int Alta = 2;

    private static Task<HttpResponseMessage> RegistrarAsync(HttpClient cliente, string nombre, bool requiereEvidencia = false,
        int? nivelPrioridad = Media) =>
        cliente.PostAsJsonAsync("/api/tipos-evento", new { nombre, requiereEvidencia, nivelPrioridad });

    private static Task<HttpResponseMessage> ModificarAsync(HttpClient cliente, int id, string nombre, bool requiereEvidencia = false,
        int nivelPrioridad = Media) =>
        cliente.PutAsJsonAsync($"/api/tipos-evento/{id}", new { nombre, requiereEvidencia, nivelPrioridad });

    private static Task<HttpResponseMessage> DesactivarAsync(HttpClient cliente, int id) =>
        cliente.PutAsync($"/api/tipos-evento/{id}/desactivar", null);

    private static async Task<JsonElement> TipoAsync(HttpClient admin, int id) =>
        await (await admin.GetAsync($"/api/tipos-evento/{id}")).LeerJsonAsync();

    private static async Task<List<int>> IdsAsync(HttpClient cliente, string url) =>
        (await (await cliente.GetAsync(url)).LeerJsonAsync()).EnumerateArray().Select(t => t.GetProperty("id").GetInt32()).ToList();

    private static async Task<int> ClienteAsync(HttpClient operador) =>
        await operador.CrearClienteAsync(await operador.CrearZonaAsync());

    [Fact]
    public async Task Un_tipo_se_registra_se_modifica_y_sin_eventos_se_elimina()
    {
        using var admin = await api.AdminAsync();
        using var operador = await api.OperadorAsync();
        var nombre = Ayudantes.Unico("T32 Mercadería dañada");

        var respuesta = await RegistrarAsync(admin, $"  {nombre} ", requiereEvidencia: true, nivelPrioridad: Alta);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var creado = await respuesta.LeerJsonAsync();
        var id = creado.GetProperty("id").GetInt32();
        Assert.Equal(nombre, creado.GetProperty("nombre").GetString());
        Assert.True(creado.GetProperty("requiereEvidencia").GetBoolean());
        Assert.Equal(Alta, creado.GetProperty("nivelPrioridad").GetInt32());
        Assert.True(creado.GetProperty("activo").GetBoolean());
        Assert.Equal(0, creado.GetProperty("eventos").GetInt32());
        // Nace activo: el formulario del evento ya lo ofrece, con su foto y su prioridad.
        var delFormulario = (await (await operador.GetAsync("/api/eventos/tipos")).LeerJsonAsync()).EnumerateArray()
            .Single(t => t.GetProperty("id").GetInt32() == id);
        Assert.True(delFormulario.GetProperty("requiereEvidencia").GetBoolean());
        Assert.Contains(id, await IdsAsync(admin, "/api/tipos-evento"));

        var nuevo = Ayudantes.Unico("T32 Producto vencido");
        var modificado = await (await ModificarAsync(admin, id, nuevo, requiereEvidencia: false, nivelPrioridad: Media)).LeerJsonAsync();
        Assert.Equal(nuevo, modificado.GetProperty("nombre").GetString());
        Assert.False(modificado.GetProperty("requiereEvidencia").GetBoolean());
        Assert.Equal(Media, modificado.GetProperty("nivelPrioridad").GetInt32());
        // Cambiar solo sus mayúsculas no choca con él mismo.
        var enMinusculas = await (await ModificarAsync(admin, id, nuevo.ToLowerInvariant())).LeerJsonAsync();
        Assert.Equal(nuevo.ToLowerInvariant(), enMinusculas.GetProperty("nombre").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/tipos-evento/{id}")).StatusCode);
        await (await admin.GetAsync($"/api/tipos-evento/{id}")).ProblemaAsync(404, "no_encontrado");
        Assert.DoesNotContain(id, await IdsAsync(operador, "/api/eventos/tipos"));
    }

    [Fact]
    public async Task El_nombre_y_la_prioridad_son_obligatorios_y_el_nombre_no_se_repite_sin_importar_mayusculas_ni_tildes()
    {
        using var admin = await api.AdminAsync();

        var errores = (await (await RegistrarAsync(admin, "   ", nivelPrioridad: null)).ProblemaAsync(400, "validacion"))
            .GetProperty("errors");
        Assert.Equal("Escribe el nombre del tipo de evento.", errores.GetProperty("nombre")[0].GetString());
        Assert.Equal("Elige la prioridad.", errores.GetProperty("nivelPrioridad")[0].GetString());
        await (await RegistrarAsync(admin, new string('a', 81))).ProblemaAsync(400, "validacion");
        await (await RegistrarAsync(admin, Ayudantes.Unico("T32 Prioridad"), nivelPrioridad: 7)).ProblemaAsync(400, "validacion");

        var nombre = Ayudantes.Unico("T32 Pérdida");
        var id = (await (await RegistrarAsync(admin, nombre)).LeerJsonAsync()).GetProperty("id").GetInt32();
        var sinTilde = nombre.ToUpperInvariant().Replace("PÉRDIDA", "PERDIDA");
        var repetido = await (await RegistrarAsync(admin, sinTilde)).ProblemaAsync(409, "tipo_evento_repetido");
        Assert.Equal($"Ya existe el tipo de evento \"{sinTilde}\".", repetido.GetProperty("detail").GetString());
        // Tampoco al renombrar otro, ni con el nombre de uno de los sembrados.
        var otro = await admin.CrearTipoEventoAsync();
        await (await ModificarAsync(admin, otro, nombre)).ProblemaAsync(409, "tipo_evento_repetido");
        await (await RegistrarAsync(admin, "otro")).ProblemaAsync(409, "tipo_evento_repetido");

        foreach (var sinUso in new[] { id, otro })
            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/tipos-evento/{sinUso}")).StatusCode);
    }

    [Fact]
    public async Task Un_tipo_inactivo_no_se_elige_en_un_evento_nuevo_pero_sus_eventos_lo_conservan_y_se_filtran_por_el()
    {
        using var admin = await api.AdminAsync();
        using var operador = await api.OperadorAsync();
        var tipoId = await admin.CrearTipoEventoAsync();
        var clienteId = await ClienteAsync(operador);
        var eventoId = await operador.CrearEventoAsync(clienteId, tipoId);

        var desactivado = await (await DesactivarAsync(admin, tipoId)).LeerJsonAsync();

        Assert.False(desactivado.GetProperty("activo").GetBoolean());
        Assert.Equal(1, desactivado.GetProperty("eventos").GetInt32());
        // El formulario ya no lo ofrece, y la API no lo acepta en un evento nuevo.
        Assert.DoesNotContain(tipoId, await IdsAsync(operador, "/api/eventos/tipos"));
        await (await operador.PostAsJsonAsync("/api/eventos", new
        {
            tipoEventoId = tipoId, clienteId, latitud = -17.7833m, longitud = -63.1821m, descripcion = "No debería entrar"
        })).ProblemaAsync(400, "tipo_no_valido");
        // El evento que ya lo tiene lo conserva al modificarse.
        var modificado = await (await operador.PutAsJsonAsync($"/api/eventos/{eventoId}",
            new { tipoEventoId = tipoId, clienteId, descripcion = "Sigue con su tipo" })).LeerJsonAsync();
        Assert.Equal(tipoId, modificado.GetProperty("tipo").GetProperty("id").GetInt32());
        // Los filtros lo siguen ofreciendo, y la lista de eventos lo encuentra.
        var opcion = (await (await operador.GetAsync("/api/tipos-evento/opciones")).LeerJsonAsync()).EnumerateArray()
            .Single(t => t.GetProperty("id").GetInt32() == tipoId);
        Assert.False(opcion.GetProperty("activo").GetBoolean());
        var lista = await (await operador.GetAsync($"/api/eventos?tipoEventoId={tipoId}")).LeerJsonAsync();
        Assert.Equal([eventoId], lista.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("id").GetInt32()));

        // Con eventos no se elimina: se desactiva.
        var nombre = desactivado.GetProperty("nombre").GetString();
        var enUso = await (await admin.DeleteAsync($"/api/tipos-evento/{tipoId}")).ProblemaAsync(409, "tipo_evento_en_uso");
        Assert.Equal($"{nombre} tiene 1 evento: desactívalo en lugar de eliminarlo.", enUso.GetProperty("detail").GetString());

        // Activarlo lo devuelve al formulario. Al final queda inactivo, como lo dejaría el administrador.
        var activado = await (await admin.PutAsync($"/api/tipos-evento/{tipoId}/activar", null)).LeerJsonAsync();
        Assert.True(activado.GetProperty("activo").GetBoolean());
        Assert.Contains(tipoId, await IdsAsync(operador, "/api/eventos/tipos"));
        (await DesactivarAsync(admin, tipoId)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Las_opciones_de_los_filtros_traen_los_activos_y_los_inactivos_con_eventos()
    {
        using var admin = await api.AdminAsync();
        using var operador = await api.OperadorAsync();
        var conEventos = await admin.CrearTipoEventoAsync();
        await operador.CrearEventoAsync(await ClienteAsync(operador), conEventos);
        var sinEventos = await admin.CrearTipoEventoAsync();
        (await DesactivarAsync(admin, conEventos)).EnsureSuccessStatusCode();
        (await DesactivarAsync(admin, sinEventos)).EnsureSuccessStatusCode();

        // El gerente abre el Mapa y el Reporte: ve las opciones. El administrador no abre ninguna pantalla de eventos.
        var gerente = await api.CrearUsuarioAsync(Roles.GerenteGeneral);
        var opciones = await IdsAsync(api.ClienteCon(gerente.Token), "/api/tipos-evento/opciones");
        await (await admin.GetAsync("/api/tipos-evento/opciones")).ProblemaAsync(403, "sin_permiso");

        Assert.Contains(conEventos, opciones);
        Assert.DoesNotContain(sinEventos, opciones);
        Assert.Subset(opciones.ToHashSet(), new HashSet<int> { 1, 2, 3, 4, 5 });
        // Los activos van primero.
        Assert.True(opciones.IndexOf(5) < opciones.IndexOf(conEventos));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/tipos-evento/{sinEventos}")).StatusCode);
    }

    [Fact]
    public async Task El_ultimo_tipo_activo_no_se_desactiva_ni_se_elimina()
    {
        using var admin = await api.AdminAsync();
        var tipoId = await admin.CrearTipoEventoAsync();
        var nombre = (await TipoAsync(admin, tipoId)).GetProperty("nombre").GetString();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Los demás activos se apagan en la base solo durante la prueba: las de la colección corren de a una, y el finally
        // los deja como estaban.
        var otrosActivos = await db.TiposEvento.Where(t => t.Activo && t.Id != tipoId).Select(t => t.Id).ToListAsync();
        JsonElement problema;
        try
        {
            await db.TiposEvento.Where(t => otrosActivos.Contains(t.Id)).ExecuteUpdateAsync(s => s.SetProperty(t => t.Activo, false));
            problema = await (await DesactivarAsync(admin, tipoId)).ProblemaAsync(400, "ultimo_tipo_activo");
            await (await admin.DeleteAsync($"/api/tipos-evento/{tipoId}")).ProblemaAsync(400, "ultimo_tipo_activo");
        }
        finally
        {
            await db.TiposEvento.Where(t => otrosActivos.Contains(t.Id)).ExecuteUpdateAsync(s => s.SetProperty(t => t.Activo, true));
        }

        Assert.Equal($"{nombre} es el único tipo de evento activo: sin él no se podrían registrar eventos. Activa o registra otro antes de desactivarlo.",
            problema.GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/tipos-evento/{tipoId}")).StatusCode);
    }

    [Fact]
    public async Task Pedir_foto_vale_para_los_eventos_pendientes_cuando_se_resuelven()
    {
        using var admin = await api.AdminAsync();
        using var operador = await api.OperadorAsync();
        var tipoId = await admin.CrearTipoEventoAsync(requiereEvidencia: false);
        var nombre = (await TipoAsync(admin, tipoId)).GetProperty("nombre").GetString()!;
        var eventoId = await operador.CrearEventoAsync(await ClienteAsync(operador), tipoId);
        Task<HttpResponseMessage> ResolverAsync() =>
            operador.PostAsJsonAsync($"/api/eventos/{eventoId}/resolver", new { solucion = "Se volvió a visitar" });

        (await ModificarAsync(admin, tipoId, nombre, requiereEvidencia: true)).EnsureSuccessStatusCode();
        await (await ResolverAsync()).ProblemaAsync(400, "evento_sin_evidencia");

        (await ModificarAsync(admin, tipoId, nombre, requiereEvidencia: false)).EnsureSuccessStatusCode();
        (await ResolverAsync()).EnsureSuccessStatusCode();
        (await DesactivarAsync(admin, tipoId)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task El_reporte_cuenta_un_tipo_inactivo_solo_si_tiene_eventos()
    {
        using var admin = await api.AdminAsync();
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var conEventos = await admin.CrearTipoEventoAsync();
        await operador.CrearEventoAsync(await operador.CrearClienteAsync(zonaId), conEventos);
        var sinEventos = await admin.CrearTipoEventoAsync();
        (await DesactivarAsync(admin, conEventos)).EnsureSuccessStatusCode();
        (await DesactivarAsync(admin, sinEventos)).EnsureSuccessStatusCode();

        var reporte = await (await operador.GetAsync($"/api/eventos/reporte?zonaId={zonaId}")).LeerJsonAsync();

        var porTipo = reporte.GetProperty("porTipo").EnumerateArray()
            .ToDictionary(t => t.GetProperty("tipoEventoId").GetInt32(), t => t.GetProperty("cantidad").GetInt32());
        Assert.Equal(1, porTipo[conEventos]);
        Assert.False(porTipo.ContainsKey(sinEventos));
        // Los activos salen también en cero.
        Assert.Equal(0, porTipo[1]);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/tipos-evento/{sinEventos}")).StatusCode);
    }

    [Fact]
    public async Task Cada_operacion_pide_su_permiso_y_queda_en_la_bitacora_en_Parametros()
    {
        using var admin = await api.AdminAsync();
        var secretaria = await api.CrearUsuarioAsync(Roles.Secretaria);
        var gerente = await api.CrearUsuarioAsync(Roles.GerenteGeneral);
        await (await RegistrarAsync(api.ClienteCon(secretaria.Token), Ayudantes.Unico("T32 Sin permiso"))).ProblemaAsync(403, "sin_permiso");
        await (await api.ClienteCon(gerente.Token).GetAsync("/api/tipos-evento")).ProblemaAsync(403, "sin_permiso");

        // Un rol que registra y saca de uso, pero no modifica.
        var (_, rol) = await admin.CrearRolAsync(Permisos.Parametros.RegistrarTipoEvento, Permisos.Parametros.DesactivarTipoEvento);
        var usuario = await api.CrearUsuarioAsync(rol);
        using var cliente = api.ClienteCon(usuario.Token);
        var id = (await (await RegistrarAsync(cliente, Ayudantes.Unico("T32 Bitácora"))).LeerJsonAsync()).GetProperty("id").GetInt32();
        await (await ModificarAsync(cliente, id, Ayudantes.Unico("T32 Otro nombre"))).ProblemaAsync(403, "sin_permiso");
        (await DesactivarAsync(cliente, id)).EnsureSuccessStatusCode();
        (await cliente.PutAsync($"/api/tipos-evento/{id}/activar", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.DeleteAsync($"/api/tipos-evento/{id}")).StatusCode);

        var registros = (await (await admin.GetAsync($"/api/bitacora?usuarioId={usuario.Id}&tamano=100")).LeerJsonAsync())
            .GetProperty("items").EnumerateArray()
            .Where(r => r.GetProperty("modulo").GetString() == Permisos.Parametros.Modulo)
            .Select(r => r.GetProperty("accion").GetString())
            .ToList();
        Assert.Equal(
            [$"Eliminar tipos de evento (DELETE /api/tipos-evento/{id})", $"Activar tipos de evento (PUT /api/tipos-evento/{id}/activar)",
             $"Desactivar tipos de evento (PUT /api/tipos-evento/{id}/desactivar)", "Registrar tipos de evento (POST /api/tipos-evento)"],
            registros);
    }
}
