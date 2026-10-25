using HerreraSystem.Application.Common;
using HerreraSystem.Application.Interfaces.Services;
using HerreraSystem.Infrastructure.Data;
using HerreraSystem.Infrastructure.Repositories;
using HerreraSystem.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace HerreraSystem.Tests;

[TestFixture]
public class InventoryExpiringBatchesTests
{
    private HerreraSystemContext _context = null!;
    private InventoryRepository _repository = null!;
    private int _restockId;
    private int _batchId;
    private int _batchLocationId;
    private string _batchCode = null!;

    [SetUp]
    public async Task SetUp()
    {
        var options = new DbContextOptionsBuilder<HerreraSystemContext>()
            .UseSqlServer(
                "Server=.;Database=HerreraSystem;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        _context = new HerreraSystemContext(options);
        var dateTimeService = new NicaraguaDateTimeService();
        _repository = new InventoryRepository(_context, dateTimeService);

        var user = await _context.Users.AsNoTracking().FirstAsync();
        var product = await _context.Products.AsNoTracking().FirstAsync();
        var status = await _context.BatchStatuses.AsNoTracking().FirstAsync();
        var location = await _context.Locations.AsNoTracking().FirstAsync();
        var uniqueCode = Guid.NewGuid().ToString("N");
        _batchCode = $"TEST-EXPIRING-{uniqueCode}";

        var restock = new HerreraSystem.Domain.Entities.Restock
        {
            RestockDate = dateTimeService.Now,
            CreatedBy = user.Id,
            RestockCode = $"TEST-RESTOCK-{uniqueCode}"
        };
        _context.Restocks.Add(restock);
        await _context.SaveChangesAsync();
        _restockId = restock.Id;

        var batch = new HerreraSystem.Domain.Entities.Batch
        {
            RestockId = _restockId,
            ProductId = product.Id,
            BatchStatusId = status.Id,
            InitialQuantity = 3,
            UnitProductionCost = 1m,
            ExpirationDate = DateOnly.FromDateTime(dateTimeService.Now).AddDays(-1),
            BatchCode = _batchCode
        };
        _context.Batches.Add(batch);
        await _context.SaveChangesAsync();
        _batchId = batch.Id;

        var batchLocation = new HerreraSystem.Domain.Entities.BatchLocation
        {
            BatchId = _batchId,
            LocationId = location.Id,
            CurrentStock = 3
        };
        _context.BatchLocations.Add(batchLocation);
        await _context.SaveChangesAsync();
        _batchLocationId = batchLocation.Id;
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_batchLocationId > 0)
        {
            var batchLocation = await _context.BatchLocations.FindAsync(_batchLocationId);
            if (batchLocation is not null)
                _context.BatchLocations.Remove(batchLocation);
        }

        if (_batchId > 0)
        {
            var batch = await _context.Batches.FindAsync(_batchId);
            if (batch is not null)
                _context.Batches.Remove(batch);
        }

        if (_restockId > 0)
        {
            var restock = await _context.Restocks.FindAsync(_restockId);
            if (restock is not null)
                _context.Restocks.Remove(restock);
        }

        await _context.SaveChangesAsync();
        await _context.DisposeAsync();
    }

    [Test]
    public async Task GetExpiringBatchesAsync_IncluyeLoteVencidoConStockYLoMarca()
    {
        var result = await _repository.GetExpiringBatchesAsync(
            15,
            _batchCode,
            null,
            null,
            new PaginationParams());

        var batch = result.Data.Single();
        Assert.That(batch.BatchId, Is.EqualTo(_batchId));
        Assert.That(batch.IsExpired, Is.True);
        Assert.That(batch.DaysToExpire, Is.EqualTo(-1));
        Assert.That(batch.TotalStock, Is.EqualTo(3));
        Assert.That(batch.StockByLocation.Sum(location => location.CurrentStock), Is.EqualTo(3));
    }
}
