# KiteoAdmin API — ASP.NET Core 8

Migración de la API Python (Flask + Waitress) a ASP.NET Core 8 con Dapper y SQL Server.

## Stack

| Capa              | Tecnología                                  |
|-------------------|---------------------------------------------|
| Framework         | ASP.NET Core 8 Web API                      |
| ORM / Data access | Dapper 2.x + Microsoft.Data.SqlClient       |
| Autenticación     | LDAP via Novell.Directory.Ldap.NETStandard  |
| Documentación     | Swashbuckle / OpenAPI 3                     |
| Servidor          | Kestrel (reemplaza Waitress)                |

## Estructura

```
KiteoAdmin.API/
├── Controllers/          # HTTP layer — thin, sin lógica de negocio
│   └── Admin/
├── Services/             # Lógica de negocio — orquestación
│   ├── Interfaces/
│   └── Implementations/
├── Repositories/         # Dapper + Stored Procedures — solo I/O
│   ├── Interfaces/
│   └── Implementations/
├── Models/
│   ├── Requests/         # DTOs de entrada (validados con DataAnnotations)
│   └── Responses/        # DTOs de salida (contrato fijo con cliente WPF)
├── Infrastructure/
│   ├── Cryptography/     # AesDecryptor — replica Python DecryptString()
│   ├── Database/         # DbConnectionFactory — resuelve cadena de conexión
│   └── Ldap/             # LdapAuthProvider — autentica contra AD
├── Configuration/        # Options pattern: SP names, LDAP config
├── Common/               # ApiResponse, ErrorCodes, DictionaryExtensions
└── Program.cs            # Composición DI + middleware pipeline
```

## Configuración

### Desarrollo (user-secrets)

```bash
cd KiteoAdmin.API

# Opción A: cadena de conexión plana
dotnet user-secrets set "ConnectionStrings:DevTest" \
  "Server=172.20.46.54;Database=BOS;User Id=sa;Password=***;TrustServerCertificate=True;"

# Opción B: AES encriptado (igual que el Python)
# Setar como variables de entorno antes de correr:
export KITEO_AES_KEY="tu_clave_aes"
export KITEO_CONN_ENCRYPTED="base64_del_connection_string_encriptado"

# Contraseña LDAP
dotnet user-secrets set "LdapOptions:BindPassword" "tu_password_ldap"
```

### Producción (variables de entorno)

```bash
KITEO_AES_KEY=...
KITEO_CONN_ENCRYPTED=...
LdapOptions__BindPassword=...
```

> **Nota**: En ASP.NET Core, `__` (doble guión bajo) equivale a `:` en las variables de entorno.

## Correr localmente

```bash
cd KiteoAdmin.API
dotnet run
# → http://localhost:5000
# → Swagger UI en http://localhost:5000/
```

## Publicar para producción

```bash
dotnet publish -c Release -o ./publish
cd publish
./KiteoAdmin.API   # Linux
KiteoAdmin.API.exe # Windows
```

### Como servicio Windows

```bash
sc create KiteoAdminAPI binPath="C:\kiteo\publish\KiteoAdmin.API.exe" start=auto
sc start KiteoAdminAPI
```

## Endpoints

| Método | Ruta                        | Descripción                        |
|--------|-----------------------------|------------------------------------|
| POST   | /auth/login                 | Autenticación AD + validación SQL  |
| GET    | /semanas                    | Semanas por cliente y tipo         |
| GET    | /semanas_pendientes         | Semanas en estatus Pendiente       |
| GET    | /empleado                   | Validar empleado por número        |
| GET    | /semana_loc                 | VINs y locaciones de semana        |
| GET    | /semana_grp_status          | Progreso por grupo                 |
| POST   | /semana_grp_faltantes       | Faltantes por grupo                |
| GET    | /semana_vin_status          | Estatus VINs con porcentaje        |
| POST   | /vin_to_adjust              | VINs pendientes de ajuste          |
| POST   | /escanear_ajuste            | Ejecutar ajuste por lista de VINs  |
| POST   | /escanear                   | Escaneo normal de ítems            |
| POST   | /semana_vines_entrega       | Entrega final de VINs              |
| POST   | /api/semanas/aprobar        | Aprobar semana (Admin)             |
| GET    | /health                     | Health check                       |

## Stored Procedures

Los nombres se configuran en `appsettings.json` → sección `StoredProcedures`.  
**Nunca están hardcodeados en el código.**

## Notas de migración

- El contrato de request/response **no cambia** — el cliente WPF (EstacionKiteo) no requiere modificaciones.
- Los SPs de SQL Server **no se modifican** — son el contrato fijo.
- `DictionaryExtensions` maneja el acceso case-insensitive a columnas dinámicas de Dapper, equivalente al `dict.get()` de Python.
- El discriminador `Tipo = "EvtData"` en los SPs de escaneo se maneja en `EscaneoService`.
