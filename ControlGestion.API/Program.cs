using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Middleware;
using ControlGestion.API.Models;
using ControlGestion.API.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// La cadena de conexión es secreta: vive en user-secrets (desarrollo) o en la variable
// de entorno ConnectionStrings__DefaultConnection (producción), nunca en appsettings.json.
var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connStr))
    throw new InvalidOperationException(ConfiguracionFaltante.Mensaje(
        "ConnectionStrings:DefaultConnection",
        "Server=<host>;Port=<puerto>;Database=control;User=<usuario>;Password=<contraseña>"));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connStr, ServerVersion.AutoDetect(connStr)));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders()
.AddErrorDescriber<IdentityErroresEnEspanol>();

// JWT: la sección "Jwt" se valida con JwtOptionsValidator (clave presente y de al menos 32 bytes).
builder.Services.AddOptions<JwtOptions>().Bind(builder.Configuration.GetSection(JwtOptions.Seccion));
builder.Services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer();

// El JWT Bearer valida los tokens con las mismas JwtOptions que AuthController usa para firmarlos.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key))
        };
    });

// Política de respaldo: un endpoint sin [Authorize] ni [AllowAnonymous] igual exige sesión.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        // Ningún usuario anidado sale con los campos internos de Identity (passwordHash, securityStamp…).
        options.JsonSerializerOptions.Converters.Add(new ApplicationUserJsonConverter());
    });
builder.Services.AddScoped<ControlGestion.API.Services.InventarioService>();
builder.Services.AddHostedService<ControlGestion.API.Services.MoraDetectionService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// Pedir las JwtOptions ejecuta JwtOptionsValidator. Se hace aquí, antes de migrar y sembrar,
// para que una clave faltante o corta detenga la API sin tocar la base.
_ = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var rol in new[] { "Admin", "Supervisor", "Promotor" })
    {
        if (!await roleManager.RoleExistsAsync(rol))
            await roleManager.CreateAsync(new IdentityRole(rol));
    }

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    // La contraseña del admin sale de Seed:AdminPassword (user-secrets o variable de entorno Seed__AdminPassword).
    await SembrarUsuarioAsync(userManager, app.Logger, "admin@control.com", "Administrador del Sistema", "Admin",
        app.Configuration["Seed:AdminPassword"], "Seed:AdminPassword");

    if (app.Environment.IsDevelopment())
    {
        var demoPassword = app.Configuration["Seed:DemoPassword"];
        await SembrarUsuarioAsync(userManager, app.Logger, "supervisor@control.com", "Supervisor del Sistema", "Supervisor",
            demoPassword, "Seed:DemoPassword");
        await SembrarUsuarioAsync(userManager, app.Logger, "promotor@control.com", "Promotor del Sistema", "Promotor",
            demoPassword, "Seed:DemoPassword");
    }
}

app.UseCors("FrontendPolicy");
app.UseStaticFiles();
app.UseAuthentication();
app.UseMiddleware<AuditoriaMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Crea un usuario con su rol si todavía no existe. Si ya existe, no cambia su contraseña.
// Los mensajes del log nunca incluyen la contraseña.
static async Task SembrarUsuarioAsync(UserManager<ApplicationUser> userManager, ILogger logger,
    string email, string nombreCompleto, string rol, string? password, string claveConfig)
{
    if (await userManager.FindByEmailAsync(email) != null)
    {
        logger.LogInformation("Siembra: {Email} ya existe; no se cambia su contraseña.", email);
        return;
    }

    if (string.IsNullOrWhiteSpace(password))
    {
        logger.LogWarning("Siembra: no se creó {Email}. {Detalle}", email,
            ConfiguracionFaltante.Mensaje(claveConfig, "<contraseña>"));
        return;
    }

    var usuario = new ApplicationUser { UserName = email, Email = email, NombreCompleto = nombreCompleto };
    var resultado = await userManager.CreateAsync(usuario, password);
    if (!resultado.Succeeded)
    {
        logger.LogError("Siembra: no se pudo crear {Email}: {Errores}", email,
            string.Join(" ", resultado.Errors.Select(e => e.Description)));
        return;
    }

    await userManager.AddToRoleAsync(usuario, rol);
    logger.LogInformation("Siembra: se creó {Email} con el rol {Rol}.", email, rol);
}
