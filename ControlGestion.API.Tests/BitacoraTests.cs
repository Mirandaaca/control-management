using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

/// <summary>La bitácora (8.5, 7.3.35): qué se guarda y cómo la consulta el administrador del sistema.</summary>
[Collection(ColeccionApi.Nombre)]
public class BitacoraTests(ApiDePrueba api)
{
    private const string Seguridad = "Usuario, Seguridad y Auditoría";

    private async Task<List<JsonElement>> RegistrosDeAsync(string usuarioId)
    {
        using var admin = await api.AdminAsync();
        var pagina = await (await admin.GetAsync($"/api/bitacora?usuarioId={usuarioId}&tamano=100")).LeerJsonAsync();
        return pagina.GetProperty("items").EnumerateArray().ToList();
    }

    private static (string? Modulo, string? Accion) Datos(JsonElement r) =>
        (r.GetProperty("modulo").GetString(), r.GetProperty("accion").GetString());

    [Fact]
    public async Task Una_operacion_queda_con_su_usuario_el_modulo_y_la_operacion_del_catalogo()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var secretaria = await api.CrearUsuarioAsync(Roles.Secretaria);
        using var cliente = api.ClienteCon(secretaria.Token);
        await cliente.CrearClienteAsync(zonaId);

        var registros = await RegistrosDeAsync(secretaria.Id);

