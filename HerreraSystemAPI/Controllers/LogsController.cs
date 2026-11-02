using HerreraSystem.Application.Common;
using HerreraSystem.Application.DTOs.LogDtos;
using HerreraSystem.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HerreraSystem.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class LogsController : ControllerBase
{
    private readonly ILogService _logService;

    public LogsController(ILogService logService) => _logService = logService;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResponse<LogListItemDto>>>> GetPaged([FromQuery] LogQueryParams query)
    {
        var result = await _logService.GetPagedAsync(query);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);
        var totalPages = (int)Math.Ceiling(result.TotalRecords / (double)pageSize);
        var response = new PagedResponse<LogListItemDto>
        {
            Data = result.Items,
            CurrentPage = page,
            PageSize = pageSize,
            TotalRecords = result.TotalRecords,
            TotalPages = totalPages
        };
        return Ok(ApiResponse<PagedResponse<LogListItemDto>>.Ok(response));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<LogDetailDto>>> GetById(string id)
    {
        var log = await _logService.GetByIdAsync(id);
        if (log is null)
            return NotFound(ApiResponse<LogDetailDto>.Fail("Log no encontrado."));
        return Ok(ApiResponse<LogDetailDto>.Ok(log));
    }
}
