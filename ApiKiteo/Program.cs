using ApiKiteo.API.Configuration;
using ApiKiteo.API.Infrastructure.Database;
using ApiKiteo.API.Infrastructure.Ldap;
using ApiKiteo.API.Infrastructure.Metrics;
using ApiKiteo.API.Repositories.Implementations;
using ApiKiteo.API.Repositories.Interfaces;
using ApiKiteo.API.Services.Implementations;
using ApiKiteo.API.Services.Interfaces;
using Microsoft.OpenApi.Models;
using Serilog;
using static ApiKiteo.API.Repositories.Interfaces.IDescaneoRepository;

// ══════════════════════════════════════════════════════════════════════════════
// ApiKiteo API  —  ASP.NET Core 8
// Migración de KiteoApp + ApiKiteo Python → C#
// ══════════════════════════════════════════════════════════════════════════════

var builder = WebApplication.CreateBuilder(args);

// ─── Windows Service support ──────────────────────────────────────────────────
// Permite que la app corra como Windows Service (sc create / sc start).
// Cuando corre como .exe directo o en VS no tiene efecto.
builder.Host.UseWindowsService();

// ─── Configuración ───────────────────────────────────────────────────────────
// Un solo appsettings.json — sin ambientes.
// ConnectionString: variables de entorno Thragg (AES key) + DvT (conn string cifrada)
// DatabaseOverride: cambia la BD destino sin tocar las env vars.

// ─── Options Pattern ──────────────────────────────────────────────────────────
builder.Services
    .AddOptions<StoredProceduresOptions>()
    .Bind(builder.Configuration.GetSection(StoredProceduresOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<LdapOptions>()
    .Bind(builder.Configuration.GetSection(LdapOptions.SectionName))
    .ValidateOnStart();



// ─── Infrastructure ───────────────────────────────────────────────────────────

// Singleton: la cadena de conexión se resuelve una sola vez
builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();

// Transient: el provider LDAP no tiene estado
builder.Services.AddTransient<ILdapAuthProvider, LdapAuthProvider>();


// ─── Repositories ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<ISemanasRepository, SemanasRepository>();
builder.Services.AddScoped<IEmpleadosRepository, EmpleadosRepository>();
builder.Services.AddScoped<IVinsRepository, VinsRepository>();
builder.Services.AddScoped<IEscaneoRepository, EscaneoRepository>();
builder.Services.AddScoped<IAdminRepository, AdminRepository>();
builder.Services.AddScoped<IAdminRolesRepository, AdminRolesRepository>();
builder.Services.AddScoped<IMandarFinalRepository, MandarFinalRepository>();
builder.Services.AddScoped<IWksRepository, WksRepository>();
builder.Services.AddScoped<IMacroRepository, MacroRepository>();
builder.Services.AddScoped<ILiberacionRepository, LiberacionRepository>();
builder.Services.AddScoped<ISchedulingRepository, SchedulingRepository>();
builder.Services.AddScoped<IDescaneoRepository, DescaneoRepository>();
builder.Services.AddScoped<IListasRepository, ListasRepository>();
builder.Services.AddScoped<IExpeditadosRepository, ExpeditadosRepository>();
// ─── Services ─────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISemanasService, SemanasService>();
builder.Services.AddScoped<IEmpleadosService, EmpleadosService>();
builder.Services.AddScoped<IVinsService, VinsService>();
builder.Services.AddScoped<IEscaneoService, EscaneoService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IAdminRolesService, AdminRolesService>();
builder.Services.AddScoped<IMandarFinalService, MandarFinalService>();
builder.Services.AddScoped<IWksService, WksService>();
builder.Services.AddScoped<IMacroService, MacroService>();
builder.Services.AddSingleton<MetricsCollector>();
builder.Services.AddScoped<ILiberacionService, LiberacionService>();
builder.Services.AddScoped<ISchedulingService, SchedulingService>();
builder.Services.AddScoped<IDescaneoService, DescaneoService>();
builder.Services.AddScoped<IListasService, ListasService>();
builder.Services.AddScoped<IExpeditadosService, ExpeditadosService>();
// ─── Controllers + JSON ───────────────────────────────────────────────────────
builder.Services
    .AddControllers()
    .AddJsonOptions(opts =>
    {
        // camelCase igual que la API Python original
        opts.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;

        // No omitir nulls — el cliente WPF los espera
        opts.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.Never;
    });

// ─── Swagger / OpenAPI ────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v3", new OpenApiInfo
    {
        Title = "ApiKiteo API",
        Version = "v3.0",
        Description = "API unificada — KiteoApp v2.7 + ApiKiteo Dashboard v3.0"
    });

    // Agrupa los endpoints igual que el Swagger Python original
    c.TagActionsBy(api =>
    {
        var tag = api.GroupName ?? api.ActionDescriptor.RouteValues["controller"];
        return new[] { tag ?? "Default" };
    });

    // Incluye comentarios XML si el proyecto los genera
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

// ─── CORS ─────────────────────────────────────────────────────────────────────
// Red interna de planta — CORS abierto (igual que la API Python original)
builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()));

