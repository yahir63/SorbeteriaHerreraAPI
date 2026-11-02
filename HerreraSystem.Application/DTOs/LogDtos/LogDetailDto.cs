namespace HerreraSystem.Application.DTOs.LogDtos;

public class LogDetailDto : LogListItemDto
{
    public string TraceId { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string ClientIp { get; set; } = string.Empty;
    public string Device { get; set; } = string.Empty;
    public string QueryString { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Exception { get; set; } = string.Empty;
}
