using System.Text;
using HerreraSystem.Application.DTOs.Auth;
using HerreraSystem.Application.Interfaces.Services;
using HerreraSystem.Application.Services;
using HerreraSystem.Infrastructure.Data;
using HerreraSystem.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace HerreraSystem.Tests;

[TestFixture]
public class AuthChangePasswordTests
{
    private const string InitialPassword = "InitialTest123!";
    private HerreraSystemContext _context = null!;
    private AuthService _service = null!;
    private int _userId;

    private sealed class FakeConfiguration : IConfiguration
    {
        private readonly Dictionary<string, string?> _values = new();

        public string? this[string key]
        {
            get => _values.TryGetValue(key, out var value) ? value : null;
            set => _values[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();
        public IChangeToken GetReloadToken() => throw new NotImplementedException();
        public IConfigurationSection GetSection(string key) => throw new NotImplementedException();
    }

    private sealed class FakeCurrentUserService(int? userId) : ICurrentUserService
    {
        public int? CurrentUserId => userId;
        public string? CurrentUsername => "change_password_test";
        public string? CurrentRole => "Admin";
        public bool IsAuthenticated => userId.HasValue;
    }

    [SetUp]
    public async Task SetUp()
    {
        var options = new DbContextOptionsBuilder<HerreraSystemContext>()
            .UseSqlServer(
                "Server=.;Database=HerreraSystem;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        _context = new HerreraSystemContext(options);
        var user = new HerreraSystem.Domain.Entities.User
        {
            UserName = $"codex_change_pwd_{Guid.NewGuid():N}",
            Email = $"codex_change_pwd_{Guid.NewGuid():N}@example.test",
            IdNumber = Guid.NewGuid().ToString("N"),
            FirstName = "Prueba",
            LastName = "Cambio",
            PasswordHash = Encoding.UTF8.GetBytes(BCrypt.Net.BCrypt.HashPassword(InitialPassword)),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        _userId = user.Id;
        _service = new AuthService(
            new UserRepository(_context),
            new FakeConfiguration(),
            new FakeCurrentUserService(_userId));
    }

    [TearDown]
    public async Task TearDown()
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == _userId);
        if (user is not null)
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }

        await _context.DisposeAsync();
    }

    [Test]
    public async Task ChangePasswordAsync_CambiaLaContrasenaDelUsuarioAutenticado()
    {
        var result = await _service.ChangePasswordAsync(new ChangePasswordDto
        {
            CurrentPassword = InitialPassword,
            NewPassword = "NewPassword123!"
        });

        var stored = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == _userId);
        var hash = Encoding.UTF8.GetString(stored.PasswordHash);

        Assert.That(result.Success, Is.True);
        Assert.That(BCrypt.Net.BCrypt.Verify("NewPassword123!", hash), Is.True);
        Assert.That(BCrypt.Net.BCrypt.Verify(InitialPassword, hash), Is.False);
    }

    [Test]
    public async Task ChangePasswordAsync_RechazaLaContrasenaActualIncorrecta()
    {
        var result = await _service.ChangePasswordAsync(new ChangePasswordDto
        {
            CurrentPassword = "WrongPassword123!",
            NewPassword = "NewPassword123!"
        });

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Is.EqualTo("La contraseña actual es incorrecta."));
    }

    [Test]
    public async Task ChangePasswordAsync_RechazaNuevaContrasenaMenorA8Caracteres()
    {
        var result = await _service.ChangePasswordAsync(new ChangePasswordDto
        {
            CurrentPassword = InitialPassword,
            NewPassword = "1234567"
        });

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("8 caracteres"));
    }
}
