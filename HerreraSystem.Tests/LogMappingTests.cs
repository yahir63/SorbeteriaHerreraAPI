using HerreraSystem.Application.DTOs.LogDtos;

namespace HerreraSystem.Tests;

[TestFixture]
public class LogMappingTests
{
    [TestCase("/api/Auth/login", "Autenticación")]
    [TestCase("/api/InventoryMovements/1", "Movimientos")]
    [TestCase("/api/Other/1", "Other")]
    public void DeriveModule_MapsKnownAndUnknownControllers(string path, string expected)
    {
        Assert.That(LogMapping.DeriveModule(path), Is.EqualTo(expected));
    }

    [TestCase("POST", "/api/Auth/login", "Inicio de sesión")]
    [TestCase("PATCH", "/api/Products/1/toggle-status", "Cambio de estado")]
    [TestCase("PATCH", "/api/Products/1", "Edición")]
    [TestCase("GET", "/api/Products", "Consulta")]
    public void DeriveType_MapsMethodAndRoute(string method, string path, string expected)
    {
        Assert.That(LogMapping.DeriveType(method, path), Is.EqualTo(expected));
    }

    [Test]
    public void CreateListItem_UsesUtcAndDerivesSuccess()
    {
        var item = LogMapping.CreateListItem("id", new DateTime(2026, 1, 1), "Warning", "1", null, null, "GET", "/api/Products", 399, 12);
        Assert.That(item.Timestamp.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(item.Success, Is.True);
        Assert.That(LogMapping.IsSuccess(400), Is.False);
    }

    [Test]
    public void SanitizePayload_MasksSensitiveKeysRecursively()
    {
        const string json = "{\"password\":\"a\",\"currentPassword\":\"b\",\"newPassword\":\"c\",\"tokenValue\":\"d\",\"child\":{\"secretKey\":\"e\"}}";
        var result = LogMapping.SanitizePayload(json);
        Assert.That(result, Does.Not.Contain("\"a\""));
        Assert.That(result, Does.Not.Contain("\"b\""));
        Assert.That(result, Does.Not.Contain("\"c\""));
        Assert.That(result, Does.Not.Contain("\"d\""));
        Assert.That(result, Does.Not.Contain("\"e\""));
        Assert.That(result, Does.Contain("******"));
    }

    [Test]
    public void SanitizePayload_TruncatesInvalidJsonAndLeavesShortText()
    {
        Assert.That(LogMapping.SanitizePayload("plain"), Is.EqualTo("plain"));
        Assert.That(LogMapping.SanitizePayload(new string('x', 2001)), Has.Length.EqualTo(2000));
    }

    [Test]
    public void GetUtcDateBounds_UsesManaguaCalendarDatesAsUtc()
    {
        var bounds = LogMapping.GetUtcDateBounds(new DateTime(2026, 10, 30, 18, 20, 0), new DateTime(2026, 10, 31, 3, 0, 0));
        Assert.That(bounds.FromUtc, Is.EqualTo(new DateTime(2026, 10, 30, 6, 0, 0, DateTimeKind.Utc)));
        Assert.That(bounds.ToUtcExclusive, Is.EqualTo(new DateTime(2026, 11, 1, 6, 0, 0, DateTimeKind.Utc)));
    }
}
