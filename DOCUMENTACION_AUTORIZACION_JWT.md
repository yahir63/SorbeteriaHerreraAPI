# Documentacion tecnica - Autenticacion, autorizacion y auditoria JWT

Este documento describe como funciona actualmente la autenticacion JWT, la autorizacion por endpoint y el acceso reutilizable al usuario autenticado en `HerreraSystemAPI`.

Fuentes principales:

- `HerreraSystem.Application/Services/AuthService.cs`
- `HerreraSystemAPI/Program.cs`
- `HerreraSystemAPI/Services/CurrentUserService.cs`
- `HerreraSystem.Application/Interfaces/Services/ICurrentUserService.cs`
- `HerreraSystemAPI/Controllers/ProductsController.cs`
- `HerreraSystem.Application/Services/ProductService.cs`

## 1. Resumen

La API usa JWT Bearer. El login emite un token firmado con la configuracion `Jwt` de `appsettings`. Los endpoints protegidos usan `[Authorize]` y ASP.NET Core valida automaticamente el token recibido en el header:

```http
Authorization: Bearer {token}
```

La auditoria no debe depender de campos enviados por el frontend. Para obtener el usuario actual se creo `ICurrentUserService`, que lee los claims del JWT desde `HttpContext.User`.

## 2. Flujo completo

1. El usuario envia credenciales a login.
2. `AuthService.LoginAsync` valida usuario activo y password.
3. `AuthService.GenerateJwtToken` genera un JWT con ID, username y roles.
4. El frontend guarda el token de forma segura segun su estrategia de sesion.
5. En llamadas protegidas, el frontend envia `Authorization: Bearer {token}`.
6. `UseAuthentication()` valida firma, issuer, audience y expiracion.
7. `UseAuthorization()` aplica `[Authorize]`, roles o politicas.
8. Los servicios de aplicacion pueden leer el usuario con `ICurrentUserService`.

## 3. Claims incluidos en el JWT

El token generado durante login incluye estos claims relevantes:

| Claim | Valor | Uso |
|---|---|---|
| `sub` | `user.Id.ToString()` | Identificador estandar del sujeto del token. |
| `UserId` | `user.Id.ToString()` | Claim explicito para auditoria interna. |
| `ClaimTypes.NameIdentifier` | `user.Id.ToString()` | Compatibilidad con APIs de identidad de .NET. |
| `ClaimTypes.Name` | `user.UserName` | Nombre de usuario autenticado. |
| `username` | `user.UserName` | Compatibilidad con el claim previo del proyecto. |
| `jti` | `Guid.NewGuid().ToString()` | Identificador unico del token. |
| `FullName` | Nombre y apellido | Informacion descriptiva. |
| `email` | Email del usuario, si existe | Informacion descriptiva. |
| `ClaimTypes.Role` | Cada rol del usuario | Autorizacion por roles. |

Fragmento conceptual:

```csharp
new("UserId", user.Id.ToString())
new(ClaimTypes.NameIdentifier, user.Id.ToString())
new(ClaimTypes.Name, user.UserName)
new Claim(ClaimTypes.Role, role)
```

Si el usuario tiene varios roles, el token contiene varios claims `ClaimTypes.Role`, uno por rol.

## 4. Configuracion en `Program.cs`

La API registra autenticacion JWT Bearer:

```csharp
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = Jwt["Issuer"],
        ValidateAudience = true,
        ValidAudience = Jwt["Audience"],
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key!))
    };
});

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
```

El orden del pipeline es importante:

```csharp
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

`UseAuthentication()` debe ejecutarse antes que `UseAuthorization()` para que los claims ya esten disponibles cuando se evalua `[Authorize]`.

## 5. Autorizacion por endpoints

`[Authorize]` exige que exista un token valido. Si no hay token o el token no es valido, ASP.NET Core responde `401 Unauthorized`.

Ejemplo:

```csharp
[HttpPost]
[Authorize]
public async Task<IActionResult> Create(CreateProductDto dto)
```

El rol administrador guardado en la base de datos y enviado en el claim es exactamente `Admin`. Para requerirlo:

```csharp
[Authorize(Roles = "Admin")]
```

Si el token es valido pero el usuario no tiene el rol requerido, la respuesta esperada es `403 Forbidden`.

## 6. Servicio de usuario actual

Interfaz:

```csharp
public interface ICurrentUserService
{
    int? CurrentUserId { get; }
    string? CurrentUsername { get; }
    string? CurrentRole { get; }
    bool IsAuthenticated { get; }
}
```

Implementacion:

```csharp
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public int? CurrentUserId => // UserId, NameIdentifier o sub
    public string? CurrentUsername => // ClaimTypes.Name o username
    public string? CurrentRole => // ClaimTypes.Role
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;
}
```

Este servicio evita repetir en controladores o servicios:

```csharp
User.FindFirst(...)
```

## 7. Auditoria basada en usuario autenticado

Regla general:

- El frontend no debe enviar `CreatedBy`, `UpdatedBy`, `CreatedAt` ni `UpdatedAt`.
- La API debe establecer esos valores usando `ICurrentUserService` y la hora del servidor.
- Los DTOs de entrada no deben incluir campos de auditoria.
- Los DTOs de salida si pueden exponer campos de auditoria cuando sean utiles para la UI.

Ejemplo actual en Products:

```csharp
if (!_currentUserService.IsAuthenticated || _currentUserService.CurrentUserId is null)
    return ServiceResult<ProductDto>.Fail("No se pudo identificar el usuario autenticado");

