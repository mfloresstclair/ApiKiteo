# ApiKiteo API

Migración de KiteoApp Python (Flask + Waitress) v2.7 a ASP.NET Core 8.  
API unificada para el sistema de kiteo industrial de línea de producción.

---

## Stack

| Capa | Tecnología |
|---|---|
| Framework | ASP.NET Core 8 Web API |
| Data Access | Dapper 2.x + Microsoft.Data.SqlClient 5.x |
| Autenticación | System.DirectoryServices.AccountManagement (Active Directory) |
| Criptografía | AES-CBC (conexión SQL encriptada en variables de entorno) |
| Documentación | Swashbuckle / Swagger UI (solo Development) |
| Servidor | Kestrel (reemplaza Waitress) |

---

## Estructura del proyecto

```
ApiKiteo.API/
├── Controllers/                       ← HTTP layer — thin, sin lógica de negocio
│   ├── KiteoBaseController.cs         ← Base: FromResult<T>() — traduce ServiceResult → HTTP
│   ├── AuthController.cs              ← POST /auth/login
│   ├── SemanasController.cs           ← GET /semanas, /semanas_pendientes
│   ├── EmpleadosController.cs         ← GET /empleado
│   ├── VinsController.cs              ← GET/POST semana_loc, grp_status, vin_status, faltantes
│   ├── EscaneoController.cs           ← POST escanear, escanear_ajuste, vin_to_adjust, entrega
│   └── Admin/
│       └── AdminSemanasController.cs  ← POST /api/semanas/aprobar
│
├── Services/
│   ├── Interfaces/IServices.cs        ← Contratos de negocio
│   └── Implementations/
│       ├── AuthService.cs             ← AD auth + validación SQL
│       ├── SemanasService.cs
│       ├── EmpleadosService.cs
│       ├── VinsService.cs
│       ├── EscaneoService.cs          ← Discriminación EvtData/VinData/GrpData
│       └── AdminService.cs
│
├── Repositories/
│   ├── Interfaces/IRepositories.cs    ← Contratos de acceso a datos
│   └── Implementations/               ← Dapper + Stored Procedures (solo I/O)
│
├── Models/
│   ├── Requests/Requests.cs           ← DTOs de entrada (C# records + [Required])
│   └── Responses/Responses.cs         ← DTOs de salida (contrato fijo con WPF)
│
├── Infrastructure/
│   ├── Cryptography/AesDecryptor.cs   ← AES-CBC decrypt — equivalente al Python original
│   ├── Database/DbConnectionFactory.cs← Resuelve conn string: AES decrypt → DatabaseOverride
│   └── Ldap/LdapAuthProvider.cs       ← PrincipalContext — autentica contra AD
│
├── Configuration/
│   ├── StoredProceduresOptions.cs     ← Nombres de SPs desde appsettings (nunca hardcodeados)
│   └── LdapOptions.cs                 ← Dominio AD
│
├── Common/
│   ├── ApiResponse.cs                 ← ServiceResult<T>, ErrorResponse
│   ├── ErrorCodes.cs                  ← Constantes: AUTH_401, KITEO_404, ADMIN_500...
│   └── DictionaryExtensions.cs        ← GetStr/GetInt/GetDecimal case-insensitive para Dapper
│
├── Properties/launchSettings.json     ← Perfiles VS: Development (HTTP) / Production (HTTP)
├── .gitignore
├── appsettings.json                   ← Base compartida: SPs + LdapOptions + DatabaseOverride=""
├── appsettings.Development.json       ← Dev overrides: logs verbose, DatabaseOverride=DevTest
├── appsettings.Development.template.json ← Plantilla para nuevos devs (va al repo)
├── appsettings.Production.json        ← Prod overrides: logs Warning, URL 0.0.0.0:5000
├── ApiKiteo.API.csproj
└── Program.cs                         ← Composición DI + middleware pipeline
```

---

## Configuración de la conexión a base de datos

### Cómo funciona (AES decrypt)

