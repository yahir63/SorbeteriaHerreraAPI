using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HerreraSystem.Application.Common;
using HerreraSystem.Application.DTOs.Auth;
using HerreraSystem.Application.Interfaces.Repositories;
using HerreraSystem.Application.Interfaces.Services;
using HerreraSystem.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace HerreraSystem.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IConfiguration _config;
    private readonly ICurrentUserService _currentUserService;

    public AuthService(
        IUserRepository userRepo,
        IConfiguration config,
        ICurrentUserService currentUserService)
    {
        _userRepo = userRepo;
        _config = config;
        _currentUserService = currentUserService;
    }

    public async Task<ServiceResult<AuthResponseDto>> LoginAsync(LoginRequestDto dto)
    {
        var user = await _userRepo.GetByUserNameAsync(dto.Username);

        if (user == null)
            return ServiceResult<AuthResponseDto>.Fail("Credenciales invalidas");

        if (user.IsActive == false)
            return ServiceResult<AuthResponseDto>.Fail("Tu cuenta ha sido desactivada. Contacta al administrador.");

        if (!VerifyPassword(dto.Password, user.PasswordHash))
            return ServiceResult<AuthResponseDto>.Fail("Credenciales invalidas");

        var roles = await _userRepo.GetUserRolesAsync(user.Id);
        var token = GenerateJwtToken(user, roles);
        var expiresAt = DateTime.UtcNow.AddMinutes(double.Parse(_config["Jwt:ExpiresInMinutes"]!));

        var response = new AuthResponseDto
        {
            Token = token,
            UserName = user.UserName,
            FirstName = user.FirstName,
            LastName = user.LastName,  
            Email = user.Email,
            Roles = roles,
            ExpiresAt = expiresAt
        };

        return ServiceResult<AuthResponseDto>.Ok(response);
    }

    public async Task<ServiceResult<string>> ChangePasswordAsync(ChangePasswordDto dto)
    {
        if (_currentUserService.CurrentUserId is not int userId)
            return ServiceResult<string>.Fail("No se pudo identificar el usuario autenticado.");

        if (string.IsNullOrEmpty(dto.NewPassword) || dto.NewPassword.Length < 8)
            return ServiceResult<string>.Fail("La nueva contraseña debe tener al menos 8 caracteres.");

        if (string.IsNullOrEmpty(dto.CurrentPassword))
            return ServiceResult<string>.Fail("La contraseña actual es requerida.");

        var user = await _userRepo.GetByIdWithRoleAsync(userId);
        if (user is null)
            return ServiceResult<string>.Fail("Usuario no encontrado.");

        if (!VerifyPassword(dto.CurrentPassword, user.PasswordHash))
            return ServiceResult<string>.Fail("La contraseña actual es incorrecta.");

        user.PasswordHash = Encoding.UTF8.GetBytes(BCrypt.Net.BCrypt.HashPassword(dto.NewPassword));
        await _userRepo.UpdateAsync(user);

        return ServiceResult<string>.Ok("Contraseña actualizada exitosamente.");
    }

    private string GenerateJwtToken(User user, List<string> roles)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("UserId", user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new("username", user.UserName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("FullName", $"{user.FirstName} {user.LastName}")
        };

        if (!string.IsNullOrEmpty(user.Email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        }

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var expires = DateTime.UtcNow.AddMinutes(double.Parse(_config["Jwt:ExpiresInMinutes"]!));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static bool VerifyPassword(string password, byte[] storedHash)
    {
        try
        {
            var hashString = Encoding.UTF8.GetString(storedHash);
            return BCrypt.Net.BCrypt.Verify(password, hashString);
        }
        catch
        {
            return false;
        }
    }
}