// ─── Logging con Serilog ──────────────────────────────────────────────────────
// Archivo: C:\APIs\APISMX_Log\ApiKiteo\apiKiteo-YYYYMMDD.log
// Rolling diario, 7 días de retención, auto-borrado.
// Corriendo como Windows Service no hay consola — los logs quedan en archivo.
Log.Logger = new LoggerConfiguration()
    // Nivel base — captura todo de la API: requests, queries, errores, warnings
    .MinimumLevel.Debug()

    // Silenciar ruido interno de .NET que no aporta para debug
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Hosting", Serilog.Events.LogEventLevel.Information)  // requests HTTP
    .MinimumLevel.Override("Microsoft.Data.SqlClient", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http", Serilog.Events.LogEventLevel.Warning)

    // Todo el código propio de la API en Debug — controllers, services, repos, infra
    .MinimumLevel.Override("ApiKiteo.API", Serilog.Events.LogEventLevel.Debug)

    .Enrich.FromLogContext()

    // Consola — visible al correr como .exe directo en dev
    .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")

    // Archivo — siempre activo, incluyendo cuando corre como Windows Service
    .WriteTo.File(
        path: @"C:\APIs\APISMX_Log\ApiKiteo\apiKiteo-.log",
        rollingInterval: RollingInterval.Day,    // un archivo por día
        retainedFileCountLimit: 7,                      // auto-borrar logs de más de 7 días
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext}{NewLine}  {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

// UseSerilog reemplaza todo el sistema de logging — no llamar ClearProviders después
builder.Host.UseSerilog();

// ══════════════════════════════════════════════════════════════════════════════
var app = builder.Build();
// ══════════════════════════════════════════════════════════════════════════════

// ─── Middleware pipeline ──────────────────────────────────────────────────────
app.UseMiddleware<MetricsMiddleware>();
// Swagger siempre disponible — un solo ambiente
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v3/swagger.json", "ApiKiteo API v3.0");
    c.RoutePrefix = string.Empty;
    c.DocumentTitle = "ApiKiteo API";
});

// Handler global de excepciones no capturadas
// Nunca expone detalles internos al cliente
// Log automático de cada request HTTP: método, ruta, status, duración
// Formato en log: GET /semanas?cliente=TBB → 200 OK en 45ms
app.UseSerilogRequestLogging(opts =>
{
    opts.MessageTemplate = "{RequestMethod} {RequestPath} → {StatusCode} en {Elapsed:0}ms";
});

app.UseExceptionHandler(errorApp => errorApp.Run(async ctx =>
{
    ctx.Response.StatusCode = 500;
    ctx.Response.ContentType = "application/json";

    await ctx.Response.WriteAsJsonAsync(new
    {
        exito = false,
        mensaje = "Error interno. Contacta a soporte.",
        codigo = "KITEO_500"
    });
}));

app.UseCors();
app.UseAuthorization();
// ── Weekboard SPA ─────────────────────────────────────────────────────────
app.UseStaticFiles();   // sirve wwwroot/weekboard/index.html
// No necesita fallback route — es un solo archivo HTML, no hay client-side routing
app.MapControllers();

// Endpoint de health-check básico
app.MapGet("/health", () => Results.Ok(new { status = "OK", timestamp = DateTime.UtcNow }))
   .ExcludeFromDescription();

app.Run();