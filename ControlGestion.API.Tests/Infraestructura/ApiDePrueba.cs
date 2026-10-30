using System.Security.Cryptography;
using ControlGestion.API.Data;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

namespace ControlGestion.API.Tests.Infraestructura;

/// <summary>
/// Levanta la API en memoria contra una base nueva y desechable del MySQL de pruebas (contenedor
/// mysql84_control_pruebas, puerto 9094). La base se crea al empezar la corrida y se borra al terminar.
/// </summary>
public class ApiDePrueba : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string CorreoAdmin = "admin@control.com";
    public const string CorreoOperador = "operador@test.local";
    public const string RolOperador = "Pruebas: todos los permisos";
    public const string CorreoPromotorDeZonas = "promotor-zonas@test.local";

    /// <summary>
    /// El promotor de las zonas que crean las pruebas cuando no eligen otro: una zona siempre tiene un promotor que
    /// recorre rutas (7.2). Es estático porque toda la corrida comparte una sola ApiDePrueba (ColeccionApi).
    /// </summary>
    public static string PromotorDeZonasId { get; private set; } = string.Empty;
    /// <summary>El único origen que CORS permite en las pruebas (Cors:Origenes).</summary>
    public const string OrigenDePrueba = "http://origen-de-prueba.local";
    private const string PrefijoDeBase = "control_test_";
    private const int PuertoDeDesarrollo = 9093;

    private readonly MySqlConnectionStringBuilder _servidor;
    private readonly string _baseDeDatos = $"{PrefijoDeBase}{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}"[..40];

    public string ContrasenaAdmin { get; } = Aleatorio(24) + "9";
    public string ContrasenaOperador { get; } = Aleatorio(24) + "9";

    // Una sola clave por corrida: una fábrica derivada (WithWebHostBuilder) vuelve a llamar a ConfigureWebHost y debe
    // firmar los tokens con la misma.
    private readonly string _claveJwt = Aleatorio(64);

    /// <summary>La carpeta de las fotos de esta corrida: una temporal, que se borra al terminar.</summary>
    public string CarpetaDeArchivos { get; } = Path.Combine(Path.GetTempPath(), $"control_test_archivos_{Guid.NewGuid():N}");

    public ApiDePrueba()
    {
        var cadena = new ConfigurationBuilder()
            .AddUserSecrets<ApiDePrueba>()
            .AddEnvironmentVariables()
            .Build()
            .GetConnectionString("Pruebas");

        if (string.IsNullOrWhiteSpace(cadena))
            throw new InvalidOperationException(
                "Falta la configuración 'ConnectionStrings:Pruebas'. Guárdala con: dotnet user-secrets set " +
                "\"ConnectionStrings:Pruebas\" \"Server=<host>;Port=9094;User=pruebas;Password=<contraseña>\" " +
                "--project ControlGestion.API.Tests (o usa la variable de entorno ConnectionStrings__Pruebas).");

        _servidor = new MySqlConnectionStringBuilder(cadena) { Database = "" };

        // Guarda: las pruebas crean y borran bases. Nunca deben correr contra el MySQL de desarrollo.
        if (_servidor.Port == PuertoDeDesarrollo)
            throw new InvalidOperationException(
                $"'ConnectionStrings:Pruebas' apunta al puerto {PuertoDeDesarrollo}, que es la base de desarrollo.");
    }

    private string CadenaConLaBase =>
        new MySqlConnectionStringBuilder(_servidor.ConnectionString) { Database = _baseDeDatos }.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Con un entorno que no es Development, la API no carga los user-secrets de desarrollo ni siembra los usuarios demo.
        builder.UseEnvironment("Testing");
        // UseSetting le llega a Program.cs como argumento de línea de comandos: ya existe cuando Program lee
        // la configuración, antes de builder.Build().
        builder.UseSetting("ConnectionStrings:DefaultConnection", CadenaConLaBase);
        builder.UseSetting("Jwt:Key", _claveJwt);
        builder.UseSetting("Seed:AdminPassword", ContrasenaAdmin);
        builder.UseSetting("Almacenamiento:Ruta", CarpetaDeArchivos);
        builder.UseSetting("Cors:Origenes:0", OrigenDePrueba);
        // Sin clave y con una dirección que no existe: una prueba que no use el proveedor falso (ConNavegacion) nunca sale
        // a internet.
        builder.UseSetting("Navegacion:Url", "https://ors.invalid/");

        builder.ConfigureServices(servicios =>
        {
            servicios.Remove(servicios.Single(d => d.ImplementationType == typeof(ProcesoDiarioService)));
            // Cada usuario de prueba pasa tres veces por el hash (crearlo, y entrar el admin y él): con las 100.000
            // iteraciones de Identity, unos 700 hashes por corrida sumaban unos 40 s. El formato del hash no cambia.
            servicios.Configure<PasswordHasherOptions>(opciones => opciones.IterationCount = 1_000);
        });
    }

    public async Task InitializeAsync()
    {
        await EjecutarAsync($"CREATE DATABASE `{_baseDeDatos}`");
        // Arrancar el servidor ejecuta Program.cs: aplica las migraciones, siembra roles y permisos, y crea el admin.
        _ = Server;
        await CrearOperadorAsync();
        await CrearPromotorDeZonasAsync();
    }

    private async Task CrearPromotorDeZonasAsync()
    {
        using var scope = Services.CreateScope();
        var usuarios = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var promotor = new ApplicationUser
        {
            UserName = CorreoPromotorDeZonas, Email = CorreoPromotorDeZonas, NombreCompleto = "Promotor de zonas de pruebas"
        };
        Exigir(await usuarios.CreateAsync(promotor, Aleatorio(24) + "9"));
        Exigir(await usuarios.AddToRoleAsync(promotor, Roles.Promotor));
        PromotorDeZonasId = promotor.Id;
    }

    /// <summary>
    /// El administrador ya no puede crear zonas, clientes ni HP. Las pruebas que solo necesitan esos datos los crean con
    /// este usuario, que tiene los 66 permisos; los permisos de cada rol real se prueban aparte.
    /// </summary>
    private async Task CrearOperadorAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var usuarios = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var rol = new IdentityRole(RolOperador);
        Exigir(await roles.CreateAsync(rol));
        db.RolesPermisos.AddRange(await db.Permisos.Select(p => new RolPermiso { RolId = rol.Id, PermisoId = p.Id }).ToListAsync());
        await db.SaveChangesAsync();

        var operador = new ApplicationUser { UserName = CorreoOperador, Email = CorreoOperador, NombreCompleto = "Operador de pruebas" };
        Exigir(await usuarios.CreateAsync(operador, ContrasenaOperador));
        Exigir(await usuarios.AddToRoleAsync(operador, RolOperador));
    }

    private static void Exigir(IdentityResult resultado)
    {
        if (!resultado.Succeeded)
            throw new InvalidOperationException(string.Join(" ", resultado.Errors.Select(e => e.Description)));
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await EjecutarAsync($"DROP DATABASE IF EXISTS `{_baseDeDatos}`");
        if (Directory.Exists(CarpetaDeArchivos)) Directory.Delete(CarpetaDeArchivos, recursive: true);
    }

    private async Task EjecutarAsync(string sql)
    {
        if (!_baseDeDatos.StartsWith(PrefijoDeBase))
            throw new InvalidOperationException($"La base de pruebas debe empezar con '{PrefijoDeBase}'.");

        await using var conexion = new MySqlConnection(_servidor.ConnectionString);
        await conexion.OpenAsync();
        await using var comando = new MySqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync();
    }

    private static string Aleatorio(int caracteres) =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(caracteres / 2)).ToLowerInvariant();
}

[CollectionDefinition(Nombre)]
public class ColeccionApi : ICollectionFixture<ApiDePrueba>
{
    public const string Nombre = "Api";
}