        // Del más reciente al más antiguo: primero el cliente, después el inicio de sesión de CrearUsuarioAsync.
        Assert.Equal(
            [("Gestión de Ventas Geolocalizadas", "Registrar clientes (POST /api/clientes)"),
             (Seguridad, "Iniciar sesión (POST /api/auth/login)")],
            registros.Select(Datos).ToList());
        var registro = registros[0];
        Assert.Equal(secretaria.Id, registro.GetProperty("usuario").GetProperty("id").GetString());
        Assert.Equal("Prueba Secretaria", registro.GetProperty("usuario").GetProperty("nombreCompleto").GetString());
        // La IP la prueba DespliegueTests, detrás del proxy: el servidor de pruebas no tiene una.
    }

    [Fact]
    public async Task Lo_que_no_salio_bien_no_se_guarda()
    {
        var secretaria = await api.CrearUsuarioAsync(Roles.Secretaria);
        using var cliente = api.ClienteCon(secretaria.Token);

        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync("/api/clientes", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PostAsJsonAsync("/api/productos", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.PostAsJsonAsync("/api/hojas-pedido/999999/anular",
            new { motivo = "No existe" })).StatusCode);

        Assert.Equal([(Seguridad, "Iniciar sesión (POST /api/auth/login)")], (await RegistrosDeAsync(secretaria.Id)).Select(Datos));
    }

    [Fact]
    public async Task El_cierre_de_sesion_y_los_cambios_propios_quedan_con_su_accion()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var cliente = api.ClienteCon(promotor.Token);
        (await cliente.PutAsJsonAsync("/api/perfil", new { nombreCompleto = "Prueba Promotor", email = promotor.Correo }))
            .EnsureSuccessStatusCode();
        (await cliente.PostAsync("/api/auth/logout", null)).EnsureSuccessStatusCode();

        var acciones = (await RegistrosDeAsync(promotor.Id)).Select(r => r.GetProperty("accion").GetString()).ToList();
        Assert.Equal(["Cerrar sesión (POST /api/auth/logout)", "Editar su perfil (PUT /api/perfil)",
            "Iniciar sesión (POST /api/auth/login)"], acciones);
    }

    [Fact]
    public async Task El_inicio_fallido_se_guarda_solo_si_el_correo_es_de_un_usuario_y_nunca_con_la_contrasena()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var anonimo = api.CreateClient();
        const string equivocada = "ClaveEquivocada123";

        var fallido = await anonimo.PostAsJsonAsync("/api/auth/login", new { email = promotor.Correo, password = equivocada, origen = 0 });
        await fallido.ProblemaAsync(401, "credenciales_incorrectas");

        var registros = await RegistrosDeAsync(promotor.Id);
        Assert.Equal((Seguridad, RegistroDeBitacora.AccionInicioFallido), Datos(registros[0]));
        Assert.DoesNotContain(registros, r => r.GetRawText().Contains(equivocada));

        // Un correo que no es de nadie no deja nada: ni el correo ni una fila sin usuario.
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var antes = await db.AuditoriaLogs.CountAsync();
        var correoAjeno = $"{Ayudantes.Unico("nadie")}@test.local";
        await (await anonimo.PostAsJsonAsync("/api/auth/login", new { email = correoAjeno, password = equivocada, origen = 0 }))
            .ProblemaAsync(401, "credenciales_incorrectas");
        Assert.Equal(antes, await db.AuditoriaLogs.CountAsync());
    }

    [Fact]
    public async Task El_inicio_de_un_usuario_inactivo_tambien_es_un_inicio_fallido()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using (var admin = await api.AdminAsync())
            (await admin.PutAsync($"/api/usuarios/{promotor.Id}/desactivar", null)).EnsureSuccessStatusCode();
        using var anonimo = api.CreateClient();

        await (await anonimo.PostAsJsonAsync("/api/auth/login", new { email = promotor.Correo, password = promotor.Contrasena, origen = 0 }))
            .ProblemaAsync(401, "usuario_inactivo");

        Assert.Equal((Seguridad, RegistroDeBitacora.AccionInicioFallido), Datos((await RegistrosDeAsync(promotor.Id))[0]));
    }

    // Así ninguna escritura nueva queda en la bitácora con un módulo o una acción que nadie entiende.
    [Fact]
    public void Cada_escritura_de_la_API_tiene_su_modulo_y_su_accion_o_se_marca_fuera_de_la_bitacora()
    {
        var escrituras = api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods
                .Any(m => m is "POST" or "PUT" or "DELETE") == true)
            .ToList();

        Assert.NotEmpty(escrituras);
        var sinOperacion = escrituras
            .Where(e => e.Metadata.GetMetadata<SinBitacoraAttribute>() is null && OperacionesDeBitacora.De(e) is null)
            .Select(e => e.RoutePattern.RawText);
        Assert.Empty(sinOperacion);

        var fuera = escrituras.Where(e => e.Metadata.GetMetadata<SinBitacoraAttribute>() is not null)
            .Select(e => e.RoutePattern.RawText).ToList();
        Assert.Equal(["api/Rutas/{id:int}/ubicaciones"], fuera);
    }

    [Fact]
    public async Task Filtra_por_usuario_modulo_y_dias_pagina_y_exporta()
    {
        var secretaria = await api.CrearUsuarioAsync(Roles.Secretaria);
        using (var operador = await api.OperadorAsync())
        using (var cliente = api.ClienteCon(secretaria.Token))
            await cliente.CrearClienteAsync(await operador.CrearZonaAsync());
        using var admin = await api.AdminAsync();
        var hoy = HoraBolivia.Hoy(TimeProvider.System);
        var filtros = $"usuarioId={secretaria.Id}&desde={hoy:yyyy-MM-dd}&hasta={hoy:yyyy-MM-dd}";

        var pagina = await (await admin.GetAsync($"/api/bitacora?{filtros}&tamano=1")).LeerJsonAsync();
        Assert.Equal(2, pagina.GetProperty("total").GetInt32());
        Assert.Equal(2, pagina.GetProperty("totalPaginas").GetInt32());
        Assert.Equal("Gestión de Ventas Geolocalizadas", pagina.GetProperty("items")[0].GetProperty("modulo").GetString());

        var modulo = Uri.EscapeDataString(Seguridad);
        var deSeguridad = await (await admin.GetAsync($"/api/bitacora?{filtros}&modulo={modulo}")).LeerJsonAsync();
        Assert.Equal(1, deSeguridad.GetProperty("total").GetInt32());
        var ayer = await (await admin.GetAsync($"/api/bitacora?usuarioId={secretaria.Id}&hasta={hoy.AddDays(-1):yyyy-MM-dd}"))
            .LeerJsonAsync();
        Assert.Equal(0, ayer.GetProperty("total").GetInt32());

        var excel = await admin.GetAsync($"/api/bitacora/excel?{filtros}");
        Assert.Equal(HttpStatusCode.OK, excel.StatusCode);
        using var libro = new XLWorkbook(await excel.Content.ReadAsStreamAsync());
        var hoja = libro.Worksheet("Bitácora");
        Assert.Equal(["Fecha y hora", "Usuario", "Módulo", "Acción", "Dirección IP"],
            Enumerable.Range(1, 5).Select(c => hoja.Cell(1, c).GetString()));
        Assert.Equal(3, hoja.LastRowUsed()!.RowNumber());
        Assert.Equal("Prueba Secretaria", hoja.Cell(2, 2).GetString());
        Assert.Equal(hoy, DateOnly.FromDateTime(hoja.Cell(2, 1).GetDateTime()));
    }

    [Fact]
    public async Task Valida_las_fechas_sin_registros_no_exporta_y_pide_su_permiso()
    {
        using var admin = await api.AdminAsync();
        await (await admin.GetAsync("/api/bitacora?desde=2026-10-21&hasta=2026-10-20")).ProblemaAsync(400, "validacion");
        await (await admin.GetAsync("/api/bitacora/excel?desde=2001-01-01&hasta=2001-01-01")).ProblemaAsync(400, "reporte_sin_datos");

        using var gerente = api.ClienteCon((await api.CrearUsuarioAsync(Roles.GerenteGeneral)).Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await gerente.GetAsync("/api/bitacora")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await gerente.GetAsync("/api/bitacora/excel")).StatusCode);
    }

    [Fact]
    public async Task Las_opciones_traen_los_usuarios_y_los_8_modulos()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var admin = await api.AdminAsync();

        var opciones = await (await admin.GetAsync("/api/bitacora/opciones")).LeerJsonAsync();

        Assert.Contains(opciones.GetProperty("usuarios").EnumerateArray(), u => u.GetProperty("id").GetString() == promotor.Id);
        Assert.Equal(["Gestión de Pedidos", "Gestión de Ventas Geolocalizadas", "Gestión de Cobranzas",
            "Gestión de Eventos y Evidencias", "Visualización Territorial y Re-visitas", "Reportes",
            "Parámetros y Configuraciones", Seguridad],
            opciones.GetProperty("modulos").EnumerateArray().Select(m => m.GetString()));
    }
}