var created = await _productRepository.CreateAsync(
    dto,
    _currentUserService.CurrentUserId.Value);
```

El repositorio asigna:

```csharp
CreatedBy = createdBy,
CreatedAt = DateTime.UtcNow
```

## 8. Estado actual por modulo

| Modulo | Estado |
|---|---|
| Auth/Login | Emite JWT con `UserId`, username y roles. |
| Program.cs | Configura autenticacion, autorizacion e `IHttpContextAccessor`. |
| Products | Mutaciones protegidas con `[Authorize]`; create usa usuario autenticado para `CreatedBy`. |
| Customers | Pendiente de migrar al patron. |
| Restock | Pendiente de migrar al patron. |
| Sales | Pendiente de migrar al patron. |
| Inventory Movements | Pendiente de migrar al patron. |
| Orders | Pendiente de migrar al patron. |

## 9. Endpoints protegidos actualmente en Products

| Endpoint | Requiere JWT | Roles requeridos |
|---|---:|---|
| `GET /api/Products` | No | Ninguno |
| `GET /api/Products/catalog` | No | Ninguno |
| `GET /api/Products/{id}` | No | Ninguno |
| `POST /api/Uploads/products` | Si | `Admin` |
| `POST /api/Products` | Si | Ninguno |
| `PUT /api/Products/{id}` | Si | Ninguno |
| `PATCH /api/Products/{id}` | Si | Ninguno |
| `DELETE /api/Products/{id}` | Si | Ninguno |

## 10. Integracion Front-end

Para endpoints protegidos:

```ts
const token = getStoredToken();

await fetch("/api/Products", {
  method: "POST",
  headers: {
    "Content-Type": "application/json",
    "Authorization": `Bearer ${token}`
  },
  body: JSON.stringify(payload)
});
```

El body de productos no debe incluir `createdBy`:

```json
{
  "linePresentationId": 1,
  "flavorId": 2,
  "productName": "Helado Fresa 1L",
  "imageUrl": "/uploads/products/imagen.png",
  "minimumStock": 10
}
```

## 11. Respuestas esperadas de seguridad

| Caso | Codigo | Explicacion |
|---|---:|---|
| Sin header `Authorization` en endpoint protegido | 401 | `ApiResponse` JSON en español. |
| Token expirado, firma, issuer o audience invalidos | 401 | `ApiResponse` JSON en español. |
| Token valido sin el rol requerido | 403 | `ApiResponse` JSON en español. |

Ejemplo de `401 Unauthorized`:

```json
{
  "success": false,
  "message": "No autorizado. Se requiere autenticación.",
  "data": null
}
```

Ejemplo de `403 Forbidden`:

```json
{
  "success": false,
  "message": "No tiene permisos para realizar esta acción.",
  "data": null
}
```

## 12. Patron para expandir a otros modulos

Para migrar un modulo:

1. Agregar `[Authorize]` a las acciones que modifican estado.
2. Quitar campos de auditoria de los DTOs de entrada.
3. Inyectar `ICurrentUserService` en el servicio de aplicacion.
4. Validar que `IsAuthenticated` sea `true` y `CurrentUserId` tenga valor.
5. Pasar `CurrentUserId.Value` al repositorio o asignarlo en la entidad.
6. Usar hora del servidor para `CreatedAt`/`UpdatedAt`.
7. Mantener campos de auditoria en DTOs de respuesta si el frontend necesita mostrarlos.

Ejemplo de constructor:

```csharp
private readonly ICurrentUserService _currentUserService;

