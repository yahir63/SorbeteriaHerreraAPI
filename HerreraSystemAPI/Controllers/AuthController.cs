using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HerreraSystem.Application.Common;
using HerreraSystem.Application.DTOs.Auth;
using HerreraSystem.Application.DTOs.UserDto;
using HerreraSystem.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HerreraSystem.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;

    public AuthController(IAuthService authService, IUserService userService)
    {
        _authService = authService;
        _userService = userService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        var result = await _authService.LoginAsync(dto);

        if (!result.Success)
            return Unauthorized(ApiResponse<string>.Fail(result.ErrorMessage!));

        return Ok(ApiResponse<AuthResponseDto>.Ok(result.Data!));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)
                       ?? User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            return Unauthorized(ApiResponse<string>.Fail("Sesión inválida."));

        var result = await _userService.GetByIdAsync(userId);
        if (!result.Success)
            return NotFound(ApiResponse<string>.Fail(result.ErrorMessage!));

        return Ok(ApiResponse<UserDto>.Ok(result.Data!));
    }
}