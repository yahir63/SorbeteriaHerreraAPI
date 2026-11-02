namespace HerreraSystem.Application.DTOs.LogDtos;

public class LogListItemDto
{
    public string Id { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Modulo { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public bool Success { get; set; }
    public long ElapsedMilliseconds { get; set; }
}
