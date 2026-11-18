using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

/// <summary>
/// El seguimiento de las entregas (tarea 31): la app manda la ubicación del promotor mientras tiene entregas en ruta para
/// hoy (o su ruta en curso), y "Entregas de hoy" muestra a cada promotor con sus entregas, su última ubicación y su rastro.
/// La API registra los pedidos para mañana: cada prueba mueve su entrega a hoy en la base.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class SeguimientoDeEntregasTests(ApiDePrueba api)
{
    private const int Contado = 0;
    private static readonly object EnSantaCruz = new { latitud = -17.7834m, longitud = -63.1822m };

    private static DateOnly Hoy => HoraBolivia.Hoy(TimeProvider.System);

    private sealed record Promotor(UsuarioDePrueba Usuario, HttpClient Cliente, int ZonaId, int ClienteId) : IDisposable
    {
        public void Dispose() => Cliente.Dispose();
    }

    /// <summary>Un promotor con su zona y un cliente en ella. Con nombre, se lo cambia en la base (para el orden).</summary>
    private async Task<Promotor> PromotorAsync(HttpClient operador, string? nombre = null)
    {
        var usuario = await api.CrearUsuarioAsync(Roles.Promotor);
        if (nombre != null)
            await EnLaBaseAsync(db => db.Users.Where(u => u.Id == usuario.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.NombreCompleto, nombre)));
        var zonaId = await operador.CrearZonaAsync(usuario.Id);
        return new Promotor(usuario, api.ClienteCon(usuario.Token), zonaId, await operador.CrearClienteAsync(zonaId));
    }

    /// <summary>
    /// Un pedido del cliente asignado al promotor, En ruta salvo que se pida otra cosa, con la entrega ese día (hoy, si no
    /// se dice) a esa hora de Bolivia.
    /// </summary>
    private async Task<int> EntregaAsync(HttpClient operador, UsuarioDePrueba promotor, int clienteId, bool enRuta = true,
        int hora = 12, DateOnly? dia = null)
    {
        var pedidoId = await operador.CrearPedidoAsync(clienteId, await operador.CrearProductoAsync(10m), 1);
        (await operador.PostAsJsonAsync("/api/pedidos/asignar", new
        {
            pedidoIds = new[] { pedidoId }, promotorId = promotor.Id, dia = Hoy.AddDays(1).ToString("yyyy-MM-dd")
        })).EnsureSuccessStatusCode();
        if (enRuta)
            (await operador.PostAsJsonAsync($"/api/pedidos/{pedidoId}/avanzar", new { })).EnsureSuccessStatusCode();
        var entrega = HoraBolivia.InicioDelDiaUtc(dia ?? Hoy).AddHours(hora);
        await EnLaBaseAsync(db => db.Pedidos.Where(p => p.Id == pedidoId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.FechaEntregaEstimada, entrega)));
        return pedidoId;
    }

    /// <summary>La ruta de hoy del promotor, con su cliente, ya iniciada por él.</summary>
    private static async Task<int> RutaEnCursoAsync(HttpClient operador, Promotor p)
    {
        var respuesta = await operador.PostAsJsonAsync("/api/rutas", new
        {
            zonaId = p.ZonaId, promotorId = p.Usuario.Id, fecha = Hoy.ToString("yyyy-MM-dd"), clientes = new[] { p.ClienteId }
        });
        respuesta.EnsureSuccessStatusCode();
        var rutaId = (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt32();
        (await p.Cliente.PostAsync($"/api/rutas/{rutaId}/iniciar", null)).EnsureSuccessStatusCode();
        return rutaId;
    }

    private async Task EnLaBaseAsync(Func<AppDbContext, Task> accion)
    {
        using var scope = api.Services.CreateScope();
        await accion(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<List<UbicacionPromotor>> UbicacionesDeAsync(string promotorId)
    {
        using var scope = api.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().UbicacionesPromotor.AsNoTracking()
            .Where(u => u.PromotorId == promotorId).OrderBy(u => u.Id).ToListAsync();
    }

    private static (int? Ruta, int Entregas) Seguimiento(JsonElement json) => (
        json.GetProperty("rutaEnCurso").ValueKind == JsonValueKind.Null ? null : json.GetProperty("rutaEnCurso").GetInt32(),
        json.GetProperty("entregasEnRuta").GetInt32());

    private static async Task<(int? Ruta, int Entregas)> SeguimientoAsync(HttpClient promotor) =>
        Seguimiento(await (await promotor.GetAsync("/api/ubicaciones/seguimiento")).LeerJsonAsync());

    private static async Task<(int? Ruta, int Entregas)> MandarAsync(HttpClient promotor)
    {
        var respuesta = await promotor.PostAsJsonAsync("/api/ubicaciones", EnSantaCruz);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return Seguimiento(await respuesta.LeerJsonAsync());
    }

    private static List<JsonElement> Promotores(JsonElement entregasDelDia) =>
        entregasDelDia.GetProperty("promotores").EnumerateArray().ToList();

    private static string IdDe(JsonElement repartidor) => repartidor.GetProperty("promotor").GetProperty("id").GetString()!;

    private static int[] IdsDeLasEntregas(JsonElement repartidor) =>
        repartidor.GetProperty("entregas").EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).ToArray();

    [Fact]
    public async Task La_app_sabe_que_seguir_su_ruta_de_hoy_en_curso_y_sus_entregas_en_ruta_de_hoy()
    {
        using var operador = await api.OperadorAsync();
        using var p = await PromotorAsync(operador);
        Assert.Equal((null, 0), await SeguimientoAsync(p.Cliente));

        // No cuentan uno que todavía no sale a ruta, uno en ruta para mañana ni uno anulado.
        await EntregaAsync(operador, p.Usuario, p.ClienteId, enRuta: false);
        await EntregaAsync(operador, p.Usuario, p.ClienteId, dia: Hoy.AddDays(1));
        var anulado = await EntregaAsync(operador, p.Usuario, p.ClienteId);
        (await operador.PostAsJsonAsync($"/api/pedidos/{anulado}/anular", new { motivo = "T31: anulado de prueba" }))
            .EnsureSuccessStatusCode();
        Assert.Equal((null, 0), await SeguimientoAsync(p.Cliente));

        await EntregaAsync(operador, p.Usuario, p.ClienteId, hora: 9);
        await EntregaAsync(operador, p.Usuario, p.ClienteId, hora: 15);
        Assert.Equal((null, 2), await SeguimientoAsync(p.Cliente));

        var rutaId = await RutaEnCursoAsync(operador, p);
        Assert.Equal((rutaId, 2), await SeguimientoAsync(p.Cliente));
    }

    [Fact]
    public async Task La_ubicacion_se_guarda_mientras_hay_entregas_en_ruta_y_al_entregar_la_ultima_ya_no()
    {
        using var operador = await api.OperadorAsync();
        using var p = await PromotorAsync(operador);
        await (await p.Cliente.PostAsJsonAsync("/api/ubicaciones", EnSantaCruz)).ProblemaAsync(409, "sin_seguimiento");

        var pedidoId = await EntregaAsync(operador, p.Usuario, p.ClienteId);
        var respuesta = await p.Cliente.PostAsJsonAsync("/api/ubicaciones", new { latitud = -17.7701m, longitud = -63.1702m });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal((null, 1), Seguimiento(await respuesta.LeerJsonAsync()));
        var guardada = Assert.Single(await UbicacionesDeAsync(p.Usuario.Id));
        Assert.Equal((-17.7701m, -63.1702m), (guardada.Latitud, guardada.Longitud));
        Assert.InRange(guardada.Fecha, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));

        // Con la última entrega registrada no queda nada que seguir: la app deja de mandar.
        (await p.Cliente.PostAsJsonAsync($"/api/pedidos/{pedidoId}/entregar", new
        {
            recibidoPor = "T31 Doña Rosa", tipoVenta = Contado, latitud = -17.7801m, longitud = -63.1795m, precisionGPS = 8
        })).EnsureSuccessStatusCode();
        await (await p.Cliente.PostAsJsonAsync("/api/ubicaciones", EnSantaCruz)).ProblemaAsync(409, "sin_seguimiento");
        Assert.Single(await UbicacionesDeAsync(p.Usuario.Id));
    }

    [Fact]
    public async Task Con_la_ruta_en_curso_tambien_se_guarda_y_terminarla_no_corta_las_entregas()
    {
        using var operador = await api.OperadorAsync();
        using var p = await PromotorAsync(operador);
        var rutaId = await RutaEnCursoAsync(operador, p);
        Assert.Equal((rutaId, 0), await MandarAsync(p.Cliente));

        await EntregaAsync(operador, p.Usuario, p.ClienteId);
        Assert.Equal((rutaId, 1), await MandarAsync(p.Cliente));

        (await p.Cliente.PostAsync($"/api/rutas/{rutaId}/terminar", null)).EnsureSuccessStatusCode();
        Assert.Equal((null, 1), await MandarAsync(p.Cliente));
        Assert.Equal(3, (await UbicacionesDeAsync(p.Usuario.Id)).Count);
    }

    [Fact]
    public async Task Entregas_de_hoy_trae_cada_promotor_con_sus_entregas_su_ultima_ubicacion_y_su_rastro()
    {
        // La hora real, fija y sin fracciones (como en DeudasVencidasTests). Pasada la medianoche de Bolivia, dos horas
        // antes: así la última media hora es de hoy.
        var real = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var ahora = HoraBolivia.DesdeUtc(real.UtcDateTime).Hour < 1 ? real.AddHours(-2) : real;
        var reloj = new RelojFijo(ahora);
        var hoy = HoraBolivia.Hoy(reloj);
        DateTime Hace(int minutos, int segundos = 0) => ahora.UtcDateTime.AddMinutes(-minutos).AddSeconds(-segundos);

        using var operador = await api.OperadorAsync();
        using var beatriz = await PromotorAsync(operador, $"T31 Beatriz {Ayudantes.Unico("B")}");
        using var alberto = await PromotorAsync(operador, $"T31 Alberto {Ayudantes.Unico("A")}");
        var sinUbicacion = await operador.CrearClienteAsync(alberto.ZonaId);
        await EnLaBaseAsync(db => db.Clientes.Where(c => c.Id == sinUbicacion)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Latitud, 0m).SetProperty(c => c.Longitud, 0m)));

        var enRuta = await EntregaAsync(operador, alberto.Usuario, alberto.ClienteId, hora: 11, dia: hoy);
        var pendiente = await EntregaAsync(operador, alberto.Usuario, sinUbicacion, enRuta: false, hora: 9, dia: hoy);
        var entregada = await EntregaAsync(operador, alberto.Usuario, alberto.ClienteId, hora: 10, dia: hoy);
        await EnLaBaseAsync(db => db.Pedidos.Where(p => p.Id == entregada).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.EstadoPedidoId, EstadosDePedido.Entregado).SetProperty(p => p.FechaEntregaReal, Hace(60))));
        var anulada = await EntregaAsync(operador, alberto.Usuario, alberto.ClienteId, hora: 13, dia: hoy);
        (await operador.PostAsJsonAsync($"/api/pedidos/{anulada}/anular", new { motivo = "T31: anulada de prueba" }))
            .EnsureSuccessStatusCode();
        await EntregaAsync(operador, alberto.Usuario, alberto.ClienteId, dia: hoy.AddDays(1));
        var deBeatriz = await EntregaAsync(operador, beatriz.Usuario, beatriz.ClienteId, dia: hoy);

        // Alberto manda cada 10 s; el rastro deja uno cada 30 s, desde hace 30 minutos, y el último.
        var puntos = new[] { Hace(40), Hace(20), Hace(19, 50), Hace(19, 40), Hace(19, 30), Hace(0, 15), Hace(0, 5) };
        await EnLaBaseAsync(db =>
        {
            db.UbicacionesPromotor.AddRange(puntos.Select((fecha, i) => new UbicacionPromotor
            {
                PromotorId = alberto.Usuario.Id, Latitud = -17.78m + i * 0.001m, Longitud = -63.18m, Fecha = fecha
            }));
            // La de Beatriz es de ayer: hoy todavía no llega la suya.
            db.UbicacionesPromotor.Add(new UbicacionPromotor
            {
                PromotorId = beatriz.Usuario.Id, Latitud = -17.79m, Longitud = -63.19m, Fecha = Hace(24 * 60)
            });
            return db.SaveChangesAsync();
        });

        await using var fabrica = api.ConReloj(reloj);
        using var cliente = fabrica.ClienteCon(await fabrica.IniciarSesionAsync(ApiDePrueba.CorreoOperador, api.ContrasenaOperador));
        var respuesta = await (await cliente.GetAsync("/api/pedidos/entregas")).LeerJsonAsync();

        Assert.Equal(hoy.ToString("yyyy-MM-dd"), respuesta.GetProperty("dia").GetString());
        Assert.Equal(ahora.UtcDateTime, respuesta.GetProperty("ahora").GetDateTime());
        var promotores = Promotores(respuesta);
        var a = promotores.FindIndex(r => IdDe(r) == alberto.Usuario.Id);
        var b = promotores.FindIndex(r => IdDe(r) == beatriz.Usuario.Id);
        Assert.True(a >= 0 && b > a, "Los promotores van por nombre: Alberto antes que Beatriz.");

        var deAlberto = promotores[a];
        Assert.Equal((3, 1, 1), (deAlberto.GetProperty("total").GetInt32(), deAlberto.GetProperty("entregadas").GetInt32(),
            deAlberto.GetProperty("enRuta").GetInt32()));
        // Por hora: la anulada y la de mañana no están.
        Assert.Equal([pendiente, entregada, enRuta], IdsDeLasEntregas(deAlberto));
        var entregas = deAlberto.GetProperty("entregas").EnumerateArray().ToList();
        Assert.Equal(["Pendiente", "Entregado", "En ruta"],
            entregas.Select(e => e.GetProperty("estado").GetProperty("nombre").GetString()));
        Assert.Equal(JsonValueKind.Null, entregas[0].GetProperty("ubicacionDelCliente").ValueKind);
        Assert.Equal(-17.7833m, entregas[2].GetProperty("ubicacionDelCliente").GetProperty("latitud").GetDecimal());
        Assert.Equal(Hace(60), entregas[1].GetProperty("fechaEntregaReal").GetDateTime());
        Assert.Equal("Calle de entrega de prueba", entregas[2].GetProperty("direccionEntrega").GetString());

        Assert.Equal(Hace(0, 5), deAlberto.GetProperty("ultimaUbicacion").GetProperty("fecha").GetDateTime());
        Assert.Equal([Hace(20), Hace(19, 30), Hace(0, 15), Hace(0, 5)],
            deAlberto.GetProperty("rastro").EnumerateArray().Select(r => r.GetProperty("fecha").GetDateTime()));

        var deLaBeatriz = promotores[b];
        Assert.Equal([deBeatriz], IdsDeLasEntregas(deLaBeatriz));
        Assert.Equal(JsonValueKind.Null, deLaBeatriz.GetProperty("ultimaUbicacion").ValueKind);
        Assert.Empty(deLaBeatriz.GetProperty("rastro").EnumerateArray());
    }

    [Fact]
    public async Task Sin_cartera_completa_ve_solo_las_entregas_de_los_clientes_de_su_cartera()
    {
        using var admin = await api.AdminAsync();
        var (_, rol) = await admin.CrearRolAsync(Permisos.Territorial.SeguirRuta, Permisos.Territorial.RecorrerRuta);
        var supervisor = await api.CrearUsuarioAsync(rol);
        using var operador = await api.OperadorAsync();
        using var p = await PromotorAsync(operador);
        var clienteDelSupervisor = await operador.CrearClienteAsync(await operador.CrearZonaAsync(supervisor.Id));
        var deSuCartera = await EntregaAsync(operador, p.Usuario, clienteDelSupervisor, hora: 9);
        var ajena = await EntregaAsync(operador, p.Usuario, p.ClienteId, hora: 10);

        using var comoSupervisor = api.ClienteCon(supervisor.Token);
        var visto = Assert.Single(Promotores(await (await comoSupervisor.GetAsync("/api/pedidos/entregas")).LeerJsonAsync()),
            r => IdDe(r) == p.Usuario.Id);
        Assert.Equal([deSuCartera], IdsDeLasEntregas(visto));

        var completo = Assert.Single(Promotores(await (await operador.GetAsync("/api/pedidos/entregas")).LeerJsonAsync()),
            r => IdDe(r) == p.Usuario.Id);
        Assert.Equal([deSuCartera, ajena], IdsDeLasEntregas(completo));
    }

    [Fact]
    public async Task Entregas_de_hoy_es_de_quien_reparte_o_sigue_rutas_y_la_ubicacion_la_manda_quien_entrega()
    {
        var secretaria = await api.CrearUsuarioAsync(Roles.Secretaria);
        var gerente = await api.CrearUsuarioAsync(Roles.GerenteGeneral);
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var comoSecretaria = api.ClienteCon(secretaria.Token);
        using var comoGerente = api.ClienteCon(gerente.Token);
        using var comoPromotor = api.ClienteCon(promotor.Token);

        Assert.Equal(HttpStatusCode.OK, (await comoSecretaria.GetAsync("/api/pedidos/entregas")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await comoGerente.GetAsync("/api/pedidos/entregas")).StatusCode);
        await (await comoPromotor.GetAsync("/api/pedidos/entregas")).ProblemaAsync(403, "sin_permiso");

        await (await comoSecretaria.GetAsync("/api/ubicaciones/seguimiento")).ProblemaAsync(403, "sin_permiso");
        await (await comoSecretaria.PostAsJsonAsync("/api/ubicaciones", EnSantaCruz)).ProblemaAsync(403, "sin_permiso");
        await (await comoPromotor.PostAsJsonAsync("/api/ubicaciones", new { latitud = 95m, longitud = -63.18m }))
            .ProblemaAsync(400);
    }
}
