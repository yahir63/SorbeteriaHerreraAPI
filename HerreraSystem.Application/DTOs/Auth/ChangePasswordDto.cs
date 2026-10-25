using System.ComponentModel.DataAnnotations;

namespace HerreraSystem.Application.DTOs.Auth;

public class ChangePasswordDto
{
    [Required(ErrorMessage = "La contraseña actual es requerida.")]
    public string CurrentPassword { get; set; } = null!;

    [Required(ErrorMessage = "La nueva contraseña es requerida.")]
    [MinLength(8, ErrorMessage = "La nueva contraseña debe tener al menos 8 caracteres.")]
    public string NewPassword { get; set; } = null!;
}