Las variables de entorno `Thragg` y `DvT` están disponibles en **todas las PCs de la red**.
La conexión a SQL Server se obtiene así:

```
Thragg  →  clave AES
DvT     →  connection string cifrada en base64
           → AesDecryptor.Decrypt()  →  conn string en texto claro (apunta a BOS)
           → NormalizeOdbcToSqlClient()  →  formato SqlClient
           → DatabaseOverride  →  reemplaza la DB si tiene valor
           → EnsureEncryptionHandled()  →  agrega Encrypt=False si falta
```

### DatabaseOverride — cómo cambia la DB por ambiente

| Archivo | `DatabaseOverride` | Resultado |
|---|---|---|
| `appsettings.json` | `""` (vacío) | Usa la DB que viene en `DvT` (`BOS`) |
| `appsettings.Development.json` | `"DevTest"` | Reemplaza la DB por `DevTest` |
| `appsettings.Production.json` | *(no definido)* | Hereda `""` del base → conecta a `BOS` |

### Fallback (solo si `Thragg`/`DvT` no están disponibles)

Si las variables de entorno no existen, `DbConnectionFactory` busca:
```json
"ConnectionStrings": {
  "KiteoDB": "Server=SMXSQL01\\SMX_PROD,1433;Database=DevTest;Integrated Security=True;TrustServerCertificate=True;Encrypt=False;"
}
```
Esto se puede poner en `appsettings.Development.json` o en user-secrets.

---

## Autenticación Active Directory

Usa `PrincipalContext` (mismo mecanismo que el cliente WPF):

```csharp
using var pc = new PrincipalContext(ContextType.Domain, null); // null = auto-detect DC
return pc.ValidateCredentials(username, password);
```

Estrategia con 3 intentos en cascada:
1. `null` → Windows auto-detecta el Domain Controller
2. `"STCLAIRTECH"` → nombre NetBIOS desde `LdapOptions.Domain`
3. `Environment.UserDomainName` → dominio de la máquina actual

No requiere configurar host, puerto ni SSL.

---

## Variables de entorno

| Variable | Quién la usa | Dev | Prod |
|---|---|---|---|
| `Thragg` | DbConnectionFactory | ✅ disponible en la PC | ✅ disponible en el servidor |
| `DvT` | DbConnectionFactory | ✅ disponible en la PC | ✅ disponible en el servidor |
| `ASPNETCORE_ENVIRONMENT` | ASP.NET Core | Seteada por `launchSettings.json` al presionar F5 | Setear manualmente en el servidor |

> No hay más variables de entorno. El AD usa la sesión de Windows directamente.

---

## Ambiente de Desarrollo

### Requisitos

- Visual Studio 2022
- .NET 8 SDK (`dotnet --version` → `8.x.x`)
- Sesión de dominio `STCLAIRTECH` activa
- Variables de entorno `Thragg` y `DvT` disponibles en la PC

### Setup

**1. Clonar el repositorio**
```bash
git clone https://github.com/tu-org/ApiKiteo.API.git
cd ApiKiteo.API
```

**2. Crear el archivo de config local**

`appsettings.Development.json` está en `.gitignore`. Copiarlo de la plantilla:
```powershell
Copy-Item appsettings.Development.template.json appsettings.Development.json
```
Este archivo ya tiene `DatabaseOverride: "DevTest"` configurado.

**3. Restaurar paquetes**
```bash
dotnet restore
```

**4. Abrir el puerto en el firewall** *(una sola vez, como Administrador)*
```powershell
netsh advfirewall firewall add rule `
    name="ApiKiteo Dev Puerto 5000" `
    dir=in action=allow protocol=TCP localport=5000
```

**5. Correr**

En Visual Studio: seleccionar perfil **`Development (HTTP)`** → **F5**

El browser abre en `http://localhost:5000` con Swagger UI.

### Configuración activa en Dev

```
appsettings.json              ← SPs + LdapOptions.Domain + DatabaseOverride=""
  + appsettings.Development.json  ← logs verbose + URL 0.0.0.0:5000 + DatabaseOverride="DevTest"
```

