
using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using HerreraSystem.API.Models;
using HerreraSystem.API.Services;
using Microsoft.AspNetCore.WebUtilities;

namespace HerreraSystem.API.Middleware;

public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _env;

    public AuditLoggingMiddleware(
        RequestDelegate next,
        IHostEnvironment env)
    {
        _next = next;
        _env = env;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ILogService logService)
    {
        var timer = Stopwatch.StartNew();
        var traceId = context.TraceIdentifier;

        // 1. Permitir la lectura múltiple del cuerpo de la solicitud.
        context.Request.EnableBuffering();

        string requestBody = string.Empty;

        if (context.Request.ContentLength > 0 &&
            context.Request.Body.CanRead)
        {
            using var reader = new StreamReader(
                context.Request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);

            requestBody = await reader.ReadToEndAsync();

            // Restaurar la posición para que el controlador
            // pueda leer el cuerpo normalmente.
            context.Request.Body.Position = 0;
        }

        // 2. Sanitizar datos sensibles del cuerpo.
        requestBody = SanitizarPayload(requestBody);

        Exception? caughtException = null;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            caughtException = ex;

            context.Response.StatusCode =
                StatusCodes.Status500InternalServerError;

            context.Response.ContentType = "application/json";

            if (!context.Response.HasStarted)
            {
                await context.Response.WriteAsync(
                    $"{{\"error\":\"Error interno en el servidor.\", " +
                    $"\"traceId\":\"{traceId}\"}}");
            }
        }
        finally
        {
            timer.Stop();

            // 3. Obtener información del usuario autenticado.
            var identidad = context.User?.Identity;
            var autenticado = identidad?.IsAuthenticated == true;

            var userId = autenticado
                ? context.User.FindFirstValue(
                    ClaimTypes.NameIdentifier)
                    ?? identidad?.Name
                : "Anonimo";

            var userName = autenticado
                ? context.User.FindFirstValue(ClaimTypes.Name)
                    ?? identidad?.Name
                : null;

            var rol = autenticado
                ? context.User.FindFirstValue(ClaimTypes.Role)
                : null;

            // 4. Obtener el estado HTTP y los datos de la solicitud.
            var statusCode = context.Response.StatusCode;

            var log = new SystemLog
            {
                TraceId = traceId,

                Timestamp = DateTime.UtcNow,

                Environment = _env.EnvironmentName,

                Level = caughtException != null
                    ? "Error"
                    : statusCode >= 500
                        ? "Error"
                        : statusCode >= 400
                            ? "Warning"
                            : "Information",

                ClientIp = context.Connection
                    .RemoteIpAddress?.ToString(),

                UserId = userId,

                UserName = userName,

                Rol = rol,

                Device = context.Request.Headers
                    .UserAgent.ToString(),

                Request = new RequestDetail
                {
                    Method = context.Request.Method,

                    Path = context.Request.Path,

                    QueryString = context.Request.QueryString.HasValue
                        ? context.Request.QueryString.Value
                        : null,

                    Payload = string.IsNullOrWhiteSpace(requestBody)
                        ? null
                        : requestBody
                },

                Response = new ResponseDetail
                {
                    StatusCode = statusCode,

                    Status = ReasonPhrases.GetReasonPhrase(
                        statusCode),

                    ElapsedMilliseconds = timer.ElapsedMilliseconds
                }
            };

            // 5. Registrar los detalles de la excepción, si existe.
            if (caughtException != null)
            {
                log.Exception = new ExceptionDetail
                {
                    Message = caughtException.Message,

                    StackTrace = caughtException.StackTrace,

                    Source = caughtException.Source
                        ?? string.Empty
                };
            }

            // 6. Guardar el registro de auditoría.
            _ = logService.InsertLogAsync(log);
        }
    }

    private static string SanitizarPayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return payload;
        }

        // Ocultar contraseñas, tokens, claves y secretos
        // enviados como valores de propiedades JSON.
        var pattern =
            @"(?i)""(password|contraseña|token|secret|clave)""\s*:\s*""([^""]+)""";

        return Regex.Replace(
            payload,
            pattern,
            "\"$1\": \"******\"");
    }
}
