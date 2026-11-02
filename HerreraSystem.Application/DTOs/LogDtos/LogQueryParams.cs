using HerreraSystem.Application.Common;

namespace HerreraSystem.Application.DTOs.LogDtos;

public class LogQueryParams : PaginationParams
{
    public string? Modulo { get; set; }
    public string? Method { get; set; }
    public string? UserId { get; set; }
    public bool? Success { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
