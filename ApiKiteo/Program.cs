using Microsoft.OpenApi.Models;
using KiteoAdmin.API.Configuration;
using KiteoAdmin.API.Infrastructure.Database;
using KiteoAdmin.API.Infrastructure.Ldap;
using KiteoAdmin.API.Repositories.Interfaces;
using KiteoAdmin.API.Repositories.Implementations;
using KiteoAdmin.API.Services.Interfaces;
using KiteoAdmin.API.Services.Implementations;

// ══════════════════════════════════════════════════════════════════════════════
// KiteoAdmin API  —  ASP.NET Core 8
// Migración de KiteoApp + KiteoAdmin Python → C#
// ══════════════════════════════════════════════════════════════════════════════

var builder = WebApplication.CreateBuilder(args);

// ─── User Secrets (desarrollo local) ─────────────────────────────────────────
// Activa: dotnet user-secrets set "ConnectionStrings:DevTest" "Server=..."
// Activa: dotnet user-secrets set "LdapOptions:BindPassword" "..."
if (builder.Environment.IsDevelopment())
    builder.Configuration.AddUserSecrets<Program>();

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
builder.Services.AddScoped<IAuthRepository,      AuthRepository>();
builder.Services.AddScoped<ISemanasRepository,   SemanasRepository>();
builder.Services.AddScoped<IEmpleadosRepository, EmpleadosRepository>();
builder.Services.AddScoped<IVinsRepository,      VinsRepository>();
builder.Services.AddScoped<IEscaneoRepository,   EscaneoRepository>();
builder.Services.AddScoped<IAdminRepository,     AdminRepository>();

// ─── Services ─────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthService,      AuthService>();
builder.Services.AddScoped<ISemanasService,   SemanasService>();
builder.Services.AddScoped<IEmpleadosService, EmpleadosService>();
builder.Services.AddScoped<IVinsService,      VinsService>();
builder.Services.AddScoped<IEscaneoService,   EscaneoService>();
builder.Services.AddScoped<IAdminService,     AdminService>();

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
        Title       = "KiteoAdmin API",
        Version     = "v3.0",
        Description = "API unificada — KiteoApp v2.7 + KiteoAdmin Dashboard v3.0"
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

// ─── Logging ──────────────────────────────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
if (builder.Environment.IsDevelopment())
    builder.Logging.AddDebug();

// ══════════════════════════════════════════════════════════════════════════════
var app = builder.Build();
// ══════════════════════════════════════════════════════════════════════════════

// ─── Middleware pipeline ──────────────────────────────────────────────────────

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v3/swagger.json", "KiteoAdmin API v3.0");
        c.RoutePrefix = string.Empty;   // Swagger en raíz "/"
    });
}

// Handler global de excepciones no capturadas
// Nunca expone detalles internos al cliente
app.UseExceptionHandler(errorApp => errorApp.Run(async ctx =>
{
    ctx.Response.StatusCode  = 500;
    ctx.Response.ContentType = "application/json";

    await ctx.Response.WriteAsJsonAsync(new
    {
        exito   = false,
        mensaje = "Error interno. Contacta a soporte.",
        codigo  = "KITEO_500"
    });
}));

app.UseCors();
app.UseAuthorization();
app.MapControllers();

// Endpoint de health-check básico
app.MapGet("/health", () => Results.Ok(new { status = "OK", timestamp = DateTime.UtcNow }))
   .ExcludeFromDescription();

app.Run();