**Resultado:** `Thragg`/`DvT` descifran la conn string a BOS, luego `DatabaseOverride` reemplaza la DB por `DevTest`.

### Verificar

```http
GET  http://localhost:5000/health
→ { "status": "OK" }

POST http://localhost:5000/auth/login
Body: { "username": "tu_usuario", "password": "tu_password" }
→ { "ok": true, "username": "mflores", "access": "LPaccess" }

GET  http://TU_IP_LOCAL:5000/health    ← desde otra máquina de la red
→ { "status": "OK" }
```

---

## Deploy a Producción (manual)

### Paso 1 — Publicar

```bash
dotnet publish -c Release -r win-x64 --self-contained true -o C:\Temp\kiteo-publish
```

O desde Visual Studio: `Build → Publish ApiKiteo.API`
- Configuration: `Release`
- Target Runtime: `win-x64`
- Deployment Mode: `Self-Contained`

> `appsettings.Development.json` **no se incluye** en el publish (Visual Studio lo excluye en Release).

### Paso 2 — Copiar al servidor

```
C:\Temp\kiteo-publish\  →  C:\Apps\ApiKiteo\  (en el servidor de producción)
```

Contenido que debe quedar en el servidor:
```
C:\Apps\ApiKiteo\
├── ApiKiteo.API.exe
├── ApiKiteo.API.dll
├── appsettings.json              ← SPs + LdapOptions + DatabaseOverride=""
├── appsettings.Production.json   ← logs Warning + URL 0.0.0.0:5000
└── [dlls de runtime...]
```

### Paso 3 — Variable de entorno en el servidor

```powershell
# Ejecutar como Administrador en el servidor
[System.Environment]::SetEnvironmentVariable(
    "ASPNETCORE_ENVIRONMENT",
    "Production",
    [System.EnvironmentVariableTarget]::Machine)
```

> `Thragg` y `DvT` ya están disponibles — no hay que setearlas.

**Reiniciar** el servidor o la sesión para que tome efecto.

### Paso 4 — Instalar como Windows Service

```powershell
sc create ApiKiteoAPI `
    binPath= "C:\Apps\ApiKiteo\ApiKiteo.API.exe" `
    DisplayName= "ApiKiteo API" `
    start= auto

sc description ApiKiteoAPI "API de kiteo industrial - KiteoApp v2.7 + ApiKiteo v3.0"

sc start ApiKiteoAPI

sc query ApiKiteoAPI
# STATE : 4  RUNNING  ← debe decir esto
```

> **Windows Auth con SQL Server:** si el servicio corre como `LocalSystem`
> y no tiene acceso al SQL, cambiar la cuenta:
> ```powershell
> sc create ApiKiteoAPI `
>     binPath= "C:\Apps\ApiKiteo\ApiKiteo.API.exe" `
>     DisplayName= "ApiKiteo API" `
>     start= auto `
>     obj= "STCLAIRTECH\cuenta_servicio" `
>     password= "password_cuenta"
> ```

### Paso 5 — Firewall del servidor

```powershell
netsh advfirewall firewall add rule `
    name="ApiKiteo API Puerto 5000" `
    dir=in action=allow protocol=TCP localport=5000
```

### Paso 6 — Verificar

```powershell
Invoke-WebRequest http://localhost:5000/health
# → {"status":"OK","timestamp":"..."}
```

### Configuración activa en Prod

```
appsettings.json               ← SPs + LdapOptions.Domain + DatabaseOverride=""
  + appsettings.Production.json  ← logs Warning + URL 0.0.0.0:5000
```

**Resultado:** `Thragg`/`DvT` descifran la conn string → conecta directamente a `BOS`
(DatabaseOverride está vacío en base → no reemplaza nada).

Swagger **no está disponible** en producción.

### Actualizar la aplicación