public RestockService(..., ICurrentUserService currentUserService)
{
    _currentUserService = currentUserService;
}
```

Ejemplo en create:

```csharp
if (!_currentUserService.IsAuthenticated || _currentUserService.CurrentUserId is null)
    return ServiceResult<T>.Fail("No se pudo identificar el usuario autenticado");

entity.CreatedBy = _currentUserService.CurrentUserId.Value;
entity.CreatedAt = DateTime.UtcNow;
```

## 13. Consideraciones pendientes

- Definir roles o politicas por operacion cuando el negocio lo requiera.
- Migrar gradualmente `Customers`, `Restock`, `Sales`, `Inventory Movements` y `Orders`.
- Si se agregan `UpdatedBy`/`UpdatedAt` a Products en base de datos, poblarlos con el mismo servicio.
- Considerar una abstraccion comun para entidades auditables si varios modelos comparten `CreatedBy`, `CreatedAt`, `UpdatedBy` y `UpdatedAt`.

## 14. Contratos actuales de Auth y Users

### `GET /api/Auth/me`

Requiere `Authorization: Bearer {token}`. Retorna el usuario autenticado en `ApiResponse<UserDto>` y `roles` contiene los roles reales cargados de `UserRoles` y `Role`.

```json
{
  "success": true,
  "message": "Operación exitosa",
  "data": {
    "id": 1,
    "userName": "marcelo",
    "email": "correo-del-usuario",
    "idNumber": "cedula-del-usuario",
    "firstName": "Marcelo",
    "lastName": "Apellido",
    "isActive": true,
    "roles": ["Admin"]
  }
}
```

### `POST /api/Auth/change-password`

Requiere cualquier JWT valido. Body:

```json
{
  "currentPassword": "contraseña actual",
  "newPassword": "nueva contraseña de 8 caracteres o más"
}
```

La API verifica la contraseña actual con BCrypt y devuelve `ApiResponse<string>`. El cambio de contraseña **no invalida los JWT emitidos anteriormente**; esos tokens siguen vigentes hasta su expiracion. La app debe borrar la sesion y volver a login despues del cambio.

Respuesta exitosa:

```json
{
  "success": true,
  "message": "Contraseña actualizada exitosamente.",
  "data": "Contraseña actualizada exitosamente."
}
```

### `GET /api/Users`

Requiere rol `Admin`. Acepta `page` (default `1`) y `pageSize` (default `10`, maximo `50`). La respuesta es `ApiResponse<PagedResponse<UserDto>>`:

```http
GET /api/Users?page=1&pageSize=10
Authorization: Bearer {token}
```

La forma de `data` es el objeto de paginacion con `data`, `currentPage`, `pageSize`, `totalRecords`, `totalPages`, `hasNextPage` y `hasPreviousPage`.

```json
{
  "success": true,
  "message": "Operación exitosa",
  "data": {
    "data": [
      {
        "id": 1,
        "userName": "marcelo",
        "email": "correo-del-usuario",
        "idNumber": "cedula-del-usuario",
        "firstName": "Marcelo",
        "lastName": "Apellido",
        "isActive": true,
        "roles": ["Admin"]
      }
    ],
    "currentPage": 1,
    "pageSize": 10,
    "totalRecords": 1,
    "totalPages": 1,
    "hasNextPage": false,
    "hasPreviousPage": false
  }
}
```

### Permisos de Users, Roles y Uploads

| Endpoint | Acceso |
|---|---|
| `GET /api/Users`, `GET /api/Users/{id}`, `POST /api/Users`, `PUT /api/Users/{id}`, `PATCH /api/Users/{id}/toggle-status` | JWT con rol `Admin` |
| `PATCH /api/Users/{id}/reset-password` | JWT con rol `Admin` |
| `POST /api/Users/forgot-password` | Anonimo |
| Todos los endpoints de `/api/Roles` | JWT con rol `Admin` |
| `POST /api/Uploads/products` | JWT con rol `Admin` |

`PATCH /api/Users/{id}/reset-password` requiere el rol `Admin`; recibe `userId` y `newPassword` y verifica que `userId` coincida con el `{id}` de la ruta. No exige el token de reseteo. Existe una sobrecarga de servicio que verifica token, pero este endpoint no la utiliza.

## 15. Otros endpoints que siguen publicos

Estos endpoints permanecen sin `[Authorize]` en sus controladores; se documentan para reflejar el acceso actual.

| Controlador | Endpoints sin autenticacion |
|---|---|
| `Auth` | `POST /api/Auth/login` |
| `Users` | `POST /api/Users/forgot-password` |
| `BusinessProfile` | `GET /api/BusinessProfile`, `POST /api/BusinessProfile` |
| `Customers` | `GET /api/Customers`, `GET /api/Customers/stats`, `GET /api/Customers/{id}`, `POST /api/Customers`, `PUT /api/Customers/{id}`, `DELETE /api/Customers/{id}` |
| `Departments` | `GET /api/Departments`, `GET /api/Departments/{id}` |
| `Flavors` | `GET /api/Flavors`, `GET /api/Flavors/{id}`, `POST /api/Flavors`, `PUT /api/Flavors/{id}`, `DELETE /api/Flavors/{id}` |
| `GeneralPrices` | `GET /api/GeneralPrices/general`, `POST /api/GeneralPrices/general`, `PUT /api/GeneralPrices/general/{linePresentationId}`, `GET /api/GeneralPrices/general/current`, `GET /api/GeneralPrices/general/history` |
| `Inventory` | `GET /api/Inventory/stats`, `GET /api/Inventory`, `GET /api/Inventory/products`, `GET /api/Inventory/products/{productId}/batches`, `GET /api/Inventory/batches/{batchId}/detail` |
| `InventoryMovements` | `GET /api/InventoryMovements/stats`, `GET /api/InventoryMovements`, `GET /api/InventoryMovements/{id}`, `GET /api/InventoryMovements/{id}/details`, `POST /api/InventoryMovements/transfer`, `POST /api/InventoryMovements/positive-adjustment`, `POST /api/InventoryMovements/negative-adjustment` |
| `LinePresentations` | `GET /api/LinePresentations`, `GET /api/LinePresentations/{id}`, `POST /api/LinePresentations`, `DELETE /api/LinePresentations/{id}` |
| `Lines` | `GET /api/Lines`, `GET /api/Lines/{id}`, `POST /api/Lines`, `PUT /api/Lines/{id}`, `DELETE /api/Lines/{id}` |
| `Municipalities` | `GET /api/Municipalities`, `GET /api/Municipalities/by-department/{departmentId}`, `GET /api/Municipalities/{id}` |
| `Orders` | `GET /api/Orders`, `GET /api/Orders/{id}` |
| `Presentations` | `GET /api/Presentations`, `GET /api/Presentations/{id}`, `POST /api/Presentations`, `PUT /api/Presentations/{id}`, `DELETE /api/Presentations/{id}` |
| `Prices` | `GET /api/prices/statistics` |
| `Products` | `GET /api/Products`, `GET /api/Products/catalog`, `GET /api/Products/by-line-presentation`, `GET /api/Products/stats`, `GET /api/Products/{id}` |
| `Restocks` | `GET /api/Restocks`, `GET /api/Restocks/{id}/detail`, `GET /api/Restocks/statistics` |
| `Sales` | `GET /api/Sales/stats`, `GET /api/Sales`, `GET /api/Sales/{id}`, `GET /api/Sales/{id}/details`, `GET /api/Sales/{id}/payments` |
| `Test` | `GET /api/Test/fail` |

`GET /api/Inventory/batches/expiring` no está en esta lista: requiere cualquier JWT valido. Los endpoints `GET /api/Auth/me` y `POST /api/Auth/change-password` requieren JWT; los endpoints de escritura de Products, Orders, Sales y Restocks mantienen su `[Authorize]` actual.

### Excepciones al contenedor `ApiResponse<T>`

- `GET /api/BusinessProfile` devuelve el DTO directamente; `POST /api/BusinessProfile` devuelve un objeto con `success`, `message` y `logoUrl`.
- `PATCH /api/Users/{id}/toggle-status` devuelve un objeto anonimo, no `ApiResponse<T>`.
- `PATCH /api/Users/{id}/reset-password` devuelve un objeto anonimo, no `ApiResponse<T>`.

## 16. Arranque para la app movil en Development

El perfil `http` normal escucha en `localhost:5177`. Para el emulador Android usa:

```text
http://10.0.2.2:5177
```

Arranque normal:

```powershell
dotnet run --project HerreraSystemAPI --launch-profile http
```

El perfil adicional `android-lan` escucha en `http://0.0.0.0:5177` y es solo para un celular fisico conectado a la misma red que la computadora. Arrancalo con:

```powershell
dotnet run --project HerreraSystemAPI --launch-profile android-lan
```

En el celular usa `http://<IP-LAN-de-la-computadora>:5177`. Para ese perfil, abre el puerto TCP `5177` en el firewall de Windows. En Development la API no aplica redireccion HTTPS.
