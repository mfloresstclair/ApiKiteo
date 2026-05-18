# ApiKiteo API

API unificada para el sistema de **kiteo industrial** de maquila (línea de producción).  
Migración de KiteoApp + ApiKiteo Python → ASP.NET Core 8 C#.

---

## Stack

| Capa | Tecnología |
|---|---|
| Framework | ASP.NET Core 8 Web API |
| ORM | Dapper + Microsoft.Data.SqlClient |
| Base de datos | SQL Server (Stored Procedures) |
| Auth | LDAP via Novell.Directory.Ldap.NETStandard |
| Docs | Swagger / Swashbuckle v6 |
| Logging | Serilog (archivo diario + consola) |
| Host | Windows Service (`sc create`) |

---

## Configuración rápida

### 1. Variables de entorno requeridas

```
Thragg   — clave AES para descifrar la connection string
DvT      — connection string cifrada con AES
```

> Nunca commitear credenciales. Usar `dotnet user-secrets` en desarrollo.

### 2. appsettings.json — sección StoredProcedures

```json
{
  "StoredProcedures": {
    "GetSemanas":              "kit_vin_wk_names",
    "GetSemanasPendientes":    "Kit_vin_wk_pend",
    "WksStatusBoard":          "kit_vin_wks_status_board",
    "BuscarCircuito":          "Kit_vin_buscar_circuito",
    "MandarFinalParents":      "Kit_vin_mandar_final_parents",
    "MandarFinalPorParent":    "Kit_vin_mandar_final_por_parent",
    "MandarFinalList":         "Kit_vin_mandar_final_list",
    "MandarFinalAdd":          "Kit_vin_mandar_final_add",
    "MandarFinalRemove":       "Kit_vin_mandar_final_remove",
    "MandarFinalCandidatos":   "Kit_vin_mandar_final_candidatos",
    "PreviewSemana":           "Kit_vin_wk_preview",
    "CrearDb":                 "kit_vin_crea_db"
  }
}
```

### 3. Correr en desarrollo

```bash
dotnet run --project ApiKiteo
# Swagger UI → http://localhost:5000
```

### 4. Instalar como Windows Service

```bat
sc create ApiKiteo binPath="C:\APIs\ApiKiteo\ApiKiteo.exe"
sc start ApiKiteo
```

Logs: `C:\APIs\APISMX_Log\ApiKiteo\apiKiteo-YYYYMMDD.log`

---

## Endpoints

### Auth
| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/auth/login` | Login LDAP — devuelve nivel de acceso |

### Semanas
| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/semanas` | Semanas por cliente y tipo |
| `GET` | `/semanas_pendientes` | Semanas con estatus Pendiente / APROBADA |

### Vins
| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/semana_loc` | Locaciones y VINs de una semana |
| `GET` | `/semana_grp_status` | Status por grupo de una semana |
| `POST` | `/semana_grp_faltantes` | Faltantes por grupo |
| `GET` | `/semana_vin_status` | Status de VINs de una semana |
| `GET` | `/buscar_circuito` | Buscar circuito por item u overlay (piso) |

### Escaneo
| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/vin_to_adjust` | VINs disponibles para ajuste |
| `POST` | `/escanear` | Escanear item en una semana |
| `POST` | `/escanear_ajuste` | Escanear con ajuste de VINs |
| `POST` | `/escanear_bulk` | Carga masiva de escaneos |
| `POST` | `/semana_vines_entrega` | Registrar entrega de VINs |

### MandarFinal
| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/mandar_final/parents` | ParentItems de la semana actual (TOP 20) |
| `GET` | `/mandar_final/por_parent` | Items hijo de un ParentItem |
| `GET` | `/mandar_final` | Lista activa de mandar_a_final |
| `POST` | `/mandar_final/add` | Agregar items a la lista |
| `POST` | `/mandar_final/remove` | Remover items de la lista |

### Wks — Pizarrón live
| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/wks/status_board` | Estado de semanas (reemplaza el pizarrón físico) |

### Admin — Empleados
| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/empleado` | Nombre de empleado por número |

### Admin — Semanas
| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/api/semanas/aprobar` | Aprobar semana |
| `GET` | `/api/semanas/preview` | Preview antes de aprobar |
| `POST` | `/api/semanas/crear` | Ejecutar kit_vin_crea_db |

### Admin — Roles
| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/api/roles` | Listar roles |
| `POST` | `/api/roles` | Crear rol |
| `PUT` | `/api/roles/{id}` | Actualizar rol |
| `DELETE` | `/api/roles/{id}` | Eliminar rol (soft-delete) |

### Admin — Exportación
| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/api/macro/export` | Exportar VinBusiness_DB_macro a CSV |

---

## Arquitectura

```
Controllers/
├── Admin/
│   ├── AdminMacroController.cs
│   ├── AdminRolesController.cs
│   └── AdminSemanasController.cs
├── AuthController.cs
├── EmpleadosController.cs
├── EscaneoController.cs
├── MandarFinalController.cs
├── SemanasController.cs
├── VinsController.cs
└── WksController.cs

Services/Implementations/    ← lógica de negocio
Repositories/Implementations/ ← acceso a datos (Dapper + SPs)
Infrastructure/
├── Database/   ← IDbConnectionFactory
└── Ldap/       ← LdapAuthProvider
Configuration/  ← StoredProceduresOptions, LdapOptions
Common/         ← ServiceResult<T>, ErrorCodes, DictionaryExtensions
```

---

## Notas de desarrollo

- Todos los métodos de datos son `async/await` con `CancellationToken`
- Los SPs con múltiples result sets usan `SqlMapper.GridReader`
- Los nombres de SP vienen de `IConfiguration` (sección `StoredProcedures`), nunca hardcodeados
- Credenciales en variables de entorno / user-secrets, nunca en `appsettings.json`
- CORS abierto — red interna de planta (sin Internet)
- El cliente principal es **EstacionKiteo** (WinForms)
