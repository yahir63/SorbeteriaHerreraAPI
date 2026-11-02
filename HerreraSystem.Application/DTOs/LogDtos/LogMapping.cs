using System.Text.Json;
using System.Text.Json.Nodes;

namespace HerreraSystem.Application.DTOs.LogDtos;

public static class LogMapping
{
    private static readonly Dictionary<string, string> Modulos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Auth"] = "Autenticación", ["Users"] = "Usuarios", ["Roles"] = "Roles",
        ["Customers"] = "Clientes", ["Products"] = "Productos", ["Flavors"] = "Sabores",
        ["Lines"] = "Líneas", ["Presentations"] = "Presentaciones", ["Orders"] = "Pedidos",
        ["Sales"] = "Ventas", ["Restocks"] = "Reabastecimientos", ["Inventory"] = "Inventario",
        ["InventoryMovements"] = "Movimientos", ["Uploads"] = "Archivos", ["BusinessProfile"] = "Negocio"
    };

    public static string DeriveModule(string? path)
    {
        var segments = (path ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || !segments[0].Equals("api", StringComparison.OrdinalIgnoreCase)) return string.Empty;
        return Modulos.TryGetValue(segments[1], out var translated) ? translated : segments[1];
    }

    public static string DeriveType(string? method, string? path)
    {
        var verb = method ?? string.Empty;
        var route = path ?? string.Empty;
        if (route.TrimEnd('/').EndsWith("/login", StringComparison.OrdinalIgnoreCase)) return "Inicio de sesión";
        if (verb.Equals("PATCH", StringComparison.OrdinalIgnoreCase) && route.TrimEnd('/').EndsWith("/toggle-status", StringComparison.OrdinalIgnoreCase)) return "Cambio de estado";
        return verb.ToUpperInvariant() switch
        {
            "POST" => "Creación", "PUT" or "PATCH" => "Edición", "DELETE" => "Eliminación",
            "GET" => "Consulta", _ => verb
        };
    }

    public static bool IsSuccess(int statusCode) => statusCode < 400;

    public static LogListItemDto CreateListItem(string? id, DateTime timestamp, string? level,
        string? userId, string? userName, string? rol, string? method, string? path,
        int statusCode, long elapsedMilliseconds)
    {
        return new LogListItemDto
        {
            Id = id ?? string.Empty,
            Timestamp = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc),
            Level = level ?? string.Empty,
            UserId = userId ?? string.Empty,
            UserName = userName ?? string.Empty,
            Rol = rol ?? string.Empty,
            Method = method ?? string.Empty,
            Path = path ?? string.Empty,
            Modulo = DeriveModule(path),
            Tipo = DeriveType(method, path),
            StatusCode = statusCode,
            Success = IsSuccess(statusCode),
            ElapsedMilliseconds = elapsedMilliseconds
        };
    }

    public static string SanitizePayload(string? payload)
    {
        if (string.IsNullOrEmpty(payload)) return string.Empty;
        try
        {
            var node = JsonNode.Parse(payload);
            MaskSensitiveValues(node);
            return node?.ToJsonString() ?? payload;
        }
        catch (JsonException)
        {
            return payload.Length <= 2000 ? payload : payload[..2000];
        }
    }

    public static string TruncateException(string? exception) =>
        string.IsNullOrEmpty(exception) || exception.Length <= 2000 ? exception ?? string.Empty : exception[..2000];

    public static (DateTime? FromUtc, DateTime? ToUtcExclusive) GetUtcDateBounds(DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate.HasValue
            ? DateTime.SpecifyKind(fromDate.Value.Date.AddHours(6), DateTimeKind.Utc)
            : (DateTime?)null;
        var to = toDate.HasValue
            ? DateTime.SpecifyKind(toDate.Value.Date.AddDays(1).AddHours(6), DateTimeKind.Utc)
            : (DateTime?)null;
        return (from, to);
    }

    private static void MaskSensitiveValues(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                if (property.Key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                    property.Key.Contains("contraseña", StringComparison.OrdinalIgnoreCase) ||
                    property.Key.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                    property.Key.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                    property.Key.Contains("clave", StringComparison.OrdinalIgnoreCase))
                    obj[property.Key] = "******";
                else
                    MaskSensitiveValues(property.Value);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array) MaskSensitiveValues(child);
        }
    }
}
