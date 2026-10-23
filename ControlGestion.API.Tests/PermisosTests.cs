using System.Net;
using System.Net.Http.Json;
using ControlGestion.API.Data;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class PermisosTests(ApiDePrueba api)
{
    [Fact]
    public async Task El_sembrado_deja_los_cuatro_roles_con_los_permisos_del_catalogo()
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var porRol = await db.Roles
            .Where(r => Roles.DelSistema.Contains(r.Name!))
            .Select(r => new { r.Name, Cantidad = db.RolesPermisos.Count(rp => rp.RolId == r.Id) })
            .ToDictionaryAsync(r => r.Name!, r => r.Cantidad);

        Assert.Equal(66, await db.Permisos.CountAsync());
        Assert.Equal(14, porRol[Roles.Administrador]);
        Assert.Equal(21, porRol[Roles.GerenteGeneral]);
        Assert.Equal(27, porRol[Roles.Secretaria]);
        Assert.Equal(23, porRol[Roles.Promotor]);
    }

    // Una base que ya existe (control, la de Azure) recibe el nombre nuevo de un permiso al arrancar la API, sin migración.
    // Decisión del 23/10/2026: la clave de los puntos de referencia cubre registrar, modificar y eliminar (7.3.21).
    [Fact]
    public async Task Al_sembrar_un_permiso_existente_toma_el_nombre_del_catalogo()
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await db.Permisos.Where(p => p.Clave == Permisos.Territorial.RegistrarPuntoReferencia)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Nombre, "Registrar puntos de referencia"));

        await SembradorDeSeguridad.SembrarAsync(db, roles, NullLogger.Instance);

        Assert.Equal("Registrar, modificar y eliminar puntos de referencia", await db.Permisos.AsNoTracking()
            .Where(p => p.Clave == Permisos.Territorial.RegistrarPuntoReferencia).Select(p => p.Nombre).SingleAsync());
    }

    [Fact]
    public async Task Volver_a_sembrar_no_cambia_nada_ni_revive_un_rol_renombrado()
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var secretaria = (await roles.FindByNameAsync(Roles.Secretaria))!;
        var asignaciones = await db.RolesPermisos.CountAsync();

        secretaria.Name = Ayudantes.Unico("Secretaria renombrada");
        await roles.UpdateAsync(secretaria);
        try
        {
            await SembradorDeSeguridad.SembrarAsync(db, roles, NullLogger.Instance);

            Assert.False(await roles.RoleExistsAsync(Roles.Secretaria));
            Assert.Equal(66, await db.Permisos.CountAsync());
            Assert.Equal(asignaciones, await db.RolesPermisos.CountAsync());
        }
        finally
        {
            secretaria.Name = Roles.Secretaria;
            await roles.UpdateAsync(secretaria);
        }
    }

    [Fact]
    public async Task Sin_el_permiso_de_la_operacion_responde_403()
    {
        var secretaria = await api.CrearUsuarioAsync(Roles.Secretaria);
        var gerente = await api.CrearUsuarioAsync(Roles.GerenteGeneral);
        var zona = new { nombre = Ayudantes.Unico("Zona de permisos"), codigo = Ayudantes.Unico("Z"), promotorId = ApiDePrueba.PromotorDeZonasId };

        await (await api.ClienteCon(secretaria.Token).PostAsJsonAsync("/api/zonas", zona)).ProblemaAsync(403, "sin_permiso");
        Assert.Equal(HttpStatusCode.Created, (await api.ClienteCon(gerente.Token).PostAsJsonAsync("/api/zonas", zona)).StatusCode);
    }

    [Fact]
    public async Task Con_la_cartera_completa_se_ven_las_cobranzas_de_todos_los_promotores()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var productoId = await operador.CrearProductoAsync(10m);
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var deOtro = await operador.CrearCobranzaAsync(await operador.CrearClienteAsync(zonaId), productoId, 1);
        var gerente = await api.CrearUsuarioAsync(Roles.GerenteGeneral);

        var ids = async (string token) => (await (await api.ClienteCon(token).GetAsync("/api/cobranzas?tamano=100")).LeerJsonAsync())
            .GetProperty("items").EnumerateArray().Select(c => c.GetProperty("id").GetInt32()).ToList();

        Assert.Contains(deOtro, await ids(gerente.Token));
        Assert.DoesNotContain(deOtro, await ids(promotor.Token));
    }
}
