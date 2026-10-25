using HerreraSystem.Application.Common;
using HerreraSystem.Application.DTOs.Auth;
using HerreraSystem.Application.DTOs.InventoryMovementDtos;
using HerreraSystem.Application.DTOs.RestockDtos;
using HerreraSystem.Application.Interfaces.Services;
using HerreraSystem.Application.Services;
using HerreraSystem.Domain.Entities;
using HerreraSystem.Infrastructure.Data;
using HerreraSystem.Infrastructure.Persistence;
using HerreraSystem.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HerreraSystem.Tests
{
    [TestFixture]
    public class NonRegressionTests
    {
        private HerreraSystemContext _context = null!;
        private UnitOfWork _unitOfWork = null!;
        private RestockService _restockService = null!;
        private InventoryMovementService _inventoryMovementService = null!;
        private AuthService _authService = null!;
        private int _createdRestockId = 0;
        private List<int> _createdMovementIds = new();
        private Batch _testBatch = null!;
        private BatchLocation _bodegaBatchLocation = null!;
        private int _initialBodegaStock = 30;

        private class FakeCurrentUserService : ICurrentUserService
        {
            public int? CurrentUserId => 1;
            public string? CurrentUsername => "Admin";
            public string? CurrentRole => "Admin";
            public bool IsAuthenticated => true;
        }

        private class FakeConfiguration : IConfiguration
        {
            private readonly Dictionary<string, string> _values = new()
            {
                { "Jwt:Key", "ClaveSecretaSuperSeguraParaTestsDeJWT123456789!" },
                { "Jwt:Issuer", "HerreraSystem" },
                { "Jwt:Audience", "HerreraSystemAudience" },
                { "Jwt:ExpiresInMinutes", "60" }
            };

            public string? this[string key]
            {
                get => _values.TryGetValue(key, out var v) ? v : null;
                set => _values[key] = value!;
            }

            public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();
            public IChangeToken GetReloadToken() => throw new NotImplementedException();
            public IConfigurationSection GetSection(string key) => throw new NotImplementedException();
        }

        [SetUp]
        public async Task SetUp()
        {
            var connectionString = "Server=.;" +
                                   "Database=HerreraSystem;" +
                                   "Trusted_Connection=True;" +
                                   "TrustServerCertificate=True;";

            var options = new DbContextOptionsBuilder<HerreraSystemContext>()
                .UseSqlServer(connectionString)
                .Options;

            _context = new HerreraSystemContext(options);
            _unitOfWork = new UnitOfWork(_context);

            var dateTimeService = new NicaraguaDateTimeService();
            var productRepo = new ProductRepository(_context, dateTimeService);
            var restockRepo = new RestockRepository(_context, dateTimeService);
            var batchRepo = new BatchRepository(_context);
            var batchLocationRepo = new BatchLocationRepository(_context);
            var movementRepo = new InventoryMovementRepository(_context, dateTimeService);
            var movementDetailRepo = new MovementDetailRepository(_context);
            var userRepo = new UserRepository(_context);
            var currentUserService = new FakeCurrentUserService();
            var config = new FakeConfiguration();

            _restockService = new RestockService(
                _unitOfWork,
                productRepo,
                restockRepo,
                batchRepo,
                batchLocationRepo,
                movementRepo,
                movementDetailRepo,
                currentUserService,
                dateTimeService);

            _inventoryMovementService = new InventoryMovementService(
                _unitOfWork,
                batchRepo,
                batchLocationRepo,
                movementRepo,
                movementDetailRepo,
                dateTimeService);

            _authService = new AuthService(userRepo, config, currentUserService);

            // Obtener lote de prueba activo para producto 1
            var batch = await _context.Batches
                .FirstOrDefaultAsync(b => b.ProductId == 1 && b.BatchStatusId == 1);

            if (batch == null)
            {
                Assert.Inconclusive("No se encontró lote activo para Producto 1 en BD.");
                return;
            }

            _testBatch = batch;

            var bl = await _context.BatchLocations
                .FirstOrDefaultAsync(l => l.BatchId == batch.Id && l.LocationId == 1);

            if (bl == null)
            {
                bl = new BatchLocation
                {
                    BatchId = batch.Id,
                    LocationId = 1,
                    CurrentStock = _initialBodegaStock
                };
                _context.BatchLocations.Add(bl);
                await _context.SaveChangesAsync();
            }
            else
            {
                bl.CurrentStock = _initialBodegaStock;
                await _context.SaveChangesAsync();
            }

            _bodegaBatchLocation = bl;
        }

        [TearDown]
        public async Task TearDown()
        {
            if (_createdRestockId > 0)
            {
                var batches = await _context.Batches.Where(b => b.RestockId == _createdRestockId).ToListAsync();
                var batchIds = batches.Select(b => b.Id).ToList();

                if (batchIds.Any())
                {
                    var movDetails = await _context.MovementDetails.Where(md => batchIds.Contains(md.BatchId)).ToListAsync();
                    var movIds = movDetails.Select(md => md.MovementId).Distinct().ToList();
                    _context.MovementDetails.RemoveRange(movDetails);

                    var movs = await _context.InventoryMovements.Where(m => movIds.Contains(m.Id)).ToListAsync();
                    _context.InventoryMovements.RemoveRange(movs);

                    var locs = await _context.BatchLocations.Where(bl => batchIds.Contains(bl.BatchId)).ToListAsync();
                    _context.BatchLocations.RemoveRange(locs);

                    _context.Batches.RemoveRange(batches);
                }

                var restock = await _context.Restocks.FirstOrDefaultAsync(r => r.Id == _createdRestockId);
                if (restock != null) _context.Restocks.Remove(restock);

                await _context.SaveChangesAsync();
                _createdRestockId = 0;
            }

            if (_createdMovementIds.Any())
            {
                var movs = await _context.InventoryMovements
                    .Where(m => _createdMovementIds.Contains(m.Id))
                    .ToListAsync();

                foreach (var mov in movs)
                {
                    var details = await _context.MovementDetails.Where(md => md.MovementId == mov.Id).ToListAsync();
                    _context.MovementDetails.RemoveRange(details);
                    _context.InventoryMovements.Remove(mov);
                }

                await _context.SaveChangesAsync();
                _createdMovementIds.Clear();
            }

            if (_testBatch != null)
            {
                var bl = await _context.BatchLocations.FirstOrDefaultAsync(l => l.BatchId == _testBatch.Id && l.LocationId == 1);
                if (bl != null)
                {
                    bl.CurrentStock = _initialBodegaStock;
                    await _context.SaveChangesAsync();
                }
            }

            await _context.DisposeAsync();
        }

        [Test]
        public async Task Reabastecimiento_DebeCrearRestockLotesYMovimientoTipo1Correctamente()
        {
            var dto = new CreateRestockDto
            {
                Notes = "Reabastecimiento de prueba no-regresión",
                Batches = new List<CreateRestockBatchDto>
                {
                    new CreateRestockBatchDto
                    {
                        ProductId = 1,
                        Quantity = 25,
                        UnitProductionCost = 30.00m,
                        ExpirationDate = DateOnly.FromDateTime(DateTime.Now.AddMonths(6))
                    }
                }
            };

            var result = await _restockService.CreateRestockAsync(dto);

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.Data, Is.Not.Null);
            Assert.That(result.Data!.RestockId, Is.GreaterThan(0));
            Assert.That(result.Data.Batches.Count, Is.EqualTo(1));

            _createdRestockId = result.Data.RestockId;

            // Verificar Restock en BD
            var restock = await _context.Restocks.FirstOrDefaultAsync(r => r.Id == _createdRestockId);
            Assert.That(restock, Is.Not.Null);

            // Verificar InventoryMovement tipo 1 (Reabastecimiento)
            var movement = await _context.InventoryMovements
                .Include(m => m.MovementDetails)
                .FirstOrDefaultAsync(m => m.Id == result.Data.InventoryMovementId);

            Assert.That(movement, Is.Not.Null);
            Assert.That(movement!.MovementTypeId, Is.EqualTo(1)); // Reabastecimiento
            Assert.That(movement.MovementDetails.Count, Is.EqualTo(1));
            Assert.That(movement.MovementDetails.First().DestinationLocationId, Is.EqualTo(1)); // Bodega
            Assert.That(movement.MovementDetails.First().Quantity, Is.EqualTo(25));
        }

        [Test]
        public async Task TransferenciaManual_SinOrderId_DebeFuncionarNormalmente()
        {
            int transferQty = 5;

            var dto = new CreateTransferDto
            {
                Notes = "Transferencia manual de prueba sin orden",
                CreatedBy = 1,
                OrderId = null, // Manual
                Details = new List<TransferDetailDto>
                {
                    new TransferDetailDto
                    {
                        BatchId = _testBatch.Id,
                        SourceLocationId = 1, // Bodega
                        DestinationLocationId = 2, // Mostrador
                        Quantity = transferQty
                    }
                }
            };

            var result = await _inventoryMovementService.TransferAsync(dto);

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.Data, Is.Not.Null);
            Assert.That(result.Data!.Id, Is.GreaterThan(0));

            _createdMovementIds.Add(result.Data.Id);

            var mov = await _context.InventoryMovements.FirstOrDefaultAsync(m => m.Id == result.Data.Id);
            Assert.That(mov, Is.Not.Null);
            Assert.That(mov!.MovementTypeId, Is.EqualTo(1002));
            Assert.That(mov.OrderId, Is.Null, "Una transferencia manual debe tener OrderId = null");
        }

        [Test]
        public async Task AjustePositivoYAjusteNegativo_DebenAjustarStockCorrectamente()
        {
            int ajustePosQty = 10;
            int stockInicial = _bodegaBatchLocation.CurrentStock;

            // 1. Ajuste Positivo (+10)
            var dtoPos = new CreatePositiveAdjustmentDto
            {
                Notes = "Ajuste positivo de prueba",
                CreatedBy = 1,
                Details = new List<AdjustmentDetailDto>
                {
                    new AdjustmentDetailDto
                    {
                        BatchId = _testBatch.Id,
                        LocationId = 1, // Bodega
                        Quantity = ajustePosQty
                    }
                }
            };

            var resPos = await _inventoryMovementService.PositiveAdjustmentAsync(dtoPos);
            Assert.That(resPos.Success, Is.True, resPos.ErrorMessage);
            _createdMovementIds.Add(resPos.Data!.Id);

            var blDespuesPos = await _context.BatchLocations.AsNoTracking().FirstOrDefaultAsync(l => l.BatchId == _testBatch.Id && l.LocationId == 1);
            Assert.That(blDespuesPos!.CurrentStock, Is.EqualTo(stockInicial + ajustePosQty));

            // 2. Ajuste Negativo (-5)
            int ajusteNegQty = 5;
            var dtoNeg = new CreateNegativeAdjustmentDto
            {
                Notes = "Ajuste negativo de prueba",
                CreatedBy = 1,
                Details = new List<AdjustmentDetailDto>
                {
                    new AdjustmentDetailDto
                    {
                        BatchId = _testBatch.Id,
                        LocationId = 1,
                        Quantity = ajusteNegQty
                    }
                }
            };

            var resNeg = await _inventoryMovementService.NegativeAdjustmentAsync(dtoNeg);
            Assert.That(resNeg.Success, Is.True, resNeg.ErrorMessage);
            _createdMovementIds.Add(resNeg.Data!.Id);

            var blDespuesNeg = await _context.BatchLocations.AsNoTracking().FirstOrDefaultAsync(l => l.BatchId == _testBatch.Id && l.LocationId == 1);
            Assert.That(blDespuesNeg!.CurrentStock, Is.EqualTo(stockInicial + ajustePosQty - ajusteNegQty));
        }

        [Test]
        public async Task Autenticacion_LoginAsync_DebeValidarCredencialesCorrectamente()
        {
            // Obtener usuario admin activo
            var adminUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == "nohelia_cortes" || u.IsActive == true);
            if (adminUser == null)
            {
                Assert.Inconclusive("No hay usuario activo en la BD para probar login.");
                return;
            }

            // Test de credenciales inválidas
            var failDto = new LoginRequestDto
            {
                Username = adminUser.UserName,
                Password = "PasswordIncorrecto123!"
            };

            var failResult = await _authService.LoginAsync(failDto);
            Assert.That(failResult.Success, Is.False);
            Assert.That(failResult.ErrorMessage, Does.Contain("Credenciales invalidas").IgnoreCase);
        }
    }
}
