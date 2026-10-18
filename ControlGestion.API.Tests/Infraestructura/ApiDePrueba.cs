using System.Security.Cryptography;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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
    private const string PrefijoDeBase = "control_test_";
    private const int PuertoDeDesarrollo = 9093;

    private readonly MySqlConnectionStringBuilder _servidor;
    private readonly string _baseDeDatos = $"{PrefijoDeBase}{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}"[..40];

    public string ContrasenaAdmin { get; } = Aleatorio(24) + "9";

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
        builder.UseSetting("Jwt:Key", Aleatorio(64));
        builder.UseSetting("Seed:AdminPassword", ContrasenaAdmin);

        builder.ConfigureServices(servicios =>
            servicios.Remove(servicios.Single(d => d.ImplementationType == typeof(MoraDetectionService))));
    }

    public async Task InitializeAsync()
    {
        await EjecutarAsync($"CREATE DATABASE `{_baseDeDatos}`");
        // Arrancar el servidor ejecuta Program.cs: aplica las migraciones y siembra el admin.
        _ = Server;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await EjecutarAsync($"DROP DATABASE IF EXISTS `{_baseDeDatos}`");
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
