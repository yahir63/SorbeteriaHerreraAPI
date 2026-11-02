# Módulo de logs de auditoría

Los logs se leen de la base `SystemLogsDb`, colección `AuditLogs`. Ambos endpoints requieren autenticación y el rol `Admin`. Una solicitud sin autenticación recibe HTTP 401 y una respuesta `ApiResponse`; un usuario autenticado sin el rol recibe HTTP 403 y una respuesta `ApiResponse`.

## Listar logs

`GET /api/Logs`

Parámetros opcionales de query: `page` (predeterminado 1), `pageSize` (predeterminado 10, máximo 50), `modulo`, `method`, `userId`, `success`, `fromDate` y `toDate`.

- `modulo` filtra el prefijo `/api/{modulo}` de la ruta; por ejemplo `Products`.
- `method` compara el método HTTP en mayúsculas; `userId` compara el identificador exacto.
- `success=true` incluye estados HTTP menores de 400; `success=false`, estados 400 o mayores.
- `fromDate` y `toDate` se interpretan como fechas de Managua (UTC-6 fijo), usando solo su parte de fecha. `fromDate` incluye desde las 00:00 de esa fecha y `toDate` incluye todo ese día; los límites de consulta se convierten a UTC.
- El listado omite payload, dispositivo y query string.

Ejemplo: `GET /api/Logs?page=1&pageSize=10&modulo=Products&success=true&fromDate=2026-10-01&toDate=2026-10-30`

Respuesta ilustrativa:

```json
{
  "success": true,
  "message": "Operación exitosa",
  "data": {
    "data": [
      {
        "id": "671234567890abcdef123456",
        "timestamp": "2026-10-30T16:20:00Z",
        "level": "Information",
        "userId": "1",
        "userName": "Admin",
        "rol": "Admin",
        "method": "GET",
        "path": "/api/Products",
        "modulo": "Productos",
        "tipo": "Consulta",
        "statusCode": 200,
        "success": true,
        "elapsedMilliseconds": 18
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

## Consultar un log

`GET /api/Logs/{id}` — `id` debe ser el ObjectId de Mongo como cadena. Un id inválido o inexistente responde HTTP 404 con `ApiResponse` y el mensaje `Log no encontrado.`.

Ejemplo: `GET /api/Logs/671234567890abcdef123456`

Respuesta ilustrativa:

```json
{
  "success": true,
  "message": "Operación exitosa",
  "data": {
    "id": "671234567890abcdef123456",
    "timestamp": "2026-10-30T16:20:00Z",
    "level": "Information",
    "userId": "1",
    "userName": "Admin",
    "rol": "Admin",
    "method": "POST",
    "path": "/api/Auth/change-password",
    "modulo": "Autenticación",
    "tipo": "Creación",
    "statusCode": 200,
    "success": true,
    "elapsedMilliseconds": 22,
    "traceId": "trace-example",
    "environment": "Production",
    "clientIp": "192.0.2.10",
    "device": "Flutter",
    "queryString": "",
    "payload": "{\"currentPassword\":\"******\",\"newPassword\":\"******\"}",
    "status": "OK",
    "exception": ""
  }
}
```

Las marcas de tiempo se representan en UTC ISO 8601 con `Z`. El detalle sanea recursivamente valores de claves cuyo nombre contiene `password`, `contraseña`, `token`, `secret` o `clave`; texto que no sea JSON válido se devuelve truncado a 2000 caracteres. `exception` también se limita a 2000 caracteres.

## Índice requerido para ordenar

Crear manualmente en el shell de Cosmos para la colección `AuditLogs`:

```javascript
db.AuditLogs.createIndex({ timestamp: -1 })
```