```powershell
# 1. Publicar nueva versión (desde tu máquina)
dotnet publish -c Release -r win-x64 --self-contained true -o C:\Temp\kiteo-publish

# 2. En el servidor — detener el servicio
sc stop ApiKiteoAPI

# 3. Copiar nuevos archivos
Copy-Item "\\TU_MAQUINA\kiteo-publish\*" "C:\Apps\ApiKiteo\" -Recurse -Force

# 4. Iniciar el servicio
sc start ApiKiteoAPI

# 5. Verificar
Invoke-WebRequest http://localhost:5000/health
```

---

## Endpoints

| Método | Ruta | Descripción | SP |
|---|---|---|---|
| `POST` | `/auth/login` | Auth AD + validación SQL | `Kit_vin_User_Access` |
| `GET` | `/semanas` | Semanas por cliente/tipo | `Kit_vin_wk` |
| `GET` | `/semanas_pendientes` | Semanas sin cargar esta semana | `Kit_vin_wk_pend` |
| `GET` | `/empleado` | Validar número de empleado | `Kit_vin_Emp` |
| `GET` | `/semana_loc` | VINs y locaciones de semana | `Kit_vin_Wk_Loc` |
| `GET` | `/semana_grp_status` | Progreso por grupo (%) | `Kit_vin_Wk_Grp_Status` |
| `POST` | `/semana_grp_faltantes` | Faltantes por grupo | `Kit_vin_wk_faltantes_grupo` |
| `GET` | `/semana_vin_status` | Porcentaje por VIN | `Kit_vin_Wk_Vin_Status` |
| `POST` | `/vin_to_adjust` | VINs del empleado para ajustar | `Kit_vin_to_adjust` |
| `POST` | `/escanear` | Escaneo normal | `Kit_vin_Scan` |
| `POST` | `/escanear_ajuste` | Revertir VINs específicos | `Kit_vin_Scan_Ajuste` |
| `POST` | `/semana_vines_entrega` | Entrega final a línea | `Kit_vin_entregado_final` |
| `POST` | `/api/semanas/aprobar` | Aprobar semana (Admin) | `Kit_vin_wk_approve` |
| `GET` | `/health` | Health check | — |

### Formato de error estándar

```json
{ "exito": false, "mensaje": "Descripción del error.", "codigo": "KITEO_400" }
```

---

## Stored Procedures

Los nombres se configuran en `appsettings.json → StoredProcedures`.
**Nunca están hardcodeados en el código.**

---

## Paquetes NuGet

| Paquete | Versión | Para qué |
|---|---|---|
| `Dapper` | 2.1.35 | Llamar Stored Procedures |
| `Microsoft.Data.SqlClient` | 5.2.1 | Driver SQL Server |
| `System.DirectoryServices.AccountManagement` | 8.0.0 | Auth Active Directory |
| `Swashbuckle.AspNetCore` | 6.8.1 | Swagger UI |
| `Microsoft.Extensions.Configuration.UserSecrets` | 8.0.0 | Secretos locales dev |

---

## Git — archivos ignorados

```gitignore
appsettings.Development.json    ← config local de cada dev (usar el .template)
bin/ obj/ .vs/ *.user
```

> Para nuevos devs: `Copy-Item appsettings.Development.template.json appsettings.Development.json`

---

## Checklist de deploy a producción

- [ ] Build `Release` sin errores
- [ ] `appsettings.Development.json` **no está** en la carpeta publish
- [ ] `ASPNETCORE_ENVIRONMENT=Production` seteado en el servidor
- [ ] `Thragg` y `DvT` disponibles en el servidor
- [ ] `DatabaseOverride` vacío en `appsettings.json` (conecta a `BOS`)
- [ ] La cuenta del servicio tiene acceso a `SMXSQL01\SMX_PROD\BOS`
- [ ] Puerto 5000 abierto en firewall
- [ ] `sc query ApiKiteoAPI` → `STATE: 4 RUNNING`
- [ ] `GET http://localhost:5000/health` → `{ "status": "OK" }`
- [ ] `POST /auth/login` con credenciales reales → `{ "ok": true }`
- [ ] Cliente WPF apunta a `http://IP_SERVIDOR:5000`
