namespace HerreraSystem.Application.DTOs.LogDtos;

public class LogPageResult
{
    public IReadOnlyList<LogListItemDto> Items { get; init; } = [];
    public int TotalRecords { get; init; }
}
